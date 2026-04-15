using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace MindAtlas.Desktop.Views;

public partial class MainWindow : Window
{
    private static readonly string[] SupportedExtensions =
        [".md", ".txt", ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".webp"];

    private string _rawDir = "";

    public MainWindow()
    {
        InitializeComponent();

        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    public void SetServerUrl(string url)
    {
        WebView.Source = new Uri(url);
    }

    public void SetRawDirectory(string rawDir)
    {
        _rawDir = rawDir;
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
            }
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
            if (data is not null)
            {
                foreach (var item in data.Items)
                {
                    if (!item.Formats.Contains(DataFormat.Text)) continue;

                    var raw = await item.TryGetRawAsync(DataFormat.Text);
                    if (raw is string text && !string.IsNullOrWhiteSpace(text))
                    {
                        var path = Path.Combine(_rawDir, $"{timestamp}_clipboard.md");
                        await File.WriteAllTextAsync(path, text);
                        e.Handled = true;
                        return;
                    }
                }
            }
        }
    }
}
