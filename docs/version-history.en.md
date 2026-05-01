# Version History

## 0.1.0 — LLM Wiki Relationship Graph and Maintenance (2026-05-01)

### Major Features

- Typed Relationship Wikilinks — Added semantic relationship type support such as `@supports`, `@contradicts`, `@supersedes`, and `@depends_on`
- Relationship Retag Maintenance — Added a maintenance flow that proposes evidence-based typed relationship candidates for existing plain wikilinks and applies selected proposals safely
- Relationship Retag Review UI — Added Lint-screen review, per-proposal selection, and pre-apply confirmation for safe relationship updates
- Typed Knowledge Graph Controls — Added relationship-based graph colors, labels, legend, and filter toggles

### Major Improvements

- LLM Wiki Ingest Quality — Strengthened ingest guidance so raw sources remain immutable evidence while generated pages include source, confidence, stale, contradiction, and supersession signals
- Wikilink Normalization — Improved page connection stability by normalizing aliases, headings, folder paths, local headings, and relationship markers
- Broken Link Maintenance — Added code-block link exclusion, near-match repair, and plain-text conversion for unresolved links
- MCP Relationship Tools — Exposed relationship retag proposal generation and confidence-based application through MCP tools
- Localization Freshness — Strengthened locale cache-busting so newly added UI strings are not hidden by browser or WebView cache

### Major Bug Fixes

- Graph Render Lifecycle — Fixed Blazor render ordering so graph rendering starts only after the graph container exists in the DOM
- Graph Offline Reliability — Removed D3 CDN dependency and stabilized graph rendering with a local SVG force layout
- Code Link Lint Accuracy — Fixed false broken-link reports for wikilinks inside fenced code and inline code

### Quality / Validation

- Relationship Regression Tests — Added regression coverage for typed link parsing, graph edge type extraction, selected retag application, and broken-link repair
- End-to-End Validation — Passed 53 total tests, completed full solution build, and visually verified Graph/Lint UI flows in the browser
