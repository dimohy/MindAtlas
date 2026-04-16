using System.Runtime.CompilerServices;
using MindAtlas.Core.Interfaces;
using MindAtlas.Core.Models;
using MindAtlas.Engine.Ingest;
using MindAtlas.Engine.Lint;
using MindAtlas.Engine.Query;

namespace MindAtlas.Engine;

/// <summary>
/// Main wiki engine — orchestrates Ingest/Query/Lint operations.
/// </summary>
public sealed class WikiEngine : IWikiEngine
{
    private readonly IngestPipeline _ingestPipeline;
    private readonly QueryEngine _queryEngine;
    private readonly LintEngine _lintEngine;

    public WikiEngine(IngestPipeline ingestPipeline, QueryEngine queryEngine, LintEngine lintEngine)
    {
        _ingestPipeline = ingestPipeline;
        _queryEngine = queryEngine;
        _lintEngine = lintEngine;
    }

    public Task<IReadOnlyList<string>> IngestAsync(string rawFilePath, CancellationToken ct = default)
        => _ingestPipeline.IngestAsync(rawFilePath, ct);

    public Task<QueryResult> QueryAsync(string question, CancellationToken ct = default)
        => _queryEngine.QueryAsync(question, ct);

    public IAsyncEnumerable<string> QueryStreamingAsync(string question, CancellationToken ct = default)
        => _queryEngine.QueryStreamingAsync(question, ct);

    public Task<LintResult> LintAsync(CancellationToken ct = default)
        => _lintEngine.LintAsync(ct);

    public Task<int> LintFixAsync(CancellationToken ct = default)
        => _lintEngine.AutoFixAsync(ct);
}
