using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RapidFetch.Core.Engine;
using RapidFetch.Core.Models;

namespace RapidFetch.Core.Services;

/// <summary>
/// Localhost bridge used by the browser extension (HTTP) and the native-messaging host.
/// Bound to 127.0.0.1 only; a per-install token gates write operations.
/// </summary>
public sealed class BrowserBridge : IDisposable
{
    private readonly DownloadEngine _engine;
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;
    private string _prefix = "";
    private readonly string _token;

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public BrowserBridge(DownloadEngine engine, string token)
    {
        _engine = engine;
        _token = token;
    }

    public bool IsRunning => _listener.IsListening;
    public int Port { get; private set; }

    public event Action<AddDownloadRequest>? ExternalAddRequested;
    public event Action? ShowRequested;
    public event Action<string>? OpenFolderRequested;
    public event Action<string>? Log;

    public void Start(int port)
    {
        if (_listener.IsListening) Stop();
        Port = port;
        _prefix = $"http://127.0.0.1:{port}/";
        _listener.Prefixes.Clear();
        _listener.Prefixes.Add(_prefix);
        _listener.Start();
        _loop = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        try { _listener.Stop(); } catch { }
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync().ConfigureAwait(false); }
            catch (Exception) { break; }

            _ = Task.Run(() => HandleAsync(ctx, token), token);
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx, CancellationToken token)
    {
        var response = ctx.Response;
        response.Headers["Access-Control-Allow-Origin"] = "*";
        response.Headers["Access-Control-Allow-Headers"] = "content-type,x-rapidfetch-token";
        response.Headers["Access-Control-Allow-Methods"] = "GET,POST,OPTIONS";

        try
        {
            if (ctx.Request.HttpMethod == "OPTIONS") { response.StatusCode = 204; response.Close(); return; }

            string path = ctx.Request.Url?.AbsolutePath.TrimEnd('/').ToLowerInvariant() ?? "";
            string body = "";
            if (ctx.Request.HasEntityBody)
                using (var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding))
                    body = await reader.ReadToEndAsync(token).ConfigureAwait(false);

            object payload;
            switch (path)
            {
                case "/ping":
                    payload = new
                    {
                        ok = true,
                        app = "RapidFetch",
                        version = ProgramInfo.Version,
                        port = Port,
                        videoSites = _engine.Settings.VideoSitesEnabled,
                        ytdlp = YtDlp.Locate(_engine.Settings) is not null,
                        ffmpeg = YtDlp.LocateFfmpeg(_engine.Settings) is not null,
                        quality = YtDlp.NormalizeQuality(_engine.Settings.YtDlpQuality)
                    };
                    break;

                case "/status":
                    payload = new
                    {
                        ok = true,
                        downloads = _engine.Items.Select(ToDto).ToList(),
                        stats = new
                        {
                            session = _engine.SessionBytes,
                            daily = _engine.DailyBytes,
                            active = _engine.Items.Count(i => i.Status == DownloadStatus.Downloading)
                        }
                    };
                    break;

                case "/formats":
                {
                    var request = JsonSerializer.Deserialize<UrlRequest>(body, Json);
                    if (request is null || string.IsNullOrWhiteSpace(request.Url))
                    {
                        await WriteAsync(response, 400, new { ok = false, error = "url required" }, token);
                        return;
                    }

                    var exe = YtDlp.Locate(_engine.Settings);
                    if (exe is null)
                    {
                        await WriteAsync(response, 400,
                            new { ok = false, error = "yt-dlp is not installed" }, token);
                        return;
                    }

                    if (!YtDlp.IsVideoSite(request.Url))
                    {
                        await WriteAsync(response, 400,
                            new { ok = false, error = "not a supported video page" }, token);
                        return;
                    }

                    var info = await YtDlp.ListFormatsAsync(exe, request.Url,
                        _engine.Settings, token).ConfigureAwait(false);
                    payload = new
                    {
                        ok = true,
                        title = info.Title,
                        duration = info.Duration,
                        formats = info.Formats
                    };
                    break;
                }

                case "/add":
                {
                    var request = JsonSerializer.Deserialize<AddRequest>(body, Json);
                    if (request is null || string.IsNullOrWhiteSpace(request.Url))
                    {
                        await WriteAsync(response, 400, new { ok = false, error = "url required" }, token);
                        return;
                    }

                    // Automatic browser captures are replayed by Chrome when it
                    // restores a session. Skip anything RapidFetch already has
                    // instead of downloading the browser history a second time.
                    if (string.Equals(request.Source, "capture", StringComparison.OrdinalIgnoreCase))
                    {
                        var existing = _engine.Items.FirstOrDefault(i =>
                            (string.Equals(i.Url, request.Url, StringComparison.OrdinalIgnoreCase)
                             || string.Equals(i.OriginalUrl, request.Url, StringComparison.OrdinalIgnoreCase)) &&
                            (i.Status is DownloadStatus.Downloading or DownloadStatus.Probing or DownloadStatus.Queued
                             || (i.Status == DownloadStatus.Completed && File.Exists(i.SavePath))));
                        if (existing is not null)
                        {
                            payload = new
                            {
                                ok = true,
                                duplicate = true,
                                reason = existing.Status == DownloadStatus.Completed ? "completed" : "active",
                                id = existing.Id,
                                fileName = existing.FileName
                            };
                            break;
                        }
                    }

                    var add = new AddDownloadRequest
                    {
                        Url = request.Url,
                        FileName = request.FileName,
                        Referrer = request.Referrer,
                        UserAgent = request.UserAgent,
                        Category = request.Category,
                        Connections = request.Connections,
                        Quality = request.Quality,
                        StartNow = request.StartNow ?? true,
                        Queue = request.Queue
                    };
                    ExternalAddRequested?.Invoke(add);
                    var item = await _engine.AddAsync(add, token).ConfigureAwait(false);
                    payload = new { ok = true, id = item.Id, fileName = item.FileName, size = item.TotalBytes };
                    break;
                }

                case "/add-batch":
                {
                    var urls = JsonSerializer.Deserialize<List<string>>(body, Json) ?? new();
                    var ids = new List<string>();
                    foreach (var url in urls.Take(200))
                    {
                        try
                        {
                            var item = await _engine.AddAsync(new AddDownloadRequest { Url = url }, token).ConfigureAwait(false);
                            ids.Add(item.Id);
                        }
                        catch { }
                    }
                    payload = new { ok = true, ids };
                    break;
                }

                case "/media":
                {
                    var media = JsonSerializer.Deserialize<MediaCandidate>(body, Json);
                    if (media is null || string.IsNullOrWhiteSpace(media.Url))
                    {
                        await WriteAsync(response, 400, new { ok = false }, token);
                        return;
                    }
                    var item = await _engine.AddAsync(new AddDownloadRequest
                    {
                        Url = media.Url,
                        Referrer = media.PageUrl,
                        FileName = media.FileName,
                        Category = "Video"
                    }, token).ConfigureAwait(false);
                    payload = new { ok = true, id = item.Id, fileName = item.FileName };
                    break;
                }

                case "/show":
                    ShowRequested?.Invoke();
                    payload = new { ok = true };
                    break;

                case "/open-folder":
                {
                    var request = JsonSerializer.Deserialize<PathRequest>(body, Json);
                    if (request?.Path is null || !Directory.Exists(request.Path))
                    {
                        await WriteAsync(response, 400, new { ok = false, error = "folder not found" }, token);
                        return;
                    }
                    OpenFolderRequested?.Invoke(request.Path);
                    payload = new { ok = true };
                    break;
                }

                case "/reveal":
                {
                    var target = JsonSerializer.Deserialize<IdRequest>(body, Json);
                    var item = _engine.Items.FirstOrDefault(i => i.Id == target?.Id);
                    if (item is null) { await WriteAsync(response, 404, new { ok = false }, token); return; }
                    var targetPath = item.SavePath;
                    if (File.Exists(targetPath))
                        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{targetPath}\"") { UseShellExecute = true });
                    else if (Directory.Exists(Path.GetDirectoryName(targetPath)))
                        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Path.GetDirectoryName(targetPath)}\"") { UseShellExecute = true });
                    payload = new { ok = true };
                    break;
                }

                case "/pause":
                case "/resume":
                case "/remove":
                {
                    var target = JsonSerializer.Deserialize<IdRequest>(body, Json);
                    var item = _engine.Items.FirstOrDefault(i => i.Id == target?.Id);
                    if (item is null) { await WriteAsync(response, 404, new { ok = false }, token); return; }
                    if (path == "/pause") _engine.Pause(item);
                    else if (path == "/resume") _ = _engine.StartAsync(item);
                    else _engine.Remove(item, false, false);
                    payload = new { ok = true };
                    break;
                }

                default:
                    await WriteAsync(response, 404, new { ok = false, error = "unknown endpoint" }, token);
                    return;
            }

            await WriteAsync(response, 200, payload, token);
        }
        catch (Exception ex)
        {
            Log?.Invoke("bridge request failed: " + ex.Message);
            try { await WriteAsync(response, 500, new { ok = false, error = ex.Message }, token); } catch { }
        }
    }

    private static async Task WriteAsync(HttpListenerResponse response, int status, object payload, CancellationToken token)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, Json));
        response.StatusCode = status;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, token).ConfigureAwait(false);
        response.Close();
    }

    private static object ToDto(DownloadItem item) => new
    {
        id = item.Id,
        url = item.Url,
        originalUrl = item.OriginalUrl,
        fileName = item.FileName,
        savePath = item.SavePath,
        total = item.TotalBytes,
        downloaded = item.DownloadedBytes,
        speed = item.Speed,
        status = item.StatusText,
        error = item.Error
    };

    private sealed class AddRequest
    {
        public string? Url { get; set; }
        public string? FileName { get; set; }
        public string? Referrer { get; set; }
        public string? UserAgent { get; set; }
        public string? Category { get; set; }
        public string? Queue { get; set; }
        public int? Connections { get; set; }
        public string? Quality { get; set; }
        public string? Source { get; set; }
        public bool? StartNow { get; set; }
    }

    private sealed class IdRequest { public string? Id { get; set; } }
    private sealed class PathRequest { public string? Path { get; set; } }
    private sealed class UrlRequest { public string? Url { get; set; } }

    private sealed class MediaCandidate
    {
        public string? Url { get; set; }
        public string? PageUrl { get; set; }
        public string? FileName { get; set; }
    }

    public void Dispose()
    {
        _cts.Cancel();
        Stop();
        _listener.Close();
    }
}

public static class ProgramInfo
{
    public const string Version = "1.0.0";
    public const string Product = "RapidFetch";
}
