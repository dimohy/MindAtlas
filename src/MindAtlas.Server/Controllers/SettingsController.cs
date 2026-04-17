using GitHub.Copilot.SDK;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MindAtlas.Core.Interfaces;
using System.Globalization;

namespace MindAtlas.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController(
    IConfiguration configuration,
    ICopilotAgentService copilotAgent) : ControllerBase
{
    private static readonly HashSet<string> SupportedLanguages =
        new(StringComparer.OrdinalIgnoreCase) { "en", "ko", "ja" };

    // Map the desktop OS culture to one of the supported UI languages.
    private static string DetectOsLanguage()
    {
        var code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return SupportedLanguages.Contains(code) ? code.ToLowerInvariant() : "en";
    }

    private static string? MaskToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        return token.Length <= 4
            ? new string('*', token.Length)
            : new string('*', Math.Min(token.Length - 4, 8)) + token[^4..];
    }

    /// <summary>
    /// GET /api/settings — current app settings.
    /// When UiLanguage/IngestLanguage are blank in appsettings.json, the server
    /// auto-detects from the desktop OS culture and returns that as the default,
    /// along with <c>IsUiLanguageAutoDetected</c>/<c>IsIngestLanguageAutoDetected</c>
    /// so the client knows it may still override (e.g. with the browser language).
    /// </summary>
    [HttpGet]
    public IActionResult Get()
    {
        var rawUi = configuration["MindAtlas:UiLanguage"];
        var rawIngest = configuration["MindAtlas:IngestLanguage"];
        var uiAuto = string.IsNullOrWhiteSpace(rawUi);
        var ingestAuto = string.IsNullOrWhiteSpace(rawIngest);
        var tokenRaw = configuration["MindAtlas:GitHubToken"];

        return Ok(new AppSettings
        {
            UiLanguage = uiAuto ? DetectOsLanguage() : rawUi!,
            IngestLanguage = ingestAuto ? DetectOsLanguage() : rawIngest!,
            DataRoot = configuration["MindAtlas:DataRoot"] ?? "./data",
            Model = configuration["MindAtlas:Model"] ?? "gpt-5-mini",
            Theme = configuration["MindAtlas:Theme"] ?? "auto",
            WebSearchDefaultEnabled = configuration.GetValue("MindAtlas:WebSearchDefaultEnabled", false),
            AutoSaveUncovered = configuration.GetValue("MindAtlas:AutoSaveUncovered", false),
            // Never return the full token to the client. Only a masked
            // preview (e.g. "********abcd") so the UI can show the user
            // whether one is configured without leaking secrets.
            GitHubToken = MaskToken(tokenRaw),
            IsGitHubTokenConfigured = !string.IsNullOrWhiteSpace(tokenRaw),
            IsUiLanguageAutoDetected = uiAuto,
            IsIngestLanguageAutoDetected = ingestAuto
        });
    }

    /// <summary>
    /// PUT /api/settings — update app settings (persisted to appsettings.json).
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] AppSettings settings, CancellationToken ct)
    {
        // Split persistence:
        //  - Non-secret preferences (language, data root, model, theme) live
        //    in the main appsettings.json that ships with the app.
        //  - The GitHub token lives in appsettings.desktop.json (preferred)
        //    so it never accidentally gets committed to source control.
        var mainPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var desktopPath = Path.Combine(AppContext.BaseDirectory, "appsettings.desktop.json");
        if (!System.IO.File.Exists(mainPath))
            return StatusCode(500, new { error = "appsettings.json not found" });

        // If the caller sent back the masked placeholder (starts with '*')
        // or an empty string (form was never touched), preserve the
        // currently-saved token instead of clobbering it.
        var incomingToken = settings.GitHubToken;
        var currentToken = configuration["MindAtlas:GitHubToken"];
        var tokenToWrite = string.IsNullOrEmpty(incomingToken) || incomingToken.StartsWith('*')
            ? currentToken
            : incomingToken;

        // --- 1) Update main appsettings.json with non-secret prefs ---
        await UpdateMindAtlasSectionAsync(mainPath, (writer, inner, written) =>
        {
            var value = inner.Name switch
            {
                "UiLanguage" => settings.UiLanguage,
                "IngestLanguage" => settings.IngestLanguage,
                "DataRoot" => settings.DataRoot,
                "Model" => settings.Model,
                "Theme" => settings.Theme,
                _ => inner.Value.ValueKind == System.Text.Json.JsonValueKind.String
                    ? inner.Value.GetString()
                    : null
            };
            // GitHubToken is never persisted here — keep existing if present.
            if (inner.Name == "GitHubToken")
            {
                inner.WriteTo(writer);
                written.Add(inner.Name);
                return;
            }
            if (inner.Name == "WebSearchDefaultEnabled")
            {
                writer.WriteBoolean(inner.Name, settings.WebSearchDefaultEnabled);
                written.Add(inner.Name);
                return;
            }
            if (inner.Name == "AutoSaveUncovered")
            {
                writer.WriteBoolean(inner.Name, settings.AutoSaveUncovered);
                written.Add(inner.Name);
                return;
            }
            if (value is not null)
            {
                writer.WriteString(inner.Name, value);
                written.Add(inner.Name);
            }
            else
            if (!written.Contains("AutoSaveUncovered"))
                writer.WriteBoolean("AutoSaveUncovered", settings.AutoSaveUncovered);
            {
                inner.WriteTo(writer);
                written.Add(inner.Name);
            }
            if (!written.Contains("WebSearchDefaultEnabled"))
                writer.WriteBoolean("WebSearchDefaultEnabled", settings.WebSearchDefaultEnabled);
        }, (writer, written) =>
        {
            if (!written.Contains("Model") && settings.Model is not null)
                writer.WriteString("Model", settings.Model);
            if (!written.Contains("Theme") && settings.Theme is not null)
                writer.WriteString("Theme", settings.Theme);
        }, ct);

        // --- 2) Update the desktop-only token file (always; created if absent) ---
        await WriteDesktopTokenAsync(desktopPath, tokenToWrite, ct);

        // Hot-reload the Copilot agent so the new token takes effect without
        // an app restart. Safe if token didn't actually change — ReloadTokenAsync
        // is a no-op in that case.
        if (!string.Equals(tokenToWrite, currentToken, StringComparison.Ordinal))
        {
            try { await copilotAgent.ReloadTokenAsync(tokenToWrite, ct); }
            catch { /* agent reload best-effort; UI already shows success */ }
        }

        return Ok(new { success = true });
    }

    private static async Task UpdateMindAtlasSectionAsync(
        string path,
        Action<System.Text.Json.Utf8JsonWriter, System.Text.Json.JsonProperty, HashSet<string>> writeKnown,
        Action<System.Text.Json.Utf8JsonWriter, HashSet<string>> appendMissing,
        CancellationToken ct)
    {
        var json = await System.IO.File.ReadAllTextAsync(path, ct);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        using var ms = new MemoryStream();
        await using (var writer = new System.Text.Json.Utf8JsonWriter(
            ms, new System.Text.Json.JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name == "MindAtlas")
                {
                    writer.WriteStartObject("MindAtlas");
                    var written = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var inner in prop.Value.EnumerateObject())
                        writeKnown(writer, inner, written);
                    appendMissing(writer, written);
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
        var tempPath = path + ".tmp";
        await System.IO.File.WriteAllTextAsync(tempPath, updated, ct);
        System.IO.File.Move(tempPath, path, overwrite: true);
    }

    private static async Task WriteDesktopTokenAsync(string path, string? token, CancellationToken ct)
    {
        // Keep only { "MindAtlas": { "GitHubToken": "..." } } — no other keys.
        var payload = new
        {
            MindAtlas = new Dictionary<string, string?>
            {
                ["GitHubToken"] = token
            }
        };
        var json = System.Text.Json.JsonSerializer.Serialize(
            payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        var tempPath = path + ".tmp";
        await System.IO.File.WriteAllTextAsync(tempPath, json, ct);
        System.IO.File.Move(tempPath, path, overwrite: true);
    }

    /// <summary>
    /// POST /api/settings/test-copilot-token — spin up a throwaway CopilotClient
    /// with the supplied token, send a minimal prompt, and confirm a response
    /// comes back. This verifies the token is not only authenticated but also
    /// has access to the Copilot Chat/GPT-5 mini model that MindAtlas uses.
    /// </summary>
    [HttpPost("test-copilot-token")]
    public async Task<IActionResult> TestCopilotToken([FromBody] TestTokenRequest req, CancellationToken ct)
    {
        var token = string.IsNullOrWhiteSpace(req.Token) || req.Token.StartsWith('*')
            ? configuration["MindAtlas:GitHubToken"]
            : req.Token;

        if (string.IsNullOrWhiteSpace(token))
            return Ok(new TestTokenResult(false, "Token is empty."));

        // 10s cap on the whole round-trip so the UI doesn't hang forever.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(TimeSpan.FromSeconds(20));

        CopilotClient? client = null;
        CopilotSession? session = null;
        try
        {
            client = new CopilotClient(new CopilotClientOptions { GitHubToken = token });
            await client.StartAsync(linked.Token);

            session = await client.CreateSessionAsync(new SessionConfig
            {
                Model = "gpt-5-mini",
                Streaming = false,
                OnPermissionRequest = PermissionHandler.ApproveAll
            }, linked.Token);

            var result = await session.SendAndWaitAsync(
                new MessageOptions { Prompt = "Reply with the single word: OK" },
                timeout: TimeSpan.FromSeconds(15),
                cancellationToken: linked.Token);

            var content = result?.Data?.Content?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(content))
                return Ok(new TestTokenResult(false, "Empty response from Copilot."));

            var preview = content.Length > 80 ? content[..80] + "..." : content;
            return Ok(new TestTokenResult(true, $"gpt-5-mini → \"{preview}\""));
        }
        catch (OperationCanceledException)
        {
            return Ok(new TestTokenResult(false, "Timed out waiting for Copilot response."));
        }
        catch (Exception ex)
        {
            return Ok(new TestTokenResult(false, ex.Message));
        }
        finally
        {
            if (session is not null) await session.DisposeAsync();
            if (client is not null) await client.DisposeAsync();
        }
    }
}

public sealed record TestTokenRequest(string? Token);
public sealed record TestTokenResult(bool Success, string Message);

public sealed record AppSettings
{
    public string UiLanguage { get; init; } = "en";
    public string IngestLanguage { get; init; } = "en";
    public string DataRoot { get; init; } = "./data";
    public string Model { get; init; } = "gpt-5-mini";
    public string Theme { get; init; } = "auto";

    /// <summary>
    /// Default state of the "Use web search" toggle for new AI query
    /// sessions. When true, the Copilot CLI's built-in url fetch tool is
    /// approved by the session permission handler; when false, it is
    /// denied by rules. Persisted in appsettings.json under
    /// <c>MindAtlas:WebSearchDefaultEnabled</c>.

    /// <summary>
    /// When true, if the post-query coverage check decides the answer is
    /// poorly covered by the existing wiki, the server saves it
    /// automatically (using the LLM-suggested title) and emits
    /// <c>event: wiki-saved</c> on the SSE stream instead of
    /// <c>event: wiki-suggestion</c>. Persisted in appsettings.json under
    /// <c>MindAtlas:AutoSaveUncovered</c>.
    /// </summary>
    public bool AutoSaveUncovered { get; init; }
    /// </summary>
    public bool WebSearchDefaultEnabled { get; init; }

    /// <summary>
    /// Only ever returned to the client as a masked preview (e.g. "****abcd").
    /// When the client sends this back unchanged (still starts with '*'), the
    /// server treats it as "keep the existing value" instead of overwriting.
    /// </summary>
    public string? GitHubToken { get; init; }

    public bool IsGitHubTokenConfigured { get; init; }

    // True when the value above was auto-detected (appsettings had no
    // user-set value). Clients may override it (e.g. Web overrides UiLanguage
    // with navigator.language after first render).
    public bool IsUiLanguageAutoDetected { get; init; }
    public bool IsIngestLanguageAutoDetected { get; init; }
}
