using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using RapidFetch.Core.Models;
using RapidFetch.Core.Util;

namespace RapidFetch.App.Views;

/// <summary>
/// Small always-on-top panel that follows running transfers without opening the
/// main window. It samples the aggregate speed twice a second and draws a
/// rolling 30-second graph.
/// </summary>
public partial class TransferWindow : Window
{
    private const int SampleCapacity = 60;          // 30 seconds at 500 ms
    private const int IdleTicksBeforeHide = 60;     // 30 seconds

    private static App Host => App.Current;

    private readonly DispatcherTimer _timer;
    private readonly ObservableCollection<DownloadItem> _rows = new();
    private readonly Queue<double> _samples = new();

    private bool _autoShown;
    private bool _userMoved;
    private int _idleTicks;
    private double _peak;
    private DownloadItem? _lastCompleted;

    /// <summary>True after the user hides the panel; auto-show respects it until transfers go idle.</summary>
    public bool IsDismissed { get; private set; }

    public TransferWindow()
    {
        InitializeComponent();
        RowsList.ItemsSource = _rows;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) => Tick();

        Loaded += (_, _) =>
        {
            PositionBottomRight();
            Refresh();
        };
        SizeChanged += (_, _) =>
        {
            if (!_userMoved) PositionBottomRight();
            DrawGraph();
        };
        MouseEnter += (_, _) => _idleTicks = 0;
    }

    // ------------------------------------------------------------- show/hide

    public void ShowPanel(bool auto)
    {
        IsDismissed = false;
        _autoShown = auto;
        _idleTicks = 0;

        if (IsVisible)
        {
            if (auto) PositionBottomRight();
        }
        else
        {
            _samples.Clear();
            _peak = 0;

            // Start near the final position so the first frame does not jump.
            var area = SystemParameters.WorkArea;
            Left = Math.Max(area.Left + 8, area.Right - Width - 18);
            Top = Math.Max(area.Top + 8, area.Bottom - 420);

            BeginAnimation(OpacityProperty, null);
            Opacity = 0;
            Show();
            UpdateLayout();
            PositionBottomRight();
            BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
        }

        Refresh();
        DrawGraph();
        _timer.Start();
    }

    public void HidePanel(bool byUser)
    {
        IsDismissed = byUser;
        _autoShown = false;
        _timer.Stop();
        BeginAnimation(OpacityProperty, null);
        Opacity = 1;
        Hide();
    }

    public void ResetDismissed() => IsDismissed = false;

    private void PositionBottomRight()
    {
        var area = SystemParameters.WorkArea;
        double width = ActualWidth > 0 ? ActualWidth : Width;
        double height = ActualHeight > 0 ? ActualHeight : 380;
        Left = Math.Max(area.Left + 8, area.Right - width - 18);
        Top = Math.Max(area.Top + 8, area.Bottom - height - 18);
    }

    // ------------------------------------------------------------------ tick

    private void Tick()
    {
        double speed = Host.Engine.Items
            .Where(i => i.Status == DownloadStatus.Downloading)
            .Sum(i => i.Speed);

        _samples.Enqueue(speed);
        while (_samples.Count > SampleCapacity) _samples.Dequeue();

        Refresh();
        DrawGraph();

        bool busy = Host.Engine.Items.Any(i => i.Status is
            DownloadStatus.Downloading or DownloadStatus.Probing or
            DownloadStatus.Queued or DownloadStatus.Paused);

        if (IsMouseOver)
            _idleTicks = 0;
        else if (!busy)
            _idleTicks++;

        if (_autoShown && _idleTicks >= IdleTicksBeforeHide)
            HidePanel(false);
    }

    private void Refresh()
    {
        SyncRows();

        var items = Host.Engine.Items;
        bool busy = items.Any(i => i.Status is DownloadStatus.Downloading
            or DownloadStatus.Probing or DownloadStatus.Queued);

        // Offer Open / Open folder / Close for the newest finished file once
        // nothing is still downloading, even if older paused or failed rows
        // are also on screen.
        _lastCompleted = _rows
            .Where(i => i.Status == DownloadStatus.Completed)
            .OrderByDescending(i => i.CompletedAt ?? DateTime.MinValue)
            .FirstOrDefault();
        bool showComplete = !busy && _lastCompleted is not null;
        CompleteActions.Visibility = showComplete ? Visibility.Visible : Visibility.Collapsed;
        OpenManagerButton.Visibility = showComplete ? Visibility.Collapsed : Visibility.Visible;
        FooterHint.Text = showComplete ? "Download complete" : "Drag to move · Esc to hide";

        var running = items.Where(i => i.Status == DownloadStatus.Downloading).ToList();
        int preparing = items.Count(i => i.Status == DownloadStatus.Probing);
        int queued = items.Count(i => i.Status == DownloadStatus.Queued);
        int paused = items.Count(i => i.Status == DownloadStatus.Paused);
        int failed = items.Count(i => i.Status == DownloadStatus.Failed);
        bool recentlyDone = items.Any(i => i.Status == DownloadStatus.Completed
                                           && i.CompletedAt is { } done
                                           && (DateTime.Now - done).TotalSeconds < 60);

        double speed = running.Sum(i => i.Speed);
        SpeedLabel.Text = speed > 0.5 ? Format.Speed(speed) : "0 B/s";
        _peak = Math.Max(_peak, speed);
        PeakLabel.Text = _peak > 0.5 ? "peak " + Format.Speed(_peak) : "peak —";

        int active = running.Count + preparing;
        if (active > 0)
        {
            SummaryLabel.Text = (preparing > 0 && running.Count == 0 ? "Connecting" : $"{active} downloading")
                + (queued > 0 ? $" · {queued} queued" : paused > 0 ? $" · {paused} paused" : "");
            SetState("Live", "Accent");
        }
        else if (queued > 0)
        {
            SummaryLabel.Text = $"{queued} queued";
            SetState("Queued", "Warning");
        }
        else if (failed > 0)
        {
            SummaryLabel.Text = failed == 1 ? "A transfer failed" : $"{failed} transfers failed";
            SetState("Error", "Danger");
        }
        else if (recentlyDone)
        {
            SummaryLabel.Text = "All transfers complete"
                + (paused > 0 ? $" · {paused} paused" : "");
            SetState("Done", "Success");
        }
        else if (paused > 0)
        {
            SummaryLabel.Text = paused == 1 ? "Paused" : $"{paused} paused";
            SetState("Paused", "Warning");
        }
        else
        {
            SummaryLabel.Text = "No active transfers";
            SetState("Idle", "Muted");
        }
    }

    private void SyncRows()
    {
        var now = DateTime.Now;
        var desired = Host.Engine.Items
            .Where(item => item.Status switch
            {
                DownloadStatus.Downloading or DownloadStatus.Probing or
                    DownloadStatus.Queued or DownloadStatus.Paused or DownloadStatus.Failed => true,
                DownloadStatus.Completed => item.CompletedAt is { } done
                                            && (now - done).TotalSeconds < 60,
                _ => false
            })
            .OrderBy(item => Priority(item.Status))
            .ThenByDescending(item => item.Added)
            .Take(6)
            .ToList();

        if (!desired.SequenceEqual(_rows))
        {
            _rows.Clear();
            foreach (var item in desired) _rows.Add(item);
        }

        bool empty = _rows.Count == 0;
        EmptyPanel.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        RowsScroll.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    private static int Priority(DownloadStatus status) => status switch
    {
        DownloadStatus.Downloading => 0,
        DownloadStatus.Probing => 1,
        DownloadStatus.Queued => 2,
        DownloadStatus.Completed => 3,
        DownloadStatus.Paused => 4,
        DownloadStatus.Failed => 5,
        _ => 6
    };

    private void SetState(string text, string brushKey)
    {
        StatePillText.Text = text;
        var brush = Res(brushKey);
        StatePillText.Foreground = brush;
        StateDot.Fill = brush;
    }

    // ----------------------------------------------------------------- graph

    private void DrawGraph()
    {
        if (SpeedCanvas is null) return;

        double width = SpeedCanvas.ActualWidth;
        double height = SpeedCanvas.ActualHeight;
        if (width <= 2 || height <= 2) return;

        SpeedCanvas.Children.Clear();

        var grid = Res("Border");
        for (int i = 1; i <= 3; i++)
        {
            double y = height * i / 4;
            SpeedCanvas.Children.Add(new Line
            {
                X1 = 0, Y1 = y, X2 = width, Y2 = y,
                Stroke = grid, StrokeThickness = 0.6, Opacity = 0.55
            });
        }

        var values = _samples.ToArray();
        if (values.Length == 0) return;

        // Scale to the 95th percentile so one burst does not flatten the whole
        // graph, while still keeping normal peaks visible.
        double max = 64 * 1024;
        var positive = values.Where(v => v > 0).OrderBy(v => v).ToArray();
        if (positive.Length > 0)
        {
            int index = (int)Math.Floor((positive.Length - 1) * 0.95);
            max = Math.Max(max, positive[index]);
        }
        max *= 1.18;

        double step = width / (SampleCapacity - 1);
        var points = new PointCollection(values.Length);
        for (int i = 0; i < values.Length; i++)
        {
            double x = width - (values.Length - 1 - i) * step;
            double y = height - 2 - Math.Clamp(values[i] / max, 0, 1) * (height - 6);
            points.Add(new Point(x, y));
        }

        if (points.Count > 1)
        {
            var areaGeometry = new StreamGeometry();
            using (var context = areaGeometry.Open())
            {
                context.BeginFigure(new Point(points[0].X, height), isFilled: true, isClosed: true);
                foreach (var point in points) context.LineTo(point, isStroked: true, isSmoothJoin: false);
                context.LineTo(new Point(points[^1].X, height), isStroked: false, isSmoothJoin: false);
            }
            areaGeometry.Freeze();

            SpeedCanvas.Children.Add(new System.Windows.Shapes.Path
            {
                Data = areaGeometry,
                Fill = WithOpacity(Res("Accent"), 0.16)
            });
        }

        SpeedCanvas.Children.Add(new Polyline
        {
            Points = points,
            Stroke = Res("Accent"),
            StrokeThickness = 1.8,
            StrokeLineJoin = PenLineJoin.Round
        });

        if (points.Count > 1)
        {
            var last = points[^1];
            var dot = new Ellipse { Width = 5, Height = 5, Fill = Res("Accent") };
            Canvas.SetLeft(dot, last.X - 2.5);
            Canvas.SetTop(dot, last.Y - 2.5);
            SpeedCanvas.Children.Add(dot);
        }
    }

    private static Brush Res(string key) =>
        Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;

    private static Brush WithOpacity(Brush brush, double opacity)
    {
        if (brush is not SolidColorBrush solid) return brush;
        var clone = new SolidColorBrush(solid.Color) { Opacity = opacity };
        clone.Freeze();
        return clone;
    }

    // --------------------------------------------------------------- actions

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        DragMove();
        _userMoved = true;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        HidePanel(true);
        e.Handled = true;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (Host.IsExiting) return;
        e.Cancel = true;
        HidePanel(true);
    }

    private void Hide_Click(object sender, RoutedEventArgs e) => HidePanel(true);

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        HidePanel(false);
        Host.ShowMain();
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: DownloadItem item } && item.CanPause)
            Host.Engine.Pause(item);
        Refresh();
    }

    private void Resume_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: DownloadItem item } && item.CanStart)
            _ = Host.Engine.StartAsync(item);
        Refresh();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: DownloadItem item })
            Host.Engine.Cancel(item);
        Refresh();
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var path = _lastCompleted?.SavePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch { }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = _lastCompleted?.SavePath;
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            if (File.Exists(path))
                Process.Start("explorer.exe", $"/select,\"{path}\"");
            else if (System.IO.Path.GetDirectoryName(path) is { Length: > 0 } folder && Directory.Exists(folder))
                Process.Start("explorer.exe", $"\"{folder}\"");
        }
        catch { }
    }

    private void ClosePanel_Click(object sender, RoutedEventArgs e) => HidePanel(true);
}
