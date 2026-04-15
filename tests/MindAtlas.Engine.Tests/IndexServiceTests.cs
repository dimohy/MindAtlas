using MindAtlas.Core.Models;
using MindAtlas.Engine.Index;

namespace MindAtlas.Engine.Tests;

public class IndexServiceTests : IDisposable
{
    private readonly string _dataRoot;
    private readonly IndexService _sut;

    public IndexServiceTests()
    {
        _dataRoot = Path.Combine(Path.GetTempPath(), "mindatlas_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_dataRoot, "wiki"));
        _sut = new IndexService(_dataRoot);
    }

    [Fact]
    public async Task Search_MatchesPageName()
    {
        await WriteIndex("- **Quantum Computing** — Introduction to qubits #physics #cs");
        await _sut.RebuildAsync();

        var results = await _sut.SearchAsync("quantum");

        Assert.Single(results);
        Assert.Equal("Quantum Computing", results[0].PageName);
    }

    [Fact]
    public async Task Search_MatchesTags()
    {
        await WriteIndex("- **SomePage** — Content about AI #machine-learning #deep-learning");
        await _sut.RebuildAsync();

        var results = await _sut.SearchAsync("#machine-learning");

        Assert.Single(results);
        Assert.Equal("SomePage", results[0].PageName);
    }

    [Fact]
    public async Task Search_MatchesSummary()
    {
        await WriteIndex("- **Algorithms** — Sorting and searching fundamentals #cs");
        await _sut.RebuildAsync();

        var results = await _sut.SearchAsync("sorting");

        Assert.Single(results);
    }

    [Fact]
    public async Task Search_ReturnsEmpty_WhenNoMatch()
    {
        await WriteIndex("- **TestPage** — Something #test");
        await _sut.RebuildAsync();

        var results = await _sut.SearchAsync("nonexistent_xyz");

        Assert.Empty(results);
    }

    [Fact]
    public async Task Search_HandlesCaseInsensitive()
    {
        await WriteIndex("- **CSharp** — C# programming language #dotnet");
        await _sut.RebuildAsync();

        var results = await _sut.SearchAsync("CSHARP");

        Assert.Single(results);
    }

    [Fact]
    public async Task GetAll_ReturnsAllEntries()
    {
        await WriteIndex(
            "- **PageA** — First page #a\n- **PageB** — Second page #b\n- **PageC** — Third page #c");
        await _sut.RebuildAsync();

        var all = await _sut.GetAllAsync();

        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task Rebuild_PicksUpWikiFilesNotInIndex()
    {
        // Only wiki file, no index
        var wikiDir = Path.Combine(_dataRoot, "wiki");
        await File.WriteAllTextAsync(
            Path.Combine(wikiDir, "UnindexedPage.md"),
            "# UnindexedPage\n\n> A page not in index\n\nTags: #orphan\n\nSome content.");

        await _sut.RebuildAsync();
        var all = await _sut.GetAllAsync();

        Assert.Single(all);
        Assert.Equal("UnindexedPage", all[0].PageName);
    }

    [Fact]
    public async Task Search_Performance_Under50Ms_For1000Items()
    {
        // Generate index with 1000 entries
        var lines = Enumerable.Range(0, 1000)
            .Select(i => $"- **Page{i}** — Description for page number {i} about topic{i} #tag{i % 10}")
            .ToArray();
        await WriteIndex(string.Join("\n", lines));
        await _sut.RebuildAsync();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var results = await _sut.SearchAsync("topic500");
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 50, $"Search took {sw.ElapsedMilliseconds}ms (limit: 50ms)");
        Assert.NotEmpty(results);
    }

    private async Task WriteIndex(string content)
    {
        var indexPath = Path.Combine(_dataRoot, "wiki", "index.md");
        await File.WriteAllTextAsync(indexPath, $"# Wiki Index\n\n{content}\n");
    }

    public void Dispose()
    {
        _sut.Dispose();
        if (Directory.Exists(_dataRoot))
            Directory.Delete(_dataRoot, recursive: true);
    }
}
