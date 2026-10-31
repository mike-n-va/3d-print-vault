# DPV-PROJECT-DEFINITIONS-001
## Project Definitions & Ownership Specification
### Version 1.0

---

# Purpose

This document defines the internal structure of every approved MVP project.

This document follows:

- DEVELOPMENT_STANDARD.md
- DPV-SOLUTION-001

No implementation is authorized by this document.

This document defines structure, responsibilities, ownership, and allowed boundaries.

---

# MVP Solution Structure

```text
src/

DPV.App

DPV.UI

DPV.Domain

DPV.Infrastructure

tests/

DPV.Tests
```

---

# Project Ownership Matrix

| Project | Responsibility |
|----------|----------------|
| DPV.App | Startup |
| DPV.UI | User Interface |
| DPV.Domain | Business Domain |
| DPV.Infrastructure | External Systems |
| DPV.Tests | Validation |

---

# DPV.App

## Purpose

Application orchestration.

Hosts the application.

Responsible for startup and configuration.

---

## Ownership

Owns:

```text
Program Startup

Dependency Injection

Configuration

Logging Bootstrap

Application Lifetime
```

---

## Internal Structure

```text
DPV.App

    Configuration/

    DependencyInjection/

    Logging/

    Startup/
```

---

## Forbidden Responsibilities

```text
SQL

File Scanning

Renderer Logic

Business Rules
```

---

# DPV.UI

## Purpose

Presentation layer.

Responsible for everything visible to the user.

---

## Ownership

Owns:

```text
Views

ViewModels

Navigation

Themes

Commands

Converters
```

---

## Internal Structure

```text
DPV.UI

    Views/

    ViewModels/

    Navigation/

    Themes/

    Commands/

    Converters/
```

---

## Views

Approved MVP Views:

```text
DashboardView

ArtistView

CollectionView

ModelView

SettingsView
```

---

## ViewModels

Approved MVP ViewModels:

```text
DashboardViewModel

ArtistViewModel

CollectionViewModel

ModelViewModel

SettingsViewModel
```

---

## Forbidden Responsibilities

```text
SQLite Access

File IO

Hashing

Thumbnail Generation

Import Logic
```

---

# DPV.Domain

## Purpose

Business rules and entities.

---

## Ownership

Owns:

```text
Artist

Collection

Model

Tag

PrintHistory

Project
```

---

## Internal Structure

```text
DPV.Domain

    Entities/

    Enums/

    ValueObjects/

    Contracts/

    Validation/
```

---

## Entities

### Artist

Represents creator.

---

### Collection

Represents release or pack.

---

### Model

Represents a single printable asset.

---

### Tag

Classification mechanism.

---

### PrintHistory

Production tracking.

---

## Rule

Domain must remain dependency free.

---

## Forbidden References

```text
DPV.UI

SQLite

File System

Helix Toolkit
```

---

# DPV.Infrastructure

## Purpose

Implementation of all external systems.

---

## Ownership

Owns:

```text
Persistence

Import

Search

Viewer

Thumbnailing

Duplicates
```

---

# Infrastructure Structure

```text
DPV.Infrastructure

    Persistence/

    Import/

    Search/

    Viewer/

    Thumbnailing/

    Duplicates/
```

---

# Persistence Module

## Responsibility

SQLite implementation.

---

### Owns

```text
Database

Dapper

Migrations

Repositories
```

---

### Structure

```text
Persistence/

    Repositories/

    Migrations/

    Database/
```

---

# Import Module

## Responsibility

Library discovery.

---

### Owns

```text
Scanning

Metadata Extraction

Import Queue
```

---

### Structure

```text
Import/

    Scanning/

    Metadata/

    Queue/
```

---

# Search Module

## Responsibility

Library search.

---

### Owns

```text
FTS5

Query Parsing

Filtering
```

---

### Structure

```text
Search/

    Engine/

    Filters/

    Queries/
```

---

# Viewer Module

## Responsibility

3D visualization.

---

### Owns

```text
Model Rendering

Statistics

Camera Controls
```

---

### Structure

```text
Viewer/

    Rendering/

    Analysis/

    Controls/
```

---

# Thumbnailing Module

## Responsibility

Preview generation.

---

### Owns

```text
Thumbnail Jobs

Thumbnail Cache

Renderer
```

---

### Structure

```text
Thumbnailing/

    Jobs/

    Cache/

    Rendering/
```

---

# Duplicates Module

## Responsibility

Duplicate detection.

---

### MVP Scope

```text
SHA256
```

only.

---

### Structure

```text
Duplicates/

    Hashing/

    Detection/

    Grouping/
```

---

### Future Scope

```text
Mesh Fingerprints

Similarity Detection

AI Analysis
```

---

# DPV.Tests

## Purpose

All automated validation.

---

## Internal Structure

```text
DPV.Tests

    Unit/

    Integration/

    Architecture/
```

---

# Unit Tests

Validate:

```text
Domain

Business Rules

Services
```

---

# Integration Tests

Validate:

```text
Database

Search

Import
```

---

# Architecture Tests

Validate:

```text
Project References

Dependency Rules

Layer Constraints
```

---

# Namespace Standards

## App

```csharp
DPV.App
```

---

## UI

```csharp
DPV.UI
```

---

## Domain

```csharp
DPV.Domain
```

---

## Infrastructure

```csharp
DPV.Infrastructure
```

---

## Tests

```csharp
DPV.Tests
```

---

# Growth Strategy

Future projects may be extracted when justified.

Candidate Projects:

```text
DPV.Search

DPV.Viewer

DPV.Duplicates

DPV.Analytics
```

Extraction requires:

Architecture Review

REA

Approval

---

# Success Criteria

Document succeeds when:

- Project ownership defined
- Boundaries defined
- Internal structure defined
- Namespace strategy defined
- MVP scope defined

---

End of Document