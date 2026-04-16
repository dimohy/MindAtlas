using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MindAtlas.Desktop.Services;

// Periodically checks a remote endpoint for newer versions.
public sealed class UpdateCheckService : IDisposable
{
    private readonly HttpClient _http = new();
    private readonly string _checkUrl;
    private readonly Timer _timer;

    public string CurrentVersion { get; } =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";

    public event Action<string>? UpdateAvailable;

    public UpdateCheckService(string checkUrl, TimeSpan interval)
    {
        _checkUrl = checkUrl;
        _timer = new Timer(_ => _ = CheckAsync(), null, TimeSpan.FromMinutes(1), interval);
    }

    public async Task CheckAsync()
    {
        try
        {
            var info = await _http.GetFromJsonAsync<VersionInfo>(_checkUrl);
            if (info is null) return;

            var remote = new Version(info.Version);
            var local = new Version(CurrentVersion);
            if (remote > local)
                UpdateAvailable?.Invoke(info.Version);
        }
        catch
        {
            // Silently ignore network/parse errors
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
        _http.Dispose();
    }

    private sealed record VersionInfo(string Version, string? DownloadUrl = null);
}
