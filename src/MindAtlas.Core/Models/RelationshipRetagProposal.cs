namespace MindAtlas.Core.Models;

/// <summary>
/// Describes a safe typed-relationship retag candidate for an existing wiki link.
/// </summary>
public sealed class RelationshipRetagProposal
{
    public required string PageTitle { get; init; }
    public required string OriginalLink { get; init; }
    public required string ProposedLink { get; init; }
    public required string TargetTitle { get; init; }
    public required string RelationshipType { get; init; }
    public required string Confidence { get; init; }
    public required string Reason { get; init; }
    public int StartIndex { get; init; }
    public int Length { get; init; }
}
