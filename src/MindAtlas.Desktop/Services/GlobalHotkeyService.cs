using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace MindAtlas.Desktop.Services;

/// <summary>
/// Registers a global hotkey (Ctrl+Shift+Space) using Windows P/Invoke.
/// Polls for WM_HOTKEY messages on a background thread.
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint VK_SPACE = 0x20;
    private const uint WM_HOTKEY = 0x0312;
    private const uint PM_REMOVE = 0x0001;
    private const int HOTKEY_ID = 9001;

    private Thread? _thread;
    private volatile bool _running;

    public event Action? HotkeyPressed;

    public void Start()
    {
        if (_running) return;
        _running = true;
        _thread = new Thread(MessageLoop) { IsBackground = true, Name = "GlobalHotkey" };
        _thread.Start();
    }

    private void MessageLoop()
    {
        if (!RegisterHotKey(IntPtr.Zero, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_SPACE))
            return;

        while (_running)
        {
            if (PeekMessage(out var msg, IntPtr.Zero, WM_HOTKEY, WM_HOTKEY, PM_REMOVE))
            {
                if (msg.message == WM_HOTKEY)
                    HotkeyPressed?.Invoke();
            }
            Thread.Sleep(50);
        }

        UnregisterHotKey(IntPtr.Zero, HOTKEY_ID);
    }

    public void Dispose()
    {
        _running = false;
        _thread?.Join(1000);
    }
}
