# Phase 9.1 - Controlled Production Pilot Foundation

Status: **Implemented as an inactive, read-only production-pilot observation foundation; legacy remains authoritative**

Date: 2026-08-22

## 1. Outcome and phase boundary

Phase 9.1 creates a limited controlled-production-pilot boundary without connecting it to production startup, routing, UI, databases, or workflow authority. The boundary validates an explicit pilot context, Phase 9.0 preparation evidence, selected-operator approvals, typed read-only observers, and rollback readiness. An operator must then move an in-memory session through explicit lifecycle calls before one observation batch can produce immutable evidence.

```text
explicit ControlledProductionPilotContext
        + Phase 9.0 ApprovedForPreparation evidence
        + selected-operator approval evidence
        + five read-only observation boundaries
        + verified rollback evidence
        -> Approve
        -> Start
        -> BeginObservation
        -> ObserveAsync once
        -> immutable monitoring and pilot evidence
        -> Complete or record Stop
        -> stop
```

No step changes production authority. `Start` starts only an in-memory observation session. `ObserveAsync` calls only explicitly supplied observation providers that return preconstructed immutable results. It does not call the existing pilot host or workflow executors. The coordinator cannot activate features, register routes, execute migration, mutate production data or settings, create users, execute ESD, replace a legacy workflow, or switch authority.

Implementation is isolated in `Application/Pilot/Production`:

- `ControlledProductionPilotContracts.cs` defines context, approval, lifecycle, observation, monitoring, stop, evidence, operation-result, and observer contracts;
- `ControlledProductionPilotCoordinator.cs` implements validation, explicit session transitions, single-batch observation, failure isolation, and disposal;
- `ControlledProductionPilotFoundationTests.cs` validates the complete boundary and protected production surface.

No Phase 9.1 type is referenced by `Program.cs`, startup, navigation, or an existing production form.

## 2. Production pilot context

`ControlledProductionPilotContext` is immutable and explicitly constructed with pilot ID, release identifier, target scope, selected operators, approved features, activation-preparation reference, rollback reference, monitoring reference, and UTC start/end window. Operator and feature collections are defensively copied, deduplicated, sorted, and exposed read-only.

The target scopes are restricted to Rasht read-only observation, Ramsar read-only observation, or combined Rasht/Ramsar read-only observation. The five approved feature categories are authentication, reporting, runtime/event, protected settings, and export observation. They use the Phase 8.8 validation-workflow enum rather than executable feature or routing contracts.

Context validation requires safe identifiers, at least one selected operator, at least one defined feature, a defined target scope, UTC boundaries, and a strictly increasing window. The context reports no automatic activation, environment discovery, production fallback, or authority change.

Identifiers use a restricted 128-character allow-list. Paths, control characters, SQL-like strings, exceptions, credentials, passwords, secrets, private-key terms, SQLite terms, connection strings, and tokens cannot enter accepted context or evidence. Failure returns fixed reason codes and does not echo unsafe input.

## 3. Phase 9.0 and rollback prerequisites

The coordinator receives an already-created `ProductionActivationReadinessResult`; it does not invoke Phase 9.0. Approval requires the exact `ApprovedForPreparation` decision, a non-null cutover package, no blockers or review items, legacy-authority preservation, and no activation permission. The package ID must exactly match the context activation-preparation reference.

This dependency preserves the Phase 9.0 distinction: approved for preparation is not approved for activation. Phase 9.1 neither advances the Phase 8.0 production state machine nor calls an activation guard or executor.

The supplied `RollbackVerificationResult` must be `Verified`, match the context rollback-plan reference, and contain safe owner and evidence references. Rollback readiness remains evidence only. The production-pilot coordinator has no restore, reversal, file, database, or destructive-operation method.

An invalid preparation package, missing package, review result, blocking result, mismatched reference, unavailable rollback, unsafe rollback metadata, or mismatched rollback plan causes approval to fail closed before a session starts.

## 4. Operator approval boundary

Every selected operator requires exactly one `ControlledPilotOperatorApproval`. It contains operator reference, approval reference, UTC approval timestamp, and approved scope. The set of approval operator references must exactly equal the context’s selected operators, without duplicates. Each scope must equal the context scope, and each approval must predate the explicit coordinator approval call.

The model is governance evidence only. It does not authenticate the operator, replace login, implement RBAC, create a permission, create a user, open a session in the application’s identity system, or establish organizational authorization. Operator references are opaque safe identifiers.

Missing, duplicate, mismatched, unsafe, future-dated, or wrong-scope approvals transition the in-memory pilot session to `Failed` with `production-pilot-operator-approval-invalid`. No raw exception or approval content is returned.

## 5. Explicit session lifecycle

`ControlledProductionPilotCoordinator` supports all requested states:

1. `Created`: construction stores explicit immutable dependencies but calls nothing.
2. `Approved`: an explicit `Approve` validates context, preparation, rollback, operators, observers, and approval time.
3. `Started`: an explicit `Start` supplies a safe session ID and time within the approved window.
4. `Observing`: an explicit `BeginObservation` opens the in-memory observation state.
5. `Completed`: after one successful observation batch, explicit `Complete` records completion.
6. `Stopped`: explicit `Stop` accepts a valid immutable stop decision while started or observing.
7. `Failed`: invalid evidence or observer/monitoring/cancellation failure closes the attempt.
8. `Disposed`: explicit disposal cancels an in-flight boundary and is permanent.

Calls out of order return fixed blocked results without silently skipping states. Time must remain UTC and inside the context window. Observation is single-use; the coordinator rejects concurrent or repeated observation and cannot restart a completed, stopped, failed, or disposed session. Disposal is idempotent.

There is no timer, scheduler, polling, automatic restart, `Task.Run`, worker thread, startup hook, or background execution. Asynchrony exists only so a future read-only provider may honor caller cancellation. Work begins only in response to an explicit caller method.

## 6. Read-only observation model

`IControlledProductionPilotObserver` exposes only its feature and `ObserveAsync`. Five marker interfaces make feature contracts explicit:

- `IControlledAuthenticationPilotObserver`;
- `IControlledReportingPilotObserver`;
- `IControlledRuntimeEventPilotObserver`;
- `IControlledProtectedSettingsPilotObserver`;
- `IControlledExportPilotObserver`.

The coordinator requires exactly one observer for each approved feature, rejects duplicates or unrelated features, and verifies the correct marker interface. The supplied concrete observers retain and return a preconstructed immutable result. They execute no workflow and access no database.

`ControlledPilotObservationResult` contains feature, match/difference status, safe result fingerprint, validation summary, difference summary, evidence reference, and UTC observation time. Only match and difference are accepted as valid evidence. Unavailable, failed, null, wrong-feature, unsafe, future, writable, mutation-capable, or authority-changing results fail closed.

The coordinator calls approved observers sequentially once. It does not call the Phase 8.3 `IPilotHost`, `PilotExecutionCoordinator`, repositories, services, production forms, feature router, login, settings, report generator, runtime/event engine, exporter, or migration layer.

## 7. Monitoring evidence

`PilotMonitoringEvidence` contains pilot ID, session ID, UTC timestamp, health status, validation summary, difference summary, and rollback status. A deterministic factory maps an all-match batch to `Healthy`; any recorded difference maps to `AttentionRequired`. A difference remains evidence and does not cause correction, target routing, or authority change.

Monitoring evidence must match the pilot, session, explicit observation time, expected health, and verified rollback status. Summaries must be safe identifiers. The model explicitly contains no secrets, credentials, raw logs, database content, or telemetry implementation.

`IPilotMonitoringEvidenceFactory` is an in-memory evidence-construction boundary, not a telemetry sink. The supplied factory stores nothing, sends nothing, subscribes to nothing, and starts no monitoring loop. Factory exceptions and invalid evidence become fixed monitoring-failure codes and transition the session to `Failed` without retaining partial pilot evidence.

`ControlledPilotEvidence` aggregates the sorted immutable observation results with monitoring evidence, pilot/session identity, and timestamp. It permanently records legacy-authority preservation and no production mutation.

## 8. Stop and rollback decision boundary

`PilotStopDecision` supports validation failure, operator stop, evidence mismatch, security concern, and rollback requested. It contains a safe decision ID, pilot ID, session ID, reason, evidence reference, and UTC decision time.

The decision must match the active pilot and session, fall inside the approved window, and follow session start. A valid explicit stop transitions the in-memory session to `Stopped`, cancels any in-flight observation boundary, and retains available evidence. It does not automatically stop production, execute rollback, perform a destructive action, change routing, disable a feature, or restore a database.

A malformed or mismatched stop decision produces `production-pilot-stop-decision-invalid`, moves the session to `Failed`, and records no decision. Even `RollbackRequested` is only an evidence record; a future separately authorized rollback workflow is still required.

## 9. Failure isolation and production protection

Configuration enumeration, observer calls, monitoring construction, cancellation, and disposal have exception boundaries. Raw exceptions are discarded. Representative fixed codes include `production-pilot-context-invalid`, `production-pilot-preparation-evidence-invalid`, `production-pilot-rollback-not-ready`, `production-pilot-operator-approval-invalid`, `production-pilot-observer-failed`, `production-pilot-observation-invalid`, `production-pilot-monitoring-failed`, `production-pilot-stop-decision-invalid`, and `production-pilot-disposed`.

The coordinator exposes fixed declarations proving no automatic activation/restart, scheduler, background execution, polling, authority change, migration, production mutation, settings modification, user creation, ESD execution, feature activation, login replacement, settings replacement, reporting replacement, runtime/event replacement, export replacement, RBAC creation, or Support identity.

Failure never falls back to a legacy command or invokes production to compensate. Legacy remains authoritative before, during, and after every in-memory state.

## 10. Tests and verification

The focused Phase 9.1 suite contains 24 passing cases after theory expansion. It covers immutable context and defensive copies; operator approval; every lifecycle state and invalid transition; all five typed observation boundaries; read-only and legacy-authority invariants; difference monitoring; all five stop reasons; rollback evidence; missing approval; invalid context; invalid preparation; observer failure; monitoring failure; invalid stop decision; cancellation; disposal; no restart; non-execution declarations; namespace dependency scans; and production startup/UI scans.

Boundary tests pin the established `Program.cs` SHA-256 and verify no Phase 9.1 reference in `Program.cs`, startup, or production forms. Reflection and source scans verify no SQLite provider, repository, migration runner, production migration executor, WinForms, production UI, pilot host, pilot execution coordinator, service locator, timer, periodic timer, background task, activation method, migration method, or automatic rollback method.

The complete Release solution builds with zero errors and the same six pre-existing NU1701 compatibility warnings for OpenTK, OpenTK.GLControl, and SkiaSharp.Views.WindowsForms. The complete test suite passes. `git diff --check` passes. `Program.cs`, startup, production WinForms, and navigation are unchanged by Phase 9.1. No production database mutation, migration, ESD cutover, authority switch, RBAC, or Support identity was introduced or executed.

## 11. Limitations before real pilot execution

Phase 9.1 is an execution-boundary foundation, not a deployed pilot. Its observers return preconstructed evidence; they are not connected to a production read model. There is no startup composition, menu, route, form, operator console, real environment adapter, telemetry sink, evidence store, alert channel, feature flag, production database reader, authentication integration, settings integration, report integration, runtime/event integration, export integration, or rollback executor.

Before a real pilot, a separately approved phase must define installation-specific scope and duration; provide independently reviewed read-only adapters; prove data-source immutability and least privilege; validate deterministic fingerprints for Rasht and Ramsar; establish operator authentication outside this evidence model; define approval authority, expiry, revocation, and separation of duties; implement evidence integrity, retention, privacy, and audit controls; establish monitoring thresholds, alert ownership, and incident procedures; rehearse stop and rollback runbooks; and complete security, operational, data-owner, and product approval.

Any live composition must remain explicitly opt-in and must preserve legacy login, settings, reporting, runtime/event, export, database, ESD, and authority behavior. Production authority cannot change until a later, separately authorized cutover phase implements and validates that decision.
