using MindAtlas.Core.Interfaces;
using MindAtlas.Engine;
using MindAtlas.Engine.Agent;
using MindAtlas.Engine.Index;
using MindAtlas.Engine.Ingest;
using MindAtlas.Engine.Lint;
using MindAtlas.Engine.Query;
using MindAtlas.Engine.Repository;

namespace MindAtlas.Server;

/// <summary>
/// Shared DI registration used by both the standalone Server and the embedded Desktop host.
/// </summary>
public static class ServerSetup
{
    public static void RegisterCoreServices(IServiceCollection services, string dataRoot)
    {
        services.AddSingleton(sp => new WikiRepository(dataRoot));
        services.AddSingleton<IWikiRepository>(sp => sp.GetRequiredService<WikiRepository>());
        services.AddSingleton<IRawRepository>(sp => new RawRepository(dataRoot));

        services.AddSingleton<IndexService>(sp => new IndexService(dataRoot));
        services.AddSingleton<IIndexService>(sp => sp.GetRequiredService<IndexService>());
        services.AddSingleton<ICopilotAgentService, CopilotAgentService>();
        services.AddSingleton<IngestPipeline>();
        services.AddSingleton<QueryEngine>();
        services.AddSingleton<LintEngine>();
        services.AddSingleton<IWikiEngine, WikiEngine>();
    }
}
