# DI-DEFINITIONS-001
## Dependency Injection Definitions & Resolver Specification
### Version 1.0
### Status: Draft
### Phase: Commit B - Definition / Resolver

---

# Purpose

This document defines the operational behavior of the Dependency Injection architecture approved by:

- DI-FOUNDATION-001
- REA-006-Dependency-Injection-Bootstrap
- DPV-CONFIGURATION-001
- DPV-DEPENDENCY-MAP-001

This document defines:

- Bootstrap responsibilities
- Module registration contracts
- Resolver definitions
- Lifetime definitions
- Startup sequence
- Shutdown sequence

No implementation is authorized.

---

# Relationship to DI-FOUNDATION-001

Operational definitions in this document implement the contract in DI-FOUNDATION-001.

Where DI-FOUNDATION-001 defines Gate 0 architectural alignment, host and UI lifecycle rules, registration purity, and validation policy, this document defines the corresponding resolver and sequence behavior.

If this document conflicts with DI-FOUNDATION-001, DI-FOUNDATION-001 takes precedence.

---

# Architecture Model

The application consists of:

```text
One Host

One Service Provider

One Composition Root
```

---

# Composition Root Definition

## Owner

```text
DPV.App
```

---

## Responsibilities

The composition root shall:

```text
Create Host Builder

Configure Services

Configure Configuration

Configure Logging

Validate Dependencies

Create Application

Coordinate Shutdown
```

---

## Forbidden Responsibilities

The composition root shall not:

```text
Contain Business Logic

Contain SQL

Contain Import Logic

Contain Search Logic
```

---

# Host Definition

## Owner

```text
DPV.App
```

---

## Purpose

The host owns:

```text
Configuration

Logging

Dependency Injection

Hosted Services

Resource Disposal
```

---

## Lifetime

```text
Application Lifetime
```

---

# Module Registration Definition

## Purpose

Modules provide service registrations.

Modules do not provide application startup.

---

## Approved Modules

```text
DPV.UI

DPV.Infrastructure

DPV.App
```

---

## Registration Contract

Modules may:

```text
Register Services

Register Options

Register Factories
```

---

Modules shall not:

```text
Build Providers

Perform I/O

Run Database Operations

Execute Startup Logic
```

---

# Registration Ownership

## DPV.UI

Owns registration of:

```text
ViewModels

Navigation

Dialogs

Window Factories
```

---

## DPV.Infrastructure

Owns registration of:

```text
Repositories

SQLite

Import Services

Search Services

Storage Services
```

---

## DPV.App

Owns registration of:

```text
Application Bootstrap

Host Services

Startup Coordinators
```

---

# Service Resolution Definition

## Approved Resolution

```text
Constructor Injection
```

---

## Approved Resolution

```text
Typed Factory Resolution
```

for runtime-created objects.

---

## Prohibited Resolution

```text
IServiceProvider Service Location

Static Provider Access

Application.Current Resolution

String-Based Resolution
```

---

# ViewModel Definition

## Purpose

ViewModels define presentation behavior.

---

## Creation

ViewModels shall be:

```text
Resolved By DI
```

or:

```text
Created By Typed Factories
```

---

## Lifecycle

Default:

```text
Transient
```

---

## Exception

Shell ViewModels may be:

```text
Singleton

Window-Lifetime
```

when approved.

---

# View Definition

## Default Strategy

Views use:

```text
Explicit Templates

Explicit Mapping
```

---

## Prohibited

```text
Reflection-Based View Resolution
```

for dependency-injected views.

---

# Navigation Definition

## Owner

```text
DPV.UI
```

---

## Responsibilities

Navigation shall:

```text
Create ViewModels

Track Current Page

Coordinate Page Changes
```

---

## Restrictions

Navigation shall not:

```text
Expose IServiceProvider

Act As A Service Locator
```

---

# Factory Definition

## Purpose

Factories create runtime objects requiring parameters unavailable to DI.

---

## Examples

```text
Dialog Factories

Import Job Factories

Navigation Factories
```

---

## Restrictions

Factories shall remain:

```text
Explicit

Typed

Testable
```

---

# Lifetime Definitions

## Singleton

Purpose:

```text
Application-Wide Resources
```

Examples:

```text
Configuration Infrastructure

Logging Infrastructure

Navigation Coordinator
```

---

## Transient

Purpose:

```text
Short-Lived Objects
```

Examples:

```text
ViewModels

Repositories

Operations
```

---

## Scoped

Purpose:

```text
Explicit Operation Boundaries
```

Examples:

```text
Import Session

Future Unit Of Work
```

---

## Restrictions

Scopes shall not be created:

```text
Per Navigation Request

Per Window
```

without approval.

---

# Configuration Definition

## Owner

```text
DPV.App
```

---

## Responsibilities

```text
Load Configuration

Bind Options

Validate Options
```

---

## Contract

Services consume:

```text
Options
```

rather than raw configuration whenever practical.

---

# Logging Definition

## Owner

```text
DPV.App
```

---

## Requirements

Two stages:

```text
Bootstrap Logging

Host Logging
```

---

## Purpose

Startup failures must be recorded before the host is fully available.

---

# Database Initialization Definition

## Owner

Orchestration:

```text
DPV.App
```

Implementation:

```text
DPV.Infrastructure
```

---

## Startup Activities

Approved:

```text
Database Existence Check

Required Migration Check

Schema Validation
```

---

## Deferred Activities

```text
Library Scan

Thumbnail Generation

Search Index Generation
```

---

# Startup Sequence Definition

## Ordered Sequence

```text
Process Entry
        ↓
Bootstrap Logging
        ↓
Create Host Builder
        ↓
Configure Services
        ↓
Validate Configuration
        ↓
Validate Dependencies
        ↓
Initialize Database
        ↓
Create UI
        ↓
Display Shell
        ↓
Background Work
```

---

# Shutdown Sequence Definition

## Ordered Sequence

```text
Cancel Operations
        ↓
Stop Hosted Services
        ↓
Flush Logs
        ↓
Dispose Provider
        ↓
Exit Process
```

---

# Validation Definition

## Required Validation

```text
ValidateOnBuild

ValidateScopes
```

---

## Additional Validation

```text
Options Validation

Root Resolution Validation

Startup Validation
```

---

# Architecture Enforcement Targets

Future architecture tests shall verify:

```text
Single Composition Root

Single Host

Single Provider

No Service Locator

Domain DI Purity
```

---

# Future Extension Definition

Future modules must integrate through:

```text
Explicit Registration

Explicit Contracts

Architecture Review
```

---

## Not Approved

```text
Runtime Plugin Loading

Automatic Discovery

Assembly Scanning
```

---

# Success Criteria

Definitions succeed when:

```text
Resolver Behavior Defined

Lifetime Behavior Defined

Registration Ownership Defined

Startup Defined

Shutdown Defined

Validation Defined
```

---

# Approval Gate

Before Commit C:

```text
Definition Review
        ↓
Approval
        ↓
Commit
        ↓
Push
```

---

# End of Document
