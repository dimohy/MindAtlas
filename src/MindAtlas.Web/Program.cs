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
builder.Services.AddScoped<LocalizationService>();

var host = builder.Build();
var http = host.Services.GetRequiredService<HttpClient>();
var l10n = host.Services.GetRequiredService<LocalizationService>();

// Load saved UI language from server settings
var lang = "en";
var settings = await http.GetFromJsonAsync<JsonElement>("api/settings");
if (settings.TryGetProperty("uiLanguage", out var langProp))
    lang = langProp.GetString() ?? "en";

await l10n.SetLanguageAsync(lang);
await host.RunAsync();
