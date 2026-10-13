# Markdown in SNEStorage

News, resource descriptions, version release notes, comments and replies render
Markdown through the shared `MarkdownText` component. Titles and metadata remain
plain text. Editors preserve the original Markdown source and existing DTOs/API
contracts; no backend, database or package-reference changes are required.

## Supported formatting

- Headings, paragraphs and line breaks.
- Bold, italic, strikethrough, quotations and separators.
- Ordered/unordered lists and read-only task indicators.
- Inline code and fenced code blocks.
- Links and tables.

`MarkdownHelp` displays syntax guidance beside the existing editors.
Code blocks and tables scroll within their containers, and heading sizes stay
relative to the surrounding content. Compact news keep their bounded height.

## Rendering boundaries

- Marked 18.1.1 and DOMPurify 3.4.17 are served locally and loaded on demand.
- Raw HTML is escaped; embedded images display their alt text instead of loading
  remote media. This avoids extra remote requests and unbounded media in comments.
- Generated HTML is sanitized with a short explicit allowlist. User-supplied
  scripts, styles, event handlers, IDs, classes, forms, iframes and SVG are excluded.
- Links allow only HTTP, HTTPS and mailto; embedded credentials are rejected.
  Links open in a new tab with noopener/noreferrer.
- JavaScript owns only a stable empty leaf node inside the Blazor component.
  Generated HTML is never returned to .NET over SignalR. Rendering is repeated
  only when the source text changes, not when votes or other metadata change.
- Plain text is rendered and escaped by Razor before initialization and remains
  visible if module loading, parsing or sanitization fails.

Dependency sources and licenses are in `wwwroot/lib/markdown/README.md`.
Keep DOMPurify updated when security fixes are released. If a different client
later renders this API text, it must apply its own safe Markdown rendering too.
