using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace MindAtlas.Desktop.Views;

public partial class QuickInputWindow : Window
{
    private readonly string _rawDir;
    private readonly string _serverUrl;
    private readonly HttpClient _httpClient;

    public QuickInputWindow() : this("", "", new HttpClient()) { }

    public QuickInputWindow(string rawDir, string serverUrl, HttpClient httpClient)
    {
        _rawDir = rawDir;
        _serverUrl = serverUrl;
        _httpClient = httpClient;

        InitializeComponent();

        // Apply localized strings
        Title = DesktopLocalizer.Get("quick_note.title");
        NoteInput.PlaceholderText = DesktopLocalizer.Get("quick_note.placeholder");
        SaveButton.Content = DesktopLocalizer.Get("quick_note.save");

        // Center on primary screen (accounting for DPI scaling)
        Opened += (_, _) =>
        {
            var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
            if (screen is not null)
            {
                var scaling = screen.Scaling;
                var winWidthPx = (int)(Width * scaling);
                var winHeightPx = (int)(Height * scaling);
                var x = (screen.WorkingArea.Width - winWidthPx) / 2 + screen.WorkingArea.X;
                var y = (screen.WorkingArea.Height - winHeightPx) / 2 + screen.WorkingArea.Y;
                Position = new PixelPoint(x, y);
            }
        };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            e.Handled = true;
            _ = SaveAndIngestAsync();
        }
        else if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        _ = SaveAndIngestAsync();
    }

    private async Task SaveAndIngestAsync()
    {
        var text = NoteInput.Text;
        if (string.IsNullOrWhiteSpace(text)) return;

        SaveButton.IsEnabled = false;
        NoteInput.IsEnabled = false;
        StatusText.Text = DesktopLocalizer.Get("quick_note.saving");

        Directory.CreateDirectory(_rawDir);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var filePath = Path.Combine(_rawDir, $"{timestamp}_quicknote.md");
        await File.WriteAllTextAsync(filePath, text);

        StatusText.Text = DesktopLocalizer.Get("quick_note.ingesting");

        try
        {
            var body = JsonSerializer.Serialize(new { content = text, title = $"{timestamp}_quicknote" });
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_serverUrl}/api/ingest")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            var response = await _httpClient.SendAsync(request);
            StatusText.Text = response.IsSuccessStatusCode
                ? DesktopLocalizer.Get("quick_note.done")
                : DesktopLocalizer.Get("quick_note.saved_pending");
        }
        catch
        {
            StatusText.Text = DesktopLocalizer.Get("quick_note.saved_pending");
        }

        await Task.Delay(1500);
        Close();
    }
}
