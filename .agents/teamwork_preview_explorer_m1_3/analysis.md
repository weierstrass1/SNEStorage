# SNEStorage UI Redesign Specification & Visual Analysis

## Executive Summary
This document defines the complete visual design language and Vanilla CSS architecture for the **SNEStorage** frontend redesign. Designed for a 1990s Super Nintendo Entertainment System (SNES) / Super Famicom aesthetic, this design system combines retro CRT hardware feel, arcade neon accents, glassmorphism paneling, and pure Vanilla CSS layout techniques without relying on any external frameworks (Bootstrap, Tailwind, etc.).

---

## 1. Visual Analysis of `logo_final.png` & Design Language

### Image Inspection & Asset Breakdown
* **Asset Location**: `SNEStorage/wwwroot/images/logo_final.png`
* **Subject & Theme**: Iconic character illustration of Mario running while carrying a wooden/cardboard storage box ("Crate"), sealed with industrial duct tape and stamped with the classic Super Famicom 4-button oval emblem.
* **Core Design Elements**:
  1. **Super Famicom 4-Button Palette**: The four oval button colors on the box represent the core action palette:
     - **Cyan Blue** (`#0080FF` / `#00F0FF` accent)
     - **Vibrant Red** (`#E60012`)
     - **Golden Yellow** (`#FFCC00`)
     - **Grass Green** (`#00A651`)
  2. **Storage Crate Logistics Palette**:
     - Cardboard Tan/Brown (`#C88A4B` highlight to `#8B5A2B` shadow)
     - Duct Tape Industrial Gray (`#B8B8B8`)
  3. **Retro Arcade Outlines**: Heavy black ink outlines (`#000000`) defining all geometric shapes, giving a crisp 16-bit box-art look.
  4. **Space / Cosmic WRAM Background Theme**: Complements the floating hero logo with deep space dark purple backgrounds (`#09090E`, `#0F0A1C`, `#2B1055`) and neon orbital accent rings (`#00F0FF` Cyan ring, `#FF007F` Magenta ring).

---

## 2. Vanilla CSS Architecture Strategy

### Elimination of External Frameworks
To guarantee maximum performance, crisp retro rendering, and complete control over the layout, the frontend redesign strictly uses pure Vanilla CSS.
- **Removed**: All Bootstrap grid (`bootstrap.min.css`) and utility dependency overrides.
- **Scoped Naming**: All design tokens and components use the `.snes-*` prefix to prevent collision and maintain modular code organization.
- **File Structure**:
  - `wwwroot/css/site.css`: Central design system (CSS variables, reset, CRT scanlines, typography, layout grid, cards, buttons, tables, badges, footers).
  - `wwwroot/css/home.css`: Home page hero and landing specific overrides.
  - `wwwroot/css/Create.css`: Form inputs, selects, textareas, and modal form styling.

---

## 3. CRT Scanline & Screen FX Engine

The retro CRT monitor simulation is implemented using a pure CSS overlay that creates an authentic 90s TV tube visual atmosphere.

```css
/* CRT Overlay Container */
.snes-crt-overlay {
    position: fixed;
    top: 0;
    left: 0;
    width: 100vw;
    height: 100vh;
    pointer-events: none;
    z-index: 9999;
    overflow: hidden;
    
    /* Horizontal Scanlines & RGB Subpixel Separation Mask */
    background: 
        linear-gradient(
            rgba(18, 16, 26, 0) 50%, 
            rgba(0, 0, 0, 0.35) 50%
        ),
        linear-gradient(
            90deg, 
            rgba(255, 0, 0, 0.04), 
            rgba(0, 255, 0, 0.02), 
            rgba(0, 0, 255, 0.04)
        );
    background-size: 100% 4px, 6px 100%;
    opacity: 0.85;
    animation: snes-crt-flicker 0.15s infinite, snes-scanline-roll 12s linear infinite;
}

/* CRT Vignette Edge Shadow */
.snes-crt-overlay::before {
    content: "";
    position: absolute;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    background: radial-gradient(
        circle at 50% 50%,
        transparent 65%,
        rgba(0, 0, 0, 0.45) 85%,
        rgba(0, 0, 0, 0.85) 100%
    );
    box-shadow: inset 0 0 100px rgba(0, 0, 0, 0.7);
}

@keyframes snes-scanline-roll {
    0% { background-position: 0 0, 0 0; }
    100% { background-position: 0 100%, 0 0; }
}

@keyframes snes-crt-flicker {
    0% { opacity: 0.82; }
    50% { opacity: 0.87; }
    100% { opacity: 0.83; }
}
```

---

## 4. Retro SNES Color Palette & CSS Custom Properties

```css
:root {
    /* SNES Hardware Console Palette */
    --snes-purple-dark: #2b1055;
    --snes-purple-main: #5b21b6;
    --snes-purple-light: #7c3aed;
    --snes-purple-glow: #a78bfa;
    --snes-console-gray: #d1d5db;
    --snes-console-darkgray: #374151;
    
    /* Super Famicom 4-Button Palette (from logo_final.png) */
    --snes-red: #e60012;
    --snes-yellow: #ffcc00;
    --snes-green: #00a651;
    --snes-blue: #0080ff;
    
    /* Cyber Neon & Arcade Accents */
    --snes-cyan: #00f0ff;
    --snes-magenta: #ff007f;
    --snes-gold: #ffd700;
    
    /* Crate Logistics Palette */
    --snes-crate-brown: #c88a4b;
    --snes-crate-dark: #8b5a2b;
    --snes-tape-gray: #b8b8b8;
    
    /* Background Void & Space Palette */
    --snes-bg-dark: #09090e;
    --snes-bg-space: #0f0a1c;
    --snes-bg-card: rgba(23, 15, 42, 0.75);
    --snes-bg-card-border: rgba(167, 139, 250, 0.25);
    --snes-bg-nav: rgba(12, 9, 24, 0.88);
    
    /* Typography Colors */
    --snes-text-main: #f3f4f6;
    --snes-text-muted: #9ca3af;
    --snes-text-dim: #6b7280;
    
    /* Radius Tokens */
    --snes-radius-sm: 4px;
    --snes-radius-md: 10px;
    --snes-radius-lg: 18px;
    --snes-radius-pill: 9999px;
    
    /* Glow & Shadow Tokens */
    --snes-glow-purple: 0 0 20px rgba(124, 58, 237, 0.6);
    --snes-glow-cyan: 0 0 20px rgba(0, 240, 255, 0.6);
    --snes-glow-magenta: 0 0 20px rgba(255, 0, 127, 0.6);
    --snes-shadow-box: 0 10px 30px rgba(0, 0, 0, 0.6);
    
    /* Typography Stacks */
    --snes-font-display: 'Press Start 2P', 'Silkscreen', cursive, sans-serif;
    --snes-font-sans: 'Inter', system-ui, -apple-system, sans-serif;
    --snes-font-mono: 'Share Tech Mono', 'Courier New', monospace;
}
```

---

## 5. Retro Typography & Header Text Shadow Specifications

```css
/* Metallic 90s SNES Header Title Gradient */
.snes-title, .snes-heading-xl {
    font-family: var(--snes-font-sans);
    font-size: 3.5rem;
    font-weight: 900;
    font-style: italic;
    text-transform: uppercase;
    text-align: center;
    margin: 1.5rem 0;
    line-height: 1.1;
    
    background: linear-gradient(
        180deg, 
        #ffffff 0%, 
        #a78bfa 30%, 
        #5b21b6 65%, 
        #ff007f 100%
    );
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
    
    filter: 
        drop-shadow(2px 2px 0px #000) 
        drop-shadow(-2px -2px 0px #000) 
        drop-shadow(2px -2px 0px #000) 
        drop-shadow(-2px 2px 0px #000)
        drop-shadow(0px 8px 15px rgba(0, 0, 0, 0.9));
}

/* Pixel Font Utility */
.snes-font-pixel {
    font-family: var(--snes-font-display);
    line-height: 1.6;
}

/* Monospace Memory Code Utility */
.snes-font-mono {
    font-family: var(--snes-font-mono);
}
```

---

## 6. Component Architecture

### A. Floating Hero Logo & Orbital Rings
- `.snes-hero-logo-wrapper`: Central wrapper for `logo_final.png` with vertical floating animation (`snes-logo-float`).
- `.snes-ring-1` & `.snes-ring-2`: Rotating elliptical neon rings positioned behind the logo (`border: 3px solid var(--snes-cyan)` and `border: 3px solid var(--snes-magenta)`) with box-shadow glows.

### B. Beveled Panels & Glass Cards
- `.snes-card` / `.snes-panel`: Glassmorphic container with backdrop blur (`backdrop-filter: blur(14px)`), semi-transparent WRAM purple background, and rounded corners (`18px`).
- `.snes-panel-pixel`: Retro 3D beveled panel with 4px purple border (`#7c3aed`) and inset highlight/shadow bevels (`inset -4px -4px 0px #2b1055, inset 4px 4px 0px #a78bfa`).

### C. Retro Interactive Buttons
- `.snes-btn`: Pill-shaped / chunky retro buttons with subtle scale animation on hover.
- `.snes-btn-primary`: Purple to Magenta gradient with neon glow shadow.
- `.snes-btn-accent`: Super Famicom Yellow/Red gradient with black bold lettering.
- `.snes-btn-outline`: Glass backdrop button with glowing border.

### D. Mod Repository Tables & Badges
- `.snes-table`: Spaced table layout with dark semi-transparent row cells, rounded ends, hover highlights (`rgba(167, 139, 250, 0.12)`), and glowing header titles.
- `.snes-badge`: Pill badges used for Crate categories (Sprites, Blocks, Patches, UberASM) with gradient background and neon box shadows.

---

## 7. Pure CSS Responsive Layout Grid

A responsive 12-column grid built without external grid libraries:
- Responsive Breakpoints:
  - **Desktop**: 1200px container, 12-column grid.
  - **Laptop/Tablet Landscape (<=992px)**: Columns collapse, title sizes adjust, orbital ring diameters scale down.
  - **Tablet Portrait (<=768px)**: 1-column stack, mobile nav spacing, compact card padding.
  - **Mobile (<=480px)**: Hides large orbital background rings to prevent overflow, adjusts title font size to 1.5rem.

---

## Summary of Findings & Verification Method
The current CSS implementation in `SNEStorage/wwwroot/css/site.css` and layout in `SNEStorage/Views/Shared/_Layout.cshtml` successfully implements this specification without external frameworks.
