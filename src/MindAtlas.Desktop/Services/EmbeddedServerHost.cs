using System;
using System.Threading.Tasks;
using MindAtlas.Engine.Ingest;
using MindAtlas.Engine.Watcher;
using MindAtlas.Server;
using MindAtlas.Server.Hubs;
using MindAtlas.Server.Mcp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;

namespace MindAtlas.Desktop.Services;

/// <summary>
/// Hosts the ASP.NET Core server (API + Blazor WASM + SignalR + MCP) as a background task.
/// </summary>
public sealed class EmbeddedServerHost : IAsyncDisposable
{
    private WebApplication? _app;
    private readonly string _dataRoot;
    private const string ServerUrl = "http://localhost:5001";

    public string BaseUrl => ServerUrl;

    public EmbeddedServerHost(string dataRoot)
    {
        _dataRoot = dataRoot;
    }

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(ServerUrl);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        ServerSetup.RegisterCoreServices(builder.Services, _dataRoot);

        builder.Services.AddHostedService<RawDirectoryWatcher>(sp =>
            new RawDirectoryWatcher(
                _dataRoot,
                sp.GetRequiredService<IngestPipeline>(),
                sp.GetService<ILogger<RawDirectoryWatcher>>()));

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            });
        });

        builder.Services.AddControllers()
            .AddApplicationPart(typeof(ServerSetup).Assembly);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSignalR();
        builder.Services.AddMcpServer().WithTools<MindAtlasTools>();

        _app = builder.Build();

        _app.UseCors();
        _app.UseBlazorFrameworkFiles();
        _app.UseStaticFiles();
        _app.MapControllers();
        _app.MapHub<WikiHub>("/hubs/wiki");
        _app.MapMcp();
        _app.MapFallbackToFile("index.html");

        await _app.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
