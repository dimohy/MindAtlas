namespace MindAtlas.Core.Models;

/// <summary>
/// Processing status of a raw source file.
/// </summary>
public enum ProcessingStatus
{
    Pending,
    Processing,
    Done,
    Failed
}

/// <summary>
/// Represents a raw source file (Layer 1 — raw/).
/// </summary>
public sealed class RawSource
{
    public required string FileName { get; set; }
    public required string FilePath { get; set; }
    public string ContentType { get; set; } = "text/plain";
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public ProcessingStatus Status { get; set; } = ProcessingStatus.Pending;

    /// <summary>
    /// Last error message when Status is Failed. Null otherwise.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// UTC timestamp of the most recent failed ingest attempt.
    /// </summary>
    public DateTime? FailedAt { get; set; }
}
