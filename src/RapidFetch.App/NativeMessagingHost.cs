using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using RapidFetch.Core.Services;

namespace RapidFetch.App;

/// <summary>
/// Chrome/Edge native-messaging endpoint. Protocol: 4-byte little-endian length
/// followed by a UTF-8 JSON document in each direction.
/// </summary>
internal static class NativeMessagingHost
{
    private const int MaxMessage = 8 * 1024 * 1024;
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(2);

    public static async Task RunAsync(string[] args)
    {
        try
        {
            using var stdin = Console.OpenStandardInput();
            using var stdout = Console.OpenStandardOutput();

            while (true)
            {
                var header = new byte[4];
                var read = ReadExactAsync(stdin, header);
                if (await Task.WhenAny(read, Task.Delay(IdleTimeout)).ConfigureAwait(false) != read)
                    break;
                if (!await read.ConfigureAwait(false)) break;
                int length = BitConverter.ToInt32(header, 0);
                if (length <= 0 || length > MaxMessage) break;

                var payload = new byte[length];
                if (!await ReadExactAsync(stdin, payload).ConfigureAwait(false)) break;

                string request = Encoding.UTF8.GetString(payload);
                string response = await HandleAsync(request).ConfigureAwait(false);
                var bytes = Encoding.UTF8.GetBytes(response);
                await stdout.WriteAsync(BitConverter.GetBytes(bytes.Length)).ConfigureAwait(false);
                await stdout.WriteAsync(bytes).ConfigureAwait(false);
                await stdout.FlushAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            try
            {
                var message = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { ok = false, error = ex.Message }));
                Console.OpenStandardOutput().Write(BitConverter.GetBytes(message.Length));
            }
            catch { }
        }
    }

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset)).ConfigureAwait(false);
            if (read <= 0) return false;
            offset += read;
        }
        return true;
    }

    private static async Task<string> HandleAsync(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string cmd = root.TryGetProperty("cmd", out var c) ? c.GetString() ?? "ping" : "ping";

            if (cmd == "ping")
            {
                var current = SettingsStore.Load(SystemIntegration.SettingsPath);
                return JsonSerializer.Serialize(new
                {
                    ok = true,
                    app = "RapidFetch",
                    version = ProgramInfo.Version,
                    videoSites = current.VideoSitesEnabled,
                    ytdlp = YtDlp.Locate(current) is not null,
                    ffmpeg = YtDlp.LocateFfmpeg(current) is not null,
                    quality = YtDlp.NormalizeQuality(current.YtDlpQuality)
                });
            }

            var settings = SettingsStore.Load(SystemIntegration.SettingsPath);
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            string baseUrl = $"http://127.0.0.1:{settings.BrowserPort}";

            string body = cmd switch
            {
                "add" => JsonSerializer.Serialize(new
                {
                    url = Get(root, "url"),
                    referrer = Get(root, "referrer"),
                    fileName = Get(root, "fileName"),
                    userAgent = Get(root, "userAgent"),
                    category = Get(root, "category"),
                    quality = Get(root, "quality"),
                    source = Get(root, "source"),
                    startNow = root.TryGetProperty("startNow", out var s) ? s.GetBoolean() : true
                }),
                "formats" => JsonSerializer.Serialize(new { url = Get(root, "url") }),
                _ => json
            };

            string endpoint = cmd switch
            {
                "add" => "/add",
                "formats" => "/formats",
                "pause" => "/pause",
                "resume" => "/resume",
                "remove" => "/remove",
                "reveal" => "/reveal",
                "show" => "/show",
                "list" => "/status",
                _ => "/status"
            };

            HttpResponseMessage? response = null;
            try
            {
                response = cmd == "list"
                    ? await client.GetAsync(baseUrl + endpoint).ConfigureAwait(false)
                    : await client.PostAsync(baseUrl + endpoint,
                        new StringContent(body, Encoding.UTF8, "application/json")).ConfigureAwait(false);
            }
            catch
            {
                await EnsureRunningAsync().ConfigureAwait(false);
                for (int i = 0; i < 20 && response is null; i++)
                {
                    await Task.Delay(250).ConfigureAwait(false);
                    try
                    {
                        response = cmd == "list"
                            ? await client.GetAsync(baseUrl + endpoint).ConfigureAwait(false)
                            : await client.PostAsync(baseUrl + endpoint,
                                new StringContent(body, Encoding.UTF8, "application/json")).ConfigureAwait(false);
                    }
                    catch { }
                }
            }

            if (response is null)
                return JsonSerializer.Serialize(new { ok = false, error = "RapidFetch is not running" });

            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { ok = false, error = ex.Message });
        }
    }

    private static string? Get(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static async Task EnsureRunningAsync()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;
            var info = new ProcessStartInfo(exe, "--minimized") { UseShellExecute = true };
            Process.Start(info);
            await Task.CompletedTask;
        }
        catch { }
    }
}
