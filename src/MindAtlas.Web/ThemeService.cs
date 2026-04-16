using Microsoft.JSInterop;

namespace MindAtlas.Web;

/// <summary>
/// Applies the UI theme (light/dark/auto) via a small JS helper and tracks the
/// currently applied value so preview/revert flows (e.g. Settings page) can work.
/// </summary>
public sealed class ThemeService(IJSRuntime js)
{
    public string Current { get; private set; } = "auto";

    public async Task ApplyAsync(string theme)
    {
        var normalized = theme switch
        {
            "light" or "dark" or "auto" => theme,
            _ => "auto"
        };
        Current = normalized;
        await js.InvokeVoidAsync("mindAtlasTheme.apply", normalized);
    }
}
