# Community page

Public route: `/Community`. The sidebar and footer link to this page. The footer
no longer has a separate hardcoded Discord invite.

Edit `wwwroot/Content/community.json` to change the invitation and page copy:

- `discordUrl`: the HTTPS Discord invite.
- `discordTitle`, `discordDescription`, `joinLabel`, `linkNote`: the main panel.
- `title`, `kicker`, `introduction`: the page heading.
- `sectionsTitle`, `sections`: the cards below it, in JSON order. Keep at least one
  section with `title` and `text`; add or remove cards as needed.
- `guidelinesText`, `guidelinesLinkLabel`: the note and link to `/Rules`.
- `unavailableText`: shown if the invitation is missing or has an invalid scheme.

All copy is plain text. The external invitation opens in a new tab with
`noopener noreferrer`. Editing this JSON does not change the privacy policy's
separate contact link, which is configured in `privacy-policy.json`.

Community.razor contains the markup, Community.razor.css the appearance, and
Community.razor.cs only selects the file and load-error text. CommunityDocument
is a frontend presentation type, not an EF model or API DTO. StaticDocumentPage
already handles loading the static JSON, retries and cancellation in the browser;
this page adds no controller, service, database access or account requirement.
