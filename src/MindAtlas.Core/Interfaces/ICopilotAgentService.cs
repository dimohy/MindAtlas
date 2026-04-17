namespace MindAtlas.Core.Interfaces;

/// <summary>
/// Service for interacting with the Copilot SDK agent (GPT-5 mini).
/// </summary>
public interface ICopilotAgentService : IAsyncDisposable
{
    Task<string> SendAsync(string prompt, CancellationToken ct = default);
    IAsyncEnumerable<string> SendStreamingAsync(string prompt, CancellationToken ct = default);

    /// <summary>
    /// Streaming send that opts in to the Copilot CLI built-in web fetch/URL
    /// tools for this request only. Permission for <c>url</c> requests is
    /// approved when <paramref name="useWebSearch"/> is true and denied by
    /// rules otherwise. Other permission kinds follow the default policy.
    /// </summary>
    IAsyncEnumerable<string> SendStreamingAsync(string prompt, bool useWebSearch, CancellationToken ct = default);

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
