# DPV-ROADMAP-001

Master roadmap for 3D Print Vault.

---

# Current Phase

```text
Sprint 2 library-import walking skeleton — complete and verified
```

Sprint 1 closure was authorized by GOV-S1-023. Sprint 2 Commits A through F
are implemented on `feature/v2-fallback`. Sprint 3 `DPV-S3-001` adds SQLite
FTS5 search over model names and file paths, with tag filtering and a
debounced dashboard search box. Sprint 3 `DPV-S3-002` adds schema version 4
for artists, collections, and tags, plus a dashboard inspector that loads
file metadata and saves those bindings. Sprint 3 `DPV-S3-003` lists exact
SHA-256 duplicates and lets the user merge metadata, ignore a hash, or delete
one selected file. Sprint 3 `DPV-S3-004` caches an isometric STL preview per content hash and shows it in the library list and inspector. Sprint 4 `DPV-S4-001` adds a library analytics page with model, disk, artist, collection, and tag totals. Sprint 4 `DPV-S4-002` exports the active catalog to CSV or JSON and exposes vacuum, backup, and restore from Settings. Sprint 4 `DPV-S4-003` queues library scans on a bounded channel and runs them on a background worker. Sprint 4 `DPV-S4-004` publishes a self-contained single-file Windows executable from the desktop app project. A version tag runs that publish from GitHub Actions and uploads the Windows bundle to a GitHub release. The xUnit suite completed 122 tests, 0 failures, including
architecture, import, rescan, search, inspector, duplicate, and dashboard
checks. The section below titled "Sprint 1 Foundation — Closure Not Verified"
is the historical audit record, not the current phase.

---

# Sprint 1 Foundation — Closure Not Verified

The historical roadmap entry claimed Sprint 1 was complete and closed on
2026-11-01. This audit preserves that claim in
[SPRINT-1-CLOSURE-AUDIT-001.md](../reviews/SPRINT-1-CLOSURE-AUDIT-001.md), but
does not treat the claim as proof of formal closure.

At audit time, GitHub listed issues #1–#4 closed and #5–#14 open, all in the
`Sprint 1 - Foundation` milestone. The roadmap previously listed only S1-001
through S1-005. The roadmap/milestone scope mismatch and unfulfilled or
unverified acceptance criteria must be reconciled before Sprint 1 is represented
as formally closed.

No issue or milestone has been closed or re-scoped by this audit.

---

# Sprint 2 — Library Import Walking Skeleton (Proposed)

## Feature outcome

Given a user-selected folder containing STL files, the application discovers
eligible files, records the imported catalog data in the local SQLite database,
and displays models by reading them back from SQLite. The user can see progress,
cancel a scan, and receive explicit errors for inaccessible or invalid inputs.
No model geometry viewer is required for this walking skeleton.

**Verification artifact:** on a documented test library, complete an import,
restart/reload the application, and show that the imported model list is
displayed from SQLite with the expected records. Preserve the test input and
verification evidence. Define measurable timing/resource thresholds in Commit
A before implementation, using a reference machine and representative library.

## Governing persistence decision

`DPV-SOLUTION-001.md` explicitly approves SQLite with Dapper and says EF Core is
not approved for the MVP. REA-001 recommended EF Core, but that recommendation
was not adopted in the reviewed decision artifact. For this feature, Dapper
remains the governing choice; any change requires a reviewed architecture
decision before implementation.

## Required sequence

These are proposed planning IDs, not evidence that GitHub issues have been
created. The connected GitHub tools are read-only and no GitHub CLI is
installed, so issue/milestone records still need to be created or updated
through authorized GitHub write access. Assign a phase owner before the
corresponding phase begins.

| Planning ID | Gate / deliverable | Required scope and exit evidence |
|---|---|---|
| DPV-S2-001 | REA — Library scanning and persistence research | Complete — REA-007 and its Domain-contract / Infrastructure-implementation ownership boundary approved by the project owner on 2026-11-01. No implementation. |
| DPV-S2-002 | Commit A — Contracts | Complete — domain import request, result, and error contract. |
| DPV-S2-003 | Commit B — Definitions / resolver | Complete — [Definition-011](../definitions/Definition-011-Scan-Identity.md). |
| DPV-S2-004 | Commit C — Evaluation / configuration | Complete — `ScanningOptions` binds root, max file size, extensions, and batch limit, and invalid values fail host startup. |
| DPV-S2-005 | Commit D — Implementation | Complete — filesystem scanner, SHA-256 identity, Dapper catalog, and real SQLite tests. |
| DPV-S2-006 | Commit E — Integration | Complete — composition root registers the import service and catalog query from `ScanningOptions`; the dashboard selects a folder, imports, cancels, and lists SQLite models. |
| DPV-S2-007 | Commit F — Verification / validation | Complete for the local Release build: `dotnet build DPV.sln -c Release` succeeded with 0 warnings, and the Release xUnit run reported 89 tests, 0 failures. GitHub-hosted Ubuntu/Windows CI still runs on pull request. |

Each phase needs a named assignee/owner in its GitHub record and a reviewable
phase artifact. Follow DEVELOPMENT_STANDARD.md's implementation, verification,
commit, and push gate before starting the next phase. REA and planning phases
must be approved before Commit A.

---

# Deferred Features

Deferred features and capabilities are prioritized under the formal Feature Priority Scoring Rubric (1–5 scale across User Value, Architectural Leverage, Implementation Complexity, Risk, Strategic Alignment, and Foundation Dependency; Max: 30). See [DEFERRED-FEATURES-REVIEW-001](../reviews/DEFERRED-FEATURES-REVIEW-001.md) for full justifications, dependency chains, and category scores.

### Prioritized Deferred Features

1. **Folder Structure Metadata Inference** (Score: 29 | Tier 1 — Build Next | Sprint 5)
   - Parses existing folder organization (Artist / Collection / Release) during import into relational catalog entities.
2. **Dedicated Artist, Collection & Tag Views** (Score: 28 | Tier 1 — Build Next | Sprint 5)
   - Replaces inert sidebar labels with interactive navigation views backed by Domain repository contracts.
3. **Visual Catalog Grid View** (Score: 26 | Tier 1 — Build Next | Sprint 5)
   - Multiple catalog presentation modes (List, Compact List, Grid, Large Thumbnail, Details View) transforming DPV into a visual asset library (Plex/Lightroom for 3D printing).
4. **Organization Recommendation Engine** (Score: 27 | Tier 1 — Build Next | Sprint 5)
   - Non-destructive move, rename, and cleanup recommendations without altering source files (ADR-DPV-ORGANIZATION-001 Phase 3).
5. **Metadata Sidecar Sync (`.dpv.json`)** (Score: 24 | Tier 2 — High Priority | Sprint 6)
   - Portable sidecar metadata files adjacent to STLs for backup, migration, and database independence.
6. **Window State & Preferences Persistence** (Score: 23 | Tier 2 — High Priority | Sprint 5)
   - Restores window geometry, layout split ratios, active view mode, and library paths across application restarts.
7. **Kit / Multi-Part Assembly Virtualization** (Score: 22 | Tier 2 — High Priority | Sprint 6)
   - Virtualized items repeater controls for large multi-part kits with hundreds of associated STLs.
8. **Interactive 3D Model Viewer** (Score: 21 | Tier 2 — High Priority | Sprint 6)
   - Real-time in-app 3D mesh rendering, rotation, pan, zoom, and wireframe/shaded preview.
9. **Archive Metadata & Ingestion (ZIP/RAR/7z)** (Score: 20 | Tier 3 — Useful / Deferrable | Sprint 7)
   - Direct cataloging of archived 3D collections without requiring prior disk extraction.
10. **High-Fidelity Shaded Thumbnail Previews** (Score: 20 | Tier 3 — Useful / Deferrable | Sprint 7)
    - Shaded, lighted 2D preview generation upgrading basic isometric wireframes.
11. **User-Approved Vault Migration & Execution** (Score: 18 | Tier 3 — Useful / Deferrable | Sprint 8)
    - Explicit, user-reviewed physical file reorganization with dry runs and rollback journals (Phase 4).
12. **AI-Assisted Metadata Classifier & Tagger** (Score: 15 | Tier 4 — Low Priority | Sprint 9+)
13. **Geometric / Topological Similarity Duplicate Detection** (Score: 15 | Tier 4 — Low Priority | Sprint 9+)
14. **Print History & Slicing Job Logging** (Score: 15 | Tier 4 — Low Priority | Sprint 9+)
15. **Managed Vault Storage Root Mode** (Score: 14 | Tier 4 — Low Priority | Sprint 10+)
16. **Cloud Synchronization & Multi-User Access** (Score: 8 | Tier 5 — Do Not Pursue Yet | Backlog / Rejected)

### Future Sprint Horizon

```text
Sprint 5: Core Discovery, Visual Grid & Shell Completion
├── Folder Structure Metadata Inference
├── Dedicated Artist, Collection & Tag Views
├── Visual Catalog Grid View
├── Organization Recommendation Engine
└── Window State & Preferences Persistence

Sprint 6: Deep Content Inspection & Portability
├── Metadata Sidecar Sync (.dpv.json)
├── Kit / Multi-Part Assembly Virtualization
└── Interactive 3D Model Viewer

Sprint 7: Advanced Ingestion & Visual Polish
├── Archive Metadata & Ingestion (ZIP/RAR/7z)
└── High-Fidelity Shaded Thumbnail Previews

Sprint 8+: User-Approved Vault Operations
└── User-Approved Vault Migration & Execution
```

Existing deferred research placeholders remain:

- REA-002: Search System
- REA-003: 3D Viewer
- REA-004: Duplicate Detection (exact SHA-256 capability implemented; similarity deferred)
