# ADR-AVALONIA-001
## Application Lifecycle and Window Construction

**Status:** Accepted  
**Date:** 2026-11-04  
**Decision Type:** Framework Architecture Decision Record  
**Scope:** Avalonia Application Lifecycle, Window Construction, Dependency Injection, UI Initialization

---

# Context

During implementation of 3D Print Vault (DPV), a critical UI defect was discovered.

The application appeared to start successfully:

```text
Window Rendered

Navigation Visible

Content Visible
```

However:

```text
No Hover States

No Button Clicks

No Keyboard Focus

No TAB Navigation

No Interactive Behavior
```

Window chrome operations continued to function:

```text
Move

Resize

Minimize

Maximize
```

This created the appearance of a functioning application while rendering the entire client area effectively unusable.

Multiple maintenance investigations were required to determine the root cause.

---

# Symptoms

Observed behavior:

```text
Application launches.

Window appears.

Visual tree renders.

Sidebar visible.

Dashboard visible.
```

But:

```text
Buttons do not highlight.

Clicks do not execute.

Keyboard focus does not move.

TAB navigation does not function.

Pointer interaction appears absent.
```

---

# Initial Investigations

Potential causes considered:

```text
Thread Ownership

MTA vs STA

Input Routing

Visual Tree Overlays

Hit Testing

ViewModel Failures

Command Failures

Focus Management
```

Several theories were investigated and partially validated but did not fully explain the defect.

---

# Root Cause

The defect was ultimately traced to:

```text
Lifecycle Inversion
```

Specifically:

```csharp
public App(MainWindow mainWindow)
{
    _mainWindow = mainWindow;
}
```

This constructor dependency caused:

```text
Dependency Injection
    ↓
MainWindow constructed
    ↓
MainWindow.InitializeComponent()
    ↓
Visual Tree Created
    ↓
App Constructed
    ↓
App.Initialize()
    ↓
FluentTheme Loaded
```

instead of:

```text
App Constructed
    ↓
App.Initialize()
    ↓
Application Infrastructure Available
    ↓
Theme Loaded
    ↓
MainWindow Constructed
```

---

# Technical Explanation

When `MainWindow` was constructed:

```text
Application.Current == null
```

and essential framework services were not yet available.

This included:

```text
Global Styles

Theme Infrastructure

Application Style Host

Theme Variant Host
```

The window became detached from the normal Avalonia application lifecycle.

The resulting visual tree rendered but did not participate correctly in the framework's styling and interaction infrastructure.

---

# Incorrect Pattern

## Prohibited

```csharp
public App(MainWindow mainWindow)
{
    _mainWindow = mainWindow;
}
```

Root windows must not be constructor-injected into `App`.

---

# Approved Pattern

## Required

```csharp
public App(IServiceProvider serviceProvider)
{
    _serviceProvider = serviceProvider;
}
```

Resolve root windows only after framework initialization.

Example:

```csharp
public override void OnFrameworkInitializationCompleted()
{
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.MainWindow =
            _serviceProvider.GetRequiredService<MainWindow>();
    }

    base.OnFrameworkInitializationCompleted();
}
```

---

# Approved Lifecycle

The required lifecycle is:

```text
DI Resolves App
    ↓
App Constructed
    ↓
Application.Current Created
    ↓
App.Initialize()
    ↓
FluentTheme Loaded
    ↓
Framework Initialization Completed
    ↓
MainWindow Resolved
    ↓
Window Shown
```

---

# Rule

## Application Owns Lifecycle

Application classes own:

```text
Framework Initialization

Theme Loading

Window Creation Timing
```

---

## Windows Own Presentation

Windows own:

```text
Navigation

Layout

Content

User Interaction
```

---

## Dependency Direction

Allowed:

```text
Window
    → ViewModel

ViewModel
    → Services
```

Not Allowed:

```text
App
    → MainWindow
```

through constructor injection.

---

# Verification Requirements

Every Avalonia desktop application shall verify:

```text
Hover State Functionality

Button Interaction

Keyboard Focus

TAB Navigation

Theme Application

Control Template Resolution
```

Verification must occur through:

```text
Manual UI Validation
```

and not solely through:

```text
Build Success

Unit Tests

Integration Tests
```

because this defect passed all automated validation while rendering the UI unusable.

---

# Warning Signs

Future investigations should immediately suspect lifecycle violations when:

```text
Window renders

Chrome works

Interaction does not work
```

especially if:

```text
Hover States Missing

Focus Missing

Keyboard Navigation Missing

Entire Client Area Inert
```

---

# Architecture Guidance

For Avalonia applications:

```text
Application first

Theme second

Window third
```

Never:

```text
Window first

Application second
```

---

# Long-Term Enforcement

Future architecture tests should prevent:

```text
Application constructors
depending directly on Window types.
```

Example Rule:

```text
App constructors may not reference MainWindow.
```

This converts a documented lesson into an enforceable architectural rule.

---

# Related Maintenance Records

```text
MAINT-2026-11-04-001
STA Thread Ownership Investigation

MAINT-2026-11-04-002
Avalonia Lifecycle Correction
```

---

# Consequences

## Positive

```text
Predictable Framework Initialization

Correct Theme Loading

Reliable Input Handling

Standard Avalonia Lifecycle

Reduced Startup Defects
```

## Negative

```text
Window creation deferred until
framework initialization completes.
```

This tradeoff is required and acceptable.

---

# Summary

Avalonia applications shall never construct root windows before the `Application` has completed initialization.

Root windows must be resolved and assigned during:

```text
OnFrameworkInitializationCompleted()
```

after:

```text
Application Current

Theme Loading

Framework Initialization
```

have been completed.

This decision is mandatory for all future Avalonia projects and shall be treated as framework guidance.

---

# End ADR
