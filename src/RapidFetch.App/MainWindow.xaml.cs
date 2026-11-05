using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using RapidFetch.Core.Engine;
using RapidFetch.Core.Models;
using RapidFetch.Core.Services;
using RapidFetch.App.Views;

namespace RapidFetch.App;

public sealed class CategoryCount : INotifyPropertyChanged
{
    private int _count;
    public required string Name { get; init; }
    public string Key => "cat:" + Name;
    public string Glyph { get; init; } = "\uE8B7";
    public int Count
    {
        get => _count;
        set { _count = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class MainWindow : Window
{
    private readonly ICollectionView _view;
    private readonly ObservableCollection<CategoryCount> _categories = new();
    private readonly DispatcherTimer _timer;
    private string _nav = "all";
    private string? _category;
    private string _search = "";
    private string _sort = "newest";
    private bool _queueRunning = true;
    private readonly HashSet<string> _queuePaused = new();
    private bool _selectAllGuard;
    private string _lastClipboard = "";

    private static App Host => (App)Application.Current;
    private DownloadEngine Engine => Host.Engine;

    public MainWindow()
    {
        InitializeComponent();

        _view = CollectionViewSource.GetDefaultView(Engine.Items);
        _view.Filter = FilterItem;
        DownloadsGrid.ItemsSource = _view;
        InitSort();

        AllNode.ItemsSource = _categories;
        RebuildCategories();

        VersionText.Text = "v" + ProgramInfo.Version;
        UpdateBridgeStatus();
        UpdateQueueStatus();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();

        Engine.ItemCompleted += item => Dispatcher.Invoke(() =>
        {
            RefreshCounts();
            UpdateDetails();
        });
        Engine.ItemFailed += item => Dispatcher.Invoke(() =>
        {
            if (!string.IsNullOrWhiteSpace(item.Error))
                Host.Notify("Download failed", $"{item.DisplayName}: {item.Error}");
            RefreshCounts();
        });
        Engine.Changed += () => Dispatcher.BeginInvoke(RefreshCounts);

        Drop += OnDrop;
        PreviewKeyDown += OnPreviewKeyDown;
        StateChanged += OnStateChanged;
        Closing += OnClosing;

        AllNode.IsSelected = true;
        RefreshCounts();
        UpdateDetails();
    }

    // ------------------------------------------------------------ filtering

    private bool FilterItem(object obj)
    {
        if (obj is not DownloadItem item) return false;
        if (_category is not null && !string.Equals(item.Category, _category, StringComparison.OrdinalIgnoreCase))
            return false;

        bool navOk = _nav switch
        {
            "active" or "queue" => item.Status is DownloadStatus.Downloading or DownloadStatus.Queued or DownloadStatus.Probing
                        or DownloadStatus.Paused or DownloadStatus.Failed,
            "done" => item.Status == DownloadStatus.Completed,
            "queued" => item.Status == DownloadStatus.Queued,
            _ => true
        };
        if (!navOk) return false;

        if (_search.Length > 0)
            return item.DisplayName.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
                   item.Url.Contains(_search, StringComparison.OrdinalIgnoreCase);
        return true;
    }

    private void RefreshFilter()
    {
        _view.Refresh();
        RefreshCounts();
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        EmptyState.Visibility = DownloadsGrid.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // --------------------------------------------------------------- sorting

    private void InitSort()
    {
        _sort = NormalizeSort(Host.Settings.SortOrder);
        ApplySort();
    }

    private static string NormalizeSort(string? value) => value switch
    {
        "oldest" or "name" or "name-desc" or "size" or "size-asc"
            or "status" or "status-desc" or "speed" or "speed-asc" or "eta" or "eta-desc" => value,
        _ => "newest"
    };

    private void DownloadsGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        string member = e.Column.SortMemberPath;
        if (string.IsNullOrEmpty(member)) return;

        bool descending;
        if (string.Equals(SortMemberFor(_sort), member, StringComparison.Ordinal))
            descending = !SortIsDescending(_sort);
        else
            descending = member is "TotalBytes" or "Speed" or "Added";

        _sort = SortKeyFor(member, descending);
        Host.Settings.SortOrder = _sort;
        Host.SaveSettings();
        ApplySort();
    }

    private static string SortMemberFor(string sort) => sort switch
    {
        "name" or "name-desc" => nameof(DownloadItem.DisplayName),
        "size" or "size-asc" => nameof(DownloadItem.TotalBytes),
        "status" or "status-desc" => nameof(DownloadItem.StatusRank),
        "speed" or "speed-asc" => nameof(DownloadItem.Speed),
        "eta" or "eta-desc" => nameof(DownloadItem.EtaSeconds),
        _ => nameof(DownloadItem.Added)
    };

    private static bool SortIsDescending(string sort) =>
        sort is "name-desc" or "size" or "status-desc" or "speed" or "eta-desc" or "newest";

    private static string SortKeyFor(string member, bool descending) => member switch
    {
        nameof(DownloadItem.DisplayName) => descending ? "name-desc" : "name",
        nameof(DownloadItem.TotalBytes) => descending ? "size" : "size-asc",
        nameof(DownloadItem.StatusRank) => descending ? "status-desc" : "status",
        nameof(DownloadItem.Speed) => descending ? "speed" : "speed-asc",
        nameof(DownloadItem.EtaSeconds) => descending ? "eta-desc" : "eta",
        _ => descending ? "newest" : "oldest"
    };

    private void ApplySort()
    {
        if (_view is not ListCollectionView list) return;

        list.IsLiveSorting = false;
        list.LiveSortingProperties.Clear();
        list.CustomSort = null;
        list.SortDescriptions.Clear();

        switch (_sort)
        {
            case "oldest":
                list.SortDescriptions.Add(new SortDescription(nameof(DownloadItem.Added), ListSortDirection.Ascending));
                break;
            case "name":
                list.CustomSort = new DownloadNameComparer(ListSortDirection.Ascending);
                break;
            case "name-desc":
                list.CustomSort = new DownloadNameComparer(ListSortDirection.Descending);
                break;
            case "size":
                list.SortDescriptions.Add(new SortDescription(nameof(DownloadItem.TotalBytes), ListSortDirection.Descending));
                EnableLiveSizeSort(list);
                break;
            case "size-asc":
                list.SortDescriptions.Add(new SortDescription(nameof(DownloadItem.TotalBytes), ListSortDirection.Ascending));
                EnableLiveSizeSort(list);
                break;
            case "status":
                list.SortDescriptions.Add(new SortDescription(nameof(DownloadItem.StatusRank), ListSortDirection.Ascending));
                break;
            case "status-desc":
                list.SortDescriptions.Add(new SortDescription(nameof(DownloadItem.StatusRank), ListSortDirection.Descending));
                break;
            case "speed":
                list.SortDescriptions.Add(new SortDescription(nameof(DownloadItem.Speed), ListSortDirection.Descending));
                break;
            case "speed-asc":
                list.SortDescriptions.Add(new SortDescription(nameof(DownloadItem.Speed), ListSortDirection.Ascending));
                break;
            case "eta":
                list.SortDescriptions.Add(new SortDescription(nameof(DownloadItem.EtaSeconds), ListSortDirection.Ascending));
                break;
            case "eta-desc":
                list.SortDescriptions.Add(new SortDescription(nameof(DownloadItem.EtaSeconds), ListSortDirection.Descending));
                break;
            default:
                list.SortDescriptions.Add(new SortDescription(nameof(DownloadItem.Added), ListSortDirection.Descending));
                break;
        }

        UpdateSortIndicators();
        list.Refresh();
    }

    private void UpdateSortIndicators()
    {
        string member = SortMemberFor(_sort);
        ListSortDirection? direction = SortIsDescending(_sort)
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;

        NameColumn.SortDirection = member == nameof(DownloadItem.DisplayName) ? direction : null;
        SizeColumn.SortDirection = member == nameof(DownloadItem.TotalBytes) ? direction : null;
        StatusColumn.SortDirection = member == nameof(DownloadItem.StatusRank) ? direction : null;
        SpeedColumn.SortDirection = member == nameof(DownloadItem.Speed) ? direction : null;
        EtaColumn.SortDirection = member == nameof(DownloadItem.EtaSeconds) ? direction : null;
        DateColumn.SortDirection = member == nameof(DownloadItem.Added) ? direction : null;
    }

    /// <summary>Keep a size sort correct while probes and downloads grow.</summary>
    private static void EnableLiveSizeSort(ListCollectionView list)
    {
        list.LiveSortingProperties.Add(nameof(DownloadItem.TotalBytes));
        list.IsLiveSorting = true;
    }

    private sealed class DownloadNameComparer : IComparer
    {
        private readonly int _direction;

        public DownloadNameComparer(ListSortDirection direction) =>
            _direction = direction == ListSortDirection.Ascending ? 1 : -1;

        public int Compare(object? x, object? y)
        {
            string left = (x as DownloadItem)?.DisplayName ?? "";
            string right = (y as DownloadItem)?.DisplayName ?? "";
            return string.Compare(left, right, StringComparison.CurrentCultureIgnoreCase) * _direction;
        }
    }

    private void RebuildCategories()
    {
        _categories.Clear();
        foreach (var rule in Host.Settings.Categories)
            _categories.Add(new CategoryCount { Name = rule.Name, Glyph = CategoryGlyph(rule.Name) });
        RefreshCounts();
    }

    private static string CategoryGlyph(string name) => name switch
    {
        "Compressed" => "\uE7B8",
        "Documents" => "\uE8A5",
        "Music" => "\uE8D6",
        "Video" => "\uE714",
        "Programs" => "\uE756",
        "Images" => "\uE91B",
        _ => "\uE8B7"
    };

    private void RefreshCounts()
    {
        var items = Engine.Items;
        CountAll.Text = items.Count.ToString();
        CountActive.Text = items.Count(i => i.Status is DownloadStatus.Downloading or DownloadStatus.Queued
            or DownloadStatus.Probing or DownloadStatus.Paused or DownloadStatus.Failed).ToString();
        CountDone.Text = items.Count(i => i.Status == DownloadStatus.Completed).ToString();
        CountMain.Text = items.Count(i => i.Status is not DownloadStatus.Completed and not DownloadStatus.Canceled).ToString();

        foreach (var category in _categories)
            category.Count = items.Count(i => string.Equals(i.Category, category.Name, StringComparison.OrdinalIgnoreCase));

        int active = items.Count(i => i.Status == DownloadStatus.Downloading);
        double totalSpeed = items.Sum(i => i.Speed);
        StatusActive.Text = active == 1 ? "1 active" : $"{active} active";
        StatusSpeed.Text = UtilSpeed(totalSpeed);
        StatusSession.Text = $"session {RapidFetch.Core.Util.Format.Bytes(Engine.SessionBytes)}";
        StatusView.Text = CurrentViewName;
        StatusCount.Text = BuildSubtitle();
        UpdateQueueStatus();
        UpdateSelectAllState();

        UpdateEmptyState();
    }

    private string CurrentViewName => _category ?? _nav switch
    {
        "active" => "Unfinished",
        "done" => "Finished",
        "queue" => "Main queue",
        _ => "All downloads"
    };

    private string BuildSubtitle()
    {
        int count = _view.Cast<object>().Count();
        return count == 1 ? "1 item" : $"{count} items";
    }

    private static string UtilSpeed(double bytes) =>
        bytes < 1 ? "0 B/s" : RapidFetch.Core.Util.Format.Bytes((long)bytes) + "/s";

    private void Tick()
    {
        RefreshCounts();
        UpdateDetails();
        CheckClipboard();
    }

    // ------------------------------------------------------------ clipboard

    private void CheckClipboard()
    {
        if (!Host.Settings.MonitorClipboard || !Clipboard.ContainsText()) return;

        string text;
        try { text = Clipboard.GetText(TextDataFormat.UnicodeText).Trim(); }
        catch { return; }

        if (text.Length == 0 || text == _lastClipboard) return;
        _lastClipboard = text;

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)) return;
        if (uri.Scheme is not ("http" or "https")) return;
        if (Engine.Items.Any(i => string.Equals(i.Url, text, StringComparison.OrdinalIgnoreCase))) return;

        var ext = Path.GetExtension(uri.AbsolutePath).TrimStart('.').ToLowerInvariant();
        var direct = ext is "zip" or "rar" or "7z" or "iso" or "exe" or "msi" or "apk" or "pdf" or "mp3"
            or "mp4" or "mkv" or "avi" or "mov" or "webm" or "flac" or "docx" or "xlsx" or "epub" or "msix";
        if (!direct) return;

        _ = Engine.AddAsync(new AddDownloadRequest { Url = text, Category = "General", StartNow = true });
        Host.Notify("Captured from clipboard", uri.Segments.LastOrDefault() ?? text);
    }

    // ------------------------------------------------------------ details

    private DownloadItem? Selected => DownloadsGrid.SelectedItem as DownloadItem;

    private void UpdateDetails()
    {
        var item = Selected;
        if (item is null)
        {
            DetailsPanel.Visibility = Visibility.Collapsed;
            return;
        }

        DetailsPanel.Visibility = Visibility.Visible;
        DetailName.Text = item.DisplayName;
        DetailName.ToolTip = item.DisplayName;
        DetailCategory.Text = item.Category;
        DetailStatus.Text = item.StatusText + (string.IsNullOrWhiteSpace(item.Error) ? "" : " · " + item.Error);
        DetailStatus.ToolTip = item.Error;
        DetailProgress.Value = item.Progress;
        DetailSize.Text = item.SizeText;
        DetailPercent.Text = item.PercentText;
        DetailSpeed.Text = item.SpeedText;
        DetailEta.Text = item.EtaText == "—" ? "" : "ETA " + item.EtaText;
        DetailConnections.Text = $"{item.Connections} threads · {item.Segments.Count} segments";
        DetailUrl.Text = item.Url;
        DetailUrl.ToolTip = item.Url;
        DetailPath.Text = item.SavePath;
        DetailPath.ToolTip = item.SavePath;
    }

    // ------------------------------------------------------------ layout

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateResponsiveLayout();

    private void UpdateResponsiveLayout()
    {
        if (DownloadsGrid is null || DateColumn is null || SearchBorder is null ||
            BrandTitle is null || VersionBadge is null || StatusSession is null ||
            FreeLabel is null || DetailNameColumn is null) return;

        bool narrow = ActualWidth < 1100;
        DateColumn.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
        DetailNameColumn.Width = new GridLength(narrow ? 220 : 260);

        bool compact = ActualWidth < 1000;
        SearchBorder.Width = compact ? 210 : 300;
        BrandTitle.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        VersionBadge.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        StatusSession.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        FreeLabel.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;

        Dispatcher.BeginInvoke(SyncNameColumnWidth, DispatcherPriority.Loaded);
    }

    private void DownloadsGrid_SizeChanged(object sender, SizeChangedEventArgs e) => SyncNameColumnWidth();

    /// <summary>
    /// Keep the Name column sized to the visible grid. WPF compresses every fixed
    /// column when a star column plus the fixed columns overflow, so the width is
    /// calculated here instead of being left to DataGrid star sizing.
    /// </summary>
    private void SyncNameColumnWidth()
    {
        if (NameColumn is null || DownloadsGrid is null || DateColumn is null) return;

        double fixedWidth = 32 + 34 + 136 + 108 + 78 + 78;
        if (DateColumn.Visibility == Visibility.Visible) fixedWidth += 112;

        double available = DownloadsGrid.ActualWidth;
        if (available <= 0) available = Math.Max(600, ActualWidth - 200);

        double target = Math.Max(NameColumn.MinWidth, available - fixedWidth - 18);
        if (Math.Abs(NameColumn.ActualWidth - target) > 0.5)
            NameColumn.Width = new DataGridLength(target);
    }

    private void UpdateBridgeStatus()
    {
        bool on = Host.Bridge.IsRunning;
        BridgeDot.Fill = (System.Windows.Media.Brush)FindResource(on ? "Success" : "Muted");
        BridgeLabel.Text = on ? $"Browser bridge · :{Host.Bridge.Port}" : "Browser bridge offline";
        StatusPort.Text = $"bridge :{Host.Settings.BrowserPort}";
    }

    // ------------------------------------------------------------ commands

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddDownloadWindow { Owner = this };
        if (Clipboard.ContainsText() && Uri.TryCreate(Clipboard.GetText(), UriKind.Absolute, out _))
        {
            var text = Clipboard.GetText().Trim();
            if (text.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                dialog.UrlText = text;
        }
        dialog.ShowDialog();
        if (dialog.Added) RefreshFilter();
    }

    private void AddUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out _)) return;
        _ = Engine.AddAsync(new AddDownloadRequest { Url = url });
    }

    private void ResumeSelected_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in TargetItems().Where(i => i.CanStart && i.Status != DownloadStatus.Completed))
        {
            _queuePaused.Remove(item.Id);
            _ = Engine.StartAsync(item);
        }
        RefreshCounts();
    }

    private void PauseSelected_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in TargetItems().Where(i => i.Status == DownloadStatus.Downloading))
            Engine.Pause(item);
        RefreshCounts();
    }

    private void StartQueue_Click(object sender, RoutedEventArgs e)
    {
        _queueRunning = true;
        var targets = Engine.Items
            .Where(i => i.Status == DownloadStatus.Queued
                        || (i.Status == DownloadStatus.Paused && _queuePaused.Contains(i.Id)))
            .ToList();

        foreach (var item in targets)
        {
            _queuePaused.Remove(item.Id);
            _ = Engine.StartAsync(item);
        }

        UpdateQueueStatus();
        RefreshCounts();
    }

    private void StopQueue_Click(object sender, RoutedEventArgs e)
    {
        _queueRunning = false;
        foreach (var item in Engine.Items.Where(i => i.Status == DownloadStatus.Queued).ToList())
        {
            _queuePaused.Add(item.Id);
            Engine.Pause(item);
        }

        UpdateQueueStatus();
        RefreshCounts();
    }

    private void StopAll_Click(object sender, RoutedEventArgs e)
    {
        _queueRunning = false;
        foreach (var item in Engine.Items
                     .Where(i => i.Status is DownloadStatus.Downloading or DownloadStatus.Queued)
                     .ToList())
        {
            if (item.Status == DownloadStatus.Queued) _queuePaused.Add(item.Id);
            Engine.Pause(item);
        }

        UpdateQueueStatus();
        RefreshCounts();
    }

    private void Queues_Click(object sender, RoutedEventArgs e)
    {
        QueuesNode.IsExpanded = true;
        MainNode.IsSelected = true;
        MainNode.BringIntoView();
    }

    private void UpdateQueueStatus()
    {
        if (QueueStatus is null) return;
        QueueStatus.Text = _queueRunning ? "Queue running" : "Queue stopped";
        QueueStatus.Foreground = (System.Windows.Media.Brush)FindResource(_queueRunning ? "Success" : "Warning");
    }

    private void RemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = TargetItems().ToList();
        if (selected.Count == 0) return;

        var dialog = new RemoveDownloadsWindow(selected) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        foreach (var item in selected) Engine.Remove(item, dialog.DeleteFiles, true);
        RefreshFilter();
        UpdateDetails();
    }

    private IEnumerable<DownloadItem> TargetItems()
    {
        var checkedItems = DownloadsGrid.Items.Cast<DownloadItem>().Where(i => i.IsChecked).ToList();
        if (checkedItems.Count > 0) return checkedItems;
        return DownloadsGrid.SelectedItems.Cast<DownloadItem>().ToList();
    }

    private void RowCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_selectAllGuard) return;
        UpdateSelectAllState();
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (_selectAllGuard) return;
        if (sender is not CheckBox box) return;

        _selectAllGuard = true;
        try
        {
            if (box.IsChecked is null) box.IsChecked = false;
            bool select = box.IsChecked == true;
            foreach (var item in DownloadsGrid.Items.Cast<DownloadItem>())
                item.IsChecked = select;
        }
        finally
        {
            _selectAllGuard = false;
        }
    }

    private void UpdateSelectAllState()
    {
        if (CheckColumn.Header is not CheckBox box) return;
        var items = DownloadsGrid.Items.Cast<DownloadItem>().ToList();

        _selectAllGuard = true;
        try
        {
            if (items.Count == 0) { box.IsChecked = false; return; }

            int selected = items.Count(i => i.IsChecked);
            box.IsChecked = selected == 0 ? false : selected == items.Count ? true : null;
        }
        finally
        {
            _selectAllGuard = false;
        }
    }

    private void DetailsClose_Click(object sender, RoutedEventArgs e)
    {
        DownloadsGrid.UnselectAll();
        UpdateDetails();
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var item = Selected;
        if (item is null) return;
        try
        {
            if (File.Exists(item.SavePath)) Process.Start(new ProcessStartInfo(item.SavePath) { UseShellExecute = true });
            else MessageBox.Show(this, "The file has not been downloaded yet.", "RapidFetch");
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "RapidFetch"); }
    }

    private void OpenFolderItem_Click(object sender, RoutedEventArgs e)
    {
        var item = Selected;
        if (item is null) return;
        var folder = Directory.Exists(item.SavePath) ? item.SavePath : Path.GetDirectoryName(item.SavePath);
        if (folder is null) return;
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
    }

    private void CopyUrl_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is { } item) Clipboard.SetText(item.Url);
    }

    private void RefreshAddress_Click(object sender, RoutedEventArgs e)
    {
        var item = Selected;
        if (item is null) return;
        if (item.Status == DownloadStatus.Completed)
        {
            MessageBox.Show(this,
                "This download is already complete. Remove it from the list and add the new address to download it again.",
                "RapidFetch", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new UrlPromptWindow(item) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        Engine.ChangeUrl(item, dialog.Url);
        _ = Engine.StartAsync(item);
        RefreshCounts();
        UpdateDetails();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(Host.Settings.DownloadFolder);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Host.Settings.DownloadFolder}\"")
        { UseShellExecute = true });
    }

    private void Grabber_Click(object sender, RoutedEventArgs e) =>
        new GrabberWindow { Owner = this }.ShowDialog();

    private void Scheduler_Click(object sender, RoutedEventArgs e) =>
        new SchedulerWindow { Owner = this }.ShowDialog();

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow { Owner = this };
        window.ShowDialog();
        if (window.Saved)
        {
            Host.ApplyTheme();
            Host.StartBridge();
            RebuildCategories();
            UpdateBridgeStatus();
            RefreshFilter();
        }
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        Host.Settings.Theme = Host.Settings.Theme == "dark" ? "light" : "dark";
        Host.ApplyTheme();
        Host.SaveSettings();
    }

    private void MenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.ContextMenu is not { } menu) return;
        menu.PlacementTarget = button;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Host.ExitApp();

    private void About_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show(this,
            $"RapidFetch {ProgramInfo.Version}\n\nA free download manager for Windows.\n" +
            "All features are unlocked: unlimited connections, queues, browser capture and no account.\n\n" +
            "Optional video-site downloads use yt-dlp when installed.",
            "About RapidFetch", MessageBoxButton.OK, MessageBoxImage.Information);

    public void NavigateToFolder(string path)
    {
        if (Directory.Exists(path))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    // ------------------------------------------------------------ events

    private void NavTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_view is null) return;

        if (e.NewValue is CategoryCount category)
        {
            _category = category.Name;
            _nav = "all";
        }
        else if (e.NewValue is TreeViewItem node && node.Tag is string tag)
        {
            _category = null;
            _nav = tag switch
            {
                "done" => "done",
                "active" => "active",
                "main" or "queue" or "queues" => "queue",
                _ => "all"
            };
        }
        else
        {
            return;
        }

        RefreshFilter();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _search = SearchBox.Text.Trim();
        SearchPlaceholder.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        RefreshFilter();
    }

    private void DownloadsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateDetails();

    private void DownloadsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => OpenFile_Click(sender, e);

    private void DownloadsGrid_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete) { RemoveSelected_Click(sender, e); e.Handled = true; }
        if (e.Key == Key.Enter) { OpenFile_Click(sender, e); e.Handled = true; }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (e.Key == Key.N) { Add_Click(sender, e); e.Handled = true; }
            else if (e.Key == Key.V)
            {
                if (Clipboard.ContainsText())
                {
                    var text = Clipboard.GetText().Trim();
                    if (Uri.TryCreate(text, UriKind.Absolute, out _)) { AddUrl(text); e.Handled = true; }
                }
            }
            else if (e.Key == Key.R) { ResumeSelected_Click(sender, e); e.Handled = true; }
            else if (e.Key == Key.F) { SearchBox.Focus(); e.Handled = true; }
        }
        else if (e.Key == Key.Escape && _search.Length > 0)
        {
            SearchBox.Text = "";
        }
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.Text))
        {
            var text = e.Data.GetData(DataFormats.Text) as string;
            if (text is not null)
            {
                foreach (var line in text.Split(new[] { '\r', '\n', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    if (Uri.TryCreate(line, UriKind.Absolute, out var uri) && uri.Scheme.StartsWith("http"))
                        AddUrl(line);
            }
        }
        else if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                foreach (var file in files.Where(File.Exists).Take(20))
                {
                    var text = File.ReadAllText(file);
                    foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                        if (line.Trim().StartsWith("http", StringComparison.OrdinalIgnoreCase)) AddUrl(line.Trim());
                }
            }
        }
    }

    // ------------------------------------------------------------ chrome

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (HasInteractiveAncestor(e.OriginalSource as DependencyObject)) return;

        if (e.ClickCount == 2) { Maximize_Click(sender, e); return; }
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    /// <summary>
    /// The menu bar is part of the drag surface. Clicks on its buttons or the
    /// search box must reach those controls instead of starting a window drag.
    /// </summary>
    private static bool HasInteractiveAncestor(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ButtonBase or TextBoxBase) return true;
            source = source is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }
        return false;
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (Host.Settings.CloseToTray && !Host.IsExiting)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        Host.ExitApp();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        MaximizeButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }
}
