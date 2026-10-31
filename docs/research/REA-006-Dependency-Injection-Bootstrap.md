# REA-006: Dependency Injection, Bootstrap, and Composition Root

**Project:** 3D Print Vault
**Review date:** 2026-10-31
**Status:** Research complete
**Scope:** Dependency injection, Avalonia startup, application composition, and future module extension
**Governing standard:** [DEVELOPMENT_STANDARD.md](../../DEVELOPMENT_STANDARD.md)

---

## 1. Executive Summary

3D Print Vault should have **one composition root in `DPV.App`**. That process-level root should build and own one Microsoft Generic Host, load and validate configuration, establish logging before startup work, explicitly register application/UI/infrastructure modules, validate the service graph, run bounded database initialization, start Avalonia, and coordinate graceful shutdown.

The existing documents assign startup, DI, configuration, logging bootstrap, and application lifetime to `DPV.App`, but the current projects do not implement that architecture:

- `DPV.App` is currently a console executable whose `Program.cs` prints “Hello, World!”
- `DPV.UI` is currently a `WinExe`; its `Program.cs` owns Avalonia startup.
- `DPV.UI.App.OnFrameworkInitializationCompleted` directly constructs `MainWindow`.
- `DPV.App` already references `DPV.UI`, while `DPV.UI` also references `DPV.Infrastructure`.

The recommended path is to **make `DPV.App` the desktop executable and composition root, and make `DPV.UI` a presentation library**. `DPV.App` should drive Avalonia using its documented application-factory entry point and pass a DI-created `App` into the framework; `DPV.UI` should not depend on `DPV.App`. This aligns runtime ownership with the documented project responsibilities and avoids a second bootstrap project. It is an architectural decision requiring an approved update to project definitions and dependency documentation before implementation.

### Recommended decisions

| Area | Recommendation |
|---|---|
| Composition location | One root in `DPV.App`; no separate bootstrap project for the MVP. |
| Desktop host | `Microsoft.Extensions.Hosting` / `HostApplicationBuilder` for DI, configuration, logging, lifecycle, and future background jobs; let Avalonia, not `IHost.Run`, own the desktop message loop. |
| Registration | Explicit, named `IServiceCollection` extension methods owned by projects/features; one central, visible orchestration point in `DPV.App`; no assembly scanning for MVP. |
| Service provider | Build exactly once per process. Do not retain it in a static global or let feature services resolve arbitrary dependencies from it. |
| Lifetimes | Singleton only for app-wide, thread-safe state/resources; transient for short-lived stateless work and page ViewModels; scoped services reserved for finite operations such as a scan/import job. |
| ViewModels and Views | Constructor injection; typed factories for navigation and view construction. Avoid resolving arbitrary types by reflection or using a global service locator. |
| Startup | Initialize bootstrap logging, build/validate the host, validate options/paths, perform only required database creation/migrations, then create the UI. Do not scan the full library, render models, or generate thumbnails before showing the app. |
| Validation | Explicit `ValidateOnBuild` and `ValidateScopes` in tests and startup; resolve important roots and validate options separately because container validation does not exercise every runtime factory/configuration path. |
| Plugins | Design for explicit compile-time modules now. Defer third-party runtime plugin loading, `AssemblyLoadContext`, isolation, and unload support until there is a concrete plugin contract and threat model. |

### Final verdict

**APPROVE WITH CHANGES.** The documented ownership of `DPV.App` as host and composition root is the right direction. Before DI implementation, resolve the executable/startup ownership mismatch, align `DPV.CONFIGURATION-001` lifetime guidance with actual service semantics, and add architecture rules for the composition root and registration boundaries. This report is research only; it changes no implementation.

### Evidence limits

- Official Avalonia templates demonstrate starter conventions, not a universal enterprise architecture. The examples below are evidence of documented patterns, not proof of industry-wide prevalence.
- A public production-scale Avalonia application is included as one illustrative sample only; its architecture is not treated as an enterprise standard.
- Microsoft DI supports common application needs but is intentionally not a plugin/container-management framework. Plugin requirements would need their own REA and security review.
- Versions, template defaults, package compatibility, and framework guidance evolve. Recheck them when the implementation is approved.

---

## 2. Composition Root Analysis

### What a composition root is

A **composition root** is the single application boundary where concrete implementations are selected, registered, assembled, and connected to the process lifetime. It is normally the executable's startup path. It is allowed to know the concrete types of the application's modules because its purpose is to wire them together.

It is not a service locator for the rest of the application. Outside startup and narrowly defined framework factories, application services and ViewModels should receive their dependencies through constructors or typed factories.

### Why it matters

A single root:

- Makes the chosen implementation graph visible and reviewable.
- Centralizes configuration, logging, startup order, and resource disposal.
- Prevents different projects from silently creating independent service providers.
- Gives tests one production registration path to validate.
- Gives architecture tests a precise target: the executable composes modules; Domain does not know about the container.
- Gives AI-generated changes one predictable place to add module registration, while leaving service ownership in the project that owns the implementation.

When registrations are scattered through Views, ViewModels, static helpers, constructors, and feature code, common results include duplicated providers/singletons, order-dependent registration, hidden overrides, incompatible lifetimes, tests that use a different graph from production, and no reliable way to know when resources should stop and be disposed.

### Where composition should occur

| Choice | Advantages | Risks and DPV fit |
|---|---|---|
| **`DPV.App`** | Matches the current project-ownership documents; natural place for process entry, host, configuration, logging, and shutdown; can explicitly compose UI and Infrastructure. | Requires resolving the current fact that UI, not App, owns Avalonia's executable entry point. **Recommended after a governed executable-role adjustment.** |
| **`DPV.UI`** | Avalonia templates put `Program` and `Application` startup in the UI executable, making framework lifecycle wiring direct. | Contradicts the approved `DPV.App` ownership of host, DI, configuration, and application lifetime. Encourages UI to own non-UI composition and risks an unresolved split with `DPV.App`. |
| **Dedicated bootstrap project** | Can make sense for multiple executables/host variants that share a large composition graph. | Adds a project and another dependency edge without a current second host or product variant. Premature for this MVP. |
| **Distributed registration** | Each project can register its own implementations. | Reasonable only when project-owned registration extensions are called by one root. It is not a reason to distribute the root or build providers in each module. |

### Recommendation

Keep **one root in `DPV.App`** and one provider for the running process. Given the existing project directions, the cleanest long-term execution model is:

- `DPV.App` becomes the desktop process executable and composition root.
- `DPV.UI` becomes a presentation library containing Avalonia `Application`, windows, Views, ViewModels, navigation, and UI registration.
- `DPV.App` is the only project that orchestrates `DPV.UI` and `DPV.Infrastructure` registrations and starts Avalonia.
- `DPV.UI` does not reference `DPV.App`; `DPV.Domain` does not reference DI, configuration, logging, Avalonia, or infrastructure.

Avalonia exposes `AppBuilder.Configure<TApp>(Func<TApp>)`; its API documentation specifically notes the factory is useful for passing dependencies to the `Application` type ([Avalonia `AppBuilder` source](https://github.com/AvaloniaUI/Avalonia/blob/432480ae82f36f40caf8693e1dfb11d4040404ea/src/Avalonia.Controls/AppBuilder.cs)). That provides a documented bridge for the root to supply the DI-created Avalonia application without making the UI call back into `DPV.App`.

If maintaining `DPV.UI` as the executable is an immovable product decision, then the documents must instead explicitly move the root into UI and redefine `DPV.App` as application-service orchestration. Do **not** retain a nominal App root while actual composition remains in UI.

---

## 3. Avalonia Startup Analysis

### Framework startup and main-window creation

Avalonia's desktop startup convention is:

1. A process entry point creates/configures an `AppBuilder`.
2. The selected desktop lifetime starts the UI framework and message loop.
3. Avalonia initializes the `Application`.
4. `Application.OnFrameworkInitializationCompleted` receives the selected lifetime; desktop applications assign `IClassicDesktopStyleApplicationLifetime.MainWindow`.
5. On shutdown, application-lifetime events allow cleanup. Avalonia has different lifetime interfaces for desktop, single-view, and Android applications ([Application lifetimes](https://docs.avaloniaui.net/docs/fundamentals/application-lifetimes), [Main window](https://docs.avaloniaui.net/docs/fundamentals/main-window)).

The `AppBuilder` factory overload allows an outer composition root to create the `Application`. Window construction still needs to happen at a safe point in Avalonia's lifecycle; do not instantiate controls before the framework has initialized merely to build the host.

### Avalonia starter-template observations

The official [`Avalonia.Templates` repository](https://github.com/AvaloniaUI/Avalonia.Templates) currently offers:

- A desktop app template with `Program` calling `BuildAvaloniaApp().StartWithClassicDesktopLifetime(args)`.
- An MVVM template that creates `MainWindow` and `MainViewModel` directly in `App.OnFrameworkInitializationCompleted`.
- A default `ViewLocator` that maps ViewModel names to View names with reflection and `Activator.CreateInstance`.

These are deliberately simple getting-started examples. They do **not** show a modular Generic Host composition root or constructor injection through the default ViewLocator.

The official Avalonia DI article demonstrates building a `ServiceCollection` inside `OnFrameworkInitializationCompleted`, then resolving a ViewModel and assigning it as `DataContext` ([Implementing dependency injection](https://docs.avaloniaui.net/docs/app-development/dependency-injection)). This proves Microsoft DI can be connected to Avalonia, but it is a minimal sample, not the recommended DPV root: registration is tied to UI startup, it doesn't coordinate a Generic Host lifecycle, and container construction is embedded in the presentation layer.

Avalonia's current ViewLocator documentation explicitly calls the reflection-based template implementation convenient for getting started but not compile-time-safe, not Native AOT-compatible, and not suitable for injecting View dependencies. It presents explicit pattern matching, XAML data templates, DI-backed view mapping, and source generation as alternatives ([View locator](https://docs.avaloniaui.net/docs/data-templates/view-locator)).

### Production-scale sample observation

The public [Stability Matrix Avalonia App source](https://github.com/LykosAI/StabilityMatrix/blob/87dced77c1980e73c467dfac67009a94a9aa3d90/StabilityMatrix.Avalonia/App.axaml.cs) illustrates a larger application using an explicit service-registration method, many interface registrations, page-ViewModel configuration, and a central provider. It also exposes `App.Services` globally and constructs a `ServiceProvider` from UI `App` code. This is useful as a real-world example of scale pressures and registration complexity, **not** a pattern DPV should copy wholesale: the static service locator and UI-owned provider weaken the single-root and constructor-injection goals.

No evidence reviewed establishes one "enterprise Avalonia" architecture used universally. Mature patterns are better identified by properties than by a label:

- Startup and shutdown have a clear owner.
- The provider has one owner and a bounded lifetime.
- Views and ViewModels do not retrieve arbitrary services globally.
- Feature registrations are grouped and explicit.
- Background tasks and UI-thread work have defined lifetimes and cancellation.
- Presentation and domain behavior remain independently testable.

### Common anti-patterns

- Resolving ViewModels from `Application.Current` or `App.Services` from arbitrary code.
- Constructing `ServiceCollection` and a provider independently in the Avalonia callback when another host/provider already exists.
- Directly `new`-ing dependency-heavy ViewModels or windows throughout code.
- Reflection/name-convention ViewLocator failures that become runtime-only blank/error views.
- Putting configuration, logging, persistence, and feature registration decisions in UI code-behind.
- Starting long library scans, thumbnail generation, rendering, or all background workers before the shell is visible.
- Blocking the UI thread on asynchronous host/database initialization.
- Treating `IHost.Run()` as a replacement for Avalonia's message loop without coordinating the two lifetimes.

**Recommendation:** retain Avalonia's `Application` and lifetime callbacks as a thin presentation adapter. Let `DPV.App` own the host/provider. Supply the `Application` through Avalonia's factory overload, and create UI roots through explicit DI registrations or typed factories after framework initialization.

---

## 4. Microsoft DI Analysis

### Why use the Microsoft container

`Microsoft.Extensions.DependencyInjection` is the correct default for DPV because:

- It is the standard DI abstraction integrated with the .NET options, configuration, logging, and hosting libraries.
- It is lightweight and sufficient for constructor injection, the three standard lifetimes, open-generic services, and factory registrations.
- Microsoft recommends using the built-in container for most applications unless they need a specific unsupported feature ([DI overview](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview), [DI guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines)).
- It supports DPV's current requirement to register services through `IServiceCollection` without introducing a second container or vendor-specific programming model.
- Explicit registrations are straightforward for reviewers and code-generation agents to follow.

### Limitations and appropriate response

The built-in container is intentionally simple. Microsoft identifies features it does not provide as first-class container behavior, including child containers, property injection, convention-based registration, custom lifetime management, and built-in `Func<T>` lazy resolution. It does not provide plugin isolation or safe dynamic unloading.

Do not adopt Autofac/DryIoc or add a container abstraction merely to address hypothetical future modules. Use explicit factory interfaces when a runtime factory is needed, and evaluate a replacement only after a concrete requirement cannot be met cleanly.

### `ServiceProviderOptions`

`ServiceProviderOptions` exposes two important checks:

- **`ValidateOnBuild`**: validates service call sites at provider construction so many constructor-graph failures are found early.
- **`ValidateScopes`**: checks invalid scope usage, especially resolving scoped services from the root provider and scoped dependencies captured by longer-lived services.

Host defaults enable both validations in Development according to the Generic Host documentation ([Generic Host builder defaults](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host), [`ServiceProviderOptions`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.serviceprovideroptions)). DPV should set the intended behavior explicitly rather than rely on build configuration defaults.

**Validation is not exhaustive.** `ValidateOnBuild` does not prove that runtime factory delegates, all configuration values, database files, native components, or dynamically loaded plugins work. Open generic registrations and services that are only constructed along unexercised runtime paths need explicit tests/resolution. Option values need their own validation; `ValidateScopes` is not a general lifetime-correctness or thread-safety proof.

### Common mistakes to prohibit

- Calling `BuildServiceProvider` during registration and accidentally creating a second provider/singletons.
- Injecting `IServiceProvider` into ordinary services or using static service access.
- Capturing scoped/transient dependencies in singletons.
- Registering mutable application state as a singleton without thread-safety and reset semantics.
- Registering disposable transient objects that are resolved from the root and retained until application shutdown.
- Async service factories or `.Result`/`.Wait()` during startup/registration.
- Ambiguous overloaded constructors where several DI-resolvable constructor signatures exist.
- Missing registration tests because successful compilation is mistaken for a complete graph.
- Keeping all registrations in a third-party assembly scan that hides duplicate/overriding registrations.

### Recommended validation

- Enable build and scope validation in the production host configuration, not only local Development.
- In tests, build the exact root registration graph with both validations enabled.
- Separately resolve the Avalonia `Application`, main window factory, navigation roots, hosted services, and key database services using isolated test configuration.
- Validate options at startup and in tests with good and bad inputs.
- Test async initialization explicitly; do not hide I/O in constructors or DI factories.

---

## 5. Registration Strategy Analysis

### Comparison

| Strategy | Maintainability | AI friendliness | Governance | Scale and error characteristics |
|---|---|---|---|---|
| **One large registration file** | Very easy initially; becomes a long mixed-responsibility file with merge conflicts and service ownership knowledge concentrated in App. | One place is easy to discover, but agents frequently make unrelated changes and can miss the owner of a type. | High visibility at first; poor project ownership as it grows. | Fine for a tiny program. As modules grow, omission/duplicate/lifetime errors and review noise rise. |
| **Separate registration files in App** | Keeps concerns separated but App still knows implementation details for every feature. | Predictable if filenames and order are documented. | Strong central root, but weaker module autonomy. | Reasonable for a small closed application; can become a central dependency bottleneck. |
| **Project/feature-owned module extensions** | Each owner registers its own types and implementation dependencies; root remains a short list of modules. | Predictable convention; agents change a module method and one root call when adding a module. | Strong when calls at the root are explicit and architecture tests constrain ownership. | Scales well across current projects. Potential package reference to DI abstractions in module projects is an intentional tradeoff. |
| **Assembly scanning / convention registration** | Less manual registration but less explicit behavior; reflection/trimming and registration-order questions. | Agents may add types that are unintentionally discovered or assume unsupported conventions. | Weak unless scans have tight explicit boundaries and diagnostics. | Useful in large plugin ecosystems with conventions; increases hidden-registration and duplicate-registration risks for DPV's current size. |
| **Hybrid explicit modules + central root** | Project/feature methods own local registrations; one root selects modules and order. | One discoverable contract plus local ownership. | **Best fit:** root is auditable; modules are cohesive; forbidden projects can be blocked. | Scales without scanning. Requires tests to ensure each expected module is called and there is only one provider. |

### Recommendation

Use the **hybrid explicit approach**:

1. `DPV.App` has the one root method that chooses the application modules and builds the host.
2. `DPV.Infrastructure` exposes one clear infrastructure registration extension; internally, persistence/import/search/viewer registrations may be split into explicit feature methods as their implementations arrive.
3. `DPV.UI` exposes one presentation registration extension for UI-owned ViewModels, navigation and window/view factories.
4. `DPV.App` registers application orchestration and startup services.
5. `DPV.Domain` registers nothing and references no DI package.
6. Each module registration method only appends registrations. It does not build a provider, load production files, initialize SQLite, scan assemblies, start work, or mutate global state.
7. Root calls are listed explicitly and tested. A feature registration may add only dependencies it owns; cross-module composition remains visible in App.

There is a deliberate dependency decision: project-owned `IServiceCollection` extensions require DI abstractions in those projects. Approve that package boundary explicitly for UI/Infrastructure, or instead keep all registration declarations in `DPV.App` if maintaining registration code out of implementation projects is a stronger policy. **Do not accidentally add a new package reference while claiming Infrastructure remains package-free.** In either variant, only App builds the provider.

### Assembly scanning

**Do not use scanning for the MVP.** Registration counts are expected to remain reviewable, while explicit registrations give immediate compiler/refactoring feedback and are easier to audit. If plugin discovery later becomes approved, scan only an explicit, validated plugin boundary before provider construction; produce diagnostics for every discovered/ignored/failed module and define ordering and duplicates.

---

## 6. Service Lifetime Analysis

### Lifetime rules

| Lifetime | Meaning in DPV | Appropriate use | Risks |
|---|---|---|---|
| **Singleton** | One instance in the root provider for the process. | Truly application-wide resources/state, immutable option monitor/factory, a shared navigation coordinator where intended, thread-safe logging/config infrastructure. | Mutable shared state, captured scoped dependencies, large object graphs retained for the whole process, UI-thread-affinity violations, difficult test isolation. |
| **Scoped** | One instance per explicitly created scope; scopes are not automatically created by Avalonia or by desktop navigation. | A scan/import operation with unit-of-work state, per-job resources, a finite batch requiring deterministic disposal. | No implicit request scope in a desktop app; resolving from root defeats intent; long-lived window scopes can retain data and resources indefinitely. |
| **Transient** | A new instance each resolution. | Stateless cheap services, commands/short-lived ViewModels, operation objects and typed factories. | Disposable transients resolved from root can be retained; state is lost if caller expects one shared instance; factories can accidentally resolve many duplicates. |

Microsoft notes that built-in DI scopes are explicit, not hierarchical, and that resolving scoped/disposable services from the root can extend their lifetime to application shutdown. It also emphasizes that the container's thread safety does not make resolved singleton instances thread-safe ([DI guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines)).

### Recommended lifetimes by category

| Service | Default recommendation | Important qualification |
|---|---|---|
| Configuration root | Host-owned singleton | Bind scenario-specific option types rather than inject raw global configuration everywhere. |
| `IOptions<T>` | Framework-managed singleton value | Good for stable configuration. |
| `IOptionsMonitor<T>` | Framework-managed singleton monitor | Use only where live reload is meaningful and services can react safely. |
| `IOptionsSnapshot<T>` | Scoped | Usually unnecessary for a single-window desktop root; usable inside explicit operation scopes but not injectable into singletons. |
| `ILogger<T>` / logger provider | Framework-managed, process-wide infrastructure | Inject typed `ILogger<T>`; don't register a separate logger per feature. Initialize Serilog before host construction if startup failures must be recorded. |
| SQLite connection factory | Singleton, stateless/thread-safe | It creates a new connection per operation; do not register an open connection, transaction, or mutable repository session as a singleton. |
| Repositories | Transient or operation-scoped | Choose scoped when they share an explicit per-operation connection/transaction; do not keep a connection open for app lifetime. |
| Search service | Singleton only if stateless/thread-safe, otherwise transient or operation-scoped | A large mutable cache is not made safe merely by singleton registration; define cache ownership/eviction separately. |
| Import coordinator | Singleton only for orchestration/queue state with explicit synchronization | It creates a scope per scan/import job and delegates job work. |
| Import job/service | Scoped to one job, or transient when it owns no scoped resources | Define cancellation, progress, and disposal at job boundary. Do not make a whole import operation an app singleton. |
| Navigation coordinator | Usually singleton per application/window | Owns current navigation state and UI dispatch; avoid static global access and avoid retaining all page ViewModels unnecessarily. |
| Page/ViewModels | Transient or typed factory per navigation | Shell/window ViewModels may be window-lifetime; expensive page state needs explicit caching policy, not an accidental singleton. |
| Views/windows | Transient factory or registered concrete root resolved at framework startup | Resolve only when Avalonia is initialized; don't use reflection ViewLocator as a DI factory. |
| Hosted background worker | Host-managed singleton worker | If it needs scoped work, create/dispose an explicit scope per job/iteration. Do not inject a scoped repository directly into the hosted service. |

### Does a desktop app need scopes?

**No scope is required just because the application uses DI.** A single process and a single main window can use a root provider and transient/singleton services.

Scopes become useful when a finite operation has resources that should be disposed together—for example, an import run with a transaction, cancellation token, telemetry context, and repositories—or when a worker performs repeated units of work. Explicitly create and dispose an async scope around that job.

Avoid a scope per navigation by default. Page navigation is not inherently a unit of work, and a scope retained for a window's entire lifetime can retain SQLite connections, large catalogs, and disposable transients. Add window scopes only if a concrete owned-resource lifetime requires them and the shutdown behavior is tested.

### Configuration-standard alignment

[DPV-CONFIGURATION-001.md](../architecture/DPV-CONFIGURATION-001.md) currently declares configuration, logging, and “Database Factory” as singleton, and import/detection/search services as transient. Keep the intent but clarify meanings:

- “Configuration singleton” means host-managed configuration/options infrastructure, not a mutable settings object handed to every service.
- “Logging singleton” means one logging pipeline with injected category loggers.
- “Database Factory singleton” means a stateless factory that creates short-lived connections, not a singleton database connection.
- Search can be transient by default, but may be singleton only if it is stateless/thread-safe and its caches/resource ownership are intentional.
- Import orchestration and import-job execution have different lifetimes; a singleton coordinator can create a scoped/transient job instance.

---

## 7. ViewModel Resolution Analysis

### ViewModels

ViewModels should be resolved through constructor injection or a typed factory when they have dependencies. This supports ordinary unit tests and makes dependencies visible. A `ViewModel` should not query `Application.Current`, a static `App.Services`, or a global container.

Register ViewModels with lifetimes matching UX:

- Shell/window root: one per application/window if it owns persistent shell state.
- Page ViewModels: transient/factory-created on navigation unless a documented state-retention policy says otherwise.
- Dialog ViewModels: transient per dialog.
- Background-operation ViewModels: preferably receive a job/status interface rather than own the actual worker/service scope.

### Views

Views with no injected dependencies may remain XAML-created or use explicit data templates. Constructor-injected Views should use registered concrete view types or explicit typed view factories. There should be few dependencies in Views; most operational dependencies belong in ViewModels/services.

The default Avalonia ViewLocator uses reflection and parameterless `Activator.CreateInstance`, so it cannot inject dependencies and is not compile-time-safe. For DPV's finite set of views, prefer explicit XAML data templates or an explicit typed mapping/factory. The latter is more verbose but keeps failures visible and avoids relying on naming conventions at runtime.

### Navigation

Use a presentation-owned navigation coordinator that:

- Accepts typed navigation intent/route data, not arbitrary strings as a service-resolution key.
- Uses explicit typed factories to create page ViewModels.
- Publishes the current page/ViewModel to the shell.
- Dispatches UI changes through Avalonia's dispatcher where required.
- Defines whether revisiting a page creates a new ViewModel, restores cached state, or rejects duplicate navigation.
- Does not expose `IServiceProvider` as a general resolver.

Navigation should not become an all-purpose service locator or an ambient global. Window/dialog ownership should use the current active top-level/window where relevant; Avalonia warns that static access to a main window can be risky for multi-window UX ([Main window](https://docs.avaloniaui.net/docs/fundamentals/main-window)).

### Testing implications

Constructor-injected ViewModels can be tested without booting Avalonia. Use substitutes/fakes for boundary services, assert visible state/commands and error behavior, and avoid asserting exact navigation implementation calls. Integration tests should cover only important real binding/template/dispatcher connections.

---

## 8. Bootstrap Architecture Analysis

### Recommended startup sequence

1. **Enter through `DPV.App`.** Parse command-line/environment selection and establish a stable application content/data path. Do not assume the current working directory is the install or user-data directory; Generic Host's default content root is the process current directory.
2. **Initialize bootstrap logging.** Create a minimal early logger so configuration, host construction, migration, or Avalonia startup failures are not lost. Then configure the final Serilog pipeline from approved settings and route `ILogger<T>` through it. Serilog's hosting integration documents both early initialization and host integration ([Serilog.Extensions.Hosting](https://github.com/serilog/serilog-extensions-hosting)).
3. **Create the host builder.** Use the modern `Host.CreateApplicationBuilder` pattern for a new .NET 10 application. Configure configuration providers, options validation, logging, and `IServiceCollection`. Host builder defaults include appsettings, environment variables, command line, and standard log providers; reconcile those defaults with the project's configuration and Serilog policy ([Generic Host](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host)).
4. **Register modules explicitly.** App services, UI/presentation, and infrastructure each contribute registrations through the approved module pattern. Root module selection/order remains visible in App.
5. **Build once and validate.** Enable `ValidateOnBuild` and `ValidateScopes`; resolve startup roots and validate configuration explicitly. Do not create a second provider.
6. **Perform bounded pre-UI initialization.** Validate essential directories/permissions and create or migrate the SQLite catalog with the intended WAL/foreign-key policy. Report actionable failure and keep migrations transactional/recoverable. Do not scan the user's entire library during this step.
7. **Start required host services deliberately.** Start only services whose failure means the shell cannot function. Long-running scans/imports/thumbnail tasks should begin after the UI is responsive and have cancellation/progress.
8. **Start Avalonia.** Construct the `Application` from the provider through the documented application-factory path, let Avalonia initialize, and create the main window in the proper desktop lifetime callback.
9. **Shutdown gracefully.** Connect Avalonia's exit/close path to host cancellation and awaited `StopAsync`, then dispose the host/provider and flush Serilog. Avoid synchronous waits on async work on the UI thread; log shutdown failures explicitly.

The Generic Host is valuable here for resource/startup/shutdown management; it does **not** replace Avalonia's UI loop. Microsoft documents that desktop apps control their own lifetime and must integrate host lifetime with desktop events ([DI overview](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview)). Its WPF example provides a useful analogous pattern: build/start the host during desktop startup, resolve the root window, then stop/dispose on application exit ([Generic Host in WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/app-development/how-to-use-host-builder)). Avalonia integration must use Avalonia's own lifecycle rather than copying WPF event names.

### Configuration

- Follow the approved `appsettings.json` and `appsettings.Development.json` naming, but make the content root deliberate.
- The installed app's writable catalog, logs, caches, and thumbnails belong under the user's application-data directory as the project specifies—not beside a protected installation executable and not relative to an unpredictable current directory.
- Bind related sections to strongly typed options such as Database, Scanning, Viewer, and Logging. Validate required paths, ranges, and incompatible values at startup. Microsoft's options pattern is intended to isolate settings by scenario and support validation ([Options pattern](https://learn.microsoft.com/en-us/dotnet/core/extensions/options)).
- Decide which file is required, optional, and reloadable. Do not silently fall back to a development database path or unvalidated defaults for required production state.
- Do not place mutable user settings/library state in the DI container as arbitrary singleton data; persist user configuration by an explicitly owned mechanism.

### Logging

- Start an early bootstrap logger before host building, then install the Serilog host integration for `ILogger<T>` and framework messages.
- Keep Serilog configuration and sink choices in the composition/bootstrap owner, not in Domain or individual features.
- Use structured event properties for startup, migrations, import, search, viewer, and errors; avoid logging sensitive file contents.
- Define flush/close at process shutdown.

### Database bootstrap

- Register a singleton connection factory; create/dispose SQLite connections at the operation boundary.
- Apply required SQLite connection settings consistently (foreign keys; approved WAL behavior).
- Run schema version check/migrations once, with bounded startup time and explicit failure reporting. Use an operation-scoped connection/transaction during migration.
- Do not synchronously enumerate 250,000+ models, build FTS indexes over all source files, generate previews, or render Helix content before the main UI is visible.
- Once the UI is responsive, schedule background catalog/import work with cancellation, user-visible progress, and controlled concurrency.

### `Program.cs` responsibilities

`Program.cs` is the thin process script: set early process-level behavior, create/configure the host and Avalonia builder, call the explicit composition/startup path, and coordinate exit code and cleanup. It should not contain feature business rules, SQL, file scanning, or a long hand-maintained service list. Keep the actual module list in one named composition method called by startup.

### Startup anti-patterns

- Heavy/async work in constructors or DI factory delegates.
- Calling `host.Run()` and Avalonia desktop lifetime independently, creating competing process loops.
- Sync-over-async on the dispatcher.
- Database scans, full library discovery, or 3D rendering before showing the main window.
- Ignoring configuration validation or swallowing startup exceptions.
- Treating `appsettings.json` in the current directory as a guaranteed production path.
- Starting every `IHostedService` on application startup without checking whether it blocks UI readiness or requires a valid database.

---

## 9. Testing Strategy

The [REA-005 testing foundation](REA-005-Testing-Foundation.md) already recommends testing the production registration path with `ValidateOnBuild`, `ValidateScopes`, real SQLite boundaries, and a small set of UI headless tests. REA-006 specializes that policy for the composition root.

### Registration and composition tests

1. **Root graph test:** invoke the exact production module-registration path with test configuration and test file locations; build one provider with both validation flags enabled.
2. **Root resolution smoke tests:** resolve `DPV.UI.App`, main-window factory, shell ViewModel, navigation root, SQLite factory/repository, and any mandatory hosted worker. Do not resolve every conceivable transient and accidentally test implementation details; cover all startup roots and open-generic/custom factories.
3. **Options tests:** validate both accepted configuration and representative invalid/missing values. Verify that tests cannot accidentally use `/data/dev.db`, a real user's `%AppData%`, or the production library root.
4. **Lifetime tests:** test only meaningful ownership contracts—singleton identity where required, transient ViewModel freshness, scoped operation isolation/disposal, and connection-per-operation. Avoid a brittle snapshot of every registration descriptor.
5. **Startup integration test:** use a temporary file-backed SQLite database, real production registration, and test logger/configuration to exercise schema initialization, migration, and startup failure behavior.
6. **Avalonia smoke test:** initialize a headless application only when needed; verify the root application/window/factory and a small representative binding/navigation path. Keep most ViewModel tests framework-independent.
7. **Shutdown test:** verify cancellation and disposal of a representative background operation and provider-owned resources; cover failure reporting for graceful shutdown.

### Architecture enforcement

Current tests in [DependencyRuleTests.cs](../../tests/DPV.Tests/Architecture/Dependencies/DependencyRuleTests.cs) prohibit production references to tests, restrict Infrastructure to Domain, and detect cycles. [DomainPurityTests.cs](../../tests/DPV.Tests/Architecture/Boundaries/DomainPurityTests.cs) checks Domain project and type dependency purity.

These checks do **not yet** enforce that DPV.App is the only composition root, that DI package references/registration methods belong to approved projects, that UI avoids provider resolution, or that App/UI have the chosen executable/library roles. Add architecture rules and project-file checks for those approved invariants before or with DI implementation.

Recommended enforceable rules:

- Domain has no project/package references to Avalonia, DI, Hosting, Logging, Configuration, SQLite, or Infrastructure.
- Only `DPV.App` creates/builds the host/provider and starts application lifetime.
- `BuildServiceProvider` does not occur inside module registration methods or production feature code.
- UI/Infrastructure expose their own explicit registration methods only if the architecture approves DI abstractions as a package dependency.
- No production type outside the composition boundary injects `IServiceProvider` or accesses static `App.Services`.
- Infrastructure remains inward-facing; UI may use approved UI/application abstractions and not own database/file scanning logic.
- Only the startup root resolves the Avalonia `Application` from the root provider.

### Common DI regressions and detection

| Failure | Detection |
|---|---|
| Missing service registration | `ValidateOnBuild`, plus resolving required application roots. |
| Singleton captures scoped service | `ValidateScopes`; scoped job tests. |
| Duplicate providers/singletons | Architecture search/rule for provider construction; root graph test and lifecycle identity checks. |
| Wrong lifetime / stale page state | Transient/factory behavior tests and navigation integration tests. |
| Registration order silently overrides service | Explicit module list, tests for intended multi-registration/enumeration behavior, diagnostics for duplicates. |
| Factory throws only on first resolution | Explicit root/factory resolution tests; do not assume `ValidateOnBuild` executes all factories. |
| Invalid/missing settings | Options validation and negative configuration tests. |
| Database connection lifetime leak | File-backed multi-connection tests, disposal/handle checks, and shutdown integration tests. |
| UI hangs during boot | Startup smoke/telemetry and cancellation tests; performance budget on essential initialization. |
| Architecture drift from AI-generated code | Required architecture tests in every PR and code review of project/package reference changes. |

---

## 10. Plugin Readiness Strategy

### What to do now

- Keep features as explicit modules in the current projects, matching the documented rule that module extraction requires complexity, clear boundaries, reduced maintenance burden, architecture review, and REA.
- Keep `DPV.Domain` pure. Cross-cutting plugin contracts, if ever approved, should live in an intentionally designed application/extension contract rather than pulling UI, SQLite, or DI into Domain.
- Give each future module an explicit registration entry point and contract now; the central root can call these without assembly scanning.
- Keep storage, rendering, import, and search implementations replaceable behind owned interfaces only where a genuine substitution or feature seam exists.

### What to defer

Do **not** create plugin manifests, arbitrary assembly discovery, runtime package loading, dependency conflict resolution, hot unload, sandbox claims, or a generalized plugin service provider in the MVP. A module method is not itself a plugin system.

### Future extension design

If third-party plugins become an approved feature:

1. Define supported capabilities, versioned contracts, compatibility rules, trust model, and user consent before implementation.
2. Discover only from an explicit plugin location and validate metadata/signature/compatibility before loading.
3. Ask each accepted plugin to register through a narrow, documented module contract before the root provider is built; reject late arbitrary mutation of the running provider.
4. Separate shared contract assemblies from plugin-private dependencies and define service lifetime/disposal per plugin.
5. Use `AssemblyLoadContext` only for dependency versioning/unloading needs it can actually solve. It provides managed dependency-loading isolation and an unload mechanism, **not a security sandbox**; untrusted plugins require process isolation and a restricted IPC contract.
6. Test plugin compatibility, failed loading, partial registration, shutdown/disposal, version conflicts, and upgrade/uninstall.

Microsoft's `AssemblyLoadContext` guidance describes per-context assembly resolution/version behavior and unload grouping; it does not promise process-level security isolation ([About `AssemblyLoadContext`](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext)).

---

## 11. AI Development Considerations

No research reviewed proves one DI container inherently produces fewer AI-generated errors. **Explicitness and executable rules** reduce the room for an agent to invent a registration convention or silently bypass a boundary.

### AI-friendly design

- Use one canonical root and a small, stable set of registration names and ownership rules.
- Require any new service to identify its owning project, interface (if warranted), lifetime, disposal/thread-safety assumptions, module registration method, and tests.
- Keep the module call list in one visible composition method. Avoid automatic scanning and undocumented naming conventions.
- Maintain a small registration example and a decision table for lifetimes; state why SQLite connections are created per operation and why job scopes are explicit.
- Include the exact validation command and report build/test output, not merely “registered service.”
- Have agents add a resolution test when a new required root is introduced and an architecture test when a boundary changes.
- Keep options strongly typed and validate ranges/required paths so invalid generated configuration fails early.
- Require generated service classes to use constructor injection and avoid injecting `IServiceProvider`, static `App.Services`, or configuration globals.
- Ask for a behavior-level test for shutdown, cancellation, offline, or startup failure when a service introduces long-running or I/O behavior.

### Prohibited generated patterns

- Building another service provider inside a registration helper.
- Adding a new container or bootstrap project without an approved REA/architecture decision.
- Registering every concrete type by assembly scan.
- Making all services singletons “for performance.”
- Making a SQLite connection, transaction, or stateful import job an application singleton.
- Adding a second `Program.Main`, competing Avalonia lifetimes, or calling Generic Host `Run` alongside Avalonia's loop.
- Resolving arbitrary ViewModels/services by a string, reflection convention, or global provider.
- Hiding failed registration or configuration behind `GetService` null fallbacks.
- Doing database/filesystem work in constructors or registration delegates.
- Adding a project/package reference to Domain to access DI, configuration, Avalonia, or infrastructure.

### Documentation guidance

Keep the concise registration/lifetime conventions here and in the approved configuration/architecture specifications. Avoid duplicating a second contradictory list in AI instructions; the AI project standard should link to the approved source of truth. If implementation reveals a reusable failure mode, update the governing development/agent guidance through the project workflow.

---

## 12. Risk Assessment

| Rank | Severity | Risk | Mitigation | Detection | Prevention |
|---:|---|---|---|---|---|
| 1 | **High** | Composition split between `DPV.App` and `DPV.UI` leaves two startup owners and unclear lifecycle. | Select one process root and align project roles before DI work. | Architecture test checks executable ownership, references, and single host construction. | Update governed project/dependency documents before implementation; prohibit UI composition. |
| 2 | **High** | Duplicate service providers create duplicate singletons, log pipelines, and undisposed resources. | Build one provider through one host. | Architecture checks for `BuildServiceProvider`; composition integration test. | Only App may build host/provider; module methods only add registrations. |
| 3 | **High** | Captive scoped dependency, leaked disposable transient, or singleton state race causes data corruption/memory growth. | Use explicit job scopes and correct lifetimes; make shared state thread-safe. | ValidateScopes; scope/disposal/concurrency tests and soak diagnostics. | Document lifetime ownership and review state/thread-safety at registration. |
| 4 | **High** | Startup performs full library scan/database indexing or blocks UI while opening thousands of files. | Do bounded schema/path preflight, show shell early, then run background operations. | Startup duration telemetry and large-fixture startup performance test. | Keep constructors/factories I/O-free; schedule explicit cancellable jobs. |
| 5 | **High** | Configuration content root or database path resolves differently from development, causing data loss, wrong catalog, or access failure. | Resolve stable per-user paths and validate configuration; test with temporary directories. | Installed/startup path tests on supported OSes. | Never depend on current working directory or development database defaults in production. |
| 6 | **High** | Avalonia and Generic Host lifetimes are not coordinated; shutdown hangs or background work is abandoned. | Bridge startup/exit explicitly and await stop/disposal without blocking dispatcher. | Shutdown/cancellation integration tests and process smoke test. | App owns both host and UI lifecycle; no independent `host.Run()` loop. |
| 7 | **Medium** | Registration spread or duplicate overrides make behavior order-dependent and hard to review. | Explicit module extensions and central module list. | Test expected module contributions and multi-registration semantics. | Avoid assembly scanning; make order and duplicate policy explicit. |
| 8 | **Medium** | Reflection ViewLocator or service locator causes runtime-only missing Views and untestable resolution. | Explicit typed view/VM factories or data templates; constructor injection. | Avalonia headless root/navigation tests and compile-time checks. | Prohibit static `App.Services`/arbitrary `IServiceProvider` access. |
| 9 | **Medium** | Plugin support is bolted on by scanning arbitrary assemblies, with dependency conflicts or unsafe code execution. | Defer plugins; later define contracts, trust model, lifecycle, and process boundary. | Plugin load/version/failure tests and security review. | Do not describe `AssemblyLoadContext` as a sandbox; no plugin framework before approval. |
| 10 | **Low** | Overbuilt DI abstraction/bootstrap layers increase AI confusion and maintenance without current benefit. | Use Generic Host and the built-in container directly behind small composition methods. | Review project count/dependency graph and onboarding friction. | No dedicated bootstrap project, custom container abstraction, or generic plugin bus without demonstrated need. |

---

## 13. Final Recommendations

1. **Composition root:** `DPV.App`, exactly one, with authority over registration, host/provider, configuration, logging setup, startup, and shutdown.
2. **Architecture decision to resolve first:** move the process executable/Avalonia entry ownership to `DPV.App` and make `DPV.UI` a presentation library, or formally amend the project standards to place the root in UI. The former best matches currently approved ownership.
3. **Host:** use `HostApplicationBuilder` and Microsoft Generic Host for application-wide DI/configuration/logging/lifecycle; Avalonia retains control of the UI event loop.
4. **Registration:** explicit, owner-local module extensions composed by one visible App root. No assembly scanning in the MVP; no module may build a provider or perform startup I/O.
5. **Package references:** explicitly approve DI abstractions in UI/Infrastructure if those projects own their `IServiceCollection` extension. Otherwise keep all registration declarations in App. In either case, Domain stays clean and App alone builds the provider.
6. **Lifetimes:** connection factory singleton, connection per operation; stateless/thread-safe shared services singleton only when justified; ViewModels transient or factory-created; import job scoped to an explicit operation where shared disposal/state is useful. A desktop app does not need request scopes by default.
7. **ViewModels/Views:** constructor injection and typed factories; no static service locator. Use explicit data templates/type mapping rather than reflection name-based resolution when a view must be created with injected dependencies.
8. **Startup:** bootstrap logging first; configure and validate host/options; run only essential database bootstrap; show the UI; perform full scans/imports/thumbnails asynchronously with cancellation and progress.
9. **Testing:** test the real module-registration path with `ValidateOnBuild`/`ValidateScopes`, resolve startup roots and factories, validate options, exercise file-backed SQLite startup, test shutdown/disposal, and smoke-test representative Avalonia bindings/headless UI.
10. **Architecture enforcement:** extend existing project-boundary tests to assert only App composes/constructs the host, Domain remains free of all framework dependencies, module registration ownership is explicit, and no static provider/service locator leaks into features.
11. **Plugins:** preserve explicit extension seams but defer dynamic plugin infrastructure. A future plugin feature requires its own REA, compatibility/security contract, and isolation decision.
12. **AI-assisted development:** teach one registration convention and lifetime table; require tests and architecture checks for every new registration; reject hidden scanning, service location, duplicate roots, and side-effectful constructors.

### Answer to the central question

`DPV.App` should compose and own the application. It explicitly invokes registration methods owned by the UI and infrastructure modules, validates one provider, and coordinates a single host with Avalonia's desktop lifetime. ViewModels and services use constructor injection; navigation uses typed factories; startup is ordered and bounded; DI correctness is proven by graph validation plus resolution and integration tests. Architecture rules ensure only App is the composition root. Future expansion adds explicit modules first and runtime plugins only after a separate approved design.

---

## 14. Implementation Strategy

The following is a **future recommendation only**. No implementation or project-file changes were made by this research.

### Gate 0 — Resolve architectural ownership

- Approve the executable-role decision: `DPV.App` desktop executable/composition root; `DPV.UI` presentation library.
- Reconcile `DPV-SOLUTION-001`, `DPV-PROJECT-DEFINITIONS-001`, `DPV-DEPENDENCY-MAP-001`, `DPV-CONFIGURATION-001`, and Testing Foundation with the selected project references, package ownership, and lifetime meanings.
- Define whether UI/Infrastructure may reference DI abstractions to own their module registration extensions.

### Commit A — Contracts

- Specify startup order, configuration source/requiredness, user-data paths, logging bootstrap/final pipeline, database initialization failure UX, and shutdown behavior.
- Specify the registration ownership rule and accepted service lifetime rationale.
- Define cancellation and progress contracts for post-startup scanning/import/thumbnail work.
- Define acceptance criteria for a healthy host graph, invalid configuration, DB migration failure, and graceful shutdown.

### Commit B — Definitions/resolvers

- Define typed settings/options, application startup orchestration boundaries, UI navigation contracts/factories, database initialization contract, and job-scope boundaries.
- Do not introduce a plugin abstraction or a general service-provider wrapper without an approved requirement.

### Commit C — Evaluation/configuration

- Update architecture/dependency/configuration/AI documentation and architecture test expectations.
- Select compatible, supported packages and versions for Generic Host, Serilog host integration, and Avalonia.
- Confirm production/development configuration behavior, log rotation/path permissions, and SQLite bootstrap policy.

### Commit D — Implementation

- Implement the thin App root and explicit module registrations.
- Keep registration pure and build exactly one host/provider.
- Keep Avalonia lifetime and `DPV.App` host lifecycle integrated without competing loops.

### Commit E — Integration

- Wire the window/ViewModel/navigation factory, configuration/options, logging pipeline, SQLite connection factory/migrations, and startup services through the approved root.
- Show the UI before expensive background library work.

### Commit F — Verification

- Run composition validation in Release and Development.
- Run architecture rules, root-resolution tests, invalid-options tests, temporary-file-backed database startup/migration tests, Avalonia headless smoke tests, cancellation and graceful-shutdown tests.
- Verify startup paths and application-data permissions in the supported Windows package/install layout; add future OS lanes when approved.

---

## Research Sources

### Project evidence

- [DEVELOPMENT_STANDARD.md](../../DEVELOPMENT_STANDARD.md)
- [DPV-DEPENDENCY-MAP-001.md](../architecture/DPV-DEPENDENCY-MAP-001.md)
- [DPV-CONFIGURATION-001.md](../architecture/DPV-CONFIGURATION-001.md)
- [DPV-PROJECT-DEFINITIONS-001.md](../architecture/DPV-PROJECT-DEFINITIONS-001.md)
- [REA-005-Testing-Foundation.md](REA-005-Testing-Foundation.md)
- [DPV.App `Program.cs`](../../src/DPV.App/Program.cs)
- [DPV.UI `Program.cs`](../../src/DPV.UI/Program.cs)
- [DPV.UI `App.axaml.cs`](../../src/DPV.UI/App.axaml.cs)
- [DPV.UI `MainWindow.axaml.cs`](../../src/DPV.UI/MainWindow.axaml.cs)
- [DependencyRuleTests.cs](../../tests/DPV.Tests/Architecture/Dependencies/DependencyRuleTests.cs)
- [DomainPurityTests.cs](../../tests/DPV.Tests/Architecture/Boundaries/DomainPurityTests.cs)

### External sources

- [Avalonia application lifetimes](https://docs.avaloniaui.net/docs/fundamentals/application-lifetimes)
- [Avalonia main window](https://docs.avaloniaui.net/docs/fundamentals/main-window)
- [Avalonia MVVM pattern](https://docs.avaloniaui.net/docs/fundamentals/the-mvvm-pattern)
- [Avalonia dependency injection guide](https://docs.avaloniaui.net/docs/app-development/dependency-injection)
- [Avalonia ViewLocator guide](https://docs.avaloniaui.net/docs/data-templates/view-locator)
- [Official Avalonia templates](https://github.com/AvaloniaUI/Avalonia.Templates)
- [Avalonia `AppBuilder` factory API source](https://github.com/AvaloniaUI/Avalonia/blob/432480ae82f36f40caf8693e1dfb11d4040404ea/src/Avalonia.Controls/AppBuilder.cs)
- [Stability Matrix Avalonia application source, pinned commit](https://github.com/LykosAI/StabilityMatrix/blob/87dced77c1980e73c467dfac67009a94a9aa3d90/StabilityMatrix.Avalonia/App.axaml.cs)
- [Microsoft DI overview](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview)
- [Microsoft DI guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines)
- [`ServiceProviderOptions`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.serviceprovideroptions)
- [.NET Generic Host](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host)
- [Options pattern and validation](https://learn.microsoft.com/en-us/dotnet/core/extensions/options)
- [Generic Host in WPF — desktop lifecycle analogy](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/app-development/how-to-use-host-builder)
- [Serilog.Extensions.Hosting](https://github.com/serilog/serilog-extensions-hosting)
- [About `AssemblyLoadContext`](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext)

**End of REA-006**
