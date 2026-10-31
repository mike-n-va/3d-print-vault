# DI-CONFIGURATION-001
## Dependency Injection Configuration Specification
### Version 1.0
### Status: Draft
### Phase: Commit C - Configuration

---

# Purpose

This document defines the approved configuration for the Dependency Injection architecture.

This document follows:

- DI-FOUNDATION-001
- DI-DEFINITIONS-001
- REA-006-Dependency-Injection-Bootstrap.md
- DPV-CONFIGURATION-001.md
- TESTING-CONFIGURATION-001.md

This document establishes:

```text
Approved Packages

Host Configuration

Provider Configuration

Registration Configuration

Validation Configuration

Architecture Enforcement Configuration

Verification Configuration
```

No implementation is authorized.

---

# Architectural Preconditions

This configuration assumes successful completion of:

```text
DI-FOUNDATION-001
Gate 0
```

including:

```text
Approved Process Ownership

Approved Composition Ownership

Approved Dependency Map Updates

Approved Project Definition Updates
```

---

# Approved Dependency Injection Stack

## Dependency Injection

Approved:

```text
Microsoft.Extensions.DependencyInjection
```

---

Purpose:

```text
Service Registration

Service Resolution

Lifetime Management
```

---

## Hosting

Approved:

```text
Microsoft.Extensions.Hosting
```

---

Purpose:

```text
Host Lifecycle

Application Lifetime

Configuration

Logging

Hosted Services
```

---

## Approved Builder Pattern

Approved:

```text
Host.CreateApplicationBuilder
```

for .NET 10 application startup.

---

## Configuration

Approved:

```text
Microsoft.Extensions.Configuration
```

---

Purpose:

```text
Configuration Loading

Configuration Binding

Configuration Composition
```

---

## Options

Approved:

```text
Microsoft.Extensions.Options
```

---

Purpose:

```text
Strongly Typed Configuration

Options Validation
```

---

# Logging Configuration

## Approved Logging Stack

Approved:

```text
Serilog

Serilog.Extensions.Hosting
```

---

Logging standards shall follow:

```text
DPV-CONFIGURATION-001
```

---

## Logging Sequence

```text
Bootstrap Logging
        ↓
Host Logging
        ↓
Application Logging
```

---

Requirement:

Startup failures shall be recordable before full host availability.

---

# Package Governance

## Approved Packages

```text
Microsoft.Extensions.DependencyInjection

Microsoft.Extensions.Hosting

Microsoft.Extensions.Configuration

Microsoft.Extensions.Options
```

---

## Deferred

```text
Scrutor

Autofac

DryIoc

Lamar
```

---

## Prohibited

```text
Alternative DI Containers

Assembly Scanning Frameworks

Service Locator Frameworks
```

without architecture approval.

---

# Package Placement Configuration

## Approved Projects

The following projects may reference approved dependency injection libraries when required by approved registration ownership:

```text
DPV.App

DPV.UI

DPV.Infrastructure
```

---

## Prohibited

```text
DPV.Domain
```

shall not reference:

```text
Microsoft.Extensions.DependencyInjection

Microsoft.Extensions.Hosting

Microsoft.Extensions.Configuration

Microsoft.Extensions.Options

Avalonia

Infrastructure Packages
```

---

# Composition Configuration

## Owner

```text
DPV.App
```

---

## Quantity

Approved:

```text
One Composition Root
```

per process.

---

## Enforcement

Architecture validation shall verify:

```text
Single Composition Root
```

---

# Process Entry Configuration

## Owner

```text
DPV.App
```

---

## Quantity

Approved:

```text
One Desktop Entry Point
```

---

## Restrictions

DPV.UI shall not own:

```text
Program.Main

Host Startup

Provider Creation
```

following Gate 0 completion.

---

# Host Configuration

## Model

Approved:

```text
Generic Host
```

---

## Quantity

Approved:

```text
One Host
```

per process.

---

## Responsibilities

The host manages:

```text
Dependency Injection

Configuration

Logging

Application Lifetime

Hosted Services
```

---

# Avalonia Integration Configuration

## Lifecycle Ownership

Avalonia owns:

```text
UI Thread

Message Loop

Window Lifetime

Application Events
```

---

Generic Host owns:

```text
Dependency Injection

Configuration

Logging

Hosted Services
```

---

## Application Creation

The root Avalonia application shall be created:

```text
After Configuration Validation

After Dependency Validation

After Host Creation
```

---

Only:

```text
DPV.App
```

may resolve or create the root application instance through the approved composition boundary.

---

## Prohibited

```text
IHost.Run()
```

shall not be used as a competing desktop message loop.

---

# Service Provider Configuration

## Quantity

Approved:

```text
One Service Provider
```

per process.

---

## Lifecycle

Provider shall be:

```text
Built Once

Validated Once

Disposed Once
```

---

## Prohibited

```text
Secondary Providers

Nested Providers

Feature Providers

Ad-Hoc Providers
```

---

## Boundary Rule

```text
BuildServiceProvider
```

shall not be executed outside approved composition-root boundaries.

---

# Validation Configuration

## Mandatory Validation

```text
ValidateOnBuild

ValidateScopes
```

---

## Execution Timing

Validation shall occur before:

```text
Application Creation

Shell Creation

Navigation Initialization
```

---

## Additional Validation

```text
Options Validation

Root Resolution Validation

Startup Validation

Shutdown Validation
```

---

## Limitation Statement

Validation does not prove:

```text
Database Availability

Configuration Correctness

Filesystem Accessibility

Factory Completeness

Runtime Behavior
```

Additional testing remains required.

---

# Options Configuration

## Sources

Configuration shall support:

```text
appsettings.json

appsettings.Development.json
```

where approved.

---

## Binding

Configuration shall be bound through:

```text
Microsoft.Extensions.Options
```

---

## Required Validation

Options validation shall occur before:

```text
Application Creation
```

---

## Ownership

Configuration ownership remains governed by:

```text
DPV-CONFIGURATION-001
```

including:

```text
Database

Scanning

Viewer

Logging
```

configuration sections.

---

## Path Governance

Approved application-data and content-root strategy shall follow:

```text
DPV-CONFIGURATION-001
```

including user-data storage rules.

---

# Registration Configuration

## Approved Strategy

```text
Hybrid Explicit Registration
```

---

## Registration Modules

Approved:

```text
DPV.App

DPV.UI

DPV.Infrastructure
```

---

## Registration Ownership

### DPV.App

```text
Host Services

Startup Services

Composition Services
```

---

### DPV.UI

```text
ViewModels

Navigation

Dialogs

Presentation Services
```

---

### DPV.Infrastructure

```text
Persistence

Repositories

Import

Search

Storage
```

---

# Registration Method Configuration

## Required Characteristics

Registration methods shall remain:

```text
Explicit

Deterministic

Reviewable
```

---

## Allowed Behavior

Registration methods may:

```text
Register Services

Register Options

Register Factories
```

---

## Prohibited Behavior

Registration methods shall not:

```text
Build Providers

Perform File I/O

Perform Database I/O

Run Migrations

Execute Startup Logic

Mutate Global State
```

---

# Registration Orchestration Configuration

## Owner

```text
DPV.App
```

---

## Requirement

All registrations shall flow through:

```text
One Registration Orchestrator
```

---

## Requirement

Registration ordering shall remain:

```text
Explicit

Visible

Reviewable
```

---

# Resolution Configuration

## Approved

```text
Constructor Injection
```

---

## Approved

```text
Typed Factory Resolution
```

---

## Prohibited

```text
Service Locator

Static Service Access

Application.Current Resolution

Ambient IServiceProvider Access

String-Based Resolution

Injecting IServiceProvider Into Feature Services
```

except narrowly approved framework integration points.

---

# Navigation Configuration

## Owner

```text
DPV.UI
```

---

## Approved

```text
Typed Navigation Factories

Explicit Navigation Requests
```

---

## Prohibited

```text
Navigation Service Locator

String-Based Service Lookup

Direct IServiceProvider Exposure
```

---

# View Configuration

## Approved

```text
Explicit View Mapping

Explicit Templates

Explicit Factories
```

---

## Prohibited

```text
Reflection-Based View Locator
```

for dependency-injected views.

---

# Lifetime Configuration

## Alignment

This document supplements:

```text
DPV-CONFIGURATION-001
```

and clarifies dependency injection semantics.

---

## Singleton

Approved uses:

```text
Configuration Infrastructure

Logging Infrastructure

SQLite Connection Factory

Navigation Coordinator
```

---

## Restriction

```text
SQLite Connections
```

shall not be singleton.

---

## Transient

Approved uses:

```text
ViewModels

Repositories

Search Services

Detection Services

Operations
```

---

## Import Processing

Recommended:

```text
Import Coordinators
    = Singleton Orchestrators

Import Jobs
    = Scoped or Transient
```

depending on operation boundaries.

---

## Scoped

Approved only for:

```text
Import Jobs

Batch Operations

Future Unit Of Work Boundaries
```

---

## MVP Position

Scoped services are:

```text
Supported

Optional

Not Required For Initial Startup
```

---

## Prohibited

Implicit scopes for:

```text
Navigation

Window Lifetime

Dialog Lifetime
```

without architecture approval.

---

# Startup Configuration

## Approved Sequence

```text
Process Entry
        ↓
Bootstrap Logging
        ↓
Create Host Builder
        ↓
Configure Services
        ↓
Configure Options
        ↓
Validate Configuration
        ↓
Validate Dependencies
        ↓
Initialize Database
        ↓
Create Application
        ↓
Create Shell
        ↓
Display Shell
        ↓
Background Work
```

---

## Deferred Activities

```text
Library Scanning

Thumbnail Generation

Search Index Creation

Catalog Processing
```

until the application shell is visible.

---

# Hosted Service Configuration

## Pre-Shell

Only startup-critical services may execute before shell creation.

---

## Post-Shell

Long-running operations shall begin only after:

```text
Application Shell Visible
```

---

Examples:

```text
Library Scanning

Thumbnail Generation

Search Index Generation

Catalog Refresh Operations
```

---

# Shutdown Configuration

## Approved Sequence

```text
Avalonia Exit
        ↓
Cancel Operations
        ↓
Stop Hosted Services
        ↓
Stop Host
        ↓
Dispose Host
        ↓
Dispose Provider
        ↓
Exit Process
```

---

# Architecture Enforcement Configuration

Future architecture tests shall verify:

```text
Single Composition Root

Single Process Entry

Single Host

Single Service Provider

Host Ownership

Provider Ownership

Registration Purity

No BuildServiceProvider Outside App

No Service Locator

Domain DI Purity
```

---

## Domain Purity Requirements

Architecture validation shall ensure Domain remains free of:

```text
Microsoft.Extensions.DependencyInjection

Microsoft.Extensions.Hosting

Microsoft.Extensions.Configuration

Microsoft.Extensions.Options

Avalonia

Infrastructure
```

dependencies.

---

# AI Governance Configuration

Generated implementations shall not:

```text
Create Secondary Providers

Use Assembly Scanning

Introduce Alternative Containers

Create Singleton SQLite Connections

Use Service Locator Patterns

Inject IServiceProvider Into Feature Services

Perform Startup I/O In Registration Methods

Run Migrations During Registration
```

---

# Verification Requirements

## Commit D

Implementation verification shall demonstrate:

```text
Provider Validation

Host Validation

Registration Validation

Startup Validation
```

---

## Commit F

Verification shall demonstrate:

```text
Architecture Compliance

Container Compliance

Validation Compliance
```

---

## Required Validation Commands

```text
dotnet restore

dotnet build

dotnet test

dotnet build -c Release

dotnet test -c Release
```

---

Both Debug and Release verification are required.

---

# Success Criteria

Configuration succeeds when:

```text
Package Choices Approved

Package Placement Approved

Validation Strategy Approved

Lifetime Strategy Approved

Architecture Rules Approved

Verification Rules Approved

Governance Rules Approved
```

---

# Approval Gate

Before Commit D:

```text
Configuration Review
        ↓
Approval
        ↓
Commit
        ↓
Push
```

---

# End of Document