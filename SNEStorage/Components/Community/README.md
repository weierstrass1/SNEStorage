# Community page

Public route: `/Community`. The sidebar and footer link to this page. The footer
no longer has a separate hardcoded Discord invite.

Edit `wwwroot/Content/community.json` to change the invitations and page copy:

- `servers`: the server panels, in JSON order. SNEStorage is first, followed by
  SNES Office. Each entry has `name`, `label`, `description`, `discordUrl`,
  `logoUrl`, and `joinLabel`. Set `primary` to true for the featured server.
- `linkNote`: the note below each invitation.
- `title`, `kicker`, `introduction`: the page heading.
- `sectionsTitle`, `sections`: the cards below it, in JSON order. Keep at least one
  section with `title` and `text`; add or remove cards as needed.
- `guidelinesText`, `guidelinesLinkLabel`: the note and link to `/Rules`.
- `unavailableText`: shown if an invitation is missing or is not an HTTPS Discord URL.
- `connectionTitle`, `connectionIntroduction`, `connectionGuides`: instructions for
  linking from Discord or from SNEStorage. Each guide has a `title` and ordered
  `steps`. Each step has `text` and optionally `linkLabel` and `linkUrl`.
  State the shared prerequisite first: a registered SNEStorage account with a
  confirmed email. Keep both guides short and assume the reader already has
  that account; account creation is not part of these linking instructions.
  Links accept `/Profile/me`, `/Profile/Edit`, or HTTPS Discord URLs.
  The profile button is named **Connect to Discord**; switching accounts uses
  **Change Discord account** in Edit profile. Keep the Discord guide to the three
  steps specified by the owner: sign in to SNEStorage, join the server, read the
  rules and press **Allow Access**. The website guide describes connecting the
  current Discord account, opening the server and pressing **Allow Access**.

All copy is plain text. The external invitation opens in a new tab with
`noopener noreferrer`. Editing this JSON does not change the privacy policy's
separate contact link, which is configured in `privacy-policy.json`.

Community.razor contains the markup, Community.razor.css the appearance, and
Community.razor.cs only selects the file and load-error text. CommunityDocument
is a frontend presentation type, not an EF model or API DTO. StaticDocumentPage
already handles loading the static JSON, retries and cancellation in the browser;
this page adds no controller, service, database access or account requirement.
