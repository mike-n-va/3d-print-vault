# DPV-SOLUTION-001
## Solution Architecture Contract
### Version 1.0

---

# Purpose

This document defines the approved solution structure for 3D Print Vault.

No project creation may begin until this contract is approved.

This document is governed by:

- DEVELOPMENT_STANDARD.md
- REA-001-Solution-Architecture.md
- REA-001-REVIEW.md

---

# Contract Status

Status:

Draft

Phase:

Commit A - Contracts

Implementation Authorized:

No

---

# Architectural Principles

## Local First

The application must function fully offline.

No cloud dependency is required.

---

## Database First

SQLite is the authoritative source of truth.

The filesystem is not.

---

## Collection First

Primary hierarchy:

Artist
    →
Collection
    →
Model

All major user workflows are built around this hierarchy.

---

## Simplicity First

The MVP shall favor simplicity over premature modularization.

Projects may be extracted later when complexity justifies separation.

---

# Approved MVP Solution Structure

Source Projects:

DPV.App

DPV.UI

DPV.Domain

DPV.Infrastructure

Tests:

DPV.Tests

---

# Project Responsibilities

## DPV.App

Purpose:

Application host.

Contains:

- Startup
- Configuration
- Dependency Injection
- Logging Bootstrap

May Reference:

- DPV.Domain
- DPV.Infrastructure
- DPV.UI

---

## DPV.UI

Purpose:

User Interface.

Contains:

- Views
- ViewModels
- Navigation
- Themes

Must Not Contain:

- SQL
- File IO
- Business Logic

May Reference:

- DPV.Domain
- DPV.Infrastructure

---

## DPV.Domain

Purpose:

Business Domain.

Contains:

- Artist
- Collection
- Model
- Tag
- PrintHistory

Must Not Reference:

Any other project.

---

## DPV.Infrastructure

Purpose:

External systems and implementation services.

Contains:

Import

Search

Viewer

Thumbnailing

Duplicates

Persistence

Contains:

- SQLite
- Dapper
- Search
- File Access
- Hashing
- Thumbnail Rendering
- Viewer Services

May Reference:

- DPV.Domain

---

## DPV.Tests

Purpose:

Testing.

Contains:

- Unit Tests
- Integration Tests
- Architecture Tests

May Reference:

All application projects.

---

# MVP Module Structure

Infrastructure shall contain:

Infrastructure/

    Import/

    Search/

    Viewer/

    Thumbnailing/

    Duplicates/

    Persistence/

---

# Duplicate Detection Contract

Status:

MVP Required

Initial Capability:

SHA256 Exact Matching

Initial Location:

Infrastructure/Duplicates

Separate DPV.Duplicates project:

Deferred

---

# Search Contract

Technology:

SQLite FTS5

Dedicated Search Engine:

Not Approved For MVP

Future Evaluation Required

---

# Viewer Contract

Technology Target:

Helix Toolkit

Location:

Infrastructure/Viewer

Separate Viewer Project:

Deferred

---

# Runtime Contract

Approved Runtime:

.NET 10 LTS

---

# Database Contract

Approved Database:

SQLite

Approved Data Access:

Dapper

Entity Framework Core:

Not Approved For MVP

---

# Forbidden Architecture

The following are prohibited during MVP:

Microservices

Separate Search Service

Separate Analytics Project

Separate Duplicate Project

Premature Feature Assemblies

---

# Future Extraction Rule

A module may become its own project only when:

- Complexity justifies extraction
- Clear boundaries exist
- Maintenance burden is reduced
- Architecture review approves extraction

---

# Approval Gate

Before Commit B begins:

DPV-SOLUTION-001 must be:

Reviewed

Approved

Committed

Pushed

---

# Success Criteria

This contract succeeds when:

- Solution structure is defined
- Responsibilities are defined
- Dependencies are defined
- MVP boundaries are defined
- Future growth path is defined

---

End of Document