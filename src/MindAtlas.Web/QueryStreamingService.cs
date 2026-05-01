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
    // Remembered for the in-flight / most recent query so we can forward
    // {question, answer, title} to POST /api/wiki/save-from-answer.
    private string? _lastQuestion;
    private string? _lastAnswer;

    public QueryStreamingService(HttpClient http) => _http = http;

    public IReadOnlyList<ChatMessage> Messages => _messages;
    public string StreamBuffer { get; private set; } = "";
    public bool IsStreaming { get; private set; }

    /// <summary>
    /// Set by the server's <c>event: wiki-suggestion</c> SSE frame after a
    /// successful answer when the coverage check decided the topic isn't
    /// covered by the existing wiki. Cleared on dismiss / save / new query.
    /// </summary>
    public SaveSuggestion? PendingSuggestion { get; private set; }

    /// <summary>
    /// Transient status shown after a save completes (manual confirm or
    /// server-side auto-save). UI clears via <see cref="ClearSaveStatus"/>.
    /// </summary>
    public SaveStatus? LastSaveStatus { get; private set; }

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
        PendingSuggestion = null;
        LastSaveStatus = null;
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
        PendingSuggestion = null;
        LastSaveStatus = null;
        _lastQuestion = question;
        _lastAnswer = null;
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
                            else if (eventType == "wiki-suggestion")
                            {
                                TryApplySuggestion(payload);
                            }
                            else if (eventType == "wiki-saved")
                            {
                                TryApplySaved(payload);
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
                _lastAnswer = StreamBuffer;
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

    /// <summary>Dismiss the current save suggestion without saving.</summary>
    public void DismissSuggestion()
    {
        if (PendingSuggestion is null) return;
        PendingSuggestion = null;
        Raise();
    }

    public void ClearSaveStatus()
    {
        if (LastSaveStatus is null) return;
        LastSaveStatus = null;
        Raise();
    }

    /// <summary>
    /// POST /api/wiki/save-from-answer with the user-confirmed title.
    /// On success, clears <see cref="PendingSuggestion"/> and sets
    /// <see cref="LastSaveStatus"/>.
    /// </summary>
    public async Task<bool> ConfirmSaveAsync(string title, string? category)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        if (string.IsNullOrEmpty(_lastQuestion) || string.IsNullOrEmpty(_lastAnswer)) return false;

        try
        {
            var resp = await _http.PostAsJsonAsync("api/wiki/save-from-answer", new
            {
                question = _lastQuestion,
                answer = _lastAnswer,
                title = title.Trim(),
                category,
            });
            if (!resp.IsSuccessStatusCode)
            {
                LastSaveStatus = new SaveStatus(false, null);
                Raise();
                return false;
            }
            var result = await resp.Content.ReadFromJsonAsync<SaveResponse>();
            PendingSuggestion = null;
            LastSaveStatus = new SaveStatus(true, result?.PageName ?? title);
            Raise();
            return true;
        }
        catch
        {
            LastSaveStatus = new SaveStatus(false, null);
            Raise();
            return false;
        }
    }

    private void TryApplySuggestion(string json)
    {
        try
        {
            var dto = System.Text.Json.JsonSerializer.Deserialize<SuggestionDto>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dto is null || string.IsNullOrWhiteSpace(dto.Title)) return;
            PendingSuggestion = new SaveSuggestion(dto.Title!, dto.Category);
            Raise();
        }
        catch { /* malformed server payload — ignore */ }
    }

    private void TryApplySaved(string json)
    {
        try
        {
            var dto = System.Text.Json.JsonSerializer.Deserialize<SaveResponse>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            PendingSuggestion = null;
            LastSaveStatus = new SaveStatus(true, dto?.PageName);
            Raise();
        }
        catch { /* ignore */ }
    }

    private void Raise() => Changed?.Invoke();

    public sealed record ChatMessage(string Role, string Html);
    public sealed record SaveSuggestion(string Title, string? Category);
    public sealed record SaveStatus(bool Success, string? PageName);
    private sealed record SuggestionDto(string? Title, string? Category);
    private sealed record SaveResponse(string? PageName);
}
