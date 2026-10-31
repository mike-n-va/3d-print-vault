# DI-FOUNDATION-001
## Dependency Injection Foundation Contract
### Version 1.0
### Status: Draft
### Phase: Commit A - Contracts

---

# Purpose

This document defines the approved dependency injection, composition root, application bootstrap, and service ownership architecture for 3D Print Vault.

This contract follows:

- DEVELOPMENT_STANDARD.md
- DPV-SOLUTION-001.md
- DPV-DEPENDENCY-MAP-001.md
- DPV-CONFIGURATION-001.md
- DPV-PROJECT-DEFINITIONS-001.md
- TESTING-FOUNDATION-001.md
- TESTING-CONFIGURATION-001.md
- REA-006-Dependency-Injection-Bootstrap.md

This document establishes architectural authority.

No implementation is authorized.

---

# Architectural Status

## REA-006 Outcome

REA-006 was approved.

However, REA-006 identified an architectural mismatch between:

```text
Documented Ownership
```

and

```text
Current Implementation
```

This contract records the approved future-state architecture.

Current implementation is not considered authoritative where conflicts exist.

---

# Gate 0 - Architectural Alignment

Before implementation begins:

The architectural ownership discrepancy identified by REA-006 shall be resolved.

Current state:

```text
DPV.UI
owns Avalonia startup.
```

Target state:

```text
DPV.App
owns application startup.
```

Before Commit D:

Required artifacts shall be updated:

```text
DPV-DEPENDENCY-MAP-001

DPV-PROJECT-DEFINITIONS-001

Solution Documentation
```

to reflect approved ownership.

Implementation shall not proceed until architectural ownership is formally approved.

---

# Objectives

The dependency injection architecture shall:

```text
Support Testing

Support Architecture Enforcement

Support AI-Assisted Development

Support Explicit Dependencies

Support Deterministic Startup

Support Deterministic Shutdown

Support Future Module Expansion
```

while maintaining:

```text
Domain Purity

Architecture Governance

Predictable Lifetimes
```

---

# Process Entry Ownership

## Rule

The application shall contain:

```text
One Desktop Entry Point
```

per process.

---

## Ownership

Process ownership belongs to:

```text
DPV.App
```

---

## Future State

Following approved implementation:

```text
DPV.App
```

owns:

```text
Program.Main

Application Startup

Host Creation

Dependency Injection Bootstrap

Application Shutdown
```

---

## Prohibited

DPV.UI shall not own:

```text
Program.Main

Host Construction

Service Provider Construction

Application Bootstrap
```

following architectural migration.

---

# Composition Root

## Rule

The application shall contain:

```text
One Composition Root
```

per running process.

---

## Ownership

Composition ownership belongs exclusively to:

```text
DPV.App
```

---

## Responsibilities

The composition root owns:

```text
Host Creation

Service Registration Orchestration

Configuration Bootstrap

Logging Bootstrap

Startup Coordination

Shutdown Coordination
```

---

## Prohibited

The following are prohibited:

```text
Multiple Composition Roots

Feature Composition Roots

Ad-Hoc Startup Pipelines

Independent Module Startup
```

---

# Host Ownership

## Approved Host

```text
Microsoft Generic Host
```

---

## Ownership

Host ownership belongs to:

```text
DPV.App
```

---

## Responsibilities

The host owns:

```text
Dependency Injection

Configuration

Options

Logging

Resource Lifetime

Background Services
```

---

# Host and UI Lifecycle

## Rule

Host ownership and UI ownership are separate concerns.

---

## Host Owns

```text
Dependency Injection

Configuration

Logging

Background Services
```

---

## Avalonia Owns

```text
Window Lifecycle

UI Thread

Message Loop

Application Lifetime Events
```

---

## Requirements

The host shall integrate with Avalonia lifecycle events.

Shutdown shall be coordinated.

---

## Prohibited

```text
Competing Application Lifecycles

Independent Process Loops

Startup Ownership Split Between Projects
```

---

Specifically prohibited:

```text
IHost.Run()
```

as a competing desktop message loop.

---

# Service Provider

## Rule

The application shall contain:

```text
One Service Provider
```

per running process.

---

## Requirements

The provider shall be:

```text
Built Once

Validated Once

Disposed Once
```

---

## Prohibited

```text
Secondary Providers

Feature Providers

Nested Providers

Ad-Hoc Providers
```

---

# Dependency Injection Standards

## Approved Container

```text
Microsoft.Extensions.DependencyInjection
```

---

## Approved Host Framework

```text
Microsoft.Extensions.Hosting
```

---

## Approved Pattern

```text
Constructor Injection
```

---

## Approved Pattern

```text
Typed Factories
```

where runtime creation is required.

---

## Prohibited

```text
Service Locator

Static Provider Access

Application.Current Resolution

Global Service Resolution

Ambient Service Resolution
```

---

# Project Responsibilities

## DPV.App

Owns:

```text
Composition Root

Host

Startup

Shutdown

Module Registration Orchestration
```

May reference:

```text
DPV.UI

DPV.Infrastructure

DPV.Domain
```

---

## DPV.UI

Owns:

```text
Views

ViewModels

Navigation

Presentation Services

Window Factories
```

Does not own:

```text
Host

Logging Bootstrap

Application Composition

Database Bootstrap
```

---

## DPV.Infrastructure

Owns:

```text
SQLite

Repositories

File System

Search Services

Import Services
```

Does not own:

```text
Host

Application Startup

Application Shutdown
```

---

## DPV.Domain

Owns:

```text
Business Rules

Entities

Value Objects

Contracts
```

Must remain free of:

```text
Dependency Injection

Hosting

Avalonia

Configuration

Logging

Infrastructure
```

---

# Registration Strategy

## Approved Pattern

```text
Hybrid Explicit Registration
```

---

## Structure

DPV.App selects modules.

Modules register their own services.

DPV.App orchestrates registration order.

---

## Registration Purity

Registration methods may:

```text
Add Services

Add Options

Add Factories
```

---

Registration methods shall not:

```text
BuildServiceProvider

Perform File I/O

Perform Database I/O

Start Services

Run Migrations

Perform Startup Work

Mutate Global State
```

---

## Root Orchestration

DPV.App shall contain:

```text
One Central Registration Method
```

listing all module registrations.

---

## Prohibited

```text
Assembly Scanning

Convention Registration

Reflection Registration
```

for MVP architecture.

---

# ViewModel and View Resolution

## ViewModels

ViewModels shall be resolved through:

```text
Dependency Injection

Typed Factories
```

---

ViewModels shall not:

```text
Construct Dependencies Directly
```

---

## Views

Injected Views shall use:

```text
Explicit Factories

Explicit Templates

Explicit View Creation
```

---

## Prohibited

Reflection-based ViewLocator resolution shall not be used as a dependency injection mechanism.

---

# Navigation

Navigation ownership belongs to:

```text
DPV.UI
```

---

Navigation shall use:

```text
Typed Factories

Explicit Navigation Requests
```

---

Navigation shall not:

```text
Expose IServiceProvider

Use String-Based Service Resolution

Act As A Service Locator
```

---

# Lifetime Ownership

Approved lifetimes:

```text
Singleton

Transient

Scoped
```

---

## Singleton

Use for:

```text
Configuration Infrastructure

Logging Infrastructure

Stateless Factories

Application-Wide Coordinators
```

---

## Transient

Use for:

```text
ViewModels

Operation Services

Short-Lived Services
```

---

## Scoped

Reserved for:

```text
Explicit Operation Scopes

Import Jobs

Future Unit Of Work Boundaries
```

---

Scoped lifetimes are:

```text
Supported

Not Required For MVP Startup
```

---

Navigation shall not create implicit scopes.

---

# Configuration Ownership

Configuration ownership belongs to:

```text
DPV.App
```

---

Configuration responsibilities:

```text
Options Binding

Options Validation

Configuration Source Selection
```

---

Configuration shall be validated before dependent services execute.

---

# Logging Ownership

Logging ownership belongs to:

```text
DPV.App
```

---

Requirements:

```text
Bootstrap Logging

Host Logging

Application Logging
```

shall be established in a deterministic sequence.

---

# Database Initialization

DPV.App owns:

```text
Initialization Orchestration
```

---

DPV.Infrastructure owns:

```text
Database Implementation
```

---

Startup may:

```text
Validate Database

Create Database

Run Required Migrations
```

---

Startup shall not:

```text
Scan Libraries

Build Search Indexes

Generate Thumbnails

Execute Long Running Jobs
```

before the shell becomes available.

---

# Startup Responsibilities

Startup may:

```text
Load Configuration

Initialize Logging

Build Host

Validate Services

Validate Configuration

Initialize Database

Create UI
```

---

Startup shall remain:

```text
Bounded

Deterministic

Recoverable
```

---

# Shutdown Responsibilities

Shutdown shall:

```text
Cancel Operations

Stop Services

Flush Logs

Dispose Resources

Dispose Service Provider
```

in a controlled sequence.

---

# Dependency Validation

The composition root shall enable:

```text
ValidateOnBuild

ValidateScopes
```

during verification.

---

## Limitation

Dependency validation is not considered exhaustive proof of runtime correctness.

Additional testing remains required.

---

# Testing Requirements

The dependency injection architecture shall support:

```text
Composition Tests

Container Validation

Options Validation

Lifetime Validation

Architecture Validation

Shutdown Validation
```

---

Future verification must validate:

```text
Production Registration Path

Root Resolution

Options Validation

Provider Validation
```

---

# AI Governance

Generated solutions shall follow approved registration patterns.

---

## Prohibited AI Patterns

```text
Service Locator

Multiple Providers

Assembly Scanning

Convention Registration

Global Service Access

Singleton SQLite Connections

Startup I/O In Registration Methods
```

---

## Required Metadata

New services shall declare:

```text
Owner

Lifetime

Registration Module

Validation Strategy
```

---

# Architecture Rules

## Rule 001

Only:

```text
DPV.App
```

may own the composition root.

---

## Rule 002

Only:

```text
DPV.App
```

may create the host.

---

## Rule 003

Only:

```text
DPV.App
```

may build the service provider.

---

## Rule 004

DPV.Domain shall remain DI-free.

---

## Rule 005

Service locator usage is prohibited.

---

## Planned Enforcement

Future architecture tests shall verify:

```text
Host Ownership

Provider Ownership

Domain Purity

Registration Boundaries
```

---

# Future Expansion

Future modules may participate through:

```text
Explicit Module Registration
```

only.

---

No plugin framework is authorized.

---

No runtime module discovery is authorized.

---

# Success Criteria

This contract succeeds when:

```text
Composition Ownership Defined

Host Ownership Defined

Startup Ownership Defined

Shutdown Ownership Defined

Registration Rules Defined

Validation Rules Defined

AI Constraints Defined
```

---

# Approval Gate

Before Commit B:

```text
Contract Review
        ↓
Approval
        ↓
Commit
        ↓
Push
```

---

# End of Document