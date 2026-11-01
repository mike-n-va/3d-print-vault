# DPV-ROADMAP-001

Master roadmap for 3D Print Vault.

---

# Current Phase

```text
Sprint 2 Library Foundation
```

---

# Sprint 1 Foundation

Status:

```text
Complete
```

Closed 2026-11-01.

| Ticket | Title | Status |
| --- | --- | --- |
| DPV-S1-001 | Repository and Development Governance Setup | Complete |
| DPV-S1-002 | Create Core Projects | Complete |
| DPV-S1-003 | Establish Test Foundation | Complete With Documented Limitation |
| DPV-S1-004 | Configure Project References | Superseded |
| DPV-S1-005 | Dependency Injection Bootstrap | Complete |

Sprint 1 delivered the solution structure, executable architecture tests,
test foundations, and the dependency injection composition root with host
lifecycle verification.

---

# Sprint 2 Library Foundation

Goal:

```text
Walk a single vertical slice through all layers.

Point the application at a folder of STL files and see the imported models
listed in the UI, persisted in SQLite.
```

This satisfies the DEVELOPMENT_STANDARD.md artifact requirement for the scanner:

```text
Successfully imports target library.
```

Tickets follow the DEVELOPMENT_STANDARD.md flow: research first, then
contracts, then implementation.

| Ticket | Title |
| --- | --- |
| DPV-S2-001 | REA-007: Library Scanning and Persistence Research |
| DPV-S2-002 | Domain Model: Artist, Collection, Model |
| DPV-S2-003 | SQLite Persistence Foundation |
| DPV-S2-004 | Library Scanner (STL Import) |
| DPV-S2-005 | Model List UI |
| DPV-S2-006 | Walking Skeleton Verification |

Order matters: DPV-S2-001 must complete before implementation begins.

---

# Deferred Research

The following research documents are placeholders and must be completed before
their features enter a sprint:

- REA-002: Search System
- REA-003: 3D Viewer
- REA-004: Duplicate Detection
