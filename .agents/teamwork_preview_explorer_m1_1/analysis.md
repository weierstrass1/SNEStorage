# SNEStorage Frontend Architecture Audit & View Mapping Analysis

**Author:** Explorer 1  
**Milestone:** Milestone 1 — SNEStorage Frontend Redesign Preview / Audit  
**Target Directory:** `c:\Users\Vivian\Desktop\SNEStorage-main\SNEStorage`  
**Date:** 2026-07-22  

---

## 1. Executive Summary

This report presents a thorough frontend codebase analysis of the **SNEStorage** ASP.NET Core MVC application (`.NET 8.0`). The goal of this investigation is to map every Razor view (`.cshtml` file), evaluate all CSS/JS dependencies (including Bootstrap and custom styling systems), record data model bindings and `ViewData`/`ViewBag` state requirements, and document all UI components (tables, buttons, forms, and navigation links).

### Key Audit Findings
1. **View Catalog:** The application contains **8 Razor view files** (`.cshtml`) across `Views/`, `Views/Home/`, `Views/Resource/`, and `Views/Shared/`.
2. **Framework & Styling Strategy:** The project does **not** consume Bootstrap CSS or JS in any active Razor views. Instead, it relies on a custom, high-fidelity 90s retro/synthwave CSS system prefixed with `snes-*` defined in `wwwroot/css/site.css` (964 lines), supplemented by Google Fonts imports (`Inter`, `Press Start 2P`, `Share Tech Mono`, `Silkscreen`).
3. **Bootstrap Footprint:** While Bootstrap 5.1.0 dist assets exist on disk in `wwwroot/lib/bootstrap/`, no view links to them. A standalone stylesheet `wwwroot/css/Create.css` references Bootstrap form classes (`.form-control`, `.form-check-label`, `.btn`), but is unreferenced in all current views.
4. **Data & View Contracts:** Two views use strongly-typed models (`Resource/Index.cshtml` binds to `IEnumerable<SNEStorage.Models.Resource>`, `Shared/Error.cshtml` binds to `ErrorViewModel`). View state flag `ViewData["ShowHero"]` conditionally renders a animated SNES hero banner in `_Layout.cshtml`.
5. **Interactive UI Inventory:** There are **0 active HTML `<form>` elements** in the current views. The UI features 1 dynamic data table (`snes-table` in `Resource/Index.cshtml`) and styled anchor CTA buttons (`snes-btn`).

---

## 2. Comprehensive Razor View Catalog

Below is the complete inventory of all `.cshtml` files in `SNEStorage/Views/`:

| Relative File Path | Line Count | Purpose & Description | Strongly Typed `@model` |
| :--- | :---: | :--- | :--- |
| `Views/_ViewStart.cshtml` | 4 | Sets default master layout (`Layout = "_Layout"`). | None |
| `Views/_ViewImports.cshtml` | 4 | Global namespace imports (`SNEStorage`, `SNEStorage.Models`) and Tag Helpers. | None |
| `Views/Shared/_Layout.cshtml` | 62 | Master HTML5 document structure, CRT scanlines, header navigation, hero banner logic, main body container, and footer. | None |
| `Views/Shared/_Layout.cshtml.css` | 5 | CSS isolation stylesheet for `_Layout.cshtml`. | N/A |
| `Views/Shared/_ValidationScriptsPartial.cshtml` | 3 | Partial view importing jQuery Validation and Unobtrusive scripts. | None |
| `Views/Shared/Error.cshtml` | 30 | Standard error page displaying request ID and ASP.NET Core dev environment details. | `ErrorViewModel` |
| `Views/Home/Index.cshtml` | 15 | Landing page displaying welcome text and CTA button to browse mod crates. | None |
| `Views/Resource/Index.cshtml` | 55 | Main mod repository/crates view displaying resources in a retro data table. | `IEnumerable<SNEStorage.Models.Resource>` |

---

## 3. CSS Framework, Asset & Layout Dependencies

### 3.1 Bootstrap & External CSS Framework Analysis
- **Bootstrap 5.1.0 (Disk Only):** Assets are present under `wwwroot/lib/bootstrap/dist/css/` and `wwwroot/lib/bootstrap/dist/js/`. However, **zero active Razor views link to Bootstrap CSS or JS**.
- **Bootstrap Class Usage in Views:** No Bootstrap grid or component utility classes (`container`, `row`, `col-md-*`, `navbar`, `btn-primary`, `table`) are used in any view.
- **Orphan Bootstrap CSS:** `wwwroot/css/Create.css` targets Bootstrap elements:
  ```css
  body, .form-control, label, textarea, select, option, input, h2 { color: white !important; ... }
  .form-control { background-color: #333 !important; border-color: #555; }
  .form-check-label { color: white !important; }
  .btn { color: white; }
  ```
  *Note:* `Create.css` is not linked by any `.cshtml` file.

### 3.2 Custom CSS Architecture (`snes-*` Design System)
All active styling is driven by `wwwroot/css/site.css` linked in `Views/Shared/_Layout.cshtml:7`:
`<link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />`

Key Design System Features in `site.css`:
1. **Google Fonts Import (`site.css:2`):**
   ```css
   @import url('https://fonts.googleapis.com/css2?family=Inter:ital,wght@0,400;0,700;0,900;1,900&family=Press+Start+2P&family=Share+Tech+Mono&family=Silkscreen&display=swap');
   ```
2. **CSS Custom Properties (`:root` tokens, lines 7–53):**
   - Color Palette: SNES Purple (`--snes-purple-dark: #2b1055`, `--snes-purple-main: #5b21b6`, `--snes-purple-light: #7c3aed`), Super Famicom 4-button palette (`--snes-red: #e60012`, `--snes-yellow: #ffcc00`, `--snes-green: #00a651`, `--snes-blue: #0080ff`), Neon Synthwave (`--snes-cyan: #00f0ff`, `--snes-magenta: #ff007f`).
   - Font Stacks: Display (`'Press Start 2P', 'Silkscreen'`), Body (`'Inter'`), Mono (`'Share Tech Mono'`).
3. **CRT Scanlines & Overlay Engine (`site.css:119–174`):**
   Fixed overlay with radial vignette and horizontal scanline roll animation (`.snes-crt-overlay`).
4. **Hero Logo & Orbital Ring System (`site.css:297–399`):**
   Animated floating logo wrapper with dual elliptical orbital rings (`.snes-ring-1`, `.snes-ring-2`).
5. **Pure Vanilla CSS Layout & Component Grid (`site.css:403–459`):**
   Custom 12-column grid (`.snes-grid`, `.snes-col-12`, `.snes-col-8`, `.snes-col-6`, etc.), flexbox helpers, cards (`.snes-card`), buttons (`.snes-btn`), tables (`.snes-table`), badges (`.snes-badge`).

### 3.3 Static Image Assets
- `wwwroot/images/logo_final.png`: Used in `_Layout.cshtml:42` inside the hero section.
- `wwwroot/images/`: Additional unreferenced assets (`caja.png`, `full_logo.png`, `full_logo_2.png`, `full_logo_3.png`, `logo.png`, `snestorageLogo.png`).

---

## 4. Model Bindings, ViewData & Layout Contracts

### 4.1 Model Binding Matrix

| View Path | Model Binding | Properties Accessed |
| :--- | :--- | :--- |
| `Views/Home/Index.cshtml` | *None* | N/A |
| `Views/Resource/Index.cshtml` | `@model IEnumerable<SNEStorage.Models.Resource>` | `@res.Title`, `@res.Description`, `@res.Crate` (Enum), `@res.Author?.UserName`, `@res.RequiresSA1` (bool), `@res.Downloads` (int) |
| `Views/Shared/Error.cshtml` | `@model ErrorViewModel` | `@Model.RequestId`, `@Model.ShowRequestId` |

### 4.2 ViewData & Layout Logic Contracts

1. **Document Title Control:**
   - `_Layout.cshtml:6`: `<title>@(ViewData["Title"] != null ? ViewData["Title"] + " - " : "")SNEStorage</title>`
   - `Home/Index.cshtml:2`: `ViewData["Title"] = "Home Page"` -> Title: `Home Page - SNEStorage`
   - `Resource/Index.cshtml:3`: `ViewData["Title"] = "Mods Repository"` -> Title: `Mods Repository - SNEStorage`
   - `Error.cshtml:3`: `ViewData["Title"] = "Error"` -> Title: `Error - SNEStorage`

2. **Conditional Hero Section:**
   - `_Layout.cshtml:36–45`:
     ```razor
     @if (ViewData["ShowHero"] != null && (bool)ViewData["ShowHero"] == true)
     {
         <section class="snes-hero-section">
             <div class="snes-hero-logo-wrapper">
                 <div class="snes-ring snes-ring-1" aria-hidden="true"></div>
                 <div class="snes-ring snes-ring-2" aria-hidden="true"></div>
                 <img src="~/images/logo_final.png" alt="SNEStorage Full Logo" class="snes-logo-img" />
             </div>
         </section>
     }
     ```
   - Only `Home/Index.cshtml` sets `ViewData["ShowHero"] = true`. Other views render without the hero banner.

---

## 5. UI Elements Inventory: Forms, Buttons, Tables & Links

### 5.1 Forms (`<form>`)
- **Current Active Forms:** **0**. None of the views currently contain form elements, text inputs, selects, or submission handlers.
- **Form Infrastructure Ready:** `_ValidationScriptsPartial.cshtml` imports jQuery validation scripts, and `Create.css` contains input styling, indicating upcoming form creation requirements.

### 5.2 Navigation Links & Buttons

| Element | View Location | Line | Markup / Code | Target Route |
| :--- | :--- | :---: | :--- | :--- |
| Brand Link | `_Layout.cshtml` | 17–19 | `<a class="snes-nav-brand" asp-area="" asp-controller="Home" asp-action="Index"><span class="snes-brand-icon">🎮</span> SNEStorage</a>` | `/Home/Index` |
| Nav Link 1 | `_Layout.cshtml` | 23 | `<a class="snes-nav-link" asp-area="" asp-controller="Home" asp-action="Index">Home</a>` | `/Home/Index` |
| Nav Link 2 | `_Layout.cshtml` | 26 | `<a class="snes-nav-link" asp-area="" asp-controller="Resource" asp-action="Index">Mods Repository</a>` | `/Resource/Index` |
| Primary CTA | `Home/Index.cshtml` | 11 | `<a asp-controller="Resource" asp-action="Index" class="snes-btn snes-btn-lg snes-btn-primary">Browse Crates</a>` | `/Resource/Index` |

### 5.3 Data Tables (`<table>`)
`Views/Resource/Index.cshtml:10–53` renders a responsive retro data table (`.snes-table`):

```html
<div class="snes-table-wrapper">
    <table class="snes-table">
        <thead>
            <tr>
                <th>Title</th>
                <th>Crate Type</th>
                <th>Author</th>
                <th>SA-1 Compatible</th>
                <th>Downloads</th>
            </tr>
        </thead>
        <tbody>
            <!-- Iterates over Model -->
            <!-- Columns: Title+Description, Crate Badge, Author, SA-1 status (✔ YES / ✖ NO), Downloads -->
            <!-- Empty state: <tr><td colspan="5" class="snes-empty-cell">No resources found. The crates are empty!</td></tr> -->
        </tbody>
    </table>
</div>
```

---

## 6. Controller & Model Alignment

1. **`HomeController` (`Controllers/HomeController.cs`):**
   - Action `Index()` returns `View()`, which resolves to `Views/Home/Index.cshtml`.
2. **`ResourceController` (`Controllers/ResourceController.cs`):**
   - Action `Index()` asynchronously queries resources with author info (`_context.Resources.Include(r => r.Author).AsNoTracking().ToListAsync()`) and passes the list to `Views/Resource/Index.cshtml`.
3. **`Program.cs` Route Config:**
   - Default route pattern: `{controller=Home}/{action=Index}/{id?}`.
   - `AddRazorPages` configures `/Home/Index` as root (`""`).

---

## 7. Strategic Recommendations for Redesign Phase

1. **Maintain Pure CSS Strategy (`snes-*`):**
   Since all views natively utilize `snes-*` class names from `site.css` rather than Bootstrap, the redesign should expand `site.css` or modularize it rather than attempting to force Bootstrap utility classes into retro Razor views.
2. **Clean Up Unused Static Assets:**
   - Remove or integrate `wwwroot/css/home.css` and `wwwroot/css/Create.css`.
   - Audit unused logo assets in `wwwroot/images/`.
3. **Form Component Standard for Resource Upload/Edit:**
   Design custom retro form controls matching `site.css` (`snes-form-group`, `snes-input`, `snes-select`) for upcoming resource creation/edit views.
4. **Enhanced Data Display in `Resource/Index`:**
   Expand table fields to include `DateAdded`, file count (`res.Files.Count`), tag badges (`res.Tags`), and action links (e.g., Download, Details).
5. **Accessibility & Semantic Polish:**
   Add ARIA landmarks and focus state indicators to `.snes-btn` and `.snes-nav-link` elements for full keyboard accessibility while preserving retro aesthetics.
