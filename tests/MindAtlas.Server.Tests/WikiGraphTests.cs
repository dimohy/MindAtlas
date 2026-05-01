using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MindAtlas.Core.Interfaces;
using MindAtlas.Core.Models;
using MindAtlas.Server.Controllers;

namespace MindAtlas.Server.Tests;

public sealed class WikiGraphTests
{
    [Fact]
    public async Task GetGraph_ExtractsTypedRelationshipsFromWikiLinks()
    {
        var pages = new List<WikiPage>
        {
            new()
            {
                Title = "Current Analysis",
                Content = "This [[Previous Analysis|new result @supersedes the old result]] and [[Evidence @supports]].",
                WikiLinks = ["Previous Analysis", "Evidence"]
            },
            new() { Title = "Previous Analysis", Content = "Old result.", WikiLinks = [] },
            new() { Title = "Evidence", Content = "Supporting source.", WikiLinks = [] }
        };

        var controller = new WikiController(new FakeWikiRepository(pages), new FakeIndexService());

        var actionResult = await controller.GetGraph(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var json = JsonSerializer.Serialize(ok.Value);

        Assert.Contains("\"source\":\"Current Analysis\"", json);
        Assert.Contains("\"target\":\"Previous Analysis\"", json);
        Assert.Contains("\"type\":\"supersedes\"", json);
        Assert.Contains("\"target\":\"Evidence\"", json);
        Assert.Contains("\"type\":\"supports\"", json);
    }

    private sealed class FakeWikiRepository(IReadOnlyList<WikiPage> pages) : IWikiRepository
    {
        public Task<IReadOnlyList<WikiPage>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(pages);

        public Task<WikiPage?> GetByNameAsync(string pageName, CancellationToken ct = default)
            => Task.FromResult(pages.FirstOrDefault(p => string.Equals(p.Title, pageName, StringComparison.OrdinalIgnoreCase)));

        public Task SaveAsync(WikiPage page, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteAsync(string pageName, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<IndexEntry>> GetIndexAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<IndexEntry>>([]);

        public Task<IReadOnlyList<LogEntry>> GetLogAsync(int? limit = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LogEntry>>([]);
    }

    private sealed class FakeIndexService : IIndexService
    {
        public Task<IReadOnlyList<IndexEntry>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<IndexEntry>>([]);

        public Task<IReadOnlyList<IndexEntry>> SearchAsync(string keyword, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<IndexEntry>>([]);

        public Task RebuildAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateAsync(IndexEntry entry, CancellationToken ct = default) => Task.CompletedTask;

        public Task RemoveAsync(string pageName, CancellationToken ct = default) => Task.CompletedTask;
    }
}