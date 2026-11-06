# DEFERRED-FEATURES-REVIEW-001
## Deferred Feature Priority Scoring & Roadmap Evaluation

**Status:** Approved  
**Date:** 2026-11-05  
**Review Type:** Strategic Product & Architecture Prioritization  
**Governing Authorities:** [`ADR-DPV-ORGANIZATION-001`](../architecture/decisions/ADR-DPV-ORGANIZATION-001.md), [`ADR-DPV-METADATA-001`](../architecture/decisions/ADR-DPV-METADATA-001.md), [`DPV-ROADMAP-001`](../roadmap/DPV-ROADMAP-001.md)

---

## 1. Feature Priority Scoring Rubric

All deferred features are evaluated on a 1–5 scale across six dimensions (Maximum Score: 30):

| Category | Description | Scale (1–5) |
|---|---|---|
| **Category 1: User Value** | Direct benefit to typical DPV user | 1 = Minimal value, 3 = Useful, 5 = High-value capability |
| **Category 2: Architectural Leverage** | Future capability unlocked | 1 = Standalone, 3 = Enables several, 5 = Foundational building block |
| **Category 3: Implementation Complexity** | Difficulty to implement | 1 = Very Complex, 3 = Moderate, 5 = Simple (Higher = lower effort) |
| **Category 4: Risk** | Technical / architectural risk | 1 = High risk, 3 = Moderate risk, 5 = Very low risk (Higher = safer) |
| **Category 5: Strategic Alignment** | Alignment with vision & ADRs | 1 = Weak alignment, 3 = Moderate, 5 = Strong alignment |
| **Category 6: Foundation Dependency** | Prerequisite for other features | 1 = No downstream impact, 3 = Supports several, 5 = Critical prerequisite |

### Priority Tiers:
- **26–30 (Tier 1):** Build Next
- **21–25 (Tier 2):** High Priority
- **16–20 (Tier 3):** Useful but Deferrable
- **11–15 (Tier 4):** Low Priority
- **6–10 (Tier 5):** Do Not Pursue Yet

---

## 2. Master Ranked Feature Table

| Rank | Feature Name | Status | Category | UV | AL | IC | R | SA | FD | Total | Priority Tier | Recommended Sprint |
| :---: | :--- | :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :--- | :---: |
| **1** | **Folder Structure Metadata Inference** | Deferred | Metadata Intelligence | 5 | 5 | 4 | 5 | 5 | 5 | **29** | **Tier 1 — Build Next** | Sprint 5 |
| **2** | **Dedicated Artist, Collection & Tag Views** | Deferred | Library Management | 5 | 5 | 4 | 5 | 5 | 4 | **28** | **Tier 1 — Build Next** | Sprint 5 |
| **3** | **Visual Catalog Grid View** | Deferred | Catalog Experience | 5 | 4 | 4 | 5 | 5 | 3 | **26** | **Tier 1 — Build Next** | Sprint 5 |
| **4** | **Organization Recommendation Engine** | Deferred | Organization Planning | 4 | 5 | 3 | 5 | 5 | 5 | **27** | **Tier 1 — Build Next** | Sprint 5 |
| **5** | **Metadata Sidecar Sync (`.dpv.json`)** | Deferred | Metadata Portability | 4 | 4 | 4 | 4 | 5 | 3 | **24** | **Tier 2 — High Priority** | Sprint 6 |
| **6** | **Window State & Preferences Persistence** | Deferred | Shell Infrastructure | 4 | 3 | 5 | 5 | 4 | 2 | **23** | **Tier 2 — High Priority** | Sprint 5 |
| **7** | **Kit / Multi-Part Assembly Virtualization** | Deferred | Model Inspection | 4 | 3 | 4 | 5 | 4 | 2 | **22** | **Tier 2 — High Priority** | Sprint 6 |
| **8** | **Interactive 3D Model Viewer** | Deferred | Visualization | 5 | 4 | 2 | 3 | 4 | 3 | **21** | **Tier 2 — High Priority** | Sprint 6 |
| **9** | **Archive Metadata & Ingestion (ZIP/RAR/7z)**| Deferred | Library Ingestion | 4 | 4 | 2 | 3 | 4 | 3 | **20** | **Tier 3 — Useful / Deferrable** | Sprint 7 |
| **10**| **High-Fidelity Shaded Thumbnail Previews** | Deferred | Visualization | 4 | 3 | 3 | 4 | 4 | 2 | **20** | **Tier 3 — Useful / Deferrable** | Sprint 7 |
| **11**| **User-Approved Vault Migration Execution** | Deferred | Vault Organization | 5 | 4 | 1 | 1 | 5 | 2 | **18** | **Tier 3 — Useful / Deferrable** | Sprint 8 |
| **12**| **AI-Assisted Metadata Classifier & Tagger** | Deferred | Intelligence | 3 | 3 | 2 | 3 | 3 | 1 | **15** | **Tier 4 — Low Priority** | Sprint 9+ |
| **13**| **Geometric / Similarity Duplicate Detection**| Deferred | Duplicate Detection | 3 | 3 | 1 | 3 | 3 | 2 | **15** | **Tier 4 — Low Priority** | Sprint 9+ |
| **14**| **Print History & Slicing Job Logging** | Deferred | Fabrication | 3 | 2 | 3 | 4 | 2 | 1 | **15** | **Tier 4 — Low Priority** | Sprint 9+ |
| **15**| **Managed Vault Storage Root Mode** | Deferred | Vault Organization | 3 | 3 | 2 | 2 | 3 | 1 | **14** | **Tier 4 — Low Priority** | Sprint 10+ |
| **16**| **Cloud Synchronization & Multi-User Access**| Deferred | Sync / Network | 2 | 2 | 1 | 1 | 1 | 1 | **8** | **Tier 5 — Do Not Pursue** | Backlog / Rejected |

*Legend: UV = User Value, AL = Architectural Leverage, IC = Implementation Complexity, R = Risk, SA = Strategic Alignment, FD = Foundation Dependency.*

---

## 3. Detailed Feature Profiles

### 1. Folder Structure Metadata Inference (Tier 2 Metadata)
- **Status:** Deferred (`ADR-DPV-METADATA-001` Tier 2)
- **Dependency Chain:** `ModelImportService` $\rightarrow$ `PathNormalizer` $\rightarrow$ `IModelDetailRepository` $\rightarrow$ SQLite (`Models`, `Artists`, `Collections`)
- **Individual Category Scores:**
  - *User Value:* **5**
  - *Architectural Leverage:* **5**
  - *Implementation Complexity:* **4**
  - *Risk:* **5**
  - *Strategic Alignment:* **5**
  - *Foundation Dependency:* **5**
- **Total Priority Score:** **29 / 30**
- **Priority Tier:** **Tier 1 — Build Next**
- **Justification:** Highest-leverage feature on the roadmap. Automatically extracts Artist, Collection, and Release hierarchy from existing folder paths during import without manual user entry.
- **Recommended Sprint:** **Sprint 5**

---

### 2. Dedicated Artist, Collection & Tag Views & Repositories
- **Status:** Deferred (Domain interfaces defined in [`DPV-REPOSITORY-CONTRACT-OWNERSHIP-001`](../architecture/DPV-REPOSITORY-CONTRACT-OWNERSHIP-001.md); implementation pending)
- **Dependency Chain:** `IArtistRepository`, `ICollectionRepository`, `ITagRepository` $\rightarrow$ Dapper/SQLite $\rightarrow$ Sidebar Navigation $\rightarrow$ `ShellViewModel`
- **Individual Category Scores:**
  - *User Value:* **5**
  - *Architectural Leverage:* **5**
  - *Implementation Complexity:* **4**
  - *Risk:* **5**
  - *Strategic Alignment:* **5**
  - *Foundation Dependency:* **4**
- **Total Priority Score:** **28 / 30**
- **Priority Tier:** **Tier 1 — Build Next**
- **Justification:** Replaces inert sidebar labels with interactive browsing views, completing the multi-entity catalog architecture.
- **Recommended Sprint:** **Sprint 5**

---

### 3. Visual Catalog Grid View
- **Status:** Deferred (`ADR-DPV-METADATA-001` Library Discovery)
- **Category:** Catalog Experience / Library Discovery
- **Description:** Provide multiple catalog presentation modes:
  - List View
  - Compact List View
  - Grid View (responsive card layout with previews)
  - Large Thumbnail View
  - Details View (tabular grid: Preview | Model | Artist | Collection | Files | Size)
  allowing users to browse large STL collections visually.
- **Vision:** Transforms DPV from a file browser into a searchable visual asset library similar to Plex, Lightroom, and Calibre for 3D printing content.
- **Dependency Chain:**
  ```text
  Thumbnail Service
      ↓
  Metadata Catalog
      ↓
  View Mode Service
      ↓
  Grid/List Presentation Layer
  ```
- **Individual Category Scores:**
  - *User Value:* **5** — Visual browsing is paramount for 3D models where file names are often obscure or non-descriptive.
  - *Architectural Leverage:* **4** — Establishes presentation mode switching, responsive wrap layouts, and reusable asset card components.
  - *Implementation Complexity:* **4** — Avalonia ItemsControl/ListBox templates with `WrapPanel` or `UniformGrid` and view-mode data templates.
  - *Risk:* **5** — Read-only presentation layer; zero risk to data or files.
  - *Strategic Alignment:* **5** — Directly supports the core product vision: Searchable, Sortable, Visual Catalog.
  - *Foundation Dependency:* **3** — Leverages existing `ThumbnailService` and catalog queries.
- **Total Priority Score:** **26 / 30**
- **Priority Tier:** **Tier 1 — Build Next**
- **Justification:** Directly supports the long-term vision of a searchable, sortable, visual asset catalog and dramatically improves discoverability for users managing thousands of STL assets. Unlike vault management features, it immediately elevates the day-to-day experience without modifying files.
- **Recommended Sprint:** **Sprint 5**

---

### 4. Organization Recommendation Engine (Phase 3 Planning)
- **Status:** Deferred (`ADR-DPV-ORGANIZATION-001` Phase 3)
- **Dependency Chain:** `IModelCatalogQuery` $\rightarrow$ `IModelDetailRepository` $\rightarrow$ `OrganizationPlanner` $\rightarrow$ `PlanReport`
- **Individual Category Scores:**
  - *User Value:* **4**
  - *Architectural Leverage:* **5**
  - *Implementation Complexity:* **3**
  - *Risk:* **5**
  - *Strategic Alignment:* **5**
  - *Foundation Dependency:* **5**
- **Total Priority Score:** **27 / 30**
- **Priority Tier:** **Tier 1 — Build Next**
- **Justification:** Analyzes existing library layout and generates non-destructive reorganization recommendations, move plans, and duplicate cleanup reports without altering source files.
- **Recommended Sprint:** **Sprint 5**

---

### 5. Metadata Sidecar Sync (`.dpv.json`)
- **Status:** Deferred (`ADR-DPV-METADATA-001` Tier 6)
- **Dependency Chain:** `IModelDetailRepository` $\rightarrow$ `JsonSerializer` $\rightarrow$ Local Filesystem Storage
- **Individual Category Scores:**
  - *User Value:* **4**
  - *Architectural Leverage:* **4**
  - *Implementation Complexity:* **4**
  - *Risk:* **4**
  - *Strategic Alignment:* **5**
  - *Foundation Dependency:* **3**
- **Total Priority Score:** **24 / 30**
- **Priority Tier:** **Tier 2 — High Priority**
- **Justification:** Generates portable `.dpv.json` sidecar files adjacent to STL models, making metadata portable between installs and independent of database corruption.
- **Recommended Sprint:** **Sprint 6**

---

### 6. Window State & Preferences Persistence
- **Status:** Deferred (`UI-REVIEW-001` UX Debt)
- **Dependency Chain:** `MainWindow` $\rightarrow$ `IClassicDesktopStyleApplicationLifetime` $\rightarrow$ Local Configuration Provider
- **Individual Category Scores:**
  - *User Value:* **4**
  - *Architectural Leverage:* **3**
  - *Implementation Complexity:* **5**
  - *Risk:* **5**
  - *Strategic Alignment:* **4**
  - *Foundation Dependency:* **2**
- **Total Priority Score:** **23 / 30**
- **Priority Tier:** **Tier 2 — High Priority**
- **Justification:** Remembers window geometry, position, active view mode, and last library path across application restarts.
- **Recommended Sprint:** **Sprint 5**

---

### 7. Kit / Multi-Part Assembly Virtualization
- **Status:** Deferred (`UI-REVIEW-001` UX Debt)
- **Dependency Chain:** `DetailedModel.Files` $\rightarrow$ `ItemsRepeater` / Virtualizing TreeView
- **Individual Category Scores:**
  - *User Value:* **4**
  - *Architectural Leverage:* **3**
  - *Implementation Complexity:* **4**
  - *Risk:* **5**
  - *Strategic Alignment:* **4**
  - *Foundation Dependency:* **2**
- **Total Priority Score:** **22 / 30**
- **Priority Tier:** **Tier 2 — High Priority**
- **Justification:** Replaces unvirtualized items list with virtualized controls to prevent UI stutter when inspecting kits containing 100+ STL parts.
- **Recommended Sprint:** **Sprint 6**

---

### 8. Interactive 3D Model Viewer
- **Status:** Deferred (`REA-003`, `DPV-ROADMAP-001`)
- **Dependency Chain:** STL Parser $\rightarrow$ 3D Rendering Pipeline (Helix Toolkit / Silk.NET) $\rightarrow$ Avalonia Viewport Host
- **Individual Category Scores:**
  - *User Value:* **5**
  - *Architectural Leverage:* **4**
  - *Implementation Complexity:* **2**
  - *Risk:* **3**
  - *Strategic Alignment:* **4**
  - *Foundation Dependency:* **3**
- **Total Priority Score:** **21 / 30**
- **Priority Tier:** **Tier 2 — High Priority**
- **Justification:** Real-time 3D rotation, pan, zoom, and wireframe/shaded preview directly in the application.
- **Recommended Sprint:** **Sprint 6**

---

### 9. Archive Metadata & Ingestion (ZIP, RAR, 7z)
- **Status:** Deferred (`ADR-DPV-METADATA-001` Tier 3)
- **Dependency Chain:** Archive Reader (`SharpCompress`) $\rightarrow$ `ModelImportService` $\rightarrow$ Temporary Stream Cache
- **Individual Category Scores:**
  - *User Value:* **4**
  - *Architectural Leverage:* **4**
  - *Implementation Complexity:* **2**
  - *Risk:* **3**
  - *Strategic Alignment:* **4**
  - *Foundation Dependency:* **3**
- **Total Priority Score:** **20 / 30**
- **Priority Tier:** **Tier 3 — Useful but Deferrable**
- **Justification:** Indexes packaged 3D models directly inside compressed archives without forcing users to decompress hundreds of gigabytes.
- **Recommended Sprint:** **Sprint 7**

---

### 10. High-Fidelity Shaded Thumbnail Previews
- **Status:** Deferred (`ADR-DPV-METADATA-001` Tier 7, `DPV-ROADMAP-001`)
- **Dependency Chain:** Software/GPU Headless Renderer $\rightarrow$ `ThumbnailService` $\rightarrow$ `ThumbnailCache`
- **Individual Category Scores:**
  - *User Value:* **4**
  - *Architectural Leverage:* **3**
  - *Implementation Complexity:* **3**
  - *Risk:* **4**
  - *Strategic Alignment:* **4**
  - *Foundation Dependency:* **2**
- **Total Priority Score:** **20 / 30**
- **Priority Tier:** **Tier 3 — Useful but Deferrable**
- **Justification:** Upgrades basic isometric wireframe rendering to shaded, lighted 2D renders for superior thumbnail clarity.
- **Recommended Sprint:** **Sprint 7**

---

### 11. User-Approved Vault Migration & Execution (Phase 4)
- **Status:** Deferred (`ADR-DPV-ORGANIZATION-001` Phase 4)
- **Dependency Chain:** Organization Planner (Phase 3) $\rightarrow$ File System Mover $\rightarrow$ Rollback Journal $\rightarrow$ Audit Log
- **Individual Category Scores:**
  - *User Value:* **5**
  - *Architectural Leverage:* **4**
  - *Implementation Complexity:* **1**
  - *Risk:* **1**
  - *Strategic Alignment:* **5**
  - *Foundation Dependency:* **2**
- **Total Priority Score:** **18 / 30**
- **Priority Tier:** **Tier 3 — Useful but Deferrable**
- **Justification:** Executes physical file moves, folder restructuring, and duplicate purging only with explicit user preview and review.
- **Recommended Sprint:** **Sprint 8**

---

### 12. AI-Assisted Metadata Classifier & Tagger (Tier 8)
- **Status:** Deferred (`ADR-DPV-METADATA-001` Tier 8)
- **Dependency Chain:** Local ONNX / Semantic NLP Engine $\rightarrow$ Metadata Suggestion UI $\rightarrow$ `IModelDetailRepository`
- **Individual Category Scores:**
  - *User Value:* **3**
  - *Architectural Leverage:* **3**
  - *Implementation Complexity:* **2**
  - *Risk:* **3**
  - *Strategic Alignment:* **3**
  - *Foundation Dependency:* **1**
- **Total Priority Score:** **15 / 30**
- **Priority Tier:** **Tier 4 — Low Priority**
- **Justification:** Heuristic and LLM-assisted tagging suggestions; long-term optional convenience.
- **Recommended Sprint:** **Sprint 9+**

---

### 13. Geometric / Similarity Duplicate Detection
- **Status:** Deferred (`REA-004`, `DPV-ROADMAP-001`)
- **Dependency Chain:** Mesh Topology Analyzer $\rightarrow$ Similarity Index Vector DB $\rightarrow$ Duplicate Review View
- **Individual Category Scores:**
  - *User Value:* **3**
  - *Architectural Leverage:* **3**
  - *Implementation Complexity:* **1**
  - *Risk:* **3**
  - *Strategic Alignment:* **3**
  - *Foundation Dependency:* **2**
- **Total Priority Score:** **15 / 30**
- **Priority Tier:** **Tier 4 — Low Priority**
- **Justification:** Detects scaled, rotated, or modified duplicates where SHA-256 hashes differ.
- **Recommended Sprint:** **Sprint 9+**

---

### 14. Print History & Slicing Job Logging
- **Status:** Deferred (`DPV-ROADMAP-001`)
- **Dependency Chain:** Slicer Integration $\rightarrow$ `PrintJob` SQLite Tables $\rightarrow$ UI History View
- **Individual Category Scores:**
  - *User Value:* **3**
  - *Architectural Leverage:* **2**
  - *Implementation Complexity:* **3**
  - *Risk:* **4**
  - *Strategic Alignment:* **2**
  - *Foundation Dependency:* **1**
- **Total Priority Score:** **15 / 30**
- **Priority Tier:** **Tier 4 — Low Priority**
- **Justification:** Tracks filament usage, printer assignments, and print outcomes.
- **Recommended Sprint:** **Sprint 9+**

---

### 15. Managed Vault Storage Root Mode
- **Status:** Deferred (`ADR-DPV-ORGANIZATION-001`)
- **Dependency Chain:** Phase 4 Migration Engine $\rightarrow$ Vault Directory Enforcer $\rightarrow$ Quarantine Provider
- **Individual Category Scores:**
  - *User Value:* **3**
  - *Architectural Leverage:* **3**
  - *Implementation Complexity:* **2**
  - *Risk:* **2**
  - *Strategic Alignment:* **3**
  - *Foundation Dependency:* **1**
- **Total Priority Score:** **14 / 30**
- **Priority Tier:** **Tier 4 — Low Priority**
- **Justification:** Optional mode where DPV assumes full custody over a dedicated storage root.
- **Recommended Sprint:** **Sprint 10+**

---

### 16. Cloud Synchronization & Multi-User Access
- **Status:** Deferred / Architecturally Prohibited (`REA-001`, `REA-007`)
- **Dependency Chain:** Client-Server Backend $\rightarrow$ Distributed Replication $\rightarrow$ Multi-User Auth
- **Individual Category Scores:**
  - *User Value:* **2**
  - *Architectural Leverage:* **2**
  - *Implementation Complexity:* **1**
  - *Risk:* **1**
  - *Strategic Alignment:* **1**
  - *Foundation Dependency:* **1**
- **Total Priority Score:** **8 / 30**
- **Priority Tier:** **Tier 5 — Do Not Pursue Yet**
- **Justification:** Contradicts single-user offline desktop catalog architecture.
- **Recommended Sprint:** **Backlog / Rejected**

---

## 4. Strategic Delivery Roadmap

```text
Sprint 5: Core Discovery, Visual Grid & Shell Completion
├── Folder Structure Metadata Inference (Score: 29)
├── Dedicated Artist, Collection & Tag Views (Score: 28)
├── Visual Catalog Grid View (Score: 26)
├── Organization Recommendation Engine (Score: 27)
└── Window State & Preferences Persistence (Score: 23)

Sprint 6: Deep Content Inspection & Portability
├── Metadata Sidecar Sync (.dpv.json) (Score: 24)
├── Kit / Multi-Part Assembly Virtualization (Score: 22)
└── Interactive 3D Model Viewer (Score: 21)

Sprint 7: Advanced Ingestion & Visual Polish
├── Archive Metadata & Ingestion (ZIP/RAR/7z) (Score: 20)
└── High-Fidelity Shaded Thumbnail Previews (Score: 20)

Sprint 8+: User-Approved Vault Operations
└── User-Approved Vault Migration & Execution (Score: 18)
```
