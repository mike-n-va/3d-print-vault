# ADR-DPV-MODEL-001
## Model as the Primary Catalog Entity

**Status:** Accepted

**Date:** 2026-11-04

**Decision Type:** Domain Architecture Decision Record

**Scope:** Catalog Architecture, Metadata Architecture, Search, Discovery, Import, Organization

---

# Context

As DPV evolved from a file scanner into a catalog platform, a fundamental domain question emerged:

```text
What is the primary thing DPV manages?
```

Historically the application has treated:

```text
1 File
=
1 Catalog Item
```

This approach aligns with file-system thinking but does not align with how users discover, organize, evaluate, or print 3D content.

Research performed in:

```text
REA-DPV-CATALOG-001
```

and reviewed in:

```text
ARCH-REVIEW-MODEL-CENTRIC-001
```

demonstrated that users think in terms of:

```text
Models
```

not:

```text
Files
```

---

# Decision

DPV shall adopt:

```text
Model
```

as the primary catalog entity.

Files become:

```text
Assets belonging to a Model.
```

---

# Product Principle

Approved Product Principle:

```text
Users discover Models.

Users store Files.
```

This principle shall guide future architecture, metadata, search, catalog presentation, and organization features.

---

# Definitions

## Model

A Model represents a logical printable object.

Examples:

```text
Ancient Red Dragon

Dungeon Entrance

Goblin Archer

Sci-Fi Cargo Crate
```

A Model is what users:

```text
Search

Browse

Favorite

Tag

Rate

Organize

Print
```

---

## File

A File represents a physical storage asset.

Examples:

```text
dragon_head.stl

dragon_wings.stl

dragon_supported.stl

dragon.3mf

render.png

instructions.pdf
```

Files are implementation details of a Model.

---

# Why This Decision Was Made

3D printing collections are rarely distributed as a single file.

A typical release contains:

```text
Mesh Files

Supported Variants

Build Plates

Instructions

Assembly Guides

Preview Images

Render Files
```

Without a Model entity:

```text
1 Dragon
=
12 Catalog Entries
```

which produces:

```text
UI Clutter

Poor Search Results

Metadata Duplication

Poor Discovery Experience
```

With a Model entity:

```text
1 Dragon
=
1 Catalog Entry

12 Files
=
Model Assets
```

which aligns with user expectations.

---

# Approved Catalog Hierarchy

Recommended hierarchy:

```text
Artist
    ↓
Collection
    ↓
Release
    ↓
Model
    ↓
Assets
```

Not every relationship is mandatory.

However:

```text
Model
```

must remain the discovery unit.

---

# Model Responsibilities

A Model owns:

```text
Catalog Identity

Metadata

Searchable Information

Tags

Ratings

Favorites

Relationships

Assets
```

A Model does NOT own:

```text
Physical Storage

Drive Layout

Folder Structure
```

---

# Asset Ownership

A Model may own:

```text
STL Files

3MF Files

OBJ Files

Render Images

Preview Images

Assembly Guides

Documentation

Instructions

Configuration Files
```

Future asset types may be added.

---

# Required Model Attributes

## Required

```text
ModelId

Name

Created

Modified
```

These attributes are required for every model.

---

## Recommended

```text
ArtistId

CollectionId

ReleaseId

ThumbnailReference

PrimaryAsset

Tags

Description

SearchTerms
```

These fields significantly improve discovery.

---

## Optional

```text
Favorite

Rating

Notes

PrintStatus

Scale

Version
```

Optional metadata improves user experience but is not required for catalog operation.

---

## Future

```text
Difficulty

Recommended Printer

Resin Profile

Filament Profile

Estimated Print Time

External Links

License Information
```

Future versions may support these attributes.

---

# Relationship Model

## Artist

```text
Artist
    1
    ↓
    N
Model
```

An artist may create many models.

---

## Collection

```text
Collection
    1
    ↓
    N
Model
```

A collection may contain many models.

---

## Release

```text
Release
    1
    ↓
    N
Model
```

A release may contain many models.

---

## Assets

```text
Model
    1
    ↓
    N
Assets
```

A model may own multiple files and file types.

---

## Tags

```text
Model
    N
    ↔
    N
Tags
```

Tags are a many-to-many relationship.

---

# Search Strategy

Search shall primarily return:

```text
Models
```

not individual files.

Search may consider:

```text
Model Name

Artist

Collection

Release

Tags

Description

Asset Names
```

however results should be surfaced at the Model level.

---

# Visual Catalog Strategy

Grid View displays:

```text
Models
```

not files.

Example:

```text
┌────────────┐
│ Thumbnail  │
├────────────┤
│ Red Dragon │
│ 7 Assets   │
└────────────┘
```

rather than:

```text
dragon_head.stl
```

---

# Import Strategy

Import remains:

```text
Read Only
```

as defined by:

```text
ADR-DPV-ORGANIZATION-001
```

Import shall:

```text
Scan

Analyze

Group

Catalog
```

The importer should eventually identify:

```text
Files belonging to the same Model
```

through rules, sidecars, folder structures, and future heuristics.

---

# Metadata Strategy

Metadata attaches to:

```text
Model
```

not individual files.

Benefits:

```text
Less Duplication

Simpler Editing

Cleaner Search

Cleaner Navigation
```

---

# Organization Strategy

Organization planning should operate on:

```text
Models
```

rather than loose files.

Example:

```text
Move Ancient Red Dragon
```

instead of:

```text
Move dragon_body.stl

Move dragon_head.stl

Move dragon_tail.stl
```

This preserves model integrity.

---

# Roadmap Implications

This ADR increases the importance of:

```text
Folder Structure Metadata Inference

Artist Views

Collection Views

Tag Views

Visual Catalog Grid View

Metadata Sidecars

Organization Planning
```

because all rely on Model-level identity.

---

# Architectural Guidance

Future features should answer:

```text
Does this operate on Models?

or

Does this operate on Files?
```

Whenever ambiguity exists:

```text
Catalog Layer
    → Model

Storage Layer
    → File
```

is the preferred architecture.

---

# Consequences

## Positive

```text
Cleaner Catalog

Better Search

Better Discovery

Improved Visual Browsing

Reduced Metadata Duplication

Better Organization Support

Better Artist Navigation

Better Collection Navigation
```

---

## Negative

```text
Requires Model Discovery Logic

Requires Asset Grouping Logic

Requires Future Schema Evolution
```

These costs are considered acceptable.

---

# Summary

DPV adopts:

```text
Model
```

as the primary catalog unit.

Files become assets owned by a Model.

Future catalog, metadata, search, organization, and visual discovery systems shall be designed around Models rather than individual files.

The defining product principle is:

```text
Users discover Models.

Users store Files.
```

---

# Related Documents

```text
ADR-DPV-CATALOG-001
Visual Catalog Experience

ADR-DPV-METADATA-001
Metadata Acquisition and Enrichment

ADR-DPV-ORGANIZATION-001
Library Ownership and Organization

REA-DPV-CATALOG-001
Visual Catalog Research

ARCH-REVIEW-MODEL-CENTRIC-001
Model-Centric Architecture Review
```

---

# End ADR
