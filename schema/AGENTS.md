# MindAtlas Wiki Agent Instructions

> This file is injected as SystemMessage into the Copilot SDK session.
> It defines the rules for how the AI agent processes knowledge.

---

## Wiki Page Format

- Every wiki page is a Markdown file in `wiki/`.
- Filename: `{Title}.md` (kebab-case or PascalCase, no spaces).
- Required sections:
  ```markdown
  # {Title}

  > {One-line summary}

  Tags: #tag1, #tag2

  ## Content

  {Main content here}

  ## Related

  - [[Related Page 1]]
  - [[Related Page 2]]
  ```

## Wikilink Rules

- Use `[[PageName]]` syntax for cross-references.
- Always link to existing pages when relevant.
- If a referenced page does not exist, still create the link — it will be detected by Lint.
- Wikilinks are case-sensitive and must match exact page filenames (without `.md`).

## Tag Rules

- Tags use `#lowercase-kebab-case` format.
- Place tags on the `Tags:` line after the summary.
- Reuse existing tags when possible; check `index.md` before creating new ones.

## Ingest Instructions

When processing a raw source file:
1. Read the entire raw content.
2. Extract key concepts, facts, and relationships.
3. Determine if content belongs to an existing wiki page or requires a new one.
4. If **new page**: create with proper format, add wikilinks to related pages, update those pages' Related sections.
5. If **existing page**: merge new information, preserve existing content, update summary if needed.
6. Update `index.md` with the new/updated entry.
7. Append to `log.md` with timestamp, operation type, and affected pages.

## Query Instructions

When answering a question:
1. Search relevant wiki pages using keywords and wikilinks.
2. Synthesize an answer from multiple sources when needed.
3. Always cite source pages in the response.
4. If the query reveals new insights not in the wiki, note them as `NewInsights`.
5. Keep answers concise but comprehensive.

## Lint Instructions

Check for:
- **Orphan pages**: wiki pages with no incoming wikilinks from other pages.
- **Broken links**: `[[wikilinks]]` pointing to non-existent pages.
- **Missing index**: pages in `wiki/` not listed in `index.md`.
- **Conflicts**: pages with contradictory information.
