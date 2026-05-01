namespace MindAtlas.Core.Models;

/// <summary>
/// Result of a typed-relationship retag proposal or apply pass.
/// </summary>
public sealed class RelationshipRetagResult
{
    public IReadOnlyList<RelationshipRetagProposal> Proposals { get; init; } = [];
    public int AppliedCount { get; init; }
    public int SkippedCount { get; init; }
}
