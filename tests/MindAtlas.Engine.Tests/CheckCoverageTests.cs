using System.Runtime.CompilerServices;
using MindAtlas.Core.Interfaces;
using MindAtlas.Core.Models;
using MindAtlas.Engine.Query;
using MindAtlas.Engine.Repository;
using Xunit;

namespace MindAtlas.Engine.Tests;

public sealed class CheckCoverageTests
{
    [Fact]
    public async Task NoIndexHits_Returns_NeedsSave()
    {
        var engine = CreateEngine(
            index: new FakeIndex([]),
            agent: new FakeAgent("제안된 제목"));

        var result = await engine.CheckCoverageAsync(
            "What is NativeAOT?",
            "NativeAOT is ahead-of-time compilation for .NET.");

        Assert.True(result.NeedsSave);
        Assert.Equal("제안된 제목", result.SuggestedTitle);
    }

    [Fact]
    public async Task EmptyQuestionOrAnswer_Returns_NoSave()
    {
        var engine = CreateEngine(
            index: new FakeIndex([]),
            agent: new FakeAgent("irrelevant"));

        var empty = await engine.CheckCoverageAsync("", "answer");
        var noAnswer = await engine.CheckCoverageAsync("q", "");

        Assert.False(empty.NeedsSave);
        Assert.False(noAnswer.NeedsSave);
    }

    [Fact]
    public async Task HighJaccardOverlap_Returns_NoSave()
    {
        // Question tokens fully covered by an existing page's keywords.
        var entry = new IndexEntry
        {
            PageName = "NativeAOT",
            Summary = "",
            Tags = [],
            Keywords = ["nativeaot", "ahead", "time", "compilation", "dotnet"],
        };
        var engine = CreateEngine(
            index: new FakeIndex([entry]),
            agent: new FakeAgent("should-not-be-used"));

        var result = await engine.CheckCoverageAsync(
            "What is NativeAOT ahead of time compilation in dotnet?",
            "An answer that doesn't matter here.");

        Assert.False(result.NeedsSave);
        Assert.Null(result.SuggestedTitle);
    }

    [Fact]
    public async Task LowJaccardOverlap_Returns_NeedsSave()
    {
        var entry = new IndexEntry
        {
            PageName = "Cooking",
            Summary = "",
            Tags = [],
            Keywords = ["recipe", "kitchen"],
        };
        var engine = CreateEngine(
            index: new FakeIndex([entry]),
            agent: new FakeAgent("Quantum Answer"));

        var result = await engine.CheckCoverageAsync(
            "Explain quantum entanglement",
            "Quantum entanglement is a physical phenomenon.");

        Assert.True(result.NeedsSave);
        Assert.Equal("Quantum Answer", result.SuggestedTitle);
    }

    [Fact]
    public async Task LlmFailure_Fallback_To_QuestionDerivedTitle()
    {
        var engine = CreateEngine(
            index: new FakeIndex([]),
            agent: new ThrowingAgent());

        var result = await engine.CheckCoverageAsync(
            "Explain quantum entanglement briefly",
            "Some answer.");

        Assert.True(result.NeedsSave);
        Assert.False(string.IsNullOrWhiteSpace(result.SuggestedTitle));
        // Fallback takes first sentence of question, <= 30 chars.
        Assert.True(result.SuggestedTitle!.Length <= 30);
    }

    // --- helpers ---

    private static QueryEngine CreateEngine(FakeIndex index, ICopilotAgentService agent)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "mindatlas-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "wiki"));
        var repo = new WikiRepository(tempRoot);
        return new QueryEngine(
            indexService: index,
            wikiRepo: repo,
            wikiRepoImpl: repo,
            agent: agent);
    }

    private sealed class FakeIndex(IReadOnlyList<IndexEntry> entries) : IIndexService
    {
        public Task<IReadOnlyList<IndexEntry>> SearchAsync(string keyword, CancellationToken ct = default)
            => Task.FromResult(entries);
        public Task RebuildAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<IndexEntry>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult(entries);
        public Task UpdateAsync(IndexEntry entry, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAsync(string pageName, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeAgent(string response) : ICopilotAgentService
    {
        public Task AbortCurrentAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task ReloadTokenAsync(string? newToken, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string> SendAsync(string prompt, CancellationToken ct = default) => Task.FromResult(response);
        public async IAsyncEnumerable<string> SendStreamingAsync(string prompt, [EnumeratorCancellation] CancellationToken ct = default)
        { await Task.Yield(); yield break; }
        public IAsyncEnumerable<string> SendStreamingAsync(string prompt, bool useWebSearch, CancellationToken ct = default)
            => SendStreamingAsync(prompt, ct);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ThrowingAgent : ICopilotAgentService
    {
        public Task AbortCurrentAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task ReloadTokenAsync(string? newToken, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string> SendAsync(string prompt, CancellationToken ct = default)
            => throw new InvalidOperationException("LLM unavailable");
        public async IAsyncEnumerable<string> SendStreamingAsync(string prompt, [EnumeratorCancellation] CancellationToken ct = default)
        { await Task.Yield(); yield break; }
        public IAsyncEnumerable<string> SendStreamingAsync(string prompt, bool useWebSearch, CancellationToken ct = default)
            => SendStreamingAsync(prompt, ct);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
