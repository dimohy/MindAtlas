namespace MindAtlas.Core.Models;

/// <summary>
/// Represents a wiki page in the knowledge base (Layer 2 — wiki/).
/// </summary>
public sealed class WikiPage
{
    public required string Title { get; set; }
    public string Summary { get; set; } = string.Empty;
    public required string Content { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<string> WikiLinks { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
