# DPV-CONFIGURATION-001
## Evaluation & Configuration Specification
### Version 1.0

---

# Purpose

This document defines project-wide configuration standards.

No implementation is authorized.

This document establishes:

- Runtime Standards
- Logging Standards
- Database Standards
- Dependency Injection Standards
- Testing Standards
- Naming Standards

---

# Runtime Configuration

## Approved Runtime

```text
.NET 10 LTS
```

---

## Build Target

```text
Windows Desktop
```

MVP Target Platform.

Future expansion:

```text
Linux

macOS
```

---

# Dependency Injection Standard

## Approved Container

```text
Microsoft.Extensions.DependencyInjection
```

---

## Registration Rules

Every service shall be registered through:

```csharp
IServiceCollection
```

---

## Service Lifetime Rules

### Singleton

Use for:

```text
Configuration

Logging

Database Factory
```

---

### Scoped

Reserved for future use.

Not required in MVP.

---

### Transient

Use for:

```text
Import Services

Detection Services

Search Services
```

---

# Configuration Standard

## Source

```text
appsettings.json
```

---

## Environment Files

Approved:

```text
appsettings.json

appsettings.Development.json
```

---

## Configuration Sections

```text
Database

Scanning

Viewer

Logging
```

---

# Database Configuration

## Database Engine

```text
SQLite
```

---

## Data Access

```text
Dapper
```

---

## Database Location

Development:

```text
/data/dev.db
```

---

Production:

```text
%AppData%/3DPrintVault/
```

---

## SQLite Settings

### Journal Mode

```text
WAL
```

Approved.

---

### Foreign Keys

```text
Enabled
```

Required.

---

### Search

```text
FTS5
```

Required.

---

# Logging Standard

## Logging Framework

```text
Serilog
```

---

## Log Location

```text
logs/
```

---

## Log Rotation

Daily.

---

## Log Levels

```text
Debug

Information

Warning

Error

Fatal
```

---

## MVP Requirements

Log:

```text
Startup

Import

Database

Search

Viewer

Errors
```

---

# Naming Standards

## Projects

```text
DPV.*
```

---

## Classes

```csharp
ArtistService
```

---

## Interfaces

```csharp
IArtistService
```

---

## ViewModels

```csharp
ArtistViewModel
```

---

## Views

```csharp
ArtistView
```

---

## Repositories

```csharp
ArtistRepository
```

---

# Folder Naming Standards

Use:

```text
PascalCase
```

Example:

```text
ViewModels

Repositories

Navigation
```

---

# Testing Standards

## Framework

```text
xUnit
```

---

## Assertion Library

```text
FluentAssertions
```

---

## Mocking

```text
Moq
```

---

## Coverage Goal

```text
80%
```

Target.

---

# Error Handling Standard

## UI

No unhandled exceptions.

---

## Infrastructure

All external system failures must be logged.

---

## Database

Database failures must generate structured logs.

---

# Search Configuration

## Approved Engine

```text
SQLite FTS5
```

---

## Deferred Engines

```text
Lucene

Elastic

Azure Search
```

Not approved for MVP.

---

# Duplicate Detection Configuration

## MVP Algorithm

```text
SHA256
```

---

## Future Algorithms

```text
Mesh Fingerprints

Bounding Box Comparison

Geometry Similarity
```

Deferred.

---

# Viewer Configuration

## Approved Toolkit

```text
Helix Toolkit
```

---

## MVP Features

```text
Rotate

Pan

Zoom

Reset Camera
```

---

## Deferred Features

```text
Measurements

Section Cuts

Build Plate Preview
```

---

# Build Standard

## Build Must

```text
Restore

Build

Test
```

without warnings.

---

# GitHub Rule

Every approved phase:

```text
Verified
        ↓
Committed
        ↓
Pushed
```

before proceeding.

---
---

# Repository Layout Standard

## Purpose

Maintain a consistent repository structure throughout the life of the project.

The repository structure is considered part of the architecture.

Changes require architecture review and approval.

---

## Approved Top-Level Structure

```text
3d-print-vault/

    src/

    tests/

    docs/

    assets/

    scripts/

    tools/

    samples/
```

---

## Governance Rule

New top-level folders are prohibited unless approved through:

```text
REA
        ↓
Architecture Review
        ↓
Approval
```

---

## Source Structure

All production code shall exist within:

```text
src/
```

Example:

```text
src/

    DPV.App

    DPV.UI

    DPV.Domain

    DPV.Infrastructure
```

---

## Test Structure

All automated tests shall exist within:

```text
tests/
```

Example:

```text
tests/

    DPV.Tests
```

---

## Documentation Structure

All architecture and governance artifacts shall exist within:

```text
docs/
```

Example:

```text
docs/

    requirements/

    architecture/

    database/

    ui/

    ai/

    research/
```

---

# Feature Folder Standard

## Purpose

Provide a predictable structure for AI-generated and human-generated code.

All Infrastructure modules shall follow a common pattern.

---

## Standard Module Layout

```text
FeatureName/

    Contracts/  (optional; module-local adapter contracts only)

    Models/

    Services/
```

---

## Example

```text
Duplicates/

    Contracts/

        IFileIdentityReader.cs

    Models/

        DuplicateGroup.cs

        DuplicateCandidate.cs

    Services/

        DuplicateDetectionService.cs
```

---

## Search Example

```text
Search/

    Contracts/

    Models/

    Services/
```

---

## Import Example

```text
Import/

    Contracts/

    Models/

    Services/
```

For cross-project use cases, the public contract belongs in
`DPV.Domain.Contracts`; this Infrastructure `Contracts/` folder is reserved
for module-local adapter boundaries and must not duplicate that public
contract.

---

# Benefits

Provides:

```text
Predictable Navigation

Consistent AI Output

Reduced Architectural Drift

Simplified Refactoring

Improved Maintainability
```

---

## Compliance Requirement

All new Infrastructure modules must follow this structure unless an architecture review explicitly approves an alternative arrangement.


# Success Criteria

Configuration is complete when:

- Runtime defined
- Database defined
- Logging defined
- Testing defined
- Naming defined
- Dependency rules defined

---

End of Document