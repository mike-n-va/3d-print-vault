\# DEVELOPMENT STANDARD



Version: 1.0



Status: Active



\---



\# Purpose



This document defines the mandatory development methodology for this project.



The purpose of this standard is to:



\- Maintain architectural consistency

\- Support multi-PC development

\- Prevent repository drift

\- Improve recovery capability

\- Establish reliable checkpoints

\- Ensure verification before advancement

\- Reduce implementation risk

\- Preserve project knowledge



This document is considered a governing project document.



\---



\# Foundational Rule



This document shall be the first file added to any new project.



Required sequence:



Repository

↓

DEVELOPMENT\_STANDARD.md

↓

Commit

↓

Push

↓

ROADMAP.md

↓

Architecture Documentation

↓

Feature Development



No major development should begin before this document exists.



\---



\# Core Principles



\## Architecture First



Architecture precedes implementation.



Systems are designed before they are built.



Implementation follows approved architecture.



\---



\## Small Verified Increments



Large systems are divided into small architectural checkpoints.



Every checkpoint must:



\- Compile

\- Verify

\- Review successfully



before continuing.



\---



\## GitHub Is Source Of Truth



GitHub is the authoritative location for project state.



Approved work should never exist only on a local machine.



\---



\## Artifact Validation



Code does not complete a feature.



A feature is complete only when a reviewable artifact exists.



Examples:



\- Generated files

\- Images

\- Reports

\- Rendered output

\- Exports

\- Executable behavior

\- Verification reports



\---



\# Standard Feature Lifecycle



Every major feature follows the same progression.



\## Commit A



\### Contracts



Purpose:



Define data structures.



Typical contents:



\- Interfaces

\- Models

\- Contracts

\- Documents



Restrictions:



No behavior.



No generation.



No business logic.



\---



\## Commit B



\### Definitions / Resolver Layer



Purpose:



Define capabilities and resolve intent.



Typical contents:



\- Definitions

\- Profiles

\- Resolvers

\- Strategies



Restrictions:



No generation.



No pipeline integration.



\---



\## Commit C



\### Evaluation / Configuration Layer



Purpose:



Translate intent into executable configuration.



Typical contents:



\- Configuration

\- Evaluators

\- Resolved policies



Restrictions:



No generation.



No production execution.



\---



\## Commit D



\### Generation / Implementation Layer



Purpose:



Create behavioral logic.



Typical contents:



\- Builders

\- Solvers

\- Engines

\- Factories

\- Services



This is typically the first commit introducing actual behavior.



\---



\## Commit E



\### Integration Layer



Purpose:



Connect the new system into production pipelines.



Typical contents:



\- Pipeline updates

\- Wiring

\- Runtime integration

\- Production registration



Restrictions:



Avoid redesigning previous layers.



\---



\## Commit F



\### Verification Layer



Purpose:



Verify behavior and generated artifacts.



Typical contents:



\- Verification reports

\- Validation results

\- Batch testing

\- Normalization passes

\- Visual inspection



Feature completion occurs only after verification.



\---



\# Commit Lifecycle



Every commit follows the same process.



Build

↓

Verify

↓

Review

↓

Approve

↓

Commit

↓

Push



\---



\# Approval Gate



No commit may advance until success criteria are reviewed and approved.



Approval confirms:



\- Scope respected

\- Verification completed

\- Deliverables created

\- Success criteria satisfied



\---



\# Commit Checkpoint Rule



After approval:



```powershell

git add .



git commit -m "Meaningful Commit Description"



git push

```



Every approved commit becomes a recovery checkpoint.



No subsequent commit begins until the current commit has been pushed.



\---



\# Session Start Procedure



Required at the beginning of every work session.



\## Verify Repository



```powershell

git rev-parse --show-toplevel

```



Confirm:



\- Correct repository



\---



\## Verify Local State



```powershell

git status

```



Confirm:



\- Expected branch

\- Clean working tree

\- Known intentional changes only



\---



\## Synchronize



```powershell

git pull

```



Confirm:



\- Up-to-date repository state



\---



\## Review Recent Activity



```powershell

git log --oneline -5

```



Confirm:



\- Latest approved commits are present



\---



\# Session End Procedure



Required before ending work.



\## Complete Current Checkpoint



Whenever possible:



Current commit



↓



Verified



↓



Approved



before ending session.



\---



\## Commit And Push



```powershell

git add .



git commit -m "Meaningful Commit Description"



git push

```



\---



\## Verify Working Tree



```powershell

git status

```



Expected:



```text

nothing to commit, working tree clean

```



\---



\## Verify Remote Synchronization



```powershell

git push

```



Expected:



```text

Everything up-to-date

```



\---



\## Create Session Handoff



Record:



\- Current Phase

\- Current Commit

\- Completed Work

\- Open Issues

\- Next Commit

\- Research Tasks

\- Known Risks



\---



\# Multi-PC Development Standard



\## GitHub Is Authoritative



All active development assumes GitHub is the authoritative version.



\---



\## No Machine Switching With Approved Unpushed Work



Never switch machines with approved but unpushed work.



Required order:



Approve

↓

Commit

↓

Push

↓

Switch Machines



\---



\## New Machine Startup



Before beginning work:



```powershell

git pull

```



Confirm expected commit history.



\---



\# Research (REA) Standard



Research should occur before major architectural uncertainty.



Research should not interrupt active implementation unnecessarily.



Preferred process:



Phase Completion

↓

Research

↓

Architecture Update

↓

Next Phase



Research outcomes should influence architecture documents and roadmaps.



\---



\# Recovery Standard



Every approved commit is a rollback point.



Examples:



Phase X Commit A

✅ Recovery Point



Phase X Commit B

✅ Recovery Point



Phase X Commit C

✅ Recovery Point



Recovery should never require unpublished local work.



\---



\# Documentation Requirements



Every project should maintain:



```text

DEVELOPMENT\_STANDARD.md



ROADMAP.md



SYSTEM\_ARCHITECTURE.md

```



before substantial implementation begins.



\---



\# Definition Of Done



A Commit Is Complete When:



✅ Success criteria satisfied



✅ Verification completed



✅ Artifacts reviewed



✅ Approval granted



✅ Commit created



✅ Push completed



\---



A Phase Is Complete When:



✅ Commits A-F completed



✅ Integration complete



✅ Verification complete



✅ Artifacts validated



✅ Recovery checkpoint exists on GitHub



✅ Handoff documented



\---



\# Official Development Workflow



Architecture

↓

Contracts

↓

Definitions

↓

Evaluation

↓

Implementation

↓

Integration

↓

Verification

↓

Approval

↓

Commit

↓

Push



Repeat until feature completion.

