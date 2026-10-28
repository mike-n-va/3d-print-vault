# DEVELOPMENT_STANDARD.md

## 3D Print Vault Development Standard

### Version 1.0

> DEVELOPMENT_STANDARD.md is the governing document for all development work.
>
> If this document conflicts with a feature plan, roadmap, backlog item, implementation approach, or AI-generated recommendation, DEVELOPMENT_STANDARD.md takes precedence.

---

# Purpose

This document defines the mandatory development workflow for 3D Print Vault.

All project work shall follow this standard.

The objective is to:

- Prevent architectural drift
- Reduce technical debt
- Improve AI-assisted development quality
- Ensure GitHub remains the source of truth
- Produce verifiable artifacts for every feature

---

# Core Principles

## 1. GitHub Is The Source of Truth

GitHub is the authoritative source for the project.

Never assume a local machine contains the latest approved state.

All approved work must be pushed before:

- Ending a work session
- Switching computers
- Beginning new feature work

---

## 2. Artifact-Based Development

A feature is not complete because code exists.

A feature is complete only when:

```text
Implementation Exists
        AND
Reviewable Artifact Exists
        AND
Artifact Is Verified
```

Examples:

### Scanner

Artifact:

```text
Successfully imports target library.
```

### Thumbnail Generator

Artifact:

```text
Thumbnails rendered and verified.
```

### Viewer

Artifact:

```text
Models open and interact correctly.
```

---

## 3. Research Before Development

All features require research before implementation.

We do not begin with implementation.

We begin with understanding.

---

# Development Flow

Every feature shall follow:

```text
REA
Research & Reverse Engineering

Commit A
Contracts

Commit B
Definition / Resolver

Commit C
Evaluation / Configuration

Commit D
Implementation / Generation

Commit E
Integration

Commit F
Verification / Validation
```

No steps may be skipped.

---

# REA Phase

## REA Is Mandatory

REA is required for every feature.

There are no exceptions.

Research may be small or large depending on feature size.

However:

```text
REA is never skipped.
```

---

## Purpose

Identify:

```text
Existing Solutions

Best Practices

Common Failures

User Expectations

Recommended Direction
```

before implementation begins.

---

## REA Deliverable

Every feature requires an REA artifact.

Location:

```text
docs/research
```

Naming:

```text
REA-001-Library-Organization.md

REA-002-Search-System.md

REA-003-Viewer.md

REA-004-Duplicate-Detection.md
```

---

## REA Completion Requirements

An REA document must answer:

### Problem

```text
What problem is being solved?
```

### Existing Solutions

```text
Who already solves this?
```

### Strengths

```text
What should be copied?
```

### Weaknesses

```text
What should be avoided?
```

### Recommendation

```text
What should 3D Print Vault do?
```

---

## REA Gate

A feature may not enter Commit A until:

```text
REA Complete
        ↓
Artifact Exists
        ↓
Artifact Reviewed
```

---

# Commit A

## Contracts

### Purpose

Define the feature contract.

### Deliverables

```text
Interfaces

Specifications

Acceptance Criteria

Dependency Requirements
```

No implementation permitted.

---

# Commit B

## Definition / Resolver

### Purpose

Define feature behavior.

### Deliverables

```text
Models

Definitions

Resolvers

Feature Rules
```

No implementation permitted.

---

# Commit C

## Evaluation / Configuration

### Purpose

Configure how the feature operates.

### Deliverables

```text
Settings

Mappings

Validation Rules

Configuration Objects
```

No implementation permitted.

---

# Commit D

## Implementation / Generation

### Purpose

Create functional code.

### Deliverables

```text
Services

Repositories

Commands

ViewModels

Views
```

Implementation only.

No integration.

---

# Commit E

## Integration

### Purpose

Integrate completed feature into system.

### Deliverables

```text
Dependency Registration

Navigation

Database Wiring

Service Registration

End-to-End Connection
```

---

# Commit F

## Verification / Validation

### Purpose

Verify feature correctness.

### Deliverables

```text
Testing

Validation

Performance Review

Artifact Verification
```

---

# Commit Approval Rule

Before a new commit phase begins:

Current phase must be:

```text
Implemented
        ↓
Verified
        ↓
Committed
        ↓
Pushed
```

Approved but unpushed work is not considered complete.

---

# Session Start Procedure

Before beginning work:

```bash
git rev-parse --show-toplevel

git status

git pull

git log --oneline -5
```

---

# Session End Procedure

Before ending work:

```bash
git add .

git commit

git push
```

Verify:

```bash
git status
```

Expected:

```text
nothing to commit, working tree clean
```

---

# Multi-PC Rule

Switching machines requires:

```text
Commit
        ↓
Push
        ↓
Switch Machine
        ↓
Pull
        ↓
Continue
```

Never switch PCs with approved but unpushed work.

---

# Documentation Requirements

Major project artifacts must exist before implementation.

Required documents:

```text
DPV-PRS-001

DPV-ARCH-001

DPV-DATA-001

DPV-UI-001

DPV-AI-001
```

Feature-specific REA artifacts are required before Commit A.

---

# Governance Philosophy

```text
Research First

Design Second

Implement Third

Verify Fourth
```

The objective is to reduce rework, prevent architectural drift, and ensure every feature is built on verified understanding rather than assumptions.

---

# Success Criteria

The development process is considered successful when:

- Every feature begins with REA.
- Every phase produces a reviewable artifact.
- Every approved phase is committed and pushed.
- GitHub always reflects the current approved state.
- Features are validated before being considered complete.
- Architectural integrity is maintained throughout the life of the project.

---

**End of Document**

**3D Print Vault Development Standard v1.0**
