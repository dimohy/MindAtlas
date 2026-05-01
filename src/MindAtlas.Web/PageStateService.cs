namespace MindAtlas.Web;

/// <summary>
/// Singleton in-memory store that lets pages persist their UI state across
/// navigation. Blazor WASM disposes page components on route change, so
/// anything we want to survive (chat history, scroll offset, filter values)
/// has to live outside the component.
///
/// Keys are typically the page route (e.g. "query", "search"). Values are
/// arbitrary boxed state objects owned by each page; pages cast them back
/// to their own private record types on restore.
/// </summary>
public sealed class PageStateService
{
    private readonly Dictionary<string, object?> _states = new(StringComparer.Ordinal);

    public void Save(string key, object? state) => _states[key] = state;

    public bool TryGet<T>(string key, out T? state) where T : class
    {
        if (_states.TryGetValue(key, out var obj) && obj is T typed)
        {
            state = typed;
            return true;
        }
        state = null;
        return false;
    }

    public void Clear(string key) => _states.Remove(key);
}
