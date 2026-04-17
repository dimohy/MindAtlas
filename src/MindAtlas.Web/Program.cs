using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MindAtlas.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBase = builder.Configuration.GetValue<string>("ApiBase") ?? builder.HostEnvironment.BaseAddress;

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBase) });
builder.Services.AddSingleton<ToastService>();
builder.Services.AddSingleton<PageStateService>();
// QueryStreamingService must outlive page navigation, so keep a singleton
// HttpClient dedicated to it (WASM = single connection pool anyway).
builder.Services.AddSingleton(sp => new QueryStreamingService(new HttpClient { BaseAddress = new Uri(apiBase) }));
builder.Services.AddScoped<LocalizationService>();
builder.Services.AddScoped<ThemeService>();

var host = builder.Build();
var http = host.Services.GetRequiredService<HttpClient>();
var l10n = host.Services.GetRequiredService<LocalizationService>();
var theme = host.Services.GetRequiredService<ThemeService>();

// Load saved UI language and theme from server settings.
// NOTE: do NOT invoke IJSRuntime here — JS interop is only available after
// host.RunAsync(). We stash the theme on the service so MainLayout applies it
// on first render.
var lang = "en";
var themeName = "auto";
var isUiLanguageAutoDetected = false;
try
{
    var settings = await http.GetFromJsonAsync<JsonElement>("api/settings");
    if (settings.TryGetProperty("uiLanguage", out var langProp))
        lang = langProp.GetString() ?? "en";
    if (settings.TryGetProperty("theme", out var themeProp))
        themeName = themeProp.GetString() ?? "auto";
    if (settings.TryGetProperty("isUiLanguageAutoDetected", out var autoProp)
        && autoProp.ValueKind == JsonValueKind.True)
        isUiLanguageAutoDetected = true;
}
catch
{
    // Server may be unreachable on first boot — fall through to defaults.
}

await l10n.SetLanguageAsync(lang);
// Remember whether the initial language was auto-detected; MainLayout uses
// this to optionally override with navigator.language on first render.
l10n.IsAutoDetected = isUiLanguageAutoDetected;
theme.SetInitial(themeName);
await host.RunAsync();
