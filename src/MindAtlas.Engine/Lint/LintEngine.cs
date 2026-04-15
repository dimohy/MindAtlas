using System.Text.RegularExpressions;
using MindAtlas.Core.Interfaces;
using MindAtlas.Core.Models;

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

        // Collect all wikilinks across all pages
        var allIncomingLinks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var brokenLinks = new List<string>();

        foreach (var page in pages)
        {
            var links = WikiLinkRegex().Matches(page.Content)
                .Select(m => m.Groups[1].Value)
                .Distinct();

            foreach (var link in links)
            {
                allIncomingLinks.Add(link);

                if (!pageNames.Contains(link))
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
}
