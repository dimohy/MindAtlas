using System.Text.RegularExpressions;
using MindAtlas.Core.Interfaces;
using MindAtlas.Core.Models;
using MindAtlas.Engine.Ingest;

namespace MindAtlas.Engine.Lint;

/// <summary>
/// Detects wiki inconsistencies — orphan pages, broken links, missing index entries.
/// </summary>
public sealed partial class LintEngine
{
    private readonly IWikiRepository _wikiRepo;
    private readonly IIndexService _indexService;

    public LintEngine(IWikiRepository wikiRepo, IIndexService indexService)
    {
        _wikiRepo = wikiRepo;
        _indexService = indexService;
    }

    public async Task<LintResult> LintAsync(CancellationToken ct = default)
    {
        var pages = await _wikiRepo.GetAllAsync(ct);
        var indexEntries = await _indexService.GetAllAsync(ct);

        var pageNames = new HashSet<string>(
            pages.Select(p => p.Title), StringComparer.OrdinalIgnoreCase);
        var indexedNames = new HashSet<string>(
            indexEntries.Select(e => e.PageName), StringComparer.OrdinalIgnoreCase);

        // Collect all wikilinks across all pages.
        var allIncomingLinks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var brokenLinks = new List<string>();

        foreach (var page in pages)
        {
            var links = ExtractWikiTargets(page.Content)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var link in links)
            {
                if (TryResolvePageTitle(link, pageNames, out var resolvedTitle))
                {
                    allIncomingLinks.Add(resolvedTitle);
                }
                else
                {
                    brokenLinks.Add($"[[{link}]] in {page.Title}");
                }
            }
        }

        // Orphan pages — no incoming wikilinks from other pages
        var orphanPages = pages
            .Where(p => !allIncomingLinks.Contains(p.Title))
            .Select(p => p.Title)
            .ToList();

        // Missing index — pages that exist but aren't in index.md
        var missingIndex = pageNames
            .Where(name => !indexedNames.Contains(name))
            .ToList();

        // Stale index — entries in index.md for deleted pages
        var conflicts = indexedNames
            .Where(name => !pageNames.Contains(name))
            .Select(name => $"Stale index entry: {name}")
            .ToList();

        return new LintResult
        {
            OrphanPages = orphanPages,
            BrokenLinks = brokenLinks,
            MissingIndex = missingIndex,
            Conflicts = conflicts
        };
    }

    [GeneratedRegex(@"\[\[([^\]]+)\]\]")]
    private static partial Regex WikiLinkRegex();

    /// <summary>
    /// Attempts to auto-fix lint issues — adds missing index entries, removes stale index entries.
    /// Returns the number of fixes applied.
    /// </summary>
    public async Task<int> AutoFixAsync(CancellationToken ct = default)
    {
        var pages = await _wikiRepo.GetAllAsync(ct);
        var result = await LintAsync(ct);
        var fixCount = 0;

        // Fix missing index — add missing pages to index
        foreach (var pageName in result.MissingIndex)
        {
            var page = pages.FirstOrDefault(p =>
                string.Equals(p.Title, pageName, StringComparison.OrdinalIgnoreCase));

            if (page is null) continue;

            await _indexService.UpdateAsync(new IndexEntry
            {
                PageName = page.Title,
                Summary = page.Summary,
                Tags = page.Tags,
                Keywords = [page.Title]
            }, ct);
            fixCount++;
        }

        // Fix stale index — remove entries for deleted pages
        foreach (var conflict in result.Conflicts)
        {
            // Extract page name from "Stale index entry: {name}"
            var name = conflict.Replace("Stale index entry: ", "");
            await _indexService.RemoveAsync(name, ct);
            fixCount++;
        }

        fixCount += await RepairBrokenWikiLinksAsync(pages, ct);

        return fixCount;
    }

    private async Task<int> RepairBrokenWikiLinksAsync(IReadOnlyList<WikiPage> pages, CancellationToken ct)
    {
        var pageNames = new HashSet<string>(pages.Select(p => p.Title), StringComparer.OrdinalIgnoreCase);
        var fixes = 0;

        foreach (var page in pages)
        {
            var codeSpans = GetMarkdownCodeSpans(page.Content);
            var changed = false;

            var repaired = WikiLinkRegex().Replace(page.Content, match =>
            {
                if (IsInsideCodeSpan(match.Index, codeSpans))
                    return match.Value;

                var rawLink = match.Groups[1].Value;
                var target = IngestPipeline.NormalizeWikiLinkTarget(rawLink);
                if (target.Length is 0)
                    return match.Value;

                if (TryResolvePageTitle(target, pageNames, out var resolvedTitle))
                {
                    if (string.Equals(target, resolvedTitle, StringComparison.Ordinal))
                        return match.Value;

                    changed = true;
                    fixes++;
                    return BuildWikiLink(rawLink, resolvedTitle);
                }

                var nearMatch = FindNearMatch(target, pageNames);
                if (nearMatch is not null)
                {
                    changed = true;
                    fixes++;
                    return BuildWikiLink(rawLink, nearMatch);
                }

                changed = true;
                fixes++;
                return GetDisplayText(rawLink);
            });

            if (!changed) continue;

            page.Content = repaired;
            page.WikiLinks = ExtractWikiTargets(repaired)
                .Where(link => TryResolvePageTitle(link, pageNames, out _))
                .Select(link => TryResolvePageTitle(link, pageNames, out var resolved) ? resolved : link)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            await _wikiRepo.SaveAsync(page, ct);
        }

        return fixes;
    }

    private static IEnumerable<string> ExtractWikiTargets(string markdown)
    {
        var codeSpans = GetMarkdownCodeSpans(markdown);

        foreach (Match match in WikiLinkRegex().Matches(markdown))
        {
            if (IsInsideCodeSpan(match.Index, codeSpans))
                continue;

            var target = IngestPipeline.NormalizeWikiLinkTarget(match.Groups[1].Value);
            if (target.Length > 0)
                yield return target;
        }
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

    private static string? FindNearMatch(string target, HashSet<string> pageNames)
    {
        var matches = pageNames
            .Where(name => LevenshteinDistance(target, name) <= 2)
            .Take(2)
            .ToList();

        return matches.Count is 1 ? matches[0] : null;
    }

    private static string BuildWikiLink(string rawLink, string target)
    {
        var parts = rawLink.Split('|', 2);
        return parts.Length is 2
            ? $"[[{target}|{parts[1].Trim()}]]"
            : $"[[{target}]]";
    }

    private static string GetDisplayText(string rawLink)
    {
        var parts = rawLink.Split('|', 2);
        var display = parts.Length is 2 ? parts[1] : parts[0];
        return RelationshipMarkerRegex().Replace(display, string.Empty).Trim();
    }

    private static int LevenshteinDistance(string left, string right)
    {
        if (left.Length is 0) return right.Length;
        if (right.Length is 0) return left.Length;

        Span<int> previous = stackalloc int[right.Length + 1];
        Span<int> current = stackalloc int[right.Length + 1];

        for (var j = 0; j <= right.Length; j++)
            previous[j] = j;

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = char.ToUpperInvariant(left[i - 1]) == char.ToUpperInvariant(right[j - 1]) ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            current.CopyTo(previous);
        }

        return previous[right.Length];
    }

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

    [GeneratedRegex(@"^\s*>?\s*(`{3,}|~{3,})", RegexOptions.Multiline)]
    private static partial Regex FenceRegex();

    [GeneratedRegex(@".*(?:\r\n|\n|\r|$)")]
    private static partial Regex LineRegex();

    [GeneratedRegex(@"`[^`\r\n]+`")]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"\s@[-\w]+")]
    private static partial Regex RelationshipMarkerRegex();
}
