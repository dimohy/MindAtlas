namespace MindAtlas.Core.Models;

/// <summary>
/// Identifies one relationship retag proposal selected by a user for safe application.
/// </summary>
public sealed class RelationshipRetagSelection
{
    public required string PageTitle { get; init; }
    public required string OriginalLink { get; init; }
    public required string TargetTitle { get; init; }
    public required string RelationshipType { get; init; }
}
