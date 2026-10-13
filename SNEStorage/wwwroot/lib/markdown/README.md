# Markdown dependencies

Local browser modules, loaded on demand by `js/markdown.js`:

- Marked 18.1.1, MIT: https://github.com/markedjs/marked
- DOMPurify 3.4.17, Apache-2.0 OR MPL-2.0: https://github.com/cure53/DOMPurify

Copies from the published npm packages. `purify.es.js` is the unchanged
`dist/purify.es.mjs`, renamed to `.js` for static hosting. License files are included.
There is no runtime CDN dependency or global parser configuration.

The renderer escapes raw HTML, shows image alt text without fetching the image,
sanitizes generated HTML with an explicit tag/attribute allowlist, and permits
only HTTP, HTTPS and mailto links. It does not allow user-supplied IDs, classes,
styles, scripts, forms, SVG, iframes or interactive inputs. Task lists use text
symbols. Generated DOM stays inside an otherwise empty Blazor leaf element.

Markdown is stored and edited as the original text. If JavaScript or parsing
fails, the escaped plain text remains visible. Existing line breaks are kept.

Keep the sanitizer updated when security releases become available.
