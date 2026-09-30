#!/usr/bin/env python3
"""
SNEStorage End-to-End (E2E) Test Suite Runner
==============================================
Requirement-Driven Multi-Tier Test Suite for SNEStorage Frontend Redesign

Tiers:
  - Tier 1: Feature Coverage (dotnet build verification, dotnet run startup verification, HTTP GET `/` and `/Resource`)
  - Tier 2: Boundary & Corner Cases (audit Blazor components for Bootstrap classes)
  - Tier 3: Cross-Feature Combinations (homepage logo and scanline treatment)
  - Tier 4: Real-World Scenarios (resource table and authenticated upload form)

Usage:
  python tests/e2e_test_runner.py
"""

import sys
import os
import re
import time
import subprocess
import urllib.request
import urllib.error
import json
from pathlib import Path

# Paths
REPO_ROOT = Path(__file__).resolve().parent.parent
PROJECT_DIR = REPO_ROOT / "SNEStorage"
CSPROJ_PATH = PROJECT_DIR / "SNEStorage.csproj"
COMPONENTS_DIR = PROJECT_DIR / "Components"
WWWROOT_DIR = PROJECT_DIR / "wwwroot"
SITE_CSS_PATH = WWWROOT_DIR / "css" / "site.css"
LOGO_IMG_PATH = WWWROOT_DIR / "images" / "logo_final.png"

# Test Results Storage
class TestResult:
    def __init__(self, tier: str, name: str, description: str):
        self.tier = tier
        self.name = name
        self.description = description
        self.passed = False
        self.details = ""
        self.duration_ms = 0.0

class E2ETestRunner:
    def __init__(self, port: int = 5055):
        self.port = port
        self.base_url = f"http://127.0.0.1:{port}"
        self.results = []
        self.server_process = None

    def run_all_tiers(self):
        print("======================================================================")
        print(" SNEStorage E2E Test Suite Execution")
        print(f" Target Repository: {REPO_ROOT}")
        print(f" Target Port: {self.port}")
        print("======================================================================")
        
        start_time = time.time()
        
        # Tier 1: Feature Coverage (dotnet build & dotnet run & HTTP GET)
        self.run_tier_1()
        
        # Tier 2: Boundary & Corner Cases (Bootstrap Class Purge Audit)
        self.run_tier_2()
        
        # Tier 3: Cross-Feature Combinations (Logo & Scanline Effect)
        self.run_tier_3()
        
        # Tier 4: Real-World Scenarios (Resource Table & Upload Form)
        self.run_tier_4()

        total_duration = time.time() - start_time
        
        self.print_summary(total_duration)
        return all(r.passed for r in self.results)

    # -------------------------------------------------------------------------
    # TIER 1: Feature Coverage
    # -------------------------------------------------------------------------
    def run_tier_1(self):
        print("\n--- TIER 1: Feature Coverage (Build, Server Startup & Live Endpoints) ---")
        
        # Test 1.1: Dotnet Build Verification
        res_build = TestResult("Tier 1", "DotnetBuildVerification", "Verify dotnet build completes with 0 errors")
        t0 = time.time()
        try:
            cmd = ["dotnet", "build", str(CSPROJ_PATH), "-c", "Debug"]
            proc = subprocess.run(cmd, capture_output=True, text=True, cwd=str(REPO_ROOT))
            res_build.duration_ms = (time.time() - t0) * 1000
            
            if proc.returncode == 0 and "0 Error(s)" in proc.stdout or proc.returncode == 0:
                res_build.passed = True
                res_build.details = f"dotnet build succeeded with exit code 0.\n{proc.stdout[-300:]}"
            else:
                res_build.passed = False
                res_build.details = f"dotnet build failed with exit code {proc.returncode}.\nSTDOUT:\n{proc.stdout}\nSTDERR:\n{proc.stderr}"
        except Exception as e:
            res_build.duration_ms = (time.time() - t0) * 1000
            res_build.passed = False
            res_build.details = f"Exception running dotnet build: {e}"
        self._record_result(res_build)

        # Test 1.2: Server Startup & Endpoint Accessibility (GET / and GET /Resource)
        res_startup = TestResult("Tier 1", "DotnetRunStartupAndGETEndpoints", "Verify app starts via dotnet run and HTTP GET / & GET /Resource return 200 OK")
        t0 = time.time()
        
        server_started = False
        homepage_html = ""
        resource_html = ""
        
        try:
            env = os.environ.copy()
            env["ASPNETCORE_URLS"] = self.base_url
            env["ASPNETCORE_ENVIRONMENT"] = "Development"
            
            cmd = ["dotnet", "run", "--project", str(CSPROJ_PATH), "--no-build", "--urls", self.base_url]
            self.server_process = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, env=env, cwd=str(REPO_ROOT))
            
            # Poll server until ready (up to 15s)
            poll_start = time.time()
            while time.time() - poll_start < 15:
                if self.server_process.poll() is not None:
                    # Process exited prematurely
                    out, err = self.server_process.communicate()
                    res_startup.details = f"Server exited prematurely with code {self.server_process.returncode}.\nStdout: {out}\nStderr: {err}"
                    break
                try:
                    req = urllib.request.Request(f"{self.base_url}/")
                    with urllib.request.urlopen(req, timeout=2) as response:
                        if response.status == 200:
                            server_started = True
                            homepage_html = response.read().decode('utf-8')
                            break
                except (urllib.error.URLError, ConnectionRefusedError, OSError):
                    time.sleep(0.5)

            if server_started:
                # Fetch /Resource endpoint
                req_res = urllib.request.Request(f"{self.base_url}/Resource")
                with urllib.request.urlopen(req_res, timeout=3) as resp_res:
                    if resp_res.status == 200:
                        resource_html = resp_res.read().decode('utf-8')

                res_startup.passed = True
                res_startup.details = (
                    f"Successfully connected to {self.base_url}/ (Status 200 OK, length {len(homepage_html)} bytes) "
                    f"and {self.base_url}/Resource (Status 200 OK, length {len(resource_html)} bytes)."
                )
                
                # Cache fetched HTML for Tiers 3 & 4
                self.fetched_homepage = homepage_html
                self.fetched_resource = resource_html
            else:
                if not res_startup.details:
                    res_startup.details = f"Timed out waiting for server to respond on {self.base_url}"
                res_startup.passed = False

        except Exception as e:
            res_startup.passed = False
            res_startup.details = f"Exception during server startup/endpoint test: {e}"
        finally:
            self._stop_server()
            res_startup.duration_ms = (time.time() - t0) * 1000

        self._record_result(res_startup)

    # -------------------------------------------------------------------------
    # TIER 2: Boundary & Corner Cases (Strict Audit of Blazor components)
    # -------------------------------------------------------------------------
    def run_tier_2(self):
        print("\n--- TIER 2: Boundary & Corner Cases (Blazor Bootstrap Class Audit) ---")
        
        res_audit = TestResult(
            "Tier 2", 
            "StrictBootstrapPurgeAudit", 
            "Audit all Blazor components to ensure ZERO Bootstrap classes exist"
        )
        t0 = time.time()

        # Strict Bootstrap class patterns
        bootstrap_patterns = [
            r'\bcol-(?:xs|sm|md|lg|xl|xxl)?-\d+\b',
            r'\bcol-(?:xs|sm|md|lg|xl|xxl)?\b',
            r'\bbtn-(?:primary|secondary|success|danger|warning|info|light|dark|link|outline-[a-z]+)\b',
            r'\bbtn-(?:lg|sm|block)\b',
            r'\bcontainer(?:-fluid|-sm|-md|-lg|-xl|-xxl)?\b',
            r'\bnavbar-(?:expand|light|dark|nav|brand|toggler)\b',
            r'\bcard-(?:body|title|text|header|footer|img)\b',
            r'\bform-(?:control|group|label|select|check|inline)\b',
            r'\btable-(?:striped|bordered|hover|active|dark|responsive)\b',
            r'\bbg-(?:primary|secondary|success|danger|warning|info|light|dark|white)\b',
            r'\btext-(?:center|left|right|muted|primary|secondary|danger|success)\b',
            r'\b(d-flex|justify-content-[a-z]+|align-items-[a-z]+)\b',
            r'bootstrap(?:\.min)?\.css',
        ]
        
        compiled_regexes = [re.compile(p, re.IGNORECASE) for p in bootstrap_patterns]
        
        violations = []
        component_files = list(COMPONENTS_DIR.glob("**/*.razor"))
        
        for file_path in component_files:
            rel_path = file_path.relative_to(REPO_ROOT)
            content = file_path.read_text(encoding='utf-8')
            lines = content.splitlines()
            
            for line_idx, line in enumerate(lines, 1):
                # Ignore snes- prefix classes (e.g. snes-container, snes-btn-primary)
                # To accurately test, strip snes- prefixed tokens before scanning for bootstrap tokens
                stripped_line = re.sub(r'snes-[a-zA-Z0-9_-]+', '', line)
                
                for regex in compiled_regexes:
                    matches = regex.findall(stripped_line)
                    if matches:
                        for m in matches:
                            violations.append(f"{rel_path}:{line_idx} - Found Bootstrap token: '{m}' in line: '{line.strip()}'")

        res_audit.duration_ms = (time.time() - t0) * 1000

        if not component_files:
            res_audit.passed = False
            res_audit.details = "No Blazor .razor components found!"
        elif not violations:
            res_audit.passed = True
            res_audit.details = f"Audited {len(component_files)} Blazor components. ZERO Bootstrap classes or framework artifacts found!"
        else:
            res_audit.passed = False
            res_audit.details = f"Found {len(violations)} Bootstrap violation(s) in Blazor components:\n" + "\n".join(violations[:15])

        self._record_result(res_audit)

    # -------------------------------------------------------------------------
    # TIER 3: Cross-Feature Combinations (Logo & Scanline Treatment)
    # -------------------------------------------------------------------------
    def run_tier_3(self):
        print("\n--- TIER 3: Cross-Feature Combinations (Logo & Scanline Treatment) ---")
        
        # Test 3.1: Logo Final Integration
        res_logo = TestResult(
            "Tier 3", 
            "HomepageLogoIntegration", 
            "Verify logo_final.png appears in the Blazor homepage and the asset exists"
        )
        t0 = time.time()
        
        logo_file_exists = LOGO_IMG_PATH.exists() and LOGO_IMG_PATH.stat().st_size > 0
        homepage_component = (COMPONENTS_DIR / "Pages" / "Home.razor").read_text(encoding='utf-8')
        
        logo_tag_in_layout = 'logo_final.png' in homepage_component
        
        homepage_live_has_logo = False
        if hasattr(self, 'fetched_homepage') and self.fetched_homepage:
            homepage_live_has_logo = 'logo_final.png' in self.fetched_homepage

        res_logo.duration_ms = (time.time() - t0) * 1000
        
        if logo_file_exists and logo_tag_in_layout:
            res_logo.passed = True
            res_logo.details = (
                f"logo_final.png exists ({LOGO_IMG_PATH.stat().st_size} bytes). "
                f"Tag present in Home.razor. "
                f"Live HTML rendered tag: {homepage_live_has_logo}."
            )
        else:
            res_logo.passed = False
            res_logo.details = (
                f"Logo checks failed: File exists={logo_file_exists}, "
                f"Layout tag={logo_tag_in_layout}, Live homepage tag={homepage_live_has_logo}"
            )
        self._record_result(res_logo)

        # Test 3.2: Scanline treatment and styling
        res_crt = TestResult(
            "Tier 3",
            "HomepageScanlineTreatment",
            "Verify the homepage scanline treatment is present in markup and CSS"
        )
        t0 = time.time()

        layout_has_crt = "snes-scanline" in homepage_component
        site_css_content = SITE_CSS_PATH.read_text(encoding='utf-8') if SITE_CSS_PATH.exists() else ""
        css_has_crt = ".snes-scanline" in site_css_content
        
        res_crt.duration_ms = (time.time() - t0) * 1000
        
        if layout_has_crt and css_has_crt:
            res_crt.passed = True
            res_crt.details = "The scanline element is present in Home.razor and styled in site.css."
        else:
            res_crt.passed = False
            res_crt.details = f"Scanline checks failed: homepage markup={layout_has_crt}, CSS rule={css_has_crt}"
        self._record_result(res_crt)

    # -------------------------------------------------------------------------
    # TIER 4: Real-World Scenarios (Retro Tables & SA-1 Badges)
    # -------------------------------------------------------------------------
    def run_tier_4(self):
        print("\n--- TIER 4: Real-World Scenarios (Resource Table & Upload Form) ---")
        
        # Test 4.1: Responsive Retro Table Rendering
        res_table = TestResult(
            "Tier 4", 
            "ResponsiveRetroTableRendering", 
            "Verify responsive resource table markup and styling in Blazor"
        )
        t0 = time.time()

        resource_view_path = COMPONENTS_DIR / "Pages" / "Resources.razor"
        resource_view_content = resource_view_path.read_text(encoding='utf-8') if resource_view_path.exists() else ""
        
        view_has_table = "snes-table" in resource_view_content and "snes-table-wrapper" in resource_view_content
        site_css_content = SITE_CSS_PATH.read_text(encoding='utf-8') if SITE_CSS_PATH.exists() else ""
        css_has_table = ".snes-table" in site_css_content and ".snes-table-wrapper" in site_css_content
        
        res_table.duration_ms = (time.time() - t0) * 1000
        
        if view_has_table and css_has_table:
            res_table.passed = True
            res_table.details = "Responsive table markup is present in Resources.razor and styled in site.css."
        else:
            res_table.passed = False
            res_table.details = f"Resource table check failed: component markup={view_has_table}, CSS rules={css_has_table}"
        self._record_result(res_table)

        # Test 4.2: Authenticated resource upload
        res_upload = TestResult(
            "Tier 4", 
            "AuthenticatedResourceUploadForm",
            "Verify resource upload is an authorized Blazor page with a file input"
        )
        t0 = time.time()

        upload_path = COMPONENTS_DIR / "Pages" / "UploadResource.razor"
        upload_content = upload_path.read_text(encoding='utf-8') if upload_path.exists() else ""
        upload_is_protected = "@attribute [Authorize]" in upload_content
        upload_has_file_input = "<InputFile" in upload_content
        res_upload.duration_ms = (time.time() - t0) * 1000
        
        if upload_is_protected and upload_has_file_input:
            res_upload.passed = True
            res_upload.details = "UploadResource.razor requires authorization and renders a file input."
        else:
            res_upload.passed = False
            res_upload.details = f"Upload form check failed: authorized={upload_is_protected}, file input={upload_has_file_input}"
        self._record_result(res_upload)

    # -------------------------------------------------------------------------
    # Helper & Summary Methods
    # -------------------------------------------------------------------------
    def _record_result(self, result: TestResult):
        self.results.append(result)
        status_str = "[PASS]" if result.passed else "[FAIL]"
        print(f" {status_str} {result.tier} :: {result.name} ({result.duration_ms:.1f}ms)")
        if not result.passed:
            print(f"      -> Reason: {result.details.splitlines()[0]}")

    def _stop_server(self):
        if self.server_process:
            try:
                self.server_process.terminate()
                self.server_process.wait(timeout=3)
            except Exception:
                try:
                    self.server_process.kill()
                except Exception:
                    pass
            self.server_process = None

    def print_summary(self, total_duration: float):
        passed_count = sum(1 for r in self.results if r.passed)
        failed_count = sum(1 for r in self.results if not r.passed)
        total_count = len(self.results)

        print("\n======================================================================")
        print(" E2E TEST SUITE SUMMARY RESULTS")
        print("======================================================================")
        print(f" Total Tests Run : {total_count}")
        print(f" Passed          : {passed_count}")
        print(f" Failed          : {failed_count}")
        print(f" Total Duration  : {total_duration:.2f} seconds")
        print("----------------------------------------------------------------------")
        
        for r in self.results:
            status = "PASS" if r.passed else "FAIL"
            print(f" [{status}] [{r.tier}] {r.name}")
            for line in r.details.splitlines():
                print(f"    {line}")
        print("======================================================================\n")

    def export_json(self, output_path: Path):
        data = {
            "timestamp": time.strftime("%Y-%m-%d %H:%M:%S UTC", time.gmtime()),
            "total_tests": len(self.results),
            "passed_tests": sum(1 for r in self.results if r.passed),
            "failed_tests": sum(1 for r in self.results if not r.passed),
            "results": [
                {
                    "tier": r.tier,
                    "name": r.name,
                    "description": r.description,
                    "passed": r.passed,
                    "duration_ms": round(r.duration_ms, 2),
                    "details": r.details
                }
                for r in self.results
            ]
        }
        output_path.write_text(json.dumps(data, indent=2), encoding='utf-8')
        print(f"Exported JSON test results to {output_path}")

if __name__ == "__main__":
    runner = E2ETestRunner(port=5055)
    success = runner.run_all_tiers()
    json_path = REPO_ROOT / ".agents" / "teamwork_preview_worker_e2e" / "e2e_test_results.json"
    runner.export_json(json_path)
    sys.exit(0 if success else 1)
