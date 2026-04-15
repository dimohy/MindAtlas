namespace MindAtlas.Core.Models;

/// <summary>
/// Type of vibe coding asset.
/// </summary>
public enum AssetType
{
    Prompt,
    Snippet,
    Template,
    Agent,
    Rule
}

/// <summary>
/// Represents a reusable vibe coding asset stored in the wiki.
/// </summary>
public sealed class VibeCodingAsset
{
    public AssetType AssetType { get; set; }
    public required string Name { get; set; }
    public required string Content { get; set; }
    public List<string> Tags { get; set; } = [];
}
