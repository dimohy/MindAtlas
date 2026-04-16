using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MindAtlas.Core.Interfaces;
using MindAtlas.Core.Models;

namespace MindAtlas.Engine.Repository;

/// <summary>
/// Source-generated JSON context for NativeAOT-compatible serialization.
/// </summary>
[JsonSerializable(typeof(Dictionary<string, ProcessingStatus>))]
internal sealed partial class RawRepositoryJsonContext : JsonSerializerContext;

/// <summary>
/// File-based raw source repository — manages files in raw/ directory.
/// </summary>
public sealed class RawRepository : IRawRepository
{
    private readonly string _rawDir;
    private readonly string _statusFile;
    private readonly object _statusLock = new();

    public RawRepository(string dataRoot)
    {
        _rawDir = Path.Combine(dataRoot, "raw");
        _statusFile = Path.Combine(_rawDir, ".status.json");
        Directory.CreateDirectory(_rawDir);
    }

    public Task<IReadOnlyList<RawSource>> GetAllAsync(CancellationToken ct = default)
    {
        var statuses = LoadStatuses();
        var files = Directory.GetFiles(_rawDir)
            .Where(f => !Path.GetFileName(f).StartsWith('.'))
            .Select(f => ToRawSource(f, statuses))
            .ToList();
        return Task.FromResult<IReadOnlyList<RawSource>>(files);
    }

    public Task<RawSource?> GetByNameAsync(string fileName, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_rawDir, fileName);
        if (!File.Exists(filePath))
            return Task.FromResult<RawSource?>(null);

        var statuses = LoadStatuses();
        return Task.FromResult<RawSource?>(ToRawSource(filePath, statuses));
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
            .Select(f => ToRawSource(f, statuses))
            .Where(r => !statuses.ContainsKey(r.FileName) ||
                        statuses[r.FileName] == ProcessingStatus.Pending ||
                        statuses[r.FileName] == ProcessingStatus.Failed)
            .ToList();
        return Task.FromResult<IReadOnlyList<RawSource>>(files);
    }

    public Task UpdateStatusAsync(string fileName, ProcessingStatus status, CancellationToken ct = default)
    {
        lock (_statusLock)
        {
            var statuses = LoadStatuses();
            statuses[fileName] = status;
            SaveStatuses(statuses);
        }
        return Task.CompletedTask;
    }

    public Task<bool> TrySetProcessingAsync(string fileName, CancellationToken ct = default)
    {
        lock (_statusLock)
        {
            var statuses = LoadStatuses();
            if (statuses.TryGetValue(fileName, out var current) && current is ProcessingStatus.Processing)
                return Task.FromResult(false);

            statuses[fileName] = ProcessingStatus.Processing;
            SaveStatuses(statuses);
            return Task.FromResult(true);
        }
    }

    // --- Private helpers ---

    private static RawSource ToRawSource(string filePath, IReadOnlyDictionary<string, ProcessingStatus> statuses)
    {
        var fileInfo = new FileInfo(filePath);
        return new RawSource
        {
            FileName = fileInfo.Name,
            FilePath = filePath,
            ContentType = InferContentType(fileInfo.Extension),
            AddedAt = fileInfo.CreationTimeUtc,
            Status = statuses.TryGetValue(fileInfo.Name, out var s) ? s : ProcessingStatus.Pending
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
        return JsonSerializer.Deserialize(json, RawRepositoryJsonContext.Default.DictionaryStringProcessingStatus) ?? [];
    }

    private void SaveStatuses(Dictionary<string, ProcessingStatus> statuses)
    {
        var json = JsonSerializer.Serialize(statuses, RawRepositoryJsonContext.Default.DictionaryStringProcessingStatus);
        File.WriteAllText(_statusFile, json, Encoding.UTF8);
    }
}
