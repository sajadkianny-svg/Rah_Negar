# Phase 8.5 - Pilot UI Surface Implementation Foundation

Status: **Implemented as an inactive, read-only observation surface; production authority and navigation remain unchanged**

Date: 2026-08-22

## 1. Outcome and phase boundary

Phase 8.5 implements the first WinForms surface that consumes the immutable Phase 8.4 `PilotDashboardState`. The surface displays pilot evidence for controlled future observation and stops at presentation. It cannot execute a pilot, activate a feature, mutate settings, access storage, run a migration, perform ESD cutover, or switch authority.

The implemented flow is:

```text
immutable PilotDashboardState
        -> IPilotWinFormsStateConsumer
        -> PilotDashboardControl
        -> sanitized read-only components
        -> operator visualization
        -> stop
```

The module is isolated under `Rah_Negar.UI.Pilot` in `UI/Pilot`. It is explicitly constructible but is not registered in `Program.cs`, startup, a production form, a designer, a menu, a shortcut, or navigation. No automatic opening or default pilot route exists. Legacy production screens and workflows remain authoritative.

## 2. UI surface architecture

`PilotDashboardControl` is a sealed `UserControl` implementing only the Phase 8.4 `IPilotWinFormsStateConsumer` and the Phase 8.5 `IPilotDashboardRefreshTarget`. Its public constructor has no parameters. Its fields are WinForms controls, presentation state, and read-only display components; there is no service provider or executable application dependency.

The module contains:

- `PilotDashboardControl.cs`: explicit surface construction, immutable-state replacement, safe rendering, and clearing;
- `PilotReadOnlyComponents.cs`: reusable status, severity, evidence, warning, and blocked-reason displays plus the surface sanitizer;
- `PilotSurfaceContracts.cs`: refresh, future navigation, localization, accessibility, and immutable rendered-snapshot contracts;
- `PilotDashboardSurfaceTests.cs`: focused rendering, safety, dependency, and production-boundary tests.

The dashboard displays current pilot ID, selected feature, execution state, comparison status, severity, evidence availability and safe reference, rollback availability, correlation ID, UTC timestamp, warnings, and blocked reasons. It contains no buttons or other mutation controls.

## 3. Read-only controls and safe rendering

`PilotStatusDisplay` maps known statuses to fixed text and high-contrast state colors. Failed and unknown values display `Unavailable`. `PilotSeverityDisplay` maps none, informational, warning, and critical severities to fixed labels; failed and unknown values use the safe fallback. `PilotEvidenceSummaryDisplay` shows availability and only an allow-listed identifier. `PilotWarningDisplay` and `PilotBlockedReasonDisplay` render sanitized, de-duplicated, sorted read-only messages.

All dynamic text crosses `PilotSurfaceTextSanitizer`. Identifiers are limited to 128 ASCII letters, digits, hyphens, underscores, periods, and colons. General text is limited to 512 characters and rejects control characters, paths, database/query fragments, exception terminology, credential terms, signatures, secrets, and authorization details. Unsafe values are replaced by fixed operator-safe messages. Raw exceptions are never displayed or retained in the rendered snapshot.

The immutable `PilotSurfaceSnapshot` exposes the effective display values for deterministic verification. Its warning and blocked-reason collections are defensive read-only copies. This snapshot is observation evidence for tests and future adapters; it is not an editable state or a command model.

## 4. State consumption and refresh model

The surface receives only `PilotDashboardState`. It does not accept `PilotExecutionCoordinator`, `IPilotHost`, repositories, SQLite connections, migration services, authorization executors, credential services, settings writers, or feature routers.

`RenderAsync` honors cancellation and delegates to state replacement. `ReplaceState` replaces the prior state in one operation. `ClearState` removes the current state and restores fixed empty values. A null, inconsistent, unknown, or unsafe state fails closed to a fixed fallback. No timer, background scheduler, automatic polling, database refresh, host invocation, or retry is present. `RequestsRefresh` and `ExecutesCommands` are permanently false.

The surface performs UI-thread marshaling only when a handle exists and a caller arrives from another thread. Marshaling failure is swallowed at the observation boundary. Visual updates are also guarded by a fail-closed wrapper so a secondary fallback-rendering error does not escape into pilot or legacy workflow code.

## 5. Navigation boundary

`IPilotNavigationBoundary` is interface-only and reserves three future operations:

- open a pilot dashboard with an already-created immutable state;
- close the pilot dashboard;
- return to the legacy workflow.

There is no implementation, registration, menu item, keyboard shortcut, route, shell replacement, or production form reference. The contract cannot start a pilot or activate a feature. Future composition requires separate explicit approval and must keep return-to-legacy behavior available.

## 6. Localization and accessibility foundation

`IPilotLocalizedTextProvider` and `PilotLocalizedTextKey` define a future localization boundary for the title and field captions. Phase 8.5 intentionally uses fixed English fallback labels and does not register a resource provider.

The dashboard and reusable controls use `AutoScaleMode.Dpi`, layout panels, percentage or automatic sizing, anchoring/docking, and scroll support. Read-only text fields remain keyboard-focusable and have accessible names. The surface publishes `PilotAccessibilityRequirements.Default`, requiring DPI scaling, keyboard navigation, and accessible names. Full localization, screen-reader certification, high-DPI visual inspection, RTL review, and operator usability validation remain future work.

## 7. Failure and authority isolation

Rendering is best-effort and observational. Invalid enum values, inconsistent dashboard/feature state, unsafe identifiers, missing evidence, invalid timestamps, null replacement, disposal, cross-thread marshaling failure, and control-update failure cannot cause workflow execution. The surface either displays sanitized state, displays a fixed fallback, or performs no visual update.

No exception text, SQL, database path, secret, credential, signature, raw authorization detail, or service object is presented. Missing evidence displays `Not available` or `Available; reference unavailable`. Unknown status or severity displays `Unavailable`. Every failure message continues to preserve legacy authority.

The UI namespace has no database, repository, connection, transaction, migration, provisioning, activation, execution, RBAC, Support identity, or login implementation. `PilotDashboardState.CanActivateFeature`, `PilotDashboardState.CanSwitchAuthority`, and the surface command flags remain false.

## 8. Tests

`PilotDashboardSurfaceTests` provides 23 focused passing cases after implementation. Coverage includes:

- explicit dashboard creation and isolated `UserControl` behavior;
- complete immutable state rendering;
- defensive state and collection consumption;
- hostile text suppression;
- blocked-state and difference-state display;
- all defined severity mappings plus unknown fallback;
- missing evidence and unknown status handling;
- state replacement, clear, cancellation, and disposed-surface behavior;
- absence of buttons, mutation commands, activation methods, automatic refresh, and execution;
- absence of service, host, coordinator, repository, connection, migration, authorization, credential, and service-provider injection;
- interface-only navigation;
- localization, DPI, keyboard, and accessible-name foundations;
- absence of storage, migration, activation, RBAC, and Support identity types;
- source-level verification that `Program.cs`, startup, and existing production forms do not reference the pilot surface or its navigation/refresh contracts.

The complete Release test suite passes 433 of 433 tests. Tests construct immutable in-memory presentation state only. The focused UI tests do not open a database, execute a migration, invoke a pilot host, activate a feature, perform ESD operations, or switch authority.

## 9. Build and dependency health

The complete Release solution build succeeds with zero errors and six existing NU1701 warnings. The warnings concern `OpenTK 3.1.0`, `OpenTK.GLControl 3.1.0`, and `SkiaSharp.Views.WindowsForms 3.119.0` resolving through .NET Framework assets for both application and test projects. Phase 8.5 adds or upgrades no package.

NuGet inspection reports no known vulnerable package in either project from the configured sources. It reports no deprecated application package; test package `xunit 2.9.3` is marked legacy with xUnit v3 as the alternative. Several newer package versions exist. They were not adopted because this phase prohibits unrelated upgrades and because major updates require compatibility review. The project also retains overlapping SQLite package/direct-reference entries; they predate Phase 8.5 and were not changed.

## 10. Production boundary verification

Verification establishes:

- `Program.cs` SHA-256 remains `33985F732E77AFC7249DDA0174E8BCC58601B5E0B3E22B93E31933F01ACCAA76` and has no tracked diff;
- startup and production WinForms sources have no tracked diff and no reference to `Rah_Negar.UI.Pilot`;
- no existing designer, menu, navigation tree, shortcut, or main-shell code changed;
- no default route or automatic opening exists;
- the pilot UI has no database or repository dependency and selected no production database;
- no migration was executed and no schema was changed;
- no ESD cutover, production authority switch, RBAC, Support identity, or login replacement was introduced;
- `git diff --check` reports no whitespace error.

Restore, build, and tests wrote only normal local build artifacts. No production database file was selected, opened, migrated, or modified.

## 11. Limitations and requirements before activation

Phase 8.5 does not provide a pilot host composition, permit acquisition, evidence retrieval, persistent UI preferences, localization resources, automated refresh, telemetry, menu/navigation integration, operator authorization, production routing, target authority, or rollback command. Rollback availability is display evidence only.

Before a real pilot is activated, a separately approved phase must provide an explicit non-default composition root; reviewed construction and lifetime rules; a read-only means to obtain already-produced dashboard state; operator capability evidence without creating ad hoc RBAC; localization and accessibility validation; DPI and RTL visual testing on supported displays; cancellation and UI-thread policy; deployment isolation; evidence retention rules; monitoring; return-to-legacy navigation; and an approved rollback procedure. That phase must independently prove that adding composition does not grant execution, migration, settings mutation, ESD, credential, or authority-switch capabilities to the view.

## 12. Initial audit record

### A. Architecture map

Production startup flows from `Program.cs` to the legacy login or startup form and then existing production forms. Phase 8.2 defines pilot controls, Phase 8.3 hosts read-only comparison, Phase 8.4 projects immutable presentation state, and Phase 8.5 consumes only that state in an unregistered WinForms module. Application, infrastructure, data, and production form dependencies do not flow back into the pilot UI.

### B. Build status

Release build: successful, zero errors, six existing compatibility warnings. Baseline focused tests were 21/21 and baseline full tests were 431/431. Final counts are 23/23 focused and 433/433 full.

### C. Dependency/package health

No known vulnerabilities were reported. The legacy xUnit v2 package is deprecated, three transitive UI packages generate NU1701 compatibility warnings, updates are available, and overlapping SQLite references warrant a future dedicated dependency review. No package change is justified within this phase.

### D. Confirmed bugs

One Phase 8.5 draft issue was confirmed: a failure during primary rendering could enter a fallback render that was not independently guarded. Failure scenario: both state rendering and fallback control updates throw, allowing an observational UI exception to escape. Severity: MEDIUM. Location: `UI/Pilot/PilotDashboardControl.cs`, `ReplaceState`/fallback path. Fix: route empty, fallback, and clear rendering through a non-throwing visual update wrapper. No production workflow bug was confirmed during the task-scoped audit.

### E. Potential bugs requiring validation

The NU1701 packages require runtime compatibility validation on supported machines. High-DPI, keyboard order, screen-reader, and RTL behavior require manual visual/accessibility validation. These are validation items, not confirmed defects.

### F. Incomplete functionality

Pilot navigation, composition, localization, evidence retrieval, refresh scheduling, authorization evidence, and activation are intentionally absent. Their absence is a safety requirement for Phase 8.5 rather than an implementation defect.

### G. Database/schema risks

The pilot UI introduces no schema, SQL, connection, repository, or migration reference. Existing overlapping SQLite dependencies are a package-maintenance risk, but no database consistency or schema defect was reproduced in this phase.

### H. Performance problems

No pilot UI performance problem was confirmed. The surface creates its controls once, replaces text in place, and has no polling, DataGrid recreation, database work, or startup cost because it is not registered.

### I. UI/DPI problems

No layout defect was reproduced. DPI-aware layout and accessibility contracts are present, but multi-monitor DPI, font scaling, RTL, localization expansion, color contrast, and assistive-technology validation remain required before activation.

### J. Code duplication/technical debt

Fixed display labels are intentionally local while localization remains unimplemented. Status/severity text appears in both snapshot mapping and reusable controls; a future localization phase may centralize it without changing authority boundaries. Existing package overlap is unrelated technical debt.

### K. Prioritized remediation plan

1. Keep the surface unregistered until explicit activation approval.
2. Complete manual DPI, accessibility, RTL, and hostile-state UI validation.
3. Design a one-way, read-only composition adapter that supplies only immutable dashboard state.
4. Define localized resources and safe evidence-retention/retrieval policy.
5. Review legacy package compatibility and SQLite dependency overlap in a separate change.
6. Re-run full build, tests, boundary scan, diff inspection, and production-data safeguards before any pilot route is added.
