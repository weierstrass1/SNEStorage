# Original User Request

## Initial Request — 2026-07-22T11:34:35-05:00

# Teamwork Project Prompt

Redesign and rebuild the frontend views and CSS of the SNEStorage ASP.NET Core MVC web application from a blank slate to perfectly integrate the new retro SNES-style logo.

Working directory: c:\Users\Vivian\Desktop\SNEStorage-main\SNEStorage
Integrity mode: development

## Requirements

### R1. Complete Vanilla CSS Redesign
Delete the existing HTML/CSS layouts in the Views directory and start from scratch. Use pure Vanilla CSS for maximum control over the retro 90s aesthetic (custom animations, CRT scanlines, etc.).

### R2. Logo Integration
The new logo (`logo_final.png`) must be the centerpiece of the design and perfectly match the surrounding UI elements.

### R3. Maintain Architecture
The application must remain an ASP.NET Core MVC application. You must rebuild the `.cshtml` Razor views without altering the backend C# controller logic.

## Acceptance Criteria

### Technical Correctness
- [ ] `dotnet build` completes with 0 errors.
- [ ] The application runs successfully via `dotnet run` without crashing.

### Design Verification
- [ ] No Bootstrap or external CSS framework classes are used in the `.cshtml` files (e.g. `col-md-6`, `btn-primary`, `container` must be absent).
- [ ] The `logo_final.png` image is displayed prominently on the homepage.
- [ ] The final UI strongly reflects a polished retro 90s SNES aesthetic.
