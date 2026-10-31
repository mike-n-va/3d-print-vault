# DI-DEFINITIONS-001
## Dependency Injection Definitions & Resolver Specification
### Version 1.0
### Status: Draft
### Phase: Commit B - Definitions

---

# Purpose

This document defines the operational behavior of the Dependency Injection architecture approved by:

- DI-FOUNDATION-001
- REA-006-Dependency-Injection-Bootstrap.md
- DPV-CONFIGURATION-001.md
- DPV-DEPENDENCY-MAP-001.md
- TESTING-CONFIGURATION-001.md

This document defines:

```text
Bootstrap Responsibilities

Composition Responsibilities

Registration Responsibilities

Resolver Definitions

Lifetime Definitions

Startup Definitions

Shutdown Definitions

Validation Definitions
```

No implementation is authorized.

---

# Relationship To DI-FOUNDATION-001

DI-FOUNDATION-001 remains the governing authority.

If a conflict exists between:

```text
DI-FOUNDATION-001
```

and:

```text
DI-DEFINITIONS-001
```

the Foundation Contract shall take precedence.

---

# Gate 0 Dependency

This document depends upon successful completion of:

```text
DI-FOUNDATION-001
Gate 0
```

Implementation shall not proceed until:

```text
Process Ownership Approved

Composition Ownership Approved

Dependency Map Updated

Project Definition Updates Approved
```

---

# Architecture Model

The application consists of:

```text
One Host

One Service Provider

One Composition Root

One Desktop Entry Point
```

per running process.

---

# Process Entry Definition

## Owner

```text
DPV.App
```

---

## Responsibilities

Process entry owns:

```text
Program.Main

Host Creation

Avalonia Initialization

Application Lifetime Coordination
```

---

## Restrictions

Following completion of Gate 0:

```text
DPV.UI
```

shall not own:

```text
Program.Main

Desktop Startup

Host Creation

Service Provider Creation
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

Create Service Provider

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

Contain Search Logic

Contain Import Logic

Contain Feature Logic
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
Dependency Injection

Configuration

Options

Logging

Hosted Services

Resource Disposal
```

---

## Lifetime

```text
Application Lifetime
```

---

# Host and UI Lifecycle Definition

## Host Ownership

The Generic Host owns:

```text
Dependency Injection

Configuration

Logging

Background Services

Application Resources
```

---

## Avalonia Ownership

Avalonia owns:

```text
UI Thread

Message Loop

Window Lifetime

Application Lifetime Events
```

---

## Lifecycle Coordination

Application shutdown shall coordinate:

```text
Avalonia Exit
        ↓
Host Stop
        ↓
Hosted Service Shutdown
        ↓
Provider Disposal
        ↓
Process Exit
```

---

## Prohibited

The application shall not execute:

```text
IHost.Run()
```

as a competing desktop message loop.

---

# Application Creation Definition

The Avalonia application shall be created:

```text
After Configuration Validation

After Dependency Validation

After Host Creation
```

through the approved composition pathway.

---

The application shall not be created before host validation completes.

---

# Module Registration Definition

## Purpose

Modules provide registrations.

Modules do not provide startup.

---

## Approved Registration Modules

```text
DPV.App

DPV.UI

DPV.Infrastructure
```

---

## Registration Contract

Modules may:

```text
Register Services

Register Options

Register Factories

Register Navigation Components
```

---

Modules shall not:

```text
Build Service Providers

Perform File I/O

Perform Database I/O

Run Migrations

Perform Startup Logic

Start Hosted Services
```

---

# Registration Orchestration

## Owner

```text
DPV.App
```

---

## Requirement

DPV.App shall contain:

```text
One Central Registration Orchestrator
```

responsible for invoking approved module registrations.

---

## Requirement

Registration sequence shall remain:

```text
Visible

Explicit

Reviewable
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

Presentation Services
```

---

## DPV.Infrastructure

Owns registration of:

```text
Repositories

SQLite Services

Import Services

Search Services

Storage Services
```

---

## DPV.App

Owns registration of:

```text
Startup Services

Host Services

Composition Services

Bootstrap Coordinators
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
Typed Factories
```

for runtime-created objects.

---

## Prohibited Resolution

```text
IServiceProvider Service Location

Static Provider Access

Application.Current Resolution

String-Based Resolution

Ambient Resolution
```

---

# ViewModel Definition

## Creation

ViewModels shall be:

```text
Resolved By DI
```

or:

```text
Created Through Typed Factories
```

---

## Default Lifetime

```text
Transient
```

---

## Approved Exception

Shell ViewModels may be:

```text
Singleton

Window-Lifetime
```

when approved.

---

# View Definition

## Approved Strategy

Views may use:

```text
Explicit Templates

Explicit Type Mapping

Explicit View Factories
```

---

## Prohibited

```text
Reflection-Based ViewLocator Resolution
```

as a dependency injection mechanism.

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

Manage Current Page

Coordinate Page Changes
```

---

## Restrictions

Navigation shall not:

```text
Expose IServiceProvider

Act As A Service Locator

Perform Service Resolution By String
```

---

# Factory Definition

## Purpose

Factories create runtime objects requiring parameters unavailable to constructor injection.

---

## Examples

```text
Dialog Factories

Navigation Factories

Import Job Factories
```

---

## Requirements

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

SQLite Connection Factory
```

---

## Clarification

The factory is singleton.

Connections are not singleton.

---

## Transient

Purpose:

```text
Short-Lived Services
```

Examples:

```text
ViewModels

Repositories

Operations

Factory-Created Components
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

Batch Processing Operation

Future Unit Of Work
```

---

## Restrictions

Scopes shall not be created:

```text
Per Navigation Request

Per Window

Per Dialog
```

without explicit approval.

---

## MVP Guidance

Scoped services are:

```text
Supported

Not Required For MVP Startup
```

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

Select Configuration Sources
```

---

## Requirement

Services should consume:

```text
Options
```

instead of raw configuration whenever practical.

---

# Logging Definition

## Owner

```text
DPV.App
```

---

## Sequence

```text
Bootstrap Logging
        ↓
Host Logging
        ↓
Application Logging
```

---

## Purpose

Startup failures shall be recordable before full host availability.

---

# Database Initialization Definition

## Ownership

Orchestration:

```text
DPV.App
```

Implementation:

```text
DPV.Infrastructure
```

---

## Approved Startup Activities

```text
Database Existence Check

Migration Check

Required Migration Execution

Schema Validation
```

---

## Deferred Activities

```text
Library Scan

Thumbnail Generation

Search Index Construction

Catalog Processing
```

---

# Hosted Service Definition

## Pre-Shell Execution

Hosted services required for startup may execute before shell creation.

---

## Post-Shell Execution

Long-running operations shall execute only after:

```text
Application Shell Visible
```

---

Examples:

```text
Library Scanning

Thumbnail Generation

Search Index Construction

Catalog Refresh Operations
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

# Shutdown Sequence Definition

## Ordered Sequence

```text
Avalonia Exit
        ↓
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

## Mandatory Validation

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

Shutdown Validation
```

---

# Architecture Enforcement Targets

Future architecture tests shall verify:

```text
Single Composition Root

Single Host

Single Service Provider

No Service Locator

Domain DI Purity

Host Ownership

Provider Ownership

Registration Purity

No BuildServiceProvider Outside App
```

---

# AI Governance

## Prohibited Patterns

Generated implementations shall not:

```text
Build Additional Providers

Use Assembly Scanning

Use Convention Registration

Use Service Locator

Use Global Service Access

Create Singleton SQLite Connections

Perform Startup I/O In Registration Methods
```

---

## Required Metadata

New services shall define:

```text
Owner

Lifetime

Registration Module

Validation Strategy
```

---

# Future Extension Definition

Future modules shall integrate through:

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

Runtime Module Resolution
```

---

# Success Criteria

Definitions succeed when:

```text
Resolver Behavior Defined

Lifetime Behavior Defined

Registration Ownership Defined

Lifecycle Ownership Defined

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