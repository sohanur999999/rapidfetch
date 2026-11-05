using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace RapidFetch.Core.Services;

/// <summary>
/// Locates the browser extension that ships beside the app, or unpacks the copy
/// embedded in the assembly. The fallback keeps the single-file portable build
/// fully functional without a companion folder.
/// </summary>
public static class EmbeddedAssets
{
    private const string ExtensionPrefix = "RapidFetch.Assets.extension/";
    private const string HostPrefix = "RapidFetch.Assets.host/";
    private const string SetupPrefix = "RapidFetch.Assets.setup.";
    private const string DocsPrefix = "RapidFetch.Assets.setup.docs/";
    private static readonly object Gate = new();
    private static string? _extensionFolder;

    public static string ExtensionFolder
    {
        get
        {
            lock (Gate) return _extensionFolder ??= ExtractExtension();
        }
    }

    public static string EnsureExtension()
    {
        lock (Gate) return _extensionFolder ??= ExtractExtension();
    }

    /// <summary>Writes a fresh copy of the embedded extension into a chosen folder.</summary>
    public static int ExtractExtensionTo(string target) =>
        ExtractTree(ExtensionPrefix, target, clean: true);

    /// <summary>Writes the extension identity files into a chosen folder.</summary>
    public static int ExtractHostTo(string target) =>
        ExtractTree(HostPrefix, target, clean: true);

    /// <summary>Writes the README, uninstaller and screenshots used by setup.</summary>
    public static void ExtractSetupAssets(string target)
    {
        Directory.CreateDirectory(target);

        var readme = ReadText(SetupPrefix + "README.md");
        if (readme is not null)
            File.WriteAllText(Path.Combine(target, "README.md"), readme);

        var uninstall = ReadText(SetupPrefix + "uninstall.ps1");
        if (uninstall is not null)
            File.WriteAllText(Path.Combine(target, "uninstall.ps1"), uninstall);

        ExtractTree(DocsPrefix, Path.Combine(target, "docs"), clean: true);
    }

    public static string? ReadHostFile(string fileName)
    {
        try
        {
            var adjacent = Path.Combine(AppContext.BaseDirectory, "host", fileName);
            if (File.Exists(adjacent)) return File.ReadAllText(adjacent).Trim();

            var assembly = typeof(EmbeddedAssets).Assembly;
            var exact = HostPrefix + fileName;
            var name = assembly.GetManifestResourceNames().FirstOrDefault(n =>
                string.Equals(n, exact, StringComparison.Ordinal) ||
                n.EndsWith("/" + fileName, StringComparison.Ordinal));
            if (name is null) return null;

            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null) return null;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd().Trim();
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractExtension()
    {
        var adjacent = Path.Combine(AppContext.BaseDirectory, "extension");
        if (File.Exists(Path.Combine(adjacent, "manifest.json"))) return adjacent;

        var target = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RapidFetch", "extension");

        try
        {
            var files = ReadTree(ExtensionPrefix);
            if (files.Count == 0) return target;

            var fingerprint = Fingerprint(files);
            var marker = Path.Combine(target, ".embedded-" + fingerprint);
            if (File.Exists(Path.Combine(target, "manifest.json")) && File.Exists(marker))
                return target;

            WriteTree(target, files);
            foreach (var old in Directory.EnumerateFiles(target, ".embedded-*"))
            {
                try { File.Delete(old); } catch { }
            }
            File.WriteAllText(marker, ProgramInfo.Version);
        }
        catch
        {
            // The downloader still works even if the extension folder cannot be
            // unpacked (read-only profile, antivirus lock, and so on).
        }

        return target;
    }

    private static int ExtractTree(string prefix, string target, bool clean)
    {
        var files = ReadTree(prefix);
        if (files.Count == 0) return 0;

        if (clean && Directory.Exists(target))
            Directory.Delete(target, recursive: true);

        WriteTree(target, files);
        return files.Count;
    }

    private static void WriteTree(string target, Dictionary<string, byte[]> files)
    {
        foreach (var file in files)
        {
            var destination = Path.Combine(target,
                file.Key.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.WriteAllBytes(destination, file.Value);
        }
    }

    private static Dictionary<string, byte[]> ReadTree(string prefix)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var assembly = typeof(EmbeddedAssets).Assembly;
        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var relative = name[prefix.Length..].Replace('\\', '/').TrimStart('/');
            if (relative.Length == 0) continue;
            if (relative.Split('/').Any(part => part is ".." or ".")) continue;

            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null) continue;
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            result[relative] = memory.ToArray();
        }
        return result;
    }

    private static string? ReadText(string resourceName)
    {
        try
        {
            using var stream = typeof(EmbeddedAssets).Assembly
                .GetManifestResourceStream(resourceName);
            if (stream is null) return null;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch
        {
            return null;
        }
    }

    private static string Fingerprint(Dictionary<string, byte[]> files)
    {
        using var sha = SHA256.Create();
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
        {
            foreach (var file in files.OrderBy(f => f.Key, StringComparer.Ordinal))
            {
                writer.Write(file.Key);
                writer.Write(file.Value.Length);
                writer.Write(file.Value);
            }
        }
        buffer.Position = 0;
        return Convert.ToHexString(sha.ComputeHash(buffer)).ToLowerInvariant()[..16];
    }
}
