using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.Configuration;
using MindAtlas.Desktop.Services;
using MindAtlas.Desktop.ViewModels;
using MindAtlas.Desktop.Views;

namespace MindAtlas.Desktop;

public partial class App : Application
{
    private EmbeddedServerHost? _serverHost;
    private GlobalHotkeyService? _hotkeyService;
    private MainWindow? _mainWindow;
    private TrayIcon? _trayIcon;
    private FileSystemWatcher? _settingsWatcher;

    // Tray menu items kept for live language updates
    private NativeMenuItem? _trayShowItem;
    private NativeMenuItem? _trayQuickNoteItem;
    private NativeMenuItem? _traySettingsItem;
    private NativeMenuItem? _trayExitItem;

    private static readonly string DataRoot =
        Environment.GetEnvironmentVariable("MINDATLAS_DATA_ROOT")
        ?? Path.Combine(AppContext.BaseDirectory, "data");

    private static string? LoadGitHubToken()
    {
        // Priority: env var > user-secrets / appsettings.json
        var envToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrEmpty(envToken))
            return envToken;

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.desktop.json", optional: true)
            .AddUserSecrets<App>(optional: true)
            .Build();

        var token = config["MindAtlas:GitHubToken"];
        return string.IsNullOrEmpty(token) ? null : token;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Start embedded server
            _serverHost = new EmbeddedServerHost(DataRoot, LoadGitHubToken());
            try
            {
                await _serverHost.StartAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to start server: {ex.Message}");
            }

            // Create main window
            _mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
            _mainWindow.SetRawDirectory(Path.Combine(DataRoot, "raw"));
            _mainWindow.SetServerUrl(_serverHost.BaseUrl);

            desktop.MainWindow = _mainWindow;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Load UI language for desktop strings
            DesktopLocalizer.LoadLanguage();

            // Setup tray icon
            SetupTrayIcon(desktop);

            // Watch appsettings.json for language changes (triggered by Settings page)
            WatchSettingsForLanguageChange();

            // Setup global hotkey (Windows only)
            if (OperatingSystem.IsWindows())
            {
                _hotkeyService = new GlobalHotkeyService();
                _hotkeyService.HotkeyPressed += () =>
                    Dispatcher.UIThread.Post(ToggleWindow);
                _hotkeyService.Start();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var menu = new NativeMenu();

        _trayShowItem = new NativeMenuItem(DesktopLocalizer.Get("tray.open"));
        _trayShowItem.Click += (_, _) => ShowWindow();
        menu.Add(_trayShowItem);

        _trayQuickNoteItem = new NativeMenuItem(DesktopLocalizer.Get("tray.quick_note"));
        _trayQuickNoteItem.Click += (_, _) =>
            Dispatcher.UIThread.Post(() => _mainWindow?.ShowQuickInput());
        menu.Add(_trayQuickNoteItem);

        menu.Add(new NativeMenuItemSeparator());

        _traySettingsItem = new NativeMenuItem(DesktopLocalizer.Get("tray.settings"));
        _traySettingsItem.Click += (_, _) =>
        {
            ShowWindow();
            _mainWindow?.SetServerUrl($"{_serverHost?.BaseUrl}/settings");
        };
        menu.Add(_traySettingsItem);

        menu.Add(new NativeMenuItemSeparator());

        _trayExitItem = new NativeMenuItem(DesktopLocalizer.Get("tray.exit"));
        _trayExitItem.Click += (_, _) =>
        {
            _settingsWatcher?.Dispose();
            _trayIcon?.Dispose();
            _hotkeyService?.Dispose();
            _ = _serverHost?.DisposeAsync();
            desktop.Shutdown();
        };
        menu.Add(_trayExitItem);

        var trayBitmap = new Avalonia.Media.Imaging.Bitmap(
            Avalonia.Platform.AssetLoader.Open(
                new Uri("avares://MindAtlas.Desktop/Assets/mindatlas.ico")));

        _trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(trayBitmap),
            ToolTipText = "MindAtlas",
            Menu = menu,
            IsVisible = true,
        };
        _trayIcon.Clicked += (_, _) => ShowWindow();
    }

    private void ToggleWindow()
    {
        if (_mainWindow is null) return;

        if (_mainWindow.IsVisible)
            _mainWindow.Hide();
        else
            ShowWindow();
    }

    private void ShowWindow()
    {
        if (_mainWindow is null) return;

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void WatchSettingsForLanguageChange()
    {
        var dir = AppContext.BaseDirectory;
        var file = "appsettings.json";
        if (!File.Exists(Path.Combine(dir, file))) return;

        _settingsWatcher = new FileSystemWatcher(dir, file)
        {
            NotifyFilter = NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };
        _settingsWatcher.Changed += (_, _) =>
        {
            // Debounce: file may be written multiple times
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var prevLang = DesktopLocalizer.CurrentLanguage;
                    DesktopLocalizer.LoadLanguage();
                    if (prevLang == DesktopLocalizer.CurrentLanguage) return;

                    // Update tray menu item text
                    if (_trayShowItem is not null) _trayShowItem.Header = DesktopLocalizer.Get("tray.open");
                    if (_trayQuickNoteItem is not null) _trayQuickNoteItem.Header = DesktopLocalizer.Get("tray.quick_note");
                    if (_traySettingsItem is not null) _traySettingsItem.Header = DesktopLocalizer.Get("tray.settings");
                    if (_trayExitItem is not null) _trayExitItem.Header = DesktopLocalizer.Get("tray.exit");
                }
                catch (Exception ex)
                {
                    // Swallow: unhandled exceptions on the UI thread would crash the app.
                    Console.Error.WriteLine($"Settings watcher handler failed: {ex.Message}");
                }
            });
        };
    }
}
