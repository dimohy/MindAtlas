using System.Text;
using System.Text.RegularExpressions;
using MindAtlas.Core.Interfaces;
using MindAtlas.Core.Models;
using MindAtlas.Engine.Repository;
using Microsoft.Extensions.Logging;

namespace MindAtlas.Engine.Ingest;

/// <summary>
/// Processes raw source files → wiki pages via Copilot Agent.
/// Manages the full ingest pipeline: read → analyze → create/update → index → log.
/// </summary>
public sealed partial class IngestPipeline
{
    private readonly IRawRepository _rawRepo;
    private readonly WikiRepository _wikiRepo;
    private readonly ICopilotAgentService _agent;
    private readonly ILogger<IngestPipeline>? _logger;

    public IngestPipeline(
        IRawRepository rawRepo,
        WikiRepository wikiRepo,
        ICopilotAgentService agent,
        ILogger<IngestPipeline>? logger = null)
    {
        _rawRepo = rawRepo;
        _wikiRepo = wikiRepo;
        _agent = agent;
        _logger = logger;
    }

    /// <summary>
    /// Ingest a raw file: send to agent, parse response, create/update wiki pages.
    /// Returns the list of created/updated page names.
    /// </summary>
    public async Task<IReadOnlyList<string>> IngestAsync(string rawFilePath, CancellationToken ct = default)
    {
        var fileName = Path.GetFileName(rawFilePath);
        _logger?.LogInformation("Ingesting raw file: {FileName}", fileName);

        await _rawRepo.UpdateStatusAsync(fileName, ProcessingStatus.Processing, ct);

        try
        {
            // Read raw content
            var rawContent = await ReadRawContentAsync(rawFilePath, ct);

            // Gather existing wiki context for agent
            var existingPages = await _wikiRepo.GetAllAsync(ct);
            var context = BuildIngestContext(existingPages);

            // Send to agent
            var prompt = BuildIngestPrompt(fileName, rawContent, context);
            var response = await _agent.SendAsync(prompt, ct);

            // Parse response into wiki pages
            var pages = ParseAgentResponse(response);

            if (pages.Count == 0)
            {
                _logger?.LogWarning("Agent returned no pages for {FileName}", fileName);
                await _rawRepo.UpdateStatusAsync(fileName, ProcessingStatus.Failed, ct);
                return [];
            }

            // Save wiki pages and update index
            var createdPages = new List<string>();
            foreach (var page in pages)
            {
                await _wikiRepo.SaveAsync(page, ct);
                await _wikiRepo.UpdateIndexAsync(new IndexEntry
                {
                    PageName = page.Title,
                    Summary = page.Summary,
                    Tags = page.Tags,
                    Keywords = ExtractKeywords(page.Title, page.Summary)
                }, ct);
                createdPages.Add(page.Title);
            }

            // Log the ingest operation
            await _wikiRepo.AppendLogAsync(new LogEntry
            {
                Operation = OperationType.Ingest,
                Description = $"Ingested {fileName} → {createdPages.Count} page(s)",
                AffectedPages = createdPages
            }, ct);

            await _rawRepo.UpdateStatusAsync(fileName, ProcessingStatus.Done, ct);
            _logger?.LogInformation("Ingest complete: {FileName} → {Pages}", fileName, string.Join(", ", createdPages));

            return createdPages;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ingest failed for {FileName}", fileName);
            await _rawRepo.UpdateStatusAsync(fileName, ProcessingStatus.Failed, ct);
            throw;
        }
    }

    // --- Private helpers ---

    private static async Task<string> ReadRawContentAsync(string filePath, CancellationToken ct)
    {
        var contentType = InferContentType(Path.GetExtension(filePath));

        if (contentType.StartsWith("text/") || contentType == "application/json")
        {
            return await File.ReadAllTextAsync(filePath, Encoding.UTF8, ct);
        }

        // For binary files, return metadata only
        var fileInfo = new FileInfo(filePath);
        return $"[Binary file: {fileInfo.Name}, Size: {fileInfo.Length} bytes, Type: {contentType}]";
    }

    private static string BuildIngestContext(IReadOnlyList<WikiPage> existingPages)
    {
        if (existingPages.Count == 0)
            return "No existing wiki pages yet.";

        var sb = new StringBuilder();
        sb.AppendLine("Existing wiki pages:");
        foreach (var page in existingPages.Take(50))
        {
            sb.AppendLine($"- {page.Title}: {page.Summary}");
        }
        return sb.ToString();
    }

    private static string BuildIngestPrompt(string fileName, string rawContent, string context)
    {
        return $"""
            Process this raw source into wiki knowledge.
            
            ## Source File: {fileName}
            
            ```
            {rawContent}
            ```
            
            ## Existing Wiki Context
            {context}
            
            ## Instructions
            Create or update wiki pages based on this content. For each page, use this exact format:
            
            ---PAGE_START---
            # Title
            
            > One-line summary
            
            Tags: #tag1, #tag2
            
            ## Content
            
            (main content with [[wikilinks]] to related topics)
            
            ## Related
            
            - [[Related Page]]
            ---PAGE_END---
            
            Create as many pages as needed. Link to existing pages when relevant.
            """;
    }

    internal static IReadOnlyList<WikiPage> ParseAgentResponse(string response)
    {
        var pages = new List<WikiPage>();
        var pageBlocks = response.Split("---PAGE_START---", StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in pageBlocks)
        {
            var content = block.Split("---PAGE_END---")[0].Trim();
            if (string.IsNullOrWhiteSpace(content)) continue;

            var page = ParsePageBlock(content);
            if (page is not null)
                pages.Add(page);
        }

        // Fallback: if agent didn't use markers, treat entire response as one page
        if (pages.Count == 0 && !string.IsNullOrWhiteSpace(response))
        {
            var fallback = ParsePageBlock(response.Trim());
            if (fallback is not null)
                pages.Add(fallback);
        }

        return pages;
    }

    private static WikiPage? ParsePageBlock(string content)
    {
        var lines = content.Split('\n');
        string? title = null;
        string summary = string.Empty;
        var tags = new List<string>();
        var wikiLinks = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("# ") && title is null)
            {
                title = trimmed[2..].Trim();
                continue;
            }

            if (trimmed.StartsWith("> ") && string.IsNullOrEmpty(summary))
            {
                summary = trimmed[2..].Trim();
                continue;
            }

            if (trimmed.StartsWith("Tags:", StringComparison.OrdinalIgnoreCase))
            {
                tags = TagRegex().Matches(trimmed).Select(m => m.Value).ToList();
                continue;
            }

            foreach (Match m in WikiLinkRegex().Matches(trimmed))
            {
                var link = m.Groups[1].Value;
                if (!wikiLinks.Contains(link))
                    wikiLinks.Add(link);
            }
        }

        if (title is null) return null;

        return new WikiPage
        {
            Title = title,
            Summary = summary,
            Content = content,
            Tags = tags,
            WikiLinks = wikiLinks
        };
    }

    private static List<string> ExtractKeywords(params string[] sources)
    {
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            foreach (var word in WordSplitRegex().Split(source))
            {
                if (word.Length >= 2 && !word.StartsWith('#'))
                    keywords.Add(word.ToLowerInvariant());
            }
        }
        return [.. keywords];
    }

    private static string InferContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".txt" => "text/plain",
        ".md" => "text/markdown",
        ".json" => "application/json",
        ".csv" => "text/csv",
        _ => "application/octet-stream"
    };

    [GeneratedRegex(@"#[\w-]+")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\[\[([^\]]+)\]\]")]
    private static partial Regex WikiLinkRegex();

    [GeneratedRegex(@"[\s,;.!?]+")]
    private static partial Regex WordSplitRegex();
}
