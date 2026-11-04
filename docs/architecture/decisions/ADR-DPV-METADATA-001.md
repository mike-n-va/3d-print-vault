# ADR-DPV-METADATA-001
## Metadata Acquisition and Enrichment Strategy

**Status:** Proposed  
**Date:** 2026-11-04  
**Decision Type:** Product Architecture Decision Record  
**Scope:** Cataloging, Metadata Management, Import Pipeline, Future Library Intelligence

---

# Context

3D Print Vault (DPV) is intended to become more than a file browser.

The long-term objective is to provide:

- Library Discovery
- Search
- Classification
- Organization Planning
- Artist Management
- Collection Management
- Release Management
- Duplicate Detection
- Analytics

To achieve this, DPV requires rich metadata.

Many STL and 3D asset collections already contain usable metadata,
but that information is scattered across:

- Folder structures
- File names
- Archive names
- User spreadsheets
- External catalogs
- User knowledge

DPV requires a strategy for gathering and enriching metadata over time.

---

# Decision

DPV shall utilize a multi-tier metadata acquisition model.

Metadata shall be accumulated and refined through multiple sources.

No single source shall be considered authoritative in every situation.

The catalog database shall become the primary knowledge repository.

---

# Metadata Source Hierarchy

## Tier 1: File System Metadata

Automatically collected during import.

### Imported Attributes

```text
File Name

Extension

Size

Created Date

Modified Date

Location

Parent Folder

Content Hash
```

### Example

```text
dragon_head.stl
```

Provides:

```text
File Name

File Type

Storage Information
```

---

# Tier 2: Folder Structure Metadata

Parse existing organization patterns.

### Example

```text
D:\Vault\
 └─ ArtisanGuild
     └─ Dragons
         └─ January2026
             └─ dragon.stl
```

Imported metadata:

```text
Artist      = ArtisanGuild

Collection  = Dragons

Release     = January2026
```

### Goal

Leverage organization users have already built.

---

# Tier 3: Archive Metadata

Extract metadata from source archives.

### Supported Examples

```text
ZIP

RAR

7Z
```

### Example

```text
ArtisanGuild_Dragons_January2026.zip
```

Potential extraction:

```text
Artist

Release

Collection
```

---

# Tier 4: User Metadata

Users may manually provide metadata.

Examples:

```text
Artist

Collection

Release

Tags

Notes

Rating

Print Status

Favorites
```

User-provided metadata shall be considered highly trusted.

---

# Tier 5: Catalog Imports

Future releases should support metadata import from:

```text
CSV

JSON

XML

Other Catalog Formats
```

### Example

```csv
File,Artist,Collection,Tags
dragon.stl,ArtisanGuild,Dragons,"dragon,fantasy"
```

Purpose:

- Migration
- Bulk Updates
- Library Recovery
- Catalog Portability

---

# Tier 6: Metadata Sidecars

Future support should be considered for metadata sidecar files.

### Example

```text
dragon.stl
dragon.dpv.json
```

### Example Content

```json
{
  "artist": "ArtisanGuild",
  "collection": "Dragons",
  "release": "January2026",
  "tags": [
    "dragon",
    "fantasy"
  ]
}
```

Benefits:

- Portable
- Versionable
- Backup Friendly
- Database Independent

---

# Tier 7: Preview and Thumbnail Metadata

DPV should generate and maintain:

```text
Preview Images

Thumbnails

Render Data
```

Associated with:

```text
Content Hash

Catalog Entry

Release
```

Rather than specific file paths.

---

# Tier 8: AI-Assisted Metadata

Long-term optional capability.

DPV may analyze:

```text
File Names

Folder Structures

Release Names

Archive Names
```

to suggest:

```text
Artist

Collection

Release

Tags

Categories
```

All suggestions must be reviewed and approved by the user.

---

# Catalog Authority Model

The catalog database shall become the authoritative source of metadata.

The file system remains the authoritative source of content.

### Catalog Owns

```text
Artist

Collection

Release

Tags

Relationships

Ratings

Library Intelligence
```

### File System Owns

```text
Physical Files

File Locations

Storage Media
```

---

# Metadata Confidence Model

Future implementations should distinguish:

### Verified

```text
User Entered

Imported from trusted source
```

### Inferred

```text
Folder Structure

Archive Name

File Name
```

### Suggested

```text
AI Generated

Pattern Derived

Heuristic
```

This allows users to understand the origin and reliability of metadata.

---

# Metadata Enhancement Workflow

Future workflow:

```text
Import
    ↓
Catalog
    ↓
Metadata Analysis
    ↓
Metadata Suggestions
    ↓
User Review
    ↓
Metadata Approval
```

---

# Future Features Enabled

This architecture enables:

```text
Artist Pages

Collection Pages

Release Pages

Duplicate Detection

Metadata Analytics

Library Health Reports

Organization Planning

Vault Migration

Smart Search

Recommendation Systems
```

---

# Guiding Principle

DPV should become smarter than the folder structure.

Files may remain disorganized.

The catalog should provide order, insight, and discoverability regardless of how files are stored.

---

# Summary

DPV shall acquire metadata from multiple sources.

Metadata collection shall begin with:

```text
File System

Folder Structure

User Input
```

and progressively expand to include:

```text
Catalog Imports

Sidecar Metadata

Generated Previews

AI-Assisted Suggestions
```

The catalog becomes the primary source of knowledge, enabling future organization, analysis, and vault-management capabilities.

---

# End ADR
