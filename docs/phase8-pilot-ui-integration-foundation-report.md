# Phase 8.4 — Pilot UI Integration Foundation Report

## 1. Outcome and phase boundary

Phase 8.4 implements the first UI-consumption layer for controlled pilot evidence. It projects the immutable `PilotExecutionResult` produced by the Phase 8.3 pilot host into immutable, UI-neutral view state. It does not execute a pilot, resolve a service, select or open a database, route a feature, activate a feature, change a production screen, or switch authority.

The implemented flow is:

```text
immutable PilotExecutionResult
        -> feature-specific presenter
        -> safe feedback mapping
        -> immutable PilotFeatureViewState
        -> immutable PilotDashboardState
        -> interface-only future WinForms consumer
```

Legacy production remains authoritative at every stage. The presentation state explicitly exposes that authority is preserved and that execution, routing, activation, and authority switching are unavailable.

The implementation is located in:

- `Application/Pilot/Presentation/PilotUiPresentationContracts.cs`;
- `Application/Pilot/Presentation/PilotPresenters.cs`;
- `Rah_Negar.Tests/Pilot/PilotUiIntegrationFoundationTests.cs`.

No existing production form, designer, menu, navigation path, startup entry point, or `Program.cs` registration references this layer.

## 2. Authoritative baseline and ownership

Phase 8.4 follows the Phase 8.1 integration design, Phase 8.2 pilot foundation, and Phase 8.3 host/adapter boundary.

| Concern | Active owner | Phase 8.4 role |
|---|---|---|
| Production workflow and displayed result | Legacy application | Unchanged |
| Pilot permit and scoped execution | Phase 8.2/8.3 contracts | Not evaluated or executed by UI |
| Legacy and target observation | Phase 8.3 host adapters | Consumed only through immutable host result |
| Comparison evidence | Phase 8.3 result | Safely summarized for presentation |
| Pilot presentation | Phase 8.4 presenters | Pure projection |
| Production navigation/forms | Existing WinForms application | Unchanged |
| Feature activation and authority switch | Future explicitly approved phase | Prohibited |

The presenter layer has no reference to `IPilotHost`. Its coordinator constructor accepts only `IPilotResultPresenter` instances. It has no `IServiceProvider`, repository, connection, settings writer, migration runner, authorization executor, or feature router.

## 3. Presentation contracts

`PilotFeatureViewState` is the primary future UI model. It contains only:

- pilot ID;
- optional known pilot feature;
- fixed title;
- UI status;
- safe description;
- comparison severity;
- fixed comparison summary;
- evidence state and optional safe evidence reference;
- UTC presentation timestamp;
- operator-safe warnings;
- operator-safe blocked reasons;
- correlation ID.

The state exposes read-only properties. Warning and blocked-reason sequences are copied, de-duplicated, ordinally sorted, and wrapped as read-only collections. The model exposes `LegacyAuthorityPreserved = true`, `AllowsExecution = false`, `AllowsRouting = false`, and `AllowsActivation = false`.

The view state deliberately omits:

- `PilotObservationResult` and adapter metadata;
- legacy or target fingerprints;
- source versions and authentication credential versions;
- raw comparison differences supplied by an adapter;
- passwords, password hashes, salts, verifiers, signatures, private keys, or authorization payloads;
- raw database rows, report rows, event rows, or settings values;
- exception objects, messages, stack traces, SQL text, and filesystem paths;
- delegates or application services.

`PilotUiViewStatus` supports the five required UI states:

- `Loading`;
- `Completed`;
- `DifferenceDetected`;
- `Blocked`;
- `Failed`.

Host `Completed` maps to UI `Completed`. Host `CompletedWithDifference` maps to `DifferenceDetected`. Host `Blocked` maps to `Blocked`. Host `TargetFailed`, host `Failed`, and unknown host status values map to `Failed`.

## 4. Feature-specific presenters

Five concrete presenters implement `IPilotResultPresenter`:

- `AuthenticationPilotPresenter`;
- `ReportingPilotPresenter`;
- `RuntimeEventPilotPresenter`;
- `ProtectedSettingsPilotPresenter`;
- `ExportPilotPresenter`.

Each presenter declares exactly one known `PilotFeature` and a fixed title. Shared transformation behavior lives in `PilotResultPresenterBase`, which rejects a result for a different feature. This prevents an authentication result from being displayed through a reporting presenter or an ESD-related blocked result from being mislabeled as an export result.

Presenters do not inspect or display legacy/target observation payloads. They use only host status, host severity, safe evidence/correlation identifiers, timestamps, and blocked reason codes. Even the Phase 8.3 `Comparison.SafeSummary` is not passed through. A compromised or incorrectly implemented upstream adapter therefore cannot place arbitrary free-form text in the Phase 8.4 operator state through that field.

Feature titles are application constants rather than input values. Descriptions always state that legacy remains authoritative. A match is evidence, not authority. A difference is a human-review signal, not a feature switch.

## 5. Safe UI feedback mapping

`PilotUiSafeFeedback` is the centralized mapper. It produces fixed operator text from enums and allow-listed internal reason codes. It never renders raw exception text, SQL errors, stack traces, file paths, credential details, or authorization internals.

Blocked reasons are mapped through an ordinal allow-list. Supported messages include safe explanations for:

- missing pilot context or permit;
- wrong-feature permit;
- unavailable workflow or incomplete input;
- pilot expiry;
- prohibited settings mutation or provisioning;
- prohibited ESD cutover;
- unsafe snapshot observation;
- unavailable workflow result.

Unknown reason codes become one generic message: the result was blocked by a safety rule. The unknown code itself is not emitted.

Pilot, evidence, and correlation identifiers must be non-empty, at most 128 characters, and contain only ASCII letters, digits, hyphen, underscore, period, or colon. Unsafe pilot/correlation identifiers are replaced by fixed placeholders. Unsafe evidence references are withheld. Backslashes, slashes, spaces, newlines, query text, and path-like strings cannot pass this identifier boundary.

The UI timestamp is the host completion timestamp only when both start and completion are UTC and completion is not earlier than start. Invalid time evidence maps to the UTC Unix epoch rather than throwing or presenting ambiguous local time.

Severity mapping emits fixed warnings for informational, warning, critical, and failed comparison states. Unknown severity values fail to `Failed`. No adapter-defined severity description is shown.

## 6. Presentation coordinator and failure isolation

`PilotPresentationCoordinator` selects the presenter by the immutable result's feature. Its constructor:

- rejects null presenters;
- rejects unknown feature declarations;
- rejects duplicate presenters for one feature;
- creates an immutable feature-to-presenter dictionary.

It does not discover presenters, use a service locator, or fall back to a different feature. An unknown or unconfigured feature produces a fixed failed view state with no selected feature and no activation capability.

Presenter exceptions are caught at the presentation boundary. Exception text is discarded. The coordinator returns a fixed failed state while retaining a syntactically safe evidence reference and correlation ID. It does not change the input result or its evidence. Presentation failure cannot call the host, rerun a pilot, mutate evidence, change routing, or affect the existing legacy workflow.

The coordinator publishes four invariant flags:

- `ExecutesPilotWorkflows = false`;
- `RoutesPilotFeatures = false`;
- `ActivatesPilotFeatures = false`;
- `ReadsExternalState = false`.

These flags document the boundary for review and tests; there is no hidden execution implementation behind them.

`CreateLoading` produces a safe loading state without invoking any service. `CreateDashboard` projects an existing immutable feature state plus caller-supplied pilot-session and rollback availability facts. Neither method starts, stops, activates, or rolls back a pilot.

## 7. Dashboard model

`PilotDashboardState` supports a future dashboard or embedded panel with:

- optional active pilot ID;
- selected feature;
- execution status;
- comparison summary;
- evidence availability;
- rollback availability;
- the selected immutable feature state.

It exposes `CanActivateFeature = false` and `CanSwitchAuthority = false`. `RollbackAvailable` is display evidence only; it is not a rollback command and does not invoke the Phase 8.2 rollback planner.

The dashboard state has no collection of services, callbacks, routes, commands, database targets, or mutable records. A future form can render it, but cannot use it to infer that an authority switch is approved.

## 8. Future WinForms integration boundary

`IPilotWinFormsStateConsumer` is an interface-only boundary for a later approved UI phase. It accepts an immutable `PilotDashboardState` and cancellation token. `PilotUiSurfaceKind` distinguishes:

- an existing-form consumer;
- a future pilot form;
- an embedded pilot panel.

Phase 8.4 provides no implementation of this interface. It adds no `System.Windows.Forms` type, form inheritance, designer file, control, navigation entry, menu item, event handler, or startup registration. Existing screens cannot discover or render this state until a future phase explicitly supplies and registers a reviewed adapter.

The boundary is presentation-only. It does not expose host execution, permit evaluation, routing, mutation, evidence editing, feature activation, or authority switching.

## 9. UI capability boundary

The future capability identifiers are defined exactly as:

- `pilot.view`;
- `evidence.view`;
- `comparison.view`.

`PilotUiCapabilityRequest` carries only a capability ID, pilot ID, and correlation ID. `IPilotUiCapabilityBoundary` is an unimplemented extension point returning `Available`, `Unavailable`, or `RequiresManualReview`.

This is not RBAC. Phase 8.4 creates no roles, role assignments, permission tables, actor hierarchy, Support identity, or login behavior. The presentation coordinator does not call the capability boundary. A future approved composition must supply capability evidence without converting these display capabilities into domain authority.

## 10. Security and authority invariants

The implementation enforces the following invariants:

1. UI receives immutable evidence state, not executable services.
2. Legacy authority is always stated and never conditionally disabled.
3. Host match, mismatch, block, and failure are display outcomes only.
4. Unknown features and unknown statuses fail closed.
5. Unknown blocked-reason text is not rendered.
6. Presenter exceptions are isolated and sanitized.
7. Unsafe evidence references are withheld.
8. Observation fingerprints and credential metadata never enter view state.
9. No view state contains a database path or raw row.
10. No UI contract can migrate, provision, finalize, unlock, recalculate, mutate ESD, consume vendor authorization, execute ManagementCredential authority, or switch routing.

The code introduces no RBAC and no Support role, profile, or login. ShiftProfile remains the only normal target operational identity, but Phase 8.4 does not authenticate it or expose its credential version.

## 11. Tests

`PilotUiIntegrationFoundationTests` adds 18 focused test cases covering:

- mapping by all five feature presenters;
- completed, difference, blocked, failed, and loading states;
- immutable properties and defensive collection copies;
- hostile message, SQL, stack trace, path, and secret suppression;
- reflection-based exclusion of credentials, raw rows, signatures, private keys, and exceptions;
- unknown feature rejection;
- allow-listed and unknown blocked-reason handling;
- informational, warning, critical, and failed severity mapping;
- safe evidence/correlation propagation;
- UTC completion and invalid timestamp handling;
- presenter exception isolation and evidence preservation;
- dashboard evidence/rollback display state;
- exact capability identifiers and absence of a capability implementation;
- future WinForms surface enumeration and interface-only boundary;
- absence of host execution, service locator, external-state dependency, and mutation methods;
- absence of connection/repository/migration, RBAC, and Support identity types.

The focused Release run passes 18 of 18 tests. The complete Release suite passes 410 of 410 tests. Tests use constructed immutable results and reflection only; Phase 8.4 tests do not open a database, invoke a pilot host, execute a migration, modify a form, or mutate production state.

## 12. Verification

The complete solution Release build succeeds with zero errors. Six existing NU1701 compatibility warnings remain for the repository's legacy `OpenTK`, `OpenTK.GLControl`, and `SkiaSharp.Views.WindowsForms` packages. Phase 8.4 changes no package or target framework.

Verification confirms:

- focused Phase 8.4 tests: 18 passed, 0 failed;
- complete Release test suite: 410 passed, 0 failed;
- `git diff --check`: no whitespace errors;
- `Program.cs`: protected SHA-256 unchanged and no tracked diff;
- startup: no presenter/coordinator registration;
- production WinForms and designers: no tracked changes;
- default pilot activation: absent;
- feature execution/routing: absent from the presentation layer;
- database connection/read/write APIs: absent from the presentation layer;
- migration execution: absent;
- ESD provisioning, mutation, and cutover: absent;
- production authority switch: absent;
- RBAC and Support identity: absent.

No production database was selected, opened, migrated, or modified during implementation or tests.

## 13. Limitations and next UI activation requirements

Phase 8.4 stops at UI-neutral state and an interface-only future consumer. It does not provide:

- a WinForms form, control, designer, panel, menu, or navigation entry;
- production startup composition;
- persistent dashboard selection or UI preferences;
- pilot host invocation from UI;
- permit acquisition or approval workflow;
- evidence storage or evidence-detail retrieval;
- live monitoring or refresh scheduling;
- localization resources;
- accessibility, DPI, or final visual design validation;
- operator identity or capability implementation;
- production telemetry;
- feature activation, target routing, or authority switch.

Before a real pilot UI surface can be activated, a future phase must provide an explicitly approved, non-default composition root; a reviewed read-only host invocation flow; operator capability evidence; UI-thread and cancellation policy; localization and accessibility review; safe persistent evidence lookup; monitoring and refresh behavior; isolated pilot deployment validation; and an explicit rollback/navigation plan.

Any future form adapter must remain a one-way consumer of `PilotDashboardState`. It must not receive repositories, migration services, protected-setting executors, vendor authorization services, ManagementCredential executors, or feature routers. Adding a display surface is not approval to activate a pilot feature, change authentication, replace reporting, recalculate Runtime/Event data, mutate settings, or cut over ESD authority.
