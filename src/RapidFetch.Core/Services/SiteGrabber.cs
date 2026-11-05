using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using RapidFetch.Core.Engine;
using RapidFetch.Core.Models;
using RapidFetch.Core.Util;

namespace RapidFetch.Core.Services;

public sealed record GrabberOptions
{
    public required string StartUrl { get; init; }
    public int MaxDepth { get; init; } = 3;
    public int MaxPages { get; init; } = 200;
    public bool SameHostOnly { get; init; } = true;
    public bool FollowLinks { get; init; } = true;
    public bool DownloadFiles { get; init; } = true;
    public string IncludePattern { get; init; } = "";
    public string ExcludePattern { get; init; } = "";
    public string Extensions { get; init; } = "zip;rar;7z;pdf;mp3;mp4;mkv;iso;exe;apk;docx;xlsx;pptx";
    public bool RespectRobots { get; init; }
    public int DelayMs { get; init; } = 120;
    public string? Referrer { get; init; }
}

public sealed record GrabberProgress(int PagesScanned, int LinksFound, string CurrentUrl, bool Done, string? Error = null);

public sealed class SiteGrabber
{
    private static readonly Regex HrefRegex = new(@"href\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SrcRegex = new(@"src\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CssUrlRegex = new(@"url\(\s*[""']?([^""')]+)[""']?\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BaseHrefRegex = new(@"<base\s+href\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HttpClient _client;

    public SiteGrabber(AppSettings? settings = null)
    {
        _client = HttpProbe.CreateClient(settings);
    }

    public async Task CrawlAsync(GrabberOptions options, IProgress<GrabberProgress> progress,
        Action<string, string?> onFile, CancellationToken token)
    {
        try
        {
            var start = new Uri(options.StartUrl);
            var extensions = options.Extensions
                .Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(e => e.TrimStart('.').ToLowerInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var include = string.IsNullOrWhiteSpace(options.IncludePattern)
                ? null : new Regex(options.IncludePattern, RegexOptions.IgnoreCase);
            var exclude = string.IsNullOrWhiteSpace(options.ExcludePattern)
                ? null : new Regex(options.ExcludePattern, RegexOptions.IgnoreCase);

            var pageQueue = new Queue<(Uri Url, int Depth)>();
            pageQueue.Enqueue((start, 0));
            var seenPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { start.ToString() };
            var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int pages = 0, files = 0;

            while (pageQueue.Count > 0 && pages < options.MaxPages)
            {
                token.ThrowIfCancellationRequested();
                var (page, depth) = pageQueue.Dequeue();
                progress.Report(new GrabberProgress(pages, files, page.ToString(), false));

                string html;
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, page);
                    if (!string.IsNullOrWhiteSpace(options.Referrer))
                        request.Headers.Referrer = new Uri(options.Referrer);
                    using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseContentRead, token)
                        .ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode) continue;
                    var type = response.Content.Headers.ContentType?.MediaType ?? "";
                    if (!type.Contains("html", StringComparison.OrdinalIgnoreCase)) continue;
                    html = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch { continue; }

                pages++;
                var baseUri = page;
                var baseMatch = BaseHrefRegex.Match(html);
                if (baseMatch.Success && Uri.TryCreate(page, baseMatch.Groups[1].Value, out var b)) baseUri = b;

                foreach (var raw in ExtractLinks(html))
                {
                    token.ThrowIfCancellationRequested();
                    if (!Uri.TryCreate(baseUri, raw, out var link)) continue;
                    if (link.Scheme is not ("http" or "https")) continue;
                    if (options.SameHostOnly && !string.Equals(link.Host, start.Host, StringComparison.OrdinalIgnoreCase)) continue;
                    if (exclude is not null && exclude.IsMatch(link.ToString())) continue;

                    var ext = Path.GetExtension(link.AbsolutePath).TrimStart('.').ToLowerInvariant();
                    bool isFile = extensions.Contains(ext);

                    if (isFile || (!options.FollowLinks && ext.Length > 0 && !LooksLikePage(link)))
                    {
                        if (!include?.IsMatch(link.ToString()) ?? false) continue;
                        if (!seenFiles.Add(link.ToString())) continue;
                        files++;
                        onFile(link.ToString(), page.ToString());
                        progress.Report(new GrabberProgress(pages, files, link.ToString(), false));
                    }
                    else if (options.FollowLinks && depth < options.MaxDepth)
                    {
                        if (seenPages.Add(link.ToString()))
                            pageQueue.Enqueue((link, depth + 1));
                    }
                }

                if (options.DelayMs > 0)
                    await Task.Delay(options.DelayMs, token).ConfigureAwait(false);
            }

            progress.Report(new GrabberProgress(pages, files, "", true));
        }
        catch (OperationCanceledException)
        {
            progress.Report(new GrabberProgress(0, 0, "", true, "Canceled"));
        }
        catch (Exception ex)
        {
            progress.Report(new GrabberProgress(0, 0, "", true, ex.Message));
        }
    }

    private static IEnumerable<string> ExtractLinks(string html)
    {
        foreach (Match m in HrefRegex.Matches(html)) yield return WebUtility.HtmlDecode(m.Groups[1].Value);
        foreach (Match m in SrcRegex.Matches(html)) yield return WebUtility.HtmlDecode(m.Groups[1].Value);
        foreach (Match m in CssUrlRegex.Matches(html)) yield return WebUtility.HtmlDecode(m.Groups[1].Value);
    }

    private static bool LooksLikePage(Uri uri)
    {
        var path = uri.AbsolutePath;
        return path.EndsWith('/') || !Path.HasExtension(path);
    }
}
