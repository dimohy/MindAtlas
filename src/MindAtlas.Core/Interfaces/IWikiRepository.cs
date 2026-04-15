using MindAtlas.Core.Models;

namespace MindAtlas.Core.Interfaces;

/// <summary>
/// Repository for wiki page CRUD operations (wiki/ directory).
/// </summary>
public interface IWikiRepository
{
    Task<IReadOnlyList<WikiPage>> GetAllAsync(CancellationToken ct = default);
    Task<WikiPage?> GetByNameAsync(string pageName, CancellationToken ct = default);
    Task SaveAsync(WikiPage page, CancellationToken ct = default);
    Task DeleteAsync(string pageName, CancellationToken ct = default);
    Task<IReadOnlyList<IndexEntry>> GetIndexAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LogEntry>> GetLogAsync(int? limit = null, CancellationToken ct = default);
}
