namespace MindAtlas.Core.Interfaces;

/// <summary>
/// Service for interacting with the Copilot SDK agent (GPT-5 mini).
/// </summary>
public interface ICopilotAgentService : IAsyncDisposable
{
    Task<string> SendAsync(string prompt, CancellationToken ct = default);
    IAsyncEnumerable<string> SendStreamingAsync(string prompt, CancellationToken ct = default);
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Hot-swap the GitHub token at runtime. Tears down the current client
    /// and session so the next request reconnects with the new token, with
    /// no app restart required.
    /// </summary>
    Task ReloadTokenAsync(string? newToken, CancellationToken ct = default);

    /// <summary>
    /// Abort the in-flight Copilot session request (if any). No-op when no
    /// session is active. Safe to call from a cancellation handler.
    /// </summary>
    Task AbortCurrentAsync(CancellationToken ct = default);
}
