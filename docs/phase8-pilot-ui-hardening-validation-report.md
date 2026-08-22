# Phase 8.6 - Pilot UI Hardening and Validation Foundation

Status: **Implemented and validated as an inactive, read-only observation surface; legacy production remains authoritative**

Date: 2026-08-22

## 1. Outcome and unchanged boundary

Phase 8.6 hardens the Phase 8.5 WinForms pilot surface against rendering faults, hostile presentation input, lifecycle races, unsafe cross-thread calls, layout constraints, missing localization resources, and accidental performance or production dependencies.

The hardened flow is:

```text
immutable PilotDashboardState
        -> handle-aware RenderAsync boundary
        -> non-throwing visual update boundary
        -> sanitized fixed presentation
        -> read-only operator observation
        -> stop
```

The surface remains inactive. It is not registered in `Program.cs`, startup, a production form, a designer, a menu, a shortcut, or navigation. It does not execute a pilot, access a database, run a migration, mutate ESD, activate a feature, replace a production workflow, or switch authority.

## 2. Phase 8.5 rendering issue closure

The Phase 8.5 audit identified one MEDIUM issue in `UI/Pilot/PilotDashboardControl.cs`: failure during primary rendering could reach a fallback visual update that needed its own independent guard. Phase 8.5 introduced a local safe wrapper. Phase 8.6 formalizes that behavior in `UI/Pilot/PilotRenderingSafetyBoundary.cs` and routes every visual mutation through it.

`PilotRenderingSafetyBoundary.TryUpdate` accepts only an in-process visual action, returns success or failure, catches every visual exception, and retains no exception object or text. It has no retry, logging side effect, service lookup, workflow callback, or external dependency.

Primary state rendering is attempted through the boundary. If it fails, the surface replaces the logical state with a fixed fallback snapshot and attempts that visual update through a separate boundary invocation. Empty and clear states use the same protection. If both primary and fallback controls throw, neither exception escapes. The managed fallback snapshot remains safe even when no visual control can be updated.

Tests deliberately attach a throwing `TextChanged` handler to the pilot ID field. That forces the primary update and the fallback update to fail. The caller receives no exception, raw exception text is absent, `HasState` is false, and no command or refresh is triggered. Separate tests force failure during clear and null-state fallback.

## 3. Hardened rendering and state model

`ReplaceState` accepts an immutable `PilotDashboardState`, replaces the current observation, and renders once. Null state uses the fixed fallback. `ClearState` removes the current state and applies a fixed empty snapshot. No operation performs automatic retry.

`RenderAsync` is non-throwing for cancellation and disposal. A token canceled before rendering returns a completed no-op. A token canceled while a marshaled update is queued prevents the update and completes normally. Calls after disposal also complete without touching controls.

The surface captures its construction thread. An update on that thread renders directly. A background caller with a valid handle is marshaled with `BeginInvoke`; completion is signaled in `finally`. A background caller before handle creation is ignored because touching or creating WinForms controls from the wrong thread would be unsafe. If the handle is disposed or marshaling fails, the request completes without rendering. No path invokes a workflow or legacy form.

## 4. Hostile input validation

The identifier allow-list remains ASCII letters, digits, hyphen, underscore, period, and colon, with a 128-character maximum. Invalid pilot IDs map to `No active pilot`; invalid correlation IDs map to `Correlation unavailable`; unsafe evidence references are withheld and display `Available; reference unavailable` when availability evidence exists.

General display text is limited to 512 characters. It rejects control characters, slash or backslash paths, database and query terms, exception and stack-trace terms, credentials, passwords, secrets, salts, signatures, private keys, authorization text, and common SQL operations. Phase 8.6 extends SQL-like detection to drop, alter, pragma, attach, and create-table forms.

Tests cover invalid IDs, invalid correlation IDs, unsafe evidence references, oversized content, control characters, Windows paths, relative paths, select/drop statements, exception-like strings, stack traces, and credential-like strings. Unsafe comparison text becomes `Comparison details are unavailable.` Unsafe warning and blocked-reason items become fixed safe messages. The rejected input never appears in `PilotSurfaceSnapshot`.

No exception object, exception message, SQL, file path, secret, or raw credential is presented or logged by the UI module.

## 5. DPI and layout validation foundation

`PilotLayoutRequirements` defines testable minimum behavior:

- minimum supported width: 720 logical pixels;
- minimum supported height: 560 logical pixels;
- DPI scaling required;
- auto-scroll required;
- responsive layout required.

`PilotDashboardControl.LayoutContract` publishes the immutable default contract and uses it for `MinimumSize`. The surface retains `AutoScaleMode.Dpi`, `AutoScroll`, docked and percentage-based layout panels, and one-time control construction.

Automated validation creates the control, creates its handle, lays it out at the minimum supported size, then lays it out in a constrained 400 by 300 area. Neither operation throws. This is a layout-resilience foundation, not high-DPI visual certification. Multi-monitor DPI transitions, font substitution, RTL expansion, color contrast, and real display inspection remain manual activation gates.

## 6. Accessibility validation foundation

`PilotAccessibilityRequirements` now explicitly requires DPI scaling, keyboard navigation, accessible names, focusable read-only controls, and prohibition of activation controls.

Tests verify every text box is read-only, remains in the tab sequence, and has a non-empty accessible name. The control tree contains no button or link control. Public surface methods contain no activation, execution, save, update, login, migration, or provisioning operation. The surface therefore supports keyboard inspection without exposing a hidden command path.

This phase does not claim accessibility certification. Screen-reader output order, Windows accessibility tooling, localization speech, focus order under all scale factors, contrast modes, and operator usability still require specialist and manual validation.

## 7. Localization safety boundary

`PilotLocalizedTextKey` remains a fixed 13-value enum. `PilotLocalizationBoundary` optionally reads a future `IPilotLocalizedTextProvider` and always has a fixed safe label for each known key. A missing provider, null or blank resource, oversized resource, unsafe resource, provider exception, or unknown key cannot escape or become a control label; the boundary returns the fixed fallback or `Unavailable`.

The provider boundary is not injected into `PilotDashboardControl` and performs no service resolution. Production labels remain fixed. Tests also render hostile dashboard content and verify that the complete set of label controls remains byte-for-byte unchanged. Dynamic pilot state can populate only sanitized read-only value controls; it cannot define captions or commands.

Complete localization is intentionally absent. A future approved phase must supply reviewed resource files and validate each locale through this boundary.

## 8. Lifecycle and thread behavior

The validated lifecycle is create, render, replace, clear, cancel, dispose, render after dispose, replace after dispose, and clear after dispose. No unexpected exception escapes and no execution flag changes.

Thread validation covers:

- direct update on the owning UI thread;
- background update before handle creation, safely ignored;
- background update with a valid handle, marshaled to the UI thread with illegal cross-thread checks enabled;
- cancellation after a background render has been queued but before dispatch;
- background update after handle disposal.

The surface never creates its own worker, scheduler, timer, or polling loop. Thread marshaling exists only to receive a caller-supplied immutable state safely.

## 9. Performance and dependency safety

The surface publishes fixed invariants: no polling, no timer, no background work, no service resolution, and no control recreation on refresh. Tests render 20 replacement states and prove that the same ordered control instances remain in the tree.

Reflection verifies the pilot UI types have no stored `Task` or timer field. Source scans verify there is no `Task.Run`, timer construction, SQLite namespace, migration runner, pilot host, pilot execution coordinator, ESD type, cutover type, or service provider in `UI/Pilot`.

State replacement changes text and fixed colors only. It performs no database query, file read, evidence retrieval, retry, external call, or production-form invocation.

## 10. Production boundary validation

Automated source validation confirms:

- `Program.cs` SHA-256 is still `33985F732E77AFC7249DDA0174E8BCC58601B5E0B3E22B93E31933F01ACCAA76`;
- `Program.cs` has no pilot UI namespace or control reference;
- startup and all production form source files have no pilot UI namespace, dashboard, navigation-boundary, or refresh-boundary reference;
- the pilot UI has no production form reference or `Form` subclass;
- navigation remains interface-only;
- the UI surface has no feature activation, migration, ESD mutation, authority-switch, RBAC, Support identity, or login implementation.

Final version-control inspection confirms no tracked diff in `Program.cs`, `UI/Startup`, or `UI/Forms`. No production database was selected, opened, migrated, or modified during implementation or testing.

## 11. Tests and verification

`PilotDashboardHardeningTests` adds 25 Phase 8.6 cases. Together with Phase 8.5 surface coverage, the focused dashboard run passes 48 of 48 tests. The cases cover forced primary and fallback failures, all hostile input classes, layout creation and constrained layout, accessibility invariants, localization fallback, immutable labels, complete lifecycle, cancellation, background marshaling, disposed handles, control-tree reuse, absence of background infrastructure, and protected production boundaries.

The complete Release test suite passes 458 of 458 tests. The complete Release solution build succeeds with zero errors. Six existing NU1701 compatibility warnings remain for `OpenTK`, `OpenTK.GLControl`, and `SkiaSharp.Views.WindowsForms`; Phase 8.6 changes no package or framework.

`git diff --check` passes. Focused source scans find no trailing whitespace in Phase 8.6 files and no prohibited production, storage, migration, execution, or ESD dependency.

## 12. Limitations and remaining activation requirements

Phase 8.6 remains a validation foundation, not approval to activate a pilot. It does not provide composition, pilot execution, evidence retrieval, persistent state, telemetry, automatic refresh, production navigation, role assignment, Support identity, alternate login, ESD authority, database migration, or target routing.

Before real pilot activation, a separately approved phase must provide and validate:

1. an explicit, non-default composition root that supplies only immutable dashboard state;
2. reviewed operator capability evidence without granting the view executable authority;
3. real DPI, multi-monitor, RTL, font-scaling, keyboard, contrast, and screen-reader testing;
4. complete localized resources with safe missing-resource behavior;
5. deployment isolation, monitoring, evidence retention, and operator support procedures;
6. explicit close and return-to-legacy navigation with no default route;
7. cancellation, shutdown, handle recreation, and long-duration observation tests in the pilot host environment;
8. independent proof that no database, migration, settings mutation, ESD, credential, RBAC, Support, feature activation, or authority-switch capability reaches the surface;
9. full build, test, diff, dependency, and production-data verification immediately before any approved registration.

## 13. Task-scoped audit record

### Architecture and build

Production startup remains legacy-only. Phase 8.4 owns immutable presentation state, Phase 8.5 owns the isolated WinForms control, and Phase 8.6 adds only UI hardening and validation. The baseline Release build passed with zero errors and six existing compatibility warnings; the baseline suite passed 433 of 433 tests.

### Confirmed finding and fix

The only confirmed task-scoped defect was the Phase 8.5 fallback-isolation item described in Section 2. Severity: MEDIUM. Failure scenario: primary and fallback control mutations both throw. The final non-throwing boundary and forced-failure tests close the issue. No production workflow bug was confirmed.

### Potential validation items

Manual DPI/accessibility/RTL behavior and legacy NU1701 package compatibility remain unvalidated under production hardware. They are not classified as confirmed bugs. No database/schema risk, startup performance regression, DataGrid recreation, polling, resource leak, navigation change, or station-specific logic leakage was introduced by Phase 8.6.

### Prioritized next actions

Keep the surface inactive; complete manual visual and accessibility validation; design a one-way state composition adapter; define reviewed localization resources; validate handle recreation and shutdown in a controlled host; review existing package compatibility separately; and repeat every production-boundary check before any future registration.
