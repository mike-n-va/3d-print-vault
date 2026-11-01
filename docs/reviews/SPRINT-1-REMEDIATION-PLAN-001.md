# Sprint 1 Remediation Plan

**Review date:** 2026-11-01
**Scope:** Issues #6, #7, #8, #9, #11, #12, and #14
**Purpose:** Identify remaining acceptance work, repository artifacts, effort,
and remediation type
**Implementation authorization:** This plan does not authorize implementation
or change any GitHub issue, milestone, architecture, or Sprint scope.

## 1. Basis and Assumptions

This plan uses the acceptance-criterion evidence in
[SPRINT-1-ACCEPTANCE-EVIDENCE-REVIEW-001](SPRINT-1-ACCEPTANCE-EVIDENCE-REVIEW-001.md),
current project source, and the approved
[DPV-ARCH-001](../architecture/DPV-ARCH-001.md),
[DPV-PROJECT-DEFINITIONS-001](../architecture/DPV-PROJECT-DEFINITIONS-001.md),
and [DPV-CONFIGURATION-001](../architecture/DPV-CONFIGURATION-001.md).

Effort is estimated in **engineering person-days**, including implementation
and issue-specific verification where needed. It excludes governance approval,
GitHub administration, waiting time, and unrelated rework. Estimates are
planning ranges, not commitments.

Current code already contains the DI composition root, Application options,
Serilog configuration, and a starter Avalonia window. The reviewed state has
no SQLite bootstrap, requested Database/Scanning/Viewer options, four
catalog entities, shell navigation, or Dashboard feature. The Release
solution build and 24-test suite pass; those results do not satisfy missing
feature acceptance.

Before implementation begins:

- Confirm the exact Sprint scope and separately authorize work under the
  applicable governance gates.
- Preserve the approved five-project topology; do not introduce
  `DPV.Application`.
- Resolve unspecified domain fields/identity before committing to entity or
  persistence schemas. Do not invent business rules to satisfy a ticket.
- Treat Issue #14 as an aggregate validation ticket; it depends on the
  implementation and evidence for the other included acceptance items.

## 2. Issue Plans

### Issue #6 — Configuration System

**Current review result:** FAIL
**Classification:** Implementation required
**Estimated effort:** 0.5-1.0 person-day

**Missing acceptance criteria**

- `DatabaseOptions` and the `Database` configuration section.
- `ScanningOptions` and the `Scanning` section.
- `ViewerOptions` and the `Viewer` section.
- Configuration binding and validation for these options. Existing
  `ApplicationOptions` binding does not cover them.

**Repository files needed**

Existing:

- `src/DPV.App/Configuration/ApplicationOptions.cs`
- `src/DPV.App/Configuration/OptionsRegistration.cs`
- `src/DPV.App/appsettings.json`
- `src/DPV.App/appsettings.Development.json`
- `tests/DPV.Tests/Integration/DependencyInjection/CompositionRootIntegrationTests.cs`

Expected additions or updates:

- Add options types under `src/DPV.App/Configuration/` for database,
  scanning, and viewer settings.
- Add the corresponding sections to both App settings files, with
  environment-appropriate values and no secrets.
- Extend `OptionsRegistration.cs` to bind and validate the sections.
- Extend `CompositionRootIntegrationTests.cs` to verify binding, valid
  configuration, and startup rejection of invalid options.

**Implementation and verification work**

Define the minimal settings needed by approved Sprint 1 scope, bind them with
the existing options pattern, and validate required values. Verify that
configuration loads from the App settings files and that invalid required
values fail host-start validation. Avoid adding feature behavior or settings
for unapproved later work.

### Issue #7 — Logging Framework

**Current review result:** CONDITIONAL
**Classification:** Verification only
**Estimated effort:** 0.25-0.5 person-day

**Missing acceptance criteria**

- Direct evidence that the file sink creates and writes a log file
  successfully. Serilog, console, and rolling-file configuration are already
  present.

**Repository files needed**

Existing:

- `src/DPV.App/Logging/BootstrapLogging.cs`
- `src/DPV.App/Startup/CompositionRootHostFactory.cs`
- `src/DPV.App/appsettings.json`
- `src/DPV.App/DPV.App.csproj`
- `tests/DPV.Tests/Integration/DependencyInjection/CompositionRootIntegrationTests.cs`

Expected additions:

- Add a focused logging integration test, for example
  `tests/DPV.Tests/Integration/Logging/FileLoggingIntegrationTests.cs`, or a
  reproducible verification record if a filesystem test would be brittle.

**Implementation and verification work**

Run the configured host with an isolated temporary working/log directory,
write a known log event, flush/dispose logging, and verify that a non-empty
rolling log file is produced. Check the console path through a captured
writer or existing supported test seam if practical. Do not claim successful
file logging solely from configuration. If the verification exposes a
runtime/configuration defect, implementation to correct it becomes necessary
and the estimate should be revised.

### Issue #8 — SQLite Bootstrap

**Current review result:** PASS — remediation completed
**Classification:** Implementation required — completed
**Estimated effort:** 2-4 person-days

**Completed acceptance criteria**

- `Microsoft.Data.Sqlite` is referenced by the Infrastructure project.
- Host startup creates the configured database and parent directory.
- The Infrastructure connection factory creates usable, short-lived
  SQLite connections with foreign keys enabled.
- Startup enables WAL and verifies the effective journal mode.
- Migration version 1 and its ledger are applied idempotently; unsupported
  newer versions fail explicitly.
- Integration tests exercise actual temporary SQLite files and the
  composition-root startup path.

**Repository files changed**

Implemented:

- `src/DPV.Infrastructure/DPV.Infrastructure.csproj`
- `src/DPV.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `src/DPV.Infrastructure/Persistence/IDatabaseConnectionFactory.cs`
- `src/DPV.Infrastructure/Persistence/SqliteConnectionFactory.cs`
- `src/DPV.Infrastructure/Persistence/DatabaseInitializer.cs`
- `src/DPV.Infrastructure/Persistence/MigrationRunner.cs`
- `tests/DPV.Tests/Integration/Persistence/SqliteBootstrapIntegrationTests.cs`
- `tests/DPV.Tests/Integration/DependencyInjection/HostLifecycleIntegrationTests.cs`
- `docs/architecture/DPV-CONFIGURATION-001.md`
- `docs/reviews/SPRINT-1-ACCEPTANCE-EVIDENCE-REVIEW-001.md`

The migration baseline creates only the version ledger; it does not invent
catalog tables or entity schema while Issue #9 remains unresolved. The
configured Development value `/data/dev.db` is unchanged and remains
non-portable on Windows; it was not treated as verified portable deployment
configuration.

**Verification evidence**

- Focused SQLite bootstrap tests: 3 passed, 0 failed.
- Release solution build: 0 warnings, 0 errors.
- Full Release test suite: 33 passed, 0 failed, 0 skipped.
- Host startup, real SQLite settings, repeat initialization, and newer-schema
  rejection are covered by the focused tests.

### Issue #9 — Initial Domain Entities

**Current review result:** FAIL
**Classification:** Implementation required
**Estimated effort:** 1-2 person-days, subject to domain-definition decisions

**Missing acceptance criteria**

- `Artist`, `Collection`, `Model`, and `Tag` entity declarations do not
  exist.
- The negative constraints (no persistence logic and no UI dependencies)
  currently hold, but do not compensate for the missing entities.
- Issue #10's repository contracts are empty markers; adding entities alone
  does not define repository operations or schema.

**Repository files needed**

Existing:

- `src/DPV.Domain/DPV.Domain.csproj`
- `src/DPV.Domain/Entities/`
- `src/DPV.Domain/ValueObjects/`
- `tests/DPV.Tests/DPV.Tests.csproj`
- `tests/DPV.Tests/Architecture/Boundaries/DomainPurityTests.cs`

Expected additions:

- `src/DPV.Domain/Entities/Artist.cs`
- `src/DPV.Domain/Entities/Collection.cs`
- `src/DPV.Domain/Entities/Model.cs`
- `src/DPV.Domain/Entities/Tag.cs`
- Domain-focused tests under `tests/DPV.Tests/Unit/Domain/`.

**Implementation and verification work**

Approve the minimum identity, required properties, and relationships for
each entity before implementation; the issue body does not specify these
semantics. Implement only those approved domain concepts. Keep constructors
and invariants framework/persistence independent; add no SQLite, Dapper,
Avalonia, filesystem, or DI dependencies to Domain. Test the agreed
invariants and retain architecture tests for purity. Coordinate the agreed
entity fields with Issue #8 before defining persisted schema.

### Issue #11 — Main Shell Application

**Current review result:** FAIL
**Classification:** Implementation required
**Estimated effort:** 1-2 person-days

**Missing acceptance criteria**

- A visible sidebar.
- A visible navigation region.
- Direct GUI evidence of application launch and no crash. Release
  compilation and DI host lifecycle tests do not demonstrate the Avalonia
  window displaying successfully.

**Repository files needed**

Existing:

- `src/DPV.UI/MainWindow.axaml`
- `src/DPV.UI/MainWindow.axaml.cs`
- `src/DPV.UI/App.axaml`
- `src/DPV.UI/App.axaml.cs`
- `src/DPV.UI/DependencyInjection/UiServiceCollectionExtensions.cs`
- `src/DPV.App/Startup/AvaloniaApplicationStartup.cs`
- `tests/DPV.Tests/Integration/DependencyInjection/CompositionRootIntegrationTests.cs`

Expected additions or updates:

- Replace starter content in `MainWindow.axaml` with shell layout.
- Add shell ViewModel and navigation state under `src/DPV.UI/ViewModels/`
  and `src/DPV.UI/Navigation/`, if required by the approved MVVM design.
- Add or update UI registration and constructor injection only as needed.
- Add view/startup verification in the existing UI test structure or a
  manual smoke-test record, subject to supported Avalonia test setup.

**Implementation and verification work**

Create a minimal, responsive shell with sidebar and a main navigation
content region. Navigation can be structural for this issue; do not build
dashboard behavior here. Preserve `DPV.App` as the executable/composition
root and keep presentation state in UI. Verify on the supported desktop
target that the window appears, both regions are visible, and startup and
shutdown complete without an unhandled exception.

### Issue #12 — Dashboard Placeholder

**Current review result:** FAIL
**Classification:** Implementation required
**Estimated effort:** 1-2 person-days; depends on Issue #11 shell

**Missing acceptance criteria**

- Dashboard View.
- Dashboard ViewModel.
- Navigation to the dashboard.
- Dashboard selected and loaded at startup.

**Repository files needed**

Existing:

- `src/DPV.UI/MainWindow.axaml`
- `src/DPV.UI/MainWindow.axaml.cs`
- `src/DPV.UI/DependencyInjection/UiServiceCollectionExtensions.cs`
- `src/DPV.UI/Views/`
- `src/DPV.UI/ViewModels/`
- `src/DPV.UI/Navigation/`
- `tests/DPV.Tests/`

Expected additions:

- A dashboard view under `src/DPV.UI/Views/`.
- `DashboardViewModel.cs` under `src/DPV.UI/ViewModels/`.
- Dashboard route/selection handling integrated with the Issue #11 shell
  navigation region.
- ViewModel unit tests and, if supported, a focused Avalonia view or
  navigation integration test under `tests/DPV.Tests/`.

**Implementation and verification work**

Add temporary dashboard content and a ViewModel that exposes only
presentation state. Make the dashboard the initial navigation destination.
Verify navigation selects the dashboard, startup presents its view in the
Issue #11 content region, and ViewModel behavior is testable without
launching the full desktop application. Do not add catalog queries or
features outside the placeholder acceptance.

### Issue #14 — Build Verification

**Current review result:** FAIL
**Classification:** Implementation required
**Estimated effort:** 0.5-1.5 person-days after prerequisite issues

**Missing acceptance criteria**

- Application launch evidence.
- Automatic database-creation evidence.
- Evidence that logging works end to end, including file output.
- Evidence that no blocking issues remain for the accepted Sprint 1 scope.
- An aggregate validation record covering restore, build, and tests at the
  final candidate revision.

Restore, Release build, tests, and CI already pass for the reviewed code,
but Issue #8's database behavior is absent and Issue #7's file output and
Issue #11/#12's UI runtime behavior remain unverified. Therefore the
criterion “No blocking issues remain” cannot pass yet.

**Repository files needed**

Existing:

- `.github/workflows/ci.yml`
- `DPV.sln`
- `tests/DPV.Tests/DPV.Tests.csproj`
- `src/DPV.App/Program.cs`
- `src/DPV.App/Startup/AvaloniaApplicationStartup.cs`
- `src/DPV.App/Logging/BootstrapLogging.cs`
- `src/DPV.App/appsettings.json`
- `docs/reviews/SPRINT-1-ACCEPTANCE-EVIDENCE-REVIEW-001.md`

Expected additions or updates:

- Add or extend integration/smoke tests with the logging, database, and UI
  acceptance work above.
- Update the issue's evidence record or create a final validation report
  under `docs/reviews/` after running against the exact candidate revision.
- Update CI only if the approved acceptance requires an additional
  automated validation step; current workflow already restores, builds, and
  tests on Ubuntu and Windows.

**Implementation and verification work**

Do not treat Issue #14 as a substitute for implementing its dependencies.
After #6-#9 and #11-#12 are complete or formally dispositioned, run restore,
Release build, tests, and the authorized application/database/logging
smoke checks. Record commit, environment, commands, results, and
limitations. Reconcile all blocking issues against the approved Sprint
scope; the presence of a green CI result alone is insufficient.

## 3. Dependency and Sequencing Recommendation

Recommended order:

1. **Issue #6** — define and validate the configuration required by
   persistence and the application.
2. **Issue #9** — approve entity identity and minimum domain shape; coordinate
   schema implications with the database decision.
3. **Issue #8** — resolve migration/schema ownership, then implement and test
   SQLite initialization and connection behavior.
4. **Issue #7** — verify file logging; fix configuration/code only if the
   runtime check fails.
5. **Issue #11** — implement and smoke-test the application shell.
6. **Issue #12** — implement the dashboard on the shell's navigation region.
7. **Issue #14** — perform aggregate verification against the final approved
   scope and revision.

Issues #6, #7, and initial UI-shell work can be developed in parallel only
after ownership and scope are authorized. Issue #12 depends on #11; #14
depends on the accepted evidence for its prerequisites. Issue #8 depends on
the database/migration decision and on any entity details required by the
initial schema.

## 4. Effort Summary

| Issue | Classification | Estimate |
|---:|---|---:|
| #6 | Implementation required | 0.5-1.0 days |
| #7 | Verification only | 0.25-0.5 days |
| #8 | Implementation required | 2-4 days |
| #9 | Implementation required | 1-2 days |
| #11 | Implementation required | 1-2 days |
| #12 | Implementation required | 1-2 days |
| #14 | Implementation required (aggregate verification plus any remaining fixes) | 0.5-1.5 days |
| **Total** |  | **6.25-13 person-days** |

The total is a simple sum, not a calendar forecast. Parallel work may reduce
elapsed time; domain/migration decisions, UI automation setup, or fixes
discovered during verification may increase it.

## 5. Governance Boundary

This is a planning artifact only. It does not:

- authorize implementation or change approved architecture;
- approve unspecified entity fields, persistence schema, or migration
  strategy;
- close, reassign, edit, or change the state of any issue or milestone;
- resolve Issue #10 or change Issue #15's GitHub state;
- declare Sprint 1 complete or authorize Sprint 2.

Each issue's acceptance evidence must be verified against its exact approved
criteria and recorded at the revision where the work is reviewed.
