# Project: SNEStorage Frontend Redesign

## Architecture
- Target Application: ASP.NET Core MVC (`SNEStorage`)
- Core Framework: .NET 8.0 / C# Controllers + CSHTML Razor Views
- UI Styling: 100% Pure Vanilla CSS (No Bootstrap, no external CSS frameworks)
- Visual Style: Retro 90s SNES-style aesthetic (CRT scanlines, custom animations, retro color palette, custom typography/styling, `snes-*` class namespace)
- Key Assets: `logo_final.png` centerpiece on homepage

## Code Layout
- Web App Root: `c:\Users\Vivian\Desktop\SNEStorage-main\SNEStorage`
- Controllers: `SNEStorage/Controllers/` (100% untouched)
- Views: `SNEStorage/Views/` (`_ViewStart.cshtml`, `_ViewImports.cshtml`, `Shared/_Layout.cshtml`, `Shared/Error.cshtml`, `Home/Index.cshtml`, `Resource/Index.cshtml`)
- Static Web Assets: `SNEStorage/wwwroot/` (`css/site.css`, `images/logo_final.png`)

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| 1 | Exploration & Codebase Analysis | Analyze views, controllers, assets, build setup, logo placement | none | DONE |
| 2 | E2E Testing Track (Test Suite & Infra) | Create requirement-driven test suite & publish `TEST_READY.md` | M1 | DONE |
| 3 | Vanilla CSS Engine & Razor Views Rebuild | Apply pure Vanilla CSS styling (`snes-*`), rebuild all Razor views, integrate `logo_final.png`, purge Bootstrap | M1 | DONE |
| 4 | Verification, Adversarial Hardening & Audit | Verification by Reviewers, Challengers, and Forensic Auditor | M2, M3 | DONE |

## Interface Contracts
### Controller ↔ View Contracts
- `ViewData["Title"]`: String title for layout template.
- `ViewData["ShowHero"]`: Boolean flag for homepage logo hero centerpiece.
- Models: `IEnumerable<SNEStorage.Models.Resource>` for Resource view; `ErrorViewModel` for Error view.
- Controller C# code remains 100% untouched.

## Acceptance Criteria Checklist
- [x] `dotnet build` completes with 0 errors.
- [x] `dotnet run` starts and runs application without crashing (200 OK on `/` and `/Resource`).
- [x] No Bootstrap or external CSS framework classes exist in any `.cshtml` file (`col-md-6`, `btn-primary`, `container`, etc. absent).
- [x] `logo_final.png` is displayed prominently on the homepage as centerpiece.
- [x] Final UI strongly reflects a polished retro 90s SNES aesthetic.
- [x] Forensic Auditor verdict is CLEAN.
