\# ADR-DPV-CATALOG-001

\## Visual Catalog Experience



\*\*Status:\*\* Proposed  

\*\*Date:\*\* 2026-11-04  

\*\*Decision Type:\*\* Product Architecture Decision Record  

\*\*Scope:\*\* User Experience, Library Discovery, Catalog Presentation, Search, Navigation



\---



\# Context



Several strategic reviews, roadmap evaluations, and deferred-feature analyses have clarified the long-term direction of DPV.



The original concept could have evolved into many different products:



```text

File Browser



Print Tracker



Vault Manager



Duplicate Finder



Metadata Editor



STL Viewer

```



During roadmap review and prioritization, a consistent vision emerged:



```text

Searchable



Sortable



Visual



Catalog

```



for large 3D printing libraries.



This ADR formalizes the user experience direction for the product.



\---



\# Problem



Traditional file systems become increasingly difficult to use as model collections grow.



Users often manage:



```text

Thousands of Models



Hundreds of Releases



Dozens of Artists



Multiple Drives



Collections Spanning Years

```



File systems are optimized for storage.



They are not optimized for discovery.



As collections grow, users increasingly think in terms of:



```text

Artist



Collection



Release



Category



Tag



Visual Appearance

```



rather than:



```text

Folder Path



Directory Structure



File Name

```



\---



\# Decision



DPV shall treat the catalog as the primary user experience.



The file system remains the source of content.



The catalog becomes the source of discovery.



\---



\# Core Product Principle



DPV is:



```text

A Visual Catalog

```



first,



and



```text

A File Management Tool

```



second.



\---



\# Primary User Workflow



Users should primarily interact through:



```text

Search



Browse



Filter



Explore



Discover

```



rather than:



```text

Navigate Folders

```



\---



\# Catalog First Philosophy



Users should be able to answer:



```text

Show me all Dragons



Show me Artisan Guild releases



Show me all Fantasy Terrain



Show me releases from January 2026



Show me unsupported models



Show me favorite models

```



without requiring knowledge of where files exist.



\---



\# Catalog As Source Of Discovery



The catalog will provide:



```text

Search



Filtering



Sorting



Grouping



Relationships



Recommendations

```



across all indexed content.



\---



\# Visual Discovery Requirement



Visual discovery is considered a first-class feature.



Text-only views are insufficient for large model collections.



DPV shall prioritize:



```text

Images



Thumbnails



Model Previews



Visual Identification

```



over file naming conventions.



\---



\# Required Presentation Modes



Future releases shall support:



\## List View



Suitable for:



```text

Administration



Auditing



Bulk Operations

```



Example:



```text

Model

Artist

Collection

Files

Size

```



\---



\## Compact List View



Suitable for:



```text

Dense Catalog Browsing



Large Libraries

```



\---



\## Grid View



Suitable for:



```text

Visual Browsing



Collection Discovery



Model Exploration

```



Example:



```text

┌────────────┐

│ Thumbnail  │

├────────────┤

│ Dragon     │

│ Artist     │

└────────────┘

```



\---



\## Large Thumbnail View



Suitable for:



```text

Model Selection



Visual Comparison



Asset Inspection

```



\---



\## Details View



Suitable for:



```text

Power Users



Catalog Management



Advanced Filtering

```



Columns may include:



```text

Preview



Model



Artist



Collection



Release



Files



Size



Tags

```



\---



\# Search Principles



Search shall operate on:



```text

Model Name



Artist



Collection



Release



Tags



Notes



File Names

```



through a unified interface.



Users should not need to know where information is stored.



\---



\# Metadata Dependency



This ADR depends upon:



```text

ADR-DPV-METADATA-001

```



because visual discovery improves as metadata quality improves.



The quality of:



```text

Search



Grouping



Catalog Views



Recommendations

```



is directly tied to metadata completeness.



\---



\# Organization Dependency



This ADR supports:



```text

ADR-DPV-ORGANIZATION-001

```



The catalog must understand the library before organization can occur.



Therefore:



```text

Catalog

&#x20;   ↓

Metadata

&#x20;   ↓

Discovery

&#x20;   ↓

Organization

```



remains the approved sequence.



\---



\# Visual Catalog Features



High-priority future capabilities include:



```text

Artist Pages



Collection Pages



Release Pages



Tag Views



Visual Grid View



Thumbnail Browsing



Rich Search



Advanced Filters

```



\---



\# Thumbnail Philosophy



The product should eventually support:



```text

Wireframe Preview



Shaded Preview



Rendered Preview

```



where practical.



Visual identification should be faster than reading file names.



\---



\# User Experience Goal



The intended experience is conceptually similar to:



```text

Plex



Lightroom



Calibre

```



for 3D printing assets.



This is a user experience comparison only.



It is not an architectural requirement.



\---



\# Explicit Rules



\## Preferred



```text

Browse Models



Browse Artists



Browse Collections



Browse Tags



Visual Discovery

```



\---



\## Avoid



```text

Folder-Centric Navigation



Path-Centric Workflows



Manual Directory Traversal

```



except where necessary for maintenance tasks.



\---



\# Feature Prioritization Impact



Features that improve:



```text

Catalog Discovery



Search



Metadata Quality



Visual Browsing

```



receive higher priority than features that:



```text

Modify Files



Move Files



Manage Storage Locations

```



because discovery is the primary value proposition.



\---



\# Success Criteria



DPV is successful when a user can:



```text

Locate a model quickly



Discover forgotten assets



Browse visually



Search naturally



Filter intelligently

```



regardless of file location.



\---



\# Consequences



\## Positive



```text

Strong Product Identity



Scales To Large Libraries



Improved Discoverability



Metadata Driven UX



Natural Growth Path

```



\---



\## Negative



```text

Requires More Metadata



Requires Thumbnail Infrastructure



Requires Rich Search

```



These tradeoffs are acceptable.



\---



\# Summary



DPV shall evolve primarily as a:



```text

Searchable



Sortable



Visual Catalog

```



for 3D printing assets.



The catalog becomes the primary user experience.



File management, vault management, and storage operations remain important but secondary capabilities.



The approved product evolution path is:



```text

Catalog

&#x20;   ↓

Metadata

&#x20;   ↓

Discovery

&#x20;   ↓

Organization

&#x20;   ↓

Optional Vault Management

```



\---



\# End ADR

