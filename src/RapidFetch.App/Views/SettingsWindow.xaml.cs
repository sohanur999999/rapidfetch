using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RapidFetch.Core.Engine;
using RapidFetch.Core.Models;
using RapidFetch.Core.Services;
using Forms = System.Windows.Forms;

namespace RapidFetch.App.Views;

public partial class SettingsWindow : Window
{
    private static App Host => (App)Application.Current;
    private readonly AppSettings _draft;
    public bool Saved { get; private set; }

    public SettingsWindow()
    {
        InitializeComponent();
        _draft = Clone(Host.Settings);
        LoadFrom(_draft);

        SectionList.SelectedIndex = 0;
        AdvancedVersion.Text = $"RapidFetch {ProgramInfo.Version} · extension {Host.ExtensionId}";
        UpdateHostStatus();
    }

    private static AppSettings Clone(AppSettings source)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(source);
        return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json,
                   new System.Text.Json.JsonSerializerOptions
                   {
                       PropertyNameCaseInsensitive = true,
                       Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
                       NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
                   }) ?? new AppSettings();
    }

    private void LoadFrom(AppSettings s)
    {
        SelectByTag(ThemeBox, s.Theme);
        SelectByTag(AccentBox, s.Accent);
        StartWithWindows.IsChecked = s.RunAtStartup;
        CloseToTray.IsChecked = s.CloseToTray;
        Notify.IsChecked = s.NotifyOnComplete;
        ClipboardWatch.IsChecked = s.MonitorClipboard;
        ShellIntegration.IsChecked = s.ShellIntegration;

        FolderBox.Text = s.DownloadFolder;
        TempBox.Text = s.TempFolder;
        ConnectionsSlider.Value = Math.Clamp(s.DefaultConnections, 1, 32);
        ConcurrentSlider.Value = Math.Clamp(s.MaxConcurrentDownloads, 1, 12);
        RetryBox.IsChecked = s.RetryAutomatically;
        ConfirmAddBox.IsChecked = s.ConfirmOnAdd;
        TransferPopupBox.IsChecked = s.ShowTransferWindow;

        LimitBox.IsChecked = s.SpeedLimitEnabled;
        LimitSlider.Value = Math.Clamp(s.SpeedLimitBytesPerSecond / 1024, 64, 102400);

        BrowserBox.IsChecked = s.BrowserIntegrationEnabled;
        MediaBox.IsChecked = s.CaptureMediaFiles;
        PortBox.Text = s.BrowserPort.ToString();

        ProxyEnabledBox.IsChecked = s.ProxyEnabled;
        SelectByTag(ProxyTypeBox, string.IsNullOrWhiteSpace(s.ProxyType) ? "http" : s.ProxyType);
        ProxyHostBox.Text = s.ProxyHost;
        ProxyPortBox.Text = s.ProxyPort.ToString();
        ProxyUserBox.Text = s.ProxyUsername;
        ProxyPasswordBox.Password = s.ProxyPassword;
        ProxyBypassLocalBox.IsChecked = s.ProxyBypassLocal;

        VideoSitesBox.IsChecked = s.VideoSitesEnabled;
        YtDlpPathBox.Text = s.YtDlpPath;
        SelectByTag(QualityBox, string.IsNullOrWhiteSpace(s.YtDlpQuality) ? "best" : s.YtDlpQuality);
        SelectByTag(CookiesBox, string.IsNullOrWhiteSpace(s.YtDlpCookies) ? "none" : s.YtDlpCookies);
        PlaylistBox.IsChecked = s.YtDlpPlaylist;

        ConnectionsValue.Text = ((int)ConnectionsSlider.Value).ToString();
        ConcurrentValue.Text = ((int)ConcurrentSlider.Value).ToString();
        LimitValue.Text = $"{LimitSlider.Value:0} KB/s";
        ExtensionPath.Text = SystemIntegration.ExtensionFolder;
        UpdateToolStatus();
    }

    private static void SelectByTag(ComboBox box, string tag)
    {
        foreach (var item in box.Items.OfType<ComboBoxItem>())
            if (string.Equals(item.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedItem = item;
                return;
            }
        box.SelectedIndex = 0;
    }

    private static string TagOf(ComboBox box, string fallback = "dark") =>
        (box.SelectedItem as ComboBoxItem)?.Tag as string ?? fallback;

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void SectionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // XAML sets IsSelected on the first section while InitializingComponent is
        // still running, before the pane fields exist.
        if (!IsInitialized || PaneGeneral is null || PaneDownloads is null || PaneSpeed is null
            || PaneBrowser is null || PaneNetwork is null || PaneAdvanced is null || PaneVideo is null) return;
        if (SectionList.SelectedItem is not ListBoxItem item) return;
        var tag = item.Tag as string;
        PaneGeneral.Visibility = tag == "general" ? Visibility.Visible : Visibility.Collapsed;
        PaneDownloads.Visibility = tag == "downloads" ? Visibility.Visible : Visibility.Collapsed;
        PaneSpeed.Visibility = tag == "speed" ? Visibility.Visible : Visibility.Collapsed;
        PaneBrowser.Visibility = tag == "browser" ? Visibility.Visible : Visibility.Collapsed;
        PaneNetwork.Visibility = tag == "network" ? Visibility.Visible : Visibility.Collapsed;
        PaneVideo.Visibility = tag == "video" ? Visibility.Visible : Visibility.Collapsed;
        PaneAdvanced.Visibility = tag == "advanced" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Connections_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ConnectionsValue is not null) ConnectionsValue.Text = ((int)e.NewValue).ToString();
    }

    private void Concurrent_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ConcurrentValue is not null) ConcurrentValue.Text = ((int)e.NewValue).ToString();
    }

    private void Limit_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LimitValue is not null) LimitValue.Text = $"{e.NewValue:0} KB/s";
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e) =>
        FolderBox.Text = PickFolder(FolderBox.Text);

    private void BrowseTemp_Click(object sender, RoutedEventArgs e) =>
        TempBox.Text = PickFolder(TempBox.Text);

    private static string PickFolder(string current)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Choose a folder",
            SelectedPath = Directory.Exists(current) ? current : Host.Settings.DownloadFolder,
            UseDescriptionForTitle = true
        };
        return dialog.ShowDialog() == Forms.DialogResult.OK ? dialog.SelectedPath : current;
    }

    private void OpenScheduler_Click(object sender, RoutedEventArgs e)
    {
        new SchedulerWindow { Owner = this }.ShowDialog();
    }

    private void OpenExtension_Click(object sender, RoutedEventArgs e)
    {
        var path = SystemIntegration.ExtensionFolder;
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    private void ReinstallHost_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var exe = Environment.ProcessPath!;
            SystemIntegration.WriteHostManifest(exe, Host.ExtensionId);
            SystemIntegration.RegisterNativeHost(SystemIntegration.HostManifestPath);
            UpdateHostStatus();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "RapidFetch"); }
    }

    private void UpdateHostStatus()
    {
        bool exists = File.Exists(SystemIntegration.HostManifestPath);
        HostStatus.Text = exists
            ? $"Native host registered · {SystemIntegration.HostManifestPath}"
            : "Native host not registered yet.";
    }

    private void UpdateToolStatus()
    {
        var ytDlp = YtDlp.Locate(YtDlpPathBox.Text);
        YtDlpStatus.Text = ytDlp is null
            ? "Not installed. Click Install yt-dlp, or browse to an existing yt-dlp.exe."
            : "Ready · " + ytDlp;

        var ffmpeg = YtDlp.LocateFfmpeg(new AppSettings { FfmpegPath = Host.Settings.FfmpegPath });
        FfmpegStatus.Text = ffmpeg is null
            ? "Not found. Merged qualities are unavailable; downloads fall back to a single file (often 360p)."
            : "Ready · " + ffmpeg;
    }

    private void BrowseYtDlp_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose yt-dlp.exe",
            Filter = "yt-dlp (yt-dlp.exe)|yt-dlp.exe|Programs (*.exe)|*.exe",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() == true)
        {
            YtDlpPathBox.Text = dialog.FileName;
            UpdateToolStatus();
        }
    }

    private void YtDlpPathBox_LostFocus(object sender, RoutedEventArgs e) => UpdateToolStatus();

    private async void InstallYtDlp_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(this,
            "Download the official yt-dlp.exe from its GitHub releases into:\n\n" +
            YtDlp.ToolsFolder +
            "\n\nIt is a separate open-source program that RapidFetch calls for video pages. Continue?",
            "Install yt-dlp", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        InstallYtDlpButton.IsEnabled = false;
        InstallYtDlpButton.Content = "Installing…";
        try
        {
            var progress = new Progress<double>(value =>
                YtDlpStatus.Text = $"Downloading yt-dlp… {value:P0}");
            var version = await YtDlp.InstallAsync(progress, CancellationToken.None);
            YtDlpPathBox.Text = YtDlp.ManagedExePath;
            YtDlpStatus.Text = $"yt-dlp {version} installed · {YtDlp.ManagedExePath}";
        }
        catch (Exception ex)
        {
            YtDlpStatus.Text = "Install failed: " + ex.Message;
        }
        finally
        {
            InstallYtDlpButton.Content = "Install yt-dlp";
            InstallYtDlpButton.IsEnabled = true;
        }
    }

    private async void CheckYtDlp_Click(object sender, RoutedEventArgs e)
    {
        var exe = YtDlp.Locate(YtDlpPathBox.Text);
        if (exe is null)
        {
            YtDlpStatus.Text = "yt-dlp was not found. Install it or browse to the executable.";
            return;
        }

        YtDlpStatus.Text = "Checking…";
        try
        {
            var version = await YtDlp.VersionAsync(exe, CancellationToken.None);
            YtDlpStatus.Text = string.IsNullOrWhiteSpace(version)
                ? $"Found {exe}, but it did not report a version."
                : $"yt-dlp {version} · {exe}";
        }
        catch (Exception ex)
        {
            YtDlpStatus.Text = "Could not run yt-dlp: " + ex.Message;
        }
    }

    private void OpenData_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{SystemIntegration.AppDataDir}\"") { UseShellExecute = true });

    private void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        if (File.Exists(SystemIntegration.LogPath))
            Process.Start(new ProcessStartInfo(SystemIntegration.LogPath) { UseShellExecute = true });
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "RapidFetch settings (*.json)|*.json",
            FileName = "rapidfetch-settings.json"
        };
        if (dialog.ShowDialog() == true)
            SettingsStore.Save(dialog.FileName, Host.Settings);
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "RapidFetch settings (*.json)|*.json"
        };
        if (dialog.ShowDialog() != true) return;

        var imported = SettingsStore.Load(dialog.FileName);
        LoadFrom(imported);
        ApplyTo(imported);
        CopyInto(imported, Host.Settings);
        Host.SaveSettings();
        Host.ApplyTheme();
        Host.Engine.ApplySettings();
        Saved = true;
        MessageBox.Show(this, "Settings imported.", "RapidFetch");
    }

    private void ClearFinished_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in Host.Engine.Items.Where(i => i.Status == DownloadStatus.Completed).ToList())
            Host.Engine.Remove(item, false, false);
    }

    private void ExportList_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV file (*.csv)|*.csv|Text file (*.txt)|*.txt",
            FileName = "rapidfetch-downloads.csv"
        };
        if (dialog.ShowDialog() != true) return;

        var csv = new StringBuilder();
        csv.AppendLine("FileName,Url,SavePath,Category,Status,Size,Downloaded,Added,Completed");
        foreach (var item in Host.Engine.Items)
        {
            csv.AppendLine(string.Join(",",
                Quote(item.DisplayName), Quote(item.Url), Quote(item.SavePath), Quote(item.Category),
                Quote(item.StatusText), item.TotalBytes, item.DownloadedBytes,
                Quote(item.Added.ToString("s")),
                Quote(item.CompletedAt?.ToString("s") ?? "")));
        }

        try
        {
            File.WriteAllText(dialog.FileName, csv.ToString(), Encoding.UTF8);
            MessageBox.Show(this, $"Exported {Host.Engine.Items.Count} items.", "RapidFetch");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "RapidFetch", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string Quote(string value) =>
        "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";

    private async void ImportLinks_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Link lists (*.txt;*.csv;*.json)|*.txt;*.csv;*.json|All files (*.*)|*.*",
            Multiselect = false
        };
        if (dialog.ShowDialog() != true) return;

        string text;
        try { text = File.ReadAllText(dialog.FileName); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "RapidFetch", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var urls = System.Text.RegularExpressions.Regex
            .Matches(text, @"https?://[^\s""'<>\\]+", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            .Select(m => m.Value.TrimEnd(',', ';', ')', ']'))
            .Where(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(500)
            .ToList();

        if (urls.Count == 0)
        {
            MessageBox.Show(this, "No http(s) links were found in that file.", "RapidFetch");
            return;
        }

        int added = 0;
        foreach (var url in urls)
        {
            try
            {
                await Host.Engine.AddAsync(new AddDownloadRequest { Url = url });
                added++;
            }
            catch { /* duplicate or probe failure stays in the list */ }
        }
        MessageBox.Show(this, $"Added {added} of {urls.Count} links.", "RapidFetch");
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Reset every option to its default value?", "RapidFetch",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        var defaults = new AppSettings();
        LoadFrom(defaults);
        ApplyTo(defaults);
        CopyInto(defaults, Host.Settings);
        Host.SaveSettings();
        Saved = true;
    }

    private void ApplyTo(AppSettings s)
    {
        s.Theme = TagOf(ThemeBox);
        s.Accent = TagOf(AccentBox);
        s.RunAtStartup = StartWithWindows.IsChecked == true;
        s.CloseToTray = CloseToTray.IsChecked == true;
        s.NotifyOnComplete = Notify.IsChecked == true;
        s.MonitorClipboard = ClipboardWatch.IsChecked == true;
        s.ShellIntegration = ShellIntegration.IsChecked == true;

        s.DownloadFolder = string.IsNullOrWhiteSpace(FolderBox.Text) ? s.DownloadFolder : FolderBox.Text.Trim();
        s.TempFolder = TempBox.Text.Trim();
        s.DefaultConnections = (int)ConnectionsSlider.Value;
        s.MaxConcurrentDownloads = (int)ConcurrentSlider.Value;
        s.RetryAutomatically = RetryBox.IsChecked == true;
        s.ConfirmOnAdd = ConfirmAddBox.IsChecked == true;
        s.ShowTransferWindow = TransferPopupBox.IsChecked == true;

        s.SpeedLimitEnabled = LimitBox.IsChecked == true;
        s.SpeedLimitBytesPerSecond = (long)LimitSlider.Value * 1024;

        s.BrowserIntegrationEnabled = BrowserBox.IsChecked == true;
        s.CaptureMediaFiles = MediaBox.IsChecked == true;
        if (int.TryParse(PortBox.Text, out var port) && port > 1024 && port < 65535)
            s.BrowserPort = port;

        s.ProxyEnabled = ProxyEnabledBox.IsChecked == true;
        s.ProxyType = TagOf(ProxyTypeBox);
        s.ProxyHost = ProxyHostBox.Text.Trim();
        s.ProxyPort = int.TryParse(ProxyPortBox.Text, out var proxyPort)
            ? Math.Clamp(proxyPort, 1, 65535) : 8080;
        s.ProxyUsername = ProxyUserBox.Text.Trim();
        s.ProxyPassword = ProxyPasswordBox.Password;
        s.ProxyBypassLocal = ProxyBypassLocalBox.IsChecked == true;

        s.VideoSitesEnabled = VideoSitesBox.IsChecked == true;
        s.YtDlpPath = YtDlpPathBox.Text.Trim();
        s.YtDlpQuality = TagOf(QualityBox, "best");
        s.YtDlpCookies = TagOf(CookiesBox, "none");
        s.YtDlpPlaylist = PlaylistBox.IsChecked == true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ApplyTo(_draft);
        CopyInto(_draft, Host.Settings);
        Host.SaveSettings();
        if (!Host.Settings.ShowTransferWindow) Host.HideTransfers(false);

        var exe = Environment.ProcessPath!;
        SystemIntegration.RegisterStartup(exe, Host.Settings.RunAtStartup);
        SystemIntegration.RegisterShell(exe, Host.Settings.ShellIntegration);
        if (Host.Settings.BrowserIntegrationEnabled)
        {
            SystemIntegration.WriteHostManifest(exe, Host.ExtensionId);
            SystemIntegration.RegisterNativeHost(SystemIntegration.HostManifestPath);
        }

        Saved = true;
        Close();
    }

    private static void CopyInto(AppSettings source, AppSettings target)
    {
        foreach (var property in typeof(AppSettings).GetProperties(
                     System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (!property.CanRead || !property.CanWrite) continue;
            property.SetValue(target, property.GetValue(source));
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
