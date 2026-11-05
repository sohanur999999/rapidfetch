using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using RapidFetch.Core.Engine;
using RapidFetch.Core.Services;

namespace RapidFetch.App.Views;

public sealed record GrabbedFile(string Url, string Page);

public partial class GrabberWindow : Window
{
    private static App Host => (App)Application.Current;
    private readonly ObservableCollection<GrabbedFile> _files = new();
    private readonly SiteGrabber _grabber;
    private CancellationTokenSource? _cts;

    public GrabberWindow()
    {
        _grabber = new SiteGrabber(Host.Settings);
        InitializeComponent();
        ResultsList.ItemsSource = _files;
        StartUrlBox.Text = "https://";
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        var start = StartUrlBox.Text.Trim();
        if (!Uri.TryCreate(start, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            MessageBox.Show(this, "Enter a valid http(s) start URL.", "RapidFetch");
            return;
        }

        _files.Clear();
        _cts = new CancellationTokenSource();
        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        StatusText.Text = "Crawling…";

        int.TryParse(DepthBox.Text, out var depth);
        int.TryParse(PagesBox.Text, out var pages);
        int.TryParse(DelayBox.Text, out var delay);

        var options = new GrabberOptions
        {
            StartUrl = start,
            MaxDepth = Math.Clamp(depth <= 0 ? 3 : depth, 1, 10),
            MaxPages = Math.Clamp(pages <= 0 ? 200 : pages, 1, 10000),
            SameHostOnly = SameHostBox.IsChecked == true,
            FollowLinks = FollowBox.IsChecked == true,
            Extensions = ExtensionsBox.Text,
            IncludePattern = IncludeBox.Text.Trim(),
            ExcludePattern = ExcludeBox.Text.Trim(),
            DelayMs = Math.Clamp(delay, 0, 5000)
        };

        var progress = new Progress<GrabberProgress>(p =>
        {
            ProgressText.Text = $"{p.PagesScanned} pages · {p.LinksFound} files";
            if (p.Done)
            {
                StatusText.Text = p.Error is null ? "Crawl finished." : "Crawl stopped: " + p.Error;
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
            }
        });

        try
        {
            await _grabber.CrawlAsync(options, progress,
                (url, page) => Dispatcher.Invoke(() => _files.Add(new GrabbedFile(url, page ?? ""))),
                _cts.Token);
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
        finally
        {
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        StopButton.IsEnabled = false;
        StatusText.Text = "Stopping…";
    }

    private async void AddSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = ResultsList.SelectedItems.Cast<GrabbedFile>().ToList();
        if (selected.Count == 0) selected = _files.ToList();
        await AddAllAsync(selected);
    }

    private async void AddAll_Click(object sender, RoutedEventArgs e)
    {
        await AddAllAsync(_files.ToList());
    }

    private async Task AddAllAsync(List<GrabbedFile> files)
    {
        if (files.Count == 0) return;
        StatusText.Text = $"Adding {files.Count} downloads…";

        foreach (var file in files)
        {
            try
            {
                await Host.Engine.AddAsync(new AddDownloadRequest
                {
                    Url = file.Url,
                    Referrer = string.IsNullOrWhiteSpace(file.Page) ? null : file.Page,
                    StartNow = true
                });
            }
            catch { }
        }

        StatusText.Text = $"Queued {files.Count} downloads.";
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _files.Clear();
        ProgressText.Text = "Ready.";
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        Close();
    }
}
