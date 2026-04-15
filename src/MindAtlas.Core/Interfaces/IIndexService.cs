using MindAtlas.Core.Models;

namespace MindAtlas.Core.Interfaces;

/// <summary>
/// Fast keyword-based index service for O(ms) search.
/// </summary>
public interface IIndexService
{
    Task<IReadOnlyList<IndexEntry>> SearchAsync(string keyword, CancellationToken ct = default);
    Task RebuildAsync(CancellationToken ct = default);
    Task<IReadOnlyList<IndexEntry>> GetAllAsync(CancellationToken ct = default);
}
