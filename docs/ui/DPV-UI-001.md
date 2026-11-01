# DPV-UI-001
## User Interface and Interaction Contract

**Status:** Approved by project owner — 2026-11-01
**Platform:** Avalonia / MVVM, Windows desktop MVP target
**No UI implementation is authorized by this document.**

---

## 1. UI Principles

- Keep Views declarative and presentation-focused.
- Keep commands, view state, progress, and validation in testable ViewModels.
- Keep catalog rules, SQL, file walking, hashing, and mesh processing out of
  Views and ViewModels.
- Use constructor injection and explicit factories through the approved
  DPV.App composition root; do not expose a service locator.
- Run filesystem and database work asynchronously; marshal presentation updates
  through the supported Avalonia UI-thread mechanism.
- Report progress, cancellation, and errors without blocking the UI.
- Treat SQLite as the source for catalog display, not a transient scan result.

## 2. Walking-Skeleton Flow

The minimum user-visible workflow is:

1. The user selects a folder.
2. The UI shows the selected root and starts a cancellable import request.
3. The UI reports progress and an explicit completed, cancelled, or failed
   outcome.
4. The UI displays the imported model list using data queried from SQLite.
5. Reopening/reloading the application still shows the persisted catalog.

The UI must distinguish incomplete/cancelled imports from completed imports.
If individual files fail, the result must summarize failures and leave
actionable details in the existing logging/error surface.

## 3. Large Catalog Behavior

- Do not create a ViewModel per catalog row up front.
- Use paging/incremental loading and virtualization for long lists.
- Define initial sort order and stable paging key in Commit A/B.
- Do not load all model bytes or thumbnails to render a list.
- Prevent stale scan responses from overwriting newer user selections.
- Disable or guard conflicting actions while a scan is active, with explicit
  cancellation semantics.

The acceptable first-page latency, refresh latency, and progress update rate
must be measured and set in Commit A on a declared reference machine.

## 4. Accessibility and Failure Presentation

- Provide keyboard-accessible folder selection, list navigation, and commands.
- Expose busy/progress/completion state to assistive technology where supported.
- Preserve actionable error messages for inaccessible roots, permission
  failures, unsupported inputs, and database errors.
- Avoid silent empty-list fallbacks when a query or import failed.
- No destructive source-file actions are part of the walking skeleton.

Detailed visual design, localization, and contrast criteria require a later
product/UI review; this document does not claim conformance until tested.

## 5. View and ViewModel Boundaries

| Concern | Owner |
|---|---|
| Folder picker interaction, state, commands, progress, list selection | DPV.UI |
| Public import contract and technology-neutral request/result types | DPV.Domain |
| Import use-case implementation/orchestration and filesystem/SQLite adapters | DPV.Infrastructure |
| Artist/Collection/Model invariants | DPV.Domain |
| Startup, navigation composition, configuration, logging bootstrap | DPV.App plus navigation owned by UI |

`DPV.Application` is not an approved project in the current solution. Do not
introduce it as part of this feature without an architecture review.
ViewModels must depend on the Domain contract, not Infrastructure
implementations or adapter types. App performs the contract-to-implementation
registration. Infrastructure orchestration coordinates adapters only; domain
invariants remain in Domain and UI presentation mapping remains in DPV.UI.
This boundary was approved on 2026-11-01; approval does not authorize
implementation.

## 6. Viewer and Preview Scope

The walking skeleton only lists imported models; it does not require rendering
or thumbnail generation. The product architecture documents list Helix Toolkit
as a target, but REA-001 requires a compatibility proof for the selected
package, Avalonia version, operating system, and GPU path. Do not treat a
renderer as an approved UI dependency until that proof and a decision are
recorded.

## 7. UI Verification

- Unit-test ViewModels for command enablement, progress, cancellation, error
  state, and query-result presentation.
- Test navigation and relevant bindings with Avalonia headless support when
  applicable.
- Keep snapshots, GPU rendering, and full UI automation deferred until a
  concrete defect risk justifies their maintenance cost.
- Verify the end-to-end artifact in a real supported desktop environment:
  import a target folder, reload from SQLite, and show the expected list.

## 8. Approval

Approved by the project owner on 2026-11-01 after review in the current
conversation. This approval does not authorize implementation or waive the REA
and Commit A–F gates.
