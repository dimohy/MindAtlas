using System.Runtime.CompilerServices;
using GitHub.Copilot.SDK;
using MindAtlas.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace MindAtlas.Engine.Agent;

/// <summary>
/// Copilot SDK agent service — manages sessions with GPT-5 mini.
/// Uses schema/AGENTS.md as system prompt.
/// </summary>
public sealed class CopilotAgentService : ICopilotAgentService
{
    private readonly string _schemaPath;
    private readonly string? _githubToken;
    private readonly ILogger<CopilotAgentService>? _logger;
    private CopilotClient? _client;
    private CopilotSession? _session;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);

    public CopilotAgentService(string dataRoot, string? githubToken = null, ILogger<CopilotAgentService>? logger = null)
    {
        _schemaPath = Path.Combine(dataRoot, "schema", "AGENTS.md");
        _githubToken = githubToken;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_client is not null) return;

        var options = string.IsNullOrEmpty(_githubToken)
            ? null
            : new CopilotClientOptions { GitHubToken = _githubToken };

        _client = new CopilotClient(options);
        await _client.StartAsync(ct);
        _logger?.LogInformation("Copilot SDK client started");
    }

    public async Task<string> SendAsync(string prompt, CancellationToken ct = default)
    {
        var session = await GetOrCreateSessionAsync(ct);
        var result = await session.SendAndWaitAsync(
            new MessageOptions { Prompt = prompt },
            timeout: TimeSpan.FromMinutes(5),
            cancellationToken: ct);
        return result?.Data?.Content ?? string.Empty;
    }

    public async IAsyncEnumerable<string> SendStreamingAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var session = await GetOrCreateSessionAsync(ct);
        var channel = System.Threading.Channels.Channel.CreateUnbounded<string>();
        var writer = channel.Writer;

        using var sub = session.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageDeltaEvent delta:
                    writer.TryWrite(delta.Data.DeltaContent);
                    break;
                case SessionIdleEvent:
                    writer.TryComplete();
                    break;
            }
        });

        await session.SendAsync(new MessageOptions { Prompt = prompt }, ct);

        await foreach (var chunk in channel.Reader.ReadAllAsync(ct))
        {
            yield return chunk;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_session is not null)
            await _session.DisposeAsync();
        if (_client is not null)
            await _client.DisposeAsync();
        _sessionLock.Dispose();
    }

    // --- Private helpers ---

    private async Task<CopilotSession> GetOrCreateSessionAsync(CancellationToken ct)
    {
        if (_session is not null)
            return _session;

        await _sessionLock.WaitAsync(ct);
        try
        {
            if (_session is not null)
                return _session;

            if (_client is null)
                await InitializeAsync(ct);

            var schemaContent = File.Exists(_schemaPath)
                ? await File.ReadAllTextAsync(_schemaPath, ct)
                : string.Empty;

            _session = await _client!.CreateSessionAsync(new SessionConfig
            {
                Model = "gpt-5-mini",
                Streaming = true,
                SystemMessage = new SystemMessageConfig
                {
                    Mode = SystemMessageMode.Append,
                    Content = schemaContent
                },
                OnPermissionRequest = PermissionHandler.ApproveAll
            }, ct);

            _logger?.LogInformation("Copilot session created with gpt-5-mini");
            return _session;
        }
        finally
        {
            _sessionLock.Release();
        }
    }
}
