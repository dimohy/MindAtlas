using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace MindAtlas.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController(IConfiguration configuration) : ControllerBase
{
    /// <summary>
    /// GET /api/settings — current app settings.
    /// </summary>
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new AppSettings
        {
            UiLanguage = configuration["MindAtlas:UiLanguage"] ?? "en",
            IngestLanguage = configuration["MindAtlas:IngestLanguage"] ?? "en",
            DataRoot = configuration["MindAtlas:DataRoot"] ?? "./data",
            Model = configuration["MindAtlas:Model"] ?? "gpt-5-mini"
        });
    }

    /// <summary>
    /// PUT /api/settings — update app settings (persisted to appsettings.json).
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] AppSettings settings, CancellationToken ct)
    {
        var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!System.IO.File.Exists(appSettingsPath))
            return StatusCode(500, new { error = "appsettings.json not found" });

        var json = await System.IO.File.ReadAllTextAsync(appSettingsPath, ct);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        using var ms = new MemoryStream();
        await using (var writer = new System.Text.Json.Utf8JsonWriter(ms, new System.Text.Json.JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name == "MindAtlas")
                {
                    writer.WriteStartObject("MindAtlas");
                    var written = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var inner in prop.Value.EnumerateObject())
                    {
                        var value = inner.Name switch
                        {
                            "UiLanguage" => settings.UiLanguage,
                            "IngestLanguage" => settings.IngestLanguage,
                            "DataRoot" => settings.DataRoot,
                            "Model" => settings.Model,
                            _ => inner.Value.ValueKind == System.Text.Json.JsonValueKind.String
                                ? inner.Value.GetString()
                                : null
                        };
                        if (value is not null)
                        {
                            writer.WriteString(inner.Name, value);
                            written.Add(inner.Name);
                        }
                        else
                        {
                            inner.WriteTo(writer);
                            written.Add(inner.Name);
                        }
                    }
                    // Append any new keys that weren't already present
                    if (!written.Contains("Model") && settings.Model is not null)
                        writer.WriteString("Model", settings.Model);
                    writer.WriteEndObject();
                }
                else
                {
                    prop.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
        }

        var updated = System.Text.Encoding.UTF8.GetString(ms.ToArray());

        // Atomic write: write to a sibling temp file, then replace. This prevents
        // readers (FileSystemWatcher consumers, IConfiguration reloadOnChange)
        // from observing a torn/empty file mid-write and crashing.
        var tempPath = appSettingsPath + ".tmp";
        await System.IO.File.WriteAllTextAsync(tempPath, updated, ct);
        System.IO.File.Move(tempPath, appSettingsPath, overwrite: true);

        return Ok(new { success = true });
    }
}

public sealed record AppSettings
{
    public string UiLanguage { get; init; } = "en";
    public string IngestLanguage { get; init; } = "en";
    public string DataRoot { get; init; } = "./data";
    public string Model { get; init; } = "gpt-5-mini";
}
