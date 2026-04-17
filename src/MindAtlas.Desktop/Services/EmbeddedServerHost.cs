using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using MindAtlas.Core.Interfaces;
using MindAtlas.Engine.Ingest;
using MindAtlas.Engine.Watcher;
using MindAtlas.Server;
using MindAtlas.Server.Hubs;
using MindAtlas.Server.Mcp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;

namespace MindAtlas.Desktop.Services;

/// <summary>
/// Hosts the ASP.NET Core server (API + Blazor WASM + SignalR + MCP) as a background task.
/// Automatically selects an available port when the preferred port is occupied.
/// </summary>
public sealed class EmbeddedServerHost : IAsyncDisposable
{
    private WebApplication? _app;
    private readonly string _dataRoot;
    private readonly string? _githubToken;
    private const int PreferredPort = 5001;
    private static readonly int[] CandidatePorts = [5001, 5002, 5003, 5004, 5005];

    public string BaseUrl { get; private set; } = $"http://localhost:{PreferredPort}";

    public EmbeddedServerHost(string dataRoot, string? githubToken = null)
    {
        _dataRoot = dataRoot;
        _githubToken = githubToken;
    }

    public async Task StartAsync()
    {
        var port = FindAvailablePort();
        BaseUrl = $"http://localhost:{port}";

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ServerSetup).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.WebHost.UseUrls(BaseUrl);
        builder.WebHost.UseStaticWebAssets();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        // Also load appsettings.desktop.json so the Settings page (which
        // reads/writes via IConfiguration) can see the GitHub token that
        // the desktop app persists there. reloadOnChange so PUT /api/settings
        // hot-reloads the value for subsequent GETs.
        builder.Configuration.AddJsonFile(
            "appsettings.desktop.json",
            optional: true,
            reloadOnChange: true);

        ServerSetup.RegisterCoreServices(builder.Services, _dataRoot, _githubToken);

        builder.Services.AddHostedService<RawDirectoryWatcher>(sp =>
            new RawDirectoryWatcher(
                _dataRoot,
                sp.GetRequiredService<IngestPipeline>(),
                sp.GetRequiredService<IRawRepository>(),
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
        builder.Services.AddMcpServer().WithHttpTransport().WithTools<MindAtlasTools>();

        _app = builder.Build();

        _app.UseCors();

        // Send no-cache headers on the root HTML document so WebView2 never
        // serves a stale index.html (which would reference old CSS filenames).
        _app.Use(async (ctx, next) =>
        {
            ctx.Response.OnStarting(() =>
            {
                if (ctx.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true)
                {
                    ctx.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
                    ctx.Response.Headers.Pragma = "no-cache";
                    ctx.Response.Headers.Expires = "0";
                }
                return Task.CompletedTask;
            });
            await next();
        });

        _app.UseBlazorFrameworkFiles();
        _app.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                // Disable caching for app CSS/JS so style tweaks show up
                // immediately inside the embedded WebView without needing
                // the user to clear browser data.
                var path = ctx.File.Name;
                if (path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
                    ctx.Context.Response.Headers.Pragma = "no-cache";
                    ctx.Context.Response.Headers.Expires = "0";
                }
            }
        });
        _app.MapControllers();
        _app.MapHub<WikiHub>("/hubs/wiki");
        _app.MapMcp("/mcp");
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

    private static int FindAvailablePort()
    {
        foreach (var port in CandidatePorts)
        {
            if (IsPortAvailable(port))
                return port;
        }

        // All candidates occupied — let the OS assign an ephemeral port
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var assignedPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return assignedPort;
    }

    private static bool IsPortAvailable(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
