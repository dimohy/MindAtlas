namespace MindAtlas.Core.Models;

/// <summary>
/// Represents an entry in the wiki index (index.md).
/// </summary>
public sealed class IndexEntry
{
    public required string PageName { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public List<string> Keywords { get; set; } = [];
}
