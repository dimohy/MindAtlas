using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using MindAtlas.Core.Interfaces;
using MindAtlas.Core.Models;
using MindAtlas.Server.Controllers;
using MindAtlas.Server.Hubs;

namespace MindAtlas.Server.Tests;

public class QueryCancellationTests
{
    [Fact]
    public async Task Query_WhenCancelled_CallsAbortCurrentAsync()
    {
        var engine = new CancellingWikiEngine();
        var agent = new TrackingCopilotAgent();

        var controller = new EngineController(
            engine,
            NullRawRepository.Instance,
            agent,
            NullHubContext<WikiHub>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    // Allow Response.Body writes to go somewhere.
                    Response = { Body = new MemoryStream() }
                }
            }
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await controller.Query(new QueryRequest("q"), cts.Token);

        Assert.True(agent.AbortCalled, "AbortCurrentAsync should be invoked on cancellation");
    }

    // --- Test doubles ---

    private sealed class CancellingWikiEngine : IWikiEngine
    {
        public Task<IReadOnlyList<string>> IngestAsync(string rawFilePath, CancellationToken ct) => throw new NotImplementedException();
        public Task<QueryResult> QueryAsync(string question, CancellationToken ct) => throw new NotImplementedException();
        public Task<LintResult> LintAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task<int> LintFixAsync(CancellationToken ct) => throw new NotImplementedException();

        public async IAsyncEnumerable<string> QueryStreamingAsync(
            string question,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            yield break;
        }

        public IAsyncEnumerable<string> QueryStreamingAsync(
            string question,
            bool useWebSearch,
            CancellationToken ct = default)
            => QueryStreamingAsync(question, ct);
    }

    private sealed class TrackingCopilotAgent : ICopilotAgentService
    {
        public bool AbortCalled { get; private set; }
        public Task AbortCurrentAsync(CancellationToken ct = default) { AbortCalled = true; return Task.CompletedTask; }
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task ReloadTokenAsync(string? newToken, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string> SendAsync(string prompt, CancellationToken ct = default) => Task.FromResult("");
        public async IAsyncEnumerable<string> SendStreamingAsync(string prompt, [EnumeratorCancellation] CancellationToken ct = default)
        { await Task.Yield(); yield break; }
        public IAsyncEnumerable<string> SendStreamingAsync(string prompt, bool useWebSearch, CancellationToken ct = default)
            => SendStreamingAsync(prompt, ct);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class NullRawRepository : IRawRepository
    {
        public static readonly NullRawRepository Instance = new();
        public Task<IReadOnlyList<RawSource>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RawSource>>(Array.Empty<RawSource>());
        public Task<IReadOnlyList<RawSource>> GetUnprocessedAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RawSource>>(Array.Empty<RawSource>());
        public Task<RawSource?> GetByNameAsync(string fileName, CancellationToken ct = default) => Task.FromResult<RawSource?>(null);
        public Task SaveAsync(string fileName, Stream content, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateStatusAsync(string fileName, ProcessingStatus status, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateStatusAsync(string fileName, ProcessingStatus status, string? errorMessage, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> TrySetProcessingAsync(string fileName, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class NullHubContext<T> : IHubContext<T> where T : Hub
    {
        public static readonly NullHubContext<T> Instance = new();
        public IHubClients Clients { get; } = new NullHubClients();
        public IGroupManager Groups { get; } = new NullGroupManager();

        private sealed class NullHubClients : IHubClients
        {
            public IClientProxy All { get; } = new NullClientProxy();
            public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => All;
            public IClientProxy Client(string connectionId) => All;
            public IClientProxy Clients(IReadOnlyList<string> connectionIds) => All;
            public IClientProxy Group(string groupName) => All;
            public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => All;
            public IClientProxy Groups(IReadOnlyList<string> groupNames) => All;
            public IClientProxy User(string userId) => All;
            public IClientProxy Users(IReadOnlyList<string> userIds) => All;
        }

        private sealed class NullClientProxy : IClientProxy
        {
            public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        private sealed class NullGroupManager : IGroupManager
        {
            public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }
}
