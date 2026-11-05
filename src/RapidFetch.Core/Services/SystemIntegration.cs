using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.Win32;

namespace RapidFetch.Core.Services;

[SupportedOSPlatform("windows")]
public static class SystemIntegration
{
    public const string ShellVerb = "RapidFetch.Download";
    public const string HostName = "com.rapidfetch.host";

    public static string AppDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RapidFetch");

    public static string SettingsPath => Path.Combine(AppDataDir, "settings.json");
    public static string StatePath => Path.Combine(AppDataDir, "downloads.json");
    public static string LogPath => Path.Combine(AppDataDir, "rapidfetch.log");
    public static string HostManifestPath => Path.Combine(AppDataDir, "host", HostName + ".json");

    public static void WriteHostManifest(string exePath, string extensionId)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(HostManifestPath)!);
        var manifest = new
        {
            name = HostName,
            description = "RapidFetch native messaging bridge",
            path = exePath,
            type = "stdio",
            allowed_origins = new[] { $"chrome-extension://{extensionId}/" }
        };
        File.WriteAllText(HostManifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void RegisterNativeHost(string manifestPath)
    {
        foreach (var browser in new[]
                 {
                     @"Software\Google\Chrome\NativeMessagingHosts",
                     @"Software\Microsoft\Edge\NativeMessagingHosts",
                     @"Software\BraveSoftware\Brave-Browser\NativeMessagingHosts",
                     @"Software\Chromium\NativeMessagingHosts",
                     @"Software\Vivaldi\NativeMessagingHosts",
                     @"Software\Opera Software\NativeMessagingHosts"
                 })
        {
            using var key = Registry.CurrentUser.CreateSubKey(Path.Combine(browser, HostName));
            key?.SetValue(null, manifestPath);
        }
    }

    public static void UnregisterNativeHost()
    {
        foreach (var browser in new[]
                 {
                     @"Software\Google\Chrome\NativeMessagingHosts",
                     @"Software\Microsoft\Edge\NativeMessagingHosts",
                     @"Software\BraveSoftware\Brave-Browser\NativeMessagingHosts",
                     @"Software\Chromium\NativeMessagingHosts",
                     @"Software\Vivaldi\NativeMessagingHosts",
                     @"Software\Opera Software\NativeMessagingHosts"
                 })
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(Path.Combine(browser, HostName), false); } catch { }
        }
    }

    public static void RegisterShell(string exePath, bool enable)
    {
        // Remove the old "every file" verb from preview installs; a download verb
        // only makes sense for Internet shortcuts and folders.
        try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\*\shell\RapidFetch", false); } catch { }

        SetVerb(@"Software\Classes\InternetShortcut\shell\RapidFetch", exePath,
            "Download with RapidFetch", enable, "\"%1\"");
        SetVerb(@"Software\Classes\Directory\shell\RapidFetch", exePath,
            "RapidFetch download here", enable, "\"%1\"");
    }

    private static void SetVerb(string baseKey, string exePath, string label, bool enable, string args)
    {
        try
        {
            if (!enable)
            {
                Registry.CurrentUser.DeleteSubKeyTree(baseKey, false);
                return;
            }

            using var key = Registry.CurrentUser.CreateSubKey(baseKey);
            key?.SetValue(null, label);
            key?.SetValue("Icon", exePath);
            using var command = key?.CreateSubKey("command");
            command?.SetValue(null, $"\"{exePath}\" --add {args}");
        }
        catch { }
    }

    public static void RegisterStartup(string exePath, bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        if (key is null) return;
        if (enable) key.SetValue("RapidFetch", $"\"{exePath}\" --minimized");
        else key.DeleteValue("RapidFetch", false);
    }

    public static bool IsStartupRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        return key?.GetValue("RapidFetch") is string;
    }

    public static string ExtensionFolder => EmbeddedAssets.EnsureExtension();
}
