## 2026-07-22T19:32:31Z
You are the E2E Testing Suite Creator for SNEStorage frontend redesign.
Your working directory is: c:\Users\Vivian\Desktop\SNEStorage-main\.agents\teamwork_preview_worker_e2e
Your task:
1. Design and build a comprehensive requirement-driven test suite & runner for SNEStorage.
2. Read requirement details from `c:\Users\Vivian\Desktop\SNEStorage-main\PROJECT.md` and `c:\Users\Vivian\Desktop\SNEStorage-main\.agents\orchestrator\ORIGINAL_REQUEST.md`.
3. Create test script (e.g. `e2e_test_runner.py` or `.ps1` in your directory or test area) that performs:
   - Tier 1: Feature Coverage (dotnet build verification, dotnet run startup verification, HTTP GET `/` and `/Resource`).
   - Tier 2: Boundary & Corner Cases (strict audit of all `.cshtml` files ensuring ZERO Bootstrap classes like `col-md-`, `btn-primary`, `container` exist).
   - Tier 3: Cross-Feature Combinations (verifying `logo_final.png` tag `<img src="~/images/logo_final.png"` is present on homepage, CRT overlay class exists).
   - Tier 4: Real-World Scenarios (verifying responsive retro table rendering and SA-1 badges).
4. Run the test suite on the codebase and document results.
5. Create `TEST_INFRA.md` and publish `TEST_READY.md` at `c:\Users\Vivian\Desktop\SNEStorage-main\TEST_READY.md`.
6. Deliver handoff.md and send message to parent (ID: e1e91524-7502-4775-995d-6279c06c00d7).

DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A Forensic Auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.
