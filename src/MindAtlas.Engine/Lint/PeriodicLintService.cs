using MindAtlas.Engine.Lint;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MindAtlas.Engine.Lint;

/// <summary>
/// Background service that runs LintEngine on a configurable schedule.
/// Default interval: 1 hour.
/// </summary>
public sealed class PeriodicLintService(
    LintEngine lintEngine,
    ILogger<PeriodicLintService>? logger = null) : BackgroundService
{
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Delay initial lint to let the system warm up
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                logger?.LogInformation("Running periodic lint check...");
                var result = await lintEngine.LintAsync(stoppingToken);
                var totalIssues = result.OrphanPages.Count + result.BrokenLinks.Count
                    + result.MissingIndex.Count + result.Conflicts.Count;
                logger?.LogInformation("Periodic lint complete: {Count} issues found", totalIssues);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Periodic lint failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
