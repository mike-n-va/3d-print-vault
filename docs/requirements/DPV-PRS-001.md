# DPV-PRS-001
## Product Requirements Specification

**Status:** Approved by project owner — 2026-11-01
**Scope:** Product-level requirements and measurable acceptance gates
**Authority:** DEVELOPMENT_STANDARD.md, Project Constitution, approved
architecture contracts, and reviewed feature REA artifacts

No implementation is authorized by this document.

---

## 1. Product Summary

3D Print Vault is an offline-first desktop application for cataloging,
organizing, searching, and visualizing large collections of 3D-printable model
files. SQLite is the authoritative catalog; model files remain in managed
filesystem locations.

The primary catalog hierarchy is:

```text
Artist → Collection → Model
```

The project is intended to support libraries of at least 250,000 cataloged
models and libraries whose model files occupy multiple terabytes. These are
scale targets, not claims that current code has been benchmarked at those sizes.

## 2. Scope and Product Principles

### In scope

- Local desktop catalog and organization.
- Offline operation without a required remote service.
- Import/discovery of STL files from user-selected folders.
- Persistent catalog records and model-list presentation.
- Exact-content duplicate detection using SHA-256 is an MVP capability, as
  approved by `REA-001-REVIEW.md`; it is a separate feature and is not part of
  the Library Import Walking Skeleton.
- Search, thumbnail generation, 3D viewing, print history, geometry similarity,
  and destructive duplicate resolution require separate research and approval.

### Principles

- The database is the source of truth for catalog metadata.
- The filesystem remains the source of model-file bytes.
- Preserve the Artist → Collection → Model hierarchy.
- Avoid silent loss, destructive file actions, and success-shaped error
  handling.
- Keep performance and storage use observable and testable at target scale.

### Out of scope for the first walking skeleton

- Editing, moving, deleting, or rewriting source model files.
- Cloud sync, multi-user access, or a database shared over a network.
- Geometry similarity search, slicing, repair, and print execution.
- A production-ready 3D renderer or thumbnail pipeline.

## 3. Functional Requirements

| ID | Requirement | Acceptance evidence |
|---|---|---|
| PR-F-001 | The application shall operate without a network connection for its core catalog workflows. | Disconnect network services and demonstrate folder import plus catalog browsing without remote dependencies. |
| PR-F-002 | Catalog organization shall support Artist → Collection → Model. | Domain and persistence tests demonstrate the approved relationships and constraints. |
| PR-F-003 | A user shall be able to select a source folder for STL discovery. | UI integration test or witnessed UI run selects a folder and reports its completion/failure. |
| PR-F-004 | Import shall discover eligible STL files and persist catalog data to SQLite. | Integration test against real files and a real SQLite database. |
| PR-F-005 | The model list shall be read from persisted SQLite data, not only from transient scan results. | Import, close/reopen or reload, then verify expected records are displayed from the database. |
| PR-F-006 | Long-running import shall expose progress and support cancellation. | Deterministic test verifies cancellation is honored at the next safe filesystem/batch boundary, no further batch is committed after cancellation is observed, and the outcome does not report a partial scan as complete. Commit A defines an acceptable response bound for supported storage. |
| PR-F-007 | Import shall report inaccessible roots, enumeration failures, and invalid inputs explicitly. | Tests verify useful structured errors/logs and no success-shaped fallback. |
| PR-F-008 | Repeated scans shall have a defined, tested idempotency/update behavior before implementation. | Commit B contract and tests cover unchanged, changed, moved, missing, and duplicate candidates. |
| PR-F-009 | Features outside the Library Import Walking Skeleton shall not be implied by its acceptance artifact. | Scope review confirms exact duplicate detection, search, viewer, thumbnail, and print-history workflows remain separately gated. |
| PR-F-010 | The MVP shall identify exact duplicate model files using SHA-256 without automatically deleting, moving, or rewriting source files. | Separate REA/Commit A contract and tests verify identical-content detection and preserve distinct source paths; this is not accepted by the walking-skeleton artifact. |

Artist/Collection assignment for imported files, treatment of non-STL files,
symlinks/reparse points, and the behavior for files that change during a scan
are product decisions to settle in Commit A/B. Do not invent silent defaults.

## 4. Nonfunctional Requirements

| ID | Requirement | Verification |
|---|---|---|
| PR-NF-001 | Support a catalog target of 250,000+ models. | Benchmark a representative generated/anonymized catalog and realistic list/search queries. Record hardware, dataset, latency, memory, and database size. |
| PR-NF-002 | Support model libraries occupying multiple terabytes without storing model bytes in SQLite. | Inspect data design and verify model files are streamed/read from managed locations. |
| PR-NF-003 | Keep the local UI responsive during discovery, import, and large catalog reads. | Exercise representative load while measuring UI responsiveness and cancellation. |
| PR-NF-004 | Bound concurrency, memory, transaction size, and UI result materialization. | Record chosen limits and verify them in tests/benchmarks before release. |
| PR-NF-005 | Preserve catalog integrity across process interruption and database errors. | Integration tests cover transaction rollback, restart/reopen, and migration from supported prior schemas. |
| PR-NF-006 | Surface file/database failures and avoid silent data loss. | Fault-injection tests and structured logs demonstrate failure visibility. |
| PR-NF-007 | Remain maintainable under AI-assisted development. | Enforce architecture and behavior via tests, explicit contracts, reviewed artifacts, and human review. |

Numeric startup, scan-throughput, query-latency, and memory thresholds are not
approved yet. Commit A must specify thresholds and a reference machine before
implementation; do not claim PR-NF-001/003/004 are met without that evidence.

## 5. Technology Baseline

- Runtime: .NET 10 LTS.
- Desktop UI: Avalonia with MVVM.
- Catalog database: local SQLite.
- Data access: Dapper, as approved by `DPV-SOLUTION-001.md`.
- Architecture: DPV.App, DPV.UI, DPV.Domain, DPV.Infrastructure, DPV.Tests.
- Tests: xUnit v3, NSubstitute, and ArchUnitNET per the testing foundation.
- Search and viewer choices remain subject to their approved feature contracts;
  no renderer/package compatibility is asserted here.

## 6. Traceability and Change Control

Each feature requirement must trace to:

1. A reviewed REA artifact.
2. Commit A acceptance criteria.
3. Commit F tests and artifact evidence.

Changes to offline behavior, hierarchy, authoritative data, or supported scale
require review of this document and relevant architecture/data/UI contracts.

## 7. Approval

Approved by the project owner on 2026-11-01 after review in the current
conversation. This approval establishes product requirements only; it does not
authorize implementation or waive the REA and Commit A–F gates.
