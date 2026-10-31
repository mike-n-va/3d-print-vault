# TESTING-DEFINITIONS-001
## Testing Definitions & Ownership Specification
### Version 1.0

---

# Purpose

This document defines the testing architecture for 3D Print Vault.

This document follows:

- DEVELOPMENT_STANDARD.md
- TESTING-FOUNDATION-001.md
- REA-005-Testing-Foundation.md

No implementation is authorized.

This document defines testing ownership, scope, responsibilities, and boundaries.

---

# Testing Philosophy

Testing exists to:

- Protect architecture
- Validate behavior
- Prevent regressions
- Guide AI-assisted development

Testing does NOT exist to:

- Increase coverage numbers
- Validate framework internals
- Test private implementation details

---

# Testing Categories

Approved categories:

```text
Unit

Integration

Architecture
```

---

# Unit Tests

## Purpose

Validate isolated business behavior.

---

## Ownership

Unit tests own:

```text
Domain Logic

Value Objects

Validation Rules

ViewModels

Application Workflows
```

---

## Requirements

```text
Fast

Deterministic

Independent
```

---

## Forbidden

```text
SQLite

File System

Network Calls

Real Services
```

---

## Example Targets

```text
Artist Validation

Collection Rules

Filtering Logic

ViewModel Commands
```

---

# Integration Tests

## Purpose

Validate real system interaction.

---

## Ownership

Integration tests own:

```text
SQLite

Dapper

Dependency Injection

File Operations

Configuration Loading
```

---

## Requirements

Use:

```text
Real Implementations

Real SQLite

Real File System
```

---

## Forbidden

```text
Production Databases

External APIs

Live Network Services
```

---

## Example Targets

```text
Repository Operations

Database Schema Validation

Migration Validation

Configuration Resolution
```

---

# Architecture Tests

## Purpose

Protect architectural integrity.

---

## Ownership

Architecture tests own:

```text
Dependency Rules

Project Boundaries

Namespace Rules

Layer Protection
```

---

## Examples

### Domain Purity

Verify:

```text
DPV.Domain
```

does not reference:

```text
DPV.UI

DPV.Infrastructure
```

---

### Infrastructure Boundary

Verify:

```text
DPV.Infrastructure
```

references only:

```text
DPV.Domain
```

---

### Test Isolation

Verify:

```text
No production project references DPV.Tests
```

---

# Test Organization

Approved structure:

```text
DPV.Tests

    Unit/

    Integration/

    Architecture/
```

---

# Unit Folder Ownership

```text
Unit/

    Domain/

    ViewModels/

    Validation/
```

---

# Integration Folder Ownership

```text
Integration/

    Database/

    Configuration/

    DependencyInjection/
```

---

# Architecture Folder Ownership

```text
Architecture/

    Dependencies/

    Boundaries/

    Rules/
```

---

# Naming Standards

## Unit Tests

Pattern:

```csharp
ClassNameTests
```

Example:

```csharp
ArtistTests
```

---

## Integration Tests

Pattern:

```csharp
FeatureNameIntegrationTests
```

Example:

```csharp
RepositoryIntegrationTests
```

---

## Architecture Tests

Pattern:

```csharp
ArchitectureRuleTests
```

Example:

```csharp
DependencyRuleTests
```

---

# Architecture Rule Definitions

## Rule 001

Domain must remain dependency free.

---

## Rule 002

Infrastructure references only Domain.

---

## Rule 003

UI must not own database concerns.

---

## Rule 004

No circular project references.

---

## Rule 005

No production project references DPV.Tests.

---
---

## Rule 006

Architecture tests must execute in CI.

Requirements:

```text
Architecture Test Execution

Required For Pull Requests

Required Before Merge
```

Failure of an architecture rule shall block merge approval.

Architecture governance is enforced through executable tests rather than documentation alone.

---

# Test Data Standards

## Purpose

Ensure all test execution remains deterministic, repeatable, and isolated.

---

## Requirements

Tests shall:

```text
Use Deterministic Data

Use Unique Temporary Databases

Use Unique Temporary Directories

Clean Up Temporary Resources

Execute Independently
```

---

## Prohibited

Tests shall not:

```text
Depend On Execution Order

Depend On Shared Files

Depend On Shared Databases

Depend On Machine State

Depend On Existing User Data
```

---

## Isolation Requirements

Every integration test requiring:

```text
SQLite

Temporary Files

Temporary Directories
```

must create and dispose its own resources.

Shared mutable resources are prohibited.

---

## AI Development Requirement

AI-generated tests must follow the same isolation requirements as human-written tests.

Generated tests introducing:

```text
Shared State

Hardcoded User Paths

Machine-Specific Dependencies
```

shall be rejected.

---

# Scale Test Definitions

## Purpose

Validate large-scale application behavior.

---

## Scope

Scale tests validate:

```text
Large Catalogs

Large Directory Trees

Metadata Volume

Memory Consumption

Performance Budgets

Cancellation Behavior
```

---

## Ownership

Scale tests own:

```text
250,000+ Catalog Records

Large Import Simulations

Search Performance Validation

Repository Performance Validation
```

---

## Execution Policy

Scale tests are:

```text
Not Unit Tests

Not Standard Integration Tests
```

---

## Execution Schedule

Scale tests execute during:

```text
Nightly Validation

Release Validation
```

They are not required for normal pull request execution.

---

## Success Criteria

Scale tests must:

```text
Remain Deterministic

Produce Repeatable Results

Provide Trend Data

Avoid Reliance On Physical User Libraries
```

---

## Prohibited

Scale tests shall not:

```text
Require Multi-Terabyte Real Libraries

Require Production User Data

Require Internet Connectivity
```

Synthetic test data is preferred.

---

## Future Expansion

Future scale-testing capabilities may include:

```text
Import Throughput Validation

Search Throughput Validation

Duplicate Detection Performance

Thumbnail Generation Performance
```

when those systems are implemented.

# Dependency Injection Test Definitions

## Validate Registration

Verify:

```text
Container Builds
```

---

## Validate Scopes

Verify:

```text
Service Lifetimes Correct
```

---

## Validate Resolution

Verify:

```text
Core Services Resolvable
```

---

# SQLite Test Definitions

## Repository Tests

Verify:

```text
CRUD Operations
```

---

## Migration Tests

Verify:

```text
Schema Evolution
```

---

## Transaction Tests

Verify:

```text
Rollback

Commit
```

---

# Avalonia Test Definitions

## Unit Test

Test:

```text
ViewModels
```

---

## Integration Test

Test:

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

Cross Platform UI Testing

GPU Rendering Tests
```

---

# AI Testing Standards

Generated code must:

```text
Build

Pass Tests

Respect Architecture Rules
```

before acceptance.

---

# Success Criteria

Document succeeds when:

- Test ownership defined
- Test responsibilities defined
- Architecture rules defined
- SQLite strategy defined
- DI strategy defined
- Avalonia strategy defined

---

# End of Document