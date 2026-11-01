# Sprint 1 Acceptance Evidence Review

**Review date:** 2026-11-01
**Scope:** GitHub Issues #5–#14, criterion-level evidence review
**GitHub issue changes:** None

## 1. Review Method

The acceptance criteria below were retrieved from the current GitHub issue
bodies for [#5](https://github.com/mike-n-va/3d-print-vault/issues/5)
through [#14](https://github.com/mike-n-va/3d-print-vault/issues/14).
Source inspection, existing project evidence, current local validation, and
the latest available CI run were considered separately. A passing build is
not treated as proof of runtime behavior or feature acceptance.

**Status meanings**

- **PASS:** Direct evidence supports every acceptance criterion for the
  reviewed issue.
- **CONDITIONAL:** Some criteria have evidence, but at least one runtime,
  approval, or replacement condition remains unverified.
- **FAIL:** At least one required criterion is demonstrably absent or
  contradicted by the current implementation.

## 2. Shared Validation Evidence

| ID | Evidence |
|---|---|
| **B1 — Current restore** | `dotnet restore DPV.sln` succeeded on 2026-11-01; three projects restored and two were up to date. |
| **B2 — Current Release build** | `dotnet build DPV.sln --configuration Release` succeeded on 2026-11-01: 0 warnings, 0 errors. |
| **T1 — Current full tests** | `dotnet test DPV.sln` passed on 2026-11-01: 55 passed, 0 failed, 0 skipped. |
| **T2 — Composition-root focused tests** | `dotnet test tests/DPV.Tests/DPV.Tests.csproj --configuration Release --no-build --filter FullyQualifiedName~CompositionRootIntegrationTests` passed: 10 passed, 0 failed, including Dashboard registrations. |
| **T3 — Issue #7 focused tests** | `dotnet test tests/DPV.Tests/DPV.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~FileLoggingIntegrationTests` passed: 2 passed, 0 failed. |
| **T4 — Issue #8 focused tests** | `dotnet test tests/DPV.Tests/DPV.Tests.csproj --configuration Release --filter FullyQualifiedName~SqliteBootstrapIntegrationTests` passed: 3 passed, 0 failed. Tests use actual SQLite files in unique temporary directories. |
| **T5 — Issue #9 focused tests** | `dotnet test tests/DPV.Tests/DPV.Tests.csproj --configuration Release --filter FullyQualifiedName~DomainEntityTests` passed: 17 passed, 0 failed. Tests cover identities, name validation, relationships, and Model tag invariants. |
| **T6 — Issue #11 Windows runtime smoke test** | Started the Release desktop application with an isolated temporary database. Windows UI Automation found the `3D Print Vault` window as on-screen and verified the sidebar labels (`Library`, `Artists`, `Collections`, `Tags`, `Settings`) plus the `Workspace` and `Choose a section` content. Closing the window ended the application normally. |
| **T7 — Shell and Dashboard structure test** | `dotnet test tests/DPV.Tests/DPV.Tests.csproj --configuration Release --filter FullyQualifiedName~ShellStructureIntegrationTests` passed: 1 passed, 0 failed. Verifies the shell regions, Dashboard view template, content binding, and Dashboard navigation binding. |
| **T8 — Current full Release tests** | `dotnet test DPV.sln --configuration Release --no-build` passed: 55 passed, 0 failed, 0 skipped. |
| **T9 — Issue #12 Dashboard tests** | Focused Debug run of `DashboardNavigationTests`, `ShellStructureIntegrationTests`, and `DashboardIsRegisteredAndSelectedOnStartup` passed: 5 passed, 0 failed. Verifies the placeholder ViewModel, startup selection, Dashboard navigation command, shell DataTemplate/content binding, and production UI DI registrations. |
| **T10 — Issue #12 Windows runtime smoke test** | Started the Release desktop executable with an isolated temporary database. Windows UI Automation found the on-screen `3D Print Vault` window and verified `Dashboard`, `Workspace`, `Welcome to 3D Print Vault`, and `Your library overview will appear here.` Closing the window ended the application normally with exit code 0. |
| **T11 — Architecture tests** | `dotnet test tests/DPV.Tests/DPV.Tests.csproj --configuration Debug --filter FullyQualifiedName~Architecture` passed: 14 passed, 0 failed. Covers project-reference direction/cycles, Domain purity, package purity, composition-root ownership, and registration boundaries. |
| **T12 — Issue #14 integrated Windows runtime verification** | Launched the Release desktop executable using the configured JSON settings with process-level overrides only for an isolated database and log path. Startup created the SQLite file, emitted database initialization and Avalonia startup events to the configured rolling file sink, showed the titled Dashboard workspace and placeholder labels through Windows UI Automation, then logged Avalonia lifetime exit and returned exit code 0 after window close. Temporary runtime files were removed after verification. |
| **C1 — Prior remote CI** | [GitHub Actions run 41319335054](https://github.com/mike-n-va/3d-print-vault/actions/runs/41319335054), commit `ee552026`, completed successfully on `main`; both Ubuntu and Windows Build and Test jobs passed. This run predates the Issue #7 remediation and is background evidence, not validation of these local changes. |
| **D1 — DI verification** | [DI-VERIFICATION-REPORT-001](../architecture/DI-VERIFICATION-REPORT-001.md) records 24 passing tests and DI foundation verification. It explicitly excludes database initialization and has no end-to-end UI display test. |
| **D2 — Existing Sprint audit** | [SPRINT-1-CLOSURE-AUDIT-001](SPRINT-1-CLOSURE-AUDIT-001.md) records the historical issue evidence and limits for #5–#14. |
| **D3 — Issue #15 review** | [DPV-REPOSITORY-CONTRACT-OWNERSHIP-001](../architecture/DPV-REPOSITORY-CONTRACT-OWNERSHIP-001.md) records the successor contract review, all seven Issue #15 criteria as PASS, and the fact that Issue #10 remains open. |

B1, B2, and T1 apply to the current local working tree. C1 is a remote
workflow result for its stated commit; it is not evidence that every
acceptance criterion was met.

## 3. Criterion-by-Criterion Review

### Issue #5 — Dependency Injection Bootstrap

**Issue result: PASS**

| Acceptance criterion | Source-code evidence | Document evidence | Build evidence | Test evidence | Result |
|---|---|---|---|---|---|---|
| DI configured | `CompositionRootHostFactory`, `DependencyInjectionOrchestrator`, and project registration extensions configure the service collection. | D1 records composition-root and registration verification. | B2 | T1; composition-root integration tests execute registrations. | PASS |
| Services resolve correctly | `Program.ValidateCompositionRoot` resolves registered foundation services. | D1 records service-resolution validation. | B2 | T1; `ProductionRegistrationPathBuildsProviderWithAllFoundationMarkers` resolves the markers and logger factory. | PASS |
| Startup uses service collection | `Program.Main` builds the host from the factory and passes it to Avalonia startup. | D1 documents the single host/composition-root path. | B2 | T1; host lifecycle and composition-root integration tests pass. | PASS |
| Build succeeds | — | D1 records Debug and Release build results. | B2; C1 also passes on both CI operating systems. | T1 | PASS |

### Issue #6 — Configuration System

**Issue result: PASS**

| Acceptance criterion | Source-code evidence | Document evidence | Build evidence | Test evidence | Result |
|---|---|---|---|---|---|
| `appsettings.json` exists | `src/DPV.App/appsettings.json` exists and declares Database, Scanning, Viewer, and Logging sections. | DPV-CONFIGURATION-001 § Configuration Standard records the section contract. | B2 | T2 verifies both settings files and required sections. | PASS |
| Configuration loads correctly | `OptionsRegistration` binds Application, Database, Scanning, and Viewer options. Production and Development settings are read through JSON configuration providers. | DPV-CONFIGURATION-001 documents App ownership and scope limits. | B2 | T2 verifies binding from each settings file and exercises valid/invalid database option validation. | PASS |
| Database options created | `DatabaseOptions` exposes the required `Location` value; both settings files supply the approved declarative location. | DPV-CONFIGURATION-001 documents the retained values and unresolved path-resolution boundary. | B2 | T2 verifies each configured location and that an empty location fails host-start validation. | PASS |
| Scanning options created | `ScanningOptions` is bound to the `Scanning` section as a section-only options type; no unapproved scanning properties were invented. | DPV-CONFIGURATION-001 documents that scanning properties await approved requirements. | B2 | T2 resolves the options from both JSON files. | PASS |
| Viewer options created | `ViewerOptions` is bound to the `Viewer` section as a section-only options type; no unapproved renderer settings were invented. | DPV-CONFIGURATION-001 documents viewer settings and Helix compatibility as separately gated. | B2 | T2 resolves the options from both JSON files. | PASS |

### Issue #7 — Logging Framework

**Issue result: PASS**

| Acceptance criterion | Source-code evidence | Document evidence | Build evidence | Test evidence | Result |
|---|---|---|---|---|---|
| Serilog installed | `DPV.App.csproj` references Serilog hosting/configuration and console/file sink packages. | `DPV-CONFIGURATION-001` records the App logging path. | B2 | T3 builds and starts the production composition-root path with Serilog registration. | PASS |
| Console logging enabled | Bootstrap logger and production `appsettings.json` configure console output. | `DPV-CONFIGURATION-001` records bootstrap and host sink behavior. | B2 | T3 verifies the Console sink is configured and exercises host startup and event logging. | PASS |
| File logging enabled | `appsettings.json` configures the rolling file sink at `logs/dpv-.log`; the file sink package is referenced. | `DPV-CONFIGURATION-001` records the relative path and daily rotation. | B2 | T3 verifies the File sink configuration and exercises it through the host. | PASS |
| Log files generated successfully | The host is started, emits a uniquely identified event, stops, and flushes Serilog; the generated file is read and checked for that event. | `DPV-CONFIGURATION-001` records the isolated temporary-directory verification. | B2 | T3 passes on Release; generated log was validated then removed with its unique temporary directory. | PASS |

### Issue #8 — SQLite Bootstrap

**Issue result: PASS**

| Acceptance criterion | Source-code evidence | Document evidence | Build evidence | Test evidence | Result |
|---|---|---|---|---|---|
| SQLite package installed | `DPV.Infrastructure.csproj` references `Microsoft.Data.Sqlite` 10.0.14; Infrastructure registers the database factory and host initializer. | `DPV-CONFIGURATION-001` § Issue #8 documents ownership and scope. D1 is historical and predates this implementation. | B2 | T4 and T1 | PASS |
| Database created automatically | `DatabaseInitializer` creates the configured parent directory and opens the database during host startup. | `DPV-CONFIGURATION-001` documents deferred I/O and path semantics. | B2 | T4 verifies no directory exists before start, then verifies database file creation on start. | PASS |
| Connection works | `SqliteConnectionFactory` produces fresh SQLite connections with foreign keys enabled. | `DPV-CONFIGURATION-001` documents the factory and short-lived connections. | B2 | T4 opens a real connection and verifies foreign keys and WAL. | PASS |
| Migration system prepared | `MigrationRunner` maintains a version ledger, applies version 1 transactionally, verifies the record, and rejects unsupported future versions. It creates no catalog schema. | `DPV-CONFIGURATION-001` limits the baseline to bootstrap metadata; catalog schema remains gated. | B2 | T4 verifies initial migration, repeated startup, and rejection of a newer schema version. | PASS |

### Issue #9 — Initial Domain Entities

**Issue result: PASS**

| Acceptance criterion | Source-code evidence | Document evidence | Build evidence | Test evidence | Result |
|---|---|---|---|---|---|
| Artist entity created | `DPV.Domain.Entities.Artist` implements the Definition-009B identity, name validation, rename behavior, and value equality by ID. | [Definition-009B](../definitions/Definition-009B-Approved-Domain-Entities.md) defines the approved entity contract. | B2 compiles Domain and the solution. | T5 verifies creation, generated non-empty ID, trimmed name, invalid-name rejection, and rename validation. | PASS |
| Collection entity created | `DPV.Domain.Entities.Collection` implements its identity, name, optional Artist association, validation, and value equality by ID. | Definition-009B specifies optional Artist association and cardinality. | B2 | T5 verifies creation, optional association, name validation, and rename behavior. | PASS |
| Model entity created | `DPV.Domain.Entities.Model` implements its identity, name, optional Collection, immutable Tags collection, and approved association validation. | Definition-009B distinguishes logical printable asset from source file/path and specifies Model associations. | B2 | T5 verifies creation, optional Collection, empty/default Tags, distinct Tags, and rejected null/duplicate Tag entries. | PASS |
| Tag entity created | `DPV.Domain.Entities.Tag` implements its identity, name validation, rename behavior, and value equality by ID. | Definition-009B specifies Tag as a named Model classification. | B2 | T5 verifies creation, generated non-empty ID, trimmed name, invalid-name rejection, and rename validation. | PASS |
| No persistence logic | Entity files contain no persistence behavior; `DPV.Domain.csproj` has no project or package references. | DPV-ARCH-001 and Definition-009B prohibit persistence dependencies and exclude schema/repository behavior. | B2 | T1 includes Domain project/package purity and architecture boundary checks; T5 covers entity behavior only. | PASS |
| No UI dependencies | Domain entity declarations depend only on Domain code and BCL types. | DPV-ARCH-001 and Definition-009B require Domain purity. | B2 | T1 includes `DomainPurityTests` and `DomainPackagePurityTests`. | PASS |

### Issue #10 — Initial Repository Interfaces

**Issue result: CONDITIONAL — approved replacement is implemented, original issue remains open**

| Acceptance criterion | Source-code evidence | Document evidence | Build evidence | Test evidence | Result |
|---|---|---|---|---|---|
| All interfaces created | Four interfaces exist under `src/DPV.Domain/Contracts/Repositories/`. | D3 records the ownership and replacement review. | B2 compiles Domain and the solution. | T1 passes; no repository-operation behavior is defined or tested. | PASS |
| Interfaces compile | All four declarations compile in Domain. | D3 records successful scoped project builds. | B2 | T1 | PASS |
| Placed in Application layer | No `DPV.Application` exists. The approved replacement assigns public contracts to Domain; placement in the nonexistent project was deliberately not implemented. | D3 records Issue #10 replacement governance and Issue #15 PASS; Issue #10 itself remains open and unchanged. | B2 confirms the current solution builds without an Application project. | T1 does not test GitHub issue disposition. | FAIL as written; replaced by approved successor |
| No implementations yet | No repository implementations found. | D3 states implementations were not part of Issue #15. | B2 | T1 does not test for implementations. | PASS |

The original Issue #10 is not a literal acceptance PASS because its
Application-layer criterion conflicts with approved architecture. The
architecture-compliant successor, Issue #15, has a documented PASS review;
that does not itself close or change the GitHub state of Issue #10.

### Issue #11 — Main Shell Application

**Issue result: PASS**

| Acceptance criterion | Source-code evidence | Document evidence | Build evidence | Test evidence | Result |
|---|---|---|---|---|---|
| Application launches | `Program.Main` starts the host and `AvaloniaApplicationStartup` starts the Avalonia desktop lifetime. `MainWindow.axaml` declares the main shell window. | `DPV-UI-001` §7 requires runtime UI verification; T6 records a Windows desktop smoke test. | B2 builds the Release App and UI projects with 0 warnings/errors. | T6 confirms the on-screen titled window; T7 verifies shell XAML structure. | PASS |
| Sidebar visible | `MainWindow.axaml` defines `SidebarRegion` with the navigation heading and five section labels. | `DPV-UI-001` assigns presentation and navigation to DPV.UI. | B2 | T6 confirms the sidebar labels are exposed by Windows UI Automation in the visible window; T7 checks the declared structure. | PASS |
| Navigation region visible | `MainWindow.axaml` defines `NavigationRegion` with Workspace and a non-dashboard “Choose a section” placeholder. | T6 provides direct runtime observation; no Dashboard behavior was added. | B2 | T6 confirms the visible content labels; T7 checks the region and placeholder. | PASS |
| No crashes | Startup, visible lifetime, and normal close complete using the composition root. | T6 records the desktop smoke test and normal shutdown. | B2 | T6 app stayed running with its main window visible and exited normally after close. | PASS |

### Issue #12 — Dashboard Placeholder

**Issue result: PASS**

| Acceptance criterion | Source-code evidence | Document evidence | Build evidence | Test evidence | Result |
|---|---|---|---|---|---|
| Dashboard View created | `src/DPV.UI/Views/DashboardView.axaml` and its code-behind define the placeholder Dashboard View. | Issue #12 permits temporary dashboard content. | B2 builds the Avalonia view and its XAML. | T7 verifies the Dashboard view template and bound placeholder content. | PASS |
| Dashboard ViewModel created | `src/DPV.UI/ViewModels/DashboardViewModel.cs` owns the title and description shown by the view. | `DPV-UI-001` keeps presentation state in UI ViewModels; no catalog behavior is introduced. | B2 builds the ViewModel. | T9 verifies the placeholder properties and shell startup selection. | PASS |
| Navigation works | `ShellViewModel.ShowDashboardCommand` is bound to the Dashboard sidebar button in `MainWindow.axaml`; the shell binds its current ViewModel to the workspace `ContentControl`. | Existing UI/navigation ownership remains in DPV.UI under `DPV-ARCH-001` and `DPV-UI-001`. | B2 builds the command and compiled bindings. | T7 verifies the command/content bindings; T9 executes the command and verifies DI registration. | PASS |
| Dashboard loads on startup | `ShellViewModel` selects `DashboardViewModel` at construction; `MainWindow` receives the shell ViewModel through UI DI; its data template renders `DashboardView` inside the existing workspace region. | This fulfills the Issue #12 startup criterion without adding Dashboard analytics or future Sprint workflows. | B2 builds the complete startup path. | T9 verifies the initial selected ViewModel and shell bindings; T10 records the Windows runtime smoke test. | PASS |

### Issue #13 — GitHub Actions Pipeline

**Issue result: PASS**

| Acceptance criterion | Source-code evidence | Document evidence | Build evidence | Test evidence | Result |
|---|---|---|---|---|---|
| Workflow file created | `.github/workflows/ci.yml` defines the CI workflow. | Workflow definition is directly reviewable. | B2 | T1 | PASS |
| Pipeline executes automatically | Workflow triggers on pushes to `main` and pull requests; run 41319335054 executed on `main`. | C1 records the run and its commit. | C1: build steps succeeded on Ubuntu and Windows. | C1: test steps succeeded on both operating systems. | PASS |
| Build succeeds | Workflow includes `dotnet build DPV.sln --configuration Release`. | C1 | B2 and C1 | T1 and C1 | PASS |
| Tests run | Workflow includes `dotnet test DPV.sln --configuration Release --no-build`. | C1 | B2 | C1 records successful Ubuntu and Windows Release test jobs. | PASS |
| Green checkmark visible | Latest retrieved CI run and both matrix jobs have successful conclusions. | C1 | C1 | C1 | PASS |

This PASS applies to the pipeline acceptance criteria. It does not imply
Sprint 1 feature issues pass.

### Issue #14 — Build Verification

**Issue result: FAIL — validation passes, but the no-blockers criterion remains unmet**

| Acceptance criterion | Source | Test | Build evidence | Runtime evidence | Result |
|---|---|---|---|---|---|
| Restore succeeds | `DPV.sln` and project restore declarations. | B1: restore succeeded; 3 projects restored and 2 were current. | Not applicable. | Not applicable. | PASS |
| Build succeeds | The five solution projects: `DPV.App`, `DPV.UI`, `DPV.Domain`, `DPV.Infrastructure`, and `DPV.Tests`. | B2: Release solution build completed. | Build succeeded with 0 warnings and 0 errors. | Not applicable. | PASS |
| Tests pass | `tests/DPV.Tests`, including unit, integration, and architecture tests. | T1: 55 passed, 0 failed, 0 skipped; T11 architecture filter: 14 passed, 0 failed. | B2 confirms test project builds. | Not applicable. | PASS |
| Application launches | `src/DPV.App/Program.cs`, `Startup/CompositionRootHostFactory.cs`, `Startup/AvaloniaApplicationStartup.cs`; UI startup in `src/DPV.UI/App.axaml.cs` and `MainWindow.axaml`. | T12: direct Windows desktop smoke test. | B2 builds all startup projects. | T12 confirms titled window and Dashboard render, followed by normal close and exit code 0. | PASS |
| Database creates | `src/DPV.Infrastructure/Persistence/DatabaseInitializer.cs`, `SqliteConnectionFactory.cs`, and Infrastructure DI registration. | T4 verifies real host-start creation, connection settings, and migration behavior. | B2 builds the initializer and registration. | T12 confirms creation of the configured isolated SQLite file by the launched desktop host. | PASS |
| Logging works | `src/DPV.App/Logging/BootstrapLogging.cs`, `CompositionRootHostFactory.cs`, and `src/DPV.App/appsettings.json`. | T3 verifies generated file output and a unique event through host startup, shutdown, and flush. | B2 builds logging registration. | T12 confirms database initialization, Avalonia startup, and lifetime-exit messages in the configured rolling log. | PASS |
| No blocking issues remain | Issue #10 is still open; its Application-layer criterion conflicts with approved architecture and has no approved disposition. Issue #15's replacement review is PASS, but Issue #10 remains open and unchanged. | The acceptance review records Issue #10 as CONDITIONAL. `Sprint-1-Governance-Decisions.md` says closure gates remain unmet; Commit E recommends against Sprint closure. | Passing restore/build does not disposition an open requirement. | T1/T11 pass but cannot close/approve issues or governance gates; T12 confirms runtime health only. | FAIL |

The requested operational integration checks all pass, including configuration
loading from the application settings, logging initialization and file output,
SQLite creation, DI composition, Dashboard startup display, and clean host/UI
shutdown. Issue #14 cannot be marked PASS because its no-blockers requirement
and this task's success condition that no Sprint 1 acceptance criteria remain
unresolved are contradicted by Issue #10's still-open, undispositioned
acceptance criterion. This verification does not alter that issue or the
governance decision.

## 4. Issue Status Summary

| Issue | Title | Result | Basis |
|---:|---|---|---|
| #5 | Dependency Injection Bootstrap | **PASS** | DI configuration, service resolution, startup integration, and build/test evidence are present. |
| #6 | Configuration System | **PASS** | Database, Scanning, and Viewer options and settings sections are present and bound; required database location is validated; focused integration tests pass. |
| #7 | Logging Framework | **PASS** | Serilog package references and Console/File configuration are present; host-start integration test verifies a uniquely identified event is written to and flushed into a generated rolling log file. |
| #8 | SQLite Bootstrap | **PASS** | Infrastructure SQLite package, host-start database creation, real connection settings, and idempotent version-ledger tests pass. |
| #9 | Initial Domain Entities | **PASS** | Four Domain entities implement Definition-009B; focused unit tests and Domain purity/architecture checks pass. |
| #10 | Initial Repository Interfaces | **CONDITIONAL** | Domain contracts and approved Issue #15 replacement pass; original Application-layer criterion conflicts with architecture, and Issue #10 remains open. |
| #11 | Main Shell Application | **PASS** | Release app launch and normal shutdown were observed on Windows; UI Automation verified the visible sidebar and navigation content; shell structure test passes. |
| #12 | Dashboard Placeholder | **PASS** | Dashboard View and ViewModel are registered in DPV.UI; Dashboard sidebar navigation and workspace content binding are tested; the ViewModel is the startup selection; Release runtime smoke test confirms visible Dashboard content and normal shutdown. |
| #13 | GitHub Actions Pipeline | **PASS** | Workflow exists, runs automatically, and the latest retrieved Ubuntu/Windows build-and-test jobs passed. |
| #14 | Build Verification | **FAIL** | Restore, Release build, all 55 tests, 14 architecture checks, application startup/shutdown, Dashboard display, SQLite creation, and logging all pass. The no-blockers criterion remains unmet because Issue #10 is still open and undispositioned. |

## 5. Overall Assessment

The review finds **8 PASS**, **1 CONDITIONAL**, and **1 FAIL** issue results.
The current Release build passed with no warnings or errors; the full Debug
suite passed 55/55, including 14 architecture tests. The Release desktop
smoke test confirmed configuration-backed startup, database initialization,
file logging, visible Dashboard content, and normal shutdown. Issue #14
remains FAIL only because the Sprint-wide no-blockers condition is not met:
Issue #10 remains open without an approved disposition. The latest
retrieved remote CI run is green on Ubuntu and Windows but predates the
Issue #7 changes; it is not presented as validation of this revision. These
checks validate the current foundation and pipeline, including Dashboard and
Issue #14 runtime evidence; they do not substitute for the unresolved Issue
#10 disposition.

This review makes no GitHub issue-state or milestone changes and does not
authorize Sprint 1 closure. In particular, Issue #10's architecture-compliant
replacement evidence is recorded separately from the original issue's
unmodified GitHub state.
