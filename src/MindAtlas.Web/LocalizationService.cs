using System.Net.Http.Json;

namespace MindAtlas.Web;

// Provides runtime UI string localization from JSON resource files
public sealed class LocalizationService(HttpClient http)
{
    private Dictionary<string, string> _strings = new();

    public string CurrentLanguage { get; private set; } = "en";
    public event Action? OnLanguageChanged;

    // True when the initial language was auto-detected (server had no
    // user-set value). MainLayout reads this to decide whether to override
    // with the browser's preferred language on first render.
    public bool IsAutoDetected { get; set; }

    public string this[string key] =>
        _strings.GetValueOrDefault(key, key);

    public string Format(string key, params object?[] args) =>
        string.Format(this[key], args);

    public async Task SetLanguageAsync(string language)
    {
        if (CurrentLanguage == language && _strings.Count > 0) return;
        CurrentLanguage = language;
        // Cache-bust by appending the assembly version so WebView2 doesn't
        // serve a stale copy of the locale JSON after the app was upgraded.
        var version = System.Reflection.Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString() ?? "0";
        _strings = await http.GetFromJsonAsync<Dictionary<string, string>>(
                $"locales/{language}.json?v={version}")
            ?? throw new InvalidOperationException($"Locale file 'locales/{language}.json' returned null");
        OnLanguageChanged?.Invoke();
    }
}
