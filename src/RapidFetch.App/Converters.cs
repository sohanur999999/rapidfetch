using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using RapidFetch.Core.Models;

namespace RapidFetch.App;

public sealed class StatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            DownloadStatus.Downloading => "Accent",
            DownloadStatus.Completed => "Success",
            DownloadStatus.Failed => "Danger",
            DownloadStatus.Paused => "Warning",
            DownloadStatus.Canceled => "Muted",
            DownloadStatus.Queued or DownloadStatus.Probing => "Warning",
            _ => "Muted"
        };
        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class StatusToGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        DownloadStatus.Downloading => "\uE896",
        DownloadStatus.Completed => "\uE73E",
        DownloadStatus.Failed => "\uEA39",
        DownloadStatus.Paused => "\uE769",
        DownloadStatus.Canceled => "\uE711",
        DownloadStatus.Queued => "\uE823",
        DownloadStatus.Probing => "\uE895",
        _ => "\uE896"
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool flag = value is bool b && b;
        if (Invert) flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;
}

public sealed class StatusToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DownloadStatus status || parameter is not string names)
            return Visibility.Collapsed;

        foreach (var name in names.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (Enum.TryParse<DownloadStatus>(name, ignoreCase: true, out var target) && status == target)
                return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class EmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool empty = value is int i && i == 0;
        return empty ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
