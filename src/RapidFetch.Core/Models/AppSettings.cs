using System.Text.Json.Serialization;

namespace RapidFetch.Core.Models;

public sealed class CategoryRule
{
    public string Name { get; set; } = "General";
    public string Folder { get; set; } = "General";
    public List<string> Extensions { get; set; } = new();
}

public sealed class SchedulerOptions
{
    public bool Enabled { get; set; }
    public TimeSpan StartTime { get; set; } = new(1, 0, 0);
    public TimeSpan StopTime { get; set; } = new(7, 0, 0);
    public HashSet<DayOfWeek> Days { get; set; } = new(Enum.GetValues<DayOfWeek>());
    public long DailyLimitBytes { get; set; }
    public bool StopAfterLimit { get; set; } = true;
    public string OnQueueFinished { get; set; } = "none"; // none|exit|sleep|hibernate|shutdown
}

public sealed class AppSettings
{
    public string DownloadFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "RapidFetch");

    public string TempFolder { get; set; } = "";
    public int DefaultConnections { get; set; } = 8;
    public int MaxConcurrentDownloads { get; set; } = 3;
    public bool StartQueuedAutomatically { get; set; } = true;
    public bool MonitorClipboard { get; set; } = true;
    public bool ConfirmOnAdd { get; set; }
    public bool UseServerTimestamps { get; set; } = true;
    public bool RetryAutomatically { get; set; } = true;
    public int RetryCount { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 5;

    // Speed limiting
    public bool SpeedLimitEnabled { get; set; }
    public long SpeedLimitBytesPerSecond { get; set; } = 2 * 1024 * 1024;
    public bool TurboMode { get; set; } = true;

    // Network
    public bool ProxyEnabled { get; set; }
    public string ProxyType { get; set; } = "http"; // http|https|socks4|socks5
    public string ProxyHost { get; set; } = "";
    public int ProxyPort { get; set; } = 8080;
    public string ProxyUsername { get; set; } = "";
    public string ProxyPassword { get; set; } = "";
    public bool ProxyBypassLocal { get; set; } = true;

    // Video sites (optional yt-dlp engine)
    public bool VideoSitesEnabled { get; set; } = true;
    public string YtDlpPath { get; set; } = "";
    public string FfmpegPath { get; set; } = "";
    public bool YtDlpPlaylist { get; set; }
    public string YtDlpQuality { get; set; } = "1080"; // best|1080|720|480|audio
    public string YtDlpCookies { get; set; } = "none"; // none|edge|chrome|firefox|brave|opera|vivaldi

    // Interface
    public string Theme { get; set; } = "dark"; // dark|light|system
    public string Accent { get; set; } = "#0A84FF";
    public string SortOrder { get; set; } = "newest";
    // newest|oldest|name|name-desc|size|size-asc|status|status-desc|speed|speed-asc|eta|eta-desc
    public bool ShowDetailsPane { get; set; } = true;
    public bool ShowTransferWindow { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool NotifyOnComplete { get; set; } = true;
    public bool AutoUpdateExtension { get; set; } = true;

    // Browser integration
    public bool BrowserIntegrationEnabled { get; set; } = true;
    public int BrowserPort { get; set; } = 55123;
    public string BrowserHostName { get; set; } = "com.rapidfetch.host";
    public bool CaptureMediaFiles { get; set; } = true;
    public List<string> SkipHosts { get; set; } = new();

    // Shell / OS
    public bool ShellIntegration { get; set; }
    public bool RunAtStartup { get; set; }

    public SchedulerOptions Scheduler { get; set; } = new();
    public List<CategoryRule> Categories { get; set; } = DefaultCategories().ToList();

    [JsonIgnore] public string TempDirectory =>
        string.IsNullOrWhiteSpace(TempFolder) ? Path.Combine(DownloadFolder, ".parts") : TempFolder;

    public static IEnumerable<CategoryRule> DefaultCategories()
    {
        yield return new CategoryRule
        {
            Name = "Compressed", Folder = "Compressed",
            Extensions = { "zip", "rar", "7z", "tar", "gz", "bz2", "xz", "iso" }
        };
        yield return new CategoryRule
        {
            Name = "Documents", Folder = "Documents",
            Extensions = { "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "epub", "txt", "csv" }
        };
        yield return new CategoryRule
        {
            Name = "Music", Folder = "Music",
            Extensions = { "mp3", "flac", "wav", "aac", "ogg", "m4a", "opus", "wma" }
        };
        yield return new CategoryRule
        {
            Name = "Video", Folder = "Video",
            Extensions = { "mp4", "mkv", "avi", "mov", "webm", "flv", "ts", "m4v", "wmv" }
        };
        yield return new CategoryRule
        {
            Name = "Programs", Folder = "Programs",
            Extensions = { "exe", "msi", "apk", "dmg", "deb", "rpm", "appx", "msix", "jar" }
        };
        yield return new CategoryRule
        {
            Name = "Images", Folder = "Images",
            Extensions = { "jpg", "jpeg", "png", "gif", "bmp", "svg", "webp", "tiff", "ico" }
        };
        yield return new CategoryRule { Name = "General", Folder = "General" };
    }

    public CategoryRule CategoryFor(string fileName)
    {
        var ext = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        if (ext.Length > 0)
        {
            foreach (var rule in Categories)
                if (rule.Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                    return rule;
        }
        return Categories.LastOrDefault(c => c.Name == "General") ?? new CategoryRule();
    }

    public string ResolveFolder(string category) =>
        Path.Combine(DownloadFolder, Categories.FirstOrDefault(c => c.Name == category)?.Folder ?? category);
}
