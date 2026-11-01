# REA-007 — Library Scanning and Persistence

**Status:** Research complete and approved for planning — 2026-11-01
**Review decision:** **APPROVE WITH CHANGES**
**Research date:** 2026-11-01
**Feature:** Library Import Walking Skeleton
**No implementation is authorized by this artifact.**

---

## 1. Executive Summary

The proposed walking skeleton is feasible within the current offline desktop
architecture: enumerate a user-selected folder, identify STL candidates,
persist catalog metadata to local SQLite, and display the persisted records.
The model files themselves must remain on disk; SQLite is a catalog, not a
multi-terabyte blob store.

The current project contracts explicitly approve SQLite + Dapper and reject
EF Core for the MVP. REA-001 recommended EF Core, but its review did not adopt
that recommendation. This report therefore treats Dapper as binding unless a
separate reviewed architecture decision changes it. Do not introduce EF Core
or a second ORM during the walking skeleton.

The highest risks are not raw catalog row count, but identity ambiguity,
non-repeatable imports, unbounded memory/concurrency, long write transactions,
inaccessible or changing files, UI-thread blocking, and incomplete recovery
behavior. In particular, .NET enumeration defaults can hide inaccessible,
hidden, or system entries; the feature must choose and report these policies
explicitly. Before implementation, define file/model identity, rescan
outcomes, path policy, migration/versioning, transaction/batch limits, and
measurable performance thresholds.

**Recommendation:** proceed to Commit A planning only after the five
foundational documents and this REA are reviewed. Keep one feature spanning
the current projects; no new project, ORM, search engine, renderer, or
destructive file operation is justified by this slice.

## 2. Problem and Scope

Users need to discover STL files in a selected folder and maintain a durable
catalog that remains available after application restart. The goal is not to
copy model bytes into a database; it is to index and present metadata while
respecting offline operation and large libraries.

### In scope

- Select a root folder.
- Enumerate candidate STL paths without loading the complete path set or file
  contents into memory.
- Record catalog metadata in SQLite using the approved Dapper stack.
- Read catalog rows back from SQLite and show them in the Avalonia UI.
- Report progress, cancellation, partial failures, and completion.
- Support repeat scans under explicit, tested identity/update rules.

### Out of scope

- Mesh parsing, repair, rendering, slicing, or thumbnail generation.
- Deleting, moving, renaming, or rewriting user source files.
- Cloud synchronization or shared/network-hosted SQLite.
- Full-text search, duplicate resolution, geometry similarity, or analytics.

## 3. Existing Solutions and Relevant Patterns

### Filesystem traversal

The .NET filesystem API exposes enumerable directory/file results rather than
requiring every path to be materialized before processing. Microsoft documents
enumerable collections as the better fit for large collections and warns that
recursive enumeration may become incomplete on access errors. `EnumerationOptions`
also makes two important defaults explicit: `IgnoreInaccessible` is `true`,
and `AttributesToSkip` defaults to `Hidden | System`. A scanner that relies on
these defaults can silently omit content. Set both policies deliberately and
surface skipped paths in the scan result.

Prefer explicit directory-by-directory traversal when DPV needs to attribute
errors to a particular subtree and continue with accessible siblings. Handle
exceptions while consuming each enumerable, not only when creating it.
Preserve the distinction between complete, partial, cancelled, and failed
scans. Decide whether directory reparse points/symlinks are followed; the safe
MVP default is not to follow them until cycle, duplicate-location, and
out-of-root behavior is specified. Decide whether hidden STL files are in
scope rather than inheriting the API default.

Enumeration is synchronous and does not provide a cancellation token in the
standard `Directory.EnumerateFiles` API. Cancellation can be checked between
entries/directories, but cannot guarantee prompt interruption while an
underlying filesystem operation is blocked. Commit A must specify the
user-visible cancellation guarantee, including removable or slow roots.

### Existing implementations

| Existing solution/pattern | Observed evidence | Relevance to DPV |
|---|---|---|
| .NET `Directory.EnumerateFiles` / `EnumerationOptions` | The .NET 10 API returns an enumerable and exposes recursion, access-error handling, and attribute filtering. The documentation recommends enumerating directories separately when access errors must be handled. | Use streaming traversal, but explicitly set filtering/error policy and test the behavior on supported operating systems. |
| Calibre library database API | Calibre's published database API describes a thread-safe multiple-reader/single-writer locking scheme. | A mature personal media catalog treats concurrent reads and serialized writes as a deliberate policy. This is a useful concurrency pattern, not evidence that DPV should copy Calibre's schema or internals. |
| SQLite embedded database | SQLite positions itself for local application/device storage. WAL permits readers and a writer to proceed concurrently, but still only one writer at a time; it requires same-host shared memory and is unsuitable for a network filesystem. | The approved local SQLite catalog is compatible with the offline desktop shape, provided writes are coordinated and the database remains local. |
| Dapper | The project describes Dapper as a simple object mapper with explicit query/execute APIs over database connections. | It fits the approved explicit-SQL approach, but DPV must own schema migrations, transaction boundaries, query/index design, and persistence tests. |

### Strengths and weaknesses of the recommended pattern

| Pattern | Strengths | Weaknesses / limits |
|---|---|---|
| Lazy, bounded file traversal | Avoids holding the complete path set in memory; supports early progress and work pipelining. | Does not make enumeration cancellable while a filesystem call blocks; access, mutation, permissions, and link behavior remain policy decisions. |
| Local SQLite catalog with external model files | Offline, single-user deployment without a database service; transactional metadata and indexed queries; keeps multi-terabyte model bytes in their original locations. | SQLite serializes writers; WAL creates sidecar/checkpoint/backup concerns; it is not a shared multi-host database. |
| Dapper with explicit SQL | Transparent query shape and mapping; few conventions obscure what the persistence code does. | More SQL and migration discipline belongs to DPV; it does not supply entity identity, schema evolution, or domain policy. |
| Batched, idempotent import | Bounds transaction duration and recovery scope; repeated scans can avoid duplicate rows once identity is defined. | Batch commits mean cancellation/failure can leave a valid but partial catalog; completion and reconciliation semantics must be modeled explicitly. |

These are architectural tradeoffs, not measured DPV performance results.

### Alternatives considered

| Alternative | Assessment |
|---|---|
| Client/server database for the MVP | Not justified for a single-user, offline-first local catalog. Reconsider only if concurrent multi-user writes, remote shared catalog access, or measured SQLite limits become product requirements. A network-mounted model-file root does not imply a network-mounted SQLite database. |
| EF Core instead of Dapper | Not selected. REA-001 recommended EF Core, but `REA-001-REVIEW.md` did not adopt it and `DPV-SOLUTION-001.md` explicitly approves Dapper. Reopening this choice requires a reviewed architecture decision, not a feature-level substitution. |
| Hash every file during initial discovery | Not selected. It reads all file bytes, increasing scan I/O substantially, and conflates content identity with path/location identity. Add content hashing only under the separately approved duplicate-detection contract or a demonstrated integrity requirement. |
| Materialize all paths, then import | Not selected. It increases memory with library size and delays useful progress. Use streaming traversal with bounded buffering. |

### SQLite

SQLite is designed for local application/device storage and provides
transactional atomicity. WAL improves reader/writer coexistence but still
allows only one writer at a time. WAL requires processes to share memory on
the same host, creates `-wal`/`-shm` sidecars, and requires checkpoint/backup
awareness; it must not be used by placing the database on a network
filesystem. A long-lived read transaction may prevent checkpoint progress and
allow the WAL to grow. The project already requires WAL and foreign-key
enforcement in `DPV-CONFIGURATION-001.md`; Commit C must validate the exact
SQLite provider/version and operational settings used by DPV.

The import pipeline should separate filesystem discovery from bounded database
writes. Keep write transactions bounded and define how interruption affects
already committed batches. Do not hold one transaction open for the entire
walk of a potentially very large root.

Serialize all import writes through a single coordinated writer path. Keep UI
list reads short and paged so readers do not unnecessarily hold snapshots open.
Choose and test busy/timeout behavior, checkpoint behavior, and backup/restore
against the selected provider rather than assuming WAL defaults meet DPV's
workload.

### Dapper and migration ownership

Dapper provides explicit SQL/mapping and fits the approved architecture, but
the project owns query shape, schema evolution, mapping, transaction behavior,
and migration verification. The Dapper API is a mapping/query layer; it does
not define DPV's migration policy. Commit C must approve a migration/versioning
mechanism before implementation. Keep all SQL and connections behind
Infrastructure. Use parameters for values and explicit transactions for each
bounded persistence unit; integration tests must exercise the actual SQLite
provider rather than mock Dapper calls.

### Avalonia/MVVM

Filesystem work, hashing, and database operations must not block the UI thread.
Expose progress and cancellation as explicit presentation state, and marshal UI
updates through Avalonia's supported dispatcher/threading model. Large result
sets should be paged/virtualized rather than retained as hundreds of thousands
of UI objects.

Avalonia documents a single UI-thread model: UI/control access must occur on
that thread, with dispatcher APIs for cross-thread updates. Keep the scanner
and persistence workflow independent of Avalonia; let the ViewModel translate
progress/results into UI state.

### Repository assessment

These are design patterns, not claims that the current DPV code already has
scanner or persistence behavior. The audited source contains no SQLite/Dapper
package, database initializer, model entities, import service, or dashboard;
the existing DI verification report explicitly says SQLite was out of scope.

## 4. Recommended Feature Flow

```text
UI folder selection
    → explicit import request / application contract
    → bounded filesystem enumeration
    → file-candidate validation and metadata extraction
    → bounded SQLite/Dapper writes
    → database-backed list query
    → paged/virtualized UI presentation
```

The application currently has no `DPV.Application` project. Respect the
approved project structure. The recommended MVP boundary is:

- `DPV.Domain.Contracts` owns the cross-project import use-case contract and
  technology-neutral request/result types.
- `DPV.Infrastructure.Import.Services` implements that contract and
  coordinates its scanning and persistence adapters.
- `DPV.UI` consumes the Domain contract and maps results to presentation state;
  it does not reference Infrastructure implementation types.
- `DPV.App` registers the contract-to-implementation mapping.

This follows the existing contracts that assign cross-project contracts to
Domain, import services to Infrastructure, and composition to App. The
tradeoff is that application orchestration lives in Infrastructure because no
Application project is approved. Keep domain invariants in Domain; do not let
Infrastructure orchestration absorb business rules. Use Infrastructure's
`Import/Contracts` only for module-local adapter boundaries, not as a duplicate
public API. Revisit this boundary through a separate architecture review if
application workflows grow beyond focused coordination of Infrastructure
adapters. This is a recommendation for review, not implementation
authorization.

### Processing safeguards

- Stream enumeration and inspect only required metadata; do not read STL
  contents merely to list files.
- Set enumeration options explicitly. Decide whether hidden/system files are
  eligible, report inaccessible entries rather than treating a silently
  skipped subtree as a complete import, and define reparse-point/symlink policy.
- Use bounded work queues and a deliberately limited concurrency policy.
- Keep a single coordinated SQLite write path unless measured evidence and
  SQLite semantics justify another design.
- Check cancellation between directories/files and before committing each
  bounded unit of work.
- Make progress meaningful (counts/stages); do not promise an exact percentage
  if total work is unknown without a full pre-scan.
- Do not hide permission, I/O, or database errors. Summarize partial failures
  and retain actionable diagnostic details.
- Treat file metadata as a point-in-time observation; define what happens if a
  file changes between enumeration and persistence.

## 5. Identity, Rescan, and Data Semantics

The existing Artist → Collection → Model hierarchy does not fully specify the
identity of physical files. A logical model may have variants or multiple
locations, but introducing that relationship requires a reviewed domain
decision.

Commit B must specify:

1. Whether one discovered STL initially maps to one Model record.
2. How imported models without known Artist/Collection values are represented.
3. A stable root identity and normalized relative-path policy.
4. Whether size/timestamp are sufficient for change detection or a content hash
   is required; hashing every multi-terabyte library has a substantial I/O cost.
5. Whether the same bytes at two paths are separate assets, duplicate records,
   or linked locations; do not delete or merge automatically.
6. Outcomes for unchanged/changed/moved/missing/inaccessible files and
   interrupted scans.
7. Whether a partial scan's records are committed incrementally or atomically
   at a defined batch boundary.

Recommend initially cataloging each discovered path distinctly unless an
approved identity rule says otherwise. Do not infer artist/collection from
directory names by default, collapse files by hash, or promise relocation
detection until those behaviors have explicit acceptance tests.

Recommended default for the walking skeleton: use a stable application-owned
source-root identity plus a normalized root-relative path as the rescan key;
apply the selected filesystem's case semantics rather than lowercasing paths
globally. Treat byte-identical files at different paths as distinct import
entries, and defer full-content hashing to the separately approved duplicate
feature. Use file length and last-write time as inexpensive metadata for
change screening, not as proof of content identity; use a content hash only
when the approved contract requires exact change/duplicate detection. On a
completed scan, unchanged paths are idempotent, changed paths update the
associated file metadata, and paths not seen in an incomplete, cancelled, or
partial scan are never marked missing. Only a completed scan may reconcile
unseen entries, and even then it must not delete user files or catalog records
without an explicit retention contract.

This keying proposal does not settle whether a path is itself a Model or a
file/location associated with a logical Model. Commit B must name that concept
and its relationship before defining the public contract and schema.

## 6. Performance and Reliability

The product target is 250,000+ models and multi-terabyte file libraries. A
count alone does not prove a specific SQLite query/scan performance result.

Commit A must define a reproducible profile:

- Reference OS, hardware, available memory, and storage type.
- Number of directory entries, STL count, nesting depth, and representative
  file-size distribution.
- Catalog starting size and existing-record ratio.
- Import throughput, peak memory, cancellation response, and database growth.
- First-page/reload latency while a scan is running.
- Explicit thresholds and acceptable partial-failure behavior.

The test dataset should include duplicate names in different directories,
mixed-case extensions, inaccessible/missing paths where portable, empty files,
large files, hidden/system entries, reparse points where supported, and path
edge cases. Keep fixtures small and deterministic in regular CI; run
scale benchmarks separately with generated/anonymized data. Define a stable
sort key and paged query contract; prefer keyset/continuation paging for
deeply-scrolled large catalogs if measurements confirm offset paging cost.

Do not treat 250,000 records as an SQLite capacity guarantee or a performance
claim. Benchmark DPV's actual schema, indexes, query projections, storage, and
provider. The target is metadata rows; storing STL bytes in the database is
out of scope.

## 7. Testing Strategy

### Unit tests

- Candidate extension/metadata rules and path identity normalization.
- Scan state transitions and progress/cancellation behavior.
- Error classification and rescan decision rules.
- ViewModel command/progress/error behavior.
- Enumeration policy for mixed-case extensions, hidden/system entries,
  inaccessible directories, symlink/reparse points, and files that disappear
  or change while being scanned.

### Integration tests

- Real temporary source directory with nested paths and controlled fixture
  files.
- Real temporary SQLite database file, migrations, Dapper mappings, constraints,
  transactions, reopen/reload, and rollback.
- Interrupted/partial scan and repeated-scan idempotency.
- Verify incomplete/cancelled scans do not reconcile unseen files as missing.
- Database-backed list query after import, not only scan-result collection.
- Concurrent read during import within the chosen write policy.
- Migration from each supported prior schema, foreign-key enforcement,
  connection configuration, backup/restore, and WAL sidecar/reopen behavior.

Avoid relying solely on `:memory:` for persistence integration tests if tests
need separate connections, reopen/restart, WAL, or migrations. A temporary
database file exercises those behaviors more faithfully. Do not mock SQL
behavior.

### End-to-end artifact

On the supported desktop target:

1. Select the controlled target folder.
2. Observe progress and completion/error summary.
3. Confirm expected rows in SQLite.
4. Reload/reopen the UI and confirm the same models are displayed from SQLite.
5. Preserve the test input, database/query evidence, CI run, and UI verification
   record as the Commit F artifact.

## 8. Common Mistakes and Long-Term Risks

| Severity | Risk | Why it matters | Mitigation |
|---|---|---|---|
| High | Treating paths, models, and physical assets as one identity | Rescans, duplicates, variants, and moves become ambiguous. | Define identity in Commit B; test repeated/moved/changed cases. |
| High | Default enumeration silently skips inaccessible, hidden, or system entries | An incomplete import can appear successful and leave catalog coverage unknowable. | Explicitly set enumeration options; record exclusions and partial-scan status. |
| High | `GetFiles`/full result materialization for a huge tree | Memory grows with the number of paths before useful work begins. | Lazy enumeration plus bounded queue/writes. |
| High | Unbounded parallel file hashing/parsing | Disk contention, memory pressure, and UI stalls. | Hash only when required; bound concurrency and benchmark. |
| High | One long transaction or multiple competing import writers | Writer contention, slow cancellation/recovery, and poor interruption behavior. | Serialize writes; use bounded transactions and explicit partial-scan semantics. |
| High | Reconcile unseen paths after an incomplete scan | A permission failure or cancellation can falsely mark valid files as missing. | Reconcile only after a completed scan; never infer missing from partial results. |
| High | SQLite file on a network share or incomplete WAL backup | WAL requires same-host shared memory and backup must account for sidecars/active state. | Keep catalog local; use a documented SQLite backup/checkpoint procedure and verify restore. |
| High | UI list populated from scan memory only or background thread touches UI state | Restart loses results or Avalonia rejects cross-thread UI access. | Query SQLite for display; keep work off the UI thread and marshal presentation updates. |
| Medium | In-memory SQLite only in integration tests | Can miss connection lifetime, WAL, reopen, backup, and migration failures. | Use temporary on-disk SQLite integration tests; keep memory DB to narrow unit-like tests. |
| Medium | Parsing every STL during discovery | Adds unnecessary work and couples import to geometry/rendering. | Catalog path/metadata first; defer geometry analysis and thumbnails. |
| High | Undefined schema migration and recovery lifecycle | Upgrades, rollback, backup, and corrupt/unavailable DB behavior become ad hoc. | Commit C migration/versioning and restore contract; test supported upgrade paths. |
| High | Claiming 250,000+ performance without a representative workload | A row-count target does not prove import/query latency or memory use. | Measure a declared dataset/hardware profile and preserve benchmark evidence. |

## 9. Recommendation

**APPROVE WITH CHANGES** to enter planning only:

1. Keep SQLite + Dapper as the approved MVP data-access stack.
2. Keep model bytes external and the SQLite database local.
3. Use bounded enumeration with cancellation checks between filesystem
   operations, explicit enumeration/error policies, and bounded database writes.
4. Make the model list read from SQLite after import/reload.
5. Define identity, rescan, path, partial-failure, migration, and performance
   contracts before implementation.
6. Use real SQLite file-backed integration tests and a separate scale
   benchmark.
7. Do not introduce a new application project, ORM, renderer, search engine, or
   destructive file workflow as part of this slice.

The project owner approved this REA and its changes on 2026-11-01. The REA
gate is complete for planning; Commit A still requires a defined GitHub
planning record/owner and explicit phase authorization. This approval does not
authorize implementation.

## 10. Source and Evidence Notes

### Repository evidence reviewed

- `DEVELOPMENT_STANDARD.md` — mandatory REA and Commit A–F gates.
- `docs/PROJECT_CONSTITUTION.md` — offline operation, database source of truth,
  Artist → Collection → Model, 250,000+ target.
- `docs/architecture/DPV-SOLUTION-001.md` — approved SQLite + Dapper; EF Core
  not approved for MVP.
- `docs/architecture/DPV-CONFIGURATION-001.md` — WAL, foreign keys, database
  location, and module boundaries.
- `docs/architecture/DI-VERIFICATION-REPORT-001.md` — its verification scope
  excludes SQLite registration and database initialization; this is not a
  formal disposition of Sprint 1 issue acceptance criteria.
- `docs/research/REA-001-Solution-Architecture.md` — SQLite/WAL, FTS5, Dapper
  versus EF, file-identity, performance, and risk analysis (reviewed
  2026-10-28).
- `docs/research/REA-001-REVIEW.md` — formal review; does not adopt REA-001's
  EF Core recommendation.
- Current `src` project files and sources — confirm no current persistence or
  scanning implementation and confirm App/UI ownership.
- GitHub CI runs `41295043104`, `41295839362`, and `41296262764` verify existing
  build/test state, not scanner/database behavior.

### External sources reviewed

The following pages were retrieved and reviewed on 2026-11-01. The findings
above are documentation/repository evidence, not measurements of DPV runtime
behavior.

- [.NET 10 `Directory.EnumerateFiles` API](https://learn.microsoft.com/en-us/dotnet/api/system.io.directory.enumeratefiles?view=net-10.0) — enumerable-returning overloads and filesystem exceptions.
- [.NET file and directory enumeration guidance](https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-enumerate-directories-and-files) — enumerable collections for large trees; separate directory traversal to catch access failures.
- [.NET 10 `EnumerationOptions.IgnoreInaccessible`](https://learn.microsoft.com/en-us/dotnet/api/system.io.enumerationoptions.ignoreinaccessible?view=net-10.0) — default is `true`, which skips inaccessible files/directories.
- [.NET 10 `EnumerationOptions.AttributesToSkip`](https://learn.microsoft.com/en-us/dotnet/api/system.io.enumerationoptions.attributestoskip?view=net-10.0) — default skips `Hidden | System`.
- [Calibre database API documentation](https://github.com/kovidgoyal/calibre/blob/master/manual/db_api.rst) — documents a thread-safe multiple-reader/single-writer scheme; used only as a concurrency-pattern comparison.
- [SQLite: Appropriate Uses For SQLite](https://www.sqlite.org/whentouse.html) — SQLite's local application/device-storage use case versus client/server databases.
- [SQLite Write-Ahead Logging](https://sqlite.org/wal.html) — reader/writer concurrency, single-writer constraint, same-host requirement, checkpointing, and sidecar behavior.
- [SQLite is Transactional](https://www.sqlite.org/transactional.html) — transaction atomicity and crash/power-failure guarantees.
- [SQLite Online Backup API](https://www.sqlite.org/backup.html) — supported backup mechanisms and the risks of copying a live database file.
- [Dapper README](https://github.com/DapperLib/Dapper/blob/main/Readme.md) — object-mapping/query API and explicit SQL execution model.
- [Avalonia threading model](https://docs.avaloniaui.net/docs/app-development/threading) — UI-thread access and dispatcher marshaling.

Revalidate exact API behavior, package compatibility, SQLite provider behavior,
and supported-platform details before Commit C/D. Live documentation and
branch-based source URLs can change after the research date.
