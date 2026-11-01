# DPV-ROADMAP-001

Master roadmap for 3D Print Vault.

---

# Current Phase

```text
Sprint 2 readiness — blocked; implementation not authorized
```

Sprint 1's roadmap closure statement is under review. The GitHub milestone
contains open Sprint 1 issues, and Sprint 1 acceptance evidence is incomplete.
See [SPRINT-1-CLOSURE-AUDIT-001.md](../reviews/SPRINT-1-CLOSURE-AUDIT-001.md).

The mandatory project documents were empty on the audited remote `main`
(`8fdb197808ff0ea736cba12a2ddd2f1b7937380f`). Local versions are now reviewed
and approved by the project owner, but remain uncommitted and unpublished.
Sprint 1 issue scope/disposition and Commit A authorization remain outstanding.

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
| DPV-S2-002 | Commit A — Contracts | Pending explicit authorization and a GitHub planning record/owner. Define import and catalog contracts, failure/cancellation behavior, acceptance criteria, and a measurable performance profile. Public use-case contract and technology-neutral request/result types belong in Domain; no implementation. |
| DPV-S2-003 | Commit B — Definitions / resolver | Define artist/collection/model/file-location semantics, scan identity, exact-hash duplicate/rescan behavior, and error classification. No implementation. |
| DPV-S2-004 | Commit C — Evaluation / configuration | Define database/root configuration, validation, migration strategy, path policy, concurrency/batch limits, and logging/progress configuration. No implementation. |
| DPV-S2-005 | Commit D — Implementation | Implement the import use case and its filesystem/persistence adapters in Infrastructure, with unit and real-SQLite tests, strictly against the approved Domain contract. No UI coupling or application-wide wiring/end-to-end integration in this phase. |
| DPV-S2-006 | Commit E — Integration | Register the Domain contract to its Infrastructure implementation in App; connect folder selection, ViewModel consumption of the Domain contract, and database-backed list presentation. |
| DPV-S2-007 | Commit F — Verification / validation | Run restore/build/tests on the supported CI platforms; verify import, reload-from-SQLite artifact, cancellation/error behavior, and agreed performance/resource thresholds. |

Each phase needs a named assignee/owner in its GitHub record and a reviewable
phase artifact. Follow DEVELOPMENT_STANDARD.md's implementation, verification,
commit, and push gate before starting the next phase. REA and planning phases
must be approved before Commit A.

---

# Deferred Features

These remain outside the walking-skeleton implementation. Exact duplicate
detection remains an approved MVP capability from `REA-001-REVIEW.md`, but is
scheduled as a separate feature after its own REA/contract gates:

- Search behavior beyond the minimal model list.
- Interactive 3D rendering and renderer/package selection.
- Thumbnail generation.
- Geometry similarity and destructive duplicate resolution.
- Print history, analytics, cloud synchronization, and multi-user access.

Existing deferred research placeholders remain:

- REA-002: Search System
- REA-003: 3D Viewer
- REA-004: Duplicate Detection (exact SHA-256 capability; not part of the walking skeleton)
