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

    /// <summary>
    /// Attempts to auto-fix lint issues — adds missing index entries, removes stale index entries.
    /// Returns the number of fixes applied.
    /// </summary>
    public async Task<int> AutoFixAsync(CancellationToken ct = default)
    {
        var result = await LintAsync(ct);
        var fixCount = 0;

        // Fix missing index — add missing pages to index
        foreach (var pageName in result.MissingIndex)
        {
            var page = (await _wikiRepo.GetAllAsync(ct)).FirstOrDefault(p =>
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

        return fixCount;
    }
}
