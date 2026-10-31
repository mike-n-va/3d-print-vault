# TESTING-CONFIGURATION-001
## Testing Configuration Specification
### Version 1.0
### Status: Draft
### Phase: Commit C - Evaluation / Configuration

---

# Purpose

This document defines the approved configuration standards for the 3D Print Vault testing ecosystem.

This document follows:

- DEVELOPMENT_STANDARD.md
- REA-005-Testing-Foundation.md
- TESTING-FOUNDATION-001.md
- TESTING-DEFINITIONS-001.md

No implementation is authorized.

This document defines:

- Package standards
- Version governance
- Architecture testing configuration
- CI execution requirements
- Test execution policies
- Coverage policies

---

---

# Approved Package Matrix

## Purpose

Define the approved testing packages for the project.

Only approved packages may be introduced without additional architecture review.

---

## Approved Packages

### Test Framework

```text
xUnit v3
```

Status:

```text
Approved
```

Purpose:

```text
Unit Tests

Integration Tests

Architecture Tests
```

---

### Mocking Framework

```text
NSubstitute
```

Status:

```text
Approved
```

Purpose:

```text
Boundary Isolation

Controlled Test Scenarios

Dependency Substitution
```

---

### Architecture Framework

```text
ArchUnitNET
```

Status:

```text
Approved
```

Purpose:

```text
Dependency Validation

Boundary Enforcement

Architecture Governance
```

---

## Deferred Packages

```text
FluentAssertions

Shouldly
```

Status:

```text
Deferred
```

Requirements:

```text
License Review

Architecture Review

Explicit Approval
```

before adoption.

---

## Prohibited Packages

Any testing package not listed in:

```text
Approved Packages
```

requires:

```text
Architecture Review

Approval

Documentation Update
```

prior to installation.

---

# Release Build Architecture Validation

## Purpose

Ensure architecture validation reflects production build behavior.

---

## Requirement

Architecture tests shall execute against:

```text
Release Builds
```

at least once during CI validation.

---

## Rationale

Debug-only validation is insufficient.

Dependency analyzers and compiled code paths can behave differently under optimization.

Architecture compliance must be verified under conditions representative of production builds.

---

## Enforcement

The CI pipeline shall include:

```text
dotnet build -c Release

Architecture Test Execution
```

prior to merge approval.

---

# Developer Validation Standard

## Purpose

Provide a single, deterministic validation workflow.

Applies equally to:

```text
Developers

Reviewers

AI Agents

CI Systems
```

---

## Approved Validation Sequence

```bash
dotnet restore

dotnet build

dotnet test
```

---

## Requirement

Before requesting review:

```text
Restore Must Pass

Build Must Pass

Tests Must Pass
```

---

## AI-Assisted Development

Generated changes shall not be considered complete until:

```text
dotnet restore

dotnet build

dotnet test
```

have executed successfully.

---

## CI Alignment

Continuous Integration shall execute the same validation sequence.

Local and CI validation workflows must remain aligned whenever possible.

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

Status:

```text
Approved
```

---

## Mocking Framework

Approved:

```text
NSubstitute
```

Purpose:

```text
Boundary Mocking

External Dependency Isolation

Controlled Test Scenarios
```

Status:

```text
Approved
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

Boundary Protection

Architecture Enforcement
```

Status:

```text
Approved
```

---

## Assertion Framework

Current Standard:

```text
xUnit Assertions
```

Status:

```text
Approved
```

---

## Deferred Frameworks

```text
FluentAssertions

Shouldly
```

Status:

```text
Deferred
```

Reason:

```text
License Review Required

No Business Need Identified
```

---

# Version Governance

## Package Strategy

Use:

```text
Stable Releases Only
```

---

## Upgrade Policy

Package upgrades require:

```text
Review

Verification

Commit

Push
```

before adoption.

---

## Pre-Release Packages

Not approved.

```text
Alpha

Beta

Preview

Nightly
```

packages are prohibited unless explicitly approved through architecture review.

---

# Test Project Configuration

## Approved Structure

```text
DPV.Tests

    Unit/

    Integration/

    Architecture/
```

---

## Namespace Standard

Unit Tests:

```csharp
DPV.Tests.Unit
```

Integration Tests:

```csharp
DPV.Tests.Integration
```

Architecture Tests:

```csharp
DPV.Tests.Architecture
```

---

# Architecture Test Configuration

## Rule Enforcement

Architecture tests shall validate:

```text
Domain Purity

Dependency Direction

Project Boundaries

Reference Rules
```

---

## Mandatory Rules

### Rule 001

DPV.Domain references:

```text
None
```

---

### Rule 002

DPV.Infrastructure references only:

```text
DPV.Domain
```

---

### Rule 003

UI contains no database ownership.

---

### Rule 004

No circular dependencies.

---

### Rule 005

No production project references:

```text
DPV.Tests
```

---

### Rule 006

Architecture tests execute in CI.

Failures block merge approval.

---

# Unit Test Configuration

## Requirements

Unit tests must be:

```text
Fast

Deterministic

Independent
```

---

## Maximum Scope

Unit tests may validate:

```text
ViewModels

Business Rules

Validation

Value Objects
```

---

## Forbidden

```text
SQLite

File System

Network Access

External Services
```

---

# Integration Test Configuration

## Requirements

Use:

```text
Real SQLite

Real Dapper

Real Dependency Injection
```

---

## Prohibited

```text
Mock Databases

Shared Databases

Persistent Test Data
```

---

## Isolation

Each test creates:

```text
Its Own Database

Its Own Temporary Directory

Its Own Test Data
```

---

# SQLite Test Configuration

## Approved Modes

### In-Memory

Use for:

```text
Fast Repository Validation
```

---

### Temporary Database Files

Use for:

```text
Migration Tests

Transaction Tests

Multi-Connection Tests
```

---

## Prohibited

```text
Production Databases

Shared Development Databases
```

---

# Dependency Injection Configuration

## Validation Required

Each execution must validate:

```text
Container Creation

Scope Validation

Dependency Resolution
```

---

## ServiceProvider Configuration

Required:

```text
ValidateOnBuild = true

ValidateScopes = true
```

---

# Avalonia Test Configuration

## Primary Testing Target

```text
ViewModels
```

---

## Approved UI Testing

```text
Bindings

Commands

Navigation
```

via:

```text
Avalonia.Headless.XUnit
```

---

## Deferred

```text
Visual Snapshot Testing

Render Validation

Cross Platform UI Automation
```

---

# Scale Test Configuration

## Execution Environment

Scale tests are:

```text
Not PR Tests
```

---

## Execution Schedule

Allowed:

```text
Nightly

Release Validation
```

---

## Dataset Requirements

Synthetic Data Preferred.

No production data permitted.

---

# Test Naming Standard

## Unit Tests

```csharp
ArtistTests
CollectionTests
ModelTests
```

---

## Integration Tests

```csharp
RepositoryIntegrationTests
DatabaseIntegrationTests
```

---

## Architecture Tests

```csharp
DependencyRuleTests
DomainPurityTests
```

---

# Coverage Policy

## Purpose

Coverage is informational.

Coverage is not a quality metric.

---

## Approved Usage

Use coverage to:

```text
Identify Gaps

Track Trends

Support Review
```

---

## Prohibited Usage

Do not:

```text
Gate Pull Requests

Force Arbitrary Percentages

Measure Quality By Coverage Alone
```

---

# CI Requirements

## Pull Requests

Must execute:

```text
dotnet restore

dotnet build

dotnet test
```

---

## Required Categories

```text
Unit

Architecture
```

---

## Release Validation

Execute:

```text
Unit

Integration

Architecture

Scale Tests
```

---

# Dependency Governance

All testing dependencies require review for:

```text
Licensing

Maintenance

Compatibility

Community Support
```

before adoption.

---

# Success Criteria

Configuration succeeds when:

- Package governance defined
- CI rules defined
- Test execution rules defined
- Architecture configuration defined
- SQLite configuration defined
- Version policy defined

---

# Approval Gate

Before Commit D begins:

```text
Reviewed
        ↓
Approved
        ↓
Committed
        ↓
Pushed
```

---

# End of Document