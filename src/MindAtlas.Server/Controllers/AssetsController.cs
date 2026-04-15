using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using MindAtlas.Core.Interfaces;
using MindAtlas.Core.Models;

namespace MindAtlas.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public partial class AssetsController(IWikiRepository wikiRepo) : ControllerBase
{
    /// <summary>
    /// GET /api/assets?type={agent|rule|prompt|snippet|template} — list assets by type.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? type, CancellationToken ct)
    {
        var assets = await LoadAssetsAsync(ct);

        if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<AssetType>(type, ignoreCase: true, out var assetType))
        {
            assets = assets.Where(a => a.AssetType == assetType).ToList();
        }

        return Ok(assets);
    }

    /// <summary>
    /// GET /api/assets/{name} — get a specific asset by name.
    /// </summary>
    [HttpGet("{name}")]
    public async Task<IActionResult> GetByName(string name, CancellationToken ct)
    {
        var assets = await LoadAssetsAsync(ct);
        var asset = assets.FirstOrDefault(a =>
            a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (asset is null)
            return NotFound(new { error = $"Asset '{name}' not found" });

        return Ok(asset);
    }

    /// <summary>
    /// GET /api/assets/search?q={query} — search assets.
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Query parameter 'q' is required" });

        var assets = await LoadAssetsAsync(ct);
        var lower = q.ToLowerInvariant();

        var results = assets.Where(a =>
            a.Name.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
            a.Content.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
            a.Tags.Any(t => t.Contains(lower, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return Ok(results);
    }

    /// <summary>
    /// Load vibe coding assets from wiki pages tagged with #asset or #agent, #rule, etc.
    /// </summary>
    private async Task<List<VibeCodingAsset>> LoadAssetsAsync(CancellationToken ct)
    {
        var pages = await wikiRepo.GetAllAsync(ct);
        var assets = new List<VibeCodingAsset>();

        foreach (var page in pages)
        {
            var assetType = InferAssetType(page.Tags);
            if (assetType is null) continue;

            assets.Add(new VibeCodingAsset
            {
                AssetType = assetType.Value,
                Name = page.Title,
                Content = page.Content,
                Tags = page.Tags
            });
        }

        return assets;
    }

    private static AssetType? InferAssetType(List<string> tags)
    {
        foreach (var tag in tags)
        {
            var lower = tag.TrimStart('#').ToLowerInvariant();
            if (lower is "agent") return AssetType.Agent;
            if (lower is "rule") return AssetType.Rule;
            if (lower is "prompt") return AssetType.Prompt;
            if (lower is "snippet") return AssetType.Snippet;
            if (lower is "template") return AssetType.Template;
            if (lower is "asset") return AssetType.Prompt; // default asset type
        }
        return null;
    }
}
