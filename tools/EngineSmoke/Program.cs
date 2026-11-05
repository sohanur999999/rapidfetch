using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using RapidFetch.Core.Engine;
using RapidFetch.Core.Models;
using RapidFetch.Core.Services;
using RapidFetch.Core.Util;

namespace EngineSmoke;

internal static class Program
{
    private static int _failures;
    private static readonly byte[] Data = BuildData(4 * 1024 * 1024);
    private static readonly byte[] SmallData = BuildData(2 * 1024 * 1024);

    private static async Task<int> Main()
    {
        Console.WriteLine("RapidFetch engine smoke test");
        Console.WriteLine("----------------------------");

        VideoSiteRouting();

        await using var server = new TestServer();
        Console.WriteLine($"Local range server: {server.BaseUrl}");

        var root = Path.Combine(Path.GetTempPath(), "RapidFetchEngineSmoke",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        await DirectDownload(server, root);
        await PauseAndResume(server, root);
        await RestartResume(server, root);
        await Concurrency(server, root);
        await NoRangeFallback(server, root);
        await Hls(server, root);

        Console.WriteLine("----------------------------");
        Console.WriteLine(_failures == 0 ? "ALL TESTS PASSED" : $"{_failures} TEST(S) FAILED");
        try { Directory.Delete(root, true); } catch { }
        return _failures == 0 ? 0 : 1;
    }

    private static void VideoSiteRouting()
    {
        Check(YtDlp.IsVideoSite("https://www.youtube.com/watch?v=dQw4w9WgXcQ"),
            "routing: youtube watch page uses yt-dlp");
        Check(YtDlp.IsVideoSite("https://www.facebook.com/watch/?v=10153231379946729"),
            "routing: facebook watch page uses yt-dlp");
        Check(YtDlp.IsVideoSite("https://facebook.com/some.page/videos/123456789/"),
            "routing: facebook page video uses yt-dlp");
        Check(YtDlp.IsVideoSite("https://www.facebook.com/reel/123456789"),
            "routing: facebook reel uses yt-dlp");
        Check(YtDlp.IsVideoSite("https://www.facebook.com/share/v/abc123/"),
            "routing: facebook share link uses yt-dlp");
        Check(YtDlp.IsVideoSite("https://www.facebook.com/share/r/abc123/"),
            "routing: facebook reel share link uses yt-dlp");
        Check(YtDlp.IsVideoSite("https://www.facebook.com/video.php?v=123456789"),
            "routing: facebook video.php uses yt-dlp");
        Check(YtDlp.IsVideoSite("https://fb.watch/abc123/"),
            "routing: fb.watch short link uses yt-dlp");
        Check(!YtDlp.IsVideoSite("https://www.facebook.com/"),
            "routing: facebook home page stays with the file engine");
        Check(!YtDlp.IsVideoSite("https://www.facebook.com/photo/?fbid=123456789"),
            "routing: facebook photo page stays with the file engine");
        Check(!YtDlp.IsVideoSite("https://scontent.xx.fbcdn.net/v/t42.1790-2/video.mp4"),
            "routing: direct video file stays with the file engine");

        var dotted = new string('a', 170) + ". " + new string('b', 220);
        var safe = PathUtil.Sanitize(dotted);
        Check(safe.Length <= 180,
            "filenames: long title with a dotted sentence is truncated without an exception");
        Check(!safe.EndsWith(' '),
            "filenames: sanitized title does not end with a trailing space");
    }

    private static async Task DirectDownload(TestServer server, string root)
    {
        var settings = Settings(root, "direct");
        using var engine = new DownloadEngine(settings, Path.Combine(root, "direct-state.json"));
        var item = await engine.AddAsync(new AddDownloadRequest
        {
            Url = server.BaseUrl + "file.bin",
            Connections = 8
        });

        await Wait(() => item.Status == DownloadStatus.Completed, 30, "direct download");
        Check(item.Status == DownloadStatus.Completed, "direct: completed");
        Check(File.Exists(item.SavePath), "direct: file exists");
        Check(Hash(item.SavePath) == Hash(Data), "direct: SHA-256 matches");
        Check(item.Segments.Count > 1, "direct: segmented into multiple ranges");
    }

    private static async Task PauseAndResume(TestServer server, string root)
    {
        var settings = Settings(root, "pause");
        using var engine = new DownloadEngine(settings, Path.Combine(root, "pause-state.json"));
        var item = await engine.AddAsync(new AddDownloadRequest
        {
            Url = server.BaseUrl + "slow.bin",
            Connections = 4
        });

        await Wait(() => item.DownloadedBytes > 700_000, 20, "pause: partial data");
        engine.Pause(item);
        var pausedBytes = item.DownloadedBytes;
        await Task.Delay(1200);

        Check(item.Status == DownloadStatus.Paused, "pause: status is paused");
        Check(item.DownloadedBytes >= pausedBytes - 128 * 1024, "pause: progress is retained");

        _ = engine.StartAsync(item);
        await Wait(() => item.Status == DownloadStatus.Downloading, 10, "pause: resumed");
        await Task.Delay(500);
        Check(item.DownloadedBytes >= pausedBytes / 2, "pause: did not restart from zero");

        await Wait(() => item.Status == DownloadStatus.Completed, 40, "pause: completed");
        Check(Hash(item.SavePath) == Hash(Data), "pause: SHA-256 matches after resume");
    }

    private static async Task RestartResume(TestServer server, string root)
    {
        var settings = Settings(root, "restart");
        var state = Path.Combine(root, "restart-state.json");

        var first = new DownloadEngine(settings, state);
        var item = await first.AddAsync(new AddDownloadRequest
        {
            Url = server.BaseUrl + "slow.bin?restart=1",
            Connections = 4
        });
        await Wait(() => item.DownloadedBytes > 1_000_000, 20, "restart: partial data");
        first.Dispose();
        await Task.Delay(1500);

        using var second = new DownloadEngine(settings, state);
        var restored = second.Items.FirstOrDefault(i => i.Id == item.Id);
        Check(restored is not null, "restart: item restored from state");
        if (restored is null) return;
        Check(restored.Status == DownloadStatus.Paused, "restart: restored as paused");
        Check(restored.Segments.Count > 1, "restart: segments restored");
        Check(restored.DownloadedBytes > 0, "restart: progress restored");

        _ = second.StartAsync(restored);
        await Wait(() => restored.Status == DownloadStatus.Completed, 40, "restart: completed");
        Check(Hash(restored.SavePath) == Hash(Data), "restart: SHA-256 matches after app restart");
    }

    private static async Task Concurrency(TestServer server, string root)
    {
        var settings = Settings(root, "queue");
        settings.MaxConcurrentDownloads = 1;
        using var engine = new DownloadEngine(settings, Path.Combine(root, "queue-state.json"));

        var one = await engine.AddAsync(new AddDownloadRequest
        {
            Url = server.BaseUrl + "slow.bin?queue=1",
            Connections = 4
        });
        var two = await engine.AddAsync(new AddDownloadRequest
        {
            Url = server.BaseUrl + "slow.bin?queue=2",
            Connections = 4
        });

        // The first "slow" file needs well under a second, so sample continuously
        // while it is running rather than checking a single late timestamp.
        bool overlapped = false;
        bool firstWasActive = false;
        int maxActive = 0;
        for (int i = 0; i < 200 && one.Status != DownloadStatus.Completed; i++)
        {
            if (one.Status == DownloadStatus.Downloading) firstWasActive = true;
            if (one.Status == DownloadStatus.Downloading && two.Status == DownloadStatus.Downloading)
                overlapped = true;
            maxActive = Math.Max(maxActive,
                engine.Items.Count(x => x.Status == DownloadStatus.Downloading));
            await Task.Delay(20);
        }

        Check(firstWasActive, "queue: first transfer became active");
        Check(!overlapped, "queue: second never overlapped the first");
        Check(maxActive == 1, $"queue: at most one active transfer (found {maxActive})");

        await Wait(() => one.Status == DownloadStatus.Completed && two.Status == DownloadStatus.Completed,
            60, "queue: both completed");
        Check(Hash(one.SavePath) == Hash(Data), "queue: first SHA-256 matches");
        Check(Hash(two.SavePath) == Hash(Data), "queue: second SHA-256 matches");
    }

    private static async Task NoRangeFallback(TestServer server, string root)
    {
        var settings = Settings(root, "norange");
        using var engine = new DownloadEngine(settings, Path.Combine(root, "norange-state.json"));
        var item = await engine.AddAsync(new AddDownloadRequest
        {
            Url = server.BaseUrl + "no-range.bin",
            Connections = 8
        });

        await Wait(() => item.Status == DownloadStatus.Completed, 30, "no-range: completed");
        Check(item.Segments.Count == 1, "no-range: single stream fallback");
        Check(Hash(item.SavePath) == Hash(SmallData), "no-range: SHA-256 matches");
    }

    private static async Task Hls(TestServer server, string root)
    {
        var settings = Settings(root, "hls");
        using var engine = new DownloadEngine(settings, Path.Combine(root, "hls-state.json"));
        var item = await engine.AddAsync(new AddDownloadRequest
        {
            Url = server.BaseUrl + "hls/master.m3u8"
        });

        await Wait(() => item.Status == DownloadStatus.Completed, 30, "hls: completed");
        Check(item.SavePath.EndsWith(".ts", StringComparison.OrdinalIgnoreCase), "hls: joined .ts output");
        Check(Hash(item.SavePath) == Hash(TestServer.HlsBytes), "hls: joined bytes match");
    }

    private static AppSettings Settings(string root, string name) => new()
    {
        DownloadFolder = Path.Combine(root, name, "downloads"),
        TempFolder = Path.Combine(root, name, "parts"),
        DefaultConnections = 4,
        MaxConcurrentDownloads = 3,
        RetryAutomatically = false
    };

    private static async Task Wait(Func<bool> condition, int seconds, string label)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(100);
        }
        Check(false, $"{label}: timed out after {seconds}s");
    }

    private static string Hash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static string Hash(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));

    private static void Check(bool ok, string message)
    {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + message);
        if (!ok) _failures++;
    }

    private static byte[] BuildData(int size)
    {
        var bytes = new byte[size];
        for (int i = 0; i < size; i++) bytes[i] = (byte)((i * 37 + 11) & 0xFF);
        return bytes;
    }
}

internal sealed class TestServer : IAsyncDisposable
{
    public static readonly byte[] HlsBytes = BuildHlsBytes();

    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;
    private readonly int _port;

    public string BaseUrl { get; }

    public TestServer()
    {
        _port = FreePort();
        BaseUrl = $"http://127.0.0.1:{_port}/";
        _listener.Prefixes.Add(BaseUrl);
        _listener.Start();
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    private async Task LoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener.IsListening)
        {
            HttpListenerContext context;
            try { context = await _listener.GetContextAsync(); }
            catch { return; }
            _ = Task.Run(() => HandleAsync(context, token));
        }
    }

    private static async Task HandleAsync(HttpListenerContext context, CancellationToken token)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";
            if (path == "/file.bin")
                await SendAsync(context, ProgramData.Data, supportsRanges: true, token);
            else if (path == "/slow.bin")
                await SendSlowAsync(context, ProgramData.Data, token);
            else if (path == "/no-range.bin")
                await SendAsync(context, ProgramData.SmallData, supportsRanges: false, token);
            else if (path == "/redirect")
            {
                context.Response.StatusCode = 302;
                context.Response.RedirectLocation = "/file.bin";
                context.Response.Close();
            }
            else if (path == "/hls/master.m3u8")
            {
                await SendTextAsync(context,
                    "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=1000000\nvariant.m3u8\n", token);
            }
            else if (path == "/hls/variant.m3u8")
            {
                var sb = new StringBuilder("#EXTM3U\n#EXT-X-VERSION:3\n#EXT-X-TARGETDURATION:4\n");
                for (int i = 0; i < 4; i++) sb.Append("#EXTINF:4.0,\nseg").Append(i).Append(".ts\n");
                sb.Append("#EXT-X-ENDLIST\n");
                await SendTextAsync(context, sb.ToString(), token);
            }
            else if (path.StartsWith("/hls/seg", StringComparison.Ordinal) && path.EndsWith(".ts"))
            {
                int index = path[8] - '0';
                var slice = new byte[200 * 1024];
                Array.Copy(HlsBytes, index * slice.Length, slice, 0, slice.Length);
                await SendAsync(context, slice, supportsRanges: false, token);
            }
            else
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
            }
        }
        catch
        {
            try { context.Response.Abort(); } catch { }
        }
    }

    private static async Task SendSlowAsync(HttpListenerContext context, byte[] data,
        CancellationToken token)
    {
        var (start, end) = Range(context, data.Length);
        SetRangeHeaders(context, data.Length, start, end, supportsRanges: true);
        context.Response.ContentType = "application/octet-stream";
        context.Response.ContentLength64 = end - start + 1;

        var buffer = new byte[32 * 1024];
        long position = start;
        while (position <= end && !token.IsCancellationRequested)
        {
            int count = (int)Math.Min(buffer.Length, end - position + 1);
            Buffer.BlockCopy(data, (int)position, buffer, 0, count);
            await context.Response.OutputStream.WriteAsync(buffer.AsMemory(0, count), token);
            await context.Response.OutputStream.FlushAsync(token);
            position += count;
            await Task.Delay(18, token);
        }
        context.Response.Close();
    }

    private static async Task SendAsync(HttpListenerContext context, byte[] data,
        bool supportsRanges, CancellationToken token)
    {
        var (start, end) = supportsRanges
            ? Range(context, data.Length)
            : (0, data.Length - 1);
        SetRangeHeaders(context, data.Length, start, end, supportsRanges);
        context.Response.ContentType = "application/octet-stream";
        context.Response.ContentLength64 = end - start + 1;
        await context.Response.OutputStream.WriteAsync(
            data.AsMemory(start, end - start + 1), token);
        context.Response.Close();
    }

    private static async Task SendTextAsync(HttpListenerContext context, string text,
        CancellationToken token)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        context.Response.ContentType = "application/vnd.apple.mpegurl";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes, token);
        context.Response.Close();
    }

    private static (int Start, int End) Range(HttpListenerContext context, int length)
    {
        var header = context.Request.Headers["Range"];
        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
            return (0, length - 1);

        var value = header["bytes=".Length..];
        var parts = value.Split('-', 2);
        if (!int.TryParse(parts[0], out var start) || start < 0 || start >= length)
            return (0, length - 1);
        int end = length - 1;
        if (parts.Length > 1 && int.TryParse(parts[1], out var requested) && requested >= start)
            end = Math.Min(requested, length - 1);
        return (start, end);
    }

    private static void SetRangeHeaders(HttpListenerContext context, int length, int start, int end,
        bool supportsRanges)
    {
        if (!supportsRanges) return;
        context.Response.StatusCode = 206;
        context.Response.Headers["Accept-Ranges"] = "bytes";
        context.Response.Headers["Content-Range"] = $"bytes {start}-{end}/{length}";
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { _listener.Stop(); } catch { }
        try { await _loop; } catch { }
        _listener.Close();
        _cts.Dispose();
    }

    private static byte[] BuildHlsBytes()
    {
        var result = new byte[4 * 200 * 1024];
        for (int i = 0; i < result.Length; i++)
            result[i] = (byte)((i * 13 + 5) & 0xFF);
        return result;
    }
}

internal static class ProgramData
{
    public static readonly byte[] Data = Build(4 * 1024 * 1024);
    public static readonly byte[] SmallData = Build(2 * 1024 * 1024);

    private static byte[] Build(int size)
    {
        var bytes = new byte[size];
        for (int i = 0; i < size; i++) bytes[i] = (byte)((i * 37 + 11) & 0xFF);
        return bytes;
    }
}
