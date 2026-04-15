using MindAtlas.Core.Interfaces;
using MindAtlas.Engine;
using MindAtlas.Engine.Agent;
using MindAtlas.Engine.Index;
using MindAtlas.Engine.Ingest;
using MindAtlas.Engine.Lint;
using MindAtlas.Engine.Query;
using MindAtlas.Engine.Repository;
using MindAtlas.Engine.Watcher;
using MindAtlas.Server.Hubs;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration ---
var dataRoot = builder.Configuration.GetValue<string>("MindAtlas:DataRoot")
    ?? Path.Combine(AppContext.BaseDirectory, "data");

// --- DI: Repositories ---
builder.Services.AddSingleton(sp => new WikiRepository(dataRoot));
builder.Services.AddSingleton<IWikiRepository>(sp => sp.GetRequiredService<WikiRepository>());
builder.Services.AddSingleton<IRawRepository>(sp => new RawRepository(dataRoot));

// --- DI: Engine services ---
builder.Services.AddSingleton<IndexService>(sp => new IndexService(dataRoot));
builder.Services.AddSingleton<IIndexService>(sp => sp.GetRequiredService<IndexService>());
builder.Services.AddSingleton<ICopilotAgentService, CopilotAgentService>();
builder.Services.AddSingleton<IngestPipeline>();
builder.Services.AddSingleton<QueryEngine>();
builder.Services.AddSingleton<LintEngine>();
builder.Services.AddSingleton<IWikiEngine, WikiEngine>();

// --- DI: Background services ---
builder.Services.AddHostedService<RawDirectoryWatcher>(sp =>
    new RawDirectoryWatcher(
        dataRoot,
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

var app = builder.Build();

// --- Middleware pipeline ---
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.MapControllers();
app.MapHub<WikiHub>("/hubs/wiki");

app.Run();
