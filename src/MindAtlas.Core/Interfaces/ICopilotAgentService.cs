namespace MindAtlas.Core.Interfaces;

/// <summary>
/// Service for interacting with the Copilot SDK agent (GPT-5 mini).
/// </summary>
public interface ICopilotAgentService : IAsyncDisposable
{
    Task<string> SendAsync(string prompt, CancellationToken ct = default);
    IAsyncEnumerable<string> SendStreamingAsync(string prompt, CancellationToken ct = default);
    Task InitializeAsync(CancellationToken ct = default);
}
