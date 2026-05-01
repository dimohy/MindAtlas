namespace MindAtlas.Core.Models;

/// <summary>
/// Result of a wiki-coverage check for a completed answer. Used by the
/// AI-query pipeline to decide whether to suggest saving the answer as a
/// new wiki page.
/// </summary>
/// <param name="NeedsSave">
/// True when the question is not well-covered by existing wiki pages
/// (keyword hit count is 0, or the best Jaccard similarity between the
/// question tokens and any matching page's keywords is below 0.5).
/// </param>
/// <param name="SuggestedTitle">
/// LLM-suggested Korean title (max 30 chars). Non-null only when
/// <see cref="NeedsSave"/> is true.
/// </param>
/// <param name="SuggestedCategory">
/// Reserved for future categorization. Currently always null; kept on the
/// record so callers can start consuming it without a breaking change.
/// </param>
public sealed record CoverageResult(
    bool NeedsSave,
    string? SuggestedTitle,
    string? SuggestedCategory);
