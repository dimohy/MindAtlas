using MindAtlas.Core.Models;
using MindAtlas.Engine.Repository;

namespace MindAtlas.Engine.Tests;

public class WikiRepositoryTests : IDisposable
{
    private readonly string _dataRoot;
    private readonly WikiRepository _repo;

    public WikiRepositoryTests()
    {
        _dataRoot = Path.Combine(Path.GetTempPath(), "mindatlas_test_" + Guid.NewGuid().ToString("N")[..8]);
        _repo = new WikiRepository(_dataRoot);
    }

    [Fact]
    public async Task SaveAndGetByName_RoundTrips()
    {
        var page = new WikiPage
        {
            Title = "TestPage",
            Summary = "A test page",
            Content = "Some content here",
            Tags = ["#test", "#unit"],
            WikiLinks = ["OtherPage"]
        };

        await _repo.SaveAsync(page);
        var loaded = await _repo.GetByNameAsync("TestPage");

        Assert.NotNull(loaded);
        Assert.Equal("TestPage", loaded.Title);
        Assert.Equal("A test page", loaded.Summary);
        Assert.Contains("#test", loaded.Tags);
        Assert.Contains("OtherPage", loaded.WikiLinks);
    }

    [Fact]
    public async Task GetAll_ReturnsAllPages()
    {
        await _repo.SaveAsync(new WikiPage { Title = "Page1", Content = "Content1" });
        await _repo.SaveAsync(new WikiPage { Title = "Page2", Content = "Content2" });

        var all = await _repo.GetAllAsync();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task Delete_RemovesPage()
    {
        await _repo.SaveAsync(new WikiPage { Title = "ToDelete", Content = "..." });
        await _repo.DeleteAsync("ToDelete");

        var result = await _repo.GetByNameAsync("ToDelete");
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateIndex_AddsAndUpdatesEntries()
    {
        await _repo.UpdateIndexAsync(new IndexEntry
        {
            PageName = "IndexedPage",
            Summary = "First version",
            Tags = ["#v1"]
        });

        var index = await _repo.GetIndexAsync();
        Assert.Single(index);
        Assert.Equal("IndexedPage", index[0].PageName);

        // Update
        await _repo.UpdateIndexAsync(new IndexEntry
        {
            PageName = "IndexedPage",
            Summary = "Updated version",
            Tags = ["#v2"]
        });

        index = await _repo.GetIndexAsync();
        Assert.Single(index);
        Assert.Equal("Updated version", index[0].Summary);
    }

    [Fact]
    public async Task AppendLog_WritesEntries()
    {
        await _repo.AppendLogAsync(new LogEntry
        {
            Operation = OperationType.Ingest,
            Description = "Ingested file.txt",
            AffectedPages = ["NewPage"]
        });

        var log = await _repo.GetLogAsync();
        Assert.Single(log);
        Assert.Equal(OperationType.Ingest, log[0].Operation);
        Assert.Contains("NewPage", log[0].AffectedPages);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataRoot))
            Directory.Delete(_dataRoot, recursive: true);
    }
}
