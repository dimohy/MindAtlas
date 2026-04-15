using MindAtlas.Engine.Ingest;
using MindAtlas.Engine.Watcher;
using MindAtlas.Server;
using MindAtlas.Server.Hubs;
using MindAtlas.Server.Mcp;
using ModelContextProtocol;

// --- stdio MCP mode ---
if (args.Contains("--mcp-stdio"))
{
    var dataRoot = Environment.GetEnvironmentVariable("MINDATLAS_DATA_ROOT")
        ?? Path.Combine(AppContext.BaseDirectory, "data");

    var host = Host.CreateApplicationBuilder(args);
    ServerSetup.RegisterCoreServices(host.Services, dataRoot);
    host.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<MindAtlasTools>();
    await host.Build().RunAsync();
    return;
}

// --- HTTP mode (Web API + MCP) ---
var builder = WebApplication.CreateBuilder(args);

// --- Configuration ---
var webDataRoot = builder.Configuration.GetValue<string>("MindAtlas:DataRoot")
    ?? Path.Combine(AppContext.BaseDirectory, "data");

ServerSetup.RegisterCoreServices(builder.Services, webDataRoot);

// --- DI: Background services ---
builder.Services.AddHostedService<RawDirectoryWatcher>(sp =>
    new RawDirectoryWatcher(
        webDataRoot,
        sp.GetRequiredService<IngestPipeline>(),
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
    .WithTools<MindAtlasTools>();

var app = builder.Build();

// --- Middleware pipeline ---
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapHub<WikiHub>("/hubs/wiki");
app.MapMcp();
app.MapFallbackToFile("index.html");

app.Run();
