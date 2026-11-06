# SPRINT-007: Collections Before Models

**Sprint Reference:** `docs/sprints/SPRINT-007-Collections-Before-Models.md`  
**Governing Standard:** `DEVELOPMENT_STANDARD.md`  
**Status:** Completed  
**Sprint Theme:** Collections Before Models — Transforming DPV from an atomic model file browser into an immersive Campaign, Set, and Release collection showcase.

---

## 1. Overview

Sprint 007 establishes the **Release / Set Layer** across the domain, persistence, artwork detection, and Avalonia presentation systems:
$$\text{Artist / Studio} \longrightarrow \text{Release / Set} \longrightarrow \text{Model} \longrightarrow \text{Assets}$$

## 2. Completed User Stories

- **S7-001 (Release Domain Foundation):** Created `ReleaseSummaryDto`, `ReleaseDetailDto`, and `ReleaseStatisticsDto`.
- **S7-002 (Release Repositories):** Implemented `IReleaseCatalogQuery` and `IReleaseDetailRepository` via `DapperReleaseRepository`.
- **S7-003 (Deterministic Release Cover Detection):** Implemented priority resolution in `ReleaseArtworkService` (Dedicated imagery -> Model imagery -> Generated collage -> Default placeholder).
- **S7-004 (Generated Release Collages):** Implemented `PureManagedPngEncoder` and `ReleaseArtworkCollageGenerator` producing 16:9 composite banner PNGs.
- **S7-005 & S7-006 (Release Hero Cards & Browser):** Implemented `ReleaseBrowserView.axaml` and `ReleaseBrowserViewModel.cs` with 16:9 cinematic hero card presentation in a responsive `WrapPanel`.
- **S7-007 & S7-008 (Artist Portfolios):** Implemented Artist Portfolio mode in `ArtistBrowserView.axaml` and `ArtistBrowserViewModel.cs` with studio header and release gallery drilldown.
- **S7-009 & S7-010 (Release Detail Experience):** Implemented `ReleaseDetailView.axaml` and `ReleaseDetailViewModel.cs` with 16:9 header banner and child model card grid with density scaling.
- **S7-011 & S7-012 (Release Favorites & Multi-Category Curation):** Added `IsFavorite` to releases and created `FavoritesView.axaml` and `FavoritesViewModel.cs` supporting Models, Releases, and Artists tabs.
- **S7-013 & S7-014 (Library Scope Switcher):** Added Primary Library Scope Switcher (`Models | Releases | Artists | Favorites`) in `MainWindow.axaml` and `ShellViewModel.cs`.
- **S7-015 (Release-Aware Search):** Implemented search prioritization in `DashboardViewModel.cs` ordering results: Exact Release -> Exact Model -> Related.

## 3. Database Migration

- Schema Version upgraded from 8 to 9 (`MigrationRunner.cs`).
- Added table `Releases`, column `Models.ReleaseId`, `Artists.IsFavorite`, and `Collections.IsFavorite`.

## 4. Test Verification

- All 238 unit and integration tests passing in both `Debug` and `Release` configurations.
