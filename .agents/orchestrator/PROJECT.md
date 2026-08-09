# Project: SNEStorage Frontend Redesign

## Architecture
- Target Application: ASP.NET Core MVC (`SNEStorage`)
- Backend Logic: Preserved C# controllers (`HomeController.cs`, `ResourceController.cs`) & Models (`Resource.cs`, `ErrorViewModel.cs`)
- Frontend Layout: Pure Vanilla CSS layout (`/wwwroot/css/site.css`), retro 90s SNES theme, CRT overlay scanline FX, floating logo centerpiece (`logo_final.png`), zero Bootstrap or external framework classes in Razor views (`.cshtml`).

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| 1 | M1: UI Architecture Analysis | Audit existing `.cshtml` views, CSS references, asset paths, and structure design specification | None | DONE |
| 2 | M2: Vanilla CSS & View Rebuild | Purge Bootstrap, implement custom 90s SNES Vanilla CSS design, rebuild `_Layout.cshtml`, `Home/Index.cshtml`, `Resource/Index.cshtml`, `Error.cshtml` | M1 | DONE |
| 3 | M3: Build & Design Verification | Run `dotnet build`, verify zero errors, audit Bootstrap absence, test app runtime, perform forensic integrity audit | M2 | DONE |

## Interface Contracts
- Controller routes:
  - `/` -> `HomeController.Index()`
  - `/Resource/Index` -> `ResourceController.Index()`
- View Models:
  - `Home/Index.cshtml`: `ViewData["Title"]`, `ViewData["ShowHero"]`
  - `Resource/Index.cshtml`: `IEnumerable<SNEStorage.Models.Resource>`
  - `Shared/Error.cshtml`: `ErrorViewModel`

## Code Layout
- Views: `SNEStorage/Views/`
  - `Shared/_Layout.cshtml`
  - `Home/Index.cshtml`
  - `Resource/Index.cshtml`
  - `Shared/Error.cshtml`
- Styles: `SNEStorage/wwwroot/css/site.css`
- Assets: `SNEStorage/wwwroot/images/logo_final.png`
