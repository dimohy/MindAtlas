using System.Text;
using MindAtlas.Core.Interfaces;
using MindAtlas.Core.Models;

namespace MindAtlas.Engine.Repository;

/// <summary>
/// File-based raw source repository — manages files in raw/ directory.
/// </summary>
public sealed class RawRepository : IRawRepository
{
    private readonly string _rawDir;
    private readonly string _statusFile;

    public RawRepository(string dataRoot)
    {
        _rawDir = Path.Combine(dataRoot, "raw");
        _statusFile = Path.Combine(_rawDir, ".status.json");
        Directory.CreateDirectory(_rawDir);
    }

    public Task<IReadOnlyList<RawSource>> GetAllAsync(CancellationToken ct = default)
    {
        var files = Directory.GetFiles(_rawDir)
            .Where(f => !Path.GetFileName(f).StartsWith('.'))
            .Select(ToRawSource)
            .ToList();
        return Task.FromResult<IReadOnlyList<RawSource>>(files);
    }

    public Task<RawSource?> GetByNameAsync(string fileName, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_rawDir, fileName);
        if (!File.Exists(filePath))
            return Task.FromResult<RawSource?>(null);

        return Task.FromResult<RawSource?>(ToRawSource(filePath));
    }

    public async Task SaveAsync(string fileName, Stream content, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_rawDir, fileName);
        await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fs, ct);
    }

    public Task<IReadOnlyList<RawSource>> GetUnprocessedAsync(CancellationToken ct = default)
    {
        var statuses = LoadStatuses();
        var files = Directory.GetFiles(_rawDir)
            .Where(f => !Path.GetFileName(f).StartsWith('.'))
            .Select(ToRawSource)
            .Where(r => !statuses.ContainsKey(r.FileName) ||
                        statuses[r.FileName] == ProcessingStatus.Pending ||
                        statuses[r.FileName] == ProcessingStatus.Failed)
            .ToList();
        return Task.FromResult<IReadOnlyList<RawSource>>(files);
    }

    public Task UpdateStatusAsync(string fileName, ProcessingStatus status, CancellationToken ct = default)
    {
        var statuses = LoadStatuses();
        statuses[fileName] = status;
        SaveStatuses(statuses);
        return Task.CompletedTask;
    }

    // --- Private helpers ---

    private static RawSource ToRawSource(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        return new RawSource
        {
            FileName = fileInfo.Name,
            FilePath = filePath,
            ContentType = InferContentType(fileInfo.Extension),
            AddedAt = fileInfo.CreationTimeUtc
        };
    }

    private static string InferContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".txt" => "text/plain",
        ".md" => "text/markdown",
        ".json" => "application/json",
        ".csv" => "text/csv",
        ".html" or ".htm" => "text/html",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".pdf" => "application/pdf",
        _ => "application/octet-stream"
    };

    private Dictionary<string, ProcessingStatus> LoadStatuses()
    {
        if (!File.Exists(_statusFile))
            return [];

        var json = File.ReadAllText(_statusFile);
        return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, ProcessingStatus>>(json) ?? [];
    }

    private void SaveStatuses(Dictionary<string, ProcessingStatus> statuses)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(statuses,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_statusFile, json, Encoding.UTF8);
    }
}
