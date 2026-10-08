# Frontend page access

MainLayout renders PageAccessGuard around the routed page. Routes supplies the
current RouteData; the layout, account menu and footer remain mounted while the
guard loads or denies a page. C# stays in code-behind files.

| Pages | Access |
| --- | --- |
| `/`, `/Home/Index`, resource list aliases, privacy, errors, not found | Public |
| Login, register, email confirmation, check email | Public account workflow |
| `/Resource/{id}` | Public route; ResourceController applies resource visibility |
| `/Profile`, `/Profile/Edit` | Verified current account |
| `/Resource/Create`, `/Resources/Create` | Verified current account |
| `/Resource/{id}/Edit` | Verified account and resource editing permission |
| `/Resource/{id}/Versions/Create` | Verified account and resource editing permission |
| `/Resource/{id}/Versions/{versionId}/Edit` | Verified account and resource editing permission; editor validates the version belongs to the resource |
| `/Resource/Created/{id}` | Verified account and resource editing permission |

Private page classes carry `[RequireAccount]`; resource management pages carry
`[RequireAccount(ResourceManagement = true)]`. Add this frontend metadata to
future private pages. Future public profiles should be separate from My Profile.

## Controller connections

- The browser requests `/api/profile/me` with fresh cookies and no response cache.
  Initial circuit claims alone never unlock a private page.
- Management pages also request `/api/resource/{id}`. The shared existing
  ResourceManagementAccess helper mirrors the service's presentation conditions:
  resource author, resource submitter, or submitter of any resource version.
- A successful check supplies AccountPageContext as the named cascade
  `VerifiedPageAccount`. Profile and its editor use that UserInfo; ResourceEditor
  uses the already loaded Resource on first load. No database calls or service
  calls are introduced in components.
- Unverified pages display a loading/error panel. Missing session or editing
  permission displays 403. Initial anonymous HTTP rendering sets the response to
  403 when headers remain writable. Interactive navigation displays the error
  panel; it cannot change an HTTP response that has already finished.
- Focus, visibility, account broadcasts and rejected API sessions trigger fresh
  checks. These events only request verification; they do not authorize a page.
  Confirmed logout, account replacement or lost editing permission removes the
  previous user's page. Transient load failures do not discard a verified draft.
- Requests are scoped to each component; generation checks ignore responses from
  previous navigation. Disposal tolerates a disconnected Blazor circuit.
- Resource details remains public and rechecks its account state to update editing
  controls after session changes. Confirmed logout/account replacement also reloads
  the resource through the controller, which rechecks private resource visibility.
  Writes still go through the existing controllers.
- Its initial resource and account requests run in parallel. The resource content
  renders when its own response arrives; editing controls wait for account verification.

## Backend review (read only)

ResourceService.CheckResourceIsEditable already checks authors and both submitter
cases. CreateResource and CreateVersion require the JWT authentication scheme.
ProfileController requires authorization and its actions use JWT authentication.

Owner action still needed: add
`[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` to
ResourceController.Edit and EditVersion, consistently with the creation actions.
Their service checks already reject callers without editing permission. The
frontend guard is presentation control; API security remains the backend's job.

No controllers, services, DTOs, models, startup or migrations were edited for this
change. Compilation verifies Razor/C# integration; session transitions, permissions
against a running database, and Opera rendering have not been exercised here.

## Seal of Quality

ResourceQualitySeal uses the existing logo and a masked CSS metallic sheen. It is
shared by the title badge, version status badges and ResourceQualityRequest in
creation/version forms. The reflection moves diagonally from the upper left to
the lower right. It remains animated with reduced motion enabled, as explicitly
requested for this decorative badge. Resource editing does not change version
quality fields.

## Resource display and media requests

Latest download is directly beneath the description in the overview panel. The
preview carousel sits centered in a separate full-width section, with a 256x224
image stage and a larger video stage after Play.

The carousel reuses File metadata already provided in ResourcePreviews. Recognized
images request their bytes through the protected download action without a second
metadata request. Recognized videos request no bytes until Play. A metadata lookup
is retained only when the detail payload lacks usable file metadata. No physical
Storage path is used as a browser URL, and the byte endpoint still checks permissions.
