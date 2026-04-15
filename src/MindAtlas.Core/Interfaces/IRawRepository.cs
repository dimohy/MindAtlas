using MindAtlas.Core.Models;

namespace MindAtlas.Core.Interfaces;

/// <summary>
/// Repository for raw source file management (raw/ directory).
/// </summary>
public interface IRawRepository
{
    Task<IReadOnlyList<RawSource>> GetAllAsync(CancellationToken ct = default);
    Task<RawSource?> GetByNameAsync(string fileName, CancellationToken ct = default);
    Task SaveAsync(string fileName, Stream content, CancellationToken ct = default);
    Task<IReadOnlyList<RawSource>> GetUnprocessedAsync(CancellationToken ct = default);
    Task UpdateStatusAsync(string fileName, ProcessingStatus status, CancellationToken ct = default);
}
