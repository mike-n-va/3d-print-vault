# REA-005: Testing Foundation

**Project:** 3D Print Vault
**Review date:** 2026-10-31
**Status:** Research complete
**Scope:** Testing strategy before feature implementation
**Governing standard:** [DEVELOPMENT_STANDARD.md](../../DEVELOPMENT_STANDARD.md)

---

## 1. Executive Summary

3D Print Vault should use a layered, behavior-oriented test strategy that stays fast for everyday work and adds realistic integration, UI, scale, and release checks only where they provide evidence that unit tests cannot.

### Recommended baseline

| Area | Recommendation |
|---|---|
| Test framework | **xUnit v3** for new tests; it fits .NET 10, is widely used in modern .NET projects, supports common IDE/CI workflows, and has both VSTest and Microsoft.Testing.Platform paths. |
| Assertion library | **Use xUnit's built-in assertions by default.** Do not standardize on FluentAssertions without an explicit commercial-license decision. Consider Shouldly only if the team wants fluent syntax and verifies its current terms. |
| Test doubles | **NSubstitute**, used sparingly for external boundaries. Prefer small hand-written fakes for deterministic infrastructure and avoid mocking SQLite, Dapper, or internal collaborators. |
| Architecture checks | **ArchUnitNET** tests plus a narrow project-reference check for the documented project graph. Run rules against production assemblies, including Release builds. |
| Avalonia | Test ViewModels as ordinary .NET classes; use **Avalonia.Headless.XUnit** for a small number of control, binding, and interaction tests. Keep native rendering and visual baselines out of the default unit suite. |
| Dependency injection | Build the real registration graph in a test with `ValidateOnBuild` and `ValidateScopes`, then resolve important application entry points and verify meaningful lifetimes. |
| SQLite/Dapper | Use in-memory SQLite for focused, single-connection tests; use unique temporary database files for repository, migration, transaction, multi-connection, and file-behavior integration tests. |
| CI | Run restore/build/fast tests/architecture tests on every PR and protected-branch update. Run broader OS, performance, large-fixture, migration, and packaging checks nightly or before release. |
| AI-assisted work | Provide agents with executable examples, explicit invariants, architecture rules, deterministic fixtures, and required test commands. Review whether tests could fail for the right reason, not just whether generated tests pass. |

### Verdict

**APPROVE WITH CHANGES.** The current repository already has an xUnit test project and documented dependency boundaries, so the direction is sound. Before feature implementation, make the framework and boundary-testing choices explicit, replace the empty placeholder test with meaningful foundation tests, and establish repeatable CI gates. This report is research only; it does not make those changes.

### Evidence limits

- There is no authoritative, current census proving one .NET test framework is used by a majority of developers. xUnit is a common modern .NET default, not a universal winner.
- No reliable comparative study was found showing that one test framework or assertion library inherently improves AI-generated code. AI quality depends more on clear contracts, useful test failures, and deterministic test execution.
- Package features, versions, and license terms change. Recheck them at adoption and during dependency updates.

---

## 2. Findings

### Repository and governing-standard observations

- [DEVELOPMENT_STANDARD.md](../../DEVELOPMENT_STANDARD.md) requires research before implementation, a reviewable artifact under `docs/research`, and verification as a feature-completion phase.
- The project targets `net10.0`. The existing test project is `DPV.Tests`, references all four production projects, and currently uses xUnit 2.9.3 with the Visual Studio runner. It contains only an empty placeholder test.
- [DPV-DEPENDENCY-MAP-001.md](../architecture/DPV-DEPENDENCY-MAP-001.md) documents the intended graph and requires `DPV.Domain` to remain dependency-free. It currently permits `DPV.UI` to reference `DPV.Infrastructure`, while also forbidding UI-owned SQL, SQLite access, file I/O, and business logic. Tests should enforce the approved graph and the forbidden responsibilities without silently changing that architecture.
- Avalonia is currently pinned at 12.1.4. The current `Avalonia.Headless.XUnit` package page lists support for Avalonia 12.1.4 and xUnit v3, which makes the recommended headless path directly relevant to this repository.
- SQLite is not yet a production dependency in `DPV.Infrastructure`; this report recommends testing patterns, not a provider choice or implementation.

### Cross-cutting principles

1. **Test observable behavior and invariants.** Avoid tests coupled to private methods, call order, exact object construction, or incidental View layout.
2. **Use the real boundary when it is the thing under test.** A mock cannot validate SQL, Dapper mapping, SQLite locking, migrations, file permissions, or Avalonia bindings.
3. **Keep tests isolated and deterministic.** Give each database and file-system test a unique temporary workspace; avoid shared mutable state, network access, sleeps, wall-clock assertions, and test-order assumptions.
4. **Treat a passing test as evidence with limits.** Unit tests do not establish production-scale throughput, real GPU rendering, successful upgrades from old databases, or offline behavior unless those are explicitly exercised.
5. **Keep test policy proportional.** Start with the small tool set and fast tests below; add specialized frameworks only when an identified risk justifies their maintenance cost.

Microsoft's .NET guidance emphasizes fast, isolated, readable tests with minimal setup, behavior-focused assertions, and no dependence between tests. Those principles apply regardless of the selected runner or mocking library ([.NET unit-testing best practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)).

---

## 3. Framework Comparison

### Section A — xUnit, NUnit, and MSTest

| Framework | Strengths | Trade-offs | DPV fit |
|---|---|---|---|
| **xUnit** | Common in contemporary .NET open-source projects; concise `[Fact]`/`[Theory]` model; mature CI and IDE ecosystem; xUnit v3 supports modern .NET and both VSTest and Microsoft.Testing.Platform integrations. Existing project already uses xUnit. | v2 and v3 are distinct major generations; migration changes packages and some APIs. Fixture and parallelization conventions require team consistency. | **Best fit.** The project has almost no tests to migrate, so choose v3 before the suite grows. |
| **NUnit** | Mature, broad feature set, extensive history and integrations; good parameterized-test and lifecycle support. | More setup/attribute surface than DPV needs; selecting it would not improve Clean Architecture or AI output by itself. | Viable, but no compelling reason to switch to it. |
| **MSTest** | First-party Microsoft framework; strong Visual Studio integration; modern MSTest supports the Microsoft test platform. | Its first-party status does not make it more architecturally correct; the existing suite and Avalonia test integration point naturally toward xUnit. | Viable if a Microsoft-specific test feature becomes necessary, but not the default recommendation. |

#### Answers

1. **Most commonly used:** xUnit is the safest answer for a widely adopted modern .NET default, especially in open-source libraries and application repositories. Treat this as an ecosystem observation, not a measured market-share fact; no current authoritative usage census settles the question.
2. **GitHub Actions, AI-generated tests, Clean Architecture:** all three work with `dotnet test` and GitHub Actions. None has an inherent Clean Architecture advantage. xUnit has the strongest fit with this repository's current test project and the current Avalonia headless xUnit package. AI tools can generate tests for any of the three; a documented local convention matters more than framework choice.
3. **Ecosystem:** xUnit, NUnit, and MSTest all have capable runners, IDE integrations, assertion support, and CI use. xUnit provides an especially good balance of community adoption and modern .NET integration for DPV.
4. **Adopt:** **xUnit v3** for new tests. The xUnit project describes support for .NET 8+ and compatibility with VSTest and Microsoft.Testing.Platform ([xUnit](https://xunit.net/)).

#### .NET 10 test-platform choice

The .NET 10 SDK supports both the established VSTest mode and the newer Microsoft.Testing.Platform (MTP) mode for `dotnet test`. These modes do not have identical CLI options or extension behavior ([Microsoft.Testing.Platform overview](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-intro), [`dotnet test` platform modes](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test)).

**Recommendation:** begin with the ordinary `dotnet test` workflow supported by the selected xUnit v3 packages and use one canonical test-platform mode in local instructions and CI. Evaluate MTP deliberately when its runner features are useful; do not invoke both platforms on every change or assume VSTest-only flags apply to MTP.

### Section B — FluentAssertions, built-in assertions, and Shouldly

| Option | Benefits | Costs and cautions |
|---|---|---|
| **xUnit built-in `Assert`** | No additional assertion package; familiar across C# projects and AI-generated examples; direct expectations; good enough for most value, exception, collection, and async assertions. | Some expressions are less fluent and some failures less descriptive than dedicated assertion libraries. |
| **FluentAssertions** | Broad fluent API, object-graph equivalency, clear natural-language expectations, helpful failure messages, and domain-specific assertion extensions. Can improve local readability when used consistently. | Version 8 and later use Xceed's license: commercial use requires a paid license, while the community terms are limited to defined non-commercial/open-source use. It also adds API surface and can encourage broad equivalency assertions that hide which contract matters. See the [current license](https://raw.githubusercontent.com/fluentassertions/fluentassertions/main/LICENSE) and [project README](https://github.com/fluentassertions/fluentassertions). |
| **Shouldly** | Fluent, compact assertions with failure messages that include the source expression; an alternative when readable failures are a priority ([project README](https://github.com/shouldly/shouldly)). | Another dependency and syntax to learn. Confirm current license terms and project compatibility before standardizing. |

#### Answers and recommendation

- FluentAssertions can make complex assertions easier to read and failures easier to diagnose. That can help maintainability when assertions state a specific behavior rather than comparing entire object graphs.
- It does **not** make generated tests correct by itself. A fluent but tautological or overly broad assertion is still a bad test.
- For a project intended for long-term development whose eventual commercial use is not established by this research, avoid making the FluentAssertions license a hidden build dependency.
- **Adopt built-in xUnit assertions as the project default.** If contributors later find the diagnostics insufficient, consider Shouldly after license review. Reconsider FluentAssertions only after recording an explicit commercial-license decision and acceptable upgrade policy.

### Section C — Moq, NSubstitute, and FakeItEasy

| Framework | Character | DPV considerations |
|---|---|---|
| **Moq** | Mature and highly visible; setup/verification APIs are familiar in many .NET examples. The repository distinguishes its stable v4 branch from its evolving v5 work ([Moq repository](https://github.com/devlooped/moq)). | Broad example availability can help AI-generated drafts, but version differences can make copied examples stale. Setup/verify syntax can add noise. |
| **NSubstitute** | Designed for concise substitutes with natural-language setup and received-call assertions; includes optional analyzers. It explicitly works with interfaces or virtual members ([NSubstitute repository](https://github.com/nsubstitute/NSubstitute)). | Requires understanding argument matching and call recording; non-virtual class members cannot be intercepted. |
| **FakeItEasy** | Focuses on fake objects and a context-aware fluent API; supports faking interfaces and classes ([FakeItEasy repository](https://github.com/FakeItEasy/FakeItEasy)). | Its `A.CallTo` style is another convention for contributors and agents to learn. |

#### Answers and recommendation

- There is no defensible universal current “industry preference” across all .NET teams. Moq has substantial historical visibility; all three are established choices.
- NSubstitute's basic API has a low learning curve. The other frameworks are also approachable, but become more intricate when tests over-configure calls.
- AI compatibility is not objectively ranked. Moq may be more represented in generic examples, while NSubstitute's compact syntax can make intent easier to review. Whichever is chosen, keep a small checked-in example and reject unsupported or version-mismatched generated APIs.
- All mocking libraries can increase maintenance burden when tests assert implementation choreography rather than outcomes.
- **Adopt NSubstitute**, with a strict boundary: substitute external collaborators (clock, notification, service interfaces) only where controlling their behavior is useful. Use concrete temporary files and SQLite for persistence tests; prefer a small handwritten fake when it is simpler than a dynamic substitute. Do not mock every class or private implementation step.

---

## 4. Architecture Testing Analysis

### Section D — Dependency rules and architectural boundaries

Successful architecture enforcement combines an explicit allowed-dependency policy with automated checks in the normal test gate. Code review alone is not reliable protection against slow, repeated dependency drift.

| Approach | What it is good at | Limitations |
|---|---|---|
| **Project-reference checks** | Exact project graph, cycles, and prohibited `<ProjectReference>` edges. Can inspect the solution/project metadata or use build tooling. | Does not detect a forbidden package reference or a source-level dependency that is allowed by the project graph. |
| **ArchUnitNET** | Testable rules over compiled C# type, namespace, member, and dependency relationships; supports xUnit v3 through its dedicated integration package. It analyzes assemblies and is designed to make architecture rules executable ([project](https://github.com/TNG/ArchUnitNET), [xUnit v3 package](https://www.nuget.org/packages/TngTech.ArchUnitNET.xUnitV3)). | Requires deliberate assembly loading and rule scope. Bytecode-analysis coverage and version changes must be understood; rules can only protect what they actually scan. |
| **NetArchTest** | Lightweight fluent rules for namespaces, type dependencies, and conventions; works with different test frameworks ([project](https://github.com/BenMorris/NetArchTest)). | The examined repository history showed its latest visible commits in 2023. It remains a possible lightweight option, but ArchUnitNET has a more active current release line and an explicit xUnit v3 extension. |
| **Custom Roslyn/MSBuild analyzers** | Can fail builds early and enforce project-specific source or reference constraints. | More maintenance and implementation effort; unnecessary as the first architecture-testing mechanism for the current solution. |

### Answers and recommendation

1. Enforce dependency direction as a version-controlled rule, and run it with ordinary tests in CI. Keep allowed edges in sync with [DPV-DEPENDENCY-MAP-001.md](../architecture/DPV-DEPENDENCY-MAP-001.md).
2. Detect forbidden references with both (a) a check of the actual project/package reference graph and (b) compiled-type dependency rules for prohibited namespaces and assemblies. A namespace-only rule is not a substitute for checking project references.
3. Protect Domain purity, UI responsibilities, and Infrastructure direction with explicit rules:
   - `DPV.Domain` has no project or third-party package dependencies.
   - Domain types do not depend on UI, SQLite, file-system, renderer, or Infrastructure types.
   - UI does not directly own SQL, database access, file I/O, import, hashing, or business rules.
   - Infrastructure may depend inward on Domain, not outward on UI or App.
   - App may compose services but must not become the owner of persistence or business behavior.
4. **Adopt ArchUnitNET** for type-level rules, with a small project-graph check in the same architecture test area. Load architecture metadata once per test collection/run where safe; provide useful failure descriptions naming the forbidden dependency.

ArchUnitNET's 0.13.5 release notes document a significant prior failure mode: dependencies inside optimized async methods and async lambdas could be missed in Release builds, and the release fixed that analysis gap ([release notes](https://github.com/TNG/ArchUnitNET/releases)). Pin a version containing the fix, read upgrade notes, and ensure the architecture gate runs in **Release** at least once. Never infer that a passing rule proves complete coverage without checking the library's limitations.

**Existing-boundary caveat:** the current dependency map explicitly allows `DPV.UI → DPV.Infrastructure` even though UI-owned persistence is forbidden. Tests should enforce the approved graph as written and restrict UI's actual type dependencies to permitted APIs. If the project later wants a stricter Application-layer boundary, approve and document that architectural change separately before changing the rules.

---

## 5. Avalonia Testing Analysis

### Section E — Avalonia and MVVM

#### Unit test

- Domain behavior and value-object invariants, without Avalonia references.
- ViewModel state transitions, commands, validation, notifications, selection, filtering, and error state.
- Async command behavior: success, failure, cancellation, stale-result suppression, and disposal.
- Search and library workflows through injected interfaces/fakes; include offline/error outcomes.
- Converters and small UI-independent formatting logic.

Keep ViewModels testable without constructing a Window or a rendered visual tree. Use ordinary xUnit tests, explicit test data, and substitutes/fakes only at dependency boundaries.

#### Integration test

- A small set of `Avalonia.Headless.XUnit` tests for controls, binding, command wiring, navigation, focus/interaction where relevant, and the visual-tree behaviors that cannot be tested at the ViewModel level.
- Startup smoke tests that verify the application composes and opens its initial navigation state.
- Platform-specific/manual or release-gated checks for native window behavior, high-DPI, accessibility, input devices, GPU/Helix rendering, and packaging.

The current [Avalonia.Headless.XUnit package](https://www.nuget.org/packages/Avalonia.Headless.XUnit) targets xUnit v3 and lists compatibility with Avalonia 12.1.4 and .NET 10. Headless UI tests still execute Avalonia's dispatcher/control infrastructure and require careful setup and teardown; they are more expensive and more susceptible to framework-version/platform changes than ViewModel unit tests.

#### Do not test by default

- Every XAML property, every control, or every layout detail in isolation.
- Private implementation details, binding names, or framework internals unless a concrete regression requires it.
- Large suites of pixel snapshots. Fonts, rasterization, platform scaling, themes, and native renderers make broad screenshot baselines brittle.
- GPU rendering or real 3D rendering on every PR if the runner has no stable graphics environment.
- Timing assumptions based on `Thread.Sleep`, real hardware, or network availability.

#### Overhead and deferral

Headless tests add package/version coupling, dispatcher lifecycle rules, UI-thread constraints, and additional runtime/setup time. Native integration and visual tests require OS- and graphics-capable workers. Begin with ViewModel unit tests and a few representative headless integration tests. Defer cross-platform screenshot baselines, full-window end-to-end automation, GPU/Helix rendering tests, accessibility audits, and installer interaction tests to release/nightly validation as relevant features become available.

**Recommendation:** test ViewModels heavily at the unit level, Views selectively with headless integration tests, and actual native rendering on supported release environments. Tests should assert user-visible behavior and bindings rather than implementation layout.

---

## 6. Dependency Injection Testing Analysis

### Section F — `Microsoft.Extensions.DependencyInjection`

DI registration is production composition logic and should be tested. The test should use the same registration extensions/composition root as the application instead of maintaining a parallel list of test-only registrations.

#### Safeguards

- Build a provider in a composition test with `ValidateOnBuild = true` and `ValidateScopes = true`.
- Resolve the important application entry points and representative registrations. Validation does not necessarily execute every factory or configuration-dependent path, so add focused resolution tests for those.
- Test options/configuration validation using both valid and invalid configurations.
- Verify lifetimes only where they represent behavior: singleton identity, scoped identity within a scope and difference across scopes, and disposal ownership.
- Keep provider construction side-effect free: registering services should not scan folders, open the real catalog, initialize GPU state, or access the network.
- Keep expensive/native integrations replaceable at explicit boundaries in tests.
- Dispose the provider and scopes correctly.
- Do not call `BuildServiceProvider` inside production service-registration code; doing so creates a second container and can produce duplicate singleton instances and ownership problems.

Microsoft's DI guidelines recommend small, testable services and describe lifetime/disposal behavior; the built-in container exposes `ValidateOnBuild` and `ValidateScopes` options ([DI guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines), [`ValidateOnBuild`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.serviceprovideroptions.validateonbuild)).

#### Common failures

- Missing registration or wrong interface-to-implementation mapping.
- A singleton capturing a scoped service (“captive dependency”).
- A registration that works only because production configuration or a file happens to exist.
- A factory that throws only when first resolved, escaping superficial registration checks.
- Duplicate registrations silently choosing an unexpected last/first service.
- Disposed resources being reused or disposed by both container and caller.
- Tests resolving only types that are manually registered in the test rather than the real application graph.

**Recommendation:** one composition test for the production registration path, plus focused tests for meaningful scopes, options, and application entry points. Treat container validation as a safety net, not proof that all runtime behavior is valid.

---

## 7. Database Testing Analysis

### Section G — SQLite and Dapper

SQLite's storage mode changes what an integration test can establish. The special `:memory:` database exists only for a connection's lifetime; separate connections to `:memory:` are separate databases. Named shared in-memory databases are available, but require shared-cache semantics and at least one open connection to keep the database alive ([SQLite in-memory databases](https://www.sqlite.org/inmemorydb.html), [Microsoft.Data.Sqlite in-memory databases](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/in-memory-databases)).

#### Test layers

1. **Pure unit tests:** domain rules, mapping-independent application logic, input validation, and query parameter construction only when it represents meaningful behavior. Do not mock SQL execution to claim repository correctness.
2. **In-memory database tests:** small, fast repository cases that keep one connection open and execute the real schema and SQL. Good for basic mapping and query behavior.
3. **Temporary file-backed database tests:** unique file per test for multiple connections, repositories opening their own connections, transactions, migrations, foreign-key configuration, journal/locking behavior, recovery, and file lifecycle. These are essential for this application.
4. **Scale/performance tests:** synthetic catalog rows and representative file metadata at controlled scale, run outside the ordinary unit-test loop.

#### Best practices

- Test repository methods with the actual SQLite provider and Dapper, not mocked `IDbConnection` behavior.
- Build schema using the same migration/schema initialization path as the application.
- Use a unique temporary directory and database filename per test; clean it up in `finally`/disposal even after failure. Never share a checked-in mutable database between parallel tests.
- Apply the same connection initialization/PRAGMAs used by production. Specifically verify foreign-key enforcement and any WAL/busy-timeout policy the application adopts; do not assume provider defaults.
- Test transaction rollback/commit and migration from representative older schemas.
- Keep database lifetime explicit. In-memory tests that close the keeper connection can silently lose the database.
- Validate a representative set of Dapper type mappings, null handling, parameter binding, and result ordering. Make ordering explicit in SQL when the contract requires it.
- Test error and cancellation paths without broad catches or success-shaped fallback behavior.

#### Scale and multi-terabyte libraries

Do not put a multi-terabyte library or 250,000 real model files in each PR run. Keep metadata/catalog correctness separate from large file-system scanning. On a scheduled or release lane, generate deterministic synthetic metadata at 250,000+ rows and representative file trees; measure query latency, memory, scan throughput, cancellation, and recovery. Track results as trends and budgets, not fragile single-run stopwatch assertions.

For file-system behavior, add smaller deterministic cases for deep/nested directories, long names, unreadable paths, missing files, duplicate names, cancellation, and offline operation. Use selected performance scenarios for actual large-scale scanning. A test database with 250k metadata records does not establish performance across multiple terabytes of files.

**Recommendation:** use both memory and disk-backed SQLite tests, with file-backed integration tests as the authority for repository behavior. Dapper's role as a thin object mapper does not remove the need to test real provider semantics ([Dapper repository](https://github.com/DapperLib/Dapper)).

---

## 8. CI/CD Testing Recommendations

### Section H — GitHub Actions and .NET workflows

GitHub documents a standard .NET workflow around `setup-dotnet`, restore, build, and `dotnet test`, and supports caching based on committed NuGet lock files ([Building and testing .NET](https://docs.github.com/en/actions/tutorials/build-and-test-code/net)). Keep the workflow close to the commands developers run locally.

| Trigger | Required validation |
|---|---|
| **Every commit / branch push** | Restore, compile, run fast unit and architecture tests, and publish test results. Keep this lane short. |
| **Pull request** | All above; release-configuration build; repository formatting/analyzer checks; SQLite and DI integration tests; small Avalonia headless smoke suite; coverage report as a diagnostic and trend. Require green checks before merge. |
| **Nightly / scheduled** | Larger synthetic catalog and directory fixtures, performance/benchmark trend, broader headless/UI smoke, dependency/security review, and any tests too slow for every PR. |
| **Before release** | Full supported-OS build/package matrix; clean install/startup smoke; migration/upgrade and backup/restore checks; offline behavior; realistic performance budgets; native UI/Helix checks on capable runners; final dependency/license review. |

#### Workflow design

- Pin the .NET SDK/toolchain intentionally and use the .NET 10 LTS feature band that the team supports.
- Commit NuGet lock files before enabling `setup-dotnet` package caching; restore in locked mode so CI tests the reviewed dependency graph.
- Build once in Release and run tests without rebuilding where the selected runner permits. Upload TRX/test diagnostics even on failure.
- Separate test categories/tags by cost and capability; do not silently skip required tests because a runner lacks a native dependency.
- Cancel superseded PR runs when safe, but do not cancel required release publication or migration checks.
- Use least-privilege GitHub token permissions, avoid secrets in untrusted fork PR workflows, and pin third-party Actions to reviewed versions/SHAs. GitHub specifically recommends restricting `GITHUB_TOKEN` permissions ([workflow syntax](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#permissions)).
- Treat code coverage as a discovery/trend tool, not a quality score. A line-coverage target cannot show whether assertions detect meaningful regressions. Use risk-based coverage and optionally mutation testing later for critical rules.
- Use OS matrices where OS behavior matters; a cross-platform UI framework does not prove that one OS run validates all native packaging and rendering behavior.

**Recommended order:** a reliable fast PR gate first; add slower lanes only when the corresponding feature or risk exists. Avoid running full scale, GPU, and all-platform tests on every commit if this makes contributors wait or encourages skipped checks.

---

## 9. AI Development Considerations

### Section I — Cursor, Claude Code, Antigravity, and generated code

No evidence reviewed establishes that Cursor, Claude Code, Antigravity, or another coding agent produces more correct tests simply because a project chose xUnit, Moq, or a fluent assertion library. These tools can all work with a normal .NET test project. The project should optimize for **clear test contracts and fast, unambiguous feedback**, not for an unsupported tool ranking.

#### Practices that improve AI-assisted implementation

- Give each task a narrow requirement, relevant architecture decision, acceptance examples, and the exact local command for validation.
- Keep representative tests near the code and use consistent, descriptive test names. A passing example demonstrates the expected style to the next agent.
- Encode dependency direction and purity in architecture tests so generated references to SQLite/UI from Domain fail automatically.
- Ask for behavior-first tests around edge cases, errors, cancellation, offline operation, and persisted data—not just the happy path.
- Require the agent to run the focused test, then the PR-equivalent gate, and report actual output. Never accept “tests added” as evidence that they pass.
- Review generated tests for an independent oracle: an assertion derived from the requirement rather than a copy of the implementation's calculation or a tautological assertion.
- Keep fixtures small and explicit; make temporary directories/databases unique; seed randomness when used; avoid real user data, network calls, sleeps, local machine paths, and implicit environment assumptions.
- Provide a minimal NSubstitute/fake example and use only the selected xUnit/assertion APIs. This lowers hallucinated or stale package patterns.
- Preserve test-result artifacts and actionable failure messages so an agent can distinguish a product regression from an environment failure.

#### Artifacts with the most guidance value

1. This testing foundation and the approved project dependency map.
2. Short feature contracts and acceptance criteria from the governed development flow.
3. Executable architecture tests and focused examples for the chosen test doubles.
4. Deterministic fixture builders for database schemas, file paths, and ViewModel dependencies.
5. One canonical CI command sequence and saved failure logs/TRX files.
6. Explicit test categories and statements of what is *not* covered by a given test.

#### Anti-patterns to prohibit

- Generating tests that duplicate production logic, test a mock's configured return value, or assert only that a method did not throw.
- Mocking Dapper, SQLite, every repository, or framework internals instead of exercising real boundaries.
- Asserting exact private call order or incidental XAML structure.
- Broad snapshot/equivalency assertions that obscure the contract.
- Nondeterministic generated data, shared database/files, tests that depend on execution order, and blanket retries that conceal flakiness.
- Treating compile success, high coverage, or an AI's claim of completion as proof of correctness.
- Creating unnecessary abstractions solely to satisfy a brittle test.

**Recommendation:** keep the test surface framework-neutral where possible, but standardize xUnit v3, built-in assertions, and NSubstitute conventions in the test project. Use architecture and integration tests as guardrails that agents cannot bypass by merely generating plausible-looking unit tests.

---

## 10. Risk Assessment

| Rank | Severity | Risk from poor testing architecture | Mitigation |
|---:|---|---|---|
| 1 | **High** | Dependency drift lets UI, Infrastructure, and Domain responsibilities leak across project boundaries. | Enforce both project-reference and type-level rules in CI; make `DPV.Domain` purity a non-negotiable architecture test. |
| 2 | **High** | Mock-heavy or tautological tests pass while actual behavior is broken. | Test outcomes and invariants; use real SQLite/file boundaries for integration; review generated tests for an independent oracle. |
| 3 | **High** | In-memory-only database tests miss connection lifetime, file locking, WAL, migration, and multi-connection failures. | Include unique temporary file-backed SQLite repository tests and migration checks. |
| 4 | **High** | The application appears correct on small fixtures but becomes unusable at 250k+ records or huge libraries. | Schedule synthetic scale/performance tests; measure query, scan, memory, and cancellation behavior separately. |
| 5 | **High** | Flaky tests caused by shared paths/databases, timing assumptions, platform state, or test order erode trust and get ignored. | Hermetic per-test resources, deterministic inputs, cleanup, no sleeps/network, and quarantine only with an owner and expiry. |
| 6 | **High** | Offline-first requirements regress because tests rely on external services or never prove no-network/error behavior. | Keep CI network-independent after restore; test offline workflows, cancellation, missing roots, and recovery explicitly. |
| 7 | **Medium** | UI tests either omit binding/interaction defects or become a brittle screenshot suite. | Unit-test ViewModels; add a few Avalonia headless integration tests; defer native visual checks to capable lanes. |
| 8 | **Medium** | Database schema changes break existing user libraries or lose data during upgrade. | Test migrations from representative prior versions, rollback/backup recovery, and release upgrade paths. |
| 9 | **Medium** | Tool or package policy changes (especially licenses and runner integration) create unexpected cost or a disruptive migration. | Prefer low-dependency defaults; record licenses; pin and review upgrades; do not adopt FluentAssertions without commercial approval. |
| 10 | **Low** | A slow, overscoped suite delays feedback and drives developers/agents to skip tests. | Keep a fast required PR lane, move high-cost tests to scheduled/release gates, and review runtime and flake trends. |

---

## 11. Final Recommendations

1. **Test runner:** standardize on xUnit v3 for the new test foundation. Do not switch frameworks solely for a claimed AI or Clean Architecture advantage.
2. **Assertions:** use built-in xUnit assertions. Avoid FluentAssertions as a default unless the project explicitly accepts the current commercial-use license. Revisit Shouldly only if diagnostic value warrants another dependency.
3. **Mocking:** standardize on NSubstitute for small boundary substitutions; prefer explicit fakes and real integration boundaries where they improve confidence.
4. **Architecture:** use ArchUnitNET plus a project-reference graph check. Run them in Release and ensure the analyzed assemblies and dependency scope are explicit.
5. **Avalonia/MVVM:** put most UI behavior in directly testable ViewModels; add a small set of Avalonia headless tests for real binding/control interactions. Defer broad native rendering and screenshot automation.
6. **DI:** test the real composition root with provider validation, scopes, options, and key entry-point resolution.
7. **SQLite/Dapper:** use both in-memory and temporary file-backed databases. File-backed integration tests are required for behavior involving separate connections, persistence, transactions, migrations, and SQLite file semantics.
8. **Scale:** test 250k+ catalog metadata and representative file trees on scheduled/release lanes, not with multi-terabyte data in every PR. Measure performance trends; avoid flaky single-run timing assertions.
9. **CI:** make fast tests, architecture rules, build, and test-result publication required PR checks. Add supported-OS, migration, offline, performance, and native UI checks as features make them relevant.
10. **AI-assisted development:** require generated code to satisfy the same tests and architecture rules as human-written code. Keep contracts, examples, deterministic fixtures, and validation commands discoverable; do not equate generated test volume or coverage with correctness.

### Answer to the central question

Testing should progress from fast isolated domain/application/ViewModel tests, through real SQLite/DI/Avalonia integration tests, to scheduled synthetic-scale and release-platform validation. The default CI gate should be hermetic, repeatable, and fast enough to run for every pull request. Architectural boundaries should be executable rules, not comments or review conventions. As scale and native features arrive, add tests that represent those specific operational risks rather than making every test imitate the full production environment.

---

## 12. Implementation Strategy

This is a sequence of recommended future work, **not implementation performed by this report**.

### Phase 0 — Establish the test contract

- Approve xUnit v3, built-in assertions, NSubstitute, ArchUnitNET, and the canonical `dotnet test` platform mode.
- Confirm package versions, current license terms, and xUnit/Avalonia compatibility at adoption.
- Decide whether the documented `DPV.UI → DPV.Infrastructure` edge remains intentional; update architecture documents through the project's governance process if not.

### Phase 1 — Before the first feature

- Replace the empty placeholder test with meaningful test-foundation checks.
- Create the planned `Unit`, `Integration`, and `Architecture` test areas without splitting assemblies unless build isolation or platform needs justify it.
- Establish architecture rules and a production DI composition test.
- Establish the required PR workflow: restore, Release build, tests, architecture gate, and saved results.
- Provide a reusable, disposable temporary-directory/database fixture only when the first integration tests need it.

### Phase 2 — With each feature

- Add unit tests for domain rules, boundaries, and ViewModel behavior.
- Add integration tests for SQL/schema, Dapper mapping, file-system operations, and DI composition when those seams are introduced.
- Add headless Avalonia coverage only for behavior that requires a real control tree or binding.
- Include error, cancellation, offline, and recovery scenarios in each feature's acceptance evidence where applicable.

### Phase 3 — As scale and product behavior emerge

- Add scheduled synthetic 250k+ metadata fixtures, representative directory trees, performance reporting, and memory/throughput budgets.
- Add database-upgrade, backup/restore, and supported-OS/native rendering checks to release validation.
- Consider targeted mutation testing for especially consequential domain or catalog rules after a stable, fast baseline exists.
- Review suite duration, flaky tests, coverage trends, and dependency/license changes regularly; split test projects only when a measured operational need appears.

---

## Research Sources

- [.NET unit-testing best practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
- [xUnit.net](https://xunit.net/)
- [Microsoft.Testing.Platform overview](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-intro)
- [Testing with `dotnet test`](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test)
- [FluentAssertions README](https://github.com/fluentassertions/fluentassertions) and [current license](https://raw.githubusercontent.com/fluentassertions/fluentassertions/main/LICENSE)
- [NSubstitute](https://github.com/nsubstitute/NSubstitute), [Moq](https://github.com/devlooped/moq), and [FakeItEasy](https://github.com/FakeItEasy/FakeItEasy)
- [ArchUnitNET](https://github.com/TNG/ArchUnitNET), [release notes](https://github.com/TNG/ArchUnitNET/releases), and [xUnit v3 package](https://www.nuget.org/packages/TngTech.ArchUnitNET.xUnitV3)
- [NetArchTest](https://github.com/BenMorris/NetArchTest)
- [Avalonia.Headless.XUnit](https://www.nuget.org/packages/Avalonia.Headless.XUnit)
- [.NET DI guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines) and [`ValidateOnBuild`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.serviceprovideroptions.validateonbuild)
- [SQLite in-memory databases](https://www.sqlite.org/inmemorydb.html) and [Microsoft.Data.Sqlite in-memory databases](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/in-memory-databases)
- [Dapper](https://github.com/DapperLib/Dapper)
- [GitHub Actions: build and test .NET](https://docs.github.com/en/actions/tutorials/build-and-test-code/net) and [workflow permissions](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#permissions)

**End of REA-005**
