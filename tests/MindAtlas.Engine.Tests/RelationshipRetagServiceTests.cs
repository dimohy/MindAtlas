using MindAtlas.Core.Interfaces;
using MindAtlas.Core.Models;
using MindAtlas.Engine.Maintenance;

namespace MindAtlas.Engine.Tests;

public sealed class RelationshipRetagServiceTests
{
    [Fact]
    public async Task ProposeAsync_CreatesHighConfidenceTypedRelationshipCandidates()
    {
        var pages = new List<WikiPage>
        {
            new()
            {
                Title = "Current Analysis",
                Content = "This evidence strongly supports [[Evidence Page]]. It contradicts [[Old Claim]].",
                WikiLinks = ["Evidence Page", "Old Claim"]
            },
            new() { Title = "Evidence Page", Content = "Evidence.", WikiLinks = [] },
            new() { Title = "Old Claim", Content = "Old claim.", WikiLinks = [] }
        };
        var sut = new RelationshipRetagService(new FakeWikiRepository(pages));

        var result = await sut.ProposeAsync();

        Assert.Equal(2, result.Proposals.Count);
        Assert.Contains(result.Proposals, proposal =>
            proposal.TargetTitle == "Evidence Page"
            && proposal.RelationshipType == "supports"
            && proposal.Confidence == "high"
            && proposal.ProposedLink == "[[Evidence Page @supports]]");
        Assert.Contains(result.Proposals, proposal =>
            proposal.TargetTitle == "Old Claim"
            && proposal.RelationshipType == "contradicts"
            && proposal.Confidence == "high"
            && proposal.ProposedLink == "[[Old Claim @contradicts]]");
    }

    [Fact]
    public async Task ProposeAsync_SkipsAmbiguousTypedAndCodeBlockLinks()
    {
        var pages = new List<WikiPage>
        {
            new()
            {
                Title = "Notes",
                Content = """
                    ```markdown
                    [[Example Page]]
                    ```
                    See [[Already Typed @supports]]. Also see [[Ambiguous Page]].
                    """,
                WikiLinks = ["Example Page", "Already Typed", "Ambiguous Page"]
            },
            new() { Title = "Example Page", Content = "Example.", WikiLinks = [] },
            new() { Title = "Already Typed", Content = "Typed.", WikiLinks = [] },
            new() { Title = "Ambiguous Page", Content = "Ambiguous.", WikiLinks = [] }
        };
        var sut = new RelationshipRetagService(new FakeWikiRepository(pages));

        var result = await sut.ProposeAsync();

        Assert.Empty(result.Proposals);
    }

    [Fact]
    public async Task ApplyAsync_AppliesOnlyRequestedConfidenceAndRefreshesWikiLinks()
    {
        var pages = new List<WikiPage>
        {
            new()
            {
                Title = "Current Analysis",
                Content = "This evidence supports [[Evidence Page|the evidence]]. See [[Reference Page]].",
                WikiLinks = ["Evidence Page", "Reference Page"]
            },
            new() { Title = "Evidence Page", Content = "Evidence.", WikiLinks = [] },
            new() { Title = "Reference Page", Content = "Reference.", WikiLinks = [] }
        };
        var repo = new FakeWikiRepository(pages);
        var sut = new RelationshipRetagService(repo);

        var result = await sut.ApplyAsync("high");

        Assert.Equal(1, result.AppliedCount);
        Assert.Contains("[[Evidence Page|the evidence @supports]]", pages[0].Content);
        Assert.Contains("[[Reference Page]]", pages[0].Content);
        Assert.Equal(["Evidence Page", "Reference Page"], pages[0].WikiLinks);
        Assert.Single(repo.SavedPages);
    }

    [Fact]
    public async Task ApplySelectedAsync_AppliesOnlySelectedProposals()
    {
        var pages = new List<WikiPage>
        {
            new()
            {
                Title = "Current Analysis",
                Content = "This evidence supports [[Evidence Page]]. It contradicts [[Old Claim]].",
                WikiLinks = ["Evidence Page", "Old Claim"]
            },
            new() { Title = "Evidence Page", Content = "Evidence.", WikiLinks = [] },
            new() { Title = "Old Claim", Content = "Old claim.", WikiLinks = [] }
        };
        var repo = new FakeWikiRepository(pages);
        var sut = new RelationshipRetagService(repo);
        var proposals = (await sut.ProposeAsync()).Proposals;
        var selected = proposals
            .Where(proposal => proposal.TargetTitle == "Old Claim")
            .Select(proposal => new RelationshipRetagSelection
            {
                PageTitle = proposal.PageTitle,
                OriginalLink = proposal.OriginalLink,
                TargetTitle = proposal.TargetTitle,
                RelationshipType = proposal.RelationshipType
            })
            .ToList();

        var result = await sut.ApplySelectedAsync(selected);

        Assert.Equal(1, result.AppliedCount);
        Assert.Contains("[[Evidence Page]]", pages[0].Content);
        Assert.Contains("[[Old Claim @contradicts]]", pages[0].Content);
        Assert.Equal(["Evidence Page", "Old Claim"], pages[0].WikiLinks);
        Assert.Single(repo.SavedPages);
    }

    private sealed class FakeWikiRepository(List<WikiPage> pages) : IWikiRepository
    {
        public List<WikiPage> SavedPages { get; } = [];

        public Task<IReadOnlyList<WikiPage>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WikiPage>>(pages);

        public Task<WikiPage?> GetByNameAsync(string pageName, CancellationToken ct = default)
            => Task.FromResult(pages.FirstOrDefault(page => string.Equals(page.Title, pageName, StringComparison.OrdinalIgnoreCase)));

        public Task SaveAsync(WikiPage page, CancellationToken ct = default)
        {
            SavedPages.Add(page);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string pageName, CancellationToken ct = default)
        {
            pages.RemoveAll(page => string.Equals(page.Title, pageName, StringComparison.OrdinalIgnoreCase));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<IndexEntry>> GetIndexAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<IndexEntry>>([]);

        public Task<IReadOnlyList<LogEntry>> GetLogAsync(int? limit = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LogEntry>>([]);
    }
}
