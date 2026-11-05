using System.Windows;
using System.Windows.Input;
using RapidFetch.Core.Models;

namespace RapidFetch.App.Views;

public partial class UrlPromptWindow : Window
{
    private readonly DownloadItem _item;

    public string Url { get; private set; } = "";

    public UrlPromptWindow(DownloadItem item)
    {
        _item = item;
        InitializeComponent();
        FileNameText.Text = item.DisplayName;
        UrlBox.Text = item.Url;
        Url = item.Url;
        Loaded += (_, _) =>
        {
            UrlBox.Focus();
            UrlBox.SelectAll();
        };
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Update_Click(object sender, RoutedEventArgs e)
    {
        var value = UrlBox.Text.Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            ErrorText.Text = "Enter a full http or https address.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        Url = value;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
