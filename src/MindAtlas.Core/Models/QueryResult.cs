namespace MindAtlas.Core.Models;

/// <summary>
/// Result of a wiki query operation.
/// </summary>
public sealed class QueryResult
{
    public required string Answer { get; set; }
    public List<string> SourcePages { get; set; } = [];
    public List<string> NewInsights { get; set; } = [];
}
