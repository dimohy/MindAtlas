using Microsoft.AspNetCore.Mvc;
using MindAtlas.Core.Models;
using MindAtlas.Engine.Maintenance;

namespace MindAtlas.Server.Controllers;

[ApiController]
[Route("api/wiki/relationships")]
public sealed class RelationshipController(RelationshipRetagService retagService) : ControllerBase
{
    /// <summary>
    /// POST /api/wiki/relationships/retag/proposals — create safe typed-link retag proposals.
    /// </summary>
    [HttpPost("retag/proposals")]
    public async Task<IActionResult> ProposeRetags(CancellationToken ct)
    {
        var result = await retagService.ProposeAsync(ct);
        return Ok(result);
    }

    /// <summary>
    /// POST /api/wiki/relationships/retag/apply — apply safe typed-link retags above the requested confidence threshold.
    /// </summary>
    [HttpPost("retag/apply")]
    public async Task<IActionResult> ApplyRetags([FromBody] ApplyRelationshipRetagsRequest? request, CancellationToken ct)
    {
        try
        {
            var result = await retagService.ApplyAsync(request?.MinimumConfidence ?? "high", ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/wiki/relationships/retag/apply-selected — apply selected safe typed-link retag proposals.
    /// </summary>
    [HttpPost("retag/apply-selected")]
    public async Task<IActionResult> ApplySelectedRetags([FromBody] ApplySelectedRelationshipRetagsRequest? request, CancellationToken ct)
    {
        var result = await retagService.ApplySelectedAsync(request?.Selections ?? [], ct);
        return Ok(result);
    }
}

public sealed record ApplyRelationshipRetagsRequest(string MinimumConfidence = "high");
public sealed record ApplySelectedRelationshipRetagsRequest(IReadOnlyList<RelationshipRetagSelection> Selections);
