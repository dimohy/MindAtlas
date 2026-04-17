using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace MindAtlas.Desktop.Services;

// ICoreWebView2.get_Settings is the first vtable method after IUnknown.
[GeneratedComInterface]
[Guid("76ECEACB-0462-4D94-AC83-423A6793775E")]
internal partial interface ICoreWebView2
{
    void GetSettings(out IntPtr settings);
}

// ICoreWebView2Settings: methods declared in exact vtable order up to and
// including put_IsStatusBarEnabled (the 8th method after IUnknown).
[GeneratedComInterface]
[Guid("E562E4F0-D7FA-43AC-8D71-C05150499F00")]
internal partial interface ICoreWebView2Settings
{
    // IsScriptEnabled
    void GetIsScriptEnabled(out int value);
    void PutIsScriptEnabled(int value);
    // IsWebMessageEnabled
    void GetIsWebMessageEnabled(out int value);
    void PutIsWebMessageEnabled(int value);
    // AreDefaultScriptDialogsEnabled
    void GetAreDefaultScriptDialogsEnabled(out int value);
    void PutAreDefaultScriptDialogsEnabled(int value);
    // IsStatusBarEnabled  <-- target
    void GetIsStatusBarEnabled(out int value);
    void PutIsStatusBarEnabled(int value);
}

/// <summary>
/// Minimal source-generated COM interop to disable the WebView2 status bar
/// (the "link URL" tooltip at the bottom-left of the control). Avalonia's
/// <c>IWindowsWebView2PlatformHandle.CoreWebView2</c> only exposes the raw
/// <see cref="IntPtr"/> to <c>ICoreWebView2</c>, so we query the interfaces
/// manually. GUIDs are taken from the official WebView2 SDK IDL.
/// </summary>
internal static class WebView2StatusBar
{
    private static readonly StrategyBasedComWrappers s_wrappers = new();

    public static bool TryDisable(IntPtr coreWebView2)
    {
        if (coreWebView2 == IntPtr.Zero) return false;
        try
        {
            var core = (ICoreWebView2)s_wrappers.GetOrCreateObjectForComInstance(
                coreWebView2, CreateObjectFlags.None);
            core.GetSettings(out var settingsPtr);
            if (settingsPtr == IntPtr.Zero) return false;
            try
            {
                var settings = (ICoreWebView2Settings)s_wrappers.GetOrCreateObjectForComInstance(
                    settingsPtr, CreateObjectFlags.None);
                settings.PutIsStatusBarEnabled(0);
                return true;
            }
            finally
            {
                Marshal.Release(settingsPtr);
            }
        }
        catch
        {
            return false;
        }
    }
}
