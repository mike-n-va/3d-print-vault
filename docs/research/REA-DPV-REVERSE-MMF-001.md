# REA-DPV-REVERSE-MMF-001: MyMiniFactory Library UX & Curation Architecture Analysis

**Document Reference:** `docs/research/REA-DPV-REVERSE-MMF-001.md`  
**Status:** Completed Research & Evaluation  
**Date:** 2026-11-06  
**Governing Standard:** [`DEVELOPMENT_STANDARD.md`](file:///c:/Project/3d-print-vault/DEVELOPMENT_STANDARD.md)  
**Governing Authorities & References:**
- [`ADR-DPV-MODEL-001`](file:///c:/Project/3d-print-vault/docs/architecture/decisions/ADR-DPV-MODEL-001.md) (Model as the Primary Catalog Entity)
- [`ADR-DPV-CATALOG-001`](file:///c:/Project/3d-print-vault/docs/architecture/decisions/ADR-DPV-CATALOG-001-Visual-Catalog-Experience.md) (Visual Catalog Experience)
- [`REA-DPV-SET-BROWSING-001`](file:///c:/Project/3d-print-vault/docs/research/REA-DPV-SET-BROWSING-001.md) (Release and Collection Hero Card Experience)
- `EDR-DPV-V2-001` (DPV v2.0 Maker Sanctuary Blueprint)

**Activity Scope:** Pure Research, UX Evaluation, and Interaction Architecture Analysis. *(No production code modifications, schema migrations, or external network dependencies).*

---

## 1. Executive Summary: From Disk Utility to Maker Sanctuary

Desktop software managing 3D printable models typically inherits the visual grammar of operating system file managers: rigid hierarchical tree views, flat rows of alphanumeric file strings, raw byte sizes, and generic file-type glyphs. When confronting a real-world collection containing tens of thousands of geometry files, slicing plates, and preview renders, this "Disk Utility" mental model collapses under cognitive fatigue. Tabletop makers, hobbyists, and studio painters do not think of their creative assets as `c:\3dprints\patreon\2026-03\orcs\v2_supp_arm_r_03.stl`; they think:

> *"I own the March 2026 'Bloodstone Warlords' release by Titan-Forge, and I want to print the modular Chieftain."*

To understand how to elevate 3D Print Vault (DPV) into a premier **Maker Sanctuary**, this research analyzes the benchmark digital library experience of **MyMiniFactory (MMF)**—specifically its cloud collection interface (`myminifactory.com/library#shared`), its **Tribes** monthly subscription drops, and its **Crowdfunding/Campaign Pledge** delivery architecture.

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│                       THE COGNITIVE SHIFT                                   │
├──────────────────────────────────────┬──────────────────────────────────────┤
│ Legacy File Scanner (Utility)        │ MMF / DPV v2.0 (Curated Sanctuary)   │
├──────────────────────────────────────┼──────────────────────────────────────┤
│ • Flat list of 10,000 files          │ • Cohesive releases and studios      │
│ • Focus on filenames and paths       │ • Focus on hero artwork and renders  │
│ • "File not found" anxiety           │ • Curated ownership pride            │
│ • Raw geometry fragments             │ • Complete multi-part miniatures     │
│ • Technical noise exposed upfront    │ • Progressive disclosure on demand   │
└──────────────────────────────────────┴──────────────────────────────────────┘
```

MMF achieves high user satisfaction because it treats digital 3D models with the visual reverence of an art museum and the structural clarity of a high-end streaming service. By systematically mapping MMF's information architecture, visual card anatomy, progressive disclosure patterns, and curation psychology, this research establishes a definitive UX blueprint for DPV v2.0.

---

## 2. Information Architecture & Object Hierarchy

### 2.1 The Four-Tiered MMF Object Model

In the MMF shared library ecosystem, users never encounter a naked `.stl` file without contextual framing. The platform organizes assets into four strict, progressive layers:

```mermaid
flowchart TD
    Creator["1. Creator Studio / Tribe\n(e.g., Artisan Guild)"]
    Campaign["2. Campaign / Monthly Release\n(e.g., March 2026: The Crimson Paladins)"]
    Model["3. Object / Model Kit\n(e.g., Paladin Grand Inquisitor)"]
    Deliverables["4. Files / Deliverables Drawer\n(Head.stl, Torso.stl, PreSupported.lys, Render.png)"]

    Creator --> Campaign
    Campaign --> Model
    Model --> Deliverables
```

1. **Creator Studio / Tribe (Level 1):** The creative identity and brand. Creators have high-contrast avatar badges, studio banners, subscriber tier indicators, and curated portfolios.
2. **Campaign / Monthly Release (Level 2):** The thematic collection container. Represents a bounded release event (e.g., a Kickstarter campaign, a Frontiers project, or a monthly Patreon/Tribes distribution bundle). It groups anywhere from 10 to 60 distinct models under unified artwork and narrative lore.
3. **Object / Model Kit (Level 3):** The atomic functional unit of the catalog. The model represents the complete physical miniature, terrain piece, or vehicle. It holds ratings, tags, paint guides, and high-resolution render carousels.
4. **Deliverables / File Drawer (Level 4):** The physical digital assets. This includes unsupported geometry, resin pre-supported geometry, slicer project files (Chitubox/Lychee), assembly guides, and stat cards. Crucially, **this drawer is collapsed by default**, surfaced only when the user intends to download or inspect files.

#### 2.1.1 Verified Live API Payload Schema (`library?v=all-objects`)
Direct telemetry extracted from live MyMiniFactory sessions confirms the exact production JSON field structure returned for library items:

```json
{
  "id": "object-643722",
  "name": "Tinker Fairy Standalone [presupported]",
  "originalId": 643722,
  "type": "object",
  "url": "tinker-fairy-standalone-presupported-643722",
  "creator": {
    "id": 987862,
    "username": "nerikson",
    "name": "Nerikson",
    "avatarUrl": "https://images2.myminifactory.com/insecure/rt:fill-down/w:200/h:200/.../avatar.jpg"
  },
  "previewUrl": "https://assets.myminifactory.com/.../1000X1000-Tinker Fairy Standalone_Beauty Render.jpg",
  "images": [
    {
      "url": "https://assets.myminifactory.com/.../720X720-Tinker Fairy Standalone_Beauty Render.jpg",
      "thumbnailUrl": "https://assets.myminifactory.com/.../230X230-Tinker Fairy Standalone_Beauty Render.jpg",
      "isPrimary": false
    },
    {
      "url": "https://assets.myminifactory.com/.../720X720-Tinker Fairy Standalone_Clay Render.jpg",
      "thumbnailUrl": "https://assets.myminifactory.com/.../230X230-Tinker Fairy Standalone_Clay Render.jpg",
      "isPrimary": false
    }
  ],
  "description": "<p>Printing information:<br/>-Presupported: YES<br/>-figure scale: 140mm+75mm+32mm</p>"
}
```

### 2.2 Contrast with Traditional Local File Directories

Local desktop storage is notorious for folder fragmentation and inconsistent naming conventions across creators:

```text
Physical Disk Reality:
D:\Miniatures\
  ├── Artisan_Guild\
  │     └── 2026_03_Paladins\
  │           ├── Supported\
  │           │     ├── Inquisitor_Body_Supp.stl
  │           │     └── Inquisitor_Weapon_Supp.stl
  │           ├── Unsupported\
  │           │     ├── Inquisitor_Body.stl
  │           │     └── Inquisitor_Weapon.stl
  │           └── Renders\
  │                 └── Promo_Banner.jpg
  └── CyberForge_SciFi\
        └── Release_April\
              └── Mech_Unit.zip (unextracted archive)
```

| Dimension | Raw Physical Filesystem | MMF Object Model | DPV v2.0 Translation |
| :--- | :--- | :--- | :--- |
| **Primary Pivot** | Directory path string | Creator & Release Theme | Relational Model & Release Entities |
| **Asset Identity** | File extension & filename | Painted Hero Visual | Hero Artwork Discovery Engine |
| **Variants** | Separate files in subfolders | Model-level toggle (Pre-supported vs. Raw) | Asset Role Classifier (`SupportedVariant`) |
| **Completeness** | Unknown (user must check files) | Release model count badge (`[24 Objects]`) | Cluster Manifest Validation |
| **Metadata** | Non-existent or buried in text files | Studio lore, tags, scale, build volume | Inferred directory tiers & `.dpv.json` sidecars |

MMF completely abstracts away the physical filesystem: users do not know or care whether an asset is stored in an S3 bucket or distributed CDN folder. DPV v2.0 must deliver this same clean abstraction **while maintaining non-destructive, read-only respect for local disk paths**.

---

## 3. Visual Cues & Card Presentation Anatomy

MMF's visual hierarchy is engineered around instant aesthetic resonance. Below is an anatomical breakdown of MMF's card presentation compared to DPV's target architecture.

### 3.1 The Release Hero Card ($16:9$ Widescreen)

For high-level browsing across an artist's portfolio or library drops, MMF uses cinematic widescreen cards that emphasize set-wide cohesion:

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│ [======================== HERO CINEMATIC BANNER ==========================] │
│ [                     Painted Showcase Render (16:9)                      ] │
│ [                                                                         ] │
│ [ ★ FAVORITE ]                                        [ 28 OBJECTS ]       │
├─────────────────────────────────────────────────────────────────────────────┤
│ THE CRIMSON CITADEL                                        MARCH 2026 DROP  │
│ Studio: Artisan Guild                              [ Pre-Supported Resin ]  │
└─────────────────────────────────────────────────────────────────────────────┘
```

- **Aspect Ratio:** $16:9$ widescreen banner. 3D creators invest heavily in box-art banners and cinematic landscape renders. Adopting $16:9$ honors their promotional graphic design.
- **Top Badge Layer:** 
  - Floating translucent bookmark/favorite star in the top-left corner.
  - Count pill in the top-right corner (`[28 Objects]`) giving instant scale to the release.
- **Bottom Typography Layer:**
  - High-contrast, bold title (uppercase or title-case).
  - Studio pill displaying creator name with monogram avatar.
  - Technical readiness pill: `[Pre-Supported Resin]`, `[FDM Optimized]`.

### 3.2 The Model Object Card ($1:1$ Square / $4:3$)

When inspecting the contents of a release or performing a global model search, MMF transitions to individual model cards:

```text
┌──────────────────────────────────────────┐
│ [======================================] │
│ [                                      ] │
│ [          HERO MODEL RENDER           ] │
│ [               (1:1)                  ] │
│ [                                      ] │
│ [ ♡ ]                  [ PRE-SUPPORTED ] │
├──────────────────────────────────────────┤
│ Grand Inquisitor Malakor                 │
│ Artisan Guild • 32mm Heroic              │
│ [ 4 Files ] [ 3D Preview Available ]     │
└──────────────────────────────────────────┘
```

#### Production Image Resolution Pipeline (Empirical Live Telemetry)
Live network inspection of `myminifactory.com/library?v=all-objects` demonstrates a standardized three-tier square ($1:1$) asset pipeline:
- **Grid Thumbnail (`230X230` px):** Rendered inside the 5-column library grid (`thumbnailUrl: .../230X230-Character Beauty Render.jpg`).
- **Card Hover & Modal Preview (`720X720` px):** Loaded on hover scrub and preview carousels (`url: .../720X720-Character Beauty Render.jpg`).
- **Master Lightbox Hero (`1000X1000` px):** Full-resolution master render for inspector deep-zoom (`previewUrl: .../1000X1000-Character Beauty Render.jpg`).
- **Creator Monogram (`200X200` px):** Cached via imgproxy (`rt:fill-down/w:200/h:200/...`) and rendered inside a 32px circular avatar badge.

#### Standard Image Role Conventions Observed in Production:
- `Beauty Render`: High-fidelity painted or studio-lit hero shot.
- `Clay Render` (1 through 5): Multi-angle turntable clay sculpts showing mesh topology.
- `Whats included` / `SetPoster`: Assembly guides, component matrices, and campaign graphics.
- `Sizes`: Physical millimeter scale comparison references ($32\text{mm}$, $75\text{mm}$, $140\text{mm}$).

- **Clean Renders Over CAD Wireframes:** Cards display high-resolution studio photographs, digital turntable renders, or painted box art. Raw isometric grey wireframes are reserved strictly as fallbacks when no artwork exists.
- **Micro-Badges (Status Taxonomy):**
  - `[Pre-Supported]`: Signals resin print readiness without requiring slicer prep.
  - `[Chitubox] / [Lychee]`: Indicates native slicer support project inclusion.
  - `[Multi-Part Kit]`: Informs the maker that the miniature requires assembly.
  - `[FDM Supportless]`: Highly valued for filament printers.
- **Hover Transitions:** Hovering over a card gently lifts the card (`translateY(-4px)`), deepens the drop shadow, and reveals quick-action buttons: *Add to Print Queue*, *Toggle Favorite*, or *Quick View*.

---

## 4. The User Journey & Navigation Flow

### 4.1 Progressive Disclosure: The 3-Click Rule

MMF enforces a strict progressive disclosure discipline: **never show technical specifications until the user has expressed interest in printing or inspecting the item.**

```mermaid
sequenceDiagram
    autonumber
    actor Maker as Tabletop Maker
    participant Shell as Catalog Navigation Rail
    participant ReleaseView as Release Gallery (16:9)
    participant ModelGrid as Model Grid (1:1)
    participant Drawer as Inspector / File Drawer

    Maker->>Shell: Clicks "Artists" or "Releases"
    Shell->>ReleaseView: Renders Studio Portfolios & Monthly Sets
    Maker->>ReleaseView: Selects "March 2026: The Crimson Citadel"
    ReleaseView->>ModelGrid: Navigates to Set Content (28 Models)
    Maker->>ModelGrid: Clicks "Grand Inquisitor Malakor"
    ModelGrid->>Drawer: Slides open Inspector Drawer
    Note over Drawer: Reveals Part List, Pre-supported STL sizes, and 3D Viewer
```

### 4.2 Click-Depth Analysis

1. **Step 1: Contextual Entry (Click 0):** The maker arrives at the Vault. The landing view is a curated discovery surface featuring *Recently Added Sets*, *Creator Hubs*, and *Favorited Collections*.
2. **Step 2: Set Selection (Click 1):** The maker clicks a Release Hero Card. The UI seamlessly transitions into the Release Detail page, presenting the release's narrative description, total model count, and the responsive grid of member models.
3. **Step 3: Model Inspection (Click 2):** The maker clicks a specific Model card. An Inspector Panel smoothly slides in from the right edge without modal blocking, presenting:
   - High-resolution turntable render carousel.
   - Scale, sculptor attribution, and lore notes.
   - Part breakdown tree (`Head`, `Torso`, `Scenic Base`).
4. **Step 4: Print Action (Click 3):** The maker selects their desired slicing variant (`Supported STL` or `Raw Geometry`) and triggers *Open in Slicer* or *Add to Print Queue*.

At no point in Clicks 0 through 2 was the user subjected to disk paths (`C:\...`), file extensions (`.stl`), or byte size statistics.

---

## 5. Collection Ownership & Curation Psychology

### 5.1 The Psychology of Pride vs. Maintenance

Why does a user spend hours organizing an MMF library or digital miniature vault?
Behavioral analysis reveals three primary collector motivations:

1. **The Trophy Room Effect (Ownership Pride):** Miniature collectors view their collection as an artistic asset portfolio. Seeing high-res cover art, studio branding, and completed set badges triggers positive feelings of accomplishment and ownership. Conversely, viewing a Windows Explorer directory triggers feelings of chores, unorganized clutter, and storage anxiety.
2. **Creative Brainstorming (Inspiration Discovery):** When preparing for a tabletop campaign (e.g., Dungeons & Dragons or Warhammer), game masters browse visually to find inspiration. Browsing is associative: *"I need an imposing villain"* is answered by visual atmosphere, not alphabetical file searching.
3. **Print-Readiness Confidence:** Makers experience high anxiety regarding print failures. When an interface prominently validates that an asset is `[Pre-Supported]` and `[Verified]`, anxiety drops and printing throughput increases.

### 5.2 Abstraction of Storage Mechanics

In MMF, storage mechanics are completely invisible. Users interact exclusively with logical entities:

$$\text{User Concept} = \text{Model} \quad \Big(\ne \sum_{i=1}^n \text{Physical Files}_i\Big)$$

In DPV v2.0, while physical storage is local and immutable, the UI must maintain this exact psychological illusion:
- **Zero Path Clutter:** File paths are relegated to the bottom of the technical inspector tab under an expandable "Filesystem Properties" disclosure header.
- **Unified Actions:** "Favorite" favorites the *Model* or the *Release*, automatically propagating state across all constituent files.
- **Virtual Tags vs. Physical Folders:** Tags (`[Vampires]`, `[Undead]`, `[Boss]`) transcend physical disk boundaries, allowing models stored in different artist folders to appear together in custom thematic collections.

---

## 6. Gap Analysis: Cloud Platform vs. Local Offline Vault

While MMF provides the gold standard for curation UX, its architectural foundation is cloud-native. DPV must bridge substantial technical divides to deliver this experience offline on local hardware:

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│                              GAP ANALYSIS MATRIX                            │
├──────────────────────────┬───────────────────────┬──────────────────────────┤
│ Architectural Vector     │ MyMiniFactory (Cloud) │ 3D Print Vault (Local)   │
├──────────────────────────┼───────────────────────┼──────────────────────────┤
│ Backend Persistence      │ Cloud RDBMS (AWS RDS) │ Embedded SQLite (Dapper) │
│ Asset Ingestion          │ Web Admin Upload Form │ Automated Disk Scanner   │
│ Media Storage            │ CloudFront CDN / S3   │ Local Disk / Win32 FS    │
│ Artwork Generation       │ Cloud Image Pipeline  │ Pure Managed C# Pipeline │
│ Metadata Source          │ Cloud Database        │ Directory Parser & JSON  │
│ Network Dependency       │ Mandatory 100% Online │ 100% Offline / Air-Gapped│
│ Modifiability            │ Platform Governed     │ User Owns Local Files    │
└──────────────────────────┴───────────────────────┴──────────────────────────┘
```

### 6.1 Challenges & Mitigations for Local Execution

1. **Missing Cover Artwork:**
   - *Cloud:* Creators are forced to upload $16:9$ promotional banners during release submission.
   - *Local:* Users often download zip files lacking top-level banners.
   - *DPV Mitigation:* Implement the deterministic **4-Tier Artwork Discovery Engine** established in Sprint 007 (P1: Dedicated banner file $\rightarrow$ P2: High-res render image $\rightarrow$ P3: Pure managed PNG collage synthesis $\rightarrow$ P4: Elegant geometric fallback).
2. **Directory Inconsistency:**
   - *Cloud:* Schema enforces creator, release, and object relationships at database entry time.
   - *Local:* Users organize folders by year, kickstarter name, or flat dumps.
   - *DPV Mitigation:* Heuristic clustering (`ModelDiscoveryEngine`) utilizing token weighting, common prefix extraction, and pre-supported pair detection to infer sets automatically.
3. **Performance at Scale (100,000+ Files):**
   - *Cloud:* Paginated API endpoints with server-side caching.
   - *Local:* Must render fluidly on a single desktop UI thread.
   - *DPV Mitigation:* High-speed virtualized wrap panels, decoupled background thumbnail generation via bounded channels, and SQLite indexed queries.

---

## 7. Actionable Design Recommendations for DPV v2.0

To realize the vision of the **Maker Sanctuary**, DPV v2.0 should implement the following eight UI/UX rules:

### Rule 1: The Artwork-First Presentation Standard
No card in the catalog should ever render a sterile grey box or raw directory path when rendered in Grid View. If no artist render is found on disk, the system must synthesize a multi-image composite collage from the constituent models rather than showing a generic placeholder.

### Rule 2: Strict Aspect Ratio Rhythm
Maintain strict visual cadence across the interface:
- **Releases / Campaigns:** Fixed $16:9$ cinematic banner cards ($320 \times 240\text{ px}$ container with $16:9$ image frame).
- **Models / Miniatures:** Fixed $1:1$ square cards with smooth corner radii ($8\text{ px}$) and subtle borders.
- **Artists / Studios:** Circular monogram avatars or wide studio banners.

### Rule 3: Hide File Paths from Primary Discovery
Directory paths (`D:\Miniatures\...`) must never appear on catalog cards, release headers, or dashboard lists. File paths belong exclusively inside the Model Inspector under a collapsed "Disk Location" drawer equipped with a single-click *"Show in File Explorer"* button.

### Rule 4: Elevate Release and Artist Portfolios to First-Class Nav
Add dedicated visual portfolio views to the primary navigation rail:
- `Models` (Global atomic search)
- `Releases` (Hero set gallery)
- `Artists` (Creator studio directory)
- `Favorites` (Curated showcase of starred models, sets, and studios)

### Rule 5: Status Badge Taxonomy
Standardize visual badges across all model cards using subtle high-contrast pills:
- `[Pre-Supported]` (Emerald green border/background tint)
- `[Unsupported]` (Neutral slate tint)
- `[Multi-Part: X Parts]` (Indigo tint)
- `[3MF Project]` (Amber tint)

### Rule 6: S/M/L Density Scaling
Makers use varying monitor setups (from 14" laptop screens to 32" 4K studio monitors). Provide instant zoom scaling controls (`Small: 180px`, `Medium: 240px`, `Large: 320px`) with fluid wrapping and state persistence across application restarts.

### Rule 7: Contextual Metadata Pivoting
Every creator name, collection pill, and tag chip must be an interactive button. Clicking *"Artisan Guild"* on a model card must immediately pivot the UI to that artist's dedicated portfolio page; clicking *"Dragons"* must instantly filter the catalog to that thematic tag.

### Rule 8: Non-Modal Inspector Drawer
When inspecting model details, the UI must never present a blocking modal dialog. Instead, open a responsive right-hand slide-over drawer that keeps the underlying gallery visible and browsable.

---

---

## 9. Empirical Evidence & Network Telemetry (HAR Analysis)

To substantiate these UX recommendations with empirical data, network telemetry was extracted from an active authenticated MyMiniFactory library session (`www.myminifactory.com.har`, 123 HTTP entries, 4.18 MB).

### 9.1 The Internal `/api/data-library/` Architecture
MMF operates a dedicated internal API namespace specifically tailored for library browsing:

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│                      MMF DATA LIBRARY API TOPOLOGY                          │
├─────────────────────────────────────┬──────┬────────────────────────────────┤
│ Endpoint Route                      │ Type │ Primary Architectural Purpose  │
├─────────────────────────────────────┼──────┼────────────────────────────────┤
│ /api/data-library/objectPreviews    │ JSON │ Full-library index manifest    │
│ /api/data-library/objects?ids[]={…} │ JSON │ 20-item batched hydration      │
│ /api/data-library/collections       │ JSON │ Virtual thematic collections   │
│ /api/data-library/user-tags         │ JSON │ User-assigned tagging facets   │
│ /object/{id}/related?page=1&store=1 │ HTML │ Card component HTML snippets   │
│ /api/v2/objects/{id}/comments       │ JSON │ Community makes & print status │
└─────────────────────────────────────┴──────┴────────────────────────────────┘
```

#### The 2-Stage Lazy Hydration Pattern
MMF separates index discovery from entity detail loading:
1. **Stage 1 (Manifest Discovery):** The client first fetches `/api/data-library/objectPreviews`, obtaining an array of lightweight object identifiers and primary preview URLs for the user's entire collection.
2. **Stage 2 (Batched Window Hydration):** As the user scrolls through the 5-column grid, the client groups 20 visible IDs into a single batch request:
   ```http
   GET /api/data-library/objects?ids[]=872969&ids[]=840883&ids[]=657783...
   ```
   This returns full metadata records containing HTML descriptions, multi-angle render galleries, and creator profile information.

*DPV v2.0 Application:* This mirrors DPV’s SQLite paging pattern. Top-level catalog views query lightweight `ModelSummaryDto` (`SELECT Id, Name, CoverImagePath, ModelCount FROM Models LIMIT 24 OFFSET @offset`), deferring physical file role queries and sidecar parsing until the user opens the Model Inspector drawer.

---

### 9.2 The Standardized Image Resolution Pipeline ($1:1$)

Telemetry confirms that all model artwork is strictly normalized into a **three-tier square aspect ratio ($1.00:1$)**:

```mermaid
flowchart LR
    Master["Master Hero Render\n(previewUrl: 1000x1000 px)"] --> Hover["Hover / Modal Preview\n(url: 720x720 px)"]
    Hover --> Grid["Library Grid Thumbnail\n(thumbnailUrl: 230x230 px)"]
    CDN["imgproxy Engine\n(rt:fill-down/w:200/h:200)"] --> Avatar["Creator Monogram Avatar\n(32px circular render)"]
```

- **`thumbnailUrl` (230×230 px):** The exact rendered dimension of model cards in the 5-column grid.
- **`url` (720×720 px):** Higher-fidelity render utilized for card hover states and detail modal popovers.
- **`previewUrl` (1000×1000 px):** Full-resolution uncompressed master used for turntable inspection and deep-zoom lightboxes.
- **`avatarUrl` (200×200 px):** Square creator portrait resized to a 32px diameter circle.

---

### 9.3 Standard Image Semantic Taxonomy
Filenames returned in production API payloads adhere to a consistent naming hierarchy:
1. **`Beauty Render`** (e.g., `1000X1000-Tinker Fairy Standalone_Beauty Render.jpg`): The primary showcase image featuring professional lighting and painted textures.
2. **`Clay Render` (`Clay Render-1` to `Clay Render-5`):** Untextured grey sculpts captured from sequential angles (Front, 3/4, Profile, Back, Top) validating mesh topology.
3. **`Whats included` / `SetPoster`:** Visual diagrams exhibiting disassembled parts, weapons, bases, and variant options.
4. **`Sizes`** (e.g., `Sizes 32.jpg`, `Sizes 75.jpg`, `Sizes 140.jpg`): Scale diagrams displaying physical height benchmarks against tabletop standards.

---

### 9.4 Embedded Print Metadata Syntax
Production records demonstrate that technical print specifications are embedded in structured HTML within the `description` string rather than separate database fields:

```text
Printing information:
-human figure scale : 140mm+75mm+32mm
-character size : 157x148x181mm (140mm)
-largest piece : 119x118x153mm (140mm)
-Presupported : YES
Files: files are provided supported and unsupported.
```

*DPV v2.0 Application:* DPV’s metadata inference engine (`MetadataInferenceService`) should incorporate regex extraction rules to scan sidecars, Readme files, and description text for standard token patterns:
- Pre-supported status: `-Presupported\s*:\s*(YES|NO)`
- Scale: `-human figure scale\s*:\s*(\d+mm[+\d+mm]*)`
- Bounding box: `-character size\s*:\s*([\dx\s]+mm)`

---

## 10. Final Verdict: The Blueprint for the Frontend Transformation

MyMiniFactory's enduring success proves that makers do not want a faster file explorer—**they want an art gallery for their imagination and a reliable launchpad for their physical craft**.

By transitioning DPV from a file-centric scanner into an artwork-first **Maker Sanctuary**, we fulfill the foundational promises of [`ADR-DPV-MODEL-001`](file:///c:/Project/3d-print-vault/docs/architecture/decisions/ADR-DPV-MODEL-001.md) and [`ADR-DPV-CATALOG-001`](file:///c:/Project/3d-print-vault/docs/architecture/decisions/ADR-DPV-CATALOG-001-Visual-Catalog-Experience.md). The architectural principles established in this research—four-tiered object hierarchy, $16:9$ release hero presentation, progressive technical disclosure, and pure offline artwork synthesis—provide the definitive blueprint for DPV v2.0.

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│                          RESEARCH VERDICT                                   │
├─────────────────────────────────────────────────────────────────────────────┤
│ • Status: APPROVED FOR SPRINT 6 & V2.0 FRONTEND EXECUTION                  │
│ • Impact: Establishes UX hierarchy: Creator → Release → Model → Files       │
│ • Next Milestone: Align Avalonia UI components with MMF Curation Standards  │
└─────────────────────────────────────────────────────────────────────────────┘
```
