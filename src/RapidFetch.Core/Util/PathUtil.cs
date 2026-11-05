using System.Text.RegularExpressions;

namespace RapidFetch.Core.Util;

public static partial class PathUtil
{
    private static readonly char[] Invalid = Path.GetInvalidFileNameChars();

    public static string Sanitize(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "download";
        name = name.Trim();
        var chars = name.Select(c => Invalid.Contains(c) || c < 32 ? '_' : c).ToArray();
        var clean = new string(chars).Trim(' ', '.');
        if (clean.Length == 0) clean = "download";
        if (clean.Length > 180)
        {
            var ext = Path.GetExtension(clean);
            if (ext.Length > 20) ext = "";
            var baseLength = Math.Min(clean.Length - ext.Length, 180 - ext.Length);
            clean = clean[..baseLength].TrimEnd(' ', '.') + ext;
        }
        return clean;
    }

    public static string Unique(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path) ?? "";
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (int i = 2; i < 10000; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
        return path;
    }

    public static string SafeJoin(string root, params string[] parts)
    {
        var combined = Path.GetFullPath(Path.Combine(new[] { root }.Concat(parts).ToArray()));
        return combined.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase) ? combined : root;
    }

    public static bool LooksLikeUrl(string value) =>
        UrlRegex().IsMatch(value.Trim());

    [GeneratedRegex(@"^(https?|ftp)://[^\s]+$", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();
}
