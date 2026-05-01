using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MindAtlas.Core.Interfaces;
using MindAtlas.Core.Models;

namespace MindAtlas.Engine.Repository;

/// <summary>
/// Persisted record for a single raw file's processing status.
/// Serialized into .status.json. Older installations used a bare
/// <see cref="ProcessingStatus"/> integer value; loading tolerates that shape
/// and upgrades to this record on next save.
/// </summary>
public sealed class RawStatusRecord
{
    public ProcessingStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? FailedAt { get; set; }
}

/// <summary>
/// Source-generated JSON context for NativeAOT-compatible serialization.
/// </summary>
[JsonSerializable(typeof(Dictionary<string, RawStatusRecord>))]
[JsonSerializable(typeof(Dictionary<string, ProcessingStatus>))]
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
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
            .Where(r => r.Status == ProcessingStatus.Pending || r.Status == ProcessingStatus.Failed)
            .ToList();
        return Task.FromResult<IReadOnlyList<RawSource>>(files);
    }

    public Task UpdateStatusAsync(string fileName, ProcessingStatus status, CancellationToken ct = default)
        => UpdateStatusAsync(fileName, status, null, ct);

    public Task UpdateStatusAsync(string fileName, ProcessingStatus status, string? errorMessage, CancellationToken ct = default)
    {
        lock (_statusLock)
        {
            var statuses = LoadStatuses();
            if (!statuses.TryGetValue(fileName, out var rec))
            {
                rec = new RawStatusRecord();
                statuses[fileName] = rec;
            }
            rec.Status = status;
            if (status == ProcessingStatus.Failed)
            {
                rec.ErrorMessage = errorMessage;
                rec.FailedAt = DateTime.UtcNow;
            }
            else
            {
                // Clear failure details when transitioning out of Failed state
                // so the UI stops showing a stale error tooltip.
                rec.ErrorMessage = null;
                rec.FailedAt = null;
            }
            SaveStatuses(statuses);
        }
        return Task.CompletedTask;
    }

    public Task<bool> TrySetProcessingAsync(string fileName, CancellationToken ct = default)
    {
        lock (_statusLock)
        {
            var statuses = LoadStatuses();
            if (statuses.TryGetValue(fileName, out var rec) && rec.Status is ProcessingStatus.Processing)
                return Task.FromResult(false);

            if (rec is null)
            {
                rec = new RawStatusRecord();
                statuses[fileName] = rec;
            }
            rec.Status = ProcessingStatus.Processing;
            rec.ErrorMessage = null;
            rec.FailedAt = null;
            SaveStatuses(statuses);
            return Task.FromResult(true);
        }
    }

    // --- Private helpers ---

    private static RawSource ToRawSource(string filePath, IReadOnlyDictionary<string, RawStatusRecord> statuses)
    {
        var fileInfo = new FileInfo(filePath);
        statuses.TryGetValue(fileInfo.Name, out var rec);
        return new RawSource
        {
            FileName = fileInfo.Name,
            FilePath = filePath,
            ContentType = InferContentType(fileInfo.Extension),
            AddedAt = fileInfo.CreationTimeUtc,
            Status = rec?.Status ?? ProcessingStatus.Pending,
            ErrorMessage = rec?.ErrorMessage,
            FailedAt = rec?.FailedAt,
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

    private Dictionary<string, RawStatusRecord> LoadStatuses()
    {
        if (!File.Exists(_statusFile))
            return [];

        string json;
        try { json = File.ReadAllText(_statusFile); }
        catch { return []; }
        if (string.IsNullOrWhiteSpace(json)) return [];

        // First try new object-per-file format.
        try
        {
            var parsed = JsonSerializer.Deserialize(json, RawRepositoryJsonContext.Default.DictionaryStringRawStatusRecord);
            if (parsed is not null && IsObjectShape(json))
                return parsed;
        }
        catch { /* fall through to legacy */ }

        // Legacy format: Dictionary<string, ProcessingStatus> — upgrade in place.
        try
        {
            var legacy = JsonSerializer.Deserialize(json, RawRepositoryJsonContext.Default.DictionaryStringProcessingStatus);
            var dict = new Dictionary<string, RawStatusRecord>();
            if (legacy is not null)
            {
                foreach (var kvp in legacy)
                    dict[kvp.Key] = new RawStatusRecord { Status = kvp.Value };
            }
            return dict;
        }
        catch
        {
            return [];
        }
    }

    private static bool IsObjectShape(string json)
    {
        // Distinguish {"file": 2} (legacy) vs {"file": {"Status": 2}} (new).
        var node = JsonNode.Parse(json);
        if (node is not JsonObject obj) return false;
        foreach (var kvp in obj)
        {
            if (kvp.Value is JsonObject) return true;
            return false; // any non-object value → legacy
        }
        return true; // empty → treat as new
    }

    private void SaveStatuses(Dictionary<string, RawStatusRecord> statuses)
    {
        var json = JsonSerializer.Serialize(statuses, RawRepositoryJsonContext.Default.DictionaryStringRawStatusRecord);
        File.WriteAllText(_statusFile, json, Encoding.UTF8);
    }
}
