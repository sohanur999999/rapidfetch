using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using RapidFetch.Core.Models;

namespace RapidFetch.Core.Services;

public sealed record YtDlpInfo(string Title, double Duration);

public sealed record YtDlpResult(bool Ok, string? FilePath, string? Error);

/// <summary>One quality choice shown by the browser media panel.</summary>
public sealed record YtDlpFormat(
    string Quality, string Label, string Ext, long Size, int Height, double Fps, string Kind);

public sealed record YtDlpFormats(
    string Title, double Duration, IReadOnlyList<YtDlpFormat> Formats);

/// <summary>
/// Optional external engine for video sites whose player hands out separate
/// audio and video tracks (YouTube and friends). RapidFetch keeps the queue,
/// categories and UI; yt-dlp resolves the page and muxes the result.
/// </summary>
public static class YtDlp
{
    public const string DownloadUrl =
        "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";

    private static readonly string[] AudioQuality = { "audio" };

#pragma warning disable CA1416 // RapidFetch is a Windows application; Core is shared for tests.
    public static string ToolsFolder => Path.Combine(SystemIntegration.AppDataDir, "tools");
    public static string ManagedExePath => Path.Combine(ToolsFolder, "yt-dlp.exe");
#pragma warning restore CA1416

    // ------------------------------------------------------------- discovery

    public static string? Locate(AppSettings settings) => Locate(settings.YtDlpPath);

    public static string? Locate(string? configured)
    {
        foreach (var candidate in Candidates(configured, "yt-dlp.exe"))
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
        return FindOnPath("yt-dlp.exe");
    }

    public static string? LocateFfmpeg(AppSettings settings)
    {
        var configured = settings.FfmpegPath;
        foreach (var candidate in Candidates(configured, "ffmpeg.exe"))
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        foreach (var candidate in new[]
                 {
                     Path.Combine(local, "Microsoft", "WinGet", "Links", "ffmpeg.exe"),
                     Path.Combine(AppDir, "ffmpeg", "bin", "ffmpeg.exe")
                 })
            if (File.Exists(candidate)) return candidate;

        return FindOnPath("ffmpeg.exe");
    }

    private static IEnumerable<string> Candidates(string? configured, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var value = Environment.ExpandEnvironmentVariables(configured.Trim().Trim('"'));
            yield return Directory.Exists(value) ? Path.Combine(value, fileName) : value;
        }

        yield return Path.Combine(AppDir, fileName);
        yield return Path.Combine(AppDir, "tools", fileName);
        yield return Path.Combine(ToolsFolder, fileName);
    }

    private static string AppDir =>
        Path.GetDirectoryName(Environment.ProcessPath ?? "") is { Length: > 0 } dir
            ? dir
            : AppContext.BaseDirectory;

    private static string? FindOnPath(string fileName)
    {
        foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "")
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(folder.Trim().Trim('"'), fileName);
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            }
            catch { /* malformed PATH entry */ }
        }
        return null;
    }

    // ------------------------------------------------------------ url rules

    /// <summary>True for page URLs that need yt-dlp rather than a file fetch.</summary>
    public static bool IsVideoSite(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme is not ("http" or "https")) return false;

        var host = uri.Host.ToLowerInvariant().TrimEnd('.');
        var path = uri.AbsolutePath.ToLowerInvariant();

        if (host == "youtu.be") return path.Length > 1;
        if (host == "youtube.com" || host.EndsWith(".youtube.com"))
            return path.StartsWith("/watch") || path.StartsWith("/shorts/")
                || path.StartsWith("/live/") || path.StartsWith("/embed/")
                || path.StartsWith("/playlist");
        if (host == "vimeo.com" || host.EndsWith(".vimeo.com"))
            return path.Length > 1 && char.IsDigit(path.TrimStart('/').FirstOrDefault());
        if (host is "dailymotion.com" or "dai.ly" || host.EndsWith(".dailymotion.com"))
            return host == "dai.ly" ? path.Length > 1 : path.Contains("/video/");
        if (host == "soundcloud.com" || host.EndsWith(".soundcloud.com"))
            return path.Length > 1;
        if (host == "bandcamp.com" || host.EndsWith(".bandcamp.com"))
            return path.Contains("/track/") || path.Contains("/album/");
        if (host == "twitch.tv" || host.EndsWith(".twitch.tv"))
            return path.StartsWith("/videos/") || path.Contains("/clip/");
        if (host == "bilibili.com" || host.EndsWith(".bilibili.com"))
            return path.StartsWith("/video/");
        if (host == "fb.watch")
            return path.Length > 1;
        if (host == "facebook.com" || host.EndsWith(".facebook.com"))
            return path.StartsWith("/watch") || path.StartsWith("/reel/")
                || path.StartsWith("/share/v/") || path.StartsWith("/share/r/")
                || path.StartsWith("/share/reel/") || path.StartsWith("/video.php")
                || path.Contains("/videos/");
        return false;
    }

    public static bool IsAudioQuality(string? quality) =>
        quality is not null && AudioQuality.Contains(quality, StringComparer.OrdinalIgnoreCase);

    public static string NormalizeQuality(string? quality) => quality?.Trim().ToLowerInvariant() switch
    {
        "audio" => "audio",
        "best" => "best",
        var value when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture,
            out var height) && height is >= 144 and <= 4320 =>
            height.ToString(CultureInfo.InvariantCulture),
        _ => "best"
    };

    public static bool IsPermanentError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        return message.Contains("sign in", StringComparison.OrdinalIgnoreCase)
            || message.Contains("not installed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("does not support", StringComparison.OrdinalIgnoreCase)
            || message.Contains("unavailable", StringComparison.OrdinalIgnoreCase)
            || message.Contains("private", StringComparison.OrdinalIgnoreCase);
    }

    // --------------------------------------------------------------- probe

    public static async Task<YtDlpInfo> ProbeAsync(string exe, string url, AppSettings settings,
        CancellationToken token)
    {
        var args = new List<string>
        {
            settings.YtDlpPlaylist ? "--yes-playlist" : "--no-playlist",
            "--no-warnings", "--no-colors", "--skip-download"
        };
        AddNetworkArgs(args, settings);
        args.Add("--print");
        args.Add("%(title)s");
        args.Add("--print");
        args.Add("%(duration)s");
        args.Add(url);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));

        var run = await RunAsync(exe, args, timeout.Token).ConfigureAwait(false);
        if (run.ExitCode != 0)
            throw new IOException(Friendly(run.Error, "yt-dlp could not read this page."));

        var lines = run.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var title = lines.FirstOrDefault(l => l.Length > 0);
        if (string.IsNullOrWhiteSpace(title) || title == "NA")
            throw new IOException("yt-dlp found no downloadable video on this page.");

        double duration = 0;
        var durationLine = lines.Skip(1).FirstOrDefault();
        if (durationLine is not null &&
            double.TryParse(durationLine, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            duration = parsed;

        return new YtDlpInfo(title.Trim(), duration);
    }

    /// <summary>
    /// Reads every advertised format so the in-page panel can show real
    /// qualities (720p60, 480p, audio, and so on) instead of a fixed list.
    /// </summary>
    public static async Task<YtDlpFormats> ListFormatsAsync(
        string exe, string url, AppSettings settings, CancellationToken token)
    {
        var args = new List<string>
        {
            "--no-playlist", "--no-warnings", "--no-colors",
            "--skip-download", "--dump-single-json"
        };
        AddNetworkArgs(args, settings);
        args.Add(url);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));

        var run = await RunAsync(exe, args, timeout.Token).ConfigureAwait(false);
        if (run.ExitCode != 0)
            throw new IOException(Friendly(run.Error, "yt-dlp could not read this page."));

        try
        {
            using var document = JsonDocument.Parse(run.Output);
            var root = document.RootElement;
            var title = ReadString(root, "title");
            var duration = ReadDouble(root, "duration");
            var formats = new List<YtDlpFormat>();

            if (root.TryGetProperty("formats", out var list) &&
                list.ValueKind == JsonValueKind.Array)
            {
                var videos = new Dictionary<int, FormatCandidate>();
                FormatCandidate? audio = null;

                foreach (var format in list.EnumerateArray())
                {
                    var id = ReadString(format, "format_id");
                    var ext = ReadString(format, "ext");
                    if (string.IsNullOrWhiteSpace(id) || ext is "mhtml" or "storyboard")
                        continue;

                    var vcodec = ReadString(format, "vcodec");
                    var acodec = ReadString(format, "acodec");
                    var height = ReadInt(format, "height");
                    var fps = ReadDouble(format, "fps");
                    var size = ReadLong(format, "filesize");
                    if (size <= 0) size = ReadLong(format, "filesize_approx");
                    var tbr = ReadDouble(format, "tbr");
                    var hasVideo = vcodec is not ("" or "none");
                    var hasAudio = acodec is not ("" or "none");

                    if (hasVideo && height > 0)
                    {
                        var candidate = new FormatCandidate(ext, height, fps, size, tbr, hasAudio);
                        if (!videos.TryGetValue(height, out var current) ||
                            candidate.Score > current.Score)
                            videos[height] = candidate;
                    }

                    if (!hasVideo && hasAudio)
                    {
                        var candidate = new FormatCandidate(ext, 0, 0, size, tbr, true);
                        if (audio is null || candidate.Score > audio.Score)
                            audio = candidate;
                    }
                }

                var audioBytes = Estimate(audio?.Size ?? 0, audio?.Tbr ?? 0, duration);
                foreach (var pair in videos.OrderByDescending(pair => pair.Key))
                {
                    var candidate = pair.Value;
                    var candidateBytes = Estimate(candidate.Size, candidate.Tbr, duration);
                    var total = candidateBytes > 0
                        ? candidateBytes + (candidate.HasAudio ? 0 : audioBytes)
                        : 0;
                    formats.Add(new YtDlpFormat(
                        pair.Key.ToString(CultureInfo.InvariantCulture),
                        QualityLabel(candidate.Height, candidate.Fps),
                        candidate.Ext, total, candidate.Height, candidate.Fps, "video"));
                }

                if (audio is not null)
                    formats.Add(new YtDlpFormat("audio", "Audio only", audio.Ext,
                        Estimate(audio.Size, audio.Tbr, duration), 0, 0, "audio"));

                if (formats.Count == 0)
                    formats.Add(new YtDlpFormat("best", "Best available", "mp4", 0, 0, 0, "video"));
            }
            else
            {
                formats.Add(new YtDlpFormat("best", "Best available", "mp4", 0, 0, 0, "video"));
            }

            return new YtDlpFormats(string.IsNullOrWhiteSpace(title) ? "This video" : title.Trim(),
                duration, formats);
        }
        catch (JsonException ex)
        {
            throw new IOException("yt-dlp returned unreadable video information.", ex);
        }
    }

    private sealed record FormatCandidate(
        string Ext, int Height, double Fps, long Size, double Tbr, bool HasAudio)
    {
        public double Score => Tbr
            + (Ext.Equals("mp4", StringComparison.OrdinalIgnoreCase) ? 100 : 0)
            + (Fps >= 50 ? 60 : 0)
            + (HasAudio ? 20 : 0);
    }

    /// <summary>Uses the reported size, or estimates from bitrate and duration.</summary>
    private static long Estimate(long size, double tbr, double duration)
    {
        if (size > 0) return size;
        if (tbr <= 0 || duration <= 0) return 0;
        return (long)(tbr * 1000 / 8 * duration);
    }

    private static string QualityLabel(int height, double fps)
    {
        var label = height switch
        {
            >= 2160 => "2160p 4K",
            >= 1440 => "1440p 2K",
            >= 1080 => "1080p HD",
            >= 720 => "720p HD",
            _ => height + "p"
        };

        if (fps >= 50) label += " 60 fps";
        else if (fps >= 45) label += " 50 fps";
        else if (fps >= 28) label += " 30 fps";
        return label;
    }

    private static string ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static int ReadInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : 0;

    private static long ReadLong(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.Number) return 0;
        if (value.TryGetInt64(out var number)) return number;
        return value.TryGetDouble(out var approximate) ? (long)approximate : 0;
    }

    private static double ReadDouble(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)
            ? number
            : 0;

    public static async Task<string> VersionAsync(string exe, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var run = await RunAsync(exe, new[] { "--version" }, timeout.Token).ConfigureAwait(false);
        return run.ExitCode == 0 ? run.Output.Trim().Split('\n').FirstOrDefault() ?? "" : "";
    }

    // ------------------------------------------------------------ download

    public static async Task<YtDlpResult> DownloadAsync(
        string exe, DownloadItem item, AppSettings settings, string basePath,
        Action<long, long> progress, Action<string>? log, CancellationToken token)
    {
        var args = BuildDownloadArgs(item, settings, basePath);
        var psi = CreateStartInfo(exe, args);

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stderr = new StringBuilder();
        var gate = new object();
        string? filePath = null;
        var trackDone = new Dictionary<string, long>(StringComparer.Ordinal);
        string? currentTrack = null;
        long currentDownloaded = 0;
        long completed = 0;

        void Update(long downloaded, long total)
        {
            lock (gate)
            {
                currentDownloaded = downloaded;
                var done = completed + downloaded;
                var all = total > 0 ? completed + total : 0;
                try { progress(done, all); } catch { }
            }
        }

        void StartTrack(string name)
        {
            lock (gate)
            {
                // yt-dlp downloads each track separately. Freeze the bytes the
                // finished track actually wrote (not its estimated total, which
                // can be wildly wrong) and start the next track from zero.
                if (currentTrack is not null && currentDownloaded > 0)
                    trackDone[currentTrack] = Math.Max(
                        trackDone.TryGetValue(currentTrack, out var known) ? known : 0,
                        currentDownloaded);

                currentTrack = name;
                currentDownloaded = 0;
                completed = trackDone.Values.Sum();
            }
        }

        process.OutputDataReceived += (_, e) =>
        {
            var line = e.Data;
            if (string.IsNullOrWhiteSpace(line)) return;

            if (line.StartsWith("RF|", StringComparison.Ordinal))
            {
                var parts = line.Split('|');
                if (parts.Length < 3) return;
                var downloaded = ParseNumber(parts[1]);
                var total = ParseNumber(parts[2]);
                if (total <= 0) total = ParseNumber(parts[3]);
                Update(downloaded, total);
                return;
            }

            if (line.StartsWith("RF-START|", StringComparison.Ordinal))
            {
                StartTrack(line["RF-START|".Length..].Trim());
                return;
            }

            if (line.StartsWith("RF-FILE|", StringComparison.Ordinal))
            {
                filePath = line["RF-FILE|".Length..].Trim();
                return;
            }

            if (line.StartsWith("ERROR:", StringComparison.Ordinal))
                lock (gate) stderr.AppendLine(line);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return;
            lock (gate) stderr.AppendLine(e.Data.Trim());
        };

        using var registration = token.Register(() => TryKill(process));

        try
        {
            if (!process.Start()) return new YtDlpResult(false, null, "yt-dlp could not be started.");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        if (token.IsCancellationRequested) throw new OperationCanceledException(token);

        string errorText;
        lock (gate) errorText = stderr.ToString();

        if (process.ExitCode != 0)
            return new YtDlpResult(false, null,
                Friendly(errorText, $"yt-dlp exited with code {process.ExitCode}."));

        var output = filePath;
        if (string.IsNullOrWhiteSpace(output) || !File.Exists(output))
            output = FindOutput(basePath);

        if (output is null || !File.Exists(output))
            return new YtDlpResult(false, null, "yt-dlp finished but the output file was not found.");

        log?.Invoke($"yt-dlp finished: {output}");
        return new YtDlpResult(true, output, null);
    }

    private static List<string> BuildDownloadArgs(DownloadItem item, AppSettings settings, string basePath)
    {
        var quality = NormalizeQuality(item.Quality ?? settings.YtDlpQuality);
        var ffmpeg = LocateFfmpeg(settings);
        var audio = quality == "audio";
        var height = int.TryParse(quality, NumberStyles.Integer, CultureInfo.InvariantCulture,
            out var parsedHeight) ? parsedHeight : 0;
        var limit = settings.SpeedLimitEnabled ? settings.SpeedLimitBytesPerSecond : 0;

        string format;
        if (audio)
            format = "ba[ext=m4a]/ba";
        else if (ffmpeg is not null)
            format = height > 0
                ? $"bv*[ext=mp4][height<={height}]+ba[ext=m4a]/bv*[height<={height}]+ba/b[height<={height}]/bv*+ba/b"
                : "bv*[ext=mp4]+ba[ext=m4a]/bv*+ba/b";
        else
            format = height > 0
                ? $"b[height<={height}][acodec!=none][vcodec!=none]/b[acodec!=none][vcodec!=none]"
                : "b[acodec!=none][vcodec!=none]/b";

        var args = new List<string>
        {
            settings.YtDlpPlaylist ? "--yes-playlist" : "--no-playlist",
            "--newline", "--no-colors", "--no-warnings", "--no-simulate", "--progress",
            "--windows-filenames", "--trim-filenames", "150",
            "-o", basePath + ".%(ext)s",
            "--progress-template",
            "download:RF|%(progress.downloaded_bytes)s|%(progress.total_bytes)s|%(progress.total_bytes_estimate)s|%(progress.speed)s",
            "--print", "before_dl:RF-START|%(filename)s",
            "--print", "after_move:RF-FILE|%(filepath)s",
            "-f", format
        };

        if (!audio && ffmpeg is not null)
        {
            args.Add("--merge-output-format");
            args.Add("mp4");
        }

        if (ffmpeg is not null)
        {
            args.Add("--ffmpeg-location");
            args.Add(ffmpeg);
        }

        var connections = Math.Clamp(item.Connections, 1, 32);
        if (connections > 1 && limit <= 0)
        {
            args.Add("-N");
            args.Add(connections.ToString(CultureInfo.InvariantCulture));
        }

        if (limit > 0)
        {
            args.Add("--limit-rate");
            args.Add(limit.ToString(CultureInfo.InvariantCulture));
        }

        AddNetworkArgs(args, settings);
        args.Add(item.Url);
        return args;
    }

    private static void AddNetworkArgs(List<string> args, AppSettings settings)
    {
        if (settings.ProxyEnabled && !string.IsNullOrWhiteSpace(settings.ProxyHost))
        {
            var scheme = settings.ProxyType?.ToLowerInvariant() switch
            {
                "socks4" => "socks4",
                "socks5" => "socks5",
                _ => "http"
            };
            var auth = "";
            if (!string.IsNullOrWhiteSpace(settings.ProxyUsername))
                auth = Uri.EscapeDataString(settings.ProxyUsername) + ":" +
                       Uri.EscapeDataString(settings.ProxyPassword ?? "") + "@";
            args.Add("--proxy");
            args.Add($"{scheme}://{auth}{settings.ProxyHost}:{settings.ProxyPort}");
        }

        var cookies = settings.YtDlpCookies?.Trim();
        if (!string.IsNullOrWhiteSpace(cookies) && !cookies.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            args.Add("--cookies-from-browser");
            args.Add(cookies);
        }
    }

    private static ProcessStartInfo CreateStartInfo(string exe, IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        psi.Environment["PYTHONUTF8"] = "1";
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        return psi;
    }

    private sealed record RunResult(int ExitCode, string Output, string Error);

    private static async Task<RunResult> RunAsync(string exe, IEnumerable<string> args, CancellationToken token)
    {
        var psi = CreateStartInfo(exe, args);
        using var process = new Process { StartInfo = psi };

        try
        {
            if (!process.Start())
                return new RunResult(-1, "", "yt-dlp could not be started.");

            var stdout = process.StandardOutput.ReadToEndAsync(token);
            var stderr = process.StandardError.ReadToEndAsync(token);
            await process.WaitForExitAsync(token).ConfigureAwait(false);

            return new RunResult(process.ExitCode,
                await stdout.ConfigureAwait(false),
                await stderr.ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
    }

    private static string? FindOutput(string basePath)
    {
        try
        {
            var dir = Path.GetDirectoryName(basePath);
            var name = Path.GetFileName(basePath);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return null;

            return Directory.EnumerateFiles(dir, name + ".*")
                .Where(file => !file.EndsWith(".part", StringComparison.OrdinalIgnoreCase)
                               && !file.EndsWith(".ytdl", StringComparison.OrdinalIgnoreCase)
                               && !file.EndsWith(".temp", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static long ParseNumber(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) &&
        number > 0 && number < long.MaxValue
            ? (long)number
            : 0;

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch { }
    }

    private static string Friendly(string stderr, string fallback)
    {
        var text = stderr ?? "";

        if (text.Contains("Sign in to confirm", StringComparison.OrdinalIgnoreCase))
            return "This connection was asked to sign in (YouTube bot check). In Options > Video sites, " +
                   "choose your browser under Cookies from browser, then try again.";

        if (text.Contains("Unsupported URL", StringComparison.OrdinalIgnoreCase))
            return "yt-dlp does not support this address.";

        if (text.Contains("Video unavailable", StringComparison.OrdinalIgnoreCase))
            return "The video is unavailable (private, removed or region-locked).";

        if (text.Contains("ffmpeg", StringComparison.OrdinalIgnoreCase))
            return "ffmpeg is needed to merge the selected quality. Install ffmpeg or choose a single-file quality.";

        var errors = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("ERROR:", StringComparison.Ordinal))
            .ToList();

        return errors.Count > 0 ? errors[^1] : fallback;
    }

    // -------------------------------------------------------------- install

    public static async Task<string> InstallAsync(IProgress<double>? progress, CancellationToken token)
    {
        Directory.CreateDirectory(ToolsFolder);
        var temp = ManagedExePath + ".download";

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        using var response = await client.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, token)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1;
        long read = 0;

        await using (var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
        await using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None,
                         81920, useAsync: true))
        {
            var buffer = new byte[81920];
            int count;
            while ((count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
                read += count;
                if (total > 0) progress?.Report((double)read / total);
            }
        }

        if (read < 1024 * 1024)
            throw new IOException("The downloaded yt-dlp file looks incomplete. Try again.");

        if (File.Exists(ManagedExePath))
        {
            var backup = ManagedExePath + ".old";
            try { File.Delete(backup); } catch { }
            try { File.Move(ManagedExePath, backup); } catch { }
        }

        File.Move(temp, ManagedExePath, overwrite: true);

        var version = await VersionAsync(ManagedExePath, token).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(version))
            throw new IOException("yt-dlp downloaded but did not run. Your antivirus may have blocked it.");
        return version;
    }
}
