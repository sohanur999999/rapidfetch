using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using RapidFetch.Core.Services;
using Forms = System.Windows.Forms;

namespace RapidFetch.App.Views;

public partial class SetupWindow : Window
{
    private bool _installing;
    private SetupResult? _result;

    public SetupWindow()
    {
        InitializeComponent();
        InstallPathBox.Text = SetupInstaller.DefaultInstallDir;
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && !_installing)
            DragMove();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var current = InstallPathBox.Text.Trim();
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Choose where RapidFetch should be installed",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(current) ? current : SetupInstaller.DefaultInstallDir
        };
        if (dialog.ShowDialog() == Forms.DialogResult.OK)
            InstallPathBox.Text = dialog.SelectedPath;
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (_installing) return;

        string target;
        try
        {
            target = Path.GetFullPath(InstallPathBox.Text.Trim());
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "The install path is not valid.\n\n" + ex.Message,
                "RapidFetch Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (Directory.Exists(target) &&
                !File.Exists(Path.Combine(target, "RapidFetch.exe")) &&
                Directory.EnumerateFileSystemEntries(target).Any())
            {
                var answer = MessageBox.Show(this,
                    "That folder already contains other files. RapidFetch will add its files " +
                    "without deleting them.\n\nContinue?",
                    "RapidFetch Setup", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (answer != MessageBoxResult.Yes) return;
            }
        }
        catch { }

        var options = new SetupOptions
        {
            InstallDir = target,
            DesktopShortcut = DesktopShortcutBox.IsChecked == true,
            ShellIntegration = ShellIntegrationBox.IsChecked == true,
            StartWithWindows = StartWithWindowsBox.IsChecked == true
        };

        SetInstalling(true);
        StatusText.Text = "Starting installation...";

        var completion = new TaskCompletionSource<SetupResult>();
        var worker = new Thread(() =>
        {
            try
            {
                var result = SetupInstaller.Install(options, (percent, message) =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        InstallProgress.Value = percent;
                        StatusText.Text = message;
                    }));
                });
                completion.SetResult(result);
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });
        worker.SetApartmentState(ApartmentState.STA);
        worker.IsBackground = true;
        worker.Start();

        try
        {
            var result = await completion.Task;
            _result = result;
            ShowFinished(result);
        }
        catch (Exception ex)
        {
            SetInstalling(false);
            StatusText.Text = "Installation failed.";
            MessageBox.Show(this, ex.Message, "RapidFetch Setup",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetInstalling(bool installing)
    {
        _installing = installing;
        SetupPanel.IsEnabled = !installing;
        CloseButton.IsEnabled = !installing;
        CancelButton.IsEnabled = !installing;
        InstallButton.IsEnabled = !installing;
        InstallButton.Content = installing ? "Installing..." : "Install RapidFetch";
        InstallProgress.Visibility = installing ? Visibility.Visible : Visibility.Hidden;
        if (!installing) InstallProgress.Value = 0;
    }

    private void ShowFinished(SetupResult result)
    {
        _installing = false;
        SetupPanel.Visibility = Visibility.Collapsed;
        FinishPanel.Visibility = Visibility.Visible;
        FinishPath.Text = result.InstallDir;
        FinishExtensionPath.Text = result.ExtensionDir;
        InstallProgress.Value = 100;
        InstallProgress.Visibility = Visibility.Visible;
        StatusText.Text = "Installation complete.";
        CancelButton.Visibility = Visibility.Collapsed;
        InstallButton.Visibility = Visibility.Collapsed;
        CloseButton.IsEnabled = true;

        if (result.Warnings.Count > 0)
        {
            MessageBox.Show(this, string.Join("\n\n", result.Warnings),
                "RapidFetch Setup", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OpenExtension_Click(object sender, RoutedEventArgs e)
    {
        if (_result is null) return;
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe",
                $"\"{_result.ExtensionDir}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "RapidFetch Setup",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenEdgeExtensions_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("msedge.exe", "edge://extensions")
            {
                UseShellExecute = true
            });
        }
        catch
        {
            MessageBox.Show(this,
                "Open Edge, go to edge://extensions, enable Developer mode, and choose Load unpacked.",
                "RapidFetch Setup", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Launch_Click(object sender, RoutedEventArgs e)
    {
        if (_result is null) return;
        SetupInstaller.Launch(_result.ExePath, _result.InstallDir);
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (!_installing) Close();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_installing) e.Cancel = true;
    }
}
