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
    Task UpdateAsync(IndexEntry entry, CancellationToken ct = default);
    Task RemoveAsync(string pageName, CancellationToken ct = default);
}
