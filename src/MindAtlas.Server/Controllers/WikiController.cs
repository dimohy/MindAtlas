using Microsoft.AspNetCore.Mvc;
using MindAtlas.Core.Interfaces;
using MindAtlas.Core.Models;

namespace MindAtlas.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WikiController(IWikiRepository wikiRepo, IIndexService indexService) : ControllerBase
{
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
}
