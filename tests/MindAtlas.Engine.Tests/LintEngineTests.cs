using MindAtlas.Core.Interfaces;
using MindAtlas.Core.Models;
using MindAtlas.Engine.Lint;

namespace MindAtlas.Engine.Tests;

public class LintEngineTests
{
    [Fact]
    public async Task Lint_DetectsOrphanPages()
    {
        // Page B links to A, but nobody links to B → B is orphan
        var pages = new List<WikiPage>
        {
            new() { Title = "PageA", Content = "Some text", WikiLinks = [] },
            new() { Title = "PageB", Content = "See [[PageA]] for details", WikiLinks = ["PageA"] }
        };
        var wikiRepo = new FakeWikiRepo(pages);
        var indexService = new FakeIndexService(pages.Select(p => new IndexEntry { PageName = p.Title }).ToList());
        var sut = new LintEngine(wikiRepo, indexService);

        var result = await sut.LintAsync();

        Assert.Contains("PageB", result.OrphanPages);
        // PageA also has no incoming links in this set except from PageB? Actually PageB links to PageA → PageA has incoming
        // PageA is NOT orphan, PageB is orphan
        Assert.DoesNotContain("PageA", result.OrphanPages);
    }

    [Fact]
    public async Task Lint_DetectsBrokenLinks()
    {
        var pages = new List<WikiPage>
        {
            new() { Title = "ExistingPage", Content = "Ref to [[NonExistent]] here", WikiLinks = ["NonExistent"] }
        };
        var wikiRepo = new FakeWikiRepo(pages);
        var indexService = new FakeIndexService([new IndexEntry { PageName = "ExistingPage" }]);
        var sut = new LintEngine(wikiRepo, indexService);

        var result = await sut.LintAsync();

        Assert.Single(result.BrokenLinks);
        Assert.Contains("NonExistent", result.BrokenLinks[0]);
    }

    [Fact]
    public async Task Lint_IgnoresWikiLinksInsideCodeBlocks()
    {
        var pages = new List<WikiPage>
        {
            new()
            {
                Title = "ExistingPage",
                Content = """
                    ```markdown
                    [[ExampleOnly]]
                    ```
                    Real link to [[TargetPage]].
                    """,
                WikiLinks = ["TargetPage"]
            },
            new() { Title = "TargetPage", Content = "Back to [[ExistingPage]].", WikiLinks = ["ExistingPage"] }
        };
        var wikiRepo = new FakeWikiRepo(pages);
        var indexService = new FakeIndexService(pages.Select(p => new IndexEntry { PageName = p.Title }).ToList());
        var sut = new LintEngine(wikiRepo, indexService);

        var result = await sut.LintAsync();

        Assert.Empty(result.BrokenLinks);
    }

    [Fact]
    public async Task AutoFix_RepairsNearMatchAndRemovesUnresolvableWikiLinks()
    {
        var pages = new List<WikiPage>
        {
            new()
            {
                Title = "ExistingPage",
                Content = "See [[Alhpa]] and [[MissingPage|missing note @references]].",
                WikiLinks = ["Alhpa", "MissingPage"]
            },
            new() { Title = "Alpha", Content = "Canonical page.", WikiLinks = [] }
        };
        var wikiRepo = new FakeWikiRepo(pages);
        var indexService = new FakeIndexService(pages.Select(p => new IndexEntry { PageName = p.Title }).ToList());
        var sut = new LintEngine(wikiRepo, indexService);

        var fixCount = await sut.AutoFixAsync();
        var result = await sut.LintAsync();

        Assert.True(fixCount >= 2);
        Assert.Contains("[[Alpha]]", pages[0].Content);
        Assert.Contains("missing note", pages[0].Content);
        Assert.DoesNotContain("[[MissingPage", pages[0].Content);
        Assert.Empty(result.BrokenLinks);
    }

    [Fact]
    public async Task Lint_DetectsMissingIndex()
    {
        var pages = new List<WikiPage>
        {
            new() { Title = "Indexed", Content = "Content" },
            new() { Title = "NotIndexed", Content = "Content" }
        };
        // Only "Indexed" is in the index
        var wikiRepo = new FakeWikiRepo(pages);
        var indexService = new FakeIndexService([new IndexEntry { PageName = "Indexed" }]);
        var sut = new LintEngine(wikiRepo, indexService);

        var result = await sut.LintAsync();

        Assert.Contains("NotIndexed", result.MissingIndex);
        Assert.DoesNotContain("Indexed", result.MissingIndex);
    }

    [Fact]
    public async Task Lint_DetectsStaleIndexEntries()
    {
        // Index has "DeletedPage" but no actual wiki page
        var pages = new List<WikiPage>
        {
            new() { Title = "ActivePage", Content = "Content" }
        };
        var indexEntries = new List<IndexEntry>
        {
            new() { PageName = "ActivePage" },
            new() { PageName = "DeletedPage" }
        };
        var wikiRepo = new FakeWikiRepo(pages);
        var indexService = new FakeIndexService(indexEntries);
        var sut = new LintEngine(wikiRepo, indexService);

        var result = await sut.LintAsync();

        Assert.Single(result.Conflicts);
        Assert.Contains("DeletedPage", result.Conflicts[0]);
    }

    [Fact]
    public async Task Lint_CleanWiki_ReturnsNoIssues()
    {
        var pages = new List<WikiPage>
        {
            new() { Title = "A", Content = "See [[B]]", WikiLinks = ["B"] },
            new() { Title = "B", Content = "See [[A]]", WikiLinks = ["A"] }
        };
        var indexEntries = pages.Select(p => new IndexEntry { PageName = p.Title }).ToList();
        var wikiRepo = new FakeWikiRepo(pages);
        var indexService = new FakeIndexService(indexEntries);
        var sut = new LintEngine(wikiRepo, indexService);

        var result = await sut.LintAsync();

        Assert.Empty(result.BrokenLinks);
        Assert.Empty(result.MissingIndex);
        Assert.Empty(result.Conflicts);
    }

    // --- Fakes ---

    private class FakeWikiRepo(List<WikiPage> pages) : IWikiRepository
    {
        public Task<IReadOnlyList<WikiPage>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WikiPage>>(pages);

        public Task<WikiPage?> GetByNameAsync(string name, CancellationToken ct = default) =>
            Task.FromResult(pages.FirstOrDefault(p => p.Title == name));

        public Task SaveAsync(WikiPage page, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(string name, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<IndexEntry>> GetIndexAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<IndexEntry>>([]);
        public Task<IReadOnlyList<LogEntry>> GetLogAsync(int? limit = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LogEntry>>([]);
    }

    private class FakeIndexService(List<IndexEntry> entries) : IIndexService
    {
        public Task<IReadOnlyList<IndexEntry>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<IndexEntry>>(entries);

        public Task<IReadOnlyList<IndexEntry>> SearchAsync(string keyword, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<IndexEntry>>(
                entries.Where(e => e.PageName.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList());

        public Task RebuildAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateAsync(IndexEntry entry, CancellationToken ct = default)
        {
            entries.RemoveAll(e => e.PageName == entry.PageName);
            entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string pageName, CancellationToken ct = default)
        {
            entries.RemoveAll(e => e.PageName == pageName);
            return Task.CompletedTask;
        }
    }
}
