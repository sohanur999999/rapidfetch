using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using RapidFetch.Core.Models;
using RapidFetch.Core.Util;

namespace RapidFetch.App.Views;

/// <summary>
/// A compact, borderless glass panel shown when the queue finishes. It gives
/// the latest file three clear actions without requiring the main window.
/// </summary>
public partial class CompletionWindow : Window
{
    private const int IdleTicksBeforeHide = 60;     // 30 seconds at 500 ms

    private static App Host => App.Current;

    private readonly DispatcherTimer _timer;
    private DownloadItem? _item;
    private int _idleTicks;

    public CompletionWindow()
    {
        InitializeComponent();

        // The window background must be transparent so only the glass card is
        // visible; the theme's implicit Window style otherwise paints a
        // rectangular backdrop around the rounded card.
        Background = System.Windows.Media.Brushes.Transparent;
        AllowsTransparency = true;
        WindowStyle = WindowStyle.None;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) => Tick();

        Loaded += (_, _) => PositionBottomRight();
        SizeChanged += (_, _) => PositionBottomRight();
        MouseEnter += (_, _) => _idleTicks = 0;
    }

    // ------------------------------------------------------------- show/hide

    public void ShowCompletion(DownloadItem item)
    {
        _item = item;
        _idleTicks = 0;
        Refresh();

        if (IsVisible)
        {
            PositionBottomRight();
        }
        else
        {
            // Start near the final position so the first frame does not jump.
            var area = SystemParameters.WorkArea;
            Left = Math.Max(area.Left + 8, area.Right - Width - 18);
            Top = Math.Max(area.Top + 8, area.Bottom - 230);

            BeginAnimation(OpacityProperty, null);
            Opacity = 0;
            Show();
            UpdateLayout();
            PositionBottomRight();
            BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
        }

        _timer.Start();
    }

    public void HideCompletion()
    {
        _timer.Stop();
        BeginAnimation(OpacityProperty, null);
        Opacity = 1;
        Hide();
    }

    private void Tick()
    {
        if (IsMouseOver || IsPointerOverCard())
        {
            _idleTicks = 0;
            return;
        }

        if (++_idleTicks >= IdleTicksBeforeHide)
            HideCompletion();
    }

    private bool IsPointerOverCard()
    {
        if (!IsVisible) return false;
        var point = Mouse.GetPosition(this);
        return point.X >= 0 && point.Y >= 0 &&
               point.X <= ActualWidth && point.Y <= ActualHeight;
    }

    private void PositionBottomRight()
    {
        var area = SystemParameters.WorkArea;
        double width = ActualWidth > 0 ? ActualWidth : Width;
        double height = ActualHeight > 0 ? ActualHeight : 210;
        Left = Math.Max(area.Left + 8, area.Right - width - 18);
        Top = Math.Max(area.Top + 8, area.Bottom - height - 18);
    }

    // ---------------------------------------------------------------- content

    private void Refresh()
    {
        if (_item is null) return;

        string path = _item.SavePath;
        FileNameText.Text = _item.DisplayName;
        FileNameText.ToolTip = path;
        FileCard.ToolTip = path;

        long bytes = _item.TotalBytes > 0 ? _item.TotalBytes : _item.DownloadedBytes;
        string size = Format.Bytes(bytes);
        FileMetaText.Text = string.IsNullOrWhiteSpace(_item.Category)
            ? size
            : $"{_item.Category} · {size}";

        bool fileExists = !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        if (!fileExists) FileMetaText.Text += " · file missing";
        OpenButton.IsEnabled = fileExists;
        OpenButton.ToolTip = fileExists ? path : "The file has moved or been deleted.";
        OpenFolderButton.IsEnabled = fileExists || DirectoryExists(path);
    }

    private static bool DirectoryExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try { return Directory.Exists(System.IO.Path.GetDirectoryName(path)); }
        catch { return false; }
    }

    // --------------------------------------------------------------- actions

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        HideCompletion();
        e.Handled = true;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (Host.IsExiting) return;
        e.Cancel = true;
        HideCompletion();
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var path = _item?.SavePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

        HideCompletion();
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch { }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = _item?.SavePath;
        if (string.IsNullOrWhiteSpace(path)) return;

        HideCompletion();
        try
        {
            if (File.Exists(path))
                Process.Start("explorer.exe", $"/select,\"{path}\"");
            else if (System.IO.Path.GetDirectoryName(path) is { Length: > 0 } folder && Directory.Exists(folder))
                Process.Start("explorer.exe", $"\"{folder}\"");
        }
        catch { }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => HideCompletion();
}
