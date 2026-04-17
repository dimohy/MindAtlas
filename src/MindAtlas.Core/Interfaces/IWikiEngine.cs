using MindAtlas.Core.Models;

namespace MindAtlas.Core.Interfaces;

/// <summary>
/// Core wiki engine — Ingest/Query/Lint operations (3-Operation pattern).
/// </summary>
public interface IWikiEngine
{
    Task<IReadOnlyList<string>> IngestAsync(string rawFilePath, CancellationToken ct = default);
    Task<QueryResult> QueryAsync(string question, CancellationToken ct = default);
    IAsyncEnumerable<string> QueryStreamingAsync(string question, CancellationToken ct = default);

    /// <summary>
    /// Streaming query that opts in/out of the Copilot CLI built-in web
    /// fetch/URL tools for this request only.
    /// </summary>
    IAsyncEnumerable<string> QueryStreamingAsync(string question, bool useWebSearch, CancellationToken ct = default);
    Task<LintResult> LintAsync(CancellationToken ct = default);
    Task<int> LintFixAsync(CancellationToken ct = default);
}
