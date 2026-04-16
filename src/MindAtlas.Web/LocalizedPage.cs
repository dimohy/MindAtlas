using Microsoft.AspNetCore.Components;

namespace MindAtlas.Web;

// Base class for pages that need localization support.
// Subscribes to language changes and triggers re-render automatically.
public abstract class LocalizedPage : ComponentBase, IAsyncDisposable
{
    [Inject] protected LocalizationService L { get; set; } = default!;

    protected override void OnInitialized()
    {
        L.OnLanguageChanged += HandleLanguageChanged;
    }

    private void HandleLanguageChanged() => InvokeAsync(StateHasChanged);

    public virtual ValueTask DisposeAsync()
    {
        L.OnLanguageChanged -= HandleLanguageChanged;
        return ValueTask.CompletedTask;
    }
}
