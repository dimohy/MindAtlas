using MindAtlas.Core.Models;
using MindAtlas.Engine.Repository;

namespace MindAtlas.Engine.Tests;

public class RawRepositoryTests : IDisposable
{
    private readonly string _dataRoot;
    private readonly RawRepository _repo;

    public RawRepositoryTests()
    {
        _dataRoot = Path.Combine(Path.GetTempPath(), "mindatlas_raw_test_" + Guid.NewGuid().ToString("N")[..8]);
        _repo = new RawRepository(_dataRoot);
    }

    [Fact]
    public async Task GetAllAsync_PopulatesStatusFromStatusJson()
    {
        var rawDir = Path.Combine(_dataRoot, "raw");
        await File.WriteAllTextAsync(Path.Combine(rawDir, "done.md"), "done content");
        await File.WriteAllTextAsync(Path.Combine(rawDir, "pending.md"), "pending content");

        await _repo.UpdateStatusAsync("done.md", ProcessingStatus.Done);

        var all = await _repo.GetAllAsync();

        var done = all.Single(r => r.FileName == "done.md");
        var pending = all.Single(r => r.FileName == "pending.md");

        Assert.Equal(ProcessingStatus.Done, done.Status);
        Assert.Equal(ProcessingStatus.Pending, pending.Status);
    }

    [Fact]
    public async Task GetUnprocessedAsync_ExcludesDoneFiles()
    {
        var rawDir = Path.Combine(_dataRoot, "raw");
        await File.WriteAllTextAsync(Path.Combine(rawDir, "done.md"), "x");
        await File.WriteAllTextAsync(Path.Combine(rawDir, "failed.md"), "x");
        await File.WriteAllTextAsync(Path.Combine(rawDir, "pending.md"), "x");

        await _repo.UpdateStatusAsync("done.md", ProcessingStatus.Done);
        await _repo.UpdateStatusAsync("failed.md", ProcessingStatus.Failed);

        var unprocessed = await _repo.GetUnprocessedAsync();
        var names = unprocessed.Select(r => r.FileName).ToHashSet();

        Assert.DoesNotContain("done.md", names);
        Assert.Contains("failed.md", names);
        Assert.Contains("pending.md", names);
    }

    [Fact]
    public async Task GetByNameAsync_ReflectsPersistedStatus()
    {
        var rawDir = Path.Combine(_dataRoot, "raw");
        await File.WriteAllTextAsync(Path.Combine(rawDir, "item.md"), "x");

        await _repo.UpdateStatusAsync("item.md", ProcessingStatus.Done);

        var loaded = await _repo.GetByNameAsync("item.md");

        Assert.NotNull(loaded);
        Assert.Equal(ProcessingStatus.Done, loaded!.Status);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataRoot))
            Directory.Delete(_dataRoot, recursive: true);
    }
}
