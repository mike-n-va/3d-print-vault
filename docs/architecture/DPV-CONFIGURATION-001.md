# DPV-CONFIGURATION-001
## Evaluation & Configuration Specification
### Version 1.0

---

# Purpose

This document defines project-wide configuration standards.

It does not authorize feature or persistence implementation.

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

### Options Ownership and Issue #6 Configuration

Configuration options and their binding belong to `DPV.App`, the approved
composition root. `OptionsRegistration` binds the application, database,
scanning, and viewer sections and validates required values at host startup.
Configuration is not a Domain concern and does not change project
dependencies.

The current Issue #6 configuration contract is intentionally limited:

| Section | Current configuration | Boundary |
|---|---|---|
| `Database` | Required `Location` string. Production configuration retains `%AppData%/3DPrintVault/`; Development retains `/data/dev.db`. | `DPV.App` owns the option and configuration value. The Issue #8 Infrastructure bootstrap expands `%AppData%`, normalizes the configured path, treats a trailing separator as a database directory (using `3d-print-vault.db` within it), and creates parent directories only when the host starts. A location without a trailing separator is treated as a database file path. |
| `Scanning` | Section-only options object. | No scanning properties are defined here because approved requirements do not yet specify them. Do not infer roots, concurrency, batching, or scanner behavior. |
| `Viewer` | Section-only options object. | No viewer properties are defined here because no runtime viewer settings are approved. Helix compatibility and rendering remain separately gated. |

The development database location is retained from the existing contract and
is not portable to the Windows MVP target. `DPV-DATA-001.md` explicitly
requires this location to be corrected or approved in a later configuration
decision; this Issue #6 implementation does not silently change it.

The production and development settings files must contain all four
configuration sections. Adding an options property requires an approved
requirement and matching validation/binding tests. These section-only
Scanning and Viewer types establish configuration boundaries only; they do
not implement scanning, rendering, or other feature behavior.

### Issue #6 Verification Evidence

Issue #6 configuration verification is implemented in
`tests/DPV.Tests/Integration/DependencyInjection/CompositionRootIntegrationTests.cs`:

- The integration registration path resolves all four options types.
- The production and development JSON files contain the Database, Scanning,
  Viewer, and Logging sections.
- Both JSON files bind their configured database location and the section-only
  Scanning and Viewer options.
- An empty database location fails host-start validation.

The configuration types are in `src/DPV.App/Configuration/`, and binding is
performed by `OptionsRegistration`. These checks verify configuration
structure and binding; they do not prove database creation, path resolution,
scanning, or viewer behavior.

Verification on 2026-11-01:

| Command | Result |
|---|---|
| `dotnet build DPV.sln --configuration Release --no-restore` | PASS — 0 warnings, 0 errors. |
| `dotnet test tests/DPV.Tests/DPV.Tests.csproj --configuration Release --no-build --filter FullyQualifiedName~CompositionRootIntegrationTests` | PASS — 9 passed, 0 failed. |
| `dotnet test DPV.sln --configuration Release --no-build` | PASS — 33 passed, 0 failed, 0 skipped after Issue #8 bootstrap verification. |

---

# Issue #8 SQLite Bootstrap

SQLite bootstrap is owned by `DPV.Infrastructure` and registered through
`InfrastructureServiceCollectionExtensions`. `Database:Location` is read
when the connection factory is first resolved, so final host configuration
overrides are honored. Dependency registration itself performs no filesystem
or database I/O.

On host startup, `DatabaseInitializer` creates the database's parent
directory, opens a short-lived SQLite connection, enables and verifies WAL,
and applies the migration baseline. New connections enable SQLite foreign
keys. The connection factory owns connection-string construction and returns
a new connection for each request.

`MigrationRunner` currently establishes schema version 1 and a
`DPVSchemaMigrations` ledger. This is bootstrap/versioning groundwork only:
it adds no catalog entities or application tables and does not implement
repositories, catalog queries, or model-file storage. Future catalog schema
and data-access decisions remain subject to their existing approval gates.

Issue #8 integration verification is in
`tests/DPV.Tests/Integration/Persistence/SqliteBootstrapIntegrationTests.cs`.
It starts the composition-root host against unique temporary database files
and verifies deferred directory creation, database creation, a real usable
connection, foreign-key and WAL settings, the version ledger, idempotent
startup, and rejection of a database with an unsupported newer schema.
The host lifecycle integration test also uses and removes an isolated
temporary database path. The existing Development value `/data/dev.db` has
not been changed; its Windows portability remains unresolved as recorded
above and in `DPV-DATA-001.md`.

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

`DPV.App/Logging/BootstrapLogging.cs` initializes console logging before host
construction. `CompositionRootHostFactory` registers Serilog with the Generic
Host and loads sinks from `src/DPV.App/appsettings.json`. The configured file
sink writes daily rolling files using the `logs/dpv-.log` path relative to the
process working directory. `Program.Main` closes and flushes logging during
shutdown.

Issue #7 verification uses
`tests/DPV.Tests/Integration/Logging/FileLoggingIntegrationTests.cs` to:

- assert that production settings register Console and File sinks and use
  daily rolling file output under `logs/`;
- build and start the real App host with an isolated temporary file path,
  emit a uniquely identified log event through `ILoggerFactory`, stop the
  host, flush Serilog, and verify that the generated file contains the event.

The test uses only its own unique temporary directory and removes it after
verification. This verifies host logging registration and file output without
writing test logs into the repository or creating persistence behavior.

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