# DPV-DEPENDENCY-MAP-001
## Solution Dependency Map
### Version 1.0
### Status: Verified
### Phase: Commit E - Integration

---

# Purpose

This document records the integrated dependency structure of the 3D Print Vault solution.

It is governed by:

- DEVELOPMENT_STANDARD.md
- DPV-SOLUTION-001
- DPV-PROJECT-DEFINITIONS-001
- DPV-CONFIGURATION-001
- DPV-SOLUTION-IMPLEMENTATION-001

This artifact describes project dependencies only.

No business features, entities, services, repositories, database schema, search, viewer, duplicate detection, or thumbnail behavior are authorized by this document.

---

# 1. Solution Overview

Solution:

```text
DPV.sln
```

Runtime:

```text
.NET 10 LTS (net10.0)
```

Projects:

| Project | Path | Type | Responsibility |
|---|---|---|---|
| DPV.App | `src/DPV.App` | Console application | Startup, configuration, dependency injection, logging bootstrap |
| DPV.UI | `src/DPV.UI` | Avalonia application | Views, view models, navigation, themes, commands, converters |
| DPV.Domain | `src/DPV.Domain` | Class library | Domain models, value objects, contracts |
| DPV.Infrastructure | `src/DPV.Infrastructure` | Class library | Persistence, import, search, viewer, thumbnailing, duplicates |
| DPV.Tests | `tests/DPV.Tests` | xUnit project | Unit, integration, and architecture tests |

Solution folders:

```text
src/

tests/
```

Namespaces:

```text
DPV.App

DPV.UI

DPV.Domain

DPV.Infrastructure

DPV.Tests
```

DPV.Domain is the dependency-free core. Production projects may depend inward toward Domain. Tests may depend on every application project. No production project may depend on DPV.Tests.

---

# 2. Project Dependency Graph

Verified project references:

```text
DPV.App
    → DPV.UI
    → DPV.Domain
    → DPV.Infrastructure

DPV.UI
    → DPV.Domain
    → DPV.Infrastructure

DPV.Infrastructure
    → DPV.Domain

DPV.Domain
    → none

DPV.Tests
    → DPV.App
    → DPV.UI
    → DPV.Domain
    → DPV.Infrastructure
```

Dependency direction:

```text
DPV.Tests
    → DPV.App
        → DPV.UI
            → DPV.Infrastructure
                → DPV.Domain
            → DPV.Domain
        → DPV.Infrastructure
            → DPV.Domain
        → DPV.Domain
```

There is no project cycle.

DPV.Domain has no project references and no package references.

Package references that are present are outside the project graph:

```text
DPV.UI
    Avalonia
    Avalonia.Desktop
    Avalonia.Themes.Fluent
    Avalonia.Fonts.Inter
    AvaloniaUI.DiagnosticsSupport

DPV.Tests
    xunit
    xunit.runner.visualstudio
    Microsoft.NET.Test.Sdk
    coverlet.collector
```

DPV.App and DPV.Infrastructure have no package references.

---

# 3. Allowed Dependencies

These project references are approved.

## DPV.App

```text
DPV.UI

DPV.Domain

DPV.Infrastructure
```

## DPV.UI

```text
DPV.Domain

DPV.Infrastructure
```

## DPV.Infrastructure

```text
DPV.Domain
```

## DPV.Domain

```text
None
```

## DPV.Tests

```text
DPV.App

DPV.UI

DPV.Domain

DPV.Infrastructure
```

Approved ownership stays inside the project that owns it:

```text
DPV.App
    Startup, configuration, dependency injection, logging bootstrap

DPV.UI
    Views, view models, navigation, themes, commands, converters

DPV.Domain
    Entities, enums, value objects, contracts, validation

DPV.Infrastructure
    Persistence, import, search, viewer, thumbnailing, duplicates

DPV.Tests
    Unit, integration, architecture
```

---

# 4. Forbidden Dependencies

These project references are not approved.

```text
DPV.Domain
    → any project

DPV.Infrastructure
    → DPV.App
    → DPV.UI
    → DPV.Tests

DPV.UI
    → DPV.App
    → DPV.Tests

DPV.App
    → DPV.Tests

DPV.App, DPV.UI, DPV.Domain, DPV.Infrastructure
    → DPV.Tests
```

DPV.Domain must remain dependency free. It must not reference:

```text
DPV.UI

SQLite

File System

Helix Toolkit
```

DPV.App must not own:

```text
SQL

File Scanning

Renderer Logic

Business Rules
```

DPV.UI must not own:

```text
SQL

SQLite Access

File IO

Hashing

Thumbnail Generation

Import Logic

Business Logic
```

The following solution shapes are prohibited for the MVP:

```text
Microservices

Separate Search Service

Separate Analytics Project

Separate Duplicate Project

Premature Feature Assemblies
```

Verified result: none of the forbidden project references are present.

---

# 5. Future Extraction Strategy

Infrastructure modules stay inside DPV.Infrastructure until extraction is justified.

Candidate projects:

```text
DPV.Search

DPV.Viewer

DPV.Duplicates

DPV.Analytics
```

Current locations, which remain inside DPV.Infrastructure:

```text
Search/          future DPV.Search

Viewer/          future DPV.Viewer

Duplicates/      future DPV.Duplicates
```

DPV.Analytics has no MVP project and no MVP folder. It stays deferred.

A module may become its own project only when:

- Complexity justifies extraction
- Clear boundaries exist
- Maintenance burden is reduced
- Architecture review approves extraction

Extraction also requires:

```text
Architecture Review

REA

Approval
```

Until that approval, search, viewer, duplicate detection, and thumbnailing remain folders in DPV.Infrastructure. They reference DPV.Domain through DPV.Infrastructure. They do not become separate assemblies.

---

# 6. Dependency Diagram

```text
DPV.App
│
├── DPV.UI
│   │
│   ├── DPV.Domain
│   │
│   └── DPV.Infrastructure
│       │
│       └── DPV.Domain
│
├── DPV.Infrastructure
│   │
│   └── DPV.Domain
│
└── DPV.Domain

DPV.Tests
    │
    ├── DPV.App
    ├── DPV.UI
    ├── DPV.Domain
    └── DPV.Infrastructure
```

DPV.Domain appears on more than one branch because DPV.App, DPV.UI, and DPV.Infrastructure each reference it directly. Those edges point at the same project. They do not create a cycle.

---

# End of Document
