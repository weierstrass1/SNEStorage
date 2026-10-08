# Source cleanup review — October 6, 2026

This is a source-reference audit of the current checkout, including controllers,
services, models, DTOs, Blazor pages/components, JavaScript imports, project files
and test sources. Generated bin/obj output and migration snapshots were excluded
from obsolete application-code reference counts. No files were deleted. Backend
files were read only.

## Unused code candidates

Paths below are relative to the repository root.

| File or directory | Evidence and removal condition |
| --- | --- |
| `SNEStorage/DTOs/FileParams.cs` | FileParams appears only in its declaration; current file operations do not use this DTO. |
| `SNEStorage/DTOs/ViewModels/RecursoViewModel.cs` | RecursoViewModel appears only in its declaration. The MVC resource views it supported have already been removed. |
| `SNEStorage/wwwroot/lib/jquery/` | No application page, script or project configuration loads jQuery. References inside the vendor libraries themselves are not application usage. |
| `SNEStorage/wwwroot/lib/jquery-validation/` | No application import or script tag loads jquery.validate or additional-methods; the old MVC validation partial has been removed. |
| `SNEStorage/wwwroot/lib/jquery-validation-unobtrusive/` | No application import or script tag loads this library. Blazor forms and current browser validation do not use it. |
| `SNEStorage/wwwroot/lib/bootstrap/dist/js/bootstrap.js` | App.razor loads bootstrap.bundle.min.js instead. |
| `SNEStorage/wwwroot/lib/bootstrap/dist/js/bootstrap.min.js` | Not loaded; the bundled minified variant is loaded instead. |
| `SNEStorage/wwwroot/lib/bootstrap/dist/js/bootstrap.esm.js` | No module import uses this variant. |
| `SNEStorage/wwwroot/lib/bootstrap/dist/js/bootstrap.esm.min.js` | No module import uses this variant. |
| `SNEStorage/wwwroot/lib/bootstrap/dist/js/bootstrap.bundle.js` | The unminified bundle is not loaded. Its minified counterpart is used. |

The unused Bootstrap variants may be kept as distribution/source copies if desired.
Do not remove the whole Bootstrap directory: App.razor still uses its CSS and
bootstrap.bundle.min.js. A source map belonging exclusively to a deleted variant
may be removed together with that variant.

## Conditional cleanup

| File | Condition |
| --- | --- |
| `SNEStorage/Controllers/HomeController.cs` and `SNEStorage/Models/ErrorViewModel.cs` | Legacy MVC pair. HomeController returns View(), but its views have been removed and Program only maps attribute-routed controllers, with no MapControllerRoute. Blazor already renders Home, Privacy and Error. ErrorViewModel is referenced only by this controller; remove the pair together if discarding the old MVC Home implementation. |
| `workload-install.ps1` | Manual Samsung Tizen workload installer, unrelated to this ASP.NET Core/Blazor project. It is not part of the application build. Remove if it is no longer needed as a local setup utility. |
| `SNEStorage/EmailTemplates/Confirmation.txt` | EmailService reads Confirmation.html and constructs TextBody inline. The .txt template is not read by the application; retain only if wanted as a reference or future plain-text template. |

`SNEStorage/Database/seed_snes_catalog.sql` is a manual database seed script. Lack of
an application call does not make a maintenance script obsolete; keep it if it is
needed to recreate the catalogue.

## Custom JavaScript: all eleven modules are used

| Module in `SNEStorage/wwwroot/js/` | Consumer |
| --- | --- |
| account-navigation.js | AccountNavigation |
| author-search.js | ResourceAuthorPicker and ResourceDependencyPicker |
| check-email.js | CheckEmail |
| profile.js | Profile, ProfileEdit, account-navigation.js and resource-details.js |
| resource-create.js | CreateResource |
| resource-details.js | ResourceDetails and resource-editor.js |
| resource-editor.js | ResourceEditor |
| resource-media.js | ResourceMediaCarousel |
| resource-pickers.js | ResourcePicker, used by the game/type pickers |
| resource-previews.js | ResourcePreviewPicker and create/edit transport |
| videogame-search.js | SearchableVideogamePicker |

Do not infer that a Blazor page or controller is unused merely because another C#
file never names its class: routing discovers them. Test classes are likewise
discovered by the test framework. EF models and build methods registered in
SnestorageContext remain part of the database model even for unfinished features.
Scoped .razor.css files are bundled by Blazor without explicit stylesheet imports.

## Frontend contract updates

- Resource author names, profile links, username colors and comment names read
  UserSummary; editor author labels and search read Username, not ApplicationUser.
- Profile, profile editor and account navigation use the summary's username,
  username color, optional email and avatar metadata. Root UserInfo color/avatar
  are compatible fallbacks. ResourceDetails does not use author avatars.
- Profile color text and the editor's initial color now follow the same summary
  as the displayed name. Unchanged fields remain omitted from the edit request.
- Resource/detail/edit module imports now use the same profile.js version as the
  profile and navigation, avoiding duplicate module instances and stale imports.

## Remaining backend corrections for the owner

### ResourceService.Get: version submitter

The chain ResourceVersions -> File -> UserInfo loads the file's associated user
info, not ResourceVersion.SubmitterUser. The subsequent FillUserSummaries iterates
SubmitterUser and can receive null. Keep the file include and add a separate chain:

```csharp
.Include(r => r.ResourceVersions)
    .ThenInclude(v => v.File)
.Include(r => r.ResourceVersions)
    .ThenInclude(v => v.SubmitterUser)
    .ThenInclude(u => u!.UserInfo)
```

No avatar include is required. The comment query currently still includes
Comment.User.UserInfo.Avatar; remove that final Avatar include if the resource
response should not load account avatars at all. The frontend does not require it.

### AccountService.GetUserInfo: owner's hidden email

The mapper checks public visibility or accountService.GetUserID(), but the caller
currently omits accountService. Supply the existing service instance:

```csharp
user.UserSummary = UserNameDTO.FromUserInfo(user, this);
```

This lets the current owner see their own email even when it is not public. The
frontend leaves an omitted email unavailable; it cannot recover JsonIgnore data.

These are instructions for the backend owner. No backend implementation was edited.
