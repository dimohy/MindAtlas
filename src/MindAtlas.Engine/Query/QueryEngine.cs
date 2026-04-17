using System.Runtime.CompilerServices;
using System.Text;
using MindAtlas.Core.Interfaces;
using MindAtlas.Core.Models;
using MindAtlas.Engine.Repository;
using Microsoft.Extensions.Configuration;
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
    private readonly IConfiguration? _configuration;
    private readonly ILogger<QueryEngine>? _logger;

    public QueryEngine(
        IIndexService indexService,
        IWikiRepository wikiRepo,
        WikiRepository wikiRepoImpl,
        ICopilotAgentService agent,
        IConfiguration? configuration = null,
        ILogger<QueryEngine>? logger = null)
    {
        _indexService = indexService;
        _wikiRepo = wikiRepo;
        _wikiRepoImpl = wikiRepoImpl;
        _agent = agent;
        _configuration = configuration;
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
        var ingestLang = _configuration?["MindAtlas:IngestLanguage"] ?? "en";
        var prompt = BuildQueryPrompt(question, context, ingestLang);
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
        var ingestLang = _configuration?["MindAtlas:IngestLanguage"] ?? "en";
        var prompt = BuildQueryPrompt(question, context, ingestLang);

        await foreach (var chunk in _agent.SendStreamingAsync(prompt, ct))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// Streaming query variant that opts in to the Copilot CLI built-in web
    /// fetch/URL tools. When <paramref name="useWebSearch"/> is false, url
    /// permission requests are denied by rules so the model cannot reach the
    /// open internet during this call.
    /// </summary>
    public async IAsyncEnumerable<string> QueryStreamingAsync(
        string question,
        bool useWebSearch,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var indexResults = await _indexService.SearchAsync(question, ct);
        var context = await BuildQueryContextAsync(question, indexResults, ct);
        var ingestLang = _configuration?["MindAtlas:IngestLanguage"] ?? "en";
        var prompt = BuildQueryPrompt(question, context, ingestLang);

        await foreach (var chunk in _agent.SendStreamingAsync(prompt, useWebSearch, ct))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// Coverage threshold: when the best Jaccard similarity between the
    /// question tokens and any matching page's keywords is below this
    /// value, the answer is considered poorly covered by the wiki.
    /// </summary>
    private const double CoverageThreshold = 0.5;

    /// <summary>
    /// Cap on the LLM-suggested title length (hard-trim after the model
    /// responds to keep UI rendering predictable).
    /// </summary>
    private const int MaxSuggestedTitleLength = 30;

    /// <summary>
    /// Decide whether the answer should be offered for save (§8.3).
    /// Keyword hit 0 OR max Jaccard &lt; 0.5 -> NeedsSave=true. When true,
    /// a follow-up LLM call produces a concise Korean title suggestion.
    /// </summary>
    public async Task<CoverageResult> CheckCoverageAsync(
        string question,
        string answer,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer))
            return new CoverageResult(false, null, null);

        var questionTokens = TokenUtil.TokenizeAndNormalize(question);
        var indexResults = await _indexService.SearchAsync(question, ct);

        bool needsSave;
        if (indexResults.Count == 0 || questionTokens.Count == 0)
        {
            needsSave = true;
        }
        else
        {
            double bestJaccard = 0.0;
            foreach (var entry in indexResults)
            {
                var entryTokens = new HashSet<string>(StringComparer.Ordinal);
                foreach (var k in entry.Keywords)
                    foreach (var t in TokenUtil.TokenizeAndNormalize(k))
                        entryTokens.Add(t);
                // Also include the page name itself so short-keyword pages
                // still register as covered.
                foreach (var t in TokenUtil.TokenizeAndNormalize(entry.PageName))
                    entryTokens.Add(t);

                var sim = TokenUtil.ComputeJaccard(questionTokens, entryTokens);
                if (sim > bestJaccard) bestJaccard = sim;
            }
            needsSave = bestJaccard < CoverageThreshold;
        }

        if (!needsSave) return new CoverageResult(false, null, null);

        // Ask the LLM for a concise Korean title. Fallback to a question-
        // derived title on any failure so the UI still has something to show.
        string? title = null;
        try
        {
            var prompt = $$"""
                Below is a user's question and the assistant's answer. Suggest a concise Korean wiki page title that captures the core topic. Output ONLY the title (no quotes, no prefix, no trailing punctuation). Max 30 characters.

                ## Question
                {{question}}

                ## Answer
                {{Truncate(answer, 1500)}}
                """;
            var raw = await _agent.SendAsync(prompt, ct);
            title = SanitizeSuggestedTitle(raw);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Title suggestion LLM call failed; falling back to question-derived title");
        }

        if (string.IsNullOrWhiteSpace(title))
            title = DeriveFallbackTitle(question);

        return new CoverageResult(true, title, null);
    }

    private static string SanitizeSuggestedTitle(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var line = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(static l => !string.IsNullOrWhiteSpace(l)) ?? string.Empty;
        line = line.Trim('"', '\'', '`', ' ', '.', '。', '!', '?');
        while (line.Length > 0 && (line[0] == '#' || line[0] == '-' || line[0] == '*'))
            line = line[1..].TrimStart();
        if (line.Length > MaxSuggestedTitleLength)
            line = line[..MaxSuggestedTitleLength].TrimEnd();
        foreach (var ch in new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' })
            line = line.Replace(ch.ToString(), "");
        return line.Trim();
    }

    private static string DeriveFallbackTitle(string question)
    {
        var trimmed = question.Trim();
        var cutoff = trimmed.IndexOfAny(['\n', '\r', '.', '?', '!', '。', '？', '！']);
        var head = cutoff > 0 ? trimmed[..cutoff].Trim() : trimmed;
        if (head.Length > MaxSuggestedTitleLength)
            head = head[..MaxSuggestedTitleLength].TrimEnd();
        return string.IsNullOrWhiteSpace(head) ? "untitled" : head;
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

    private static string BuildQueryPrompt(string question, string context, string language = "en")
    {
        var langInstruction = language != "en"
            ? $"\n            **IMPORTANT: Answer in {Ingest.IngestPipeline.GetLanguageName(language)}.**\n"
            : "";

        return $"""
            Answer this question using the wiki knowledge below.
            {langInstruction}
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
