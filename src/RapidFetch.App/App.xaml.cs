using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using RapidFetch.Core.Engine;
using RapidFetch.Core.Models;
using RapidFetch.Core.Services;
using RapidFetch.App.Views;
using Forms = System.Windows.Forms;

namespace RapidFetch.App;

public partial class App : Application
{
    private Mutex? _mutex;
    private Forms.NotifyIcon? _tray;
    private TransferWindow? _transfer;
    private CompletionWindow? _completion;
    private DispatcherTimer? _activityTimer;
    private bool _wasActive;
    private bool _exiting;

    public static new App Current => (App)Application.Current;
    public bool IsExiting => _exiting;
    public AppSettings Settings { get; private set; } = new();
    public DownloadEngine Engine { get; private set; } = null!;
    public BrowserBridge Bridge { get; private set; } = null!;
    public MainWindow? Shell { get; private set; }
    public string InstallPath => AppContext.BaseDirectory;
    public string ExtensionId { get; private set; } = "";
    public string ExtensionKey { get; private set; } = "";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        GlassEffects.ApplyToAllWindows();

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log("FATAL " + (args.ExceptionObject as Exception)?.ToString());
        DispatcherUnhandledException += (_, args) =>
        {
            Log("UI EXCEPTION " + args.Exception);
            MessageBox.Show(args.Exception.Message, "RapidFetch error");
            args.Handled = true;
        };

        try
        {
            StartCore(e.Args);
        }
        catch (Exception ex)
        {
            Log("STARTUP FAILED " + ex);
            MessageBox.Show(ex.ToString(), "RapidFetch failed to start");
            Shutdown(1);
        }
    }

    private void StartCore(string[] args)
    {

        if (args.Any(a => a.Equals("--native-host", StringComparison.OrdinalIgnoreCase)) ||
            args.Any(a => a.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase)))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            NativeMessagingHost.RunAsync(args).GetAwaiter().GetResult();
            Shutdown(0);
            return;
        }

        if (SetupInstaller.IsSetupRequest(args))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            if (SetupInstaller.IsSilent(args))
            {
                Shutdown(SetupInstaller.RunSilent(args));
                return;
            }

            var setup = new SetupWindow();
            MainWindow = setup;
            setup.Closed += (_, _) => Shutdown(0);
            setup.Show();
            return;
        }

        _mutex = new Mutex(true, "RapidFetch.SingleInstance.v1", out bool created);
        if (!created)
        {
            ForwardAndExit(args);
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Directory.CreateDirectory(SystemIntegration.AppDataDir);
        Settings = SettingsStore.Load(SystemIntegration.SettingsPath);
        Engine = new DownloadEngine(Settings, SystemIntegration.StatePath);
        Engine.Log += Log;
        Engine.ExitRequested += () => Dispatcher.BeginInvoke(new Action(ExitApp));
        Bridge = new BrowserBridge(Engine, "local");
        Bridge.Log += Log;
        Bridge.ShowRequested += () => Dispatcher.BeginInvoke(new Action(ShowMain));
        Bridge.OpenFolderRequested += path => Dispatcher.BeginInvoke(new Action(() => OpenFolderInApp(path)));

        LoadExtensionIdentity();
        EmbeddedAssets.EnsureExtension();
        ApplyTheme();
        EnsureBrowserIntegration();
        StartBridge();

        Shell = new MainWindow();
        MainWindow = Shell;

        bool minimized = args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
        if (!minimized) Shell.Show();

        CreateTray();
        StartTransferWatcher();
        HandleCommandLine(args);
        Log($"RapidFetch {ProgramInfo.Version} started");
    }

    private static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(SystemIntegration.AppDataDir);
            File.AppendAllText(SystemIntegration.LogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { }
    }

    private void LoadExtensionIdentity()
    {
        try
        {
            var idFile = Path.Combine(InstallPath, "host", "extension-id.txt");
            var keyFile = Path.Combine(InstallPath, "host", "extension-key.txt");
            if (File.Exists(idFile)) ExtensionId = File.ReadAllText(idFile).Trim();
            if (File.Exists(keyFile)) ExtensionKey = File.ReadAllText(keyFile).Trim();
            if (string.IsNullOrWhiteSpace(ExtensionId))
                ExtensionId = EmbeddedAssets.ReadHostFile("extension-id.txt") ?? "";
            if (string.IsNullOrWhiteSpace(ExtensionKey))
                ExtensionKey = EmbeddedAssets.ReadHostFile("extension-key.txt") ?? "";
        }
        catch { }
    }

    private void EnsureBrowserIntegration()
    {
        if (string.IsNullOrWhiteSpace(ExtensionId)) return;
        try
        {
            var exe = Environment.ProcessPath ?? Path.Combine(InstallPath, "RapidFetch.exe");
            SystemIntegration.WriteHostManifest(exe, ExtensionId);
            if (Settings.BrowserIntegrationEnabled)
                SystemIntegration.RegisterNativeHost(SystemIntegration.HostManifestPath);
            SystemIntegration.RegisterShell(exe, Settings.ShellIntegration);
            SystemIntegration.RegisterStartup(exe, Settings.RunAtStartup);
        }
        catch (Exception ex) { Log("Integration setup failed: " + ex.Message); }
    }

    public void StartBridge()
    {
        try
        {
            if (!Settings.BrowserIntegrationEnabled) return;
            Bridge.Start(Settings.BrowserPort);
            Log($"Browser bridge listening on 127.0.0.1:{Settings.BrowserPort}");
        }
        catch (Exception ex) { Log("Bridge start failed: " + ex.Message); }
    }

    // ------------------------------------------------------- transfer panel

    private void StartTransferWatcher()
    {
        _activityTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _activityTimer.Tick += (_, _) => WatchTransfers();
        _activityTimer.Start();

        // New downloads (from the extension, tray or command line) should pop the
        // panel up at once instead of waiting for the next watcher tick.
        Engine.Items.CollectionChanged += (_, e) =>
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                Dispatcher.BeginInvoke(new Action(AutoShowTransfers));
        };

        // A finished transfer gets the larger completion panel when the queue is
        // idle; otherwise the normal notification is enough.
        Engine.ItemCompleted += OnItemCompleted;
    }

    private void OnItemCompleted(DownloadItem item)
    {
        if (TryShowCompletion(item)) return;
        Notify("Download complete", item.DisplayName);
    }

    private bool TryShowCompletion(DownloadItem item)
    {
        if (!Settings.ShowTransferWindow) return false;
        if (Shell is { IsVisible: true, IsActive: true, WindowState: not WindowState.Minimized })
            return false;
        if (Engine.Items.Any(i => i.Status is DownloadStatus.Downloading
            or DownloadStatus.Probing or DownloadStatus.Queued))
            return false;

        _transfer?.HidePanel(false);
        _completion ??= new CompletionWindow();
        _completion.ShowCompletion(item);
        return true;
    }

    private void WatchTransfers()
    {
        bool active = Engine.Items.Any(i => i.Status is DownloadStatus.Downloading
            or DownloadStatus.Probing or DownloadStatus.Queued);

        if (active && !_wasActive)
            AutoShowTransfers();

        _wasActive = active;
        if (!active)
            _transfer?.ResetDismissed();
    }

    private void AutoShowTransfers()
    {
        if (!Settings.ShowTransferWindow) return;
        if (_transfer?.IsDismissed == true) return;
        if (Shell is { IsVisible: true, IsActive: true, WindowState: not WindowState.Minimized })
            return;
        _completion?.HideCompletion();
        ShowTransferPanel(auto: true);
    }

    /// <summary>Show the floating transfer panel (used by the tray menu).</summary>
    public void ShowTransfers()
    {
        _completion?.HideCompletion();
        ShowTransferPanel(auto: false);
    }

    public void HideTransfers(bool byUser) => _transfer?.HidePanel(byUser);

    private void ShowTransferPanel(bool auto)
    {
        _transfer ??= new TransferWindow();
        _transfer.ShowPanel(auto);
    }

    public void ApplyTheme()
    {
        bool light = Settings.Theme == "light" ||
                     (Settings.Theme == "system" && IsSystemLight());
        var source = new Uri(light ? "Themes/Light.xaml" : "Themes/Dark.xaml", UriKind.Relative);
        Resources.MergedDictionaries[0] = new ResourceDictionary { Source = source };

        try
        {
            var accent = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(Settings.Accent);
            Resources["AccentColor"] = accent;
            Resources["AccentHoverColor"] = Lighten(accent, 0.12);
        }
        catch { }
    }

    private static System.Windows.Media.Color Lighten(System.Windows.Media.Color color, double amount) =>
        System.Windows.Media.Color.FromArgb(color.A,
            (byte)Math.Min(255, color.R + (255 - color.R) * amount),
            (byte)Math.Min(255, color.G + (255 - color.G) * amount),
            (byte)Math.Min(255, color.B + (255 - color.B) * amount));

    private static bool IsSystemLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch { return false; }
    }

    public void SaveSettings()
    {
        SettingsStore.Save(SystemIntegration.SettingsPath, Settings);
        Engine.ApplySettings();
    }

    private void CreateTray()
    {
        _tray = new Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "RapidFetch",
            Visible = true
        };
        _tray.DoubleClick += (_, _) => ShowMain();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open RapidFetch", null, (_, _) => ShowMain());
        menu.Items.Add("Show transfers", null, (_, _) => ShowTransfers());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Pause all", null, (_, _) =>
        {
            foreach (var item in Engine.Items.Where(i => i.Status == DownloadStatus.Downloading).ToList())
                Engine.Pause(item);
        });
        menu.Items.Add("Resume all", null, (_, _) =>
        {
            foreach (var item in Engine.Items.Where(i => i.CanStart && i.Status != DownloadStatus.Completed).ToList())
                _ = Engine.StartAsync(item);
        });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());
        _tray.ContextMenuStrip = menu;
        _tray.BalloonTipClicked += (_, _) => ShowMain();
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        var exe = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exe))
        {
            var icon = System.Drawing.Icon.ExtractAssociatedIcon(exe);
            if (icon is not null) return icon;
        }
        return System.Drawing.SystemIcons.Application;
    }

    public void ShowMain()
    {
        if (Shell is null) return;
        _completion?.HideCompletion();
        _transfer?.HidePanel(false);
        Shell.Show();
        Shell.WindowState = WindowState.Normal;
        Shell.Activate();
        Shell.Topmost = true;
        Shell.Topmost = false;
    }

    public void Notify(string title, string message)
    {
        if (!Settings.NotifyOnComplete || _tray is null) return;
        _tray.BalloonTipTitle = title;
        _tray.BalloonTipText = message;
        _tray.BalloonTipIcon = Forms.ToolTipIcon.Info;
        _tray.ShowBalloonTip(4000);
    }

    public void ExitApp()
    {
        if (_exiting) return;
        _exiting = true;
        _activityTimer?.Stop();
        try { _completion?.HideCompletion(); } catch { }
        try { _transfer?.HidePanel(false); } catch { }
        try { Engine.Dispose(); } catch { }
        try { Bridge.Dispose(); } catch { }
        if (_tray is not null) { _tray.Visible = false; _tray.Dispose(); }
        _mutex?.ReleaseMutex();
        Shutdown(0);
    }

    private void HandleCommandLine(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is "--add" or "-a" && i + 1 < args.Length)
            {
                var url = args[++i];
                if (PathUtilLite.IsUrl(url))
                    _ = Engine.AddAsync(new AddDownloadRequest { Url = url });
                else if (File.Exists(url) && url.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
                {
                    var target = PathUtilLite.ReadInternetShortcut(url);
                    if (target is not null) _ = Engine.AddAsync(new AddDownloadRequest { Url = target });
                }
                else if (Directory.Exists(url))
                    OpenFolderInApp(url);
            }
        }
    }

    public void OpenFolderInApp(string path)
    {
        if (!Directory.Exists(path)) return;
        Settings.DownloadFolder = path;
        SaveSettings();
        ShowMain();
    }

    private static void ForwardAndExit(string[] args)
    {
        try
        {
            var settings = SettingsStore.Load(SystemIntegration.SettingsPath);
            var port = settings.BrowserPort;
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };

            var target = ExtractAddArgument(args);
            string? url = null;

            if (PathUtilLite.IsUrl(target))
            {
                url = target;
            }
            else if (!string.IsNullOrWhiteSpace(target) && File.Exists(target)
                     && target.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
            {
                url = PathUtilLite.ReadInternetShortcut(target);
            }
            else if (!string.IsNullOrWhiteSpace(target) && Directory.Exists(target))
            {
                var body = JsonSerializer.Serialize(new { path = target });
                client.PostAsync($"http://127.0.0.1:{port}/open-folder",
                    new StringContent(body, Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
            }
            else
            {
                url = args.FirstOrDefault(PathUtilLite.IsUrl);
            }

            if (url is not null)
            {
                var body = JsonSerializer.Serialize(new { url });
                client.PostAsync($"http://127.0.0.1:{port}/add",
                    new StringContent(body, Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
            }
        }
        catch { }
        Environment.Exit(0);
    }

    private static string? ExtractAddArgument(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] is "--add" or "-a")
                return args[i + 1];
        return null;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (!_exiting)
        {
            try { Engine?.Dispose(); } catch { }
            try { Bridge?.Dispose(); } catch { }
            if (_tray is not null) { _tray.Visible = false; _tray.Dispose(); }
        }
        base.OnExit(e);
    }
}

internal static class PathUtilLite
{
    public static bool IsUrl(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public static string? ReadInternetShortcut(string path)
    {
        try
        {
            foreach (var line in File.ReadAllLines(path))
            {
                if (!line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase)) continue;
                var value = line[4..].Trim();
                if (IsUrl(value)) return value;
            }
        }
        catch { }
        return null;
    }
}
