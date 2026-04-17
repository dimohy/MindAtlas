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
    /// After a query completes successfully (not cancelled / not error), the
    /// service exposes a save suggestion so the UI can offer "save to wiki".
    /// Null while streaming, after cancel/error, or after the user dismisses
    /// or saves the suggestion.
    /// </summary>
    public SaveSuggestion? PendingSuggestion { get; private set; }

    /// <summary>
    /// Transient status after a save attempt: "saved", "failed", or null.
    /// The UI clears this by calling <see cref="ClearSaveStatus"/>.
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
        StreamBuffer = "";
        IsStreaming = true;
        // A new query invalidates any outstanding suggestion / save status.
        PendingSuggestion = null;
        LastSaveStatus = null;
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
                {
                    var answer = StreamBuffer;
                    _messages.Add(new ChatMessage("assistant", answer));
                    // Offer a save-to-wiki suggestion for the completed answer.
                    if (!string.IsNullOrWhiteSpace(answer))
                        PendingSuggestion = new SaveSuggestion(
                            Title: BuildTitleFromQuestion(question),
                            Question: question,
                            AnswerMarkdown: answer);
                }
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

    /// <summary>
    /// Save the pending suggestion as a new wiki page via the ingest pipeline.
    /// The caller provides the final title (default is pre-filled from the
    /// question). Sets <see cref="LastSaveStatus"/> to reflect success/failure.
    /// </summary>
    public async Task SaveSuggestionAsync(string title, CancellationToken ct = default)
    {
        var suggestion = PendingSuggestion;
        if (suggestion is null) return;

        var finalTitle = string.IsNullOrWhiteSpace(title)
            ? suggestion.Title
            : title.Trim();

        // Compose a small markdown body that preserves the original question
        // so the ingested page is self-contained.
        var body = $"# {finalTitle}\n\n> {suggestion.Question}\n\n{suggestion.AnswerMarkdown}\n";

        try
        {
            var resp = await _http.PostAsJsonAsync(
                "api/ingest",
                new { title = finalTitle, content = body },
                ct);
            if (resp.IsSuccessStatusCode)
            {
                LastSaveStatus = new SaveStatus(true, finalTitle);
                PendingSuggestion = null;
            }
            else
            {
                LastSaveStatus = new SaveStatus(false, finalTitle);
            }
        }
        catch
        {
            LastSaveStatus = new SaveStatus(false, finalTitle);
        }
        Raise();
    }

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
    /// Build a wiki-friendly title from the user's question: trim, take the
    /// first sentence, and cap length. Public for testability.
    /// </summary>
    public static string BuildTitleFromQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question)) return "untitled";
        var trimmed = question.Trim();
        // First sentence boundary in Korean (. ? ! 。 ？ ！ newline).
        var cutoff = trimmed.IndexOfAny(['\n', '\r', '.', '?', '!', '。', '？', '！']);
        var head = cutoff > 0 ? trimmed[..cutoff] : trimmed;
        head = head.Trim();
        if (head.Length == 0) head = trimmed;
        const int MaxLen = 60;
        return head.Length <= MaxLen ? head : head[..MaxLen].TrimEnd();
    }

    private void Raise() => Changed?.Invoke();

    public sealed record ChatMessage(string Role, string Html);
    public sealed record SaveSuggestion(string Title, string Question, string AnswerMarkdown);
    public sealed record SaveStatus(bool Success, string Title);
}
