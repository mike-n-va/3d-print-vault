# TESTING-FOUNDATION-001
## Testing Foundation Contract
### Version 1.0

---

---

# Third-Party Dependency Governance

## Purpose

Prevent future licensing, maintenance, and support issues.

All testing dependencies must undergo a licensing and project-health review before adoption.

---

## Review Requirements

Before a testing package may become a project standard, verify:

```text
License Type

Commercial Use Terms

Maintenance Activity

Release Cadence

.NET 10 Compatibility

Community Adoption
```

---

## License Review Status

### Approved

```text
xUnit
```

Status:

```text
Approved
```

---

### Approved

```text
NSubstitute
```

Status:

```text
Approved
```

---

### Approved

```text
ArchUnitNET
```

Status:

```text
Approved
```

---

### Deferred

```text
FluentAssertions
```

Status:

```text
Pending License Review
```

Reason:

Commercial licensing implications require explicit review before adoption.
```

# Purpose

This document defines the approved testing strategy for 3D Print Vault.

No testing implementation is authorized by this document.

This document establishes:

- Testing architecture
- Testing boundaries
- Approved testing technologies
- Test categories
- Architecture enforcement rules

---

# Governing Artifacts

This contract is governed by:

- DEVELOPMENT_STANDARD.md
- REA-005-Testing-Foundation.md
- DPV-SOLUTION-001.md
- DPV-PROJECT-DEFINITIONS-001.md
- DPV-CONFIGURATION-001.md

---

# Objectives

The testing foundation shall:

- Protect architecture boundaries
- Prevent dependency drift
- Validate business behavior
- Support AI-assisted development
- Provide fast developer feedback
- Scale with future project growth

---

# Approved Testing Stack

## Test Framework

Approved:

```text
xUnit v3
```

Purpose:

```text
Unit Tests

Integration Tests

Architecture Tests
```

---

## Mocking Framework

Approved:

```text
NSubstitute
```

Usage:

```text
Service Boundaries

External Dependencies

Controlled Test Scenarios
```

---

## Architecture Testing

Approved:

```text
ArchUnitNET
```

Purpose:

```text
Dependency Validation

Architecture Enforcement

Boundary Protection
```

---

## Assertions

Status:

```text
Deferred
```

Current Default:

```text
xUnit Assertions
```

Future review required before adopting:

```text
FluentAssertions

Shouldly
```

---

# Test Categories

## Unit Tests

Purpose:

Validate:

```text
Domain Logic

Value Objects

Business Rules

ViewModels
```

Requirements:

```text
Fast

Deterministic

No External Dependencies
```

---

## Integration Tests

Purpose:

Validate:

```text
Database Behavior

Dapper Mappings

Dependency Injection

File System Operations
```

Requirements:

```text
Real Dependencies

Real SQLite

Real Configuration
```

---

## Architecture Tests

Purpose:

Validate:

```text
Project References

Boundary Rules

Dependency Direction

Forbidden Dependencies
```

Requirements:

```text
Automatic

Repeatable

CI Enforced
```

---

# Architectural Rules

## Domain Purity

DPV.Domain must not reference:

```text
DPV.UI

DPV.Infrastructure

SQLite

Helix Toolkit

File System APIs
```

---

## Infrastructure Boundary

DPV.Infrastructure may reference:

```text
DPV.Domain
```

only.

---

## UI Boundary

DPV.UI must not own:

```text
SQL

SQLite

Hashing

Import Logic

Business Rules
```

---

# SQLite Testing Standard

Approved:

```text
Real SQLite Tests
```

Not Approved:

```text
Mock Database Tests
```

Repository correctness must be validated against real SQLite behavior.

---

# Dependency Injection Testing

Requirements:

```text
ValidateOnBuild

ValidateScopes
```

must be verified through automated tests.

---

# Avalonia Testing Strategy

Unit Test:

```text
ViewModels
```

Integration Test:

```text
Bindings

Commands

Navigation
```

using:

```text
Avalonia.Headless.XUnit
```

Deferred:

```text
Visual Snapshot Testing

GPU Rendering Tests

End-to-End UI Automation
```

---

# CI Requirements

Every Pull Request must execute:

```text
Restore

Build

Unit Tests

Architecture Tests
```

before merge approval.

---

# Success Criteria

This contract succeeds when:

- Testing stack is approved
- Architecture rules are approved
- Test categories are approved
- Boundary enforcement strategy is approved
- CI expectations are approved

---

# Approval Gate

Before Commit B begins:

```text
Contract Reviewed
        ↓
Approved
        ↓
Committed
        ↓
Pushed
```

---

# End of Document