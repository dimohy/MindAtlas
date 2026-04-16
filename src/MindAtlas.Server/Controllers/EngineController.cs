using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MindAtlas.Core.Interfaces;
using MindAtlas.Server.Hubs;

namespace MindAtlas.Server.Controllers;

[ApiController]
[Route("api")]
public class EngineController(
    IWikiEngine wikiEngine,
    IRawRepository rawRepo,
    IHubContext<WikiHub> hubContext) : ControllerBase
{
    /// <summary>
    /// POST /api/ingest — upload text/file to raw/ and trigger ingest.
    /// </summary>
    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest([FromBody] IngestRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { error = "Content is required" });

        // Save to raw/
        var fileName = !string.IsNullOrWhiteSpace(request.Title)
            ? $"{SanitizeFileName(request.Title)}.md"
            : $"ingest_{DateTime.UtcNow:yyyyMMdd_HHmmss}.md";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(request.Content));
        await rawRepo.SaveAsync(fileName, stream, ct);

        // Get the raw file path for ingest
        var rawPath = GetRawFilePath(fileName);

        // Notify clients
        await hubContext.Clients.All.SendAsync("OnIngestStarted", fileName, ct);

        // Run ingest
        var pages = await wikiEngine.IngestAsync(rawPath, ct);

        // Notify clients
        await hubContext.Clients.All.SendAsync("OnIngestCompleted", fileName, pages, ct);
        foreach (var page in pages)
        {
            await hubContext.Clients.All.SendAsync("OnWikiUpdated", page, ct);
        }

        return Ok(new { fileName, pages });
    }

    /// <summary>
    /// POST /api/query — natural language query with SSE streaming response.
    /// </summary>
    [HttpPost("query")]
    public async Task Query([FromBody] QueryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            Response.StatusCode = 400;
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        await foreach (var chunk in wikiEngine.QueryStreamingAsync(request.Question, ct))
        {
            await Response.WriteAsync($"data: {chunk}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }

        await Response.WriteAsync("data: [DONE]\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    /// <summary>
    /// POST /api/query/sync — non-streaming query returning full result.
    /// </summary>
    [HttpPost("query/sync")]
    public async Task<IActionResult> QuerySync([FromBody] QueryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(new { error = "Question is required" });

        var result = await wikiEngine.QueryAsync(request.Question, ct);
        return Ok(result);
    }

    /// <summary>
    /// POST /api/lint — run wiki lint check.
    /// </summary>
    [HttpPost("lint")]
    public async Task<IActionResult> Lint(CancellationToken ct)
    {
        var result = await wikiEngine.LintAsync(ct);
        return Ok(result);
    }

    /// <summary>
    /// POST /api/lint/fix — auto-fix lint issues (missing index, stale entries).
    /// </summary>
    [HttpPost("lint/fix")]
    public async Task<IActionResult> LintFix(CancellationToken ct)
    {
        var fixCount = await wikiEngine.LintFixAsync(ct);
        return Ok(new { fixedCount = fixCount });
    }

    /// <summary>
    /// GET /api/ingest/status — current ingest queue status.
    /// </summary>
    [HttpGet("ingest/status")]
    public async Task<IActionResult> IngestStatus(CancellationToken ct)
    {
        var all = await rawRepo.GetAllAsync(ct);
        var unprocessed = await rawRepo.GetUnprocessedAsync(ct);

        return Ok(new
        {
            total = all.Count,
            pending = unprocessed.Count,
            done = all.Count(r => r.Status == Core.Models.ProcessingStatus.Done),
            failed = all.Count(r => r.Status == Core.Models.ProcessingStatus.Failed),
            processing = all.Count(r => r.Status == Core.Models.ProcessingStatus.Processing),
            items = all.Select(r => new { r.FileName, Status = r.Status.ToString(), r.AddedAt })
        });
    }

    private string GetRawFilePath(string fileName)
    {
        // Resolve raw directory from the data root configuration
        var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var dataRoot = config.GetValue<string>("MindAtlas:DataRoot")
            ?? Path.Combine(AppContext.BaseDirectory, "data");
        return Path.Combine(dataRoot, "raw", fileName);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            sb.Append(invalid.Contains(c) ? '_' : c);
        }
        return sb.ToString();
    }
}

public sealed record IngestRequest(string Content, string? Title = null);
public sealed record QueryRequest(string Question);
