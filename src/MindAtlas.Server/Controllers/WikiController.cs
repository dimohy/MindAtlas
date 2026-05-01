using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using MindAtlas.Core.Interfaces;
using MindAtlas.Core.Models;

namespace MindAtlas.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WikiController(IWikiRepository wikiRepo, IIndexService indexService) : ControllerBase
{
    private static readonly HashSet<string> RelationshipTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "supports", "contradicts", "supersedes", "references", "causes", "depends_on",
        "prerequisite_for", "parent_of", "child_of", "part_of", "example_of", "instance_of",
        "similar_to", "related_to", "blocks", "blocked_by", "enables", "fixes", "explains",
        "challenges", "evidence_for", "evidence_against", "derived_from", "evolution_of"
    };

    /// <summary>
    /// GET /api/wiki — all wiki pages (index-based summary list).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var entries = await indexService.GetAllAsync(ct);
        return Ok(entries);
    }

    /// <summary>
    /// GET /api/wiki/{pageName} — single wiki page with full content.
    /// </summary>
    [HttpGet("{pageName}")]
    public async Task<IActionResult> GetByName(string pageName, CancellationToken ct)
    {
        var page = await wikiRepo.GetByNameAsync(pageName, ct);
        if (page is null)
            return NotFound(new { error = $"Page '{pageName}' not found" });
        return Ok(page);
    }

    /// <summary>
    /// GET /api/wiki/search?q={keyword} — keyword search.
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Query parameter 'q' is required" });

        var results = await indexService.SearchAsync(q, ct);
        return Ok(results);
    }

    /// <summary>
    /// GET /api/wiki/log — activity log (most recent first).
    /// </summary>
    [HttpGet("log")]
    public async Task<IActionResult> GetLog([FromQuery] int? limit, CancellationToken ct)
    {
        var log = await wikiRepo.GetLogAsync(limit, ct);
        return Ok(log);
    }

    /// <summary>
    /// GET /api/wiki/tags — unique tag list across all indexed pages.
    /// </summary>
    [HttpGet("tags")]
    public async Task<IActionResult> GetTags(CancellationToken ct)
    {
        var entries = await indexService.GetAllAsync(ct);
        var tags = entries
            .SelectMany(e => e.Tags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t)
            .ToList();
        return Ok(tags);
    }

    /// <summary>
    /// GET /api/wiki/graph — node-edge data for knowledge graph visualization.
    /// </summary>
    [HttpGet("graph")]
    public async Task<IActionResult> GetGraph(CancellationToken ct)
    {
        var pages = await wikiRepo.GetAllAsync(ct);
        var pageNames = new HashSet<string>(pages.Select(p => p.Title), StringComparer.OrdinalIgnoreCase);

        var nodes = pages.Select(p => new { id = p.Title, group = p.Tags.FirstOrDefault() ?? "default" }).ToList();
        var links = pages
            .SelectMany(p => ExtractGraphLinks(p, pageNames))
            .GroupBy(l => new
            {
                Source = l.Source.ToUpperInvariant(),
                Target = l.Target.ToUpperInvariant(),
                Type = l.Type.ToUpperInvariant()
            })
            .Select(g => g.First())
            .Select(l => new { source = l.Source, target = l.Target, type = l.Type })
            .ToList();

        return Ok(new { nodes, links });
    }

    private static IEnumerable<GraphLink> ExtractGraphLinks(WikiPage page, HashSet<string> pageNames)
    {
        var foundTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in WikiLinkRegex.Matches(page.Content))
        {
            var rawLink = match.Groups[1].Value;
            var target = NormalizeWikiLinkTarget(rawLink);
            if (target.Length is 0 || !TryResolvePageTitle(target, pageNames, out var resolvedTitle))
                continue;

            foundTargets.Add(resolvedTitle);
            yield return new GraphLink(page.Title, resolvedTitle, ExtractRelationshipType(rawLink));
        }

        foreach (var wikiLink in page.WikiLinks)
        {
            var target = NormalizeWikiLinkTarget(wikiLink);
            if (target.Length is 0 || !TryResolvePageTitle(target, pageNames, out var resolvedTitle) || foundTargets.Contains(resolvedTitle))
                continue;

            yield return new GraphLink(page.Title, resolvedTitle, "related");
        }
    }

    private static bool TryResolvePageTitle(string target, HashSet<string> pageNames, out string resolvedTitle)
    {
        if (pageNames.TryGetValue(target, out resolvedTitle!))
            return true;

        var slashIndex = target.LastIndexOfAny(['/', '\\']);
        if (slashIndex >= 0 && slashIndex < target.Length - 1)
        {
            var leaf = target[(slashIndex + 1)..];
            if (pageNames.TryGetValue(leaf, out resolvedTitle!))
                return true;
        }

        resolvedTitle = string.Empty;
        return false;
    }

    private static string NormalizeWikiLinkTarget(string rawLink)
    {
        var target = rawLink.Split('|', 2)[0].Trim();
        if (target.StartsWith('#'))
            return string.Empty;

        var headingIndex = target.IndexOf('#', StringComparison.Ordinal);
        if (headingIndex >= 0)
            target = target[..headingIndex].Trim();

        var slashIndex = target.LastIndexOfAny(['/', '\\']);
        if (slashIndex >= 0 && slashIndex < target.Length - 1)
            target = target[(slashIndex + 1)..].Trim();

        return RelationshipMarkerRegex.Replace(target, string.Empty).Trim();
    }

    private static string ExtractRelationshipType(string rawLink)
    {
        foreach (Match match in RelationshipTypeRegex.Matches(rawLink))
        {
            var type = match.Groups[1].Value;
            if (RelationshipTypes.Contains(type))
                return type.ToLowerInvariant();
        }

        return "related";
    }

    private sealed record GraphLink(string Source, string Target, string Type);

    private static readonly Regex WikiLinkRegex = new(@"\[\[([^\]]+)\]\]", RegexOptions.Compiled);
    private static readonly Regex RelationshipMarkerRegex = new(@"\s@[-\w]+", RegexOptions.Compiled);
    private static readonly Regex RelationshipTypeRegex = new(@"(?:^|\s)@([A-Za-z][\w-]*)", RegexOptions.Compiled);
}
