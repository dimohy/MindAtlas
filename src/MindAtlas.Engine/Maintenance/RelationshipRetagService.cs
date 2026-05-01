using System.Text;
using System.Text.RegularExpressions;
using MindAtlas.Core.Interfaces;
using MindAtlas.Core.Models;
using MindAtlas.Engine.Ingest;

namespace MindAtlas.Engine.Maintenance;

/// <summary>
/// Generates and applies safe typed-relationship proposals for existing wiki links.
/// </summary>
public sealed partial class RelationshipRetagService(IWikiRepository wikiRepo)
{
    private static readonly RelationshipRule[] Rules =
    [
        new("contradicts", "context signals disagreement or conflict", ["contradicts", "conflicts with", "disagrees with", "refutes", "inconsistent with", "모순", "반박", "충돌", "불일치"], ["but", "however", "반면", "하지만", "그러나"]),
        new("supersedes", "context signals replacement or newer knowledge", ["supersedes", "replaces", "deprecated", "obsolete", "newer than", "대체", "폐기", "구버전", "최신", "갱신"], ["instead of", "no longer", "이전", "새로운"]),
        new("depends_on", "context signals dependency or requirement", ["depends on", "requires", "needs", "prerequisite", "의존", "필요", "요구", "전제"], ["before", "먼저", "기반"]),
        new("supports", "context signals evidence or support", ["supports", "evidence", "confirms", "proves", "validates", "근거", "증거", "지지", "뒷받침", "검증"], ["because", "therefore", "그래서", "따라서"]),
        new("fixes", "context signals a fix or resolution", ["fixes", "resolves", "patches", "해결", "수정", "고침"], ["bug", "issue", "문제"]),
        new("blocks", "context signals blocking or prevention", ["blocks", "prevents", "stops", "차단", "막음", "방해"], ["cannot", "못함", "불가"]),
        new("explains", "context signals explanation", ["explains", "describes why", "reason", "설명", "이유", "원인"], ["why", "because", "왜냐하면"]),
        new("example_of", "context signals an example relationship", ["example of", "instance of", "예시", "사례"], ["for example", "e.g.", "예를 들어"]),
        new("references", "context signals citation or reference", ["references", "cites", "refer to", "참조", "인용"], ["source", "citation", "출처"])
    ];

    public async Task<RelationshipRetagResult> ProposeAsync(CancellationToken ct = default)
    {
        var pages = await wikiRepo.GetAllAsync(ct);
        var pageNames = new HashSet<string>(pages.Select(page => page.Title), StringComparer.OrdinalIgnoreCase);
        var proposals = new List<RelationshipRetagProposal>();

        foreach (var page in pages)
            proposals.AddRange(CreateProposals(page, pageNames));

        return new RelationshipRetagResult
        {
            Proposals = proposals,
            SkippedCount = CountUntypedResolvableLinks(pages, pageNames) - proposals.Count
        };
    }

    public async Task<RelationshipRetagResult> ApplyAsync(string minimumConfidence = "high", CancellationToken ct = default)
    {
        var minimumRank = GetConfidenceRank(minimumConfidence);
        var pages = await wikiRepo.GetAllAsync(ct);
        var pageNames = new HashSet<string>(pages.Select(page => page.Title), StringComparer.OrdinalIgnoreCase);
        var allProposals = pages
            .SelectMany(page => CreateProposals(page, pageNames))
            .ToList();
        var applicable = allProposals
            .Where(proposal => GetConfidenceRank(proposal.Confidence) >= minimumRank)
            .GroupBy(proposal => proposal.PageTitle, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(proposal => proposal.StartIndex).ToList(), StringComparer.OrdinalIgnoreCase);

        return await ApplyProposalsAsync(pages, pageNames, allProposals, applicable, ct);
    }

    public async Task<RelationshipRetagResult> ApplySelectedAsync(
        IReadOnlyCollection<RelationshipRetagSelection> selections,
        CancellationToken ct = default)
    {
        var pages = await wikiRepo.GetAllAsync(ct);
        var pageNames = new HashSet<string>(pages.Select(page => page.Title), StringComparer.OrdinalIgnoreCase);
        var allProposals = pages
            .SelectMany(page => CreateProposals(page, pageNames))
            .ToList();
        var selectedKeys = selections
            .Select(selection => GetProposalKey(selection.PageTitle, selection.OriginalLink, selection.TargetTitle, selection.RelationshipType))
            .ToHashSet(StringComparer.Ordinal);
        var applicable = allProposals
            .Where(proposal => selectedKeys.Contains(GetProposalKey(proposal)))
            .GroupBy(proposal => proposal.PageTitle, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(proposal => proposal.StartIndex).ToList(), StringComparer.OrdinalIgnoreCase);

        return await ApplyProposalsAsync(pages, pageNames, allProposals, applicable, ct);
    }

    private async Task<RelationshipRetagResult> ApplyProposalsAsync(
        IReadOnlyList<WikiPage> pages,
        HashSet<string> pageNames,
        IReadOnlyList<RelationshipRetagProposal> allProposals,
        IReadOnlyDictionary<string, List<RelationshipRetagProposal>> applicable,
        CancellationToken ct)
    {

        var appliedCount = 0;
        foreach (var page in pages)
        {
            if (!applicable.TryGetValue(page.Title, out var proposals))
                continue;

            var content = new StringBuilder(page.Content);
            foreach (var proposal in proposals)
            {
                content.Remove(proposal.StartIndex, proposal.Length);
                content.Insert(proposal.StartIndex, proposal.ProposedLink);
                appliedCount++;
            }

            page.Content = content.ToString();
            page.WikiLinks = ExtractWikiTargets(page.Content, pageNames).ToList();
            page.UpdatedAt = DateTime.UtcNow;
            await wikiRepo.SaveAsync(page, ct);
        }

        return new RelationshipRetagResult
        {
            Proposals = allProposals,
            AppliedCount = appliedCount,
            SkippedCount = allProposals.Count - appliedCount
        };
    }

    private static string GetProposalKey(RelationshipRetagProposal proposal)
        => GetProposalKey(proposal.PageTitle, proposal.OriginalLink, proposal.TargetTitle, proposal.RelationshipType);

    private static string GetProposalKey(string pageTitle, string originalLink, string targetTitle, string relationshipType)
        => string.Join('\u001f', pageTitle, originalLink, targetTitle, relationshipType).ToUpperInvariant();

    private static IEnumerable<RelationshipRetagProposal> CreateProposals(WikiPage page, HashSet<string> pageNames)
    {
        var codeSpans = GetMarkdownCodeSpans(page.Content);
        foreach (Match match in WikiLinkRegex().Matches(page.Content))
        {
            if (IsInsideCodeSpan(match.Index, codeSpans))
                continue;

            var rawLink = match.Groups[1].Value;
            if (HasRelationshipType(rawLink))
                continue;

            var normalizedTarget = IngestPipeline.NormalizeWikiLinkTarget(rawLink);
            if (normalizedTarget.Length is 0 || !TryResolvePageTitle(normalizedTarget, pageNames, out var resolvedTitle))
                continue;

            var context = GetContextWindow(page.Content, match.Index, 180);
            var classification = Classify(context);
            if (classification is null)
                continue;

            yield return new RelationshipRetagProposal
            {
                PageTitle = page.Title,
                OriginalLink = match.Value,
                ProposedLink = BuildTypedWikiLink(rawLink, classification.Type),
                TargetTitle = resolvedTitle,
                RelationshipType = classification.Type,
                Confidence = classification.Confidence,
                Reason = classification.Reason,
                StartIndex = match.Index,
                Length = match.Length
            };
        }
    }

    private static RelationshipClassification? Classify(string context)
    {
        var normalized = context.ToLowerInvariant();
        RelationshipClassification? best = null;
        var bestScore = 0;
        var tie = false;

        foreach (var rule in Rules)
        {
            var score = 0;
            score += rule.StrongSignals.Count(signal => normalized.Contains(signal, StringComparison.OrdinalIgnoreCase)) * 2;
            score += rule.MediumSignals.Count(signal => normalized.Contains(signal, StringComparison.OrdinalIgnoreCase));

            if (score > bestScore)
            {
                bestScore = score;
                tie = false;
                best = new RelationshipClassification(
                    rule.Type,
                    score >= 2 ? "high" : "medium",
                    rule.Reason);
            }
            else if (score > 0 && score == bestScore)
            {
                tie = true;
            }
        }

        return bestScore is 0 || tie ? null : best;
    }

    private static string BuildTypedWikiLink(string rawLink, string relationshipType)
    {
        var parts = rawLink.Split('|', 2);
        if (parts.Length is 2)
        {
            var target = RelationshipMarkerRegex().Replace(parts[0], string.Empty).Trim();
            var label = RelationshipMarkerRegex().Replace(parts[1], string.Empty).Trim();
            return $"[[{target}|{label} @{relationshipType}]]";
        }

        var cleanTarget = RelationshipMarkerRegex().Replace(rawLink, string.Empty).Trim();
        return $"[[{cleanTarget} @{relationshipType}]]";
    }

    private static IEnumerable<string> ExtractWikiTargets(string markdown, HashSet<string> pageNames)
    {
        var codeSpans = GetMarkdownCodeSpans(markdown);
        foreach (Match match in WikiLinkRegex().Matches(markdown))
        {
            if (IsInsideCodeSpan(match.Index, codeSpans))
                continue;

            var target = IngestPipeline.NormalizeWikiLinkTarget(match.Groups[1].Value);
            if (target.Length > 0 && TryResolvePageTitle(target, pageNames, out var resolvedTitle))
                yield return resolvedTitle;
        }
    }

    private static int CountUntypedResolvableLinks(IReadOnlyList<WikiPage> pages, HashSet<string> pageNames)
        => pages.Sum(page => CreateUntypedResolvableLinks(page, pageNames).Count());

    private static IEnumerable<string> CreateUntypedResolvableLinks(WikiPage page, HashSet<string> pageNames)
    {
        var codeSpans = GetMarkdownCodeSpans(page.Content);
        foreach (Match match in WikiLinkRegex().Matches(page.Content))
        {
            if (IsInsideCodeSpan(match.Index, codeSpans))
                continue;

            var rawLink = match.Groups[1].Value;
            if (HasRelationshipType(rawLink))
                continue;

            var target = IngestPipeline.NormalizeWikiLinkTarget(rawLink);
            if (target.Length > 0 && TryResolvePageTitle(target, pageNames, out _))
                yield return target;
        }
    }

    private static bool HasRelationshipType(string rawLink)
    {
        foreach (Match match in RelationshipTypeRegex().Matches(rawLink))
        {
            if (RelationshipTypes.Contains(match.Groups[1].Value))
                return true;
        }

        return false;
    }

    private static bool TryResolvePageTitle(string target, HashSet<string> pageNames, out string resolvedTitle)
    {
        if (pageNames.TryGetValue(target, out resolvedTitle!))
            return true;

        var slashIndex = target.LastIndexOfAny(['/', '\\']);
        if (slashIndex >= 0 && slashIndex < target.Length - 1)
        {
            var leaf = target[(slashIndex + 1)..];
            if (pageNames.TryGetValue(leaf, out resolvedTitle!))
                return true;
        }

        resolvedTitle = string.Empty;
        return false;
    }

    private static string GetContextWindow(string text, int index, int radius)
    {
        var sentenceStart = text.LastIndexOfAny(['.', '!', '?', '\n', '\r'], Math.Max(0, index - 1));
        var sentenceEnd = text.IndexOfAny(['.', '!', '?', '\n', '\r'], index);
        var start = sentenceStart >= 0 ? sentenceStart + 1 : Math.Max(0, index - radius);
        var end = sentenceEnd >= 0 ? sentenceEnd : Math.Min(text.Length, index + radius);
        return text[start..end];
    }

    private static int GetConfidenceRank(string confidence) => confidence.Trim().ToLowerInvariant() switch
    {
        "medium" => 1,
        "high" => 2,
        _ => throw new ArgumentException("Minimum confidence must be 'high' or 'medium'.", nameof(confidence))
    };

    private static List<(int Start, int End)> GetMarkdownCodeSpans(string markdown)
    {
        var spans = new List<(int Start, int End)>();
        var position = 0;
        var fenceStart = -1;
        char fenceChar = default;
        var fenceLength = 0;

        foreach (Match lineMatch in LineRegex().Matches(markdown))
        {
            var line = lineMatch.Value;
            var fence = FenceRegex().Match(line);
            if (fence.Success)
            {
                var marker = fence.Groups[1].Value;
                if (fenceStart < 0)
                {
                    fenceStart = position;
                    fenceChar = marker[0];
                    fenceLength = marker.Length;
                }
                else if (marker[0] == fenceChar && marker.Length >= fenceLength)
                {
                    spans.Add((fenceStart, position + line.Length));
                    fenceStart = -1;
                }
            }

            position += line.Length;
        }

        if (fenceStart >= 0)
            spans.Add((fenceStart, markdown.Length));

        foreach (Match inline in InlineCodeRegex().Matches(markdown))
            spans.Add((inline.Index, inline.Index + inline.Length));

        return spans;
    }

    private static bool IsInsideCodeSpan(int index, List<(int Start, int End)> spans)
        => spans.Any(span => index >= span.Start && index < span.End);

    private sealed record RelationshipRule(
        string Type,
        string Reason,
        string[] StrongSignals,
        string[] MediumSignals);

    private sealed record RelationshipClassification(string Type, string Confidence, string Reason);

    private static readonly HashSet<string> RelationshipTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "supports", "contradicts", "supersedes", "references", "causes", "depends_on",
        "prerequisite_for", "parent_of", "child_of", "part_of", "example_of", "instance_of",
        "similar_to", "related_to", "blocks", "blocked_by", "enables", "fixes", "explains",
        "challenges", "evidence_for", "evidence_against", "derived_from", "evolution_of"
    };

    [GeneratedRegex(@"\[\[([^\]]+)\]\]")]
    private static partial Regex WikiLinkRegex();

    [GeneratedRegex(@"\s@[-\w]+")]
    private static partial Regex RelationshipMarkerRegex();

    [GeneratedRegex(@"(?:^|\s)@([A-Za-z][\w-]*)")]
    private static partial Regex RelationshipTypeRegex();

    [GeneratedRegex(@"^\s*>?\s*(`{3,}|~{3,})", RegexOptions.Multiline)]
    private static partial Regex FenceRegex();

    [GeneratedRegex(@".*(?:\r\n|\n|\r|$)")]
    private static partial Regex LineRegex();

    [GeneratedRegex(@"`[^`\r\n]+`")]
    private static partial Regex InlineCodeRegex();
}
