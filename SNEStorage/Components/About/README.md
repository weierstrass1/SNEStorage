# About page

The public `/About` page reads `wwwroot/Content/about.json` locally during component initialization using `StaticDocumentPage`. The first render includes the text, without browser JSON requests or a shared server cache. It does not call business services or require a logged-in account. The creator's profile loads separately.

Edit the JSON to change the introduction, section cards, creator biography, project badges, or links. Keep at least one item in `sections`; the shared loader expects a nonempty section list. Creator links must be absolute HTTPS URLs. Text is displayed as plain text, without interpreting HTML.

The initial creator biography is paraphrased from https://anonimzwx.me/ and the creator's interview at https://gamingreinvented.com/interview/lets-interview-smw-asm-guru-anonimzwx/ (August 28, 2025). The user supplied the alias and role as SNEStorage creator. No personal email, legal name, or location is included.

Presentation is in `Pages/About.razor` and `Pages/About.razor.css`; the code-behind only selects the document and load error message.
