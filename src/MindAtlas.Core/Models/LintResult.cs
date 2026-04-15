namespace MindAtlas.Core.Models;

/// <summary>
/// Result of a lint operation — detects wiki inconsistencies.
/// </summary>
public sealed class LintResult
{
    public List<string> OrphanPages { get; set; } = [];
    public List<string> BrokenLinks { get; set; } = [];
    public List<string> MissingIndex { get; set; } = [];
    public List<string> Conflicts { get; set; } = [];
}
