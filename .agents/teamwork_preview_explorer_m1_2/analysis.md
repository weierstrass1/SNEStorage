# Technical Exploration Report: SNEStorage Frontend Architecture & Backend Contracts

**Author:** Explorer 2 (Milestone 1)  
**Target:** SNEStorage ASP.NET Core MVC Redesign  
**Date:** 2026-07-22  

---

## Executive Summary

This report delivers a full architectural examination of the `SNEStorage` ASP.NET Core MVC project, focusing on build infrastructure, static web assets, controller action routes, ViewData/ViewBag dynamic key bindings, model data structures, and the centerpiece logo location (`logo_final.png`). All findings ensure that upcoming Razor view rebuilds and custom CSS enhancements preserve existing backend C# contracts with 100% fidelity.

---

## 1. Project Build System & File Layout

### 1.1 Project Specification
* **Project File:** `SNEStorage/SNEStorage.csproj`
* **Target Framework:** `.NET 8.0` (`<TargetFramework>net8.0</TargetFramework>`)
* **Project Type:** ASP.NET Core Web App (`Microsoft.NET.Sdk.Web`) with MVC and Razor Pages support.

### 1.2 Core Nuget Package Dependencies
| Package Name | Version | Usage / Functionality |
|---|---|---|
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | `8.0.11` | User Identity & Auth management |
| `Microsoft.EntityFrameworkCore.InMemory` | `8.0.8` | In-memory database provider (`SNEStorageContext`) |
| `Microsoft.EntityFrameworkCore.SqlServer` | `8.0.11` | SQL Server DB provider |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | `8.0.11` | JWT Token authentication scheme |
| `Swashbuckle.AspNetCore` | `7.1.0` | Swagger REST API Documentation (`/swagger`) |
| `Azure.Identity` | `1.13.1` | Azure identity integration |

### 1.3 Build Capability Status
* Project standard build tooling targets `.NET 8.0 SDK`.
* Program configuration (`Program.cs`) initializes `AppDbContext` with `UseInMemoryDatabase("SNEStorageContext")`, sets up session state, JWT bearer authentication, policy authorization ("Admin", "Moderator"), custom middleware for JWT header insertion, static files middleware (`app.UseStaticFiles()`), routing, and CORS.

---

## 2. Static Web Assets & `logo_final.png` Inventory

### 2.1 Centerpiece Logo (`logo_final.png`)
* **Verified Absolute Location:** `c:\Users\Vivian\Desktop\SNEStorage-main\SNEStorage\wwwroot\images\logo_final.png`
* **Relative Web Path:** `~/images/logo_final.png`
* **Current Layout Usage:** Referenced in `Views/Shared/_Layout.cshtml` (line 42) inside the conditional hero logo wrapper:
  ```html
  <img src="~/images/logo_final.png" alt="SNEStorage Full Logo" class="snes-logo-img" />
  ```
* **Raw Brand Asset Archive:** Additional high-resolution logo source files exist under root `/Logo/`:
  - `Logo/snestorage logo (1).png`
  - `Logo/snestorage para web.png`
  - `Logo/caja optimizada para web.png`

### 2.2 wwwroot Directory Tree Overview
```
SNEStorage/wwwroot/
├── css/
│   ├── site.css (964 lines - main custom retro SNES CSS system)
│   ├── home.css (60 lines - legacy home layout)
│   └── Create.css (18 lines - legacy dark form helpers)
├── js/
│   └── site.js (boilerplate JS file)
├── images/
│   ├── logo_final.png (PRIMARY CENTERPIECE)
│   ├── logo.png, full_logo.png, full_logo_2.png, full_logo_3.png, snestorageLogo.png, caja.png
├── Content/
│   ├── Avatars/ (sample user avatars)
│   └── Images/ (background.png, download.png)
└── lib/
    ├── bootstrap/ (Bootstrap 5.x assets - NOT referenced in _Layout.cshtml)
    ├── jquery/
    ├── jquery-validation/
    └── jquery-validation-unobtrusive/
```

---

## 3. Controller Actions, Routes & View Mapping Matrix

| Controller | Action Method | Verb | Route Pattern | View Path | Model Passed | ViewData/ViewBag Keys |
|---|---|---|---|---|---|---|
| `HomeController` | `Index()` | GET | `/`, `/Home`, `/Home/Index` | `Views/Home/Index.cshtml` | `null` | `ViewData["Title"] = "Home Page"`<br/>`ViewData["ShowHero"] = true` |
| `ResourceController` | `Index()` | GET | `/Resource`, `/Resource/Index` | `Views/Resource/Index.cshtml` | `IEnumerable<Resource>` | `ViewData["Title"] = "Mods Repository"` |
| `HomeController` (fallback) | `Error()` | GET | `/Home/Error` | `Views/Shared/Error.cshtml` | `ErrorViewModel` | `ViewData["Title"] = "Error"` |

---

## 4. ViewData / ViewBag Contract Specification

1. **`ViewData["Title"]`**
   - **Type:** `string`
   - **Evaluated in:** `Views/Shared/_Layout.cshtml` line 6:
     ```cshtml
     <title>@(ViewData["Title"] != null ? ViewData["Title"] + " - " : "")SNEStorage</title>
     ```
   - **Set in Views:**
     - `Home/Index.cshtml`: `"Home Page"`
     - `Resource/Index.cshtml`: `"Mods Repository"`
     - `Shared/Error.cshtml`: `"Error"`

2. **`ViewData["ShowHero"]`**
   - **Type:** `bool`
   - **Set in:** `Views/Home/Index.cshtml` line 3 (`ViewData["ShowHero"] = true;`)
   - **Evaluated in:** `Views/Shared/_Layout.cshtml` line 36:
     ```cshtml
     @if (ViewData["ShowHero"] != null && (bool)ViewData["ShowHero"] == true)
     ```
   - **Behavior:** Dynamically toggles display of the floating logo centerpiece (`logo_final.png`) and SNES orbital accent rings (`snes-ring-1`, `snes-ring-2`).

---

## 5. Model Data Contracts

### 5.1 `SNEStorage.Models.Resource`
* **File:** `SNEStorage/Models/Resource.cs`
* **Properties:**
  - `int Id` (Primary Key)
  - `string Title` `[Required, MaxLength(100)]`
  - `string Description` `[Required]`
  - `CrateType Crate` (Enum: `Sprite`, `Graphics`, `Music`, `Sample`, `Hack`, `Homebrew`, `Patch`, `Tool`, `Script`)
  - `bool RequiresSA1` (Flag indicating SA-1 coprocessor compatibility)
  - `bool IsModerated`
  - `int Downloads`
  - `DateTime DateAdded`
  - `DateTime LastUpdated`
  - `int AuthorId` (Foreign Key to `User`)
  - `User Author` (Navigation property)
  - `ICollection<ResourceFile> Files`
  - `ICollection<Tag> Tags`
* **View Contracts (`Resource/Index.cshtml`):**
  - Model type: `@model IEnumerable<SNEStorage.Models.Resource>`
  - Properties bound: `@res.Title`, `@res.Description`, `@res.Crate`, `@(res.Author?.UserName ?? "Unknown")`, `@res.RequiresSA1`, `@res.Downloads`.

### 5.2 `SNEStorage.Models.User`
* **File:** `SNEStorage/Models/User.cs`
* **Base Class:** `IdentityUser<int>` (provides `UserName`, `Email`, `Id`, etc.)
* **Properties:**
  - `DateTime JoinDate`
  - `ICollection<Resource> Resources`

### 5.3 `SNEStorage.Models.CrateType` (Enum)
* **File:** `SNEStorage/Models/CrateType.cs`
* **Enum Values:** `Sprite`, `Graphics`, `Music`, `Sample`, `Hack`, `Homebrew`, `Patch`, `Tool`, `Script`.

### 5.4 `SNEStorage.Models.ResourceFile`
* **File:** `SNEStorage/Models/ResourceFile.cs`
* **Properties:** `Id`, `Version` `[MaxLength(20)]`, `FileName`, `FileSizeBytes`, `FileHash`, `UploadDate`, `ResourceId`, `Resource`.

### 5.5 `SNEStorage.Models.Tag`
* **File:** `SNEStorage/Models/Tag.cs`
* **Properties:** `Id`, `Name` `[MaxLength(30)]`, `ICollection<Resource> Resources`.

### 5.6 `SNEStorage.Models.ErrorViewModel`
* **File:** `SNEStorage/Models/ErrorViewModel.cs`
* **Properties:** `RequestId` (`string?`), `ShowRequestId` (`bool => !string.IsNullOrEmpty(RequestId)`).

---

## 6. Critical Invariants & Rules for Downstream Milestones

1. **Backend Controller Immutability:** Controllers (`HomeController.cs`, `ResourceController.cs`) and models MUST NOT be edited or broken during frontend Razor/CSS work.
2. **View Binding Invariants:** All Razor model declarations (`@model IEnumerable<SNEStorage.Models.Resource>`), property accesses (`@res.Title`, `@res.Crate`, `@res.Author.UserName`, `@res.RequiresSA1`), and `ViewData` keys (`Title`, `ShowHero`) must be strictly maintained.
3. **Route Link Attributes:** `asp-controller="Home"`, `asp-controller="Resource"`, and `asp-action="Index"` routing tag helpers must remain intact across navigation elements.
4. **Logo Location:** `logo_final.png` is permanently located at `SNEStorage/wwwroot/images/logo_final.png` and served via `~/images/logo_final.png`.
