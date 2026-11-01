# DPV-AI-001
## AI-Assisted Development and Repository Governance

**Status:** Approved by project owner — 2026-11-01
**Applies to:** AI-assisted research, planning, implementation, review, and
verification
**Governing process:** `DEVELOPMENT_STANDARD.md`

---

## 1. Source of Truth

Use this precedence order:

1. User's current explicit direction and approvals.
2. `DEVELOPMENT_STANDARD.md`.
3. Reviewed and approved architecture/product/data/UI contracts.
4. GitHub issues, milestones, commits, and CI evidence.
5. Unreviewed research, plans, prior summaries, and AI-generated suggestions.

Do not describe a proposed or local-only artifact as approved, committed,
pushed, or CI-verified. When local and remote state differ, verify the remote
before relying on local documents.

## 2. Mandatory Feature Workflow

Every feature follows the complete REA → Commit A → B → C → D → E → F flow.
Do not skip phases or start implementation while required project documents,
feature REA, or its review gate is outstanding.

Each phase must have:

- A narrow, explicit scope and named owner in its GitHub record.
- A reviewable artifact and acceptance criteria.
- Targeted tests and evidence at the phase where applicable.
- Explicitly recorded assumptions, rejected options, open questions, and
  deviations.
- The standard's verification/commit/push gate before the next phase.

The existence of a file or a passing build is not sufficient proof of feature
completion.

## 3. AI Change Boundaries

- Read governing instructions and relevant source before proposing or editing.
- Make only changes authorized for the current phase; do not convert a research
  task into implementation.
- Preserve user changes. Never reset, overwrite, stash, commit, push, close, or
  change project status without approval for that action.
- Never add credentials, private data, or secrets to code, prompts, logs, or
  repository artifacts.
- Do not send private repository content or credentials to an external
  service.
- Do not invent requirements, package compatibility, test outcomes, approvals,
  user intent, issue state, or runtime observations.
- If a tool is unavailable, state the limitation and continue only with
  evidence-supported work.

## 4. Generated Code and Database Safety

Before generated implementation is accepted:

- Compile and run the narrowest relevant unit/integration/architecture tests.
- Review generated SQL, migrations, transaction boundaries, and error handling
  manually.
- Use real SQLite tests for persistence behavior.
- Test import idempotency, interruption, cancellation, inaccessible paths,
  large inputs, and file identity rules.
- Never silently swallow exceptions or return success-shaped defaults.
- Never generate code that mutates/deletes source model files without explicit
  approved product requirements and user confirmation.
- Verify UI work does not introduce database/filesystem logic into Views or
  ViewModels.

## 5. Evidence and Reporting

Separate:

- Observed repository/source/CI facts.
- Inferences drawn from those facts.
- Recommendations and proposed decisions.
- Unknowns or blocked verification.

CI claims must name the run and commit. A report's statement that a review was
approved is not independent approval evidence unless the required approval
record exists. Do not upgrade a local test result into a CI claim.

## 6. Anti-Patterns

- Implementing before REA and contracts are reviewed.
- Broad prompts that ask an agent to build a complete subsystem at once.
- Creating projects, service/repository abstractions, or configuration options
  without an approved boundary or use case.
- Duplicating architecture rules across contradictory documents.
- Treating AI output as authoritative or tests as a substitute for review.
- Mocking SQLite behavior, swallowing filesystem errors, unbounded concurrent
  scanning, or materializing the full catalog in UI memory.
- Adding a new dependency without a documented purpose, license/health review,
  and compatibility verification.

## 7. Review Checklist

For each AI-assisted change, a human reviewer should confirm:

- The exact phase and scope were authorized.
- Requirements trace to approved artifacts.
- Boundaries and package choices match the approved architecture.
- Tests cover success, failure, cancellation, and boundary cases.
- Documentation and test artifacts match actual behavior.
- No secrets, unrelated edits, or unverified claims were introduced.

## 8. Approval

Approved by the project owner on 2026-11-01 after review in the current
conversation. This document supplements but does not override
`DEVELOPMENT_STANDARD.md`.
