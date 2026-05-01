using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace MindAtlas.Desktop.Services;

/// <summary>
/// Registers a global hotkey (Ctrl+Shift+Space) using Windows P/Invoke.
/// Polls for WM_HOTKEY messages on a background thread.
/// Uses LibraryImport source generator for NativeAOT compatibility.
/// </summary>
public sealed partial class GlobalHotkeyService : IDisposable
{
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(nint hWnd, int id);

    [LibraryImport("user32.dll", EntryPoint = "PeekMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PeekMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public nint hwnd;
        public uint message;
        public nint wParam;
        public nint lParam;
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
    private uint _modifiers = MOD_CONTROL | MOD_SHIFT;
    private uint _virtualKey = VK_SPACE;

    public event Action? HotkeyPressed;

    /// <summary>
    /// Sets the hotkey combination. Must be called before Start().
    /// </summary>
    public void SetHotkey(uint modifiers, uint virtualKey)
    {
        _modifiers = modifiers;
        _virtualKey = virtualKey;
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _thread = new Thread(MessageLoop) { IsBackground = true, Name = "GlobalHotkey" };
        _thread.Start();
    }

    private void MessageLoop()
    {
        if (!RegisterHotKey(0, HOTKEY_ID, _modifiers, _virtualKey))
            return;

        while (_running)
        {
            if (PeekMessage(out var msg, 0, WM_HOTKEY, WM_HOTKEY, PM_REMOVE))
            {
                if (msg.message == WM_HOTKEY)
                    HotkeyPressed?.Invoke();
            }
            Thread.Sleep(50);
        }

        UnregisterHotKey(0, HOTKEY_ID);
    }

    public void Dispose()
    {
        _running = false;
        _thread?.Join(1000);
    }
}
