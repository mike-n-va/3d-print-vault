# Definition-008A — Sprint Alignment and Governance

- **Status:** Draft — ready for Commit A review
- **Date:** 2026-11-01
- **Scope:** Sprint 1 closure governance and issue/milestone alignment
- **Implementation authorization:** None

## 1. Purpose

This definition establishes an auditable process for deciding whether Sprint 1
is eligible for formal closure and whether its GitHub issues, milestone, and
roadmap agree with the approved project requirements and architecture.

Sprint 1 cannot be declared closed solely because a roadmap says it is closed,
individual issues are closed, or CI is green. Closure requires objective
evidence for the agreed sprint scope, explicit disposition of every issue in
that scope, verification of the milestone itself, and recorded project-owner
approval. The existing [Sprint 1 Closure Audit](../reviews/SPRINT-1-CLOSURE-AUDIT-001.md)
records that issues #1–#4 were closed and #5–#14 remained open at audit time;
the milestone's own state was not verified. These are point-in-time findings,
not a substitute for rechecking GitHub at closure.

GitHub is the authoritative record for issue and milestone state. The roadmap
expresses approved project sequence and scope; requirements define product
acceptance; the architecture defines boundaries and ownership. These records
must agree, but a roadmap entry cannot override an approved architecture or
turn an unverified acceptance claim into evidence.

**Source note:** The [REA-008 research report](../research/REA-008-Sprint-Alignment-and-Governance-Resolution.md)
is now present. Its source note records that the separate approved REA-008
brief was not located; the report therefore does not claim to reproduce or
independently validate that brief. This definition uses the available
[Development Standard](../../DEVELOPMENT_STANDARD.md), approved requirements
and architecture, roadmap, audit, and REA-007. Compare the report with the
approved brief before claiming full alignment with that source.

## 2. Scope

### In scope

- Sprint 1 closure governance.
- Milestone identity, state, and issue-membership validation.
- Issue review and documented disposition.
- Roadmap-to-issue scope validation.
- Review and resolution of conflicts with approved architecture.
- Verification evidence and approval requirements.
- Traceability among authoritative artifacts and GitHub records.

### Out of scope

- Scanner or import implementation.
- Persistence, Dapper, or SQLite implementation.
- Duplicate detection or search implementation.
- AI or UI implementation.
- Sprint 2 planning or implementation.
- New architecture, project boundaries, or product requirements.
- Changing GitHub issues, milestones, or their states under this definition.

This definition is a governance contract only. It creates no permission to
edit GitHub, close issues or milestones, or begin another development phase.

## 3. Governance Contract

All reviews record the reviewer, date, exact artifact or GitHub record
reviewed, decision, evidence links, unresolved questions, and required
follow-up. Evidence must identify the behavior or acceptance criterion it
proves; a link without a stated scope is insufficient.

| Workflow | Prerequisite inputs | Review activity | Required evidence | Approval | Verification |
|---|---|---|---|---|---|
| Issue review | Current issue body, acceptance criteria, labels/assignee, state, milestone, linked artifacts, relevant code/test evidence | Check whether the issue is in scope, architecture-compliant, measurable, and supported by evidence; compare every acceptance criterion separately | Issue number and URL; reviewed body/criteria; criterion-to-evidence mapping; proposed disposition and rationale | Project-owner approval for scope, disposition, or acceptance changes; technical reviewer for evidence adequacy | Re-read the issue after any approved update; confirm status, milestone, links, and recorded decision match the approved disposition |
| Milestone review | Exact milestone record, description, state, dates, and complete issue membership | Reconcile milestone membership with the approved sprint scope and issue dispositions; identify omitted, extra, open, or unverified issues | Milestone URL/identifier and observed state; complete issue-number list and per-issue state/disposition; retrieval date | Project-owner approval for scope or status changes | Re-query GitHub after any authorized change; compare the result to the approved membership and decision |
| Roadmap validation | Current roadmap, approved requirements/architecture, REA and definition artifacts, GitHub issue/milestone inventory | Check that roadmap claims, scope, sequence, and status agree with governing artifacts and evidence; flag stale or historical statements | Roadmap revision/commit; discrepancy list; links to authoritative records and evidence | Project-owner approval for roadmap scope/status changes | Review the updated roadmap against the current remote records; preserve prior claims as history when correction is needed |
| Architectural conflict resolution | Conflicting issue/planning text and the approved architecture decision(s) | Identify the exact conflict; apply the authority hierarchy in §7; select and document a permitted issue disposition without changing architecture implicitly | Conflicting text; controlling architecture section/decision; rationale; linked disposition/successor record | Project-owner approval; architecture change additionally requires its own REA, review, and explicit approval | Confirm no prohibited project/reference/ownership change was introduced; verify the issue preserves its prior text and links to the decision |
| Verification evidence review | Approved requirements and issue criteria; submitted test, CI, artifact, or witnessed-run evidence; commit/version and environment identity | Assess whether each evidence item directly addresses a stated criterion, whether the method and result are reproducible or reviewable, and what limitations remain; distinguish failed, absent, and out-of-scope checks | Criterion-to-evidence matrix; evidence URL/identifier; commit/version; environment; observed result; reviewer and date; limitations | Technical reviewer accepts sufficiency; project owner approves any exception or scope exclusion | Independently confirm evidence identity and scope against the source run/artifact; record Pass, Fail, or Unverified per criterion |
| Sprint closure approval | Completed gates in §6; current issue and milestone data; audit and evidence register | Review every gate, exception, issue, and discrepancy; determine whether the approved sprint scope is complete and no closure-critical blocker remains | Gate checklist; complete issue/milestone reconciliation; evidence links; exception list; dated decision record | Explicit project-owner approval naming Sprint 1 and the exact scope being closed | Independently recheck GitHub issue/milestone state after authorized closure actions; report actual state, not intended state |

No workflow may use an inferred state as a verified result. An unavailable
GitHub record is **Unverified**, not implicitly open, closed, accepted, or
complete.

## 4. Issue Disposition Contract

These are governance dispositions, not GitHub-native issue states. They do
not themselves close an issue or change a milestone. Each issue receives one
current disposition, a rationale, an approving decision, and links to its
evidence and related issues.

| Disposition | Purpose and triggering criteria | Approval and evidence | Verification |
|---|---|---|---|
| **Valid** | The issue remains an authorized requirement or task, is consistent with governing artifacts, and has testable acceptance criteria. | Technical review plus owner approval of scope; preserve the issue body, criteria, and controlling artifact links. | Confirm the issue remains assigned to the correct approved scope and that each acceptance criterion has an eventual evidence plan. |
| **Superseded** | The issue's requested outcome or approach is no longer authoritative, and a later approved decision makes the original request obsolete. Supersession does not mean the work was completed. | Owner approval; cite the superseding decision and explain which original requirements no longer apply. Preserve the issue and its history. | Confirm the controlling decision is approved and accessible; verify the issue links to it and is not represented as completed work. |
| **Replaced** | A successor issue takes over the original deliverable, typically with clarified or split acceptance criteria. | Owner approval; successor issue must exist, identify the carried-forward criteria, and link back to the original. | Compare original criteria with successor criteria; confirm no requirement was dropped without an explicit decision and the successor has an approved milestone/owner. |
| **Deferred** | The issue remains valid but is intentionally not being executed now; no claim of completion is made. | Owner approval; record the reason, unmet prerequisite or review trigger, and expected review point. | Confirm it is excluded from current closure scope only through an approved disposition; verify the deferral is visible in the authoritative record. |
| **Moved To Future Sprint** | The issue remains valid but is formally assigned to a different, identified future sprint. This is a scope transfer, not completion. | Owner approval; destination sprint/milestone must be identified and its scope authorized. | Verify issue membership in the destination and absence from the closing sprint; retain a link from the sprint decision to the issue. |
| **Ready For Closure** | Every approved acceptance criterion is met and reviewed, required artifacts are verified, and no known unmet criterion remains. This is a recommendation, not a closed state. | Technical review plus explicit owner approval to close; provide criterion-by-criterion evidence and verification results. | Recheck the issue after authorized closure and record the actual GitHub state and timestamp. |
| **Blocked** | Work or acceptance cannot proceed because of a named dependency, conflict, unavailable evidence/access, or unresolved decision. | Owner approval of the blocker and its disposition; record blocker, impact, responsible decision-maker, and unblock condition. | Verify the blocker remains accurate and that no closure claim treats blocked criteria as satisfied. Re-review when the unblock condition changes. |

Issues may move between dispositions only through a recorded, approved
decision. A status label or milestone reassignment alone is not sufficient
evidence of disposition.

## 5. Issue #10 Resolution Contract

Issue #10 (DPV-S1-010) requests repository contracts in a `DPV.Application`
layer. The approved architecture specifies the existing five-project shape,
does not define a `DPV.Application` project, and places the Library Import
Walking Skeleton's public, technology-neutral contracts in `DPV.Domain`.
`DPV.Infrastructure` implements that contract; `DPV.UI` consumes it; and
`DPV.App` composes the implementation. The approved architecture prevails.
Issue #10 must not cause a project or boundary change by implication.

Permitted resolution paths are:

1. **Supersession:** Record that an approved architecture decision supersedes
   the original Application-layer placement. Link that decision from #10 and
   preserve its original request and history.
2. **Replacement:** Create an owner-approved successor issue whose criteria
   align with the approved Domain contract boundary. Link both issues and map
   every retained, changed, or dropped criterion explicitly.
3. **Reassignment:** Retain the issue and its history, but assign its valid,
   architecture-compliant scope to the approved project/feature owner and
   milestone. Do not silently reinterpret its Application-layer request.
4. **Clarification:** Obtain owner approval for a clarifying comment or
   amendment that explicitly maps the requested contracts to `DPV.Domain`
   and records the architecture decision. Preserve the original wording and
   link the clarification to the controlling architecture section.

The project owner must choose and record one path before #10 can be considered
resolved for sprint alignment. The path must state whether remaining work is
valid, superseded, replaced, deferred, moved, or ready for closure.

**Prohibited:** silent closure; silent rewrite or deletion of the original
request; marking the issue complete merely because the architecture decision
exists; adding `DPV.Application`; or modifying the approved architecture
without a separate REA, architecture review, and explicit approval.

Historical traceability is mandatory: retain the original issue number, body
and status history; link the architecture decision and any successor; record
the decision date and approver; and preserve a criterion mapping.

## 6. Sprint Closure Contract

Every gate is **Pass**, **Fail**, or **Unverified**. Closure is permitted only
when all gates pass for the exact approved Sprint 1 scope. A missing source,
record, or evidence item is Unverified and blocks closure; it is not treated as
a pass.

| Gate | Objective pass criteria | Required evidence and verification | Approval |
|---|---|---|---|
| **Requirements Approved** | The authoritative requirements document exists, is non-empty, has explicit approval, and every acceptance criterion for work in the approved Sprint 1 scope is mapped to an issue and direct evidence or an approved disposition. | Inspect the current committed requirements artifact and approval record; compare the criterion mapping to the complete milestone issue list and approved scope. | Project owner confirms the requirements baseline and any accepted scope exclusions. |
| **Architecture Approved** | The approved architecture and project definitions are identified; every Sprint 1 issue and claimed implementation conforms, or has a separately approved architecture change. | Cite the exact architecture decision/section and review relevant implementation or verification evidence; resolve each conflict. | Project owner confirms the controlling architecture and approved exceptions. |
| **Research Approved** | A reviewed applicability register identifies every REA required for the approved Sprint 1 scope; each applicable artifact is complete, present, reviewed, and approved. No required research is inferred from a title or roadmap row. | Verify the applicability rationale and each artifact's content, status, and approval. If a cited governing brief or source is unavailable, the affected research entry and this gate are Unverified. | Project owner records acceptance of the research baseline and any explicitly excluded research with rationale. |
| **Audit Complete** | A dated audit checks the complete milestone issue set, roadmap scope, acceptance evidence, CI/artifact scope, and known discrepancies. | Audit includes a per-issue table and identifies unavailable evidence; independently compare it with current GitHub and repository records. | Reviewer signs the audit; owner accepts its findings, not merely its existence. |
| **Open Issues Reviewed** | Every issue in the original milestone membership has one approved disposition. Every issue retained in closure scope has all acceptance criteria verified. A Blocked issue cannot pass this gate while it remains in closure scope; exclusion requires a separately approved disposition and complete traceability. | Retrieve the full milestone membership; compare issue numbers and states with the disposition register, approved scope changes, and criterion evidence. | Owner approves each scope/disposition decision; technical reviewers approve evidence adequacy. |
| **Milestone Verified** | The exact Sprint 1 milestone identity, current state, and complete issue membership are directly retrievable and match the approved closure scope. | Record the GitHub milestone URL/ID, retrieval time, state, and issue list; re-query after authorized changes. | Owner approves any required milestone-state or membership action. |
| **Verification Evidence Recorded** | Each in-scope acceptance criterion has direct, reviewable evidence identifying what was tested, result, environment, commit/version, and any limitation. CI success is credited only for the checks and platforms it actually ran. | Link test reports, CI runs/jobs, reviewable artifacts, and witnessed UI/runtime evidence as applicable; verify links and scope. | Technical reviewer accepts evidence; owner approves any explicitly accepted limitation or exception. |
| **Owner Approval Recorded** | A dated decision explicitly names Sprint 1, its exact scope, the gate results, unresolved exceptions, and the decision to close or not close. | Inspect the durable approval record and ensure it is linked from the audit/roadmap or authoritative GitHub record. | Project owner approval is mandatory; approval does not substitute for any failed gate. |

After all gates pass, authorized GitHub actions may align issue and milestone
state with the approved decision. Re-query and record the resulting state.
Until that verification is complete, report the sprint as **closure approved,
GitHub state not yet verified**, not formally closed. No gate authorizes
closing an unmet issue or suppressing an unresolved acceptance criterion.

## 7. Governance Traceability Contract

Maintain a reviewable, bidirectional mapping with stable identifiers:

| Record | Must trace to |
|---|---|
| Requirement | Controlling architecture; applicable REA; definition/contract; roadmap scope; issue(s); milestone; acceptance evidence |
| Architecture decision | Requirements and research rationale; affected definitions, roadmap entries, and issues; approval record; verification protecting the boundary |
| Research artifact | Problem and recommendation; affected requirement/architecture/definition; review status and approval; resulting issue or recorded decision |
| Definition/contract | Governing research; requirements and architecture; issue/milestone scope; measurable acceptance and verification evidence |
| Roadmap item | Requirement/definition; issue number(s); sprint/milestone; phase owner; current state and evidence/decision links |
| Issue | Stable issue URL/number; requirement/decision and acceptance criteria; disposition; milestone; owner; evidence and successor/related issue links |
| Milestone | Approved sprint scope; complete issue membership; audit and closure decision; verified current GitHub state |
| Future-sprint reference | An approved disposition, identified destination milestone, retained issue/requirement links, and owner; it is only a traceability destination, not authorization to plan or implement that work here |

No orphan is permitted in a closure review: every issue in the milestone maps
to an approved requirement, decision, or explicit disposition; every
roadmap-listed Sprint 1 item maps to an issue or an approved explanation; and
every closure claim maps to evidence and owner approval. Preserve exact URLs,
issue numbers, commit hashes, CI run/job identifiers, dates, and evidence
limitations. Do not replace historical records; append linked decisions.

**Conflict resolution order for substantive project decisions:**

1. **Approved architecture** (`DPV-ARCH-001` and its approved decision
   records) controls architectural boundaries and project ownership.
2. **Approved requirements** control product scope and acceptance where they
   do not conflict with the approved architecture.
3. **Reviewed and approved research** informs decisions but cannot override
   approved architecture or requirements.
4. **Reviewed definitions/contracts** operationalize higher-authority
   decisions within their approved scope; they cannot amend those decisions.
5. **The roadmap** records approved sequence and status; it cannot override
   architecture, requirements, research, or definitions.

`DEVELOPMENT_STANDARD.md` governs process and phase gates separately from this
substantive hierarchy. GitHub issues and milestones are authoritative evidence
of their actual current state and membership, but their text or state does not
by itself approve a scope or architecture change. Unapproved notes, historical
roadmap claims, and AI recommendations have no authority to alter these
records.

If two equally authoritative approved artifacts conflict, mark the affected
gate Unverified and obtain a documented owner decision through the applicable
review process. Do not resolve the conflict by choosing whichever text is
newer or easier to implement.

## 8. Risk Register

| Risk | Description | Likelihood | Impact | Mitigation | Verification approach |
|---|---|---:|---:|---|---|
| **Architectural Drift** | Issues or roadmap language introduce a project, dependency, or responsibility not approved by architecture. | Medium | High | Apply the authority hierarchy; require explicit architecture review for boundary changes; retain conflict links. | Review every Sprint 1 issue and claimed artifact against approved project/ownership decisions; record exceptions. |
| **Milestone Misalignment** | Roadmap scope, milestone membership, and issue states describe different sprint boundaries. | High | High | Reconcile the complete remote issue list against roadmap and approved dispositions before closure. | Record and compare exact issue-number sets and milestone state from a current GitHub retrieval. |
| **Silent Scope Change** | Criteria are removed, rewritten, deferred, or transferred without approval or history. | Medium | High | Require a dated decision, criterion mapping, links, and owner approval for every scope change. | Compare current issue text/history and successor mapping with the approved disposition record. |
| **Issue Traceability Loss** | Superseded or replaced issues are closed or edited without retaining relationships and acceptance history. | Medium | High | Keep original issue identity/history; link successors and preserve criterion mapping. | Confirm both directions of links and verify no original criterion is unmapped. |
| **Premature Sprint Closure** | A historical claim, green CI run, or partial issue closure is mistaken for complete sprint acceptance. | High | High | Require all eight closure gates and direct evidence; treat missing evidence as Unverified. | Independent gate-by-gate review and current milestone/issue re-query before reporting closure. |
| **Implementation Before Approval** | Work begins while research, contracts, scope, or phase approval is incomplete. | Medium | High | Keep this definition planning-only; enforce the Development Standard's ordered gates and explicit phase authorization. | Review commit history and worktree changes for implementation; verify approval/push records before the next phase. |

Likelihood and impact are qualitative planning assessments, not measured
incident rates. Reassess them during the closure review using recorded
discrepancies.

## 9. Acceptance Criteria

Definition-008A is acceptable for Commit A review when all criteria below are
verified:

- [ ] Issue, milestone, roadmap, architecture, and sprint-closure workflows
  each specify inputs, review activity, evidence, approval, and verification.
- [ ] All seven issue dispositions have distinct triggering criteria,
  approval/evidence requirements, and a verification rule.
- [ ] Issue #10 conflict and all four permitted resolution paths are defined;
  prohibited silent changes and historical traceability are explicit.
- [ ] All eight sprint closure gates have objective criteria, evidence,
  verification method, and approval requirement.
- [ ] Closure requires current milestone identity/state/membership and
  criterion-level evidence; missing information cannot pass by inference.
- [ ] The traceability model links requirements, architecture, research,
  definitions, roadmap, issues, milestones, future-sprint references, and
  verification evidence.
- [ ] Conflict authority and handling of equal-authority conflicts are
  defined without changing architecture.
- [ ] The risk register contains the six required risks and, for each, a
  description, likelihood, impact, mitigation, and verification approach.
- [ ] Scope excludes implementation and new architecture; no source code,
  database objects, services, or UI components are proposed or created.
- [ ] The missing REA-008 source is resolved and reviewed before approval that
  claims this definition is governed by REA-008.

## 10. Verification Requirements

Before accepting this definition, the reviewer shall verify:

1. **Measurable gates:** every closure gate can be marked Pass, Fail, or
   Unverified using the stated objective criteria and cited evidence.
2. **Issue states:** each disposition is distinct and cannot be mistaken for
   GitHub's native open/closed state.
3. **Issue #10:** all resolution paths preserve the approved Domain contract
   boundary and the original issue history.
4. **Objective closure:** no historical claim, partial CI result, or missing
   evidence can satisfy a gate by implication.
5. **Complete traceability:** all required artifact types and cross-links in
   §7 are covered, with no silent scope transfer.
6. **No implementation:** the document contains governance rules only and
   changes no code, architecture, GitHub record, issue, or milestone.
7. **Development Standard:** the artifact is treated as Commit A planning
   material, reviewed before approval, and does not authorize Commit B–F or
   implementation.
8. **Research source:** locate and review REA-008, then record its exact
   repository path, status, and any relevant findings before removing the
   source note or claiming full REA-008 alignment.

**Current verification status:** Draft prepared from available artifacts.
The REA-008 report is present, but the separate approved research brief was
not located; criterion 10 in §9 and item 8 above remain unverified.
Accordingly, this document is ready for substantive Commit A review, but it
is not yet eligible for final approval as a contract verified against that
brief.
