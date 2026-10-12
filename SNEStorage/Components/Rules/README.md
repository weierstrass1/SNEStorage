# Public site rules

`/Rules` reads `wwwroot/content/site-rules.json` in the browser. The static file
is the source of the public policy, with English copy and stable section/rule
anchors. It does not query an account endpoint, database or backend service.

`SiteRulesDocument` and its section/entry classes are presentation data only.
Rules.razor.cs selects the document and its numbering. StaticDocumentPage shares
browser transport and cleanup with the separate Seal of Quality page, keeping
those details out of the views. A failed read shows a retry button. Requests
time out and are cancelled on disposal; disconnected-circuit cleanup is handled.

The JSON is revalidated on page entry. Text is rendered with normal Razor
encoding. Preserve unique section and rule IDs to keep incoming links valid.
Sections receive letters in JSON order (A, B, C, …). Rule numbers restart at 1
in each section and appear as A.1, A.2, B.1, …; reordering or adding a rule
requires no manual renumbering. Section letters appear in headings and the
contents index. Numbers are plain
text, not links. Section links include the Rules path because fragment-only
URLs would resolve against the application's root base URL. Dates are edited
explicitly. New sections and entries require no view changes.

Entries within each section are ordered by severity or functional priority,
with the most important items first. This order is stored directly in each
JSON rules array; moving an entry changes its displayed reference automatically.
Context still determines sanctions. Benefits and access policies use importance,
not disciplinary severity. IDs remain stable when entries are reordered.

Sections can also contain `groups`, each with an ID, title, description and rules.
Numbering continues through those groups within the section. SiteRulesList
renders ordinary and grouped entries with no business logic.

Seal of Quality is a separate public page at `/SealOfQuality`, backed by
`wwwroot/content/seal-of-quality.json`. Its title, kicker, introduction, section
descriptions and entries are editable there. It retains the golden design and
the existing animated ResourceQualitySeal component. Its five sections receive
their own letters and numbering; they are not part of the Rules index or JSON.
Both pages have links in the main navigation and footer. The guide documents
seal policies; it does not implement review or restrictions in the backend.
Its title and sealed resource names in the list use the shared QualityText
component: gold lettering plus a separate moving white reflection. It provides
solid gold fallback and wrapping. The logo and text share a 1600ms cycle with a
640ms sweep, synchronized through the document animation clock. Reduced motion
reduces its opacity while preserving speed; forced colors uses plain readable text.

Navigation, footer and the resource submission guidelines link to this page.
The private moderation proposal and editing guide are in
`docs/reglas-edicion-y-moderacion.md`. Suggested sanction durations are not
published in the public policy or enforced by these frontend changes.
