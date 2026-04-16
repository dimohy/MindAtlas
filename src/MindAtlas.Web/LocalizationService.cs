using System.Net.Http.Json;

namespace MindAtlas.Web;

// Provides runtime UI string localization from JSON resource files
public sealed class LocalizationService(HttpClient http)
{
    private Dictionary<string, string> _strings = new();

    public string CurrentLanguage { get; private set; } = "en";
    public event Action? OnLanguageChanged;

    public string this[string key] =>
        _strings.GetValueOrDefault(key, key);

    public string Format(string key, params object?[] args) =>
        string.Format(this[key], args);

    public async Task SetLanguageAsync(string language)
    {
        if (CurrentLanguage == language && _strings.Count > 0) return;
        CurrentLanguage = language;
        _strings = await http.GetFromJsonAsync<Dictionary<string, string>>($"locales/{language}.json")
            ?? throw new InvalidOperationException($"Locale file 'locales/{language}.json' returned null");
        OnLanguageChanged?.Invoke();
    }
}
