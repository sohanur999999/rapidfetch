using System.Net;
using System.Net.Http.Headers;
using RapidFetch.Core.Models;
using RapidFetch.Core.Util;

namespace RapidFetch.Core.Engine;

public sealed record ProbeResult(
    bool Ok,
    string? Error,
    long TotalBytes,
    bool AcceptRanges,
    string? FileName,
    string? ContentType,
    string Url)
{
    public static ProbeResult Fail(string url, string error) => new(false, error, 0, false, null, null, url);
}

public static class HttpProbe
{
    public static HttpClient CreateClient(AppSettings? settings = null)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.None,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 12,
            ConnectTimeout = TimeSpan.FromSeconds(20),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            UseCookies = true,
            CookieContainer = new CookieContainer()
        };

        if (settings is not null)
            handler.Proxy = new SettingsProxy(settings);

        var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36");
        client.DefaultRequestHeaders.Accept.ParseAdd("*/*");
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("identity");
        return client;
    }

    public static async Task<ProbeResult> ProbeAsync(HttpClient client, string url, DownloadItem? item, CancellationToken token)
    {
        try
        {
            using var get = new HttpRequestMessage(HttpMethod.Get, url);
            ApplyHeaders(get, item);
            get.Headers.Range = new RangeHeaderValue(0, 0);

            using var response = await client.SendAsync(get, HttpCompletionOption.ResponseHeadersRead, token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.PartialContent)
                return ProbeResult.Fail(url, $"Server returned {(int)response.StatusCode} {response.ReasonPhrase}");

            long total = 0;
            bool ranges = response.StatusCode == HttpStatusCode.PartialContent;
            var contentRange = response.Content.Headers.ContentRange;
            if (contentRange?.Length is > 0) total = contentRange.Length.Value;
            else total = response.Content.Headers.ContentLength ?? 0;

            if (!ranges && response.Content.Headers.ContentLength is > 1)
            {
                // Server ignored Range but did report a length; single-stream fallback.
                total = response.Content.Headers.ContentLength.Value;
            }

            var name = ResolveFileName(response, url);
            var type = response.Content.Headers.ContentType?.MediaType;
            return new ProbeResult(true, null, total, ranges, name, type, response.RequestMessage?.RequestUri?.ToString() ?? url);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ProbeResult.Fail(url, ex.Message);
        }
    }

    public static void ApplyHeaders(HttpRequestMessage request, DownloadItem? item)
    {
        if (item is null) return;
        if (!string.IsNullOrWhiteSpace(item.Referrer))
            request.Headers.Referrer = new Uri(item.Referrer);
        if (!string.IsNullOrWhiteSpace(item.UserAgent))
            request.Headers.TryAddWithoutValidation("User-Agent", item.UserAgent);
    }

    public static string ResolveFileName(HttpResponseMessage response, string url)
    {
        var disposition = response.Content.Headers.ContentDisposition;
        var candidate = disposition?.FileNameStar ?? disposition?.FileName;
        if (!string.IsNullOrWhiteSpace(candidate))
            candidate = candidate.Trim('"').Trim();

        if (string.IsNullOrWhiteSpace(candidate))
        {
            var media = response.Content.Headers.ContentType?.MediaType ?? "";
            var ext = ExtensionForMime(media);
            var path = url.Split('?', '#')[0];
            candidate = path.Split('/').LastOrDefault(s => s.Length > 0) ?? "download";
            if (!string.IsNullOrWhiteSpace(ext) && !candidate.Contains('.')) candidate += ext;
        }

        return PathUtil.Sanitize(candidate);
    }

    public static string ExtensionForMime(string mime) => mime.ToLowerInvariant() switch
    {
        "video/mp4" => ".mp4",
        "video/webm" => ".webm",
        "video/x-matroska" => ".mkv",
        "audio/mpeg" => ".mp3",
        "audio/aac" => ".aac",
        "audio/ogg" => ".ogg",
        "application/pdf" => ".pdf",
        "application/zip" => ".zip",
        "application/x-7z-compressed" => ".7z",
        "application/x-rar-compressed" => ".rar",
        "application/vnd.android.package-archive" => ".apk",
        "application/vnd.apple.mpegurl" => ".m3u8",
        "application/dash+xml" => ".mpd",
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        _ => ""
    };
}

/// <summary>
/// Reads the proxy configuration live, so changing Options affects the next
/// request without restarting the app or discarding cookies.
/// </summary>
public sealed class SettingsProxy : IWebProxy
{
    private readonly AppSettings _settings;

    public SettingsProxy(AppSettings settings) => _settings = settings;

    public ICredentials? Credentials
    {
        get => string.IsNullOrWhiteSpace(_settings.ProxyUsername)
            ? null
            : new NetworkCredential(_settings.ProxyUsername, _settings.ProxyPassword);
        set { /* credentials come from settings */ }
    }

    public Uri? GetProxy(Uri destination)
    {
        if (IsBypassed(destination)) return destination;

        var scheme = string.IsNullOrWhiteSpace(_settings.ProxyType)
            ? "http"
            : _settings.ProxyType.Trim().ToLowerInvariant();
        if (scheme is not ("http" or "https" or "socks4" or "socks4a" or "socks5"))
            scheme = "http";

        var host = _settings.ProxyHost.Trim();
        if (host.Contains("://", StringComparison.Ordinal) &&
            Uri.TryCreate(host, UriKind.Absolute, out var parsed))
            host = parsed.Host;

        if (!Uri.TryCreate($"{scheme}://{host}:{Math.Clamp(_settings.ProxyPort, 1, 65535)}",
                UriKind.Absolute, out var proxy))
            return destination;
        return proxy;
    }

    public bool IsBypassed(Uri host)
    {
        if (!_settings.ProxyEnabled || string.IsNullOrWhiteSpace(_settings.ProxyHost))
            return true;

        if (_settings.ProxyBypassLocal &&
            (host.IsLoopback ||
             host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
             host.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }
}
