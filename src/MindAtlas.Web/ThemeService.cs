using Microsoft.JSInterop;

namespace MindAtlas.Web;

/// <summary>
/// Applies the UI theme (light/dark/auto) via a small JS helper and tracks the
/// currently applied value so preview/revert flows (e.g. Settings page) can work.
/// </summary>
public sealed class ThemeService(IJSRuntime js)
{
    public string Current { get; private set; } = "auto";
    public bool IsApplied { get; private set; }

    /// <summary>
    /// Stash an initial theme loaded before JS interop is available. Call
    /// <see cref="ApplyPendingAsync"/> from a component's OnAfterRenderAsync
    /// to push it to the DOM.
    /// </summary>
    public void SetInitial(string theme)
    {
        Current = Normalize(theme);
    }

    public async Task ApplyPendingAsync()
    {
        if (IsApplied) return;
        await InvokeApplyAsync(Current);
        IsApplied = true;
    }

    public async Task ApplyAsync(string theme)
    {
        Current = Normalize(theme);
        await InvokeApplyAsync(Current);
        IsApplied = true;
    }

    private async Task InvokeApplyAsync(string theme)
    {
        await js.InvokeVoidAsync("mindAtlasTheme.apply", theme);
    }

    private static string Normalize(string? theme) => theme switch
    {
        "light" or "dark" or "auto" => theme,
        _ => "auto"
    };
}
