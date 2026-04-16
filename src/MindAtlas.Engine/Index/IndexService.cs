using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using MindAtlas.Core.Interfaces;
using MindAtlas.Core.Models;

namespace MindAtlas.Engine.Index;

/// <summary>
/// In-memory index service — parses index.md and provides fast keyword search.
/// Auto-reloads when wiki/ files change via FileSystemWatcher.
/// </summary>
public sealed partial class IndexService : IIndexService, IDisposable
{
    private readonly string _wikiDir;
    private readonly string _indexPath;
    private readonly FileSystemWatcher? _watcher;
    private ConcurrentDictionary<string, IndexEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool _dirty = true;
    private readonly SemaphoreSlim _rebuildLock = new(1, 1);

    public IndexService(string dataRoot)
    {
        _wikiDir = Path.Combine(dataRoot, "wiki");
        _indexPath = Path.Combine(_wikiDir, "index.md");
        Directory.CreateDirectory(_wikiDir);

        if (Directory.Exists(_wikiDir))
        {
            _watcher = new FileSystemWatcher(_wikiDir, "*.md")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                IncludeSubdirectories = false
            };
            _watcher.Changed += OnWikiChanged;
            _watcher.Created += OnWikiChanged;
            _watcher.Deleted += OnWikiChanged;
            _watcher.Renamed += (_, _) => _dirty = true;
            _watcher.EnableRaisingEvents = true;
        }
    }

    public async Task<IReadOnlyList<IndexEntry>> SearchAsync(string keyword, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);

        if (string.IsNullOrWhiteSpace(keyword))
            return [];

        var lower = keyword.ToLowerInvariant();
        return _entries.Values
            .Where(e => MatchesKeyword(e, lower))
            .ToList();
    }

    public async Task RebuildAsync(CancellationToken ct = default)
    {
        await _rebuildLock.WaitAsync(ct);
        try
        {
            var newEntries = new ConcurrentDictionary<string, IndexEntry>(StringComparer.OrdinalIgnoreCase);

            // Parse from index.md if available
            if (File.Exists(_indexPath))
            {
                var content = await File.ReadAllTextAsync(_indexPath, ct);
                foreach (var entry in ParseIndexContent(content))
                {
                    newEntries[entry.PageName] = entry;
                }
            }

            // Scan wiki/ files to fill gaps
            foreach (var file in Directory.GetFiles(_wikiDir, "*.md"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (name is "index" or "log") continue;
                if (newEntries.ContainsKey(name)) continue;

                var entry = await ExtractEntryFromFileAsync(file, ct);
                if (entry is not null)
                    newEntries[entry.PageName] = entry;
            }

            _entries = newEntries;
            _dirty = false;
        }
        finally
        {
            _rebuildLock.Release();
        }
    }

    public async Task<IReadOnlyList<IndexEntry>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        return _entries.Values.OrderBy(e => e.PageName).ToList();
    }

    public async Task UpdateAsync(IndexEntry entry, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        _entries[entry.PageName] = entry;
        await PersistIndexAsync(ct);
    }

    public async Task RemoveAsync(string pageName, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        _entries.TryRemove(pageName, out _);
        await PersistIndexAsync(ct);
    }

    private async Task PersistIndexAsync(CancellationToken ct)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Index");
        sb.AppendLine();

        foreach (var entry in _entries.Values.OrderBy(e => e.PageName))
        {
            sb.AppendLine($"## {entry.PageName}");
            sb.AppendLine($"- **Summary**: {entry.Summary}");
            if (entry.Tags.Count > 0)
                sb.AppendLine($"- **Tags**: {string.Join(", ", entry.Tags)}");
            if (entry.Keywords.Count > 0)
                sb.AppendLine($"- **Keywords**: {string.Join(", ", entry.Keywords)}");
            sb.AppendLine();
        }

        await File.WriteAllTextAsync(_indexPath, sb.ToString(), ct);
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _rebuildLock.Dispose();
    }

    // --- Private helpers ---

    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        if (_dirty)
            await RebuildAsync(ct);
    }

    private void OnWikiChanged(object sender, FileSystemEventArgs e)
    {
        _dirty = true;
    }

    private static bool MatchesKeyword(IndexEntry entry, string keyword)
    {
        // Check page name
        if (entry.PageName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            return true;

        // Check summary
        if (entry.Summary.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            return true;

        // Check tags
        if (entry.Tags.Any(t => t.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            return true;

        // Check keywords
        if (entry.Keywords.Any(k => k.StartsWith(keyword, StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }

    private static IReadOnlyList<IndexEntry> ParseIndexContent(string content)
    {
        var entries = new List<IndexEntry>();

        foreach (var line in content.Split('\n'))
        {
            var match = IndexLineRegex().Match(line.Trim());
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

    private static async Task<IndexEntry?> ExtractEntryFromFileAsync(string filePath, CancellationToken ct)
    {
        var lines = await File.ReadAllLinesAsync(filePath, ct);
        var name = Path.GetFileNameWithoutExtension(filePath);
        string summary = string.Empty;
        var tags = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("# "))
            {
                name = trimmed[2..].Trim();
                continue;
            }
            if (trimmed.StartsWith("> ") && string.IsNullOrEmpty(summary))
            {
                summary = trimmed[2..].Trim();
                continue;
            }
            if (trimmed.StartsWith("Tags:", StringComparison.OrdinalIgnoreCase))
            {
                tags = TagRegex().Matches(trimmed).Select(m => m.Value).ToList();
                break;
            }
        }

        return new IndexEntry
        {
            PageName = name,
            Summary = summary,
            Tags = tags,
            Keywords = ExtractKeywords(name, summary)
        };
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

    [GeneratedRegex(@"^- \*\*(.+?)\*\*(.*)$")]
    private static partial Regex IndexLineRegex();

    [GeneratedRegex(@"#[\w-]+")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"[\s,;.!?]+")]
    private static partial Regex WordSplitRegex();
}
