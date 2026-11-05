using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace RapidFetch.Core.Models;

public enum DownloadStatus
{
    Queued,
    Probing,
    Downloading,
    Paused,
    Completed,
    Failed,
    Canceled
}

public sealed class DownloadSegment
{
    public long Start { get; set; }
    public long End { get; set; }
    public long Position { get; set; }
    public bool Done { get; set; }

    [JsonIgnore]
    public long Remaining => Math.Max(0, End - Position);

    [JsonIgnore]
    public long Length => End - Start + 1;
}

public sealed class DownloadItem : INotifyPropertyChanged
{
    private long _downloadedBytes;
    private long _totalBytes;
    private DownloadStatus _status = DownloadStatus.Queued;
    private double _speed;
    private bool _isChecked;
    private string _url = "";
    private string _fileName = "";
    private string _savePath = "";
    private string? _error;
    private int _connections = 8;
    private string _category = "General";

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Url
    {
        get => _url;
        set
        {
            if (Set(ref _url, value))
                Raise(nameof(DisplayName));
        }
    }
    /// <summary>The URL as originally requested, before any redirects. Used to
    /// recognise repeat browser captures of the same address.</summary>
    public string? OriginalUrl { get; set; }
    public string? Referrer { get; set; }
    public string? UserAgent { get; set; }
    public string? ContentType { get; set; }
    public string? Queue { get; set; }
    /// <summary>"http" for the built-in engine, "ytdlp" for the optional video-site engine.</summary>
    public string Engine { get; set; } = "http";
    /// <summary>Requested quality for the yt-dlp engine: best|1080|720|480|audio.</summary>
    public string? Quality { get; set; }
    public DateTime Added { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }
    public bool StartImmediately { get; set; } = true;

    public string FileName
    {
        get => _fileName;
        set
        {
            if (Set(ref _fileName, value))
                Raise(nameof(DisplayName));
        }
    }

    public string SavePath
    {
        get => _savePath;
        set => Set(ref _savePath, value);
    }

    public string Category
    {
        get => _category;
        set => Set(ref _category, value);
    }

    public int Connections
    {
        get => _connections;
        set => Set(ref _connections, Math.Clamp(value, 1, 32));
    }

    public long TotalBytes
    {
        get => _totalBytes;
        set
        {
            if (Set(ref _totalBytes, value))
                Raise(nameof(Progress), nameof(SizeText), nameof(PercentText), nameof(IsIndeterminate),
                      nameof(EtaText), nameof(EtaSeconds));
        }
    }

    public long DownloadedBytes
    {
        get => _downloadedBytes;
        set
        {
            if (Set(ref _downloadedBytes, value))
                Raise(nameof(Progress), nameof(SizeText), nameof(PercentText), nameof(IsIndeterminate),
                      nameof(EtaText), nameof(EtaSeconds));
        }
    }

    public DownloadStatus Status
    {
        get => _status;
        set
        {
            if (Set(ref _status, value))
                Raise(nameof(StatusText), nameof(CanStart), nameof(CanPause), nameof(Progress),
                      nameof(IsIndeterminate), nameof(SizeText), nameof(EtaText), nameof(EtaSeconds),
                      nameof(StatusRank), nameof(ShowProgress));
        }
    }

    public double Speed
    {
        get => _speed;
        set
        {
            if (Set(ref _speed, value))
                Raise(nameof(SpeedText), nameof(EtaText), nameof(EtaSeconds));
        }
    }

    /// <summary>Row checkbox state for multi-select actions. Never persisted.</summary>
    [JsonIgnore]
    public bool IsChecked
    {
        get => _isChecked;
        set => Set(ref _isChecked, value);
    }

    public string? Error
    {
        get => _error;
        set => Set(ref _error, value);
    }

    public List<DownloadSegment> Segments { get; set; } = new();

    [JsonIgnore] public double Progress => TotalBytes > 0 ? Math.Clamp((double)DownloadedBytes / TotalBytes, 0, 1) : 0;

    [JsonIgnore] public bool IsIndeterminate => TotalBytes <= 0 && Status == DownloadStatus.Downloading;

    [JsonIgnore] public bool ShowProgress => Status != DownloadStatus.Completed;

    [JsonIgnore] public bool CanStart =>
        Status is DownloadStatus.Paused or DownloadStatus.Failed or DownloadStatus.Canceled or DownloadStatus.Queued;

    [JsonIgnore] public bool CanPause => Status == DownloadStatus.Downloading;

    [JsonIgnore] public string PercentText =>
        TotalBytes > 0 ? $"{Progress * 100:0.0}%" : (Status == DownloadStatus.Completed ? "100%" : "—");

    [JsonIgnore]
    public string SizeText
    {
        get
        {
            if (TotalBytes <= 0) return Util.Format.Bytes(DownloadedBytes);
            if (Status == DownloadStatus.Completed || DownloadedBytes == TotalBytes)
                return Util.Format.Bytes(TotalBytes);
            return $"{Util.Format.Bytes(DownloadedBytes)} / {Util.Format.Bytes(TotalBytes)}";
        }
    }

    /// <summary>The name shown in the UI. Failed captures and pending probes can
    /// have no file name yet, so fall back to a readable part of the URL instead
    /// of rendering a blank row.</summary>
    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(FileName)) return FileName;
            if (string.IsNullOrWhiteSpace(Url)) return "Untitled download";
            if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri)) return Url;

            string path;
            try { path = Uri.UnescapeDataString(uri.AbsolutePath).TrimEnd('/'); }
            catch { path = uri.AbsolutePath.TrimEnd('/'); }

            string leaf = path.Length == 0 ? "" : path[(path.LastIndexOf('/') + 1)..];
            if (leaf.Contains('.')) return leaf;

            string hostPath = uri.Host + path;
            return string.IsNullOrWhiteSpace(hostPath) ? Url : hostPath;
        }
    }

    [JsonIgnore] public string SpeedText => Status == DownloadStatus.Downloading ? Util.Format.Speed(Speed) : "";
    /// <summary>Time left shown in the manager's Time Left column.</summary>
    [JsonIgnore]
    public string EtaText
    {
        get
        {
            if (Status != DownloadStatus.Downloading || TotalBytes <= DownloadedBytes || Speed <= 0)
                return "—";
            return Util.Format.Eta(TotalBytes - DownloadedBytes, Speed);
        }
    }

    /// <summary>Numeric form for header sorting; unknown ETAs sort last.</summary>
    [JsonIgnore]
    public double EtaSeconds =>
        Status == DownloadStatus.Downloading && TotalBytes > DownloadedBytes && Speed > 0
            ? (TotalBytes - DownloadedBytes) / Speed
            : double.MaxValue;

    [JsonIgnore] public string AddedText => Util.Format.Date(Added);

    /// <summary>Groups rows by state when the Status header is clicked.</summary>
    [JsonIgnore]
    public int StatusRank => Status switch
    {
        DownloadStatus.Downloading => 0,
        DownloadStatus.Probing => 1,
        DownloadStatus.Queued => 2,
        DownloadStatus.Paused => 3,
        DownloadStatus.Failed => 4,
        DownloadStatus.Completed => 5,
        _ => 6
    };

    [JsonIgnore]
    public string StatusText => Status switch
    {
        DownloadStatus.Queued => "Queued",
        DownloadStatus.Probing => "Connecting…",
        DownloadStatus.Downloading => "Downloading",
        DownloadStatus.Paused => "Paused",
        DownloadStatus.Completed => "Complete",
        DownloadStatus.Failed => "Error",
        DownloadStatus.Canceled => "Canceled",
        _ => ""
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }

    public void Raise(params string?[] names)
    {
        if (PropertyChanged is null) return;
        foreach (var name in names)
            PropertyChanged(this, new PropertyChangedEventArgs(name));
    }

    public void RefreshAll()
    {
        Raise(nameof(Progress));
        Raise(nameof(SizeText));
        Raise(nameof(PercentText));
        Raise(nameof(SpeedText));
        Raise(nameof(StatusText));
        Raise(nameof(IsIndeterminate));
    }
}
