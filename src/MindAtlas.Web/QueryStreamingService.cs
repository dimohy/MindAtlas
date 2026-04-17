using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MindAtlas.Web;

/// <summary>
/// Runs the AI query SSE stream as a background task that survives page
/// navigation. The Query page subscribes to <see cref="Changed"/> to rerender
/// while the stream is still active.
/// </summary>
public sealed class QueryStreamingService
{
    private readonly HttpClient _http;
    private readonly List<ChatMessage> _messages = [];
    private CancellationTokenSource? _cts;

    public QueryStreamingService(HttpClient http) => _http = http;

    public IReadOnlyList<ChatMessage> Messages => _messages;
    public string StreamBuffer { get; private set; } = "";
    public bool IsStreaming { get; private set; }

    /// <summary>
    /// Persisted across navigations: whether the user has enabled the web
    /// search (Copilot CLI url/fetch tools) for their next query. The Query
    /// page binds its checkbox to this field and initializes the default
    /// from the server's <c>WebSearchDefaultEnabled</c> setting.
    /// </summary>
    public bool UseWebSearch { get; set; }

    /// <summary>
    /// True once the Query page has seeded <see cref="UseWebSearch"/> from
    /// the server settings. Prevents the page-init code from clobbering a
    /// later user toggle on subsequent navigations.
    /// </summary>
    public bool HasUseWebSearchOverride { get; set; }

    /// <summary>Fires whenever the streaming state or buffer changes.</summary>
    public event Action? Changed;

    public void ResetHistory()
    {
        _messages.Clear();
        StreamBuffer = "";
        Raise();
    }

    public void SeedHistory(IEnumerable<ChatMessage> messages)
    {
        // Only seed when we have nothing yet, to avoid clobbering an active
        // stream's messages on subsequent navigations.
        if (_messages.Count > 0 || IsStreaming) return;
        _messages.AddRange(messages);
    }

    public async Task SendAsync(string question, string errorResponseText, string errorConnectionText, string cancelledText)
    {
        if (IsStreaming || string.IsNullOrWhiteSpace(question)) return;

        _messages.Add(new ChatMessage("user", question));
        StreamBuffer = "";
        IsStreaming = true;
        Raise();

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "api/query")
                {
                    Content = JsonContent.Create(new { question, useWebSearch = UseWebSearch })
                };
                var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                if (!response.IsSuccessStatusCode)
                {
                    _messages.Add(new ChatMessage("assistant", $"<em>{errorResponseText}</em>"));
                    return;
                }

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var reader = new System.IO.StreamReader(stream);

                var dataBuf = new StringBuilder();
                string? errorMsg = null;
                bool wasCancelled = false;
                string? eventType = null;
                while (!ct.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(ct);
                    if (line is null) break;

                    if (line.Length == 0)
                    {
                        if (dataBuf.Length > 0)
                        {
                            var payload = dataBuf.ToString();
                            if (payload == "[DONE]")
                            {
                                dataBuf.Clear();
                                eventType = null;
                                break;
                            }
                            if (eventType == "error")
                            {
                                errorMsg = payload;
                            }
                            else if (eventType == "cancelled")
                            {
                                wasCancelled = true;
                            }
                            else
                            {
                                StreamBuffer += payload;
                                Raise();
                            }
                            dataBuf.Clear();
                            eventType = null;
                        }
                        continue;
                    }

                    if (line.StartsWith("event: "))
                        eventType = line[7..];
                    else if (line.StartsWith("data: "))
                    {
                        if (dataBuf.Length > 0) dataBuf.Append('\n');
                        dataBuf.Append(line[6..]);
                    }
                }

                if (errorMsg is not null)
                    _messages.Add(new ChatMessage("assistant",
                        $"<em>⚠ {System.Net.WebUtility.HtmlEncode(errorMsg)}</em>"));
                else if (wasCancelled)
                {
                    // Preserve any partial buffer, then append a system-style note.
                    if (!string.IsNullOrEmpty(StreamBuffer))
                        _messages.Add(new ChatMessage("assistant", StreamBuffer));
                    _messages.Add(new ChatMessage("system", $"<em>{cancelledText}</em>"));
                }
                else
                    _messages.Add(new ChatMessage("assistant", StreamBuffer));
            }
            catch (OperationCanceledException)
            {
                // User cancelled locally — server may not get a chance to emit
                // event: cancelled before the connection drops, so synthesize
                // the note here too.
                if (!string.IsNullOrEmpty(StreamBuffer))
                    _messages.Add(new ChatMessage("assistant", StreamBuffer));
                _messages.Add(new ChatMessage("system", $"<em>{cancelledText}</em>"));
            }
            catch
            {
                _messages.Add(new ChatMessage("assistant", $"<em>{errorConnectionText}</em>"));
            }
            finally
            {
                StreamBuffer = "";
                IsStreaming = false;
                Raise();
            }
        }, ct);

        await Task.CompletedTask;
    }

    public void Cancel()
    {
        _cts?.Cancel();
    }

    private void Raise() => Changed?.Invoke();

    public sealed record ChatMessage(string Role, string Html);
}
