using System.Globalization;
using System.Text;

namespace MindAtlas.Engine.Query;

/// <summary>
/// Lightweight tokenization + Jaccard similarity helpers for the
/// wiki-coverage check (§8.3). Intentionally language-agnostic: treats any
/// letter/digit run (Unicode) as a single token so Korean, English, and
/// numbers coexist without extra rules.
/// </summary>
public static class TokenUtil
{
    private const int MinTokenLength = 2;

    /// <summary>
    /// Lower-case, Unicode-aware tokenizer. Returns a set of distinct
    /// tokens with length >= 2. Punctuation and whitespace are separators.
    /// </summary>
    public static HashSet<string> TokenizeAndNormalize(string? text)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(text)) return result;

        var buf = new System.Text.StringBuilder(capacity: 32);
        foreach (var rune in text.EnumerateRunes())
        {
            var cat = Rune.GetUnicodeCategory(rune);
            var isWord = cat switch
            {
                UnicodeCategory.UppercaseLetter => true,
                UnicodeCategory.LowercaseLetter => true,
                UnicodeCategory.TitlecaseLetter => true,
                UnicodeCategory.OtherLetter => true, // Hangul, CJK, etc.
                UnicodeCategory.ModifierLetter => true,
                UnicodeCategory.DecimalDigitNumber => true,
                _ => false,
            };

            if (isWord)
            {
                // Lowercase-invariant append; for OtherLetter this is a no-op.
                foreach (var r in rune.ToString().ToLowerInvariant())
                    buf.Append(r);
            }
            else if (buf.Length > 0)
            {
                Flush(buf, result);
            }
        }
        if (buf.Length > 0) Flush(buf, result);
        return result;

        static void Flush(System.Text.StringBuilder b, HashSet<string> acc)
        {
            if (b.Length >= MinTokenLength) acc.Add(b.ToString());
            b.Clear();
        }
    }

    /// <summary>
    /// Jaccard similarity: |A ∩ B| / |A ∪ B|. Returns 0.0 when both sets
    /// are empty (treated as "no overlap" for coverage purposes).
    /// </summary>
    public static double ComputeJaccard(HashSet<string> a, HashSet<string> b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.Count == 0 && b.Count == 0) return 0.0;

        int intersection = 0;
        // Iterate smaller set for speed.
        var (small, large) = a.Count <= b.Count ? (a, b) : (b, a);
        foreach (var token in small)
            if (large.Contains(token)) intersection++;

        int union = a.Count + b.Count - intersection;
        return union == 0 ? 0.0 : (double)intersection / union;
    }
}
