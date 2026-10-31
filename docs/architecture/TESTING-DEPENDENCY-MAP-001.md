# TESTING-DEPENDENCY-MAP-001
## Testing Foundation Dependency Map
### Version 1.0
### Status: Verified
### Phase: Commit E - Integration
### Verification Date: 2026-10-31

---

# Purpose

This document records the integrated testing architecture for 3D Print Vault.

This document verifies that the implemented testing foundation matches the approved testing contracts and configuration.

This document covers:

- Testing package relationships
- Test category ownership
- Architecture rule coverage
- Validation workflow
- CI execution requirements

No new implementation is authorized.

---

# Governing Artifacts

This document is governed by:

- DEVELOPMENT_STANDARD.md
- REA-005-Testing-Foundation.md
- TESTING-FOUNDATION-001.md
- TESTING-DEFINITIONS-001.md
- TESTING-CONFIGURATION-001.md
- TESTING-IMPLEMENTATION-001.md

---

# Testing Stack Overview

## Test Framework

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
Verified
```

---

## Mocking Framework

```text
NSubstitute
```

Purpose:

```text
Dependency Substitution

Boundary Testing
```

Status:

```text
Verified
```

---

## Architecture Framework

```text
ArchUnitNET
```

Purpose:

```text
Dependency Validation

Architecture Governance
```

Status:

```text
Verified
```

---

# Package Dependency Graph

```text
DPV.Tests
    │
    ├── xunit.v3 (4.0.2)
    │       └── xunit.v3.mtp-v2 (transitive)
    │
    ├── NSubstitute (6.2.0)
    │
    ├── TngTech.ArchUnitNET (0.13.5)
    │
    └── TngTech.ArchUnitNET.xUnitV3 (0.13.5)
```

Project references (production assemblies under test):

```text
DPV.Tests
    → DPV.App
    → DPV.UI
    → DPV.Domain
    → DPV.Infrastructure
```

Execution configuration:

```text
global.json
    test.runner = Microsoft.Testing.Platform

DPV.Tests.csproj
    OutputType = Exe
    TargetFramework = net10.0
```

Removed from the test project (obsolete or not approved):

```text
xunit (v2)

xunit.runner.visualstudio

Microsoft.NET.Test.Sdk

coverlet.collector

UnitTest1.cs
```

Assertions:

```text
xUnit built-in Assert
```

NSubstitute is installed for future boundary tests. No substitute usage is required in the foundation phase.

---

# Test Category Structure

```text
DPV.Tests

    Unit/

        Foundation/

    Integration/

    Architecture/

        Dependencies/

        Boundaries/
```

Verified:

```text
Present
```

---

# Architecture Rule Coverage

## Rule 001

Domain Purity

```text
DPV.Domain

must not reference:

DPV.UI
DPV.Infrastructure
```

Enforced by:

```text
DomainPurityTests.DomainHasNoProjectReferences

DomainPurityTests.DomainTypesDoNotDependOnUi

DomainPurityTests.DomainTypesDoNotDependOnInfrastructure
```

Status:

```text
Implemented
```

---

## Rule 002

Production projects shall not reference:

```text
DPV.Tests
```

Enforced by:

```text
DependencyRuleTests.ProductionProjectsDoNotReferenceTests
```

Status:

```text
Implemented
```

---

## Rule 003

No circular project references.

Enforced by:

```text
DependencyRuleTests.ProjectReferencesAreNotCircular
```

Status:

```text
Implemented
```

---

## Rule 004

Architecture test execution.

Enforced by:

```text
DomainPurityTests.DomainHasNoProjectReferences

DomainPurityTests.DomainTypesDoNotDependOnUi

DomainPurityTests.DomainTypesDoNotDependOnInfrastructure
```

Status:

```text
Implemented
```

---

## Rule 005

Infrastructure references only Domain.

Enforced by:

```text
DependencyRuleTests.InfrastructureReferencesOnlyDomain
```

Status:

```text
Implemented
```

---

# Foundation Test Inventory

Total foundation tests: 11

```text
Unit/Foundation/SolutionStructureTests.cs
    SolutionContainsApprovedProjects
    ApprovedTestFoldersExist
    TemplateTestArtifactIsAbsent
    TestProjectReferencesApprovedPackages

Architecture/Dependencies/DependencyRuleTests.cs
    ProductionProjectsDoNotReferenceTests
    InfrastructureReferencesOnlyDomain
    ProjectReferencesAreNotCircular

Architecture/Boundaries/DomainPurityTests.cs
    DomainHasNoProjectReferences
    DomainTypesDoNotDependOnUi
    DomainTypesDoNotDependOnInfrastructure
    ArchitectureRulesAreExecutable
```

Support (not a test category):

```text
Support/TestRepository.cs
```

Integration folder exists with placeholder only. No integration tests are authorized in this phase.

---

# Test Execution Flow

Developer

```text
Code Change
        ↓
dotnet restore
        ↓
dotnet build
        ↓
dotnet test
```

---

CI

```text
Pull Request
        ↓
Restore
        ↓
Build
        ↓
Architecture Tests
        ↓
Test Execution
        ↓
Merge Evaluation
```

---

# Approved Package Matrix

## Approved

```text
xUnit v3

NSubstitute

ArchUnitNET
```

---

## Deferred

```text
FluentAssertions

Shouldly
```

---

## Prohibited

Any package not reviewed through:

```text
REA

Architecture Review

Approval
```

---

# Validation Requirements

Required Validation:

```text
dotnet restore

dotnet build

dotnet test

dotnet test -c Release
```

---

# Release Validation

Architecture rules must execute against:

```text
Release Builds
```

Status:

```text
Verified
```

---

# Test Governance Summary

The testing foundation is responsible for:

```text
Architecture Protection

Dependency Governance

Future Test Expansion

AI-Assisted Development Protection
```

The testing foundation is not responsible for:

```text
Feature Validation

Business Logic Validation

Repository Validation

SQLite Validation
```

Those responsibilities belong to future tickets.

---

# Success Criteria

Integration succeeds when:

- Package structure verified
- Test structure verified
- Architecture rules verified
- Validation workflow verified
- Release validation verified

---

# Integration Verification Record

Verified against repository state on 2026-10-31.

| Check | Result |
|---|---|
| `DPV.Tests` references `xunit.v3` 4.0.2 | Pass |
| `NSubstitute` 6.2.0 referenced | Pass |
| `TngTech.ArchUnitNET` and `.xUnitV3` 0.13.5 referenced | Pass |
| Prohibited packages absent from `DPV.Tests.csproj` | Pass |
| `UnitTest1.cs` absent | Pass |
| Approved folder structure present | Pass |
| Rules 001–005 mapped to executable tests | Pass |
| `global.json` MTP runner configured | Pass |
| `dotnet restore` | Pass |
| `dotnet build` | Pass (0 warnings, 0 errors) |
| `dotnet test` | Pass when production assemblies load (11/11) |
| `dotnet test -c Release` | Pass when production assemblies load (11/11) |

Aligns with:

```text
DPV-DEPENDENCY-MAP-001.md
```

for production project reference rules validated by architecture tests.

---

# End of Document