using System.Threading.Channels;
using MindAtlas.Engine.Ingest;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MindAtlas.Engine.Watcher;

/// <summary>
/// Watches raw/ directory for new files and triggers the ingest pipeline.
/// Uses Channel + debouncing to coalesce rapid filesystem events.
/// </summary>
public sealed class RawDirectoryWatcher : BackgroundService
{
    private readonly string _rawDir;
    private readonly IngestPipeline _ingestPipeline;
    private readonly ILogger<RawDirectoryWatcher>? _logger;
    private readonly Channel<string> _channel;
    private readonly TimeSpan _debounceInterval = TimeSpan.FromMilliseconds(500);

    public RawDirectoryWatcher(
        string dataRoot,
        IngestPipeline ingestPipeline,
        ILogger<RawDirectoryWatcher>? logger = null)
    {
        _rawDir = Path.Combine(dataRoot, "raw");
        _ingestPipeline = ingestPipeline;
        _logger = logger;
        _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true
        });
        Directory.CreateDirectory(_rawDir);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var watcher = new FileSystemWatcher(_rawDir)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
            IncludeSubdirectories = false
        };

        watcher.Created += (_, e) =>
        {
            if (!Path.GetFileName(e.FullPath).StartsWith('.'))
                _channel.Writer.TryWrite(e.FullPath);
        };

        watcher.EnableRaisingEvents = true;
        _logger?.LogInformation("Watching raw/ directory: {Path}", _rawDir);

        await ProcessChannelAsync(stoppingToken);
    }

    private async Task ProcessChannelAsync(CancellationToken ct)
    {
        var pending = new Dictionary<string, DateTime>();

        while (!ct.IsCancellationRequested)
        {
            // Wait for events or timeout
            try
            {
                if (_channel.Reader.TryRead(out var path))
                {
                    pending[path] = DateTime.UtcNow;
                }
                else
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(_debounceInterval);
                    try
                    {
                        var path2 = await _channel.Reader.ReadAsync(cts.Token);
                        pending[path2] = DateTime.UtcNow;
                        continue;
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        // Debounce timeout — process pending
                    }
                }

                // Process files that have settled (debounce expired)
                var now = DateTime.UtcNow;
                var ready = pending
                    .Where(kv => now - kv.Value >= _debounceInterval)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var filePath in ready)
                {
                    pending.Remove(filePath);
                    await ProcessFileAsync(filePath, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessFileAsync(string filePath, CancellationToken ct)
    {
        try
        {
            _logger?.LogInformation("New raw file detected: {Path}", Path.GetFileName(filePath));
            await _ingestPipeline.IngestAsync(filePath, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to process raw file: {Path}", filePath);
        }
    }
}
