## 2026-07-22T16:36:03Z
You are the Implementation Worker for Milestone 2 of the SNEStorage frontend redesign.
Working directory: c:\Users\Vivian\Desktop\SNEStorage-main\.agents\teamwork_preview_worker_m2
Project file: c:\Users\Vivian\Desktop\SNEStorage-main\.agents\orchestrator\PROJECT.md
Explorer Reports:
- Explorer 1 Audit: c:\Users\Vivian\Desktop\SNEStorage-main\.agents\teamwork_preview_explorer_m1_1\analysis.md
- Explorer 2 CSS Spec: c:\Users\Vivian\Desktop\SNEStorage-main\.agents\teamwork_preview_explorer_m1_2\analysis.md
- Explorer 3 Views Spec: c:\Users\Vivian\Desktop\SNEStorage-main\.agents\teamwork_preview_explorer_m1_3\analysis.md

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A Forensic Auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

Your Tasks:
1. Rebuild `SNEStorage/wwwroot/css/site.css` with the full `snes-*` pure Vanilla CSS system specified in Explorer 2's spec. Include CRT overlay FX, scanlines, space background, 90s SNES typography, centerpiece logo floating animation for `logo_final.png` with orbital rings (`.snes-ring-1`, `.snes-ring-2`), pure custom grid/flex layout, navbar, cards, table, badges, and buttons.
2. Rebuild all Razor Views in `SNEStorage/Views/`:
   - `Shared/_Layout.cshtml`: Remove Bootstrap CSS `<link>` and JS `<script>`. Add CRT overlay, `.snes-navbar`, centerpiece logo wrapper with `logo_final.png` and orbital rings (`.snes-ring-1`, `.snes-ring-2`) when `ViewData["ShowHero"]` is true, main container, footer. Zero Bootstrap classes.
   - `Home/Index.cshtml`: Rebuild using `snes-*` classes. Zero Bootstrap classes (`row`, `col-md-8`, `offset-md-2`, `btn`, `btn-outline-light`, etc.).
   - `Resource/Index.cshtml`: Rebuild repository crates view using `snes-card`, `snes-table`, `snes-badge`. Zero Bootstrap classes (`text-muted`, `text-success`, `text-danger`, `py-5`, etc.).
   - `Shared/Error.cshtml`: Rebuild using custom retro alert panel. Zero Bootstrap classes (`text-danger`).
   - Clean up `Shared/_Layout.cshtml.css` if necessary.
3. Build Verification: Run `dotnet build c:\Users\Vivian\Desktop\SNEStorage-main\SNEStorage\SNEStorage.csproj` and verify 0 errors.
4. Framework Audit Verification: Perform a search across `SNEStorage/Views/` to ensure no Bootstrap classes (`container`, `col-`, `row`, `navbar-`, `btn-`, `text-muted`, `text-success`, `text-danger`, `mb-`, `mt-`, `py-`, etc.) exist in any `.cshtml` file.
5. Write your implementation report to `c:\Users\Vivian\Desktop\SNEStorage-main\.agents\teamwork_preview_worker_m2\handoff.md` with full build and audit verification results, and send a handoff message.
