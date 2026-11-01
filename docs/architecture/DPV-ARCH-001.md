# DPV-ARCH-001
## Product Architecture Contract

**Status:** Approved by project owner — 2026-11-01
**Scope:** Runtime structure, ownership, dependency direction, and architecture
decisions
**No implementation is authorized by this document.**

---

## 1. Governing Artifacts

- `DEVELOPMENT_STANDARD.md`
- `docs/PROJECT_CONSTITUTION.md`
- `docs/architecture/DPV-SOLUTION-001.md`
- `docs/architecture/DPV-PROJECT-DEFINITIONS-001.md`
- `docs/architecture/DPV-DEPENDENCY-MAP-001.md`
- `docs/architecture/DPV-CONFIGURATION-001.md`
- `docs/architecture/DI-FOUNDATION-001.md`
- `docs/research/REA-001-Solution-Architecture.md` and its review

Where a research recommendation conflicts with an approved contract, the
approved contract controls until a new reviewed decision supersedes it.

## 2. Approved Architecture

Use a single-process, offline-first modular desktop application. Keep explicit
layer boundaries and feature modules inside the approved projects; do not add
microservices or project-per-feature assemblies for the MVP.

```text
DPV.App
  → DPV.UI
  → DPV.Infrastructure
  → DPV.Domain

DPV.UI
  → DPV.Infrastructure
  → DPV.Domain

DPV.Infrastructure
  → DPV.Domain

DPV.Domain
  → none

DPV.Tests → all production projects
```

Allowed project references are defined authoritatively in
`DPV-DEPENDENCY-MAP-001.md`. `DPV.Domain` has no project/package dependencies.
`DPV.Infrastructure` may depend on Domain but not App, UI, or Tests. UI must not
own persistence, direct filesystem scanning, or domain rules. App is the
executable and composition root.

### Project responsibilities

| Project | Responsibility | Must not own |
|---|---|---|
| DPV.App | Executable entry, host, composition root, configuration, logging bootstrap, lifecycle | Catalog business rules, SQL, file scanning, renderer implementation |
| DPV.UI | Avalonia Views, ViewModels, navigation, presentation state | SQL/database access, direct import/scanning, hashing, business invariants |
| DPV.Domain | Catalog concepts and genuine domain invariants | Framework, UI, persistence, filesystem, or DI dependencies |
| DPV.Infrastructure | SQLite/Dapper, filesystem adapters, import, search, viewer/thumbnail adapters | UI composition or application startup |
| DPV.Tests | Unit, integration, and architecture tests | Production runtime responsibilities |

The current application uses DPV.App as a `WinExe` and DPV.UI as a library;
this is verified in the repository after sync. Older documents describing UI
as the executable are stale and should be reconciled during document review.

## 3. Composition and Feature Flow

- Keep one composition root in DPV.App.
- Register App, UI, and Infrastructure modules explicitly through the existing
  orchestrator; do not use assembly scanning for the MVP.
- Use constructor injection and typed factories; do not expose
  `IServiceProvider` to feature code or use service locators.
- Organize feature code inside the existing projects and follow the common
  module layout in `DPV-CONFIGURATION-001.md`.
- For the Library Import Walking Skeleton, DPV.Domain owns the public,
  technology-neutral use-case contract and request/result types;
  DPV.Infrastructure implements that contract and coordinates its import and
  persistence adapters; DPV.UI consumes only the Domain contract; DPV.App
  registers the implementation. Keep domain invariants in Domain and
  presentation mapping in UI.
- This is the MVP compromise within the approved project set, not a general
  rule that Infrastructure owns business workflows. Do not add a
  DPV.Application project without a separate architecture review.
- SQLite is the authoritative source for catalog reads; UI state is not a
  second source of truth.

## 4. Data and External-System Boundaries

- SQLite is a local catalog store, not a container for model file bytes.
- Catalog metadata and derived indexes belong in the database; model files
  remain at user-managed filesystem locations.
- A network-mounted or removable model root does not authorize placing the
  SQLite database on a network filesystem.
- Use Dapper for persistence and an explicit, tested migration/versioning
  mechanism. The migration mechanism itself remains a Commit C decision.
- Keep FTS5/index data derived and recoverable; its detailed lifecycle belongs
  to the Search feature contract.
- Keep file access, hashing, and import behind Infrastructure/application
  boundaries. Never mutate/delete source files as an incidental import action.

## 5. Explicit Decision Register

| ID | Decision | Status / evidence |
|---|---|---|
| ARCH-001 | Runtime is .NET 10 LTS. | Approved by existing architecture/configuration artifacts and current project targets. |
| ARCH-002 | Desktop stack is Avalonia/MVVM with DPV.App as executable composition root. | Approved by DI foundation and current implementation; verify platform behavior separately. |
| ARCH-003 | SQLite is the local authoritative catalog; model bytes remain external. | Approved by Project Constitution and solution contract. |
| ARCH-004 | MVP data access is Dapper; EF Core is not approved. | Explicit in `DPV-SOLUTION-001.md` and `DPV-ARCH-001` prior contract. REA-001's EF Core recommendation was not adopted by `REA-001-REVIEW.md`; no EF Core dependency is authorized. Reconsider only via a reviewed ADR before persistence implementation. |
| ARCH-005 | Keep the existing five-project solution shape (four production projects plus tests). | Approved/current. REA-001's proposal to merge UI into App and add Application was not adopted. Changing project boundaries requires a separate architecture review. |
| ARCH-006 | Search begins with SQLite FTS5. | Approved in existing reviewed architecture; performance must be measured and search remains a separately gated feature. |
| ARCH-007 | Helix Toolkit is not yet a verified runtime dependency. | Earlier architecture documents list a target; REA-001 reports a compatibility proof is required. Do not add a package or claim OS/GPU support until verified. |
| ARCH-008 | Library Import Walking Skeleton ownership uses a Domain contract, Infrastructure implementation/orchestration, UI consumption, and App registration. | Approved by the project owner on 2026-11-01. Infrastructure coordinates adapters but does not own domain invariants. No `DPV.Application` project is approved. |

## 6. Architectural Quality Attributes

- Offline operation for core catalog workflows.
- Integrity under interrupted scans and database failures.
- Bounded concurrency and memory for large folders/catalogs.
- Cancellation and explicit progress for long-running work.
- Paginated/virtualized catalog presentation; do not materialize 250,000
  ViewModels or thumbnails at once.
- Enforce project and package boundaries through automated architecture tests.
- Keep feature workflows testable without launching the complete desktop UI,
  while retaining end-to-end verification for user-visible artifacts.

Specific latency, memory, and throughput limits must be set in Commit A for
each feature using a declared reference machine and dataset.

## 7. Verification Requirements

Architecture tests shall protect:

- Allowed/forbidden project references and cycle freedom.
- Domain project/package purity.
- UI consumption of cross-project feature contracts without dependency on
  Infrastructure implementation or adapter types.
- Single composition root/provider and explicit module registration.
- Registration purity and no service-locator leaks.
- UI/Infrastructure/Domain boundaries.

Integration tests shall use real SQLite for persistence behavior. CI shall run
restore, build, tests, and architecture checks on supported platforms. Release
validation shall include representative large-catalog and import-artifact
checks.

## 8. Change Control

Any change to project boundaries, persistence technology, source-of-truth
semantics, local/offline guarantees, or renderer platform support requires:

```text
REA → Architecture Review → Explicit Approval → Updated Contracts
```

The relevant documents must be committed and pushed before implementation
begins, following DEVELOPMENT_STANDARD.md.

## 9. Approval

Approved by the project owner on 2026-11-01 after review in the current
conversation. This approval records the architecture direction but does not
authorize implementation or waive the REA and Commit A–F gates. Contradictory
legacy descriptions must be reconciled before they are used as authority.
