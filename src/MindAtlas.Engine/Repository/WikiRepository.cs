using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MindAtlas.Core.Interfaces;
using MindAtlas.Core.Models;

namespace MindAtlas.Engine.Repository;

/// <summary>
/// File-based wiki repository — reads/writes markdown files from wiki/ directory.
/// </summary>
public sealed partial class WikiRepository : IWikiRepository
{
    private readonly string _wikiDir;
    private readonly string _indexPath;
    private readonly string _logPath;

    public WikiRepository(string dataRoot)
    {
        _wikiDir = Path.Combine(dataRoot, "wiki");
        _indexPath = Path.Combine(_wikiDir, "index.md");
        _logPath = Path.Combine(_wikiDir, "log.md");
        Directory.CreateDirectory(_wikiDir);
    }

    public async Task<IReadOnlyList<WikiPage>> GetAllAsync(CancellationToken ct = default)
    {
        var pages = new List<WikiPage>();
        var files = Directory.GetFiles(_wikiDir, "*.md")
            .Where(f => !IsSpecialFile(f));

        foreach (var file in files)
        {
            var page = await ParseWikiFileAsync(file, ct);
            if (page is not null)
                pages.Add(page);
        }

        return pages;
    }

    public async Task<WikiPage?> GetByNameAsync(string pageName, CancellationToken ct = default)
    {
        var filePath = GetPageFilePath(pageName);
        if (!File.Exists(filePath))
            return null;

        return await ParseWikiFileAsync(filePath, ct);
    }

    public async Task SaveAsync(WikiPage page, CancellationToken ct = default)
    {
        var filePath = GetPageFilePath(page.Title);
        var content = FormatWikiPage(page);
        await File.WriteAllTextAsync(filePath, content, Encoding.UTF8, ct);
    }

    public Task DeleteAsync(string pageName, CancellationToken ct = default)
    {
        var filePath = GetPageFilePath(pageName);
        if (File.Exists(filePath))
            File.Delete(filePath);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<IndexEntry>> GetIndexAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_indexPath))
            return [];

        var content = await File.ReadAllTextAsync(_indexPath, ct);
        return ParseIndex(content);
    }

    public async Task<IReadOnlyList<LogEntry>> GetLogAsync(int? limit = null, CancellationToken ct = default)
    {
        if (!File.Exists(_logPath))
            return [];

        var lines = await File.ReadAllLinesAsync(_logPath, ct);
        var entries = ParseLog(lines);
        return limit.HasValue ? entries.TakeLast(limit.Value).ToList() : entries;
    }

    public async Task UpdateIndexAsync(IndexEntry entry, CancellationToken ct = default)
    {
        var entries = (await GetIndexAsync(ct)).ToList();
        var existing = entries.FindIndex(e =>
            string.Equals(e.PageName, entry.PageName, StringComparison.OrdinalIgnoreCase));

        if (existing >= 0)
            entries[existing] = entry;
        else
            entries.Add(entry);

        await WriteIndexAsync(entries, ct);
    }

    public async Task AppendLogAsync(LogEntry entry, CancellationToken ct = default)
    {
        var line = FormatLogEntry(entry);
        await File.AppendAllTextAsync(_logPath, line + Environment.NewLine, Encoding.UTF8, ct);
    }

    // --- Private helpers ---

    private string GetPageFilePath(string pageName)
        => Path.Combine(_wikiDir, $"{SanitizeFileName(pageName)}.md");

    private static bool IsSpecialFile(string filePath)
    {
        var name = Path.GetFileName(filePath);
        return name is "index.md" or "log.md";
    }

    private static string SanitizeFileName(string name)
        => InvalidCharsRegex().Replace(name, "_").Trim();

    private async Task<WikiPage?> ParseWikiFileAsync(string filePath, CancellationToken ct)
    {
        var content = await File.ReadAllTextAsync(filePath, ct);
        var lines = content.Split('\n');

        string title = Path.GetFileNameWithoutExtension(filePath);
        string summary = string.Empty;
        var tags = new List<string>();
        var wikiLinks = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Extract title from H1
            if (trimmed.StartsWith("# ") && title == Path.GetFileNameWithoutExtension(filePath))
            {
                title = trimmed[2..].Trim();
                continue;
            }

            // Extract summary from blockquote
            if (trimmed.StartsWith("> ") && string.IsNullOrEmpty(summary))
            {
                summary = trimmed[2..].Trim();
                continue;
            }

            // Extract tags
            if (trimmed.StartsWith("Tags:", StringComparison.OrdinalIgnoreCase))
            {
                tags = TagRegex().Matches(trimmed)
                    .Select(m => m.Value)
                    .ToList();
                continue;
            }

            // Extract wikilinks
            foreach (Match match in WikiLinkRegex().Matches(trimmed))
            {
                var link = match.Groups[1].Value;
                if (!wikiLinks.Contains(link))
                    wikiLinks.Add(link);
            }
        }

        var fileInfo = new FileInfo(filePath);
        return new WikiPage
        {
            Title = title,
            Summary = summary,
            Content = content,
            Tags = tags,
            WikiLinks = wikiLinks,
            CreatedAt = fileInfo.CreationTimeUtc,
            UpdatedAt = fileInfo.LastWriteTimeUtc
        };
    }

    private static string FormatWikiPage(WikiPage page)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {page.Title}");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(page.Summary))
        {
            sb.AppendLine($"> {page.Summary}");
            sb.AppendLine();
        }

        if (page.Tags.Count > 0)
        {
            sb.AppendLine($"Tags: {string.Join(", ", page.Tags)}");
            sb.AppendLine();
        }

        sb.AppendLine(page.Content);

        if (page.WikiLinks.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Related");
            sb.AppendLine();
            foreach (var link in page.WikiLinks)
            {
                sb.AppendLine($"- [[{link}]]");
            }
        }

        return sb.ToString();
    }

    private static IReadOnlyList<IndexEntry> ParseIndex(string content)
    {
        var entries = new List<IndexEntry>();
        var lines = content.Split('\n');

        foreach (var line in lines)
        {
            // Format: - **PageName** — Summary #tag1 #tag2
            var match = IndexEntryRegex().Match(line.Trim());
            if (!match.Success) continue;

            var pageName = match.Groups[1].Value;
            var rest = match.Groups[2].Value;

            var tags = TagRegex().Matches(rest).Select(m => m.Value).ToList();
            var summary = TagRegex().Replace(rest, "").Trim().TrimStart('—').Trim();

            entries.Add(new IndexEntry
            {
                PageName = pageName,
                Summary = summary,
                Tags = tags,
                Keywords = ExtractKeywords(pageName, summary)
            });
        }

        return entries;
    }

    private static List<LogEntry> ParseLog(string[] lines)
    {
        var entries = new List<LogEntry>();

        foreach (var line in lines)
        {
            // Format: - [2026-04-16T00:00:00Z] [Ingest] Description | Pages: page1, page2
            var match = LogEntryRegex().Match(line.Trim());
            if (!match.Success) continue;

            if (DateTime.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var timestamp) &&
                Enum.TryParse<OperationType>(match.Groups[2].Value, true, out var operation))
            {
                var descPart = match.Groups[3].Value;
                var pipeIndex = descPart.IndexOf('|');
                var description = pipeIndex >= 0 ? descPart[..pipeIndex].Trim() : descPart.Trim();
                var pages = new List<string>();

                if (pipeIndex >= 0)
                {
                    var pagesStr = descPart[(pipeIndex + 1)..].Trim();
                    if (pagesStr.StartsWith("Pages:", StringComparison.OrdinalIgnoreCase))
                    {
                        pages = pagesStr[6..].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
                    }
                }

                entries.Add(new LogEntry
                {
                    Timestamp = timestamp,
                    Operation = operation,
                    Description = description,
                    AffectedPages = pages
                });
            }
        }

        return entries;
    }

    private async Task WriteIndexAsync(IReadOnlyList<IndexEntry> entries, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# MindAtlas Index");
        sb.AppendLine();

        foreach (var entry in entries.OrderBy(e => e.PageName))
        {
            var tags = entry.Tags.Count > 0 ? " " + string.Join(" ", entry.Tags) : "";
            var summary = !string.IsNullOrEmpty(entry.Summary) ? $" — {entry.Summary}" : "";
            sb.AppendLine($"- **{entry.PageName}**{summary}{tags}");
        }

        await File.WriteAllTextAsync(_indexPath, sb.ToString(), Encoding.UTF8, ct);
    }

    private static string FormatLogEntry(LogEntry entry)
    {
        var pages = entry.AffectedPages.Count > 0
            ? $" | Pages: {string.Join(", ", entry.AffectedPages)}"
            : "";
        return $"- [{entry.Timestamp:O}] [{entry.Operation}] {entry.Description}{pages}";
    }

    private static List<string> ExtractKeywords(params string[] sources)
    {
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            foreach (var word in WordSplitRegex().Split(source))
            {
                if (word.Length >= 2 && !word.StartsWith('#'))
                    keywords.Add(word.ToLowerInvariant());
            }
        }
        return [.. keywords];
    }

    [GeneratedRegex(@"[<>:""/\\|?*]")]
    private static partial Regex InvalidCharsRegex();

    [GeneratedRegex(@"#[\w-]+")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\[\[([^\]]+)\]\]")]
    private static partial Regex WikiLinkRegex();

    [GeneratedRegex(@"^- \*\*(.+?)\*\*(.*)$")]
    private static partial Regex IndexEntryRegex();

    [GeneratedRegex(@"^- \[(.+?)\] \[(\w+)\] (.+)$")]
    private static partial Regex LogEntryRegex();

    [GeneratedRegex(@"[\s,;.!?]+")]
    private static partial Regex WordSplitRegex();
}
