## 2026-07-22T14:32:31Z
You are the Implementation Worker for the SNEStorage frontend redesign project.
Your working directory is: c:\Users\Vivian\Desktop\SNEStorage-main\.agents\teamwork_preview_worker_m3_1
Your task:
1. Read the replacement Razor views specified in `c:\Users\Vivian\Desktop\SNEStorage-main\.agents\teamwork_preview_explorer_m1_3\analysis.md`.
2. Update the Razor views in `c:\Users\Vivian\Desktop\SNEStorage-main\SNEStorage\Views\`:
   - `Shared/_Layout.cshtml`
   - `Home/Index.cshtml`
   - `Resource/Index.cshtml`
   - `Shared/Error.cshtml`
3. Ensure `SNEStorage/wwwroot/css/site.css` provides full `snes-*` styling (CRT scanline overlay, retro buttons, retro table, badge, container, navbar).
4. Verify `SNEStorage/wwwroot/images/logo_final.png` is correctly referenced and displayed prominently on homepage (`Home/Index.cshtml` / `_Layout.cshtml`).
5. Ensure ZERO Bootstrap or external CSS framework classes remain in any `.cshtml` file (purge `container`, `navbar`, `col-`, `btn-primary`, `row`, `text-muted`, etc.).
6. Run `dotnet build` in `c:\Users\Vivian\Desktop\SNEStorage-main\SNEStorage` and verify 0 errors.
7. Run `dotnet run` (or start app test) in `c:\Users\Vivian\Desktop\SNEStorage-main\SNEStorage` and verify successful startup without crashing.
8. Document all changes and build/test outputs in `changes.md` and deliver `handoff.md` in your working directory.
9. Send message to parent (ID: e1e91524-7502-4775-995d-6279c06c00d7) with your report.
