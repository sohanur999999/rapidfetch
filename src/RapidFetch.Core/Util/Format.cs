using System.Globalization;

namespace RapidFetch.Core.Util;

public static class Format
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    public static string Bytes(long value)
    {
        if (value <= 0) return "0 B";
        double v = value;
        int i = 0;
        while (v >= 1024 && i < Units.Length - 1) { v /= 1024; i++; }
        return i == 0 ? $"{(long)v} B" : $"{v:0.##} {Units[i]}";
    }

    public static string Speed(double bytesPerSecond) =>
        bytesPerSecond <= 0 ? "" : Bytes((long)bytesPerSecond) + "/s";

    public static string Eta(long remainingBytes, double bytesPerSecond)
    {
        if (remainingBytes <= 0) return "";
        if (bytesPerSecond < 1) return "—";
        var span = TimeSpan.FromSeconds(remainingBytes / bytesPerSecond);
        if (span.TotalDays >= 1) return $"{(int)span.TotalDays}d {span.Hours}h";
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h {span.Minutes}m";
        if (span.TotalMinutes >= 1) return $"{(int)span.TotalMinutes}m {span.Seconds}s";
        return $"{span.Seconds}s";
    }

    public static string Date(DateTime value) =>
        value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    public static long ParseBytes(string text)
    {
        text = text.Trim().ToUpperInvariant();
        int i = 0;
        while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.' || text[i] == ',')) i++;
        if (i == 0) return 0;
        if (!double.TryParse(text[..i].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
            return 0;
        var suffix = text[i..].Trim();
        int idx = Array.IndexOf(Units, suffix);
        return idx <= 0 ? (long)n : (long)(n * Math.Pow(1024, idx));
    }
}
