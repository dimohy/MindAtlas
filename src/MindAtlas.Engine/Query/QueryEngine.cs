using System.Runtime.CompilerServices;
using System.Text;
using MindAtlas.Core.Interfaces;
using MindAtlas.Core.Models;
using MindAtlas.Engine.Repository;
using Microsoft.Extensions.Logging;

namespace MindAtlas.Engine.Query;

/// <summary>
/// Query engine — Fast Path (keyword search) and AI Path (Copilot Agent).
/// </summary>
public sealed class QueryEngine
{
    private readonly IIndexService _indexService;
    private readonly IWikiRepository _wikiRepo;
    private readonly ICopilotAgentService _agent;
    private readonly WikiRepository _wikiRepoImpl;
    private readonly ILogger<QueryEngine>? _logger;

    public QueryEngine(
        IIndexService indexService,
        IWikiRepository wikiRepo,
        WikiRepository wikiRepoImpl,
        ICopilotAgentService agent,
        ILogger<QueryEngine>? logger = null)
    {
        _indexService = indexService;
        _wikiRepo = wikiRepo;
        _wikiRepoImpl = wikiRepoImpl;
        _agent = agent;
        _logger = logger;
    }

    /// <summary>
    /// Query the wiki — uses Fast Path for keyword matches, AI Path for complex questions.
    /// </summary>
    public async Task<QueryResult> QueryAsync(string question, CancellationToken ct = default)
    {
        _logger?.LogInformation("Query: {Question}", question);

        // Fast Path: keyword search
        var indexResults = await _indexService.SearchAsync(question, ct);
        if (indexResults.Count > 0)
        {
            _logger?.LogInformation("Fast Path: {Count} index matches", indexResults.Count);
        }

        // AI Path: send to agent with wiki context
        var context = await BuildQueryContextAsync(question, indexResults, ct);
        var prompt = BuildQueryPrompt(question, context);
        var response = await _agent.SendAsync(prompt, ct);

        var result = ParseQueryResponse(response, indexResults);

        // Log the query
        await _wikiRepoImpl.AppendLogAsync(new LogEntry
        {
            Operation = OperationType.Query,
            Description = $"Query: {Truncate(question, 100)}",
            AffectedPages = result.SourcePages.ToList()
        }, ct);

        return result;
    }

    /// <summary>
    /// Streaming query — returns chunks as they arrive from the agent.
    /// </summary>
    public async IAsyncEnumerable<string> QueryStreamingAsync(
        string question,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var indexResults = await _indexService.SearchAsync(question, ct);
        var context = await BuildQueryContextAsync(question, indexResults, ct);
        var prompt = BuildQueryPrompt(question, context);

        await foreach (var chunk in _agent.SendStreamingAsync(prompt, ct))
        {
            yield return chunk;
        }
    }

    // --- Private helpers ---

    private async Task<string> BuildQueryContextAsync(
        string question,
        IReadOnlyList<IndexEntry> indexResults,
        CancellationToken ct)
    {
        var sb = new StringBuilder();

        // Include matched pages' full content
        var relevantPages = indexResults.Take(5);
        foreach (var entry in relevantPages)
        {
            var page = await _wikiRepo.GetByNameAsync(entry.PageName, ct);
            if (page is not null)
            {
                sb.AppendLine($"## {page.Title}");
                sb.AppendLine(page.Content);
                sb.AppendLine();
            }
        }

        if (sb.Length is 0)
        {
            // No matches — provide index overview
            var allEntries = await _indexService.GetAllAsync(ct);
            sb.AppendLine("Available wiki pages:");
            foreach (var entry in allEntries.Take(30))
            {
                sb.AppendLine($"- {entry.PageName}: {entry.Summary}");
            }
        }

        return sb.ToString();
    }

    private static string BuildQueryPrompt(string question, string context)
    {
        return $"""
            Answer this question using the wiki knowledge below.
            
            ## Question
            {question}
            
            ## Wiki Context
            {context}
            
            ## Instructions
            - Cite source pages with [[PageName]] wikilinks.
            - If you discover new insights not in the wiki, note them at the end under "New Insights:".
            - Be concise but comprehensive.
            """;
    }

    private static QueryResult ParseQueryResponse(string response, IReadOnlyList<IndexEntry> indexResults)
    {
        var sourcePages = indexResults.Select(e => e.PageName).ToList();

        // Extract new insights section
        var newInsights = new List<string>();
        var insightMarker = "New Insights:";
        var insightIdx = response.IndexOf(insightMarker, StringComparison.OrdinalIgnoreCase);
        if (insightIdx >= 0)
        {
            var insightBlock = response[(insightIdx + insightMarker.Length)..];
            foreach (var line in insightBlock.Split('\n'))
            {
                var trimmed = line.Trim().TrimStart('-', '*', ' ');
                if (!string.IsNullOrWhiteSpace(trimmed))
                    newInsights.Add(trimmed);
            }
        }

        return new QueryResult
        {
            Answer = response,
            SourcePages = sourcePages,
            NewInsights = newInsights
        };
    }

    private static string Truncate(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..maxLength] + "...";
}
