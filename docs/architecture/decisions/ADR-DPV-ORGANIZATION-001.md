# ADR-DPV-ORGANIZATION-001
## Library Ownership and Organization Model

**Status:** Proposed  
**Date:** 2026-11-04  
**Decision Type:** Product Architecture Decision Record  
**Scope:** Library Management, Import Pipeline, Catalog System, Future Vault Organization

---

# Context

3D Print Vault (DPV) is intended to become the primary management system for large collections of 3D printing assets.

Users may have thousands of files distributed across:

```text
Downloads

Artist Releases

MyMiniFactory Exports

Patreon Deliveries

Tribes Downloads

Network Shares

NAS Devices

External Drives

Organized Folders

Completely Unorganized Folders
```

A key product decision is determining what happens when the user performs an import.

Several approaches were considered:

```text
1. Index Only

2. Managed Vault

3. Hybrid Approach
```

---

# Problem

Many applications immediately assume ownership of user files by:

```text
Moving Files

Renaming Files

Deleting Duplicates

Restructuring Folders
```

This introduces significant risk:

```text
Broken References

Unexpected File Movement

Loss of User Trust

Data Loss Potential

Difficulty Reversing Changes
```

The project requires a strategy that:

```text
Provides immediate value

Keeps user data safe

Allows future organization capabilities

Supports massive libraries
```

without forcing users to surrender control of their existing file structure.

---

# Decision

DPV shall adopt a:

```text
Hybrid Library Ownership Model
```

consisting of:

```text
Phase 1:
Index + Catalog

Phase 2:
Metadata Intelligence

Phase 3:
Organization Planning

Phase 4:
Optional Managed Vault Operations
```

---

# Phase 1: Import Means Index

## Rule

Importing a library SHALL NOT move user files.

Importing a library SHALL:

```text
Scan

Hash

Catalog

Index

Analyze
```

without modifying the source library.

---

## Files Remain In Place

Example:

```text
D:\Downloads\Dragon\dragon.stl

E:\Patreon\ArtistA\Release17\dragon_scaled.stl

\\NAS\Models\Terrain\castle.stl
```

remain exactly where they are.

---

## DPV Stores

```text
File Paths

Hashes

Metadata

Artist

Collection

Release

Tags

Relationships

Statistics

Preview Information
```

inside the catalog database.

---

## Benefits

```text
Safe

Fast

Non-Destructive

Easy Adoption

Supports Multiple Drives

Supports Existing Organization Structures
```

---

# Phase 2: Metadata Intelligence

After indexing, DPV becomes the authoritative knowledge source
for the library.

DPV shall detect:

```text
Missing Metadata

Unknown Artists

Missing Collections

Duplicate Files

Duplicate Releases

Broken File References

Uncategorized Content

Unorganized Content
```

without changing files.

---

# Phase 3: Organization Planning

DPV may generate recommendations for organizing a library.

Examples:

```text
Current:

D:\Random\Models
D:\Downloads
E:\MyMiniFactory
```

Proposed:

```text
D:\Vault
 ├─ Artist
 │   ├─ Collection
 │   │   ├─ Release
 │   │   │   └─ Files
```

or:

```text
D:\Vault
 ├─ Collection
 │   ├─ Artist
 │   │   └─ Files
```

or any future user-defined structure.

---

## Important Rule

Planning SHALL NOT modify files.

Planning only produces:

```text
Recommendations

Move Plans

Rename Plans

Duplicate Cleanup Plans

Storage Reports
```

---

# Phase 4: Organization Execution

Library organization becomes an explicit feature.

A dedicated operation such as:

```text
Organize Library

Apply Library Plan

Execute Vault Migration
```

must be performed by the user.

---

## User Review Required

Before any files are moved, DPV shall present:

```text
Preview

Differences

Move Operations

Folder Creation

Rename Operations

Duplicate Handling
```

for review and approval.

---

## Example Preview

```text
Files To Move: 1,245

Files To Rename: 312

Folders To Create: 87

Duplicates Removed: 42

Storage Recovered: 18 GB
```

---

# File Safety Requirements

DPV SHALL NOT:

```text
Move Files Automatically

Delete Files Automatically

Rename Files Automatically
```

during Import operations.

---

## Import Is Read-Only

Import is defined as:

```text
Scan

Read

Analyze

Catalog
```

only.

---

# Future Managed Vault Capability

Future releases may optionally support:

```text
Managed Vault Mode
```

where DPV owns a specific storage root.

Example:

```text
D:\DPVVault
```

and manages:

```text
Folders

Names

Organization

Storage Layout
```

inside that approved root.

---

## Managed Vault Rules

Managed Vault mode must:

```text
Require Explicit User Consent

Provide Dry Run Capability

Provide Preview Capability

Verify Operations

Support Recovery
```

before destructive actions occur.

---

# Product Philosophy

DPV should earn trust before taking ownership of files.

Workflow:

```text
Catalog First

Understand Second

Recommend Third

Reorganize Last
```

---

# Examples of Similar Models

Conceptually similar systems include:

```text
Plex

Calibre

Adobe Lightroom Catalog

Asset Management Systems

Media Library Managers
```

where the application first catalogs content and only later offers organization assistance.

---

# Consequences

## Positive

```text
Lower Risk

Higher User Trust

Immediate Utility

Supports Existing Libraries

Supports Multiple Storage Strategies

Future Expansion Friendly
```

---

## Negative

```text
File Structure May Remain Messy

Metadata And Folder Layout May Diverge

Organization Benefits Arrive Later
```

These tradeoffs are considered acceptable.

---

# Explicit Rules

## Allowed

```text
Import

Scan

Hash

Metadata Extraction

Tag Assignment

Duplicate Detection

Relationship Discovery

Catalog Updates
```

---

## Prohibited During Import

```text
Move

Rename

Delete

Reorganize
```

---

# Future Architecture Impact

Future organization systems should be designed as:

```text
Independent Workflows
```

rather than part of:

```text
Import
```

This separation is intentional.

---

# Summary

DPV adopts a Hybrid Library Ownership Model.

Import SHALL:

```text
Index + Catalog
```

Import SHALL NOT:

```text
Move Files
```

Library organization shall remain a future,
explicit, user-approved workflow built on top
of the catalog and metadata foundation.

---

# End ADR
