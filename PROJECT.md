# SNEStorage

## Architecture

- ASP.NET Core 10 web application with a Blazor Web App using Interactive Server rendering.
- ASP.NET Core Identity cookies serve browser sign-in; JWT endpoints remain for the API.
- `SnestorageContext` is currently configured with EF Core InMemory. Lookup catalogs are seeded when the app starts in this mode.
- Uploaded files and generated video posters are stored under `SNEStorage/App_Data/files`, outside `wwwroot`.
- The visual system follows the supplied neon and 16-bit landing mockup, with project-owned CSS and image assets.

## Browser pages

- `/`: landing page and five newest resources that the current visitor may list.
- `/Resource`: searchable, filterable, sortable, paginated resource catalog. Ordinary visitors see public resources; moderators and admins can list all visibility levels.
- `/Resource/{id}`: detail page, preview carousel, download, tags, authors, ratings, comments and nested replies.
- `/Resources/Create`: authenticated resource submission with visibility, tags, authors, content warnings and media previews. Any signed-in member can submit; the uploader is stored separately from resource authors.
- `/Login`, `/Register`, `/Profile`, `/Profile/Edit`, `/Rules`, `/Error/{code}`: account, profile, rules and error pages.
- Private resources are visible only to their submitter, registered authors, moderators and admins. Unlisted resources stay out of catalogs and can be opened through their direct link.
- Browser video uses the native HTML5 player. It does not autoplay or preload the video; a poster is generated from the first frame when the uploader selects the video.

## Editable site copy

Edit these UTF-8 text files and restart the app to update the corresponding copy:

- `SNEStorage/Content/landing.txt`
- `SNEStorage/Content/rules.txt`
- `SNEStorage/Content/disclaimer.txt`

The compact and full brand images are in `SNEStorage/wwwroot/images/logo-box.png` and `logo-full.png`.

## Data and file storage

- `CatalogInitializer` seeds visibility, file type, timezone, game and resource type lookups only for the current InMemory provider.
- The InMemory provider loses users, resources, ratings and comments when the process stops. SQL Server is not configured as the active provider yet.
- Migration `20260930120000_ResourceMediaAndCommentReplies` adds the resource preview gallery and comment reply relationship for `SnestorageContext`.
- Download and preview endpoints check the resource visibility before serving private files. `/api/file` requires an authenticated JWT.

## Verification status

- `dotnet build SNEStorage.sln --no-restore --verbosity quiet -clp:ErrorsOnly` succeeds with 0 errors and 24 existing nullable-reference warnings.
- The E2E runner has not been run against these views. See `TEST_READY.md`.
