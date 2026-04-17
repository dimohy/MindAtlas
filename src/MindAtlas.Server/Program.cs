using MindAtlas.Core.Interfaces;
using MindAtlas.Engine.Ingest;
using MindAtlas.Engine.Watcher;
using MindAtlas.Server;
using MindAtlas.Server.Hubs;
using MindAtlas.Server.Mcp;
using ModelContextProtocol;
using Serilog;

// --- Bootstrap Serilog ---
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(AppContext.BaseDirectory, "logs", "mindatlas-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14)
    .CreateLogger();

try
{

// --- stdio MCP mode ---
if (args.Contains("--mcp-stdio"))
{
    var dataRoot = Environment.GetEnvironmentVariable("MINDATLAS_DATA_ROOT")
        ?? Path.Combine(AppContext.BaseDirectory, "data");

    var host = Host.CreateApplicationBuilder(args);
    host.Services.AddSerilog();
    // Prefer the persisted MindAtlas:GitHubToken from appsettings.json and
    // only fall back to the GITHUB_TOKEN env var when unset.
    var stdioToken = host.Configuration.GetValue<string>("MindAtlas:GitHubToken")
        ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
    ServerSetup.RegisterCoreServices(host.Services, dataRoot, stdioToken);
    host.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<MindAtlasTools>();
    await host.Build().RunAsync();
    return;
}

// --- HTTP mode (Web API + MCP) ---
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// --- Configuration ---
var webDataRoot = builder.Configuration.GetValue<string>("MindAtlas:DataRoot")
    ?? Path.Combine(AppContext.BaseDirectory, "data");
var webGithubToken = builder.Configuration.GetValue<string>("MindAtlas:GitHubToken")
    ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");

ServerSetup.RegisterCoreServices(builder.Services, webDataRoot, webGithubToken);

// --- DI: Background services ---
builder.Services.AddHostedService<RawDirectoryWatcher>(sp =>
    new RawDirectoryWatcher(
        webDataRoot,
        sp.GetRequiredService<IngestPipeline>(),
        sp.GetRequiredService<IRawRepository>(),
        sp.GetService<ILogger<RawDirectoryWatcher>>()));

// --- CORS ---
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5001", "https://localhost:5002", "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// --- Controllers ---
builder.Services.AddControllers();

// --- Swagger/OpenAPI ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// --- SignalR ---
builder.Services.AddSignalR();

// --- MCP (HTTP) ---
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<MindAtlasTools>();

var app = builder.Build();

// --- Middleware pipeline ---
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();

// Global exception handler
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
    });
});

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapHub<WikiHub>("/hubs/wiki");
app.MapMcp("/mcp");
app.MapFallbackToFile("index.html");

app.Run();

}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
