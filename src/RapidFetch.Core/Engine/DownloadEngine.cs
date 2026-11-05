using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;
using RapidFetch.Core.Models;
using RapidFetch.Core.Services;
using RapidFetch.Core.Util;

namespace RapidFetch.Core.Engine;

public sealed record AddDownloadRequest
{
    public required string Url { get; init; }
    public string? FileName { get; init; }
    public string? SaveFolder { get; init; }
    public string? Referrer { get; init; }
    public string? UserAgent { get; init; }
    public string? Queue { get; init; }
    public int? Connections { get; init; }
    public string? Category { get; init; }
    /// <summary>Video-site quality for the yt-dlp engine: best|1080|720|480|audio.</summary>
    public string? Quality { get; init; }
    public bool StartNow { get; init; } = true;
    public bool Silent { get; init; }
}

public sealed class DownloadEngine : IDisposable
{
    private const int BufferSize = 128 * 1024;

    private readonly HttpClient _client;
    private readonly Dictionary<string, CancellationTokenSource> _cancel = new();
    private readonly Dictionary<string, int> _generation = new();
    private readonly Dictionary<string, int> _retries = new();
    private readonly Dictionary<string, long> _lastSample = new();
    private readonly Dictionary<string, long> _lastSampleBytes = new();
    private readonly object _gate = new();
    private readonly RateLimiter _limiter;
    private readonly SynchronizationContext? _sync;
    private readonly int _uiThreadId;
    private readonly System.Threading.Timer _ticker;
    private readonly System.Threading.Timer _schedulerTimer;
    private long _sessionBytes;
    private DateTime _sessionStart = DateTime.Now;
    private long _dailyBytes;
    private DateTime _dailyDate = DateTime.Today;
    private int _activeSlots;

    public ObservableCollection<DownloadItem> Items { get; } = new();
    public AppSettings Settings { get; }
    public string StatePath { get; }
    public long SessionBytes => Interlocked.Read(ref _sessionBytes);
    public long DailyBytes => Interlocked.Read(ref _dailyBytes);

    public event Action<DownloadItem>? ItemCompleted;
    public event Action<DownloadItem>? ItemFailed;
    public event Action<string>? Log;
    public event Action? Changed;
    public event Action? ExitRequested;

    public DownloadEngine(AppSettings settings, string statePath)
    {
        _sync = SynchronizationContext.Current;
        _uiThreadId = Environment.CurrentManagedThreadId;
        Settings = settings;
        StatePath = statePath;
        _client = HttpProbe.CreateClient(settings);
        _limiter = new RateLimiter(settings.SpeedLimitEnabled ? settings.SpeedLimitBytesPerSecond : 0);
        _ticker = new System.Threading.Timer(_ => Tick(), null, 500, 500);
        _schedulerTimer = new System.Threading.Timer(_ => SchedulerTick(), null, 5000, 15000);
        LoadState();
    }

    public void ApplySettings()
    {
        _limiter.Update(Settings.SpeedLimitEnabled ? Settings.SpeedLimitBytesPerSecond : 0);
    }

    // ---------------------------------------------------------------- add / probe

    public async Task<DownloadItem> AddAsync(AddDownloadRequest request, CancellationToken token = default)
    {
        var url = request.Url.Trim();

        // Video-site pages expose a player, not a file: hand them to yt-dlp.
        if (YtDlp.IsVideoSite(url))
            return await AddExternalAsync(request, token).ConfigureAwait(false);

        var item = new DownloadItem
        {
            Url = url,
            OriginalUrl = url,
            Referrer = request.Referrer,
            UserAgent = request.UserAgent,
            Queue = string.IsNullOrWhiteSpace(request.Queue) ? null : request.Queue,
            Connections = request.Connections ?? Settings.DefaultConnections,
            Status = DownloadStatus.Probing,
            Added = DateTime.Now
        };

        if (!request.Silent && Items.Any(i => string.Equals(i.Url, url, StringComparison.OrdinalIgnoreCase)
                                             && i.Quality == request.Quality
                                             && i.Status is DownloadStatus.Downloading or DownloadStatus.Probing))
            throw new InvalidOperationException("This URL is already downloading.");

        Ui(() => Items.Add(item));
        Ui(() => Changed?.Invoke());

        var probe = await HttpProbe.ProbeAsync(_client, url, item, token).ConfigureAwait(false);
        if (!probe.Ok)
        {
            item.Status = DownloadStatus.Failed;
            item.Error = probe.Error;
            SaveState();
            Ui(() => ItemFailed?.Invoke(item));
            return item;
        }

        item.TotalBytes = probe.TotalBytes;
        item.ContentType = probe.ContentType;
        item.Url = probe.Url;
        item.FileName = request.FileName ?? probe.FileName ?? "download";
        item.Category = request.Category ?? Settings.CategoryFor(item.FileName).Name;

        var folder = request.SaveFolder ?? Settings.ResolveFolder(item.Category);
        Directory.CreateDirectory(folder);
        item.SavePath = PathUtil.Unique(Path.Combine(folder, PathUtil.Sanitize(item.FileName)));

        item.Status = request.StartNow ? DownloadStatus.Queued : DownloadStatus.Paused;
        SaveState();
        Ui(() => Changed?.Invoke());

        if (request.StartNow)
            _ = StartAsync(item);

        return item;
    }

    private async Task<DownloadItem> AddExternalAsync(AddDownloadRequest request, CancellationToken token)
    {
        var url = request.Url.Trim();
        var quality = YtDlp.NormalizeQuality(request.Quality ?? Settings.YtDlpQuality);
        var item = new DownloadItem
        {
            Url = url,
            OriginalUrl = url,
            Engine = "ytdlp",
            Quality = quality,
            Referrer = request.Referrer,
            UserAgent = request.UserAgent,
            Queue = string.IsNullOrWhiteSpace(request.Queue) ? null : request.Queue,
            Connections = request.Connections ?? Settings.DefaultConnections,
            Status = DownloadStatus.Probing,
            Added = DateTime.Now
        };

        if (!request.Silent && Items.Any(i => string.Equals(i.Url, url, StringComparison.OrdinalIgnoreCase)
                                             && i.Quality == quality
                                             && i.Status is DownloadStatus.Downloading or DownloadStatus.Probing))
            throw new InvalidOperationException("This page is already being prepared.");

        Ui(() => Items.Add(item));
        Ui(() => Changed?.Invoke());

        if (!Settings.VideoSitesEnabled)
        {
            FailExternal(item, "Video-site downloads are turned off in Options > Video sites.");
            return item;
        }

        var exe = YtDlp.Locate(Settings);
        if (exe is null)
        {
            FailExternal(item, "yt-dlp is not installed. Open Options > Video sites and click Install yt-dlp.");
            return item;
        }

        YtDlpInfo info;
        try
        {
            info = await YtDlp.ProbeAsync(exe, url, Settings, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            FailExternal(item, ex.Message);
            return item;
        }

        var title = PathUtil.Sanitize(info.Title).Replace('%', '_');
        var requested = Path.GetFileNameWithoutExtension(request.FileName?.Trim() ?? "");
        var baseName = string.IsNullOrWhiteSpace(requested) ? title : PathUtil.Sanitize(requested).Replace('%', '_');
        var extension = quality == "audio" ? ".m4a" : ".mp4";

        item.FileName = baseName + extension;
        item.Category = request.Category ?? Settings.CategoryFor(item.FileName).Name;

        var folder = request.SaveFolder ?? Settings.ResolveFolder(item.Category);
        Directory.CreateDirectory(folder);
        item.SavePath = PathUtil.Unique(Path.Combine(folder, PathUtil.Sanitize(item.FileName)));

        item.Status = request.StartNow ? DownloadStatus.Queued : DownloadStatus.Paused;
        SaveState();
        Ui(() => Changed?.Invoke());

        if (request.StartNow)
            _ = StartAsync(item);

        return item;
    }

    private void FailExternal(DownloadItem item, string message)
    {
        item.Status = DownloadStatus.Failed;
        item.Speed = 0;
        item.Error = message;
        Log?.Invoke($"Video download failed: {message}");
        SaveState();
        Ui(() => ItemFailed?.Invoke(item));
        Ui(() => Changed?.Invoke());
    }

    // ---------------------------------------------------------------- control
    public Task StartAsync(DownloadItem item, bool resetRetries = true)
    {
        int generation;
        lock (_gate)
        {
            generation = BumpGeneration(item.Id);
            if (resetRetries) _retries.Remove(item.Id);
        }
        return Task.Run(() => RunAsync(item, generation));
    }

    public void Pause(DownloadItem item)
    {
        lock (_gate)
        {
            BumpGeneration(item.Id);
            if (_cancel.Remove(item.Id, out var cts)) cts.Cancel();
        }
        item.Status = DownloadStatus.Paused;
        item.Speed = 0;
        PersistMeta(item);
        SaveState();
        Changed?.Invoke();
    }

    public void Cancel(DownloadItem item)
    {
        lock (_gate)
        {
            BumpGeneration(item.Id);
            if (_cancel.Remove(item.Id, out var cts)) cts.Cancel();
        }
        item.Status = DownloadStatus.Canceled;
        item.Speed = 0;
        PersistMeta(item);
        SaveState();
        Changed?.Invoke();
    }

    public void ChangeUrl(DownloadItem item, string url)
    {
        Pause(item);
        item.Url = url.Trim();
        item.Error = null;
        item.TotalBytes = 0;
        item.DownloadedBytes = 0;
        item.CompletedAt = null;
        item.Segments.Clear();
        TryDelete(PartPath(item));
        TryDelete(MetaPath(item));
        item.Status = DownloadStatus.Paused;
        lock (_gate)
        {
            _retries.Remove(item.Id);
            _lastSample.Remove(item.Id);
            _lastSampleBytes.Remove(item.Id);
        }
        item.RefreshAll();
        SaveState();
        Changed?.Invoke();
    }

    public void Remove(DownloadItem item, bool deleteFile, bool deletePart)
    {
        Cancel(item);
        if (deleteFile && File.Exists(item.SavePath)) TryDelete(item.SavePath);
        if (deletePart) TryDelete(PartPath(item));
        TryDelete(MetaPath(item));
        lock (_gate)
        {
            _generation.Remove(item.Id);
            _retries.Remove(item.Id);
        }
        Ui(() => Items.Remove(item));
        SaveState();
        Ui(() => Changed?.Invoke());
    }

    // ---------------------------------------------------------------- download

    private int BumpGeneration(string id)
    {
        var next = (_generation.TryGetValue(id, out var current) ? current : 0) + 1;
        _generation[id] = next;
        return next;
    }

    private int GenerationOf(string id) =>
        _generation.TryGetValue(id, out var value) ? value : 0;

    private async Task RunAsync(DownloadItem item, int generation)
    {
        bool slot = false;
        try
        {
            if (!SchedulerAllowsNow())
            {
                item.Status = DownloadStatus.Queued;
                item.Speed = 0;
                item.Error = "Waiting for the scheduler window.";
                SaveState();
                Changed?.Invoke();
                return;
            }

            if (!await WaitForSlotAsync(item, generation).ConfigureAwait(false)) return;
            slot = true;

            var cts = new CancellationTokenSource();
            lock (_gate)
            {
                if (GenerationOf(item.Id) != generation || _cancel.ContainsKey(item.Id))
                {
                    cts.Dispose();
                    return;
                }
                _cancel[item.Id] = cts;
            }

            try
            {
                item.Status = DownloadStatus.Downloading;
                item.Error = null;
                item.Speed = 0;
                Changed?.Invoke();

                if (string.Equals(item.Engine, "ytdlp", StringComparison.OrdinalIgnoreCase))
                    await RunYtDlpAsync(item, cts.Token).ConfigureAwait(false);
                else if (IsHls(item)) { await RunHlsAsync(item, cts.Token).ConfigureAwait(false); }
                else { await RunSegmentedAsync(item, cts.Token).ConfigureAwait(false); }

                if (item.Status != DownloadStatus.Canceled)
                {
                    item.Status = DownloadStatus.Completed;
                    item.CompletedAt = DateTime.Now;
                    item.Speed = 0;
                    item.DownloadedBytes = item.TotalBytes > 0 ? item.TotalBytes : item.DownloadedBytes;
                    item.RefreshAll();
                    TryDelete(MetaPath(item));
                    lock (_gate) _retries.Remove(item.Id);
                    SaveState();
                    Ui(() => ItemCompleted?.Invoke(item));
                    Ui(() => Changed?.Invoke());
                    HandleQueueFinished();
                }
            }
            catch (OperationCanceledException)
            {
                item.Speed = 0;
                bool superseded;
                lock (_gate) superseded = _cancel.TryGetValue(item.Id, out var active) && !ReferenceEquals(active, cts);
                if (!superseded && item.Status == DownloadStatus.Downloading)
                {
                    item.Status = DownloadStatus.Paused;
                    PersistMeta(item);
                }
            }
            catch (Exception ex)
            {
                Log?.Invoke($"Download failed: {item.FileName}: {ex.Message}");
                item.Error = ex.Message;
                item.Speed = 0;
                item.Status = DownloadStatus.Failed;
                PersistMeta(item);
                Ui(() => ItemFailed?.Invoke(item));

                if (Settings.RetryAutomatically && !YtDlp.IsPermanentError(item.Error))
                    ScheduleRetry(item);
            }
            finally
            {
                lock (_gate)
                {
                    if (_cancel.TryGetValue(item.Id, out var active) && ReferenceEquals(active, cts))
                        _cancel.Remove(item.Id);
                }
            }
        }
        finally
        {
            if (slot)
            {
                lock (_gate) _activeSlots = Math.Max(0, _activeSlots - 1);
            }
            SaveState();
            Changed?.Invoke();
        }
    }

    private void ScheduleRetry(DownloadItem item)
    {
        int attempt;
        lock (_gate)
        {
            attempt = (_retries.TryGetValue(item.Id, out var current) ? current : 0) + 1;
            _retries[item.Id] = attempt;
        }

        if (attempt > Math.Max(1, Settings.RetryCount))
        {
            Log?.Invoke($"Giving up after {attempt - 1} retries: {item.FileName}");
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, Settings.RetryDelaySeconds)))
                    .ConfigureAwait(false);
                lock (_gate)
                {
                    if (item.Status != DownloadStatus.Failed) return;
                }
                Log?.Invoke($"Retry {attempt}/{Settings.RetryCount}: {item.FileName}");
                await StartAsync(item, resetRetries: false).ConfigureAwait(false);
            }
            catch (Exception ex) { Log?.Invoke("Retry scheduling failed: " + ex.Message); }
        });
    }

    private async Task<bool> WaitForSlotAsync(DownloadItem item, int generation)
    {
        while (true)
        {
            lock (_gate)
            {
                if (GenerationOf(item.Id) != generation) return false;
                if (_activeSlots < Math.Max(1, Settings.MaxConcurrentDownloads))
                {
                    _activeSlots++;
                    return true;
                }
            }

            item.Status = DownloadStatus.Queued;
            item.Speed = 0;
            await Task.Delay(400).ConfigureAwait(false);
        }
    }

    private async Task RunSegmentedAsync(DownloadItem item, CancellationToken token)
    {
        var probe = await HttpProbe.ProbeAsync(_client, item.Url, item, token).ConfigureAwait(false);
        if (!probe.Ok) throw new IOException(probe.Error ?? "Probe failed");

        long total = probe.TotalBytes;
        if (total > 0) item.TotalBytes = total;
        item.ContentType ??= probe.ContentType;
        item.Url = probe.Url;

        bool ranged = probe.AcceptRanges && total > 0;
        int connections = ranged ? Math.Clamp(item.Connections, 1, 32) : 1;

        if (item.Segments.Count == 0 || (total > 0 && item.Segments.Sum(s => s.Length) != total))
            BuildSegments(item, total, connections, ranged);

        var partPath = PartPath(item);
        Directory.CreateDirectory(Path.GetDirectoryName(partPath)!);

        // A missing or resized .part cannot contain the data the saved segment
        // offsets describe. Start those segments over instead of producing a
        // file with silent holes.
        bool freshPart = !File.Exists(partPath) ||
                         (total > 0 && new FileInfo(partPath).Length != total);
        if (freshPart)
        {
            foreach (var segment in item.Segments)
            {
                segment.Position = segment.Start;
                segment.Done = false;
            }
        }

        {
            using var handle = File.OpenHandle(partPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
            if (total > 0 && RandomAccess.GetLength(handle) != total)
                RandomAccess.SetLength(handle, total);

            var pending = item.Segments.Where(s => !s.Done).ToList();
            var tasks = new List<Task>(pending.Count);
            int perConnection = ranged ? Math.Max(1, connections / Math.Max(1, pending.Count)) : 1;

            foreach (var segment in pending)
                tasks.Add(DownloadSegmentAsync(item, handle, segment, perConnection, token));

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        if (item.Status == DownloadStatus.Canceled) return;

        if (total <= 0)
        {
            total = new FileInfo(partPath).Length;
            item.TotalBytes = total;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(item.SavePath)!);
        if (File.Exists(item.SavePath)) item.SavePath = PathUtil.Unique(item.SavePath);
        File.Move(partPath, item.SavePath, overwrite: true);
        Interlocked.Add(ref _sessionBytes, total);
        Interlocked.Add(ref _dailyBytes, total);
        item.DownloadedBytes = total > 0 ? total : item.DownloadedBytes;
        item.Raise(nameof(DownloadItem.Progress));
    }

    private static void BuildSegments(DownloadItem item, long total, int connections, bool ranged)
    {
        item.Segments.Clear();
        if (!ranged || total <= 0 || connections <= 1)
        {
            item.Segments.Add(new DownloadSegment { Start = 0, End = total > 0 ? total - 1 : long.MaxValue });
            return;
        }

        long chunk = total / connections;
        long start = 0;
        for (int i = 0; i < connections; i++)
        {
            long end = i == connections - 1 ? total - 1 : start + chunk - 1;
            item.Segments.Add(new DownloadSegment { Start = start, End = end, Position = start });
            start = end + 1;
        }
    }

    private async Task DownloadSegmentAsync(DownloadItem item, SafeFileHandle handle, DownloadSegment segment,
        int connectionsPerSegment, CancellationToken token)
    {
        int failures = 0;
        while (!segment.Done && segment.Position <= segment.End)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, item.Url);
                HttpProbe.ApplyHeaders(request, item);
                request.Headers.Range = new RangeHeaderValue(segment.Position,
                    segment.End == long.MaxValue ? null : segment.End);

                using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                    {
                        segment.Done = true;
                        return;
                    }
                    throw new HttpRequestException($"{(int)response.StatusCode} {response.ReasonPhrase}");
                }

                if (response.StatusCode == HttpStatusCode.OK && segment.Position > 0)
                    throw new HttpRequestException("The server ignored the Range header.");

                await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                var buffer = new byte[BufferSize];

                while (true)
                {
                    int read = await stream.ReadAsync(buffer, token).ConfigureAwait(false);
                    if (read <= 0) break;

                    long writeAt = segment.Position;
                    if (writeAt + read > segment.End + 1 && segment.End != long.MaxValue)
                        read = (int)Math.Max(0, segment.End + 1 - writeAt);
                    if (read <= 0) break;

                    if (_limiter.Enabled)
                        await _limiter.AcquireAsync(read, token).ConfigureAwait(false);

                    await RandomAccess.WriteAsync(handle, buffer.AsMemory(0, read), writeAt, token).ConfigureAwait(false);
                    segment.Position += read;

                    if (segment.Position > segment.End) break;
                }

                if (segment.Position > segment.End ||
                    (segment.End == long.MaxValue && item.TotalBytes > 0 &&
                     segment.Position >= item.TotalBytes))
                    segment.Done = true;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (++failures <= 4)
            {
                Log?.Invoke($"segment {segment.Start} retry {failures}: {ex.Message}");
                await Task.Delay(400 * failures, token).ConfigureAwait(false);
            }
        }

        segment.Done = true;
    }

    // ---------------------------------------------------------------- HLS

    private static bool IsHls(DownloadItem item) =>
        item.Url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase) ||
        (item.ContentType?.Contains("mpegurl", StringComparison.OrdinalIgnoreCase) ?? false);

    private async Task RunHlsAsync(DownloadItem item, CancellationToken token)
    {
        string playlist = await _client.GetStringAsync(item.Url, token).ConfigureAwait(false);
        var baseUri = new Uri(item.Url);
        var entries = ParsePlaylist(playlist, baseUri);

        // Master playlist: follow the highest-bandwidth variant.
        if (entries.Count > 0 && entries.All(e => !e.Segment))
        {
            var best = entries.OrderByDescending(e => e.Bandwidth).First();
            playlist = await _client.GetStringAsync(best.Url, token).ConfigureAwait(false);
            baseUri = new Uri(best.Url);
            entries = ParsePlaylist(playlist, baseUri);
        }

        var segments = entries.Where(e => e.Segment).Select(e => e.Url).ToList();
        if (segments.Count == 0) throw new IOException("Playlist contains no media segments.");

        item.TotalBytes = entries.Where(e => e.Segment).Sum(e => e.Bytes);
        var partPath = PartPath(item);
        Directory.CreateDirectory(Path.GetDirectoryName(partPath)!);

        await using (var output = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.Read, BufferSize, true))
        {
            int index = 0;
            foreach (var segmentUrl in segments)
            {
                token.ThrowIfCancellationRequested();
                using var response = await _client.GetAsync(segmentUrl, HttpCompletionOption.ResponseHeadersRead, token)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                await stream.CopyToAsync(output, BufferSize, token).ConfigureAwait(false);
                index++;
                if (item.TotalBytes > 0) item.DownloadedBytes = item.TotalBytes * index / segments.Count;
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(item.SavePath)!);
        var target = PathUtil.Unique(Path.ChangeExtension(item.SavePath, ".ts"));
        File.Move(partPath, target, overwrite: true);
        item.SavePath = target;
        item.TotalBytes = new FileInfo(target).Length;
        item.DownloadedBytes = item.TotalBytes;
    }

    // -------------------------------------------------------------- yt-dlp

    private async Task RunYtDlpAsync(DownloadItem item, CancellationToken token)
    {
        var exe = YtDlp.Locate(Settings)
                  ?? throw new IOException(
                      "yt-dlp is not installed. Open Options > Video sites and click Install yt-dlp.");

        var basePath = Path.ChangeExtension(item.SavePath, null);
        if (string.IsNullOrWhiteSpace(basePath))
            throw new IOException("The output folder for this download is not set.");

        var result = await YtDlp.DownloadAsync(exe, item, Settings, basePath,
            (downloaded, total) =>
            {
                if (total > 0) item.TotalBytes = total;
                if (downloaded > 0) item.DownloadedBytes = downloaded;
            },
            message => Log?.Invoke(message), token).ConfigureAwait(false);

        if (token.IsCancellationRequested) throw new OperationCanceledException(token);
        if (!result.Ok) throw new IOException(result.Error ?? "yt-dlp failed.");

        var path = result.FilePath!;
        item.SavePath = path;
        item.FileName = Path.GetFileName(path);
        item.TotalBytes = new FileInfo(path).Length;
        item.DownloadedBytes = item.TotalBytes;

        Interlocked.Add(ref _sessionBytes, item.TotalBytes);
        Interlocked.Add(ref _dailyBytes, item.TotalBytes);
    }

    private sealed record PlaylistEntry(string Url, long Bytes, bool Segment, long Bandwidth);
    private static List<PlaylistEntry> ParsePlaylist(string playlist, Uri baseUri)
    {
        var list = new List<PlaylistEntry>();
        var lines = playlist.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        long pendingBytes = 0, bandwidth = 0;

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.StartsWith("#EXT-X-STREAM-INF", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(line, @"BANDWIDTH=(\d+)");
                bandwidth = match.Success ? long.Parse(match.Groups[1].Value) : 0;
                var next = lines.Skip(i + 1).FirstOrDefault(l => !l.StartsWith('#'));
                if (next != null) list.Add(new PlaylistEntry(Absolute(baseUri, next), 0, false, bandwidth));
            }
            else if (line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
            {
                var next = lines.Skip(i + 1).FirstOrDefault(l => !l.StartsWith('#'));
                if (next != null) list.Add(new PlaylistEntry(Absolute(baseUri, next), pendingBytes, true, 0));
            }
            else if (line.StartsWith("#EXT-X-BYTERANGE", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(line, @"(\d+)");
                if (match.Success) pendingBytes = long.Parse(match.Groups[1].Value);
            }
        }

        return list;
    }

    private static string Absolute(Uri baseUri, string value) =>
        Uri.TryCreate(baseUri, value, out var uri) ? uri.ToString() : value;

    // ---------------------------------------------------------------- state

    private string PartPath(DownloadItem item) =>
        Path.Combine(Settings.TempDirectory, item.Id + ".part");

    private string MetaPath(DownloadItem item) =>
        Path.Combine(Settings.TempDirectory, item.Id + ".rfmeta");

    public void SaveState()
    {
        try
        {
            List<DownloadItem>? snapshot = null;
            Ui(() => snapshot = Items.ToList());
            if (snapshot is null) return;

            var dir = Path.GetDirectoryName(StatePath)!;
            var json = JsonSerializer.Serialize(snapshot, JsonOptions);
            lock (_stateGate)
            {
                Directory.CreateDirectory(dir);
                var temp = StatePath + "." + Environment.CurrentManagedThreadId + ".tmp";
                File.WriteAllText(temp, json);
                File.Move(temp, StatePath, true);
            }
        }
        catch (Exception ex) { Log?.Invoke("State save failed: " + ex.Message); }
    }

    private readonly object _stateGate = new();

    private void LoadState()
    {
        try
        {
            if (!File.Exists(StatePath)) return;
            var items = JsonSerializer.Deserialize<List<DownloadItem>>(File.ReadAllText(StatePath), JsonOptions);
            if (items is null) return;

            foreach (var item in items)
            {
                // Queued downloads keep waiting when the scheduler owns the queue, so a
                // restart during the "off" window does not silently turn them into paused.
                if (item.Status is DownloadStatus.Downloading or DownloadStatus.Probing
                    || (item.Status == DownloadStatus.Queued && !Settings.Scheduler.Enabled))
                    item.Status = DownloadStatus.Paused;
                item.Speed = 0;
                LoadMeta(item);
                Items.Add(item);
            }
        }
        catch (Exception ex) { Log?.Invoke("State load failed: " + ex.Message); }
    }

    private void LoadMeta(DownloadItem item)
    {
        try
        {
            var path = MetaPath(item);
            if (!File.Exists(path) || item.TotalBytes <= 0) return;
            var segments = JsonSerializer.Deserialize<List<DownloadSegment>>(
                File.ReadAllText(path), JsonOptions);
            if (segments is null || segments.Count == 0) return;
            if (segments.Any(s => s.End < s.Start)) return;
            if (segments.Sum(s => s.Length) != item.TotalBytes) return;

            foreach (var segment in segments)
                segment.Position = Math.Clamp(segment.Position, segment.Start, segment.End + 1);
            item.Segments = segments;
        }
        catch (Exception ex) { Log?.Invoke("Meta load failed: " + ex.Message); }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private void Tick()
    {
        // Compare thread ids, not SynchronizationContext instances: WPF creates a
        // fresh DispatcherSynchronizationContext per message loop, so reference
        // equality would always look like "not the UI thread" and loop forever.
        if (_sync is not null && Environment.CurrentManagedThreadId != _uiThreadId)
        {
            _sync.Post(_ => Tick(), null);
            return;
        }

        try
        {
            if (_dailyDate != DateTime.Today)
            {
                _dailyDate = DateTime.Today;
                Interlocked.Exchange(ref _dailyBytes, 0);
            }

            foreach (var item in Items.ToList())
            {
                if (item.Status != DownloadStatus.Downloading) continue;

                long total = item.Segments.Count > 0 ? SumPosition(item) : item.DownloadedBytes;
                long previous = _lastSample.TryGetValue(item.Id, out var prev) ? prev : 0;
                var lastTime = _lastSampleBytes.TryGetValue(item.Id, out var t) ? t : 0;
                long now = Stopwatch.GetTimestamp();
                double seconds = lastTime == 0 ? 0 : (now - lastTime) / (double)Stopwatch.Frequency;

                if (item.Segments.Count > 0) item.DownloadedBytes = total;
                if (seconds > 0.2)
                {
                    double instant = Math.Max(0, total - previous) / seconds;
                    item.Speed = item.Speed <= 0 ? instant : item.Speed * 0.6 + instant * 0.4;
                    _lastSample[item.Id] = total;
                    _lastSampleBytes[item.Id] = now;
                }
                else if (!_lastSampleBytes.ContainsKey(item.Id))
                {
                    _lastSampleBytes[item.Id] = now;
                    _lastSample[item.Id] = total;
                }

                PersistMeta(item);
            }
        }
        catch (Exception ex) { Log?.Invoke("tick failed: " + ex.Message); }
    }

    private static long SumPosition(DownloadItem item) =>
        item.Segments.Sum(s => s.End == long.MaxValue
            ? Math.Max(0, s.Position - s.Start)
            : Math.Clamp(s.Position - s.Start, 0, s.Length));

    private void PersistMeta(DownloadItem item)
    {
        try
        {
            var path = MetaPath(item);
            if (item.Segments.Count == 0) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(item.Segments, JsonOptions));
        }
        catch { }
    }

    private void SchedulerTick()
    {
        if (!Settings.Scheduler.Enabled) return;

        var s = Settings.Scheduler;
        bool allowed = SchedulerAllowsNow();

        foreach (var item in Items.ToList())
        {
            if (allowed && item.Status == DownloadStatus.Queued && Settings.StartQueuedAutomatically)
                _ = StartAsync(item);
            else if (!allowed && item.Status == DownloadStatus.Downloading)
                Pause(item);
        }

        if (!allowed) HandleQueueFinished();
    }

    private bool SchedulerAllowsNow()
    {
        var s = Settings.Scheduler;
        if (!s.Enabled) return true;

        var now = DateTime.Now;
        if (!s.Days.Contains(now.DayOfWeek)) return false;
        if (!IsInWindow(now.TimeOfDay, s.StartTime, s.StopTime)) return false;
        if (s.DailyLimitBytes > 0 && DailyBytes >= s.DailyLimitBytes) return false;
        return true;
    }

    private static bool IsInWindow(TimeSpan now, TimeSpan start, TimeSpan stop) =>
        start <= stop ? now >= start && now < stop : now >= start || now < stop;

    private DateTime _lastQueueAction = DateTime.MinValue;

    private void HandleQueueFinished()
    {
        var s = Settings.Scheduler;
        if (!s.Enabled || s.OnQueueFinished == "none") return;
        if (SchedulerAllowsNow()) return;
        if (DateTime.Now - _lastQueueAction < TimeSpan.FromMinutes(30)) return;
        if (Items.Any(i => i.Status is DownloadStatus.Downloading or DownloadStatus.Queued or DownloadStatus.Probing))
            return;
        if (!Items.Any(i => i.CompletedAt is not null && i.CompletedAt.Value.Date == DateTime.Today))
            return;

        _lastQueueAction = DateTime.Now;
        Log?.Invoke($"Scheduler window ended — action: {s.OnQueueFinished}");

        try
        {
            switch (s.OnQueueFinished)
            {
                case "exit":
                    Ui(() => ExitRequested?.Invoke());
                    break;
                case "sleep":
                    Process.Start(new ProcessStartInfo("rundll32.exe",
                        "powrprof.dll,SetSuspendState 0,1,0") { UseShellExecute = false, CreateNoWindow = true });
                    break;
                case "hibernate":
                    Process.Start(new ProcessStartInfo("shutdown.exe", "/h") { UseShellExecute = false, CreateNoWindow = true });
                    break;
                case "shutdown":
                    Process.Start(new ProcessStartInfo("shutdown.exe", "/s /t 60") { UseShellExecute = false, CreateNoWindow = true });
                    break;
            }
        }
        catch (Exception ex) { Log?.Invoke("Scheduler action failed: " + ex.Message); }
    }

    private void Ui(Action action)
    {
        if (_sync is null || Environment.CurrentManagedThreadId == _uiThreadId) { action(); return; }
        try { _sync.Send(_ => action(), null); } catch { }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    public void Dispose()
    {
        foreach (var item in Items.ToList())
        {
            if (item.Status == DownloadStatus.Downloading) PersistMeta(item);
        }
        lock (_gate)
        {
            foreach (var cts in _cancel.Values) cts.Cancel();
            _cancel.Clear();
        }
        _ticker.Dispose();
        _schedulerTimer.Dispose();
        _client.Dispose();
        SaveState();
    }
}
