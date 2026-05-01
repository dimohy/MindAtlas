# MindAtlas

[한국어 README](README.ko.md) · [Version history](docs/version-history.en.md) · [License](LICENSE)

MindAtlas is a local-first **LLM Wiki** for turning raw notes, research, conversations, and project knowledge into persistent wiki pages, wikilinks, search indexes, and relationship graphs.

It keeps original raw sources as the source of truth, then builds and maintains a generated wiki layer that can be queried by humans, the web UI, the desktop app, and MCP-compatible AI agents.

## Highlights

- **LLM Wiki ingest** — transform raw Markdown/text into durable wiki knowledge pages.
- **Source-aware knowledge maintenance** — generated pages can carry source, confidence, stale, contradiction, and supersession signals.
- **Obsidian-style wikilinks** — supports aliases, heading/path normalization, and typed relationship markers.
- **Typed relationship graph** — visualize `@supports`, `@contradicts`, `@supersedes`, `@depends_on`, and related edges with labels, colors, legends, and filters.
- **Relationship retag workflow** — review typed-relationship proposals, select individual changes, and apply them safely after confirmation.
- **Lint and repair** — detect orphan pages, broken links, missing index entries, and repair safe wikilink issues.
- **MCP integration** — expose ingest, search, query, lint, asset lookup, and relationship retag tools to compatible AI clients.
- **Desktop + Web** — Avalonia desktop shell with an ASP.NET Core server and Blazor WebAssembly UI.

## Architecture

```text
Desktop (Avalonia)        Web UI (Blazor WASM)        MCP clients
        │                         │                       │
        └──────────────┬──────────┴──────────────┬────────┘
                       ▼                         ▼
              MindAtlas.Server (ASP.NET Core, REST, SignalR, MCP)
                       │
                       ▼
              MindAtlas.Engine (Ingest, Query, Lint, Maintenance)
                       │
                       ▼
              MindAtlas.Core (Models and Interfaces)
```

## Requirements

- Windows is the primary development environment.
- .NET SDK 10 or later.
- A GitHub token is required for Copilot-backed ingest/query flows when it is not configured in app settings.

## Quick start

Run the desktop app:

```powershell
./run.ps1
```

Run the server/web app directly:

```powershell
dotnet run --project src/MindAtlas.Server
```

Then open:

```text
http://localhost:5001
```

Run tests:

```powershell
dotnet test MindAtlas.slnx
```

Build everything:

```powershell
dotnet build MindAtlas.slnx
```

## Configuration

MindAtlas reads server settings from `appsettings.json`, environment variables, and runtime settings.

Common local settings:

- `MindAtlas:DataRoot` — data directory, default `./data`.
- `MindAtlas:GitHubToken` or `GITHUB_TOKEN` — token for Copilot-backed operations.
- `MindAtlas:UiLanguage` — UI language.
- `MindAtlas:Theme` — `auto`, light, or dark theme setting.

An `.env.example` file is provided for development convenience. Copy it to `.env` for local secrets, but remember that `.env` files are ignored by Git and may need to be loaded into your shell or environment explicitly.

## MCP usage

MindAtlas can run as an MCP stdio server:

```powershell
dotnet run --project src/MindAtlas.Server -- --mcp-stdio
```

Available MCP capabilities include:

- ingest text into the wiki
- keyword search
- natural-language wiki query
- vibe-coding asset lookup
- lint health checks
- relationship retag proposal generation
- relationship retag application

## Repository layout

```text
src/MindAtlas.Core      Shared models and interfaces
src/MindAtlas.Engine    Ingest, query, lint, repository, and maintenance logic
src/MindAtlas.Server    ASP.NET Core REST/SignalR/MCP host
src/MindAtlas.Web       Blazor WebAssembly UI
src/MindAtlas.Desktop   Avalonia desktop shell
tests/                  xUnit test projects
docs/                   Documentation and version history
```

## LLM Wiki workflow

1. Save immutable raw source text.
2. Generate or update wiki pages from the raw source.
3. Normalize and maintain wikilinks.
4. Use lint to find broken links, orphan pages, and index issues.
5. Review relationship retag proposals.
6. Visualize knowledge as a typed graph.
7. Query the wiki from the web UI, desktop app, or MCP clients.

## License

MindAtlas is licensed under the [Apache License 2.0](LICENSE).
