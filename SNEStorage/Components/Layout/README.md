# Account navigation

MainLayout delegates account UI to AccountNavigation. Initially, a neutral
placeholder reserves space until the browser verifies the current API session.
The circuit's initial authentication state is not used to display a user: it can
be stale after logout. Browser requests to GET /api/profile/me use current cookies
and disable caching to resolve and refresh that UI:

- HTTP 200 with a UserInfo (userId): display UserSummary.Username, UserSummary.UsernameColor
  and UserSummary.Avatar (FileResponse). The root UsernameColor and Avatar remain
  fallbacks for the profile data; ApplicationUser navigations are ignored in JSON.
- HTTP 401: display Log in and Sign up; remove the old name and avatar.
- Other statuses, missing data or a network failure: retain the last verified account
  display, or the neutral placeholder if not verified yet, and log the status.
  A missing profile or failed database query does not
  prove login or logout. No diagnostic button replaces the user menu.

Recheck on navigation, window focus, visibility restoration, a protected API's
401 response, and account changes announced by another tab through BroadcastChannel.
The channel only triggers another server check; messages cannot set account state.
No periodic polling keeps an idle server session alive. Only one status request
runs at a time; events during a request schedule a fresh check when it finishes.
Disposal removes listeners, closes the channel and cancels the pending request.

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
