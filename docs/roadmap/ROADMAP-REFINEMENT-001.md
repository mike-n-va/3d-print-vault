# ROADMAP-REFINEMENT-001 — Catalog-First Roadmap & Priority Refinement

**Document ID:** `ROADMAP-REFINEMENT-001`  
**Status:** Architecture & Roadmap Specification  
**Date:** 2026-11-05  
**Governing Standard:** `DEVELOPMENT_STANDARD.md`  
**Decision Alignment:** [`ADR-DPV-CATALOG-001`](../architecture/decisions/ADR-DPV-CATALOG-001.md), [`ADR-DPV-METADATA-001`](../architecture/decisions/ADR-DPV-METADATA-001.md), [`ADR-DPV-ORGANIZATION-001`](../architecture/decisions/ADR-DPV-ORGANIZATION-001.md), [`ADR-AVALONIA-001`](../architecture/decisions/ADR-AVALONIA-001-Application-Lifecycle.md), [`ARCH-DPV-MODEL-ENTITY-001`](../architecture/ARCH-DPV-MODEL-ENTITY-001.md)  
**Scope:** Realignment of Roadmap Horizons, Sprint Sequence, and Feature Dependencies around the Core Model Entity and Visual Discovery.  
**Constraint:** Design and roadmap refinement only. No production code changes, no database migrations, and no direct feature implementations.

---

## 1. Existing Roadmap Review

The master roadmap ([`DPV-ROADMAP-001`](DPV-ROADMAP-001.md)) and prioritization analysis ([`DEFERRED-FEATURES-REVIEW-001`](../reviews/DEFERRED-FEATURES-REVIEW-001.md)) successfully completed the foundational milestones:
- **Sprint 1:** Composition root, SQLite + Dapper foundation, and host architecture verified.
- **Sprint 2:** Library scan walking skeleton verified with non-destructive in-place indexing.
- **Sprint 3:** SQLite FTS5 search, metadata schema version 4, exact duplicate review, and isometric wireframe thumbnail caching.
- **Sprint 4:** Library analytics, catalog export/maintenance, bounded channel background worker, and Windows self-contained packaging.

### The Missing Architectural Link:
While Sprint 2 through Sprint 4 established the persistence and background scanning mechanics, the implementation remained largely **file-centric** ($1\text{ STL file} = 1\text{ Model}$). Multi-part tabletop kits, terrain assemblies, and pre-supported variants currently fragment into independent, unlinked catalog entries.

Recent comparative research ([`REA-DPV-CATALOG-001`](../research/REA-DPV-CATALOG-001-Visual-Catalog-Research.md)) and architecture specification ([`ARCH-DPV-MODEL-ENTITY-001`](../architecture/ARCH-DPV-MODEL-ENTITY-001.md)) reveal that users evaluate 3D printing collections by **Models**, not loose geometry files. The roadmap must now realign to deliver the **Model Entity** as the primary visual and structural aggregate.

---

## 2. Revised Priority Order

Integrating the Model Entity refines the priority sequence, establishing the logical grouping and visual presentation layer before downstream planning and file movement operations:

| Revised Rank | Feature / Architectural Milestone | Tier | Primary Impact Area | Justification for Ordering |
| :---: | :--- | :---: | :--- | :--- |
| **1** | **Model Entity & Kit Grouping Ingestion** | **Tier 1** | Domain & Scanner Architecture | **Foundational Backbone:** Groups constituent STLs, supported variants, and renders into cohesive Model entities during scan. |
| **2** | **Folder Structure Metadata Inference** | **Tier 1** | Metadata Intelligence | Automatically populates Artist, Collection, and Model attributes from directory hierarchies (`ADR-DPV-METADATA-001` Tier 2). |
| **3** | **Visual Catalog Grid View** | **Tier 1** | Catalog Presentation Experience | Delivers the responsive card grid with preview thumbnails, file count badges, and zoom scaling (`ADR-DPV-CATALOG-001`). |
| **4** | **Dedicated Artist, Collection & Tag Views** | **Tier 1** | Shell Navigation & Browsing | Activates the navigation rail, providing dedicated visual hubs and portfolio pages for creators and collections. |
| **5** | **Window State & Preferences Persistence** | **Tier 2** | Desktop Quality of Life | Remembers active view mode (Grid/List), splitter positions, and window bounds across sessions. |
| **6** | **Kit / Multi-Part Assembly Virtualization** | **Tier 2** | Model Detail Inspection | Provides a high-performance virtualized file tree in the inspector panel for models with 50+ constituent parts. |
| **7** | **Metadata Sidecar Sync (`.dpv.json`)** | **Tier 2** | Portability & Backup | Serializes the Model entity and its constituent file mappings into portable `.dpv.json` sidecar files (`ADR-DPV-METADATA-001` Tier 6). |
| **8** | **Interactive 3D Model Viewer** | **Tier 2** | Visual Inspection | Real-time 3D rotation, inspection, and measurement of model geometry directly in the inspector. |
| **9** | **Organization Recommendation Engine** | **Tier 2** | Organization Planning | Generates non-destructive vault reorganization plans based on model-level metadata (`ADR-DPV-ORGANIZATION-001` Phase 3). |
| **10**| **Archive Metadata & Ingestion (ZIP/RAR/7z)**| **Tier 3** | Advanced Ingestion | Discovers and catalogs model kits packaged directly inside archives without requiring prior extraction. |
| **11**| **High-Fidelity Shaded Thumbnail Previews** | **Tier 3** | Visual Quality | Upgrades wireframe thumbnails to shaded/lighted 2D renders. |
| **12**| **User-Approved Vault Migration Execution** | **Tier 3** | Physical Vault Organization | Atomic, user-reviewed physical relocation of files on disk with rollback journals (`ADR-DPV-ORGANIZATION-001` Phase 4). |

---

## 3. New Feature Dependencies

The **Model Entity** acts as the central pivot upon which all subsequent features depend:

```text
┌────────────────────────────────────────────────────────┐
│     Model Entity & Kit Grouping Ingestion (Core)       │
└──────────────────────────┬─────────────────────────────┘
                           │
         ┌─────────────────┼──────────────────┐
         ▼                 ▼                  ▼
┌─────────────────┐ ┌───────────────┐ ┌───────────────────┐
│ Folder Metadata │ │ Visual Grid   │ │ Dedicated Views   │
│ Inference       │ │ Presentation  │ │ (Artists / Tags)  │
└────────┬────────┘ └───────┬───────┘ └─────────┬─────────┘
         │                  │                   │
         └──────────────────┼───────────────────┘
                            ▼
         ┌──────────────────────────────────────┐
         │ Kit Assembly Virtualization & Viewer │
         └──────────────────┬───────────────────┘
                            ▼
         ┌──────────────────────────────────────┐
         │ Metadata Sidecars (.dpv.json)        │
         └──────────────────┬───────────────────┘
                            ▼
         ┌──────────────────────────────────────┐
         │ Organization Recommendation Planner  │
         └──────────────────┬───────────────────┘
                            ▼
         ┌──────────────────────────────────────┐
         │ User-Approved Vault Migration Engine │
         └──────────────────────────────────────┘
```

---

## 4. Metadata Roadmap Impact

### From File Tags to Model Attributes
- **Before:** Tags and metadata were stored against raw `ModelFiles` records or loosely grouped. Attaching an artist to a multi-part kit required updating dozens of file rows.
- **After:** Metadata attaches strictly to the parent `Model` record. 
- **Folder Harvesting Refinement:** When scanning `D:\3D\TitanForge\Titans\Colossus\`, the harvester:
  1. Identifies `Colossus` as the Model boundary.
  2. Ingests all 6 constituent STLs into `Model.Files`.
  3. Assigns `Artist = "TitanForge"` and `Collection = "Titans"` once at the Model level.
- **Sidecar Synchronization:** Emits one `colossus.dpv.json` file summarizing the model and its relative file members, drastically simplifying backup and portability.

---

## 5. Catalog Roadmap Impact

### From Flat File Tables to Visual Media Cards
- **Grid View Transformation:** Each visual card in the grid represents a Model. Instead of displaying 10 cards for 10 pieces of a dragon, the grid displays **1 cohesive dragon card** featuring:
  - Hero thumbnail preview
  - Artist and Collection attribution
  - Part count badge (`8 Parts • 2 Supported`)
  - Print status indicator (`✔ Printed`)
- **Inspector Panel Evolution:** The inspector becomes a dual-purpose control:
  - Upper section manages Model-level metadata and hero preview.
  - Lower section hosts the virtualized constituent file tree, allowing users to select individual STLs for slicing or part-level inspection.

---

## 6. Organization Roadmap Impact

### Moving Models as Atomic Units
- **Atomic Operations:** In Phase 4 Vault Migration ([`ADR-DPV-ORGANIZATION-001`](../architecture/decisions/ADR-DPV-ORGANIZATION-001.md)), DPV must never move a single file while leaving its companion files behind.
- **Model-Level Planning:** The Organization Recommendation Engine plans directory shifts at the Model level:
  $$\text{Model Path} = \text{VaultRoot} / \{\text{Artist}\} / \{\text{Collection}\} / \{\text{ModelName}\} /$$
- When approved, all constituent geometry, supported variants, slicer plates, and images move together as an atomic bundle, preventing broken assemblies.

---

## 7. Recommended Sprint Sequence

### Sprint 5: Model Entity, Visual Grid & Shell Activation
- **DPV-S5-001:** Domain Model Entity contracts & leaf-folder kit grouping scanner.
- **DPV-S5-002:** Folder structure metadata harvester (Artist / Collection / Release inference).
- **DPV-S5-003:** Visual Catalog Grid View (Multi-mode presentation: Grid, Compact, Details).
- **DPV-S5-004:** Dedicated Artist, Collection, and Tag navigation views.
- **DPV-S5-005:** Window state, split layout, and view mode persistence.

### Sprint 6: Deep Model Inspection & Metadata Portability
- **DPV-S6-001:** Kit / Multi-Part assembly file tree virtualization in Inspector.
- **DPV-S6-002:** Metadata sidecar synchronization (`.dpv.json`).
- **DPV-S6-003:** Interactive 3D model viewer integration (in-app mesh rotation).
- **DPV-S6-004:** Batch metadata editing for multi-selected models.

### Sprint 7: Advanced Ingestion & Visual Polish
- **DPV-S7-001:** Streaming archive ingestion (ZIP/RAR/7z container inspection).
- **DPV-S7-002:** High-fidelity shaded and lighted thumbnail rendering worker.
- **DPV-S7-003:** Smart collections / saved query filters in sidebar.

### Sprint 8+: User-Approved Vault Operations
- **DPV-S8-001:** Organization Recommendation Engine (Phase 3 non-destructive planner).
- **DPV-S8-002:** Visual dry run, diff preview, and conflict resolution interface.
- **DPV-S8-003:** User-Approved Vault Migration execution engine with rollback journal.

---

## 8. Strategic Rationale

1. **Immediate User Value:** Users immediately gain an uncluttered, visual, and joyful catalog experience in Sprint 5, without waiting for high-risk file moving capabilities.
2. **Data Model Stability:** Introducing the Model entity early prevents building complex UI views on top of a fragile $1\text{ file} = 1\text{ item}$ assumption.
3. **Strict Adherence to Safety Standards:** Keeps physical file manipulation deferred until metadata and planning layers are fully proven and hardened.

---

# End Roadmap Refinement
