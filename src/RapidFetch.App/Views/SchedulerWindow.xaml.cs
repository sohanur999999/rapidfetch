using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RapidFetch.Core.Models;

namespace RapidFetch.App.Views;

public partial class SchedulerWindow : Window
{
    private static App Host => (App)Application.Current;

    public SchedulerWindow()
    {
        InitializeComponent();
        var s = Host.Settings.Scheduler;

        EnabledBox.IsChecked = s.Enabled;
        StartBox.Text = s.StartTime.ToString(@"hh\:mm");
        StopBox.Text = s.StopTime.ToString(@"hh\:mm");
        SetDay(MonBox, DayOfWeek.Monday, s);
        SetDay(TueBox, DayOfWeek.Tuesday, s);
        SetDay(WedBox, DayOfWeek.Wednesday, s);
        SetDay(ThuBox, DayOfWeek.Thursday, s);
        SetDay(FriBox, DayOfWeek.Friday, s);
        SetDay(SatBox, DayOfWeek.Saturday, s);
        SetDay(SunBox, DayOfWeek.Sunday, s);
        LimitBox.Text = (s.DailyLimitBytes / (1024 * 1024)).ToString();
        StopAfterBox.IsChecked = s.StopAfterLimit;

        foreach (var item in ActionBox.Items.OfType<ComboBoxItem>())
            if (string.Equals(item.Tag as string, s.OnQueueFinished, StringComparison.OrdinalIgnoreCase))
                ActionBox.SelectedItem = item;
        if (ActionBox.SelectedItem is null) ActionBox.SelectedIndex = 0;

        UpdateSummary();
    }

    private static void SetDay(CheckBox box, DayOfWeek day, SchedulerOptions options) =>
        box.IsChecked = options.Days.Contains(day);

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private static bool TryParseTime(string text, out TimeSpan time) =>
        TimeSpan.TryParse(text.Trim(), out time) && time >= TimeSpan.Zero && time < TimeSpan.FromDays(1);

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseTime(StartBox.Text, out var start) || !TryParseTime(StopBox.Text, out var stop))
        {
            MessageBox.Show(this, "Times must use HH:MM, 00:00 to 23:59.", "RapidFetch");
            return;
        }

        var s = Host.Settings.Scheduler;
        s.Enabled = EnabledBox.IsChecked == true;
        s.StartTime = start;
        s.StopTime = stop;
        s.Days = new HashSet<DayOfWeek>();
        AddDay(MonBox, DayOfWeek.Monday, s);
        AddDay(TueBox, DayOfWeek.Tuesday, s);
        AddDay(WedBox, DayOfWeek.Wednesday, s);
        AddDay(ThuBox, DayOfWeek.Thursday, s);
        AddDay(FriBox, DayOfWeek.Friday, s);
        AddDay(SatBox, DayOfWeek.Saturday, s);
        AddDay(SunBox, DayOfWeek.Sunday, s);

        if (long.TryParse(LimitBox.Text, out var mb))
            s.DailyLimitBytes = Math.Max(0, mb) * 1024 * 1024;
        s.StopAfterLimit = StopAfterBox.IsChecked == true;
        s.OnQueueFinished = (ActionBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "none";

        Host.SaveSettings();
        Close();
    }

    private static void AddDay(CheckBox box, DayOfWeek day, SchedulerOptions options)
    {
        if (box.IsChecked == true) options.Days.Add(day);
    }

    private void UpdateSummary()
    {
        SummaryText.Text = "The window wraps midnight when the stop time is earlier than the start time.";
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
