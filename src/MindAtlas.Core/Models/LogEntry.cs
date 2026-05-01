namespace MindAtlas.Core.Models;

/// <summary>
/// Type of wiki engine operation.
/// </summary>
public enum OperationType
{
    Ingest,
    Query,
    Lint
}

/// <summary>
/// Represents a log entry for wiki operations (log.md).
/// </summary>
public sealed class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public OperationType Operation { get; set; }
    public required string Description { get; set; }
    public List<string> AffectedPages { get; set; } = [];
}
