# Markdown in SNEStorage

News, resource descriptions, version release notes, user biographies, comments and replies render
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
Profile biographies use the same renderer on both the owner's profile and public
profiles, with syntax help in Edit profile. Existing biographies remain stored
as text with the same 2,000-character limit; no migration is required.
Code blocks and tables scroll within their containers, and heading sizes stay
relative to the surrounding content. Compact news keep their bounded height.
News excerpts keep the sanitized Markdown formatting, including emphasis, links,
lists and code, instead of converting it to plain text. CSS bounds the rendered
preview to four lines on the landing and six in the news list; the full article
keeps the normal typography and scrollable code/table containers. No character
limit is applied to the Markdown source before parsing.

## Rendering boundaries

- Marked 18.1.1 and DOMPurify 3.4.17 are served locally and preloaded once per
  document. markdown-loader.js initializes the shared renderer for existing and
  incoming blocks, without JS interop or server acknowledgements per text.
- Raw HTML is escaped; embedded images display their alt text instead of loading
  remote media. This avoids extra remote requests and unbounded media in comments.
- Generated HTML is sanitized with a short explicit allowlist. User-supplied
  scripts, styles, event handlers, IDs, classes, forms, iframes and SVG are excluded.
- Links allow only HTTP, HTTPS and mailto; embedded credentials are rejected.
  Links open in a new tab with noopener/noreferrer.
- JavaScript owns only a stable empty leaf node inside the Blazor component.
  Generated HTML is never returned to .NET over SignalR. Rendering is repeated
  only when the source text changes, not when votes or other metadata change.
- Razor encodes the Markdown source in a hidden leaf. Before initial library
  loading, a small placeholder reserves space; raw Markdown does not flash first.
  Once loaded, a MutationObserver formats newly received blocks before the next
  paint and skips its own output mutations. Only changed sources/excerpt mode are
  reparsed. If module loading, parsing or sanitization fails, the output uses
  textContent as a safe plain-text fallback. A noscript fallback is also provided.

Dependency sources and licenses are in `wwwroot/lib/markdown/README.md`.
Keep DOMPurify updated when security fixes are released. If a different client
later renders this API text, it must apply its own safe Markdown rendering too.
