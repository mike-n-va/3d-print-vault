# REA-001 — Solution Architecture

**Status:** Research complete  
**Decision:** **APPROVE WITH CHANGES**  
**Research date:** 2026-10-28  
**Scope:** Architecture suitability before implementation; no source code or project files generated.

## 1. Executive Summary

3D Print Vault is pointed in the right direction: a local-first desktop application, a relational catalog, an Avalonia/MVVM presentation layer, and a single-process modular monolith are a sound starting point for managing a large personal 3D-model library. SQLite is appropriate for the catalog and FTS5 is a credible initial text-search engine. The proposed scale of 250,000+ models does not by itself require a server database or a dedicated search service.

The proposed **12-project MVP solution is over-partitioned**. Several projects name features or infrastructure technologies rather than stable, independently useful boundaries. This makes dependency direction, testing, and AI-assisted changes harder without creating a real deployment or ownership benefit. Start with a small set of explicit layers and keep feature modules inside them; extract a feature into its own project only when a concrete dependency, testing, reuse, or team boundary justifies it.

**Before implementation, change or confirm these decisions:**

1. **Move from .NET 9 to .NET 10 LTS.** As of this research date, Microsoft lists .NET 9 end of support as November 10, 2026, less than two weeks away; .NET 10 LTS is supported through November 14, 2028.
2. **Reduce the MVP to four core projects:** `DPV.App` (including UI), `DPV.Domain`, `DPV.Application`, and `DPV.Infrastructure`. Keep `DPV.Viewer` separate only if a short compatibility proof shows that isolating its renderer/GPU dependencies is useful. Treat the remaining proposed projects as feature areas within these projects, not separate assemblies yet.
3. **Prove the Avalonia + Helix Toolkit + target-OS combination early.** The Helix Toolkit repository lists an Avalonia SharpDX implementation, but the same repository describes its SharpDX engine as DirectX 11-based. Avalonia being cross-platform does not establish that this particular rendering path is portable to every intended OS.
4. **Keep model files outside SQLite.** Store catalog records, metadata, paths, and derived index data in SQLite; stream large model files from their managed locations. Define how missing, moved, removable, and network-hosted roots are represented.
5. **Use SQLite FTS5 first and benchmark the real search workload.** There is no defensible universal model-count threshold at which a separate engine becomes necessary. Add one only for unmet measured latency or capabilities, while accounting for the consistency and packaging burden of a second index.
6. **Choose one default data-access approach.** For this metadata-oriented application, use EF Core for ordinary catalog reads/writes and migrations, with targeted SQL for FTS5 and bulk ingestion. Do not add Dapper throughout the application “for performance”; benchmark first and keep any specialized SQL path narrow.

The strongest design is a **modular monolith** for runtime/deployment, using **Clean Architecture dependency direction** for the core layers and **vertical slices** for application use cases. Apply DDD selectively to actual catalog and file-identity rules; do not build aggregates and abstractions around simple CRUD.

### Direct answers

| Question | Finding |
|---|---|
| Is the proposed structure appropriate for an MVP? | **Not as 12 projects.** Keep the core boundaries; merge the feature-specific assemblies into cohesive modules and defer analytics. |
| Is Avalonia + MVVM appropriate? | **Yes**, with UI-specific code kept out of domain/application logic and long-running I/O kept off the UI thread. |
| Is SQLite FTS5 sufficient? | **A good MVP choice** for metadata search at the stated catalog size; prove it with a representative benchmark and explicit search requirements. |
| Dapper or EF Core? | **EF Core as the default** for catalog data, with explicit SQL where needed. Choose Dapper instead only if the team deliberately owns SQL, schema migrations, and mapping end-to-end. |
| Should `DPV.Viewer` be separate? | **Conditionally.** It has real value if it contains a renderer integration boundary or isolates native/GPU dependencies; it is not valuable merely as a name for viewer UI code. |
| Are we building the right architecture? | **Yes, with changes** to project count, runtime version, viewer compatibility proof, and file/index lifecycle decisions. |

## 2. Findings

### 2.1 Architecture shape

Use the following concepts together rather than treating them as competing choices:

- **Modular monolith:** one desktop application and one local data store, with explicit feature/module boundaries. It matches offline operation and avoids distributed services that add deployment, failure, and data-consistency costs.
- **Clean Architecture dependency rule:** keep domain rules independent of UI, persistence, and rendering frameworks. Application use cases depend inward on domain concepts; infrastructure implements application-defined ports.
- **Vertical slices inside the application layer:** organize use cases such as “scan a root,” “search models,” and “open a model” by feature/request, keeping related validation, orchestration, and tests close. This reduces generic service/repository ceremony without erasing the outer dependency boundaries.
- **Selective DDD:** use a shared catalog vocabulary and model invariants where they exist. A `Model` can describe a catalog entry, while one or more physical files/variants describe stored assets. Avoid forcing every table or CRUD operation into a rich aggregate.

This is deliberately less elaborate than a production modular-monolith reference application. That is a feature: the project is a desktop MVP, not a large business system with separate teams and independently deployed services.

### 2.2 Avalonia and MVVM

Avalonia’s current MVVM guidance describes separate Views, ViewModels, and Models, one-way dependency toward the UI, data-bound view state, and testable POCO ViewModels. Its official samples are deliberately small and independently runnable, while a solution can bring multiple samples together. These are useful patterns and learning references; they do not prescribe one project per screen or feature.

For DPV:

- Keep Views mostly declarative and limit code-behind to view-specific behavior that genuinely requires it.
- Put presentation state and commands in ViewModels, but keep catalog rules, file scanning, database access, and mesh processing out of them.
- Use an explicit composition root for dependency registration and application startup.
- Run filesystem scans, hashing, thumbnail rendering, and large queries asynchronously; report progress/cancellation and marshal only UI updates to the UI thread.
- Use virtualization and incremental/paged loading for large model grids. Avoid materializing the full 250,000-row catalog or full-size thumbnail set into UI-bound collections.
- Avoid global service locators, static mutable application state, database calls in Views/ViewModels, and one “main” ViewModel that owns unrelated features.

For a long-lived project or team, stable dependency rules and feature ownership matter more than maximizing assembly count. Keep UI navigation, shared themes, and common controls cohesive. Split a UI library only when there is a second host, a meaningful independent UI surface, or a compile-time boundary that can be enforced.

### 2.3 SQLite and FTS5

SQLite is designed for local application storage, including desktop cataloging/media-management use cases. Its WAL mode allows readers and a writer to proceed concurrently, but it does not make SQLite a multi-writer database. WAL requires processes to share the same host and is not suitable for a database placed on a network filesystem. SQLite should therefore hold DPV’s catalog, not be treated as a shared database for multiple PCs or as the storage container for multi-terabyte model files.

FTS5 provides full-text indexing, query matching, ranking, configurable tokenization, and support for an index over content stored elsewhere. It is a reasonable starting point for names, descriptions, tags, artist/collection metadata, and other text. It is not a geometry search engine and should not index binary mesh contents.

The meaningful search decision is based on the workload, not just row count. Measure representative search and filter requests against a generated or anonymized catalog at and beyond the target size. Include index-build time, update cost, database/index growth, cold and warm query latency, and UI result rendering. Keep structured filters in normal indexed columns; use FTS5 for text terms and join its matches to catalog rows.

Keep FTS state maintainable:

- Define which catalog fields are indexed and how edits/deletes/imports update the FTS index.
- If using an external-content table, make index synchronization and rebuild behavior explicit and test it.
- Safely construct/escape FTS query expressions; parameterization of SQL alone does not remove FTS query-language semantics.
- Provide a recoverable index rebuild path. The searchable index is derived data, not the authoritative catalog.

Do not add Elasticsearch, OpenSearch, Meilisearch, or another engine merely because the library contains 250,000 models. A separate local engine adds a second process or embedded dependency, a second index lifecycle, more disk use, consistency/rebuild behavior, and packaging/support work. Revisit it if FTS5 cannot satisfy measured latency or product requirements such as richer typo tolerance, advanced language analysis, or sophisticated ranking.

### 2.4 Data access: Dapper and EF Core

Neither library removes the need to design SQLite indexes, pagination, transactions, schema upgrades, or FTS5 synchronization. EF Core’s own performance guidance emphasizes query plans and indexes as the main drivers of query speed; ORM choice alone is not a scale strategy.

| Concern | Dapper | EF Core |
|---|---|---|
| Programming model | Thin object mapper over explicit SQL; the application owns most SQL and mapping decisions. | ORM with LINQ queries, entity mapping, change tracking, and schema migration tooling. |
| Strength for DPV | Direct control over FTS5, specialized filters, and ingestion SQL; low abstraction overhead. | Productive catalog CRUD and typed mappings; migrations and relationships are easier to keep consistent for a small team. |
| Cost/risk for DPV | More handwritten SQL/mapping and a separately selected migration mechanism; repetitive patterns can drift across feature code. | Provider-specific query/migration limitations; tracking should be disabled for read-only lists and bulk paths may need explicit SQL. |
| Performance | Often attractive for tight, SQL-centric paths; still subject to SQLite locking, indexes, query shape, and file I/O. | Good enough for ordinary metadata operations when queries are projected, bounded, and measured; unnecessary tracking/materialization can hurt. |

**Recommendation:** adopt EF Core as the sole default catalog ORM, target the SQLite provider, and use explicit SQL for FTS5 DDL/queries and measured bulk-ingest hot paths. Keep those SQL operations behind the infrastructure boundary. Do not add Dapper as a second general-purpose persistence stack. If the team has strong SQL-first experience and values hand-authored schema/query control over ORM tooling, Dapper remains a defensible alternative—but then define and test the migration/versioning approach before implementation.

SQLite has provider-specific EF Core limitations, including unsupported schema concepts and table rebuilds for some migrations. Treat migrations as application-owned, versioned artifacts, test upgrades from real prior schema versions, and avoid assuming every database migration is a simple `ALTER TABLE`.

### 2.5 Viewer boundary

A viewer boundary has more potential value than a general feature-project split because 3D rendering introduces specialized controls, GPU/backend choices, mesh-loading constraints, and potentially native/platform-specific dependencies. The Helix Toolkit repository lists an Avalonia/SharpDX component, alongside WPF and WinUI components, and describes its SharpDX renderer as a DirectX 11 engine. That is evidence of an Avalonia integration path, not evidence that the exact package/version selected is compatible with every intended operating system or that it meets DPV’s STL scale/performance needs.

Keep viewer-specific rendering/adaptation isolated from the catalog and application rules. If the renderer is a distinct, testable assembly with controlled references, a `DPV.Viewer` project is justified. Keep navigation, selection state, and ordinary screen composition in the app/UI layer. If the first implementation is only an Avalonia control hosted by one screen and has no independent dependency boundary, keep it as a feature module instead of creating an empty project.

Before deciding the final project boundary, prove:

1. Supported OS and GPU requirements, including headless/remote/virtualized environments if relevant.
2. The exact Helix package, version, license, target frameworks, and Avalonia version combination.
3. Loading, rendering, cancellation, disposal, and responsiveness for representative large STL files.
4. Whether thumbnail rendering can reuse the viewer pipeline safely or needs a separate headless/off-screen strategy.

### 2.6 AI-assisted development

Cursor, Antigravity, and Claude Code all document repository-scoped or path-scoped persistent instructions (including AGENTS.md-compatible workflows in their current documentation). Their mechanisms differ, so architecture should not depend on one vendor-specific convention. Keep one canonical project standard and use concise tool-specific pointers only where necessary.

Patterns that reduce AI-generated technical debt:

- Keep a short, authoritative architecture decision record and a current project/dependency map; remove or reconcile stale competing plans.
- Make dependency rules mechanically testable (Domain must not reference Avalonia, EF Core, Helix, or Infrastructure, for example).
- Use small feature-sized changes with clear acceptance criteria and targeted tests; avoid broad “implement the whole system” prompts.
- Organize feature code so the request, validation, orchestration, persistence adapter, and tests are discoverable together without duplicating core rules.
- Require DB migration tests, FTS synchronization tests, filesystem edge-case tests, and an actual viewer smoke test for relevant changes.
- Keep error paths, cancellation, progress reporting, and idempotency explicit. AI-generated success-shaped fallbacks and silent exceptions are particularly damaging in import/scanning flows.
- Keep code generation bounded by stable contracts and examples. Prefer fewer, well-named abstractions over a generic repository/service layer mechanically repeated across every feature.
- Treat AI instructions as guidance, not enforcement. Use builds, tests, analyzers, and architectural checks as gates.

AI suitability is not achieved by making every feature a project or by creating a large prompt file. Clear module ownership, enforced dependency rules, concrete tests, and a single source of truth produce more predictable changes across coding agents.

## 3. Existing Solutions Analysis

The references below were compared as public product documentation or public source repositories. For proprietary products, conclusions are limited to advertised behavior; no claim is made about their internal architecture.

| Reference | What it demonstrates for DPV | Limit / caution |
|---|---|---|
| **Avalonia Samples** | Feature-focused examples, including MVVM, commands, validation, and desktop integration; examples are independently runnable and can be collected in one solution. Useful for learning framework conventions and building small integration spikes. | Samples do not prescribe DPV’s production solution structure or justify one assembly per sample/feature. |
| **Calibre** | A mature cross-platform personal library manager with cataloging, metadata, viewing, conversion, and device workflows. It is a close reference for library organization, user-edited metadata, import, and “the user’s library is local” expectations. | It manages e-books with different metadata/file conventions and a long-established ecosystem. Do not copy its data layout or infer its internals from its user-facing docs. |
| **LaunchBox** | Collection-oriented UX such as custom fields and saved filters illustrates the value of user-defined metadata and reusable collection views. | Proprietary product; public feature pages do not establish its architecture or implementation details. |
| **Plex** | Publicly describes library scanning/organization and multiple client experiences. Useful as a UX reference for scanning progress, metadata presentation, and browsing a large media collection. | Server/client streaming and account/service behaviors differ from a local-first offline desktop catalog. Do not import its deployment shape into DPV. |
| **Jellyfin** | Open-source media-server example for library management and multiple client projects; demonstrates how a media catalog can be separated from playback clients when that product model requires it. | It is explicitly a dedicated server and client ecosystem, not a small desktop/local catalog. Its split is not a reason to create a DPV server or many projects. |
| **Komga** | Public feature set includes libraries/series/books, metadata editing/import, collections/read lists, a web reader, and duplicate file/page detection. It is a particularly useful reference for scanner/library workflows and treating duplicate files separately from duplicate content/pages. | It is a multi-user media server with REST/API and web-reader requirements, substantially broader than the DPV MVP. |
| **Lychee Slicer** | Resin-print preparation workflow is a reference for how a 3D model is inspected before slicing/printing. | Proprietary product; publicly available product material does not expose implementation architecture. It is not a catalog architecture precedent. |
| **CHITUBOX** | Resin slicer workflow and model preview/print-preparation are useful product-context references for model usability and viewer expectations. | Proprietary product; internal layering, storage, and scaling decisions are not established by its product page. |
| **PrusaSlicer** | Open-source desktop 3D model processing, editing, and preview; its public repository documents a native C++/wxWidgets/OpenGL-oriented stack and a CLI. Useful for 3D workflow and performance observations, not for copying technology choices. | A slicer has computation and device-output needs DPV does not. Its codebase is a poor size/architecture target for an MVP. |
| **MeshLab** | Open-source, cross-platform processing and editing of large triangular meshes, with an extensible processing focus. Useful for representative mesh and rendering stress cases. | It is a mesh-processing/editor product, not a searchable artist/collection asset manager. |
| **Clean Architecture references** | Reinforce the dependency rule: presentation and infrastructure can depend inward; domain rules should not depend on framework/persistence details. | Microsoft’s cited architecture material is primarily web-oriented. Transfer the dependency principle, not its entire web/API stack. |
| **Modular Monolith reference** | Shows explicit module boundaries, internal communication, architecture tests, integration tests, and decision records within a single deployable system. | The cited sample is intentionally comprehensive and includes more enterprise patterns than DPV needs. Avoid copying its scale wholesale. |
| **Vertical Slice Architecture** | Encourages grouping code around use cases and reducing unnecessary cross-feature shared abstractions. Fits DPV’s import/search/viewer operations. | It is a design approach, not a mandate to erase all layers or duplicate every shared rule. Retain the outer dependency boundaries. |

Overall, successful collection products validate the **user-facing needs**—reliable scanning, editable metadata, saved filters/collections, fast browsing, previews, and graceful missing-file handling. They do not prove that DPV should mimic their server architectures or internal code organization.

## 4. Strengths

- **Local-first and offline operation** are consistent with SQLite’s strengths as embedded application storage.
- The **Artist → Collection → Model** hierarchy gives the product a discoverable domain vocabulary and a basis for navigation and filtering.
- **Avalonia + MVVM** is a sound cross-platform desktop direction when ViewModels remain testable and application logic is independent of UI.
- **SQLite + FTS5** keeps the initial architecture simple, self-contained, and capable of structured queries plus full-text metadata search.
- The proposal recognizes **separate product concerns** (import, search, viewer, thumbnails, duplicates) early, which can later become modular boundaries if code ownership and dependency needs justify them.
- A **single-user/single-process deployment** avoids premature network services and supports fully offline workflows.
- The development standard emphasizes research, reviewable artifacts, and verification before implementation—good controls for AI-assisted work as long as the documents remain consistent and enforceable.

## 5. Weaknesses

- **Project names currently overstate the need for assembly boundaries.** `DPV.Import`, `DPV.Search`, `DPV.Thumbnailing`, `DPV.Analytics`, and `DPV.Duplicates` can begin as use-case modules without separate projects.
- `DPV.Persistence` duplicates the scope of `DPV.Infrastructure` unless it has an actual independent implementation boundary, such as a separately reusable storage adapter.
- `DPV.App` and `DPV.UI` may introduce unnecessary Avalonia resource, styling, and composition complexity when there is one executable and one UI. The UI can initially live in the app project.
- **The runtime choice is about to age out of support.** .NET 9 is STS and has a November 10, 2026 support end date; the project is planning phase and should start on .NET 10 LTS instead.
- **The target OS list is missing.** The proposed 3D stack cannot be assessed as cross-platform until exact Helix/backend and operating-system combinations are verified.
- **“250,000 models” is not a performance contract.** It does not define searchable fields, result-set behavior, thumbnail dimensions/cache, concurrent work, accepted latency, import duration, or target hardware.
- **The data boundary between catalog and files is underspecified.** Multi-terabyte libraries require durable path/root semantics, offline/missing-path states, restartable scans, backups, and safe index rebuilds—not just a `Model` table.
- **Dapper is listed without an explicit SQL/migration ownership strategy.** It is viable, but every module must consistently handle schema changes, query parameters, mapping, and transaction boundaries.
- The high-level hierarchy does not yet distinguish **catalog identity, physical file, format variant, and duplicate equivalence**. A single model can have multiple assets; exact-byte duplicate and geometry-equivalent duplicate are not the same claim.
- Analytics and advanced duplicate detection broaden scope before the core catalog, scanning, persistence, and viewer workflows are proven.

## 6. Risks

| # | Severity | Risk | Why it matters | Mitigation |
|---:|---|---|---|---|
| 1 | **High** | .NET 9 reaches end of support on 2026-11-10. | Starting implementation on a runtime with imminent support expiry creates avoidable patching and upgrade work and may constrain dependency compatibility. | Change the target to .NET 10 LTS before the first implementation phase; verify Avalonia, EF Core, and Helix package target-framework support. |
| 2 | **High** | Avalonia/Helix renderer and supported OS/GPU combination is unproven. | A UI framework’s portability does not guarantee that the selected DirectX-based rendering backend runs on all target platforms or deployment environments. | Decide supported OSes; pin candidate package versions; build a minimal rendering spike and record backend/GPU/disposal requirements before committing to the viewer architecture. |
| 3 | **High** | Large-catalog browsing can exhaust memory or stall the UI. | Loading hundreds of thousands of rows, thumbnails, or mesh previews into memory causes poor startup, scrolling, and responsiveness. | Use paging/virtualization, bounded thumbnail caches, asynchronous loading, cancellation, and a 250K+ benchmark on reference hardware. Measure cold/warm search and first-page rendering. |
| 4 | **High** | Catalog metadata and external files can become inconsistent. | Files may move, be removed, live on a removable volume, or be renamed while a scan/import is active; a DB transaction cannot atomically commit filesystem moves. | Model library roots and asset paths explicitly; track availability and last scan state; make scans restartable/idempotent; avoid destructive file operations by default; test interrupted scans and moved roots. |
| 5 | **High** | Large imports create write contention, unbounded work, or unrecoverable partial state. | SQLite has serialized writes; hashing and thumbnailing compete for CPU/I/O; an import may be interrupted after some catalog/index updates. | Use bounded producer/consumer work, explicit cancellation/progress, small transactional batches, a durable scan checkpoint, and a single coordinated DB-write path. Keep file parsing and hashing streamed. |
| 6 | **High** | Backup/restore and WAL handling are not defined. | A user may lose catalog edits, scan state, or indexes; copying only the main DB file while WAL is active can produce an inconsistent backup. | Specify the backup scope (catalog/settings vs model files), use SQLite’s backup mechanism or a quiesced checkpointed procedure, restore-test it, and treat FTS as rebuildable derived data. |
| 7 | **Medium** | Search feature expectations exceed FTS5 or the index drifts from catalog truth. | FTS5 handles text search, not geometric similarity; trigger/index maintenance errors can cause missing or stale results. | Define search fields and behavior; test insert/update/delete/rebuild; keep FTS derived; benchmark using realistic metadata; add a separate engine only for demonstrated requirements or unmet latency. |
| 8 | **Medium** | Too many projects and abstractions amplify AI-generated drift. | Broad dependency graphs, duplicated service/repository patterns, and unclear ownership make edits span many files and weaken consistency. | Begin with four core projects; organize application code by feature; enforce dependency rules with architecture tests; keep one canonical architecture source and focused AI instructions. |
| 9 | **Medium** | Domain identity and duplicate semantics are ambiguous. | A model entry, a path, a file variant, and a byte-identical copy may be conflated, producing incorrect collections, destructive deduplication, or misleading search results. | Define domain terms before schema implementation; distinguish catalog model from physical asset; identify exact-hash duplicates separately from geometry similarity; keep duplicate detection advisory and non-destructive initially. |
| 10 | **Medium** | MVP scope expands into analytics and advanced duplicate detection before the catalog proves itself. | More modules, background jobs, metrics, and workflows delay the primary value: reliable import, organization, search, and viewing. | Define a narrow MVP acceptance boundary; defer standalone analytics and similarity detection; retain only the simplest exact-hash capability if required by initial user workflows. |

SQLite adds a specific operational constraint to risks 4–6: WAL is designed for processes on the same host and does not work over a network filesystem. Keep the **database file local**, even if cataloged model roots are removable or network-mounted; document that distinction and test path availability behavior.

## 7. Recommendations

1. **Adopt a modular-monolith design, not microservices.** One local desktop process and one authoritative catalog DB match offline operation and the current product boundary.
2. **Combine modular monolith + clean dependency direction + vertical slices.** Keep Domain and Application independent of UI/storage/rendering; implement use cases by feature within Application; keep Infrastructure adapters behind defined ports.
3. **Reduce the starting project count.** Use four core projects and add `DPV.Viewer` only when a renderer boundary is proven useful. Do not make a project per feature.
4. **Move to .NET 10 LTS now.** Align all package versions and CI/build targets with the chosen Avalonia and viewer stack.
5. **Use SQLite as the local catalog, not as the model-file container.** Store file paths and computed metadata; read mesh files through streaming/bounded pipelines.
6. **Use FTS5 as the initial full-text engine.** Define a representative catalog benchmark (250K+ records and realistic searchable metadata), latency and memory targets, and a rebuild operation. Reassess from measurements rather than a guessed row threshold.
7. **Choose EF Core as the default persistence stack.** Use no-tracking projections/paged queries for browsing; use raw SQL for FTS5 and explicitly measured import paths. Keep one versioned migration story.
8. **Set performance and operational contracts before implementation.** Define target hardware/OS, cold startup, search p95, first page/display latency, thumbnail cache limits, scan throughput, cancellation, backup/restore, and missing-root behavior.
9. **Define asset identity and path semantics.** Decide whether a catalog `Model` can contain multiple physical assets/formats, how filenames and root IDs are tracked, how moved libraries are reconciled, and which duplicate equivalence types exist.
10. **Protect UI responsiveness and resource ownership.** Specify bounded concurrency and cancellation for scans, imports, thumbnails, preview loads, and hashing; ensure GPU resources are disposed when views/models are closed.
11. **Use DDD where rules warrant it.** Make the artist/collection/model vocabulary consistent and protect real invariants; do not invent aggregates for every table.
12. **Turn AI guidance into enforceable feedback.** Keep authoritative instructions concise; add architecture checks and targeted tests. Require human review of generated SQL/migrations and import/destructive-file behavior.

### Search-engine upgrade criteria

Do not define the switch in terms of “N models.” Re-evaluate a dedicated search engine only when one or more of these conditions remain true after indexing/query/UI tuning:

- FTS5 cannot meet an agreed p95 latency or index-build/update budget on the documented reference dataset and hardware.
- Required relevance, typo-tolerance, stemming/language analysis, faceting, or hybrid/vector search cannot be implemented acceptably with SQLite indexes and FTS5.
- Search has evolved into an independently scaled, multi-user/server feature—outside the current local-first desktop scope.

Before adoption, compare its deployment model, offline behavior, memory/disk cost, index recovery, transactional consistency, upgrade path, and support burden against the measured requirement.

## 8. Proposed Revisions To Current Architecture

### 8.1 Recommended MVP project structure

| Project / proposal | MVP decision | Responsibility / reason |
|---|---|---|
| `DPV.App` | **Keep** | Executable, startup/composition root, app settings, navigation shell, and initially the Avalonia UI. |
| `DPV.UI` | **Merge into `DPV.App` initially** | A separate UI assembly is not justified for one desktop host. Revisit if a second host or independently reusable UI surface appears. |
| `DPV.Domain` | **Keep separate** | Catalog concepts, identity/value types, and genuine invariants; no Avalonia, EF Core, SQLite, or Helix references. |
| `DPV.Application` | **Keep separate** | Use cases, validation/orchestration, and ports for catalog, filesystem, search, and viewer capabilities. Organize by feature/vertical slice. |
| `DPV.Infrastructure` | **Keep separate** | Implement persistence, filesystem scanning, hashing, metadata extraction, thumbnailing, and FTS5. Keep these as cohesive folders/modules internally. |
| `DPV.Persistence` | **Merge into `DPV.Infrastructure`** | It is not a separate deployable or independently reusable boundary for the MVP. Keep schema, EF mappings, migrations, and FTS5 implementation together. |
| `DPV.Import` | **Merge as a feature module** | Import/scanning use cases belong in Application; file walking and parsing adapters belong in Infrastructure. Extract only if a real independent extension/API boundary emerges. |
| `DPV.Search` | **Merge as a feature module** | Search contract/use case in Application; SQLite indexes and FTS5 queries in Infrastructure. A separate assembly is premature before a second search backend exists. |
| `DPV.Viewer` | **Conditional separate project** | Keep separate if it isolates the Helix/rendering backend and platform dependencies behind a narrow adapter. Otherwise keep the first control/screen in the app and do not create an empty abstraction project. Prove exact target OS/package support first. |
| `DPV.Thumbnailing` | **Merge into Infrastructure** | A bounded background capability, not a separate MVP assembly. Extract if the renderer pipeline becomes a separately deployable/reusable component. |
| `DPV.Analytics` | **Defer** | No MVP requirement or independent deployment boundary is stated. Avoid collecting extra state/telemetry before the core library workflows are validated. |
| `DPV.Duplicates` | **Defer as a project** | If exact duplicate detection is part of MVP, implement it as a small Application feature backed by streamed hashes in Infrastructure. Defer geometry similarity and destructive deduplication. |

The initial solution is therefore **four projects**, with an optional fifth `DPV.Viewer` project after the compatibility proof. This retains meaningful compile-time boundaries without making every feature a project.

### 8.2 Dependency direction

Recommended logical direction:

```text
DPV.App (Avalonia UI, composition root)
  ├── DPV.Application
  ├── DPV.Infrastructure
  └── [DPV.Viewer, if isolated renderer boundary is justified]

DPV.Infrastructure ──implements──> DPV.Application ports
DPV.Application ──uses──> DPV.Domain
DPV.Domain ──depends on──> no DPV project or UI/persistence framework
```

`DPV.App` may reference Infrastructure at startup for dependency registration. Application should not reference Infrastructure. If `DPV.Viewer` needs application-level commands/contracts, those contracts should not make Domain depend on rendering types.

### 8.3 Keep, change, defer

**Keep as planned:** local-first/offline product direction; Artist → Collection → Model user hierarchy; Avalonia and MVVM; SQLite catalog; FTS5-first search; separation of domain/use cases/adapters; dedicated 3D viewing capability.

**Change before implementation:** target .NET 10 LTS; reduce projects; consolidate persistence into Infrastructure; define the exact OS/rendering backend and validate it; pick one default ORM; define the catalog/file boundary and performance/backup contracts.

**Defer:** dedicated search service, standalone analytics assembly, advanced similarity detection, distributed/server architecture, and any project split without a concrete test/dependency/reuse boundary.

## 9. Final Verdict

# APPROVE WITH CHANGES

The **overall direction is appropriate**: a local-first desktop modular monolith with an Avalonia/MVVM UI, a clean inward dependency rule, and a SQLite catalog fits the product better than a distributed architecture. The proposed hierarchy and the separation between metadata cataloging and 3D viewing also make sense.

Do **not** begin implementation with the proposed 12-project shape unchanged. First move to .NET 10 LTS, collapse the feature-specific assemblies into the four core projects, select and validate the renderer against the actual OS targets, choose a single default persistence stack, and write down asset identity, scanning, backup, and performance contracts. FTS5 is a suitable starting point; there is no evidence-based reason to add an external search engine solely for 250,000 models.

These changes preserve the useful boundaries while preventing avoidable project sprawl, renderer/platform surprises, stale-runtime work, and catalog/file consistency problems.

---

## Sources and evidence notes

Sources were reviewed on **2026-10-28**. Public product pages establish advertised features, not hidden implementation details. Where a product is proprietary, this report uses it only as a UX/workflow reference. Architecture guidance from web-app samples is applied only for general dependency principles, not copied as a complete desktop design.

### Project context

- [DEVELOPMENT_STANDARD.md](../../DEVELOPMENT_STANDARD.md) — REA-first workflow, artifact requirements, and project governance.
- [DPV-PRS-001](../requirements/DPV-PRS-001.md), [DPV-ARCH-001](../architecture/DPV-ARCH-001.md), [DPV-DATA-001](../database/DPV-DATA-001.md), [DPV-UI-001](../ui/DPV-UI-001.md), and [DPV-AI-001](../ai/DPV-AI-001.md) — required project artifacts are present in the repository tree but were blank when inspected; therefore, this report treats the request’s supplied project context as the current design baseline.

### Framework, database, and architecture

- [Avalonia MVVM pattern](https://docs.avaloniaui.net/docs/fundamentals/the-mvvm-pattern)
- [Avalonia Samples repository](https://github.com/AvaloniaUI/Avalonia.Samples)
- [Helix Toolkit repository](https://github.com/helix-toolkit/helix-toolkit)
- [SQLite FTS5 documentation](https://sqlite.org/fts5.html)
- [SQLite Write-Ahead Logging](https://sqlite.org/wal.html)
- [When To Use SQLite](https://www.sqlite.org/whentouse.html)
- [SQLite Limits](https://www.sqlite.org/limits.html)
- [Dapper repository](https://github.com/DapperLib/Dapper)
- [EF Core efficient querying](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying)
- [EF Core SQLite provider limitations](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations)
- [Microsoft .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- [Common web application architectures](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures)
- [Architectural principles](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/architectural-principles)
- [DDD-oriented design guidance](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/ddd-oriented-microservice)
- [Modular Monolith with DDD sample](https://github.com/kgrzybek/modular-monolith-with-ddd)
- [Vertical Slice Architecture](https://www.jimmybogard.com/vertical-slice-architecture/)

### Collection and 3D product references

- [Calibre source repository](https://github.com/kovidgoyal/calibre)
- [Calibre user manual](https://manual.calibre-ebook.com/)
- [LaunchBox product site](https://www.launchbox-app.com/)
- [Plex media library overview](https://www.plex.tv/your-media/)
- [Jellyfin source repository](https://github.com/jellyfin/jellyfin)
- [Komga source repository and feature overview](https://github.com/gotson/komga)
- [Lychee Slicer product site](https://lychee.co/lychee-slicer)
- [CHITUBOX product site](https://www.chitubox.com/en/)
- [PrusaSlicer source repository](https://github.com/prusa3d/PrusaSlicer)
- [MeshLab source repository](https://github.com/cnr-isti-vclab/meshlab)

### AI-assisted development references

- [Cursor Rules](https://cursor.com/docs/rules)
- [Google Antigravity Rules](https://antigravity.google/docs/rules)
- [Claude Code project memory and instructions](https://code.claude.com/docs/en/memory)
