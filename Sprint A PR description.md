# SPRINT A: Idempotent scanning with schema v11, natural keys, and end-of-scan reconciliation

**Branch:** `sprint-a/idempotent-scanning` → `feature/v2-maker-sanctuary`
**Commit:** `b62d687` (single commit, patch attached)
**Scope source:** [DPV-ROADMAP-001](../roadmap/DPV-ROADMAP-001.md) Sprint A — *Scanning the same library N times yields byte-identical catalog contents; deleting a folder on disk and rescanning removes its cards.*

---

## Summary

Sprint A makes the v2 scanner idempotent and self-healing. Re-scanning a library no
longer duplicates catalog rows, model GUIDs and user metadata survive rescans,
rows for folders that vanish from disk are retired, and retired rows never surface
in the UI. The one-time v10 → v11 migration also cleans up the damage the
per-scan-GUID bug already caused in existing databases (duplicate `Models` rows
per folder, legacy v1 flat rows).

## Defects addressed

| # | Defect | Root cause | Fix |
|---|--------|-----------|-----|
| 1 | Re-scans duplicate every model | `DirectoryScannerService` minted a fresh GUID per scan, so `ON CONFLICT(Id)` could never fire | Tier-3 upsert keyed on natural key `FolderPath` (schema v11 enforces it with a unique partial index) |
| 2 | Deleted folders stay on the dashboard | Scanner had no reconciliation pass | Bottom-up end-of-scan sweep retires Files → Models → Releases → Artists with no disk backing |
| 3 | Quick hash broke duplicate detection | `ComputeQuickHash` hashed path+size | Real SHA-256 content hash (`ContentHasher.Hash`) with edge sampling for large files; quick-hash only as IO-error fallback |
| 4 | Legacy v1 rows mixed into v2 catalog | Two pipelines write the same tables | v11 migration retires v1-lineage rows (`FolderPath IS NULL`) with `SourcePipeline='legacy'`; read queries filter `IsActive = 1` |
| 5 | No per-release atomicity | Inserts ran outside any transaction | Per-release transaction with commit/rollback and per-release error isolation (scan continues past a bad release) |
| 6 | Scans could touch other roots | No root scoping | Reconciliation scoped to the scan root with separator-boundary checks (`lib` vs `lib-extra`), skipped when a scan is cancelled |

## Changes

### Schema v11 (`MigrationRunner.cs`)

- `IsActive` tombstones on `Artists`, `Releases`, `Models`, `Files` (default 1)
- `SourcePipeline` lineage on `Models`/`Releases` (default `'v2'`)
- `Files.ContentHash` for real content hashes
- One-time migration: retires legacy rows (`FolderPath IS NULL`), dedupes duplicate
  `Models` per `FolderPath` keeping the newest, then creates
  `UX_Models_FolderPath` (unique among active rows) and `IX_Files_ContentHash`
  (partial, active rows only)

### Scanner (`DirectoryScannerService.cs`)

- Model upsert: `SELECT` by `FolderPath` (preferring the active row) then `INSERT`
  or `UPDATE` — GUIDs survive; `IsFavorite`/`PrintNotes` untouched by rescan
- Per-release `BeginTransaction` with commit/rollback and error isolation
- Real content hashing via `ContentHasher.Hash(stream, size)`
- `ReconcileAsync`: retires dead files, then empty models → releases → artists,
  scoped to the scan root (SQL prefix includes the separator boundary; C#
  `IsUnderRoot` mirrors it), runs in its own transaction, skipped when cancelled
- Retired artists/releases/models are reactivated on rescan
- Stale `ScanState` rows for vanished folders are deleted

### Read side

- `DapperCatalogQuery`, `DapperReleaseRepository`, `DapperModelDetailRepository`:
  `IsActive = 1` filters on all read paths so retired rows never reach the UI
- `MainDashboardViewModel`: status text reports retired counts

### Tests (`ScannerIdempotencyAndReconciliationTests.cs`, 18 new)

Rescan idempotency across all tiers, model identity + user-metadata preservation,
reconciliation for deleted file / deleted folder / removed release, reactivation of
restored folders, root scoping (sibling-prefix regression), cancelled-scan safety,
content-hash behavior (same content → same hash, same size different content →
different hash, modified content updates hash, moved file repoints), and three
v11 migration tests (columns/indexes, legacy retirement + dedup, unique-index
enforcement including a `DowngradeToV10` re-migration path).

## Verification

- `dotnet build` — success, 0 warnings, 0 errors (.NET 10)
- `dotnet test` — **285 total: 276 pass, 9 fail (pre-existing on the base branch,
  confirmed by running the same filters on a stashed tree)** — failures are the
  disconnected semantic-clustering and dashboard tests already failing on
  `feature/v2-maker-sanctuary`; not touched by this PR
- All 18 new Sprint A tests pass

## Acceptance criteria mapping

- *Scanning the same library N times yields byte-identical catalog contents* —
  `Rescan_SameLibrary_DoesNotDuplicateAnyTier`, `Rescan_PreservesModelIdentity`,
  `Rescan_ThroughCatalogQuery_ReturnsSameModelCount`
- *Deleting a folder on disk and rescanning removes its cards* —
  `Reconcile_DeletedModelFolder_RetiresModelAndCascadesUpwardWhenEmpty`,
  `Reconcile_RemovedRelease_HidesCardFromReleaseQuery`

## Risks and follow-ups

- Reconciliation is scoped per scan root: a file deleted under root B is not
  cleaned up by a scan of root A (correct, but stale rows persist until that root
  is scanned again — documented in the test)
- Duplicate rows retired by the v11 dedup remain in the DB (soft delete) for
  auditability; a later Sprint E vacuum/cleanup task can purge them
- Remaining Sprint A review items not in this PR: FTS index does not yet filter
  retired models inside `Models_Fts` matching (queries filter `IsActive` after
  the FTS match — acceptable at current catalog sizes, revisit with Sprint C)

## How to try it

```bash
git fetch origin && git checkout sprint-a/idempotent-scanning
dotnet test --filter "FullyQualifiedName~ScannerIdempotencyAndReconciliationTests"
```

Or apply the attached patch: `git am sprint-a-idempotent-scanning.patch`

<sub>Created with [Perplexity](https://www.perplexity.ai/computer/tasks/8ca8e8b0-12c0-485d-a960-91610387208c)</sub>
