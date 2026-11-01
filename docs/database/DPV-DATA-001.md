# DPV-DATA-001
## Data Architecture and Persistence Contract

**Status:** Approved by project owner — 2026-11-01
**Database:** SQLite
**Data access:** Dapper (approved for MVP by `DPV-SOLUTION-001.md`)
**No schema or persistence implementation is authorized by this document.**

---

## 1. Authority and Invariants

- The local SQLite database is the authoritative source for catalog metadata.
- STL/model file bytes remain outside SQLite at user-managed paths.
- The core hierarchy is Artist → Collection → Model.
- A model's catalog record and its physical file location are concepts that
  must not be conflated without an explicit domain decision.
- The database is local to the application host. Do not place a WAL database
  on a network share or treat it as a cross-PC shared database.
- Enable SQLite foreign-key enforcement and use WAL as required by the existing
  `DPV-CONFIGURATION-001.md`; verify actual connection configuration in tests.

## 2. Data Categories

| Category | Authority | Notes |
|---|---|---|
| Artist, collection, model metadata | SQLite | Exact fields, nullability, and relationships are Commit B decisions. |
| Source model bytes | User-managed filesystem | Never copy multi-terabyte libraries into the catalog database. |
| Source path/root metadata | SQLite reference to filesystem | Root identity, normalization, case sensitivity, relocation, and missing-root behavior require an explicit contract. |
| Search index (future) | Derived from catalog | FTS/index rebuild and synchronization belong to the Search feature. |
| Thumbnails/derived previews (future) | Rebuildable cache | Cache location, size limits, and invalidation are not part of the walking skeleton. |
| Logs/configuration | Application files/settings | Do not treat logs or appsettings as catalog authority. |

## 3. Walking-Skeleton Data Contract (to approve in Commit B)

The import feature must define, before implementation:

- Whether one discovered STL initially creates one Model record or an asset
  attached to a separately identified logical model.
- How Artist/Collection are represented when folder names cannot safely
  determine them.
- Stable identity for a source root and file location.
- Whether identity uses normalized root-relative path, file content hash, or
  another explicit key; paths and hashes solve different identity problems.
- Behavior for rescans: unchanged file, changed file, moved file, missing file,
  duplicate bytes at another path, inaccessible file, and file changing during
  scan.
- Whether duplicate paths/content are warnings, separate records, or linked
  assets; no source file may be automatically deleted.

Do not silently infer Artist/Collection or collapse byte-identical files into a
single record. A later feature may revise the model after reviewed contracts.

## 4. SQLite and Dapper Rules

- Dapper is the one approved general data-access approach for the MVP.
- Keep SQL, mapping, connections, and migrations in Infrastructure.
- Use parameterized SQL for values; explicitly handle any query-language
  semantics for FTS expressions in the separately approved Search feature.
- Define an explicit schema version and migration runner before database
  creation is integrated into startup.
- Test migrations from each supported prior schema using real SQLite.
- Use transactions for logically atomic catalog changes. Define bounded batch
  size and cancellation semantics before importing large trees; do not hold a
  single unbounded transaction over an entire multi-terabyte file walk.
- Keep database connections short-lived/non-singleton; follow the existing DI
  lifetime contract for the factory and connections.
- Configure and verify foreign keys, busy/timeout behavior, WAL, and checkpoint
  policy. These operational settings must be tested rather than assumed.
- Never mock database correctness; integration tests use real SQLite.

## 5. Location and Deployment

The existing configuration contract lists `%AppData%/3DPrintVault/` for
production and `/data/dev.db` for development. The latter is not a portable
Windows path and needs correction/approval in Commit C; this document does not
silently replace it.

Commit C must define:

- Per-user database location and directory creation/permissions.
- Development/test database locations.
- Recovery behavior for an unavailable or corrupt database.
- Backup/restore procedure that accounts for active WAL state.
- Whether removable/network-hosted model roots are supported and how their
  availability is surfaced.

Do not back up a live WAL database by copying only its main database file.
Choose a supported SQLite backup/checkpoint procedure and verify restore.

## 6. Scale and Performance

The catalog target is at least 250,000 models; model files may total multiple
terabytes. This does not imply the database stores file bytes or that an
unmeasured latency target is already satisfied.

Before implementation, define a representative benchmark profile including:

- Catalog size and realistic metadata/path lengths.
- Source file count and directory depth.
- Cold and warm query/list latency.
- Import throughput, memory, and database growth.
- Concurrent UI reads during import.
- Cancellation and restart behavior.

Use bounded result pages/projections for lists and avoid loading all records
into memory. Establish numeric acceptance thresholds in Commit A, not after
performance regressions appear.

## 7. Testing

- Repository/query tests run against real SQLite, not mocks.
- Prefer temporary database files for integration tests that need multiple
  connections, reopen/restart, WAL, and migration behavior.
- If an in-memory database is used for a narrow test, keep the same connection
  alive or explicitly test the selected shared-memory behavior; do not assume
  separate connections share an ordinary in-memory database.
- Use isolated temporary source folders with fixtures for enumeration edge
  cases; tests must clean only their own known temporary paths.
- Test foreign keys, transaction rollback, migrations, path/identity
  uniqueness, concurrent read/write expectations, and failure reporting.

## 8. Open Decisions

1. Exact entity fields and constraints.
2. Model-versus-file/asset identity and rescan semantics.
3. Migration tooling/format and schema-version policy.
4. Production/development/test paths and backup/restore.
5. Busy timeout, transaction batch size, and import concurrency.
6. File-root lifecycle and path normalization per supported OS.

Resolve these in the relevant Commit B/C artifacts before database/scanner
implementation.

## 9. Approval

Approved by the project owner on 2026-11-01 after review in the current
conversation. The identified identity, migration, configuration, and
performance decisions remain assigned to Commit B/C. Approval of this contract
does not authorize implementation or waive the REA and Commit A–F gates.
