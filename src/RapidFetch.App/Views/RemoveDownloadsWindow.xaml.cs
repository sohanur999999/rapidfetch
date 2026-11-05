using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RapidFetch.Core.Models;

namespace RapidFetch.App.Views;

public partial class RemoveDownloadsWindow : Window
{
    private readonly int _itemCount;
    private readonly int _fileCount;

    public bool DeleteFiles => DeleteFilesCheck.IsChecked == true;

    public RemoveDownloadsWindow(IReadOnlyList<DownloadItem> items)
    {
        InitializeComponent();

        int count = items.Count;
        _itemCount = count;
        MessageText.Text = count == 1
            ? $"Remove \"{items[0].DisplayName}\" from the list?"
            : $"Remove {count} downloads from the list?";

        _fileCount = items.Count(item =>
            !string.IsNullOrWhiteSpace(item.SavePath) && File.Exists(item.SavePath));

        if (_fileCount == 0)
        {
            DeleteFilesCheck.IsEnabled = false;
            DeleteDetailText.Text = "No downloaded files were found on disk.";
        }
        else
        {
            DeleteDetailText.Text =
                $"Permanently deletes {_fileCount} file{(_fileCount == 1 ? "" : "s")} " +
                "from storage. This cannot be undone.";
        }

        UpdateState();
        Loaded += (_, _) => CancelButton.Focus();
    }

    private void UpdateState()
    {
        bool deleteFiles = DeleteFiles;
        if (_fileCount == 0)
        {
            SummaryText.Text = _itemCount == 1
                ? "The downloaded file is no longer on disk."
                : "The downloaded files are no longer on disk.";
            SummaryText.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        }
        else if (deleteFiles)
        {
            SummaryText.Text = _fileCount == 1
                ? "1 downloaded file will be permanently deleted."
                : $"{_fileCount} downloaded files will be permanently deleted.";
            SummaryText.SetResourceReference(TextBlock.ForegroundProperty, "Danger");
        }
        else
        {
            SummaryText.Text = _fileCount == 1
                ? "The downloaded file stays in your download folder."
                : "The downloaded files stay in your download folder.";
            SummaryText.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        }

        ConfirmButton.Content = deleteFiles ? "Delete files and remove" : "Remove";
        ConfirmButton.Style = (Style)FindResource(
            deleteFiles ? "DeleteConfirmButton" : "PrimaryButton");
        System.Windows.Automation.AutomationProperties.SetName(
            ConfirmButton, deleteFiles ? "Delete files and remove" : "Remove downloads");
    }

    private void DeleteFiles_Changed(object sender, RoutedEventArgs e) => UpdateState();

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
