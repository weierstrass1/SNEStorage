# Site navigation

The header renders the SNEStorage logo on the left and account controls on the right.
SideNavigation renders site links in a fixed right rail on desktop. Its arrow button
minimizes the rail to 72px with labeled icons, or expands it to 232px. MainLayout
holds only this UI state in its code-behind; it remains while navigating in that layout.
Mobile navigation always displays full labels in the drawer.
The sticky top bar spans the full viewport width. The rail starts below it using
--site-header-height as its top offset; only page content and the footer reserve
the rail width. Both areas use --site-divider-color for their borders.
Below 992px, the Menu button below the header opens the same links in a Bootstrap
offcanvas drawer from the right. Bootstrap handles focus, Escape, the backdrop and scroll locking.
The UI-only side-navigation.js closes the drawer after choosing a link, back/forward
navigation or resizing to desktop. It performs no API requests.

The account area contains AccountNavigation: Log in/Sign up or the existing account
button with username color, avatar, View profile and Log out. There is one account
component for all screen sizes. MainLayout reserves the rail width for page content
and the footer; narrow screens retain the full content width.

## Account navigation

MainLayout delegates account UI to AccountNavigation. Initially, a neutral
placeholder reserves space until the browser verifies the current API session.
The circuit's initial authentication state is not used to display a user: it can
be stale after logout. Browser requests to GET /api/profile/me use current cookies
and bypass HTTP caching. A shared JavaScript module keeps a validated response
in browser memory for 15 seconds to resolve and refresh that UI:

- HTTP 200 with a UserInfo (userId): display UserSummary.Username, UserSummary.UsernameColor
  and UserSummary.Avatar (FileResponse). The root UsernameColor and Avatar remain
  fallbacks for the profile data; ApplicationUser navigations are ignored in JSON.
- HTTP 401: display Log in and Sign up; remove the old name and avatar.
- Other statuses, missing data or a network failure: retain the last verified account
  display, or show an unavailable message with Retry/Log in if not verified yet, and log the status.
  A missing profile or failed database query does not
  prove login or logout. No diagnostic button replaces the user menu.

Navigation reuses a fresh snapshot. Window focus and visibility restoration
trigger checks when it expires. The header, page guard, resource detail and
CheckEmail share a pending request through account-session.js. Disposing one
consumer only cancels its own wait. The request remains available for the next
page even if no consumer is attached during navigation. It is aborted when the
document closes, the session is invalidated, or its 30-second deadline expires.
Timeouts are reported as 504; a server cancellation (499) is displayed as a
recoverable error instead of leaving the menu checking indefinitely.

A protected API's 401 response, a successful profile edit or a different account
announced by another tab invalidates the snapshot. BroadcastChannel messages
cannot set account state; fresh browser cookies resolve it through the controller.
Login/logout use full document navigation, which discards that document's cache
and announces the newly verified identity to other tabs. Back/forward document
restoration also invalidates it. No profile data is persisted in localStorage,
sessionStorage or a server cache. Errors are never cached as logout.

The resource detail keeps its confirmed identity, comments and draft during a
recheck. It resets account-dependent content only after a confirmed change.
No periodic polling is added to the header; CheckEmail retains its existing
visible-page monitor but reads the same shared cache.

AccountMenu only renders the supplied username/color/avatar and the existing logout
form, including its antiforgery token. The profile response also supplies the
avatar, avoiding a second profile query. Logout performs a regular HTTP POST so
the redirect starts a fresh Blazor circuit. No local flag simulates a logout.

This updates the header only; it doesn't replace AuthenticationStateProvider or
alter authorization on pages/controllers. The backend remains responsible for
the session, token lifetime and signing out cookies. LoginController.Logout
currently requires Bearer authentication: an expired/missing JWT can reject the
request before AccountService.Logout clears the Identity cookie. The backend
owner can authorize this web logout using IdentityConstants.ApplicationScheme.

No backend files were modified.

## Seal of Quality navigation

The dedicated link uses a gold surface, the existing 28px seal logo, a soft glow
and a diagonal highlight. Text stays above the highlight for contrast; hover,
focus and active states retain the gold palette. The decorative icon is hidden
from assistive technologies because the link already has its text label.
Reduced motion reduces reflection opacity while retaining the requested speed.
The button respects OS forced colors.
The guide and sealed resource names in the list and detail share QualityText. It separates
the gold lettering from a narrow white reflection, preserving wrapping and
exposing the text once to assistive technologies. ResourceQualitySeal uses a
feathered ivory/gold reflection moving from top left to bottom right.
The isolated stylesheet URL is versioned to refresh previously cached CSS.

`css/quality-shine.css` defines the shared 1600ms cycle. Every reflection sweeps
from 4% to 44% (640ms), then pauses. `js/quality-shine.js` aligns their CSS
animation start times to the browser document clock on animationstart, including
late Blazor renders and list blocks. It adds no frame timers or HTTP requests.
The original image mask confines the logo reflection to its opaque pixels.

## Shared control appearance (2026-10-09)

App loads `css/controls.css` after the isolated component stylesheet. Its palette
in `.app-shell` covers native and Blazor text fields, selects, file/color inputs,
checkboxes and radios, including placeholders, autofill, focus, validation and
disabled/read-only states. It preserves page-specific input dimensions.
Picker triggers use the same palette; quality requests retain their gold accent.
The document declares a dark color scheme for native browser controls. Native
input elements/bindings remain intact, and OS forced-colors mode uses native
checkbox/radio rendering. ProfileEdit and the comment composer use `::deep`
anchored to their native wrapper so styles reach Blazor-generated controls.
