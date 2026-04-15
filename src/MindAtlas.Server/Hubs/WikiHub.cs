using Microsoft.AspNetCore.SignalR;

namespace MindAtlas.Server.Hubs;

/// <summary>
/// SignalR hub for real-time wiki events (page updates, ingest status, log entries).
/// </summary>
public sealed class WikiHub : Hub
{
    // Client methods (server → client):
    // - OnWikiUpdated(string pageName)
    // - OnIngestStarted(string fileName)
    // - OnIngestCompleted(string fileName, string[] pages)
    // - OnLogAppended(string logEntry)
}
