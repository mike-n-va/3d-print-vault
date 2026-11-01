# DPV Repository Contract Ownership

**Status:** Contract definitions and architecture review complete
**Review date:** 2026-11-01
**Scope:** Ownership and dependency boundaries for `IArtistRepository`,
`ICollectionRepository`, `IModelRepository`, and `ITagRepository`
**Implementation:** Not authorized or included

## 1. Decision

Recommend that the four public repository contracts be owned by
`DPV.Domain`, with their concrete implementations owned by
`DPV.Infrastructure.Persistence`. `DPV.UI` consumes the technology-neutral
Domain contracts. `DPV.App` remains the composition root and maps each
contract to its Infrastructure implementation. `DPV.Tests` may reference all
production projects for validation.

This recommendation applies only to the four catalog repository abstractions
in scope. It does not create an application layer, authorize repository
methods or behavior, or change the approved project graph. It follows the
existing Domain `Contracts/` boundary and the approved rule that
Infrastructure depends inward on Domain. It is not a general decision that
Infrastructure owns business workflows.

## 2. Governing Evidence

The ownership assessment follows:

- [DPV-ARCH-001](DPV-ARCH-001.md), especially the approved project
  responsibilities, feature-flow boundary, and decisions ARCH-005/ARCH-008.
- [DPV-PROJECT-DEFINITIONS-001](DPV-PROJECT-DEFINITIONS-001.md), which
  assigns `Contracts/` to Domain and persistence/repositories to
  Infrastructure.
- [DPV-DEPENDENCY-MAP-001](DPV-DEPENDENCY-MAP-001.md), the approved project
  reference map.
- [Definition-008A](../definitions/Definition-008A-Sprint-Alignment-Governance.md),
  §5, which requires an owner-approved architecture-compliant replacement
  and criterion traceability for Issue #10.
- [Sprint-1-Governance-Decisions](../reviews/Sprint-1-Governance-Decisions.md),
  GOV-S1-018, approving replacement of Issue #10.
- [Commit-F-Owner-Decision-Package-008A](../reviews/Commit-F-Owner-Decision-Package-008A.md),
  recording the approved replacement and its constraints.

The project definitions already distinguish the public contract from the
implementation: Domain owns technology-neutral cross-project contracts;
Infrastructure owns persistence and repositories. This document applies
that boundary to the four named repository abstractions and records the
rationale. It does not amend DPV-ARCH-001 or authorize code.

## 3. Ownership Assessment

| Contract | Owning project | Implementation owner | Consumers | Why this ownership fits |
|---|---|---|---|---|
| [`IArtistRepository`](../../src/DPV.Domain/Contracts/Repositories/IArtistRepository.cs) | `DPV.Domain` | `DPV.Infrastructure` — Persistence | `DPV.UI` through Domain contracts; `DPV.App` only for composition; tests as needed | Artist is a catalog/domain concept. Infrastructure owns storage implementation; UI and App are not authorities for catalog contracts. |
| [`ICollectionRepository`](../../src/DPV.Domain/Contracts/Repositories/ICollectionRepository.cs) | `DPV.Domain` | `DPV.Infrastructure` — Persistence | `DPV.UI` through Domain contracts; `DPV.App` only for composition; tests as needed | Collection is a catalog/domain concept. Infrastructure owns storage implementation; UI and App are not authorities for catalog contracts. |
| [`IModelRepository`](../../src/DPV.Domain/Contracts/Repositories/IModelRepository.cs) | `DPV.Domain` | `DPV.Infrastructure` — Persistence | `DPV.UI` through Domain contracts; `DPV.App` only for composition; tests as needed | Model catalog metadata is a domain concept. Infrastructure owns storage implementation; UI and App must not own file-system or database details. |
| [`ITagRepository`](../../src/DPV.Domain/Contracts/Repositories/ITagRepository.cs) | `DPV.Domain` | `DPV.Infrastructure` — Persistence | `DPV.UI` through Domain contracts; `DPV.App` only for composition; tests as needed | Tag is a catalog/domain concept. Infrastructure owns storage implementation; UI and App are not authorities for catalog contracts. |

### Per-contract boundary review

| Contract | Ownership justification / why not another project | Dependency and use | Neutrality and architecture review |
|---|---|---|---|
| `IArtistRepository` | Artist is a Domain catalog concept; Infrastructure owns the storage implementation. UI is presentation and App is composition, so neither owns this abstraction. | `DPV.UI` consumes the Domain contract; `DPV.Infrastructure` implements it; `DPV.App` registers the mapping. | Empty marker interface has no external types. Domain ownership matches DPV-ARCH-001. |
| `ICollectionRepository` | Collection is a Domain catalog concept; Infrastructure owns the storage implementation. UI is presentation and App is composition, so neither owns this abstraction. | `DPV.UI` consumes the Domain contract; `DPV.Infrastructure` implements it; `DPV.App` registers the mapping. | Empty marker interface has no external types. Domain ownership matches DPV-ARCH-001. |
| `IModelRepository` | Model metadata is a Domain catalog concept; Infrastructure owns the storage implementation and file-system adapters. UI is presentation and App is composition, so neither owns this abstraction. | `DPV.UI` consumes the Domain contract; `DPV.Infrastructure` implements it; `DPV.App` registers the mapping. | Empty marker interface has no file-system, persistence, or other external types. Domain ownership matches DPV-ARCH-001. |
| `ITagRepository` | Tag is a Domain catalog concept; Infrastructure owns the storage implementation. UI is presentation and App is composition, so neither owns this abstraction. | `DPV.UI` consumes the Domain contract; `DPV.Infrastructure` implements it; `DPV.App` registers the mapping. | Empty marker interface has no external types. Domain ownership matches DPV-ARCH-001. |

These are intentionally minimal marker interfaces. The approved requirements
do not yet define Domain entity declarations, identity types, query semantics,
or repository operations. Adding CRUD or query signatures here would invent
feature behavior and types beyond this contract-only task. Define operation
signatures only after those Domain and feature decisions are approved; this
does not authorize repository implementation.

### Why not the other projects?

- **`DPV.Infrastructure` does not own the public contracts.** It owns
  persistence and repository implementations. Putting public contracts there
  would make consumers depend on the implementation project and would invert
  the approved inward dependency boundary.
- **`DPV.UI` does not own them.** UI owns presentation, navigation, and
  presentation state, not catalog rules or persistence abstractions. It is a
  consumer of Domain contracts, not their authority.
- **`DPV.App` does not own them.** App owns startup and composition. It
  selects and registers implementations; it must not become the owner of
  catalog business concepts or contracts.
- **`DPV.Application` is not an approved project.** Creating it or moving
  contracts into it would conflict with the approved solution shape and the
  owner-approved Issue #10 replacement.

## 4. Technology-Neutral Contract Requirements

Every repository contract exposed outside `DPV.Infrastructure` must be
expressible using Domain-owned types and framework-neutral language. A
contract must not require, return, accept, or expose:

- SQLite-specific or Dapper-specific types
- SQL text, commands, query fragments, or schema details
- `DbConnection` or another database connection/command implementation
- Infrastructure implementation classes or namespaces
- File handles, scanner implementations, or other persistence/adapter
  implementation details

The contract may use the relevant Domain entities and value objects, subject
to Domain purity. It must not import or reference `DPV.Infrastructure`,
database packages, UI frameworks, or filesystem APIs. Persistence-specific
translation belongs behind the Infrastructure implementation.

The four interfaces now exist under
`src/DPV.Domain/Contracts/Repositories/`. They have no method signatures or
external type dependencies. Direct source review confirms no SQLite, Dapper,
SQL, connection abstraction, or Infrastructure reference is exposed. The
neutrality requirement applies to any future members as well.

## 5. Dependency Direction Verification

### Contract and runtime direction

```text
DPV.UI
    → DPV.Domain repository contracts

DPV.Infrastructure
    → DPV.Domain contracts
    implements the repository contracts

DPV.App
    composition root
    → DPV.Infrastructure implementations
    → DPV.Domain contracts
    → DPV.UI

DPV.Tests
    → production projects for validation
```

The arrows above describe compile-time project references. “Implements” and
“consumes” describe the corresponding contract relationship: Infrastructure
implements Domain-owned abstractions, UI consumes those abstractions, and App
registers the implementation mapping at startup. The physical UI project
also has a direct reference to Infrastructure in the approved project map;
that existing reference is not permission for UI feature code to depend on
Infrastructure persistence types. Do not remove or add project references
under this ownership decision.

### Verified project references

The current `.csproj` files were compared with the allowed graph in
DPV-DEPENDENCY-MAP-001:

| Project | Actual project references | Review |
|---|---|---|
| `DPV.Domain` | None | Matches dependency-free Domain boundary. |
| `DPV.Infrastructure` | `DPV.Domain` | Matches inward dependency; implementation project depends on its contracts. |
| `DPV.UI` | `DPV.Domain`, `DPV.Infrastructure` | Both references are in the approved map. Repository abstraction usage is directed to Domain contracts. |
| `DPV.App` | `DPV.UI`, `DPV.Domain`, `DPV.Infrastructure` | Matches the approved composition-root references. |
| `DPV.Tests` | `DPV.App`, `DPV.UI`, `DPV.Domain`, `DPV.Infrastructure` | Matches the approved test references. |

`DPV.sln` contains the four production projects and `DPV.Tests`; no
`DPV.Application` project or project reference exists. Source inspection
also confirms the App project references Infrastructure and the App startup
composition registers project modules there.

The `DPV.Domain`, `DPV.Infrastructure`, and `DPV.UI` projects each build
successfully after the contract definitions were added. Static project-file
comparison confirms the approved graph, and source review confirms the
contracts have no forbidden references. The automated architecture test
suite could not be executed because building its project graph fails in the
pre-existing App startup source: `AppBuilder.WithDeveloperTools` is
unavailable at
[AvaloniaApplicationStartup.cs](../../src/DPV.App/Startup/AvaloniaApplicationStartup.cs#L27).
The App startup file is outside this task and remains unchanged. This
validation limitation is recorded separately from the successful
project-reference review and builds.

## 6. Issue Traceability

### Issue #10 — replaced by Issue #15

[Issue #10](https://github.com/mike-n-va/3d-print-vault/issues/10) requested
the four repository interfaces in a `DPV.Application` layer. That placement
conflicts with the approved project structure, which has no such project.
The owner's GOV-S1-018 decision approved replacing Issue #10. Issue #10
remains open until the replacement is verified; this document does not
change its GitHub state.

### Issue #10 criterion mapping

| Original Issue #10 requirement | Successor treatment | Evidence / rationale |
|---|---|---|
| Create `IArtistRepository`, `ICollectionRepository`, `IModelRepository`, and `ITagRepository` | **Completed as contract definitions only; implementation deferred.** Issue #15 defines ownership and compiles the four interfaces; it does not authorize repository implementation. | Ownership table in §3; interface files in `src/DPV.Domain/Contracts/Repositories/`; Issue #15 excludes implementation work. |
| All interfaces created and compile | **Completed.** The four Domain-owned interface declarations build successfully in `DPV.Domain`. Their operation signatures remain intentionally unspecified because entity identity and query semantics are open decisions. | `dotnet build src/DPV.Domain/DPV.Domain.csproj`; §4. |
| Place interfaces in the Application layer | **Changed by approved replacement.** Public contracts belong in `DPV.Domain`; implementations belong in `DPV.Infrastructure`. | DPV-ARCH-001, DPV-PROJECT-DEFINITIONS-001, GOV-S1-018, and §3 above. No `DPV.Application` project is created. |
| No implementations yet | **Retained and strengthened for Issue #15.** This issue authorizes no repository or persistence implementation. | Issue #15 scope and this document's implementation boundary. |

## 7. Issue #15 Completion Review

| Issue #15 criterion | Status | Rationale and evidence |
|---|---|---|
| **AC-01 — Architecture compliance** | **PASS** | `dotnet sln DPV.sln list` and the project-reference review confirm the four approved production projects plus `DPV.Tests`; no `DPV.Application` project or reference exists. |
| **AC-02 — Contract ownership defined** | **PASS** | §§1–3 assign all four contracts to `DPV.Domain`, implementation to `DPV.Infrastructure.Persistence`, and identify consumers and per-contract rationale. |
| **AC-03 — Technology-neutral contracts** | **PASS** | All four interfaces exist in Domain and are empty marker contracts. Source review confirms no SQLite, Dapper, SQL, connection, or Infrastructure types/references. §4 sets the neutrality requirement for future members. |
| **AC-04 — Dependency direction verified** | **PASS** | §5 compares every current `.csproj` reference with DPV-DEPENDENCY-MAP-001. Domain, Infrastructure, and UI builds passed. The unavailable automated test build is recorded separately in §8. |
| **AC-05 — Traceability preserved** | **PASS** | §6 links [Issue #10](https://github.com/mike-n-va/3d-print-vault/issues/10), successor [Issue #15](https://github.com/mike-n-va/3d-print-vault/issues/15), the replacement reason, and GOV-S1-018; it maps original criteria. Issue #10 was not modified. |
| **AC-06 — Sprint scope preserved** | **PASS** | Only four Domain contract boundaries were added. No project, layer, feature, milestone, architecture, persistence implementation, or Sprint scope was added or changed. |
| **AC-07 — Architecture review passes** | **PASS** | §§2–5 review each contract and the project graph against DPV-ARCH-001 and DPV-PROJECT-DEFINITIONS-001. Static reference checks and all three required project builds passed; no architecture deviation was found. |

## 8. Build and Architecture-Test Validation

| Validation | Result |
|---|---|
| `dotnet build src/DPV.Domain/DPV.Domain.csproj --no-restore` | **PASS** — 0 warnings, 0 errors. |
| `dotnet build src/DPV.Infrastructure/DPV.Infrastructure.csproj --no-restore` | **PASS** — 0 warnings, 0 errors. |
| `dotnet build src/DPV.UI/DPV.UI.csproj --no-restore` | **PASS** — 0 warnings, 0 errors. |
| `dotnet build src/DPV.App/DPV.App.csproj --no-restore` | **FAIL — pre-existing issue** at `AppBuilder.WithDeveloperTools` in [AvaloniaApplicationStartup.cs](../../src/DPV.App/Startup/AvaloniaApplicationStartup.cs#L27). App startup was not modified. |
| Automated architecture test project build | **BLOCKED** by the same App compile error; no test result is claimed. |

## 9. Architecture Review Findings

1. **Ownership:** Assigning the four public abstractions to Domain follows
   the approved Domain `Contracts/` boundary and the dependency-free core.
   Infrastructure remains the persistence/repository implementation owner.
2. **Consumers and composition:** UI consumes Domain contracts; App owns
   contract-to-implementation registration. The existing direct UI reference
   to Infrastructure is allowed by the dependency map but must not become a
   persistence API dependency in feature code.
3. **Technology neutrality:** All four interface declarations have no member
   signatures and no forbidden imports or references. The boundary prohibits
   SQLite, Dapper, SQL, connection, and Infrastructure types in future
   signatures as well.
4. **No topology change:** No `DPV.Application` project, layer, feature,
   project reference, or implementation is introduced by this decision.
5. **Verification limitation:** Domain, Infrastructure, and UI builds passed.
   The architecture test suite is blocked by the pre-existing
   `WithDeveloperTools` App compile error; this review does not fix that
   unrelated issue or claim that the tests passed.

### Final Issue #15 disposition

**PASS.**

The ownership definition, four Domain-owned contract declarations,
traceability, project-boundary review, neutrality assessment, and architecture
evidence satisfy Issue #15. The interfaces deliberately define no repository
operations because Domain entity identity and query semantics have not been
approved; this task creates contract boundaries only and adds no unapproved
behavior. All AC-01 through AC-07 are **PASS**. All three required project
builds passed. The App build and automated architecture test project remain
blocked by the separately documented pre-existing App startup compile
failure; this does not change the scoped contract build or static dependency
review results. No repository implementations, persistence code, project
references, architecture layers, or `DPV.Application` project were added.
Issue #10 remains open until its replacement is verified under the governance
process.
