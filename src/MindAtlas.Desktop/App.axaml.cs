using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
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

    private static readonly string DataRoot =
        Environment.GetEnvironmentVariable("MINDATLAS_DATA_ROOT")
        ?? Path.Combine(AppContext.BaseDirectory, "data");

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Start embedded server
            _serverHost = new EmbeddedServerHost(DataRoot);
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

            // Setup tray icon
            SetupTrayIcon(desktop);

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

        var showItem = new NativeMenuItem("Open MindAtlas");
        showItem.Click += (_, _) => ShowWindow();
        menu.Add(showItem);

        menu.Add(new NativeMenuItemSeparator());

        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) =>
        {
            _trayIcon?.Dispose();
            _hotkeyService?.Dispose();
            _ = _serverHost?.DisposeAsync();
            desktop.Shutdown();
        };
        menu.Add(exitItem);

        _trayIcon = new TrayIcon
        {
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
}
