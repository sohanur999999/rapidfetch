using System.Windows;
using System.Windows.Input;
using RapidFetch.Core.Engine;
using Forms = System.Windows.Forms;

namespace RapidFetch.App.Views;

public partial class AddDownloadWindow : Window
{
    private static App Host => (App)Application.Current;
    public bool Added { get; private set; }

    public string UrlText
    {
        set => UrlBox.Text = value;
    }

    public AddDownloadWindow()
    {
        InitializeComponent();
        CategoryBox.ItemsSource = Host.Settings.Categories.Select(c => c.Name).ToList();
        CategoryBox.SelectedItem = "General";
        FolderBox.Text = Host.Settings.DownloadFolder;
        ConnectionsSlider.Value = Math.Clamp(Host.Settings.DefaultConnections, 1, 32);
        Loaded += (_, _) =>
        {
            UrlBox.Focus();
            UrlBox.CaretIndex = UrlBox.Text.Length;
        };
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void UrlBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var lines = UrlBox.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        UrlHint.Text = lines.Count > 1
            ? $"{lines.Count} URLs queued"
            : "One URL per line. Paste a playlist or a whole list.";
    }

    private void Connections_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ConnectionsValue is not null)
            ConnectionsValue.Text = ((int)e.NewValue).ToString();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Choose where RapidFetch saves downloads",
            SelectedPath = Directory.Exists(FolderBox.Text) ? FolderBox.Text : Host.Settings.DownloadFolder,
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog() == Forms.DialogResult.OK)
            FolderBox.Text = dialog.SelectedPath;
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var urls = UrlBox.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

        if (urls.Count == 0)
        {
            ShowError("Paste at least one download address.");
            return;
        }

        foreach (var url in urls)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("http" or "https" or "ftp"))
            {
                ShowError($"Not a valid download address: {url}");
                return;
            }
        }

        AddButton.IsEnabled = false;
        AddButton.Content = "Adding…";
        ErrorText.Visibility = Visibility.Collapsed;

        try
        {
            foreach (var url in urls)
            {
                var request = new AddDownloadRequest
                {
                    Url = url,
                    FileName = urls.Count == 1 && !string.IsNullOrWhiteSpace(FileNameBox.Text)
                        ? FileNameBox.Text.Trim() : null,
                    SaveFolder = string.IsNullOrWhiteSpace(FolderBox.Text) ? null : FolderBox.Text.Trim(),
                    Category = CategoryBox.SelectedItem as string,
                    Queue = QueueBox.Text.Trim(),
                    Referrer = string.IsNullOrWhiteSpace(ReferrerBox.Text) ? null : ReferrerBox.Text.Trim(),
                    Connections = (int)ConnectionsSlider.Value,
                    StartNow = StartNowBox.IsChecked == true
                };
                await Host.Engine.AddAsync(request);
            }

            Added = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            AddButton.IsEnabled = true;
            AddButton.Content = "Download";
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
