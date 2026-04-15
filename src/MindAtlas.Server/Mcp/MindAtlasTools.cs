using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;
using MindAtlas.Core.Interfaces;
using MindAtlas.Core.Models;

namespace MindAtlas.Server.Mcp;

/// <summary>
/// MCP tool definitions for MindAtlas — 5 tools exposed to VS Code / Copilot Chat.
/// </summary>
[McpServerToolType]
public class MindAtlasTools(
    IWikiEngine wikiEngine,
    IIndexService indexService,
    IWikiRepository wikiRepo,
    IRawRepository rawRepo)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [McpServerTool(Name = "mindatlas_ingest")]
    [Description("Saves text as MindAtlas knowledge. The text is analyzed by AI and converted into wiki pages.")]
    public async Task<string> Ingest(
        [Description("The text content to ingest")] string content,
        [Description("Optional title for the raw file")] string? title = null)
    {
        var fileName = !string.IsNullOrWhiteSpace(title)
            ? $"{SanitizeFileName(title)}.md"
            : $"ingest_{DateTime.UtcNow:yyyyMMdd_HHmmss}.md";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        await rawRepo.SaveAsync(fileName, stream);

        var rawPath = GetRawFilePath(fileName);
        var pages = await wikiEngine.IngestAsync(rawPath);

        return $"Ingested successfully. Created {pages.Count} page(s): {string.Join(", ", pages)}";
    }

    [McpServerTool(Name = "mindatlas_search")]
    [Description("Searches the MindAtlas wiki by keyword. Returns matching page summaries.")]
    public async Task<string> Search(
        [Description("The keyword to search for")] string keyword,
        [Description("Maximum number of results")] int limit = 10)
    {
        var results = await indexService.SearchAsync(keyword);
        var limited = results.Take(limit).ToList();

        if (limited.Count == 0)
            return $"No results found for '{keyword}'.";

        return JsonSerializer.Serialize(limited, JsonOptions);
    }

    [McpServerTool(Name = "mindatlas_query")]
    [Description("Asks a natural language question to the MindAtlas wiki. AI analyzes wiki content and returns an answer.")]
    public async Task<string> Query(
        [Description("The question to ask")] string question)
    {
        var result = await wikiEngine.QueryAsync(question);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool(Name = "mindatlas_get_asset")]
    [Description("Retrieves vibe coding assets (agents, rules, prompts, snippets, templates) from the wiki.")]
    public async Task<string> GetAsset(
        [Description("Asset type filter: agent, rule, prompt, snippet, template")] string? assetType = null,
        [Description("Search query")] string query = "")
    {
        var pages = await wikiRepo.GetAllAsync();
        var assets = new List<VibeCodingAsset>();

        foreach (var page in pages)
        {
            var type = InferAssetType(page.Tags);
            if (type is null) continue;

            if (assetType is not null
                && !Enum.TryParse<AssetType>(assetType, ignoreCase: true, out var filter))
                continue;
            else if (assetType is not null
                && Enum.TryParse<AssetType>(assetType, ignoreCase: true, out var ft)
                && ft != type)
                continue;

            assets.Add(new VibeCodingAsset
            {
                AssetType = type.Value,
                Name = page.Title,
                Content = page.Content,
                Tags = page.Tags
            });
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            assets = assets.Where(a =>
                a.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                a.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (assets.Count == 0)
            return "No matching assets found.";

        return JsonSerializer.Serialize(assets, JsonOptions);
    }

    [McpServerTool(Name = "mindatlas_lint")]
    [Description("Runs a wiki health check — detects orphan pages, broken links, missing index entries.")]
    public async Task<string> Lint(
        [Description("Lint scope: 'full' (default)")] string scope = "full")
    {
        var result = await wikiEngine.LintAsync();
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    // --- Helpers ---

    private static string GetRawFilePath(string fileName)
    {
        var dataRoot = Environment.GetEnvironmentVariable("MINDATLAS_DATA_ROOT")
            ?? Path.Combine(AppContext.BaseDirectory, "data");
        return Path.Combine(dataRoot, "raw", fileName);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(invalid.Contains(c) ? '_' : c);
        return sb.ToString();
    }

    private static AssetType? InferAssetType(List<string> tags)
    {
        foreach (var tag in tags)
        {
            var lower = tag.TrimStart('#').ToLowerInvariant();
            return lower switch
            {
                "agent" => AssetType.Agent,
                "rule" => AssetType.Rule,
                "prompt" => AssetType.Prompt,
                "snippet" => AssetType.Snippet,
                "template" => AssetType.Template,
                "asset" => AssetType.Prompt,
                _ => null
            };
        }
        return null;
    }
}
