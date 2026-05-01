using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace MindAtlas.Desktop.Services;

/// <summary>
/// Manages Windows auto-start registration via Registry (HKCU\Software\Microsoft\Windows\CurrentVersion\Run).
/// </summary>
public static class AutoStartService
{
    private const string RegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "MindAtlas";

    public static bool IsEnabled()
    {
        if (!OperatingSystem.IsWindows()) return false;

        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, writable: false);
        return key?.GetValue(AppName) is not null;
    }

    public static void Enable()
    {
        if (!OperatingSystem.IsWindows()) return;

        var exePath = Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName;

        if (string.IsNullOrEmpty(exePath)) return;

        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, writable: true);
        key?.SetValue(AppName, $"\"{exePath}\"");
    }

    public static void Disable()
    {
        if (!OperatingSystem.IsWindows()) return;

        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, writable: true);
        key?.DeleteValue(AppName, throwOnMissingValue: false);
    }

    public static void SetEnabled(bool enabled)
    {
        if (enabled) Enable();
        else Disable();
    }
}
