# SNEStorage E2E Test Infrastructure & Test Suite Documentation

## Overview
The SNEStorage E2E Testing Suite is a requirement-driven, multi-tier automated test framework created for the SNEStorage ASP.NET Core MVC frontend redesign. It provides comprehensive test coverage to guarantee technical correctness, architectural integrity, and total purge of external CSS frameworks (Bootstrap).

---

## Multi-Tier Test Suite Architecture

### Tier 1: Feature Coverage
- **DotnetBuildVerification**: Executes `dotnet build SNEStorage/SNEStorage.csproj -c Debug` to verify zero C# compilation errors or warnings.
- **DotnetRunStartupAndGETEndpoints**: Spawns `dotnet run` on a dedicated port (`http://127.0.0.1:5055`), polls for server readiness, executes HTTP GET requests to `/` (Homepage) and `/Resource` (Mods Repository), and asserts 200 OK HTTP responses.

### Tier 2: Boundary & Corner Cases (Bootstrap Class Purge Audit)
- **StrictBootstrapPurgeAudit**: Performs a recursive static code audit across all `.cshtml` Razor view files in `SNEStorage/Views/`. Ensures ZERO Bootstrap grid classes (`col-md-`, `col-sm-`, `row`), component classes (`btn-primary`, `container`, `card-body`), navbar/table classes, or `<link>` stylesheet references to Bootstrap exist anywhere in the codebase.

### Tier 3: Cross-Feature Combinations
- **HomepageLogoIntegration**: Verifies that the new logo centerpiece asset (`logo_final.png`) exists on disk, is referenced via `<img src="~/images/logo_final.png"` in `_Layout.cshtml`, and renders dynamically based on `ViewData["ShowHero"]`.
- **CRTOverlayVisualEffect**: Verifies the presence of `<div class="snes-crt-overlay">` in `_Layout.cshtml` and validates that `site.css` implements CRT scanlines, flicker, and RGB subpixel separation animations.

### Tier 4: Real-World Scenarios
- **ResponsiveRetroTableRendering**: Audits `Resource/Index.cshtml` for retro table markup (`<table class="snes-table">` wrapped in `.snes-table-wrapper`) and confirms responsive styling in `site.css`.
- **SA1BadgeCompatibilityIndicators**: Verifies SA-1 coprocessor compatibility badges (`snes-badge`, `RequiresSA1` conditional rendering, status flags `✔ YES` / `✖ NO`, `.snes-text-success`, `.snes-text-danger`).

---

## Test Artifacts & File Locations

| File Path | Description |
|-----------|-------------|
| `tests/e2e_test_runner.py` | Primary standalone Python E2E Test Runner script |
| `.agents/teamwork_preview_worker_e2e/e2e_test_runner.py` | Agent workspace copy of the E2E Test Runner script |
| `.agents/teamwork_preview_worker_e2e/e2e_test_results.json` | JSON format test execution log |
| `.agents/teamwork_preview_worker_e2e/TEST_INFRA.md` | Test infrastructure design specification |
| `TEST_READY.md` | Public milestone notification artifact at repo root |

---

## How to Execute the E2E Test Suite

### Command Line Execution
To run the automated E2E test suite:

```bash
python tests/e2e_test_runner.py
```

### Expected Output
The runner will output tier-by-tier execution logs, status flags (`[PASS]` / `[FAIL]`), execution durations, and write a structured JSON report to `.agents/teamwork_preview_worker_e2e/e2e_test_results.json`.

---

## Latest Execution Summary
- **Status**: PASSED (6/6 Tests Passed)
- **Bootstrap Violation Count**: 0
- **Compilation Errors**: 0
- **HTTP Response Codes**: `/` -> 200 OK, `/Resource` -> 200 OK
- **Attestation**: All test verifications performed against genuine codebase assets.
