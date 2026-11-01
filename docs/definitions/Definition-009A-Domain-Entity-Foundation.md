# Definition-009A
## Domain Entity Foundation Review
### Sprint 1 Issue #9 Resolution Package

**Review date:** 2026-11-01
**Status:** Evidence review complete; entity semantics and implementation remain unapproved
**Scope:** Artist, Collection, Model, Tag, and their repository contracts
**Implementation:** None authorized by this review

## 1. Objective and Conclusion

This review identifies the smallest domain foundation supported by existing
approved sources and separates it from decisions that remain open. It does
not approve new properties, identifiers, relationships, invariants,
repository operations, or persistence schema.

The existing sources establish four named Domain concepts and broad purposes:
Artist represents a creator; Collection represents a release or pack; Model
represents a printable asset; Tag is a classification mechanism. The
architecture assigns these concepts to `DPV.Domain`, which must remain free
of framework, UI, persistence, filesystem, and dependency-injection
dependencies.

Those statements do not define entity identity or implementable entity
contracts. `DPV-DATA-001` expressly leaves exact fields, nullability, and
relationships for later approval. The repository interfaces are empty
markers, and their operation semantics are explicitly deferred by
`DPV-REPOSITORY-CONTRACT-OWNERSHIP-001`.

**Recommended decision: D — Split Issue #9 into definition and
implementation work.** This document records the evidence and unknowns; it
does not itself supply the missing owner-approved entity definitions. First
approve a bounded entity contract, then implement the four entities and
their tests in a separately authorized implementation step. Issue #9 cannot
be marked PASS on the current evidence.

## 2. Governing Evidence

- [GitHub Issue #9](https://github.com/mike-n-va/3d-print-vault/issues/9)
  requires `Artist`, `Collection`, `Model`, and `Tag` entities, with no
  persistence logic and no UI dependencies. It specifies no fields,
  identifiers, relationships, or invariants.
- [DPV-ARCH-001](../architecture/DPV-ARCH-001.md) assigns catalog concepts
  and genuine domain invariants to Domain and prohibits framework, UI,
  persistence, filesystem, and DI dependencies there.
- [DPV-PROJECT-DEFINITIONS-001](../architecture/DPV-PROJECT-DEFINITIONS-001.md)
  names the four entities, gives their brief purposes, and assigns persistence
  and repositories to Infrastructure.
- [DPV-DATA-001](../database/DPV-DATA-001.md) identifies SQLite as catalog
  metadata authority, keeps model bytes outside SQLite, and states that exact
  fields, nullability, and relationships remain Commit B decisions. It also
  lists unresolved model/file identity and import semantics.
- [DPV-REPOSITORY-CONTRACT-OWNERSHIP-001](../architecture/DPV-REPOSITORY-CONTRACT-OWNERSHIP-001.md)
  assigns the four public contracts to Domain and implementation ownership
  to Infrastructure, while expressly deferring repository operations.
- [SPRINT-1-REMEDIATION-PLAN-001](../reviews/SPRINT-1-REMEDIATION-PLAN-001.md)
  says identity, required properties, and relationships must be approved
  before entity implementation.
- [SPRINT-1-ACCEPTANCE-EVIDENCE-REVIEW-001](../reviews/SPRINT-1-ACCEPTANCE-EVIDENCE-REVIEW-001.md)
  records the four missing entity declarations and separately assesses the
  no-persistence and no-UI-dependency constraints.

## 3. Entity Analysis

### Artist

#### Purpose

Represents a creator, according to
`DPV-PROJECT-DEFINITIONS-001`.

#### Known Requirements

- `Artist` is one of the four Domain entities required by Issue #9.
- Catalog concepts belong in `DPV.Domain`.
- Catalog metadata is within the SQLite catalog's authority, but the
  persisted fields are not defined.
- The data contract cautions against inferring Artist or Collection from
  folder names.

#### Explicitly Undefined

- Identifier type, generation, and stability.
- Name/display-name properties, normalization, uniqueness, and validation.
- Whether creator identity includes aliases or external references.
- Lifecycle, audit metadata, and deletion behavior.
- Whether Artist directly relates to Model, or only participates in the
  conceptual Artist → Collection → Model hierarchy.
- Relationship cardinality and ownership.

#### Prohibited Assumptions

- Do not derive or persist an Artist from a folder name without an approved
  rule.
- Do not choose an ID, uniqueness rule, required name, or lifecycle behavior.
- Do not add a direct Artist–Model association based only on the hierarchy.

### Collection

#### Purpose

Represents a release or pack, according to
`DPV-PROJECT-DEFINITIONS-001`.

#### Known Requirements

- `Collection` is one of the four Domain entities required by Issue #9.
- The approved data contract describes Artist → Collection → Model as the
  core conceptual hierarchy.
- Collection is a catalog concept; exact metadata is undecided.
- Folder names must not be used to infer Collection semantics without an
  approved rule.

#### Explicitly Undefined

- Identifier, name, uniqueness, and normalization.
- Whether “release” and “pack” are equivalent concepts or distinct types.
- Required properties, lifecycle, audit metadata, and deletion behavior.
- Whether Collection has a required Artist, optional Artist, or multiple
  Artists.
- Collection–Model cardinality, ownership, and navigation representation.

#### Prohibited Assumptions

- Do not interpret the conceptual hierarchy as a specific foreign key,
  cardinality, or navigation-property design.
- Do not assume one Artist per Collection, one Collection per Model, or
  that every Model must belong to a Collection.
- Do not map directory structure directly to Collection ownership.

### Model

#### Purpose

Represents a single printable asset, according to
`DPV-PROJECT-DEFINITIONS-001`.

#### Known Requirements

- `Model` is one of the four Domain entities required by Issue #9.
- The data contract places catalog metadata in SQLite and model file bytes
  at user-managed filesystem locations.
- A model's catalog record and its physical file location are distinct
  concepts and must not be conflated without an explicit decision.
- The data contract leaves open whether a discovered STL creates one Model
  record or an asset attached to a separately identified logical model.
- Path identity, content identity, rescan, move, duplicate, and inaccessible
  file behavior remain undecided.

#### Explicitly Undefined

- Whether Model means logical design, printable asset, file, or another
  explicitly bounded concept.
- Identifier strategy and relation to file/root identity.
- Required metadata, file-location representation, and validation.
- Artist and Collection associations and their cardinalities.
- Tag association and its cardinality.
- Rescan, relocation, duplicate, missing-file, and deletion semantics.
- Lifecycle and audit metadata.

#### Prohibited Assumptions

- Do not store model file bytes in the database.
- Do not equate a Model record with a file path, file hash, or logical design
  without an approved identity contract.
- Do not infer Artist or Collection from a path.
- Do not collapse byte-identical files, delete source files, or choose
  rescan/move behavior implicitly.

### Tag

#### Purpose

Provides a classification mechanism, according to
`DPV-PROJECT-DEFINITIONS-001`.

#### Known Requirements

- `Tag` is one of the four Domain entities required by Issue #9.
- It is a Domain concept and must have no UI or persistence dependencies.

#### Explicitly Undefined

- Identifier, label/name rules, normalization, uniqueness, and scope.
- Whether tags classify Models, Collections, Artists, or multiple entity
  types.
- Tag–Model cardinality and whether tags are shared or owned.
- Lifecycle, audit metadata, deletion, and merge behavior.

#### Prohibited Assumptions

- Do not assume Tag–Model is many-to-many or add a join entity/table.
- Do not define tag scope, casing, uniqueness, or automatic tagging.
- Do not introduce search/filter operations as an entity requirement.

## 4. Identity Review

| Candidate strategy | Approved by reviewed sources? | Finding |
|---|---|---|
| GUID | No | Unresolved; do not select or generate one. |
| Integer ID | No | Unresolved; do not select one. |
| String key | No | Unresolved; do not select one. |
| Composite key | No | Unresolved; do not select one. |

`DPV-DATA-001` discusses stable source-root and file-location identity as
future import decisions. It does not approve entity primary-key strategy.
Path identity and content hashes address different file/import questions and
must not be treated as approved entity IDs.

No identity strategy is selected in this review.

## 5. Relationship Review

| Relationship | Evidence in approved sources | Decision |
|---|---|---|
| Artist ↔ Collection | `DPV-DATA-001` names Artist → Collection → Model as the core hierarchy. This is conceptual hierarchy evidence only. | The conceptual ordering is supported; direct association, cardinality, ownership, and persistence representation remain unresolved. |
| Collection ↔ Model | Same conceptual hierarchy provides evidence of sequence, not a fully specified association. | Direct association, cardinality, optionality, ownership, and navigation representation remain unresolved. |
| Artist ↔ Model | The hierarchy places Collection between Artist and Model; no direct Artist–Model association is stated. | Unresolved; do not add a direct relationship. |
| Tag ↔ Model | No approved source defines a Tag–Model association. | Unresolved; do not add a relationship. |

No foreign keys, navigation properties, join entities, or relationship
invariants are approved by this review.

## 6. Repository Operation Review

The current Domain contracts are:

- [`IArtistRepository`](../../src/DPV.Domain/Contracts/Repositories/IArtistRepository.cs)
- [`ICollectionRepository`](../../src/DPV.Domain/Contracts/Repositories/ICollectionRepository.cs)
- [`IModelRepository`](../../src/DPV.Domain/Contracts/Repositories/IModelRepository.cs)
- [`ITagRepository`](../../src/DPV.Domain/Contracts/Repositories/ITagRepository.cs)

Each is currently an empty marker interface. The ownership document states
that operation signatures remain deferred until entity identity and query
semantics are approved.

| Operation | Approved for any of the four contracts? |
|---|---|
| Create | No |
| Update | No |
| Delete | No |
| GetById | No; identity is undefined. |
| Search | No; query and search semantics are undefined. |
| List | No; filtering, ordering, and paging semantics are undefined. |

Do not add repository methods, Infrastructure repository classes, SQL, or
catalog tables under this definition review.

## 7. Sprint 1 Minimum Model

The minimum foundation that can be stated without adding requirements is:

1. Four distinct Domain concepts named `Artist`, `Collection`, `Model`, and
   `Tag`.
2. The documented broad purposes: creator, release or pack, printable asset,
   and classification mechanism, respectively.
3. Ownership by `DPV.Domain`, with no UI, framework, persistence, filesystem,
   or DI dependencies.
4. No catalog persistence schema or repository behavior inferred from those
   names.
5. The conceptual Artist → Collection → Model hierarchy recorded as
   source-supported, while leaving its entity associations and cardinalities
   undecided.

This is a vocabulary and boundary foundation, not an implementable entity
contract. It does not establish properties, identity, constructors,
invariants, or relationship types. Creating empty classes solely to satisfy
the names would not resolve those omissions or provide evidence of usable
domain entities.

No implementation assumptions are approved in this section.

## 8. Issue #9 Acceptance Criteria Review

| Issue #9 criterion | Result | Evidence and rationale |
|---|---|---|
| Artist entity created | **FAIL** | The Domain contains no `Artist` entity declaration. Issue #9 names the required entity but provides no implementable definition. |
| Collection entity created | **FAIL** | The Domain contains no `Collection` entity declaration. Required fields and relationship semantics are unapproved. |
| Model entity created | **FAIL** | The Domain contains no `Model` entity declaration. The distinction between logical model, printable asset, and file remains open in `DPV-DATA-001`. |
| Tag entity created | **FAIL** | The Domain contains no `Tag` entity declaration or approved association/scope. |
| No persistence logic | **PASS (limited)** | Domain remains free of persistence implementation. This negative constraint is satisfied, but does not offset the four missing entity declarations. |
| No UI dependencies | **PASS (limited)** | Domain has no UI dependency; the architecture boundary and existing architecture tests support this. This negative constraint does not offset the missing entities. |

**Issue #9 disposition:** **FAIL / blocked on approved entity definitions.**
The four positive creation criteria remain unmet. This document does not
change the GitHub issue state or authorize implementation.

## 9. Recommended Path Forward

Proceed with decision **D — split definition and implementation work**:

1. Treat this document as the evidence inventory and unresolved-decision
   register; do not treat it as approval of an ID, field, relationship, or
   repository operation.
2. Obtain explicit owner approval for only the minimum entity contract
   needed by Issue #9: entity distinctions, required properties, identity,
   and any required relationships/invariants. Keep optional behavior out of
   the Sprint 1 minimum.
3. Reconcile any approved Model/file identity or relationship decisions with
   the open Commit B decisions in `DPV-DATA-001`.
4. Authorize a separate, bounded implementation step for the four Domain
   declarations and tests. Preserve Domain purity and keep schema, SQL,
   migrations, and repository implementations outside Domain.
5. Reassess each Issue #9 criterion against source and test evidence. Do not
   mark the issue PASS until all four declarations exist and the two
   architectural constraints remain verified.

This review creates no entities, repository methods, schema, migrations,
GitHub changes, or Sprint scope changes.
