# Resource submission frontend

## Comment read contract

CommentApiClient reads CommentReadDto from GET /api/comment/resource/{resourceId}
and GET /api/comment/{commentId}/replies. Cards render Likes, Dislikes, MyVote and
HasReplies directly; lists do not reconstruct Score.LikesUsers or Childrens.
MyVote=Remove means no vote. Anonymous, own and deleted comments have no vote controls.
Reply forms, expansion, automatic page loading and scrolling to a new comment remain.

Mutation responses keep their existing controller contracts: new resource comments
return ResourceComments, replies return CommentComment, and votes return LikeUsers.
CommentPresentation adapts a newly created comment to the read DTO and replaces
immutable DTOs after confirmed actions. It has no database or business service access.
Votes update only after the controller confirms success; the GET remains the source
for aggregate counts after refresh. Deleted comments are shown only while HasReplies
is true. Edit/delete refresh the loaded pages without repeating the mutation.

## Resource text validation

Creation and editing share browser validation in resource-form-validation.js:
names use 2–100 letters/digits/internal spaces, repository URLs use HTTP/HTTPS and
at most 2,048 characters, and descriptions and changed release notes use 10–10,000
characters. Empty repository links are permitted by the frontend to clear an existing
link; ResourceService.Edit must also accept that empty-string contract. Unchanged
repository links and unchanged version labels/release notes are omitted when applicable.
Resource errors are presented using the controller-prefixed enum names.

## Version input

`ResourceVersionInput` is shared by resource creation, version uploads and version
editing. Its markup, C# presentation code and styles are separate files. The controls
combine a base number (`1`, `1.2` or `1.2.3`), a Stable/Alpha/Beta/RC selector and an
optional suffix number into the existing single multipart `Version` field. Examples:
`1-alpha.10` and `2.50-beta`. The complete label is limited to 25 characters and each numeric
component contains 1–4 digits, including leading zeros. Browser validation reads the visible controls
before submission. An unchanged legacy label is omitted when editing, allowing release
notes to be updated without renaming that version. Backend validation remains authoritative.

## Routes and controller contract

- Form: `/Resource/Create`. The previous `/Resources/Create` URL is kept as an alias.
- Options: `GET /api/resource/options`, using the existing `ResourceCreateOptionsDto`.
- Games: `GET /api/videogames/search?likeString={search}&maxAmount=10`, returning Videogame entities.
- User authors: `GET /api/user/search?likeString={search}&maxAmount=10`, consuming Id and Username from UserNameDTO.
- External author suggestions: `GET /api/externa-author?likeString={search}&maxAmount=10`, returning strings.
- Resource dependencies: `GET /api/resource/search?likeString={search}&maxAmount=10`, returning a JSON list with Id and Name.
- Submission: `POST /api/resource/create/submit`, with multipart form data bound to `ResourceCreateDTO`.
- Successful response: the saved Resource as a JSON object with a positive `id`, with HTTP 200 or 201. A plain JSON ID is also supported.
- Confirmation: `/Resource/Created/{id}`. After three seconds, it opens `/Resource/{id}`.

The frontend supplies Id=0, Name, Description, VideogameId, Version, ChangeLog, SealOfQualityRequested, VisibilityId,
RepositoryURL,
Types (repeated numeric fields), Tags (repeated string fields), IAmAuthor and all five
content-warning booleans, DependencyIDs, indexed ExternalDependencies and Previews.
The archive is sent under **File**, matching the DTO's
IFormFile property. The file travels directly from the browser to the controller;
the component does not open a browser stream or create an IFormFile.

Unchecked booleans are explicitly sent as false. AuthorsIDs contains the selected
SNEStorage user IDs as repeated fields. ExternalAuthors contains whole author names
as repeated fields, including a nonempty name still being typed when submitted.
Empty selections use the DTO's default empty arrays. When IAmAuthor is true, the
backend should associate the authenticated user as an author; the frontend excludes
that account from AuthorsIDs to avoid crediting it twice. No user ID is supplied by
the browser for the IAmAuthor association. Id and CreatedAt are assigned by the backend.

## Backend work to complete (frontend does not implement this)

GetCreateOptions supplies resource types and visibility choices. Videogames stays
empty in that DTO: the picker fetches initial IDs 1 and 2 and search results directly
from VideogameController. Keep the complete game-catalogue query out of GetCreateOptions
to preserve the reduced initial load.

CreateResource now calls ResourceService.Create and requires JWT authentication.
Remaining backend responsibilities (owned by the project owner):

1. Reject unauthenticated submissions before processing files or data (JWT authorization returns HTTP 401).
2. Validate the antiforgery token; the form includes AntiforgeryToken.
3. Validate the DTO, archive format and allowed visibility. Maximum archive size is
   250 * 1024 * 1024 bytes; at most 20 previews, each at most 5 * 1024 * 1024 bytes.
4. Save the resource, its first ResourceVersion, types, tags, authors, previews and dependencies.
5. Return the saved resource's real ID in its response. Return only public data, excluding Identity password hashes and security stamps from related users.

The UI accepts ZIP, RAR, 7Z, TAR, GZ and TAR.GZ archives, up to 250 MB. Previews accept
PNG, JPG/JPEG, GIF, WEBP, AVIF, MP4, WEBM and OGV. They can be added in multiple batches
and individually removed. The browser retains their File objects and sends them as
repeated Previews parts; no browser file streams are read by the C# component.
Thumbnail media boxes are 160px high and use object-fit: contain so large images/GIFs
do not expand the layout. Videos have native controls and preload="none"; changing
tabs pauses videos in hidden panels. Object URLs are revoked on removal and disposal.

The owner must configure the action/server upload limits to accept the complete
request: a 250 MB archive plus twenty 5 MB previews can total 350 MB before overhead.
For example, set RequestSizeLimit to 400 * 1024 * 1024 and RequestFormLimits with
MultipartBodyLengthLimit of 250 * 1024 * 1024 on CreateResource, and configure any
hosting/proxy limits accordingly. These transport limits do not replace per-file
validation in the service.

ExternalDependencies binds using contiguous indexed names such as
ExternalDependencies[0].DependencyName, .DependencyURL and .Mirror1 through .Mirror4.
Names and primary URLs are required; mirrors are optional. HTTP/HTTPS URLs are checked
locally, without contacting the external website. Resource dependencies are stored
as unique long IDs in DependencyIDs. The backend must validate their existence,
visibility and dependency rules. Resource.Name now maps to nvarchar(50); the frontend
limits names to 50 characters. Keep the database schema consistent with that mapping.

On a direct anonymous HTTP request, CreateResource sets status 403 during prerendering
when the response is still writable. Interactive navigation displays the same denial
screen without loading the form. The controller must separately protect the POST;
the UI restriction is not a replacement for that check.

The individual `/Resource/{id}` view calls `GET /api/resource/{id}` using browser
credentials and reads ResourceReadDTO. The controller handles public/unlisted/private
access and supplies CanEdit. Editing links use that flag and the account verified
by GET /api/profile/me. The component does not reconstruct contributor permissions
from authors or versions; mutation endpoints enforce their own permissions.

Public account data comes from each relation's UserSummary (UserNameDTO), not its
ignored ApplicationUser navigation. ResourceDetails uses that summary for author
names, profile links, username colors and comment names; ResourceEditor uses it for
the initial author picker labels. Relation Id and UserId remain available for editing
and permission checks. Missing summaries display neutral fallback labels.

Profile and ProfileEdit also read UserSummary, including its optional Email for the
signed-in user's account. The backend decides whether Email is visible: the current
mapper returns it for public email visibility or for the account owner when given
AccountService. The frontend does not reconstruct an omitted email. Avatar in
UserNameDTO is FileResponse, containing Id, URL and FileTypeId without model navigations.
Profile summaries must be populated by the backend; adding NotMapped alone does not
populate them. The frontend does not query the database or reconstruct these summaries.
ResourceDetails does not display or request author avatars. Comment avatar presentation
remains pending while comment support is on standby; the backend can retain those avatars.

Previews and version downloads use the agreed contract
`GET /api/resource/{resourceId}/file/{resourceFileId}` for file metadata as JSON, and
`GET /api/resource/{resourceId}/file/{resourceFileId}/download` for downloads.
resourceFileId is File.Id (the ID associated with a preview or version).
ResourceMediaCarousel reads that JSON, determines the media format from FileType or
the stored filename, and fetches the bytes from the existing download action. A
browser object URL lets images and the native video player display those bytes even
when the download response has an attachment filename. Storage paths are not used as
public URLs. Both actions must check
that the file belongs to the resource and that the caller can access the resource.
Return the correct MIME type, and enable range processing for video seeking.
Never expose Storage as an unrestricted static-file directory.

ResourceMediaCarousel displays one preview at a time. Video metadata can load when
the slide opens, but video bytes are fetched only after pressing Play; the native
player provides playback, pause, seeking and volume. Preview downloads are bounded
to 5 MB. Switching previews cancels obsolete requests and releases the previous
object URL; disposal releases it too. A first-frame video poster requires
a server-generated thumbnail/URL; the current File/ResourcePreview models provide no
poster field. Until one is supplied, the UI displays a Play placeholder without
downloading video data automatically.

ResourceReleaseList displays release history and individual download links.
ResourceCreated already redirects to `/Resource/{id}` after three seconds and cancels
that redirect when disposed; its implementation was kept.

Get supplies general information, Videogame, Visibility, Types (including category
fields) and Tags through ResourceReadDTO. Authors, dependencies, rating, latest version
and previews load through their separate actions with section loading/error states.
Version history loads when its tab opens. Comments, replies and votes use their
existing controller actions. The previous Used by block had no populated response
or dedicated endpoint; it is not rendered with this DTO.

Community credits display UserSummary.Username and link to `/Profile/{userId}`
using UserSummary.Id. PublicProfile reads `/api/profile/{userId}`; the backend
replaces Birthday before returning public data and respects email visibility.
The private `/Profile/me` page reads the verified account and displays its birthday.
Both pages share UserProfileView. External credits remain plain author strings without a
SNEStorage profile link. Load ResourcesAuthors -> User -> UserInfo in the backend
to populate those summaries; ApplicationUser is ignored when serializing the response.
ResourceExternalAuthors.Author is a scalar string and requires no ThenInclude.

ApiErrorPresentation maps ResourceServiceResult, AccountServiceResult and
EmailServiceResult by name or numeric value, with HTTP status fallbacks. The error
mapping includes ResourceServiceResult.FileNotFound with a dedicated message. The error
page accepts `/Error/{status}?source=resource&error=DBError` (source can also be
account or email). Numeric enum values need the source to avoid ambiguity between
enums. Resource requests show the same messages inline, preserving unsaved form data.
Unknown codes or non-JSON responses show a generic message rather than raw server output.
NotFound.razor is registered as Router.NotFoundPage for .NET 10. Direct unknown HTTP
URLs require status-code re-execution middleware in Program.cs if they should also
render that page; the frontend does not modify the middleware pipeline.

## Account summaries and current review

The /api/user/search response returns UserNameDTO. The author picker consumes only
Id and Username; it does not fetch avatars or require ApplicationUser in that JSON.
Names, colors and optional profile email elsewhere use UserSummary. JSON-ignored
ApplicationUser navigations remain available to backend queries and permissions.

The source cleanup audit and remaining summary-loading corrections are recorded in
[CLEANUP_REVIEW_2026-10-06.md](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Components/Resources/CLEANUP_REVIEW_2026-10-06.md).
Earlier backend review documents are historical findings, not a current checklist.

The backend must still validate selected user IDs and remove duplicate author associations.

## Editable content and presentation components

- CreateResource has six persistent tab panels: General (types, tags, game,
  visibility), File and release, Previews, Authorship, Dependencies, Content warnings.
  General uses two columns on larger screens, a shorter resizable description, and
  collapsible visibility guidance. Switching tabs does not recreate inputs or discard files/selections.
  Tabs support keyboard arrows/Home/End, and Previous/Next buttons. Submission opens
  the tab containing the first invalid native field before focusing it. The form
  uses novalidate because hidden required fields cannot be focused until revealed.
- ResourceSubmissionRules: collapsible submission guidelines read from the same
  Content/site-rules.json used by /Rules. Includes distribution and authorship,
  resources and previews, and visibility and permissions; ROM restrictions are
  highlighted. The full rules link opens a new tab to preserve form input.
- SearchableVideogamePicker: initial Any/Other choices, remote search after two
  characters and a 300 ms pause, ten results per request, browser-request cancellation,
  rejection of obsolete responses. Native oninput binding updates Blazor's input
  value tracking before the asynchronous search, avoiding stale text writes while
  typing quickly. The selected game appears in a compact field;
  clicking opens the search and choices. Selecting a game closes the dropdown.
- ResourceTypePicker: a compact multi-select field showing chosen type names. Options
  open on mouse hover, click or keyboard; native checkboxes send repeated Types values.
  Create and Edit deserialize ResourceCreateOptionsDto directly, with its
  ResourceTypeDTO entries. Types are grouped by CategoryId/CategoryName from
  GET /api/resource/options. Each
  category starts collapsed and shows a selected/total count. Clicking a row label
  toggles its checkbox. Empty category names use the neutral label Other types.
- ResourceContentWarnings: shared Content panel for CreateResource and ResourceEditor;
  consistent vertical selectable rows, labels and multipart boolean names.
- ResourceQualityRequest: shared request checkbox for the first release during
  resource creation and for version creation/editing. Resource editing does not
  modify version quality fields. Uses the guide's gold title, animated seal and
  optional review explanation, with a /SealOfQuality link that opens a new tab.
- ResourcePicker: shared button and floating options panel. Blazor owns open/closed
  state, click/hover/Escape actions and the hidden/aria-expanded attributes. JavaScript enhances outside-click
  and focus-leave dismissal, Escape dismissal with focus restoration, and touch support.
  resource-pickers.js contains only browser dropdown and focus behavior.
- ResourceTokenInput: comma/Enter input, removable tags and duplicate suppression.
- ResourcePreviewPicker: local media selection, thumbnail display and removal.
- ResourceDependencyPicker: remote resource search after two characters/300ms,
  duplicate suppression and removable selected dependencies.
- ResourceExternalDependencyEditor: external names/URLs and up to four mirror links.
- ResourceAuthorPicker: reusable multi-author selector with two modes. SNEStorage
  authors are selected by ID; external authors accept whole free-text names, Enter/Add
  and suggestions. Selected authors are removable and hidden from suggestions;
  external names are deduplicated ignoring case and surrounding whitespace.
  Both searches start after two characters and a 300 ms pause, with ten results.
  Only the typing delay is cancelled; HTTP requests finish and obsolete responses
  are discarded without cancelling server queries when another character is typed.
- All presentation logic is in the corresponding .razor.cs files.
- resource-create.js: browser HTTP/form transport and preventing Enter from submitting
  the form while adding a tag, native validation across tabs, tab focus and video pausing.
- resource-previews.js: browser File storage, count/size/format checks, object URL
  lifecycle and multipart preview attachment.
- videogame-search.js: browser HTTP transport to VideogameController with cancellation.
- author-search.js: browser HTTP transport to UserController and ExternalAuthorController,
  plus /api/resource/search transport and prevention of form submission when Enter is
  pressed in author or dependency search fields.

## Individual resource layout and editing

The October 6 model/service review is recorded in
[BACKEND_REVIEW_2026-10-06.md](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Components/Resources/BACKEND_REVIEW_2026-10-06.md).
Resource submission uses a frontend form-state class while fields are incomplete;
the browser sends the ResourceCreateDTO contract as multipart data without constructing
IFormFile objects. Name and Description have minimum lengths of 2 and 10, respectively;
Version and ChangeLog are now required. RepositoryURL accepts an optional HTTP/HTTPS
link up to 2,048 characters. Names allow 2–100 letters, digits and internal spaces;
descriptions and changed release notes allow 10–10,000 characters. ResourceCreateDTO supplies SealOfQualityRequested for
the first release; the browser explicitly sends true or false. ResourceEditDTO
does not supply this field because it edits resource information, not versions.
ResourceDetails shows Resource.UpdatedAt and an approved-rules message only when
AlreadyModerated is true. There is no awaiting-review badge. The latest version's
SealOfQuality controls the existing seal image beside the resource title.
ResourceVersionStatus displays each version's own moderation, quality-review request
and awarded Seal of Quality beside the latest download and in version history.
History also shows ResourceVersion.UpdatedAt when present. The resource
editor includes RepositoryURL; only the version editor includes the quality-review
request from ResourceVersionCreateDTO/ResourceVersionEditDTO. The forms now submit
multipart requests to the ResourceController actions listed below.

- ResourceDetails displays the title and rating, community/external authors, then
  an Overview tab with game, types, tags, description, content warnings, dependencies,
  latest-version download and compact previews. Comments remain below those sections.
- Community author names use UserSummary.UsernameColor (validated hex, matching
  the existing profile presentation) and link to /Profile/{userId} using the summary ID.
- ResourceRating displays five empty stars, 0 / 5 and Not rated when no votes exist.
  Rated resources show their average and vote count from Score.ScoresUsers. Logged-in
  users can preview a 1–5 selection; Save rating is disabled until a rating contract
  is provided. It does not alter the returned score or persist a fake vote locally.
- Images/GIFs fit within a 256 x 224 stage. Video bytes still load only on Play;
  playback expands the carousel up to 640px, constrained to the available width.
  Switching to Version history pauses any playing preview.
- Version history has download links and Edit version / Add version links for
  signed-in authors or submitters of the resource or any version. Edit resource
  uses the same display condition. GET /api/profile/me verifies current browser
  credentials before management controls become visible; the circuit's initial
  authentication state alone does not make those controls visible.
- Editor pages: /Resource/{id}/Edit, /Resource/{id}/Versions/Create and
  /Resource/{id}/Versions/{versionId}/Edit. ResourceEditor loads existing data through
  ResourceController and verifies current login plus authorship/submission for displaying the forms. Draft fields
  are presentation state, not backend DTOs. Resource editing has five persistent
  tabs: General, Previews, Authorship, Dependencies, Content. Version editing has a name and release notes; new versions
  also select an archive up to 250 MB. Other users see an access-denied panel.
- ResourceEditor submits through browser HTTP transport in resource-editor.js:
  - ResourceEditDTO: PUT /api/resource/{id}/edit.
  - ResourceVersionCreateDTO: POST /api/resource/{id}/version/create/submit.
  - ResourceVersionEditDTO: PUT /api/resource/{id}/version/{versionId}/edit.
  Version release notes are sent as ReleaseNotes, not the model's ChangeLog property.
  Files travel directly as multipart File parts without C# browser stream conversion.
  All resource content flags and new-version SealOfQualityRequested are sent explicitly.
  Version edits omit unchanged fields to preserve the DTO's null/no-change semantics;
  clearing a modified text field sends an empty string. RepositoryURL is also omitted
  when unchanged and sent empty when cleared. ReleaseNotes preserves its formatting.
- Editing tags/types, community authors, external authors and resource dependencies
  sends only additions and the original join-row IDs to remove, using ResourceEditDTO.
  ResourceEditorSelectionSnapshot retains the small original selection metadata needed
  for multipart serialization; it does not query the database or implement persistence.
  Existing author/dependency labels are hydrated from the resource response. Searches
  reuse the creation pickers; resource search excludes the current resource.
- External dependencies include editable names, URLs and four optional mirrors.
  Unchanged rows are omitted; changed rows are removed and re-added, because the DTO
  exposes additions/removals rather than an individual update operation. New entries
  use contiguous multipart indexes after unchanged entries are removed.
- ResourceExistingPreviews shows the retained existing previews using the protected
  media endpoints, with Remove/Undo before saving. The existing carousel loads only
  when the Previews tab opens. ResourcePreviewPicker adds new images/GIFs/videos;
  retained and new previews together are limited to 20, each new file to 5 MB.
  Undoing a removal never silently drops a newly selected file; if it exceeds the
  limit the user must remove a preview before saving. New files travel directly from
  browser File objects in multipart Previews, without C# stream conversion.
- Tabs retain all inputs and files, support arrows/Home/End and Previous/Next, pause
  hidden videos and close hidden pickers. Validation switches to the offending tab
  before focusing the field, including fields inside collapsed mirror sections.
- ResourceEditorResponse recognizes the existing Success enum responses for edits
  and the returned ResourceVersion ID/resource ID for a new version. Only a confirmed
  HTTP success displays the success panel and redirects to /Resource/{id} after
  three seconds. Errors preserve the draft and show the service enum message or
  model validation errors; network failures tell the user to check before retrying.
  Leaving the editor cancels its own requests/redirect, and JS disconnection during
  disposal is handled. Saving locks the controls and prevents duplicate submissions.
- Rating persistence still needs a controller contract. No backend code was changed.

### Backend adjustments pending in the saved files at connection time

The edit-version route, RemoveTagIDs/RemovedTypeIDs properties, author Includes and
RepositoryURL empty-string binding were corrected by the owner during integration.
The last saved source still needs these owner changes for the actions to persist
everything correctly:

- ResourceVersionEditDTO.Version and ReleaseNotes must be public get/set properties
  for FromForm binding (keep ConvertEmptyStringToNull=false).
- ResourceService.EditVersion still uses AsNoTracking; the modified version needs
  tracking in that write operation.
- The owner corrected ResourceService.Edit to assign the dependencies to
  ResourceDependencies while the tabbed resource editor was being implemented.
- AddVersion must copy dto.SealOfQualityRequested to the new ResourceVersion.
- Edit's tag deduplication still uses TagId before new join rows have their IDs
  propagated. Multiple new tags can have TagId=0 and collapse into one; deduplicate
  by the referenced Tag identity/name instead of an unset foreign key.
- Edit's preview FileId and new external-author/dependency Id deduplication have the
  same zero-ID issue: several additions can collapse into one before SaveChanges.
  The owner needs to change those keys for multi-item editing to persist correctly.
- Edit uses ResourceEditDTO.IAmAuthor. The self-author checkbox starts checked when the current account is an author. Unchanged self-credit omits IAmAuthor; changed self-credit sends true/false. Unchecking also sends the original author association in RemoveAuthorsIDs. The picker selects other accounts.

Compilation checks the code and Razor integration, not persistence against SQL Server.
No controller was invoked to mutate live resource data during this work.

## Resource details: independent section requests (2026-10-08)

- `ResourceDetails` renders the main `GET /api/resource/{id}` response immediately.
  The initial view no longer waits for profile verification or the section responses.
- `ResourceDetails.Sections.cs` orchestrates browser requests to the controller and
  keeps separate loading, data, and error states. It does not call services or EF.
- Overview requests run concurrently: `authors`, `external-authors`, `dependencies`,
  `external-dependencies`, `versions/latest`, `rating`, and `previews` under
  `/api/resource/{id}`. Each section displays `Loading…` and has its own retry action.
- Internal authors use `ResourceAuthorDTO`: its Id identifies the resource-author
  association, and UserSummary contains `UserNameDTO` with the user's Id/name/color.
  Internal dependency
  names and links use `ResourceDependencyViewDTO`. Ratings use `RatingReadDto`'s
  aggregate and current user's vote, without receiving the full vote collection.
- Saving a rating refreshes only `/rating`. A failed summary refresh does not imply
  that the successful rating submission failed.
- `/versions` loads on opening Version history. It also loads for authenticated
  visitors whose contributor status is not yet confirmed, because the current API
  does not provide a lightweight permission result for previous version submitters.
  Rating controls wait for those permissions; the public content remains usable.
- The download remains next to the description and the preview carousel remains
  centered. Until the history is available, downloads are explicitly labeled as
  downloads of the latest version rather than an incomplete total.
- Section requests have separate cancellation keys and a 30-second loading limit.
  Responses from a previous resource/page load cannot overwrite the current view.
- `GET /api/resource/{id}/previews` now returns `List<ResourcePreview>` with
  association IDs, FileIds, and File metadata. The existing carousel uses that data
  and requests video bytes only after Play.

## Editors and access after the read API split

- `PageAccessGuard` reads ResourceReadDTO and checks CanEdit after verifying the
  account. It does not fetch authors or version history to determine access.
- `AccountPageContext` passes the initial ResourceReadDTO to the editor, avoiding
  a repeated general-information request. This is the current page's view state.
- `ResourceEditor.Loading.cs` loads authors, external authors, dependencies,
  external dependencies, previews and editor options concurrently for resource
  editing. Add version needs no section requests; edit version reads history to
  locate the selected version using the available API contract. An explicit reload
  requests fresh responses; same-account access rechecks preserve the draft.
- The form is shown only after its required responses succeed. A failed read is
  presented with retry instead of rendering empty original selections.
- `ResourceEditorSelectionSnapshot` retains association IDs for removals and user/
  resource IDs for additions. In particular RemoveAuthorsIDs uses ResourceAuthorDTO.Id
  and AuthorsIDs uses ResourceAuthorDTO.UserSummary.Id. ResourceReadDTO.Tags.Id and
  Types.Id are association IDs; TagId and ResourceTypeId identify their catalog entries.
- `ResourceApiClient.cs` and `resource-read.js` are shared frontend HTTP transport
  helpers. They deserialize controller responses and coordinate cancellation/timeouts;
  they contain no queries, business operations, or calls to backend services.

## CAPTCHA (2026-10-09)

- `CaptchaPrompt` is a shared Blazor view with a separate code-behind for browser
  initialization/disposal. Its dialog uses English copy and preserves the form behind it.
- `captcha.js` lazily loads the official Turnstile script only when needed. It renders
  the widget explicitly, uses the public SiteKey from App's metadata, and handles
  cancellation, errors, expiry, retry and widget cleanup. The SecretKey never reaches JS.
- Register, resource creation/editing and version creation/editing request a fresh token
  at submission. Tokens travel in `X-Captcha-Token`, including the Register form's
  existing antiforgery field and multipart data. DTOs are unchanged.
- Resource comments, replies, comment edits, likes/dislikes and resource ratings make
  the ordinary request first. Only a `CaptchaRequired`/`CaptchaInvalid` rejection
  causes a challenge and one retry. The API owns the frequency policy and counters.
- HTTP writes are never repeated after network errors, unknown responses or 5xx.
  The server must only return CAPTCHA rejections before running the action.
- Registration still submits to RegisterController and follows its CheckEmail redirect.
  A failed registration redirect or CAPTCHA error preserves the current inputs/files.
- Removing/navigating away from the originating component aborts its pending challenge
  and request. No CAPTCHA token is persisted or shared with another operation.
- `docs/captcha-backend-integracion.md` contains the backend contract and proposed
  frequency implementation for the owner to apply, including deployment limitations.

## Resource library (2026-10-09)

- `/Resources` (also `/Resource` and `/Resource/Index`) reads
  `GET /api/resource/find` with query parameters matching ResourceFinderDTO,
  `Sorting.SortType`, `Sorting.SortOrderDescending`, `maxAmount` and `page`.
  Success is a plain ResourceFinderViewDTO array; errors use ResourceServiceResult.
- Resources.razor contains presentation; its code-behind coordinates UI state.
  ResourceListApiClient and resource-list.js provide browser HTTP with session
  cookies, cancellation and a 30-second timeout. They never call services directly.
- The default page is 20 resources, newest first. Pages of 100/200 request 50 at
  a time, with backend page = UI page * (UI page size / 50) + block index.
  All requests consecutive blocks of 50 without a page boundary. An observer
  prefetches near the bottom; Load more remains available for keyboard users.
  A failed block preserves existing rows and retries only that block.
- The API has no total count. Previous/Next navigation stops on a short block;
  if an exact full final page is followed by an empty page, the previous rows
  remain displayed and Next is disabled. No total or page count is invented.
- Filter edits are drafts until Apply filters. Types reuse the categorized picker;
  games reuse the searchable picker without automatically selecting Any (id 1).
  These options preserve the pickers' default behavior in creation/editing forms.
- Fixed single-game/type/category filters hide their corresponding columns.
  Names/colors, dates, latest archive, ratings, content flags and the quality seal
  come from the list response; no additional request is made for each row.
- Authors/types/categories expand inline. The first five alphabetical tags are
  always visible; additional tags use a native popover opened with a button and
  closed with Escape or outside click. Compact dates align left beside the tags
  or below them when space is limited; the download count follows the download button.
  Download links use `/api/resource/{id}/file/{MainVersion.Id}/download`.
- Name is the primary filter, always visible. Authors/tags, classification,
  rating, dates and content preferences are separate collapsed groups. Dates
  contain paired From/Through ranges for Created At and Last Version Created At.
- The author text field sends `AuthorLike`; the backend owner must expose it in
  ResourceFinderDTO and implement matching internal and external author names.

## Home latest resources

Home contains a logo cover and LatestResources only. Edit its introductory sentence
in Components/Pages/Home.razor. LatestResources reuses ResourceListRow and the
ResourceListApiClient; its code-behind only manages loading, errors and disposal.

The browser calls GET /api/resource/find with maxAmount=5, page=0,
Sorting.SortType=CreatedAt, Sorting.SortOrderDescending=true, and all five
CanIncludes* flags false. Filtering, sorting and pagination stay in ResourceController
and ResourceService. The request uses credentials: omit so it is anonymous: even
staff and contributors see only the public feed, rather than their private resources.
It does not change the user's session or the credentials of downloads/account requests.

The cover renders before the list arrives. There is one resource query, no options,
profile checks, previews or per-resource queries in this component. Failed loads
have a retry button; leaving the page aborts only this feed's request. No backend
files or DTOs were changed for the Home implementation.
