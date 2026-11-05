using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using RapidFetch.Core.Models;
using RapidFetch.Core.Services;

namespace RapidFetch.App;

internal sealed record SetupOptions
{
    public string InstallDir { get; init; } = SetupInstaller.DefaultInstallDir;
    public bool DesktopShortcut { get; init; } = true;
    public bool ShellIntegration { get; init; } = true;
    public bool StartWithWindows { get; init; } = true;
    public bool LaunchAfterInstall { get; init; }
}

internal sealed record SetupResult(
    string InstallDir,
    string ExePath,
    string ExtensionDir,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Per-user installation used by both the setup window and the silent
/// <c>--setup --silent</c> path. The running setup binary is copied as
/// RapidFetch.exe, so the installed app needs no .NET runtime or companion DLLs.
/// </summary>
internal static class SetupInstaller
{
    private static readonly string LogPath =
        Path.Combine(Path.GetTempPath(), "RapidFetch-Setup.log");

    public static string DefaultInstallDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs", "RapidFetch");

    public static bool IsSetupRequest(string[] args)
    {
        if (args.Any(a => a.Equals("--setup", StringComparison.OrdinalIgnoreCase)))
            return true;

        var name = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "");
        return name.Contains("setup", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSilent(string[] args) =>
        args.Any(a => a.Equals("--silent", StringComparison.OrdinalIgnoreCase) ||
                      a.Equals("/s", StringComparison.OrdinalIgnoreCase));

    public static int RunSilent(string[] args)
    {
        try
        {
            var options = ParseOptions(args);
            Log($"Silent install starting: {options.InstallDir}");
            var result = Install(options, (_, message) => Log(message));
            if (options.LaunchAfterInstall)
                Launch(result.ExePath, result.InstallDir);
            Log($"Silent install completed: {result.InstallDir}");
            return 0;
        }
        catch (Exception ex)
        {
            Log("Silent install failed: " + ex);
            return 1;
        }
    }

    public static SetupOptions ParseOptions(string[] args)
    {
        var dir = DefaultInstallDir;
        bool desktop = true, shell = true, startup = true, launch = false;

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals("--install-dir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                dir = args[++i];
            else if (arg.StartsWith("--install-dir=", StringComparison.OrdinalIgnoreCase))
                dir = arg["--install-dir=".Length..];
            else if (arg.Equals("--desktop", StringComparison.OrdinalIgnoreCase))
                desktop = true;
            else if (arg.Equals("--no-desktop", StringComparison.OrdinalIgnoreCase))
                desktop = false;
            else if (arg.Equals("--shell", StringComparison.OrdinalIgnoreCase))
                shell = true;
            else if (arg.Equals("--no-shell", StringComparison.OrdinalIgnoreCase))
                shell = false;
            else if (arg.Equals("--startup", StringComparison.OrdinalIgnoreCase))
                startup = true;
            else if (arg.Equals("--no-startup", StringComparison.OrdinalIgnoreCase))
                startup = false;
            else if (arg.Equals("--launch", StringComparison.OrdinalIgnoreCase))
                launch = true;
            else if (arg.Equals("--no-launch", StringComparison.OrdinalIgnoreCase))
                launch = false;
        }

        return new SetupOptions
        {
            InstallDir = dir,
            DesktopShortcut = desktop,
            ShellIntegration = shell,
            StartWithWindows = startup,
            LaunchAfterInstall = launch
        };
    }

    public static SetupResult Install(SetupOptions options, Action<int, string>? progress)
    {
        var warnings = new List<string>();
        var target = ValidateTarget(options.InstallDir);
        var self = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot determine the setup executable path.");
        var selfDir = Path.GetDirectoryName(self) ?? "";
        if (PathsEqual(target, selfDir))
            throw new InvalidOperationException(
                "Run the setup from outside the folder you are installing into.");

        var destExe = Path.Combine(target, "RapidFetch.exe");
        var existing = File.Exists(destExe);

        Report(progress, 2, "Preparing install folder...");
        StopProcesses(target, destExe);

        Directory.CreateDirectory(target);
        if (existing)
        {
            Report(progress, 8, "Removing the previous program files...");
            ClearDirectory(target);
        }
        else if (Directory.EnumerateFileSystemEntries(target).Any())
        {
            warnings.Add("Existing unrelated files in the install folder were left untouched.");
            Log(warnings[^1]);
        }

        Report(progress, 14, "Copying RapidFetch...");
        File.Copy(self, destExe, overwrite: true);
        File.SetAttributes(destExe, FileAttributes.Normal);
        Unblock(destExe);

        Report(progress, 42, "Installing the browser extension...");
        var extensionDir = Path.Combine(target, "extension");
        var hostDir = Path.Combine(target, "host");
        if (EmbeddedAssets.ExtractExtensionTo(extensionDir) == 0 ||
            !File.Exists(Path.Combine(extensionDir, "manifest.json")))
            throw new InvalidOperationException("The embedded browser extension is missing.");
        if (EmbeddedAssets.ExtractHostTo(hostDir) == 0 ||
            !File.Exists(Path.Combine(hostDir, "extension-id.txt")))
            throw new InvalidOperationException("The embedded native-host identity is missing.");

        Report(progress, 56, "Writing documentation and uninstaller...");
        EmbeddedAssets.ExtractSetupAssets(target);
        var uninstallScript = Path.Combine(target, "uninstall.ps1");
        if (!File.Exists(uninstallScript))
            throw new InvalidOperationException("The uninstaller could not be written.");

        var extensionId = (EmbeddedAssets.ReadHostFile("extension-id.txt") ?? "").Trim();
        if (extensionId.Length == 0)
            throw new InvalidOperationException("The extension identity is missing.");

        Report(progress, 64, "Registering the browser native host...");
        SystemIntegration.WriteHostManifest(destExe, extensionId);
        SystemIntegration.RegisterNativeHost(SystemIntegration.HostManifestPath);

        Report(progress, 74, "Applying integration options...");
        SystemIntegration.RegisterShell(destExe, options.ShellIntegration);
        SystemIntegration.RegisterStartup(destExe, options.StartWithWindows);
        ApplySettings(options);

        Report(progress, 84, "Creating shortcuts...");
        CreateShortcut(Path.Combine(StartMenuDir, "RapidFetch.lnk"), destExe, target, warnings);
        var desktopLink = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "RapidFetch.lnk");
        if (options.DesktopShortcut)
            CreateShortcut(desktopLink, destExe, target, warnings);
        else
            TryDelete(desktopLink);

        Report(progress, 92, "Registering uninstall information...");
        RegisterUninstallEntry(target, destExe, uninstallScript);

        Report(progress, 100, "RapidFetch is ready.");
        Log($"Installed {destExe}");
        return new SetupResult(target, destExe, extensionDir, warnings);
    }

    public static void Launch(string exe, string workingDirectory)
    {
        try
        {
            Process.Start(new ProcessStartInfo(exe)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log("Launch failed: " + ex.Message);
        }
    }

    private static string ValidateTarget(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("Choose an install folder.");

        var target = Path.GetFullPath(raw.Trim());
        var root = Path.GetPathRoot(target);
        if (string.IsNullOrWhiteSpace(root) || PathsEqual(target, root.TrimEnd('\\') + "\\"))
            throw new InvalidOperationException("Choose a folder other than the drive root.");

        if (target.Length > 220)
            throw new InvalidOperationException("The install path is too long.");

        if (File.Exists(target))
            throw new InvalidOperationException("The install path points to a file.");

        Directory.CreateDirectory(target);
        var probe = Path.Combine(target, ".rapidfetch-write-test");
        try
        {
            File.WriteAllText(probe, "ok");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "RapidFetch cannot write to that folder. Choose a per-user location such as " +
                DefaultInstallDir + ".", ex);
        }
        finally
        {
            TryDelete(probe);
        }

        return target.TrimEnd('\\');
    }

    private static void StopProcesses(string target, string destExe)
    {
        var folder = target.TrimEnd('\\') + "\\";
        var candidates = new List<Process>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.Id == Environment.ProcessId) continue;
                string? path = null;
                try { path = process.MainModule?.FileName; } catch { }

                var ownApp = process.ProcessName.Equals("RapidFetch", StringComparison.OrdinalIgnoreCase);
                var inTarget = path is not null &&
                               (PathsEqual(path, destExe) ||
                                path.StartsWith(folder, StringComparison.OrdinalIgnoreCase));
                if (ownApp || inTarget)
                    candidates.Add(process);
                else
                    process.Dispose();
            }
            catch
            {
                process.Dispose();
            }
        }

        if (candidates.Count == 0) return;

        Log("Closing running RapidFetch instances...");
        foreach (var process in candidates)
        {
            try
            {
                if (process.MainWindowHandle != IntPtr.Zero)
                    process.CloseMainWindow();
            }
            catch { }
        }

        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline && candidates.Any(p => !HasExited(p)))
            Thread.Sleep(150);

        foreach (var process in candidates)
        {
            if (!HasExited(process))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
            }
            process.Dispose();
        }
        Thread.Sleep(300);
    }

    private static bool HasExited(Process process)
    {
        try { return process.HasExited; } catch { return true; }
    }

    private static void ClearDirectory(string target)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(target))
        {
            var attributes = File.GetAttributes(entry);
            if (attributes.HasFlag(FileAttributes.Directory))
                Directory.Delete(entry, recursive: true);
            else
            {
                File.SetAttributes(entry, FileAttributes.Normal);
                File.Delete(entry);
            }
        }
    }

    private static void ApplySettings(SetupOptions options)
    {
        var path = SystemIntegration.SettingsPath;
        if (File.Exists(path))
            TryCopy(path, path + ".bak");

        var settings = SettingsStore.Load(path);
        settings.BrowserIntegrationEnabled = true;
        settings.ShellIntegration = options.ShellIntegration;
        settings.RunAtStartup = options.StartWithWindows;
        SettingsStore.Save(path, settings);
    }

    private static void RegisterUninstallEntry(string target, string exe, string uninstallScript)
    {
        using var key = Registry.CurrentUser.CreateSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\RapidFetch");
        if (key is null) return;

        key.SetValue("DisplayName", "RapidFetch Download Manager");
        key.SetValue("DisplayVersion", ProgramInfo.Version);
        key.SetValue("Publisher", "RapidFetch");
        key.SetValue("DisplayIcon", exe);
        key.SetValue("InstallLocation", target);
        key.SetValue("UninstallString",
            $"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"{uninstallScript}\"");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);

        try
        {
            var bytes = Directory.EnumerateFiles(target, "*", SearchOption.AllDirectories)
                .Sum(file => new FileInfo(file).Length);
            key.SetValue("EstimatedSize", (int)Math.Min(int.MaxValue, bytes / 1024),
                RegistryValueKind.DWord);
        }
        catch { }
    }

    private static void CreateShortcut(string linkPath, string exe, string workingDirectory,
        List<string> warnings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(linkPath)!);
            var shellType = Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new InvalidOperationException("WScript.Shell is unavailable.");
            dynamic shell = Activator.CreateInstance(shellType)
                ?? throw new InvalidOperationException("Cannot create the shortcut helper.");
            try
            {
                dynamic link = shell.CreateShortcut(linkPath);
                try
                {
                    link.TargetPath = exe;
                    link.WorkingDirectory = workingDirectory;
                    link.IconLocation = exe + ",0";
                    link.Description = "RapidFetch Download Manager";
                    link.Save();
                }
                finally
                {
                    Marshal.FinalReleaseComObject(link);
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
        catch (Exception ex)
        {
            var warning = $"Shortcut could not be created: {ex.Message}";
            warnings.Add(warning);
            Log(warning);
        }
    }

    private static string StartMenuDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        @"Microsoft\Windows\Start Menu\Programs");

    private static void Report(Action<int, string>? progress, int percent, string message)
    {
        try { progress?.Invoke(percent, message); } catch { }
        Log(message);
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(a.TrimEnd('\\'), b.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void TryCopy(string source, string destination)
    {
        try { File.Copy(source, destination, overwrite: true); } catch { }
    }

    private static void Unblock(string path)
    {
        try { DeleteFile(path + ":Zone.Identifier"); } catch { }
    }

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}",
                Encoding.UTF8);
        }
        catch { }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool DeleteFile(string fileName);
}
