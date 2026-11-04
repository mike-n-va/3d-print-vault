# Definition 011: Scan Identity and Rescan Rules

### Sprint 2 Commit B — DPV-S2-003

**Status:** Definition only. No scanner, schema, query, or infrastructure implementation is authorized.
**Date:** 2026-11-04
**Scope:** STL eligibility, `ModelIdentity`, content hashing, rescan behavior, and `ImportError` codes

## 1. Purpose

This document defines the deterministic rules for file eligibility, unique identity assignment, content hashing, and rescan behavior within the **3D Print Vault** library import pipeline (`DPV-S2-003`). These rules ensure that local file changes are tracked reliably without corrupting database state or violating domain purity.

This document is a specification. It does not add a scanner, database table, Dapper query, repository method, or UI behavior. [Definition-009B](Definition-009B-Approved-Domain-Entities.md) still gives each domain Model an immutable `Guid`. `ModelIdentity` below is the catalog identity used by import. It does not replace that `Guid`, and it does not add fields to the domain entities.

The import never deletes, moves, or rewrites the user's files. Geometry bytes stay on disk.

## 2. STL File Eligibility Criteria

A file encountered during a library scan is considered an eligible 3D model candidate **only if** it satisfies all of the following conditions:

- **Extension Matching:** The file extension matches `.stl` (evaluated case-insensitively). The comparison uses the final extension only. `model.STL` matches. `model.stl.bak` does not.
- **Size Threshold:** The file size is greater than 0 bytes and does not exceed the maximum configured file size limit (default: 2 GB, meaning 2,147,483,648 bytes). A size of 0 is not eligible and is not one of the error codes in §5. A size above the configured maximum is `ERR_FILE_TOO_LARGE`.
- **Accessibility:** The file can be opened with exclusive read sharing (`FileShare.Read`), ensuring incomplete downloads, active slicer writes, or locked files are safely skipped during the active scan pass. A skipped file is not cataloged from this observation, is not marked missing, and does not change an existing record. The open request allows other readers and refuses to treat a file another process is writing as eligible.

Hidden and system files are not excluded by these rules when the three conditions above succeed.

## 3. Unique Model Identity (`ModelIdentity`)

A model's identity in the catalog is governed by a composite key consisting of:

1. **Normalized Absolute File Path:** The canonical, fully-resolved absolute file path (normalized to lowercase on case-insensitive filesystems to prevent duplicate tracking across varying casing). Resolution removes `.` and `..` and does not keep a trailing directory separator. Casing is preserved on case-sensitive filesystems. On case-insensitive filesystems, lowercase uses the invariant culture. Path equality is ordinal after that normalization. At most one catalog record exists for one normalized path.
2. **Content SHA-256 Hash:** A cryptographic hash calculated from the first 8 KB and last 8 KB (along with file size for optimization on large files, or full-file hash for files under 16 MB) to uniquely identify the exact geometry bytes regardless of file renaming or folder relocation.

The hash input is fixed so two implementations produce the same digest:

| File size | Hash input |
|---|---|
| Greater than 0 and under 16 MB (16,777,216 bytes) | Every file byte, in order. |
| 16 MB or larger, and within the configured maximum | The first 8,192 bytes, immediately followed by the last 8,192 bytes, immediately followed by the file length as an unsigned 64-bit little-endian integer. |

The stored canonical hash is 64 lowercase hexadecimal characters. A hash is retained only after the selected input is read completely. A partial digest is never stored.

The same hash at two normalized paths that are both still present is two `ModelIdentity` values and two catalog records. This definition does not merge those records.

Filesystem last-write time and file length are observations used by §4. They are not additional identity components. The scan root is the normalized absolute path of the selected folder. A catalog path must remain inside that root after normalization.

## 4. Rescan & Change Detection Matrix

When executing a subsequent library rescan against previously cataloged records, the system evaluates files using the following matrix. Records are compared only within the same scan root.

| State Change Scenario | Detection Mechanism | System Action |
| :--- | :--- | :--- |
| **Unchanged File** | Path exists, modified timestamp matches, and SHA-256 hash matches. | Skip processing; increment `TotalSkipped` metric. Do not rewrite the path, hash, timestamp, or domain `Guid`. |
| **Modified File** | Path exists, but modified timestamp or SHA-256 hash differs from stored record. | Update file metadata, re-index hash, and flag record as updated. Keep the domain `Guid` and the normalized path. |
| **Moved / Renamed File** | Path differs, but SHA-256 hash matches an existing cataloged record. | Relocate file path mapping in the catalog without re-importing geometry. Keep the domain `Guid` and the content hash. |
| **Deleted / Missing File** | Cataloged path no longer exists on disk during a full library scan pass. | Mark record as missing/inactive (soft-delete or archive status per retention policy). Do not remove the record or the user's file. |

Evaluation order, so the rows cannot corrupt one another:

1. A file that fails §2, or that returns an error in §5, is not an accepted observation. It cannot prove a path unchanged, modified, moved, or deleted.
2. **Unchanged** requires both the stored modified timestamp and the stored hash to match. If either differs, the file is **Modified**, including a timestamp change whose hash is unchanged.
3. **Moved / Renamed** applies only when the new path's hash equals exactly one existing record and that record's path was not accepted during this scan. The old path is then the relocated path, not a second missing record. If several records share that hash, or the matched record's path is still present, the new path is a separate identity and no existing path is rewritten.
4. **Deleted / Missing** applies only on a full library scan pass that completes. A cancelled, failed, or partial pass must not mark records missing or inactive. A path skipped under §2 because it was locked or still being written is still present and must not be marked missing.

`TotalSkipped` counts unchanged files only. It is an import-result metric, not a database column defined by this document.

A first acceptance of an eligible file creates one catalog record. Its initial domain Model name is the file name without the extension, validated by Definition-009B. Artist and Collection are not inferred from the path.

## 5. Structured Error Classes

The import and identification pipeline must return standardized error codes via `ImportError` when encountering anomalies. One file error does not stop the rest of the scan and does not mutate a catalog record from that failed observation. Each error carries the code, the normalized path when one could be formed, and a human-readable message.

- `ERR_IO_ACCESS`: File could not be read due to permission locks, I/O exceptions, or network dropouts. A sharing violation that prevents the `FileShare.Read` open in §2 is reported with this code and skipped for catalog changes.
- `ERR_INVALID_FORMAT`: File extension matches, but header inspection or parsing fails basic structural validation. Validation passes only when either the binary STL length rule holds or the file is ASCII STL. Binary validation passes when the file is at least 84 bytes and `file length = 84 + (little-endian triangle count at offset 80 × 50)`. ASCII validation passes when that binary rule fails and the file begins with an optional UTF-8 BOM, optional whitespace, and the case-sensitive prefix `solid`. Any other readable `.stl` is this error.
- `ERR_HASH_FAILURE`: Exception occurred while computing the SHA-256 content hash, or the read ended before the hash input required by §3 was complete.
- `ERR_FILE_TOO_LARGE`: File exceeds the maximum allowable size threshold.

No other import error code is defined for this pipeline.

## 6. What this definition does not create

Commit B stops at these rules. The repository must not gain, as part of this definition, a directory scanner, hash implementation, SQLite table, migration, Dapper query, or change to `IModelRepository`. Commit C may bind the maximum file size and the scan-root location. It must not redefine eligibility, `ModelIdentity`, the rescan matrix, or these error codes.
