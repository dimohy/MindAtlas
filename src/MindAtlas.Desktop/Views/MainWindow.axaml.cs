using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace MindAtlas.Desktop.Views;

public partial class MainWindow : Window
{
    private static readonly string[] SupportedExtensions =
        [".md", ".txt", ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".webp"];

    private string _rawDir = "";
    private string _serverUrl = "";
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromMinutes(5) };

    public MainWindow()
    {
        InitializeComponent();

        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);

        // Hide the WebView2 status bar that shows link URLs on hover.
        WebView.AdapterCreated += (_, _) => TryDisableWebViewStatusBar();
    }

    private void TryDisableWebViewStatusBar()
    {
        if (WebView.TryGetPlatformHandle() is IWindowsWebView2PlatformHandle handle)
        {
            Services.WebView2StatusBar.TryDisable(handle.CoreWebView2);
        }
    }

    public void SetServerUrl(string url)
    {
        _serverUrl = url.TrimEnd('/');
        // Append a unique query so WebView2 never serves a cached copy of
        // the root document (which would pin us to a stale CSS URL and make
        // theme tweaks invisible across launches).
        var bust = "nocache=" + Guid.NewGuid().ToString("N");
        var sep = url.Contains('?') ? '&' : '?';
        WebView.Source = new Uri(url + sep + bust);
    }

    public void SetRawDirectory(string rawDir)
    {
        _rawDir = rawDir;
    }

    public void ShowQuickInput()
    {
        var quickInput = new QuickInputWindow(_rawDir, _serverUrl, _httpClient);
        quickInput.Show();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Minimize to tray instead of closing
        e.Cancel = true;
        Hide();
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer is not null)
            DropOverlay.IsVisible = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        DropOverlay.IsVisible = false;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        DropOverlay.IsVisible = false;

        if (e.DataTransfer is null || string.IsNullOrEmpty(_rawDir)) return;

        Directory.CreateDirectory(_rawDir);
        var ingestedFiles = new List<string>();

        foreach (var item in e.DataTransfer.Items)
        {
            if (!item.Formats.Contains(DataFormat.File)) continue;

            var raw = item.TryGetRaw(DataFormat.File);
            var storageItems = raw switch
            {
                IEnumerable<IStorageItem> list => list,
                IStorageItem single => [single],
                _ => Enumerable.Empty<IStorageItem>()
            };

            foreach (var si in storageItems.OfType<IStorageFile>())
            {
                var ext = Path.GetExtension(si.Name).ToLowerInvariant();
                if (Array.IndexOf(SupportedExtensions, ext) < 0) continue;

                var destName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{si.Name}";
                var destPath = Path.Combine(_rawDir, destName);

                await using var src = await si.OpenReadAsync();
                await using var dst = File.Create(destPath);
                await src.CopyToAsync(dst);

                ingestedFiles.Add(destPath);
            }
        }

        // Trigger ingest for each dropped file via API
        foreach (var filePath in ingestedFiles)
        {
            _ = TriggerIngestAsync(filePath);
        }
    }

    protected override async void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Ctrl+V clipboard paste
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.V)
        {
            if (string.IsNullOrEmpty(_rawDir)) return;

            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null) return;

            Directory.CreateDirectory(_rawDir);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            var data = await clipboard.TryGetDataAsync();
            if (data is null) return;

            foreach (var item in data.Items)
            {
                // Try text
                if (item.Formats.Contains(DataFormat.Text))
                {
                    var raw = await item.TryGetRawAsync(DataFormat.Text);
                    if (raw is string text && !string.IsNullOrWhiteSpace(text))
                    {
                        var path = Path.Combine(_rawDir, $"{timestamp}_clipboard.md");
                        await File.WriteAllTextAsync(path, text);
                        e.Handled = true;
                        _ = TriggerIngestAsync(path);
                        return;
                    }
                }

                // Try image (bitmap)
                if (item.Formats.Contains(DataFormat.Bitmap))
                {
                    var raw = await item.TryGetRawAsync(DataFormat.Bitmap);
                    if (raw is byte[] imageBytes)
                    {
                        var path = Path.Combine(_rawDir, $"{timestamp}_clipboard.png");
                        await File.WriteAllBytesAsync(path, imageBytes);
                        e.Handled = true;
                        _ = TriggerIngestAsync(path);
                        return;
                    }

                    if (raw is Stream imageStream)
                    {
                        var path = Path.Combine(_rawDir, $"{timestamp}_clipboard.png");
                        await using var fs = File.Create(path);
                        await imageStream.CopyToAsync(fs);
                        e.Handled = true;
                        _ = TriggerIngestAsync(path);
                        return;
                    }
                }
            }
        }
    }

    private int _pendingIngestCount;

    /// <summary>
    /// Triggers ingest for a file by reading its content and calling the ingest API.
    /// Shows status bar during processing.
    /// </summary>
    private async Task TriggerIngestAsync(string filePath)
    {
        if (string.IsNullOrEmpty(_serverUrl)) return;

        var fileName = Path.GetFileName(filePath);
        Interlocked.Increment(ref _pendingIngestCount);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IngestStatusText.Text = $"Ingesting: {fileName}...";
            IngestStatusBar.IsVisible = true;
        });

        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            var body = JsonSerializer.Serialize(new { content, title = Path.GetFileNameWithoutExtension(fileName) });

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_serverUrl}/api/ingest")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            await _httpClient.SendAsync(request);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IngestStatusText.Text = $"Ingested: {fileName}";
            });
        }
        catch
        {
            // FileSystemWatcher will pick up the file as a backup path
        }
        finally
        {
            if (Interlocked.Decrement(ref _pendingIngestCount) == 0)
            {
                // Hide status bar after a brief delay
                await Task.Delay(2000);
                if (_pendingIngestCount == 0)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        IngestStatusBar.IsVisible = false;
                    });
                }
            }
        }
    }
}
