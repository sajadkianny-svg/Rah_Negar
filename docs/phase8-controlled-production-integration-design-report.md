# Phase 8.1 — Controlled Production Integration Design Foundation

Status: **Implemented as inactive bridge architecture; legacy production authority remains unchanged**

Date: 2026-08-22

## 1. Executive conclusion

Phase 8.1 defines the controlled bridge between current legacy production authority and the inactive target foundations created in Phases 4–8. It provides an integration inventory, immutable routing decisions, generalized read-only shadow comparison, workflow-specific boundaries, feature approval evaluation, isolated pilot contracts, an activation dependency graph, monitoring contracts, and central safety rules.

It does not route a production request. It does not register services, change startup, enable a feature, replace a WinForms screen, select or open a production database, run a migration, provision or cut over ESD authority, or switch reporting/authentication/Runtime/Event authority. No Phase 8.1 type is referenced by current production composition.

The future sequence is explicitly modeled as:

```text
Legacy production authority
        -> read-only shadow validation
        -> comparison evidence
        -> isolated selected-scope pilot boundary
        -> feature-specific approval
        -> future authority switch
```

Phase 8.1 stops after contracts and policy evaluation. Legacy remains the only active production authority.

## 2. Integration boundary inventory

`IntegrationBoundaryInventory.CreateCurrent()` records the current, legacy, and future owners, integration point, activation dependency, and required earlier phases for eight areas.

| Area | Current target component | Legacy production owner | Future owner | Planned integration point | Activation dependency |
|---|---|---|---|---|---|
| Authentication | Phase 7 ShiftProfile and credential contracts | `FrmLogin`, `AppSession`, `AppSettingsService` | ShiftProfile authentication adapter | Observe legacy login and compare target result | Migrated/provisioned credential persistence and scoped approval |
| Reporting snapshots | Immutable snapshot/read contracts | `FrmReportCenter`, legacy `Services.Reports` | `IFinalizedReportReader` and snapshot reporting | Read-side snapshot shadow comparison | Snapshot, export, read-routing validation and approval |
| Runtime/Event projection | Runtime shadow runner and Event comparison service | Legacy Runtime/Event report services and tables | Runtime projection engine and normalized Event chain | Read-only copy evaluation and comparison evidence | Stable evidence and selected pilot approval |
| Security persistence | Phase 7.7 inactive SQLite security adapters | Legacy `app_settings` and session behavior | ShiftProfile, ManagementCredential, keys, audit, replay persistence | Future composition after approved migration | Migration, provisioning, recovery, and approval |
| Migration readiness | Phase 7.9 preflight/backup/rehearsal | No automatic startup migration owner | Future `IProductionMigrationExecutor` | Explicit installation and maintenance authorization | Verified evidence, approval, and migration authorization |
| Protected settings | Phase 7 protected settings and exactly-once ESD contracts | `FrmSettings` and legacy `app_settings` | Management proof plus external vendor authorization | Legacy-authoritative shadow/pilot presentation | Security persistence and separate ESD-cutover approval |
| Report export | Snapshot export contracts/renderers | Legacy report/PDF/Excel services | `IReportExporter` over validated snapshots | Artifact validation before pilot reads | Snapshot/export validation and approval |
| Activation controls | Phase 8.0 state/evidence/approval/guard | No legacy automatic activation owner | Future controlled executors | Feature guard and explicit routing decision | Evidence package, approval, checklist, rollback readiness |

The inventory is executable metadata for future planning, not a registration catalog. It contains no service resolver, reflection discovery, factory, or startup action.

## 3. Authority model and routing modes

`IntegrationAuthorityMode` defines the four future modes:

- `LegacyOnly`
- `ShadowValidation`
- `PilotTarget`
- `FullTarget`

Every `IntegrationRoutingRequest` explicitly identifies the feature, requested mode, target scope, evidence package ID, correlation ID, and safety context. `IntegrationAuthorityRoutingDecision` retains the requested mode and exposes a nullable effective mode. A blocked request has no effective mode; it is not silently changed to `LegacyOnly`. The separate `LegacyRemainsAuthoritative` field records that existing production remains authoritative after rejection without pretending that routing succeeded.

Mode semantics are:

| Mode | Authority | Target behavior | Mutation |
|---|---|---|---|
| `LegacyOnly` | Legacy | Target not invoked by the contract | Prohibited |
| `ShadowValidation` | Legacy | Read-only target observation and comparison | Prohibited |
| `PilotTarget` | Global legacy; future selected pilot scope only | Requires approved feature decision, isolated pilot, and rollback | Prohibited by Phase 8.1 |
| `FullTarget` | Describes a future approved target route | Requires complete evidence and approval | No executor exists in this phase |

No policy promotes modes automatically. A caller must create a new request for each requested mode and retain its evidence/correlation binding. Unknown enum values fail closed.

## 4. Central integration safety rules

`IntegrationSafetyValidator` encodes the common rejection rules before workflow-specific evaluation:

- unknown mode or feature;
- missing evidence package ID or correlation ID;
- any ESD cutover request during feature integration;
- `PilotTarget` or `FullTarget` without an `Allowed`, exactly bound feature decision;
- pilot routing without a valid isolated pilot and legacy rollback;
- authentication target routing without migration readiness;
- reporting/export target routing without snapshot validation.

The routing policy also requires exact equality among requested feature, mode, evidence ID, correlation ID, and their safety context. Binding failure returns `Blocked`, leaves effective mode null, retains legacy authority, and permits no production mutation.

These rules do not grant production activation when they return `Allowed`; they only describe a valid future routing decision. No router is composed in production.

## 5. Generalized shadow comparison architecture

`GeneralizedShadowComparisonCoordinator<TRequest,TLegacy,TTarget>` is UI-neutral and database-neutral. It depends only on:

- `ILegacyShadowResultReader`, whose output is explicitly authoritative;
- `IReadOnlyTargetShadowEvaluator`, which exposes evaluation rather than mutation;
- `IShadowResultComparer`, which returns a fingerprint, severity, and safe differences.

The coordinator reads legacy first, evaluates the target read-only path, compares both, and returns an immutable result containing:

- legacy result;
- target result;
- comparison fingerprint;
- zero or more safe difference codes/descriptions;
- severity (`None`, `Informational`, `Warning`, `Critical`, or `Failed`);
- evidence/correlation ID;
- feature and target scope;
- UTC observation time;
- legacy and target version labels;
- safe result category.

`LegacyRemainsAuthoritative` is always true and `TargetProductionMutationAllowed` is always false. The coordinator accepts no repository writer, transaction, callback mutation, UI presenter, or authority switch. Non-cancellation failures return safe comparison-unavailable evidence without leaking exception text or replacing the legacy result.

This generalized boundary complements the existing specialized Runtime shadow runner and Event comparison service. It does not replace or invoke either in production. Future adapters may translate their typed outputs into generalized evidence.

## 6. Authentication integration boundary

The future authentication model is:

```text
legacy login observation
        -> target ShiftProfile authentication observation
        -> comparison/routing decision
        -> optional approved pilot or future authority
```

`AuthenticationIntegrationMode` provides `LegacyLogin`, `ShiftProfileShadow`, `ShiftProfilePilot`, and `ShiftProfileAuthority`. The target observation contains only ShiftProfile ID, Station ID, credential version, success, and a safe category. It has no role, permission, ManagementCredential login, or Support identity.

`AuthenticationIntegrationPolicy` maps each workflow mode to the matching generic authority mode. Shadow keeps legacy login authoritative. Pilot/authority requires migration readiness and a valid target ShiftProfile observation. Missing ShiftProfile/Station, invalid credential version, routing mismatch, or mutation-capable routing blocks the decision.

ManagementCredential remains privileged proof material for protected actions and is not accepted as a normal login observation. No contract replaces `FrmLogin`, creates a session, or invokes a credential repository.

## 7. Reporting and snapshot integration boundary

`ReportingIntegrationMode` provides the required modes:

- `LegacyReporting`
- `SnapshotShadow`
- `SnapshotPilot`
- `SnapshotAuthority`

Reporting evidence records snapshot validation, finalized-snapshot immutability, legacy readability, export validation, read-routing validation, and comparison evidence ID. `SnapshotShadow` requires a valid immutable snapshot while keeping legacy reports authoritative/readable. Pilot additionally requires export validation. Future authority additionally requires validated read routing.

Invalid or mutable snapshot evidence fails closed. The integration policy exposes read permission only; it has no snapshot writer, report recalculation, finalization operation, UI selection, or authority switch. Ordinary ShiftProfile Finalize behavior and existing finalized-month protection remain unchanged.

## 8. Runtime/Event integration boundary

`RuntimeEventIntegrationMode` provides:

- `LegacyRuntimeEvent`
- `TargetProjection`
- `ShadowComparison`
- `PilotReadOnly`

Evidence separately identifies Runtime and Event comparison records, requires a read-only target, forbids mutation attempts and recalculation side effects, and requires evidence preservation. Any missing evidence, mutation indication, or recalculation side effect blocks the request.

Shadow and pilot keep legacy projection authoritative. `TargetProjection` describes only a future fully approved route and still requires read-only evidence in this foundation. No Event insert/update/delete, Runtime baseline update, cache rebuild, report recalculation, or production data access is exposed.

## 9. Protected settings integration boundary

`ProtectedSettingsIntegrationMode` provides:

- `LegacySettings`
- `ProtectedSettingsShadow`
- `ProtectedSettingsPilot`

Every allowed result keeps legacy settings authoritative and target provisioning disallowed. Evidence must show `LegacyAuthoritative` ESD mode, readable legacy settings, no target provisioning request, no ESD cutover request, no mutation attempt, and a nonblank evidence ID.

The central safety validator also rejects ESD cutover regardless of routing mode. Phase 8.1 cannot provision `SecurityDeploymentSettings`, consume vendor authorization, execute a protected change, or switch ESD ownership. A future pilot may observe protected decision behavior only after separate approval; actual ESD changes continue through current production authority until a dedicated cutover phase.

## 10. Feature activation coordinator

`IFeatureIntegrationActivationCoordinator` evaluates an activation evidence package, feature-specific approval, feature, target scope, and correlation ID. Its implementation is a pure evaluator named `FeatureIntegrationActivationCoordinator`; it has no feature executor or configuration writer.

It validates:

- complete Phase 8.0 evidence;
- correlation binding;
- exact feature and selected target-scope approval;
- exact Phase 8.0 approval scope;
- database identity, evidence package, correlation, timestamp, and expiry;
- evidence approval-boundary scope.

Feature-to-scope mappings cover authentication, snapshot reporting, Runtime/Event projection, protected settings, report export, and migration tooling. Missing or mismatched approval returns `Blocked`. Historical draft/adoption states return `RequiresManualReview`. Only complete, current, exactly bound evidence returns `Allowed`.

`Allowed` is not execution. There is no feature executor in the integration namespace, no configuration update, and no registration.

## 11. Pilot environment boundary

`PilotEnvironmentBoundary` describes a future pilot with:

- explicit pilot ID;
- required isolation;
- one selected Station;
- explicit selected ShiftProfile IDs;
- a limited feature set;
- mandatory rollback to legacy;
- evidence package and correlation IDs.

Validation rejects non-isolated pilots, empty station/shift scope, empty or unlimited feature selection, migration tooling in a pilot, missing evidence, or absent legacy rollback. `ProductionRegistrationAllowed` and `ActivationPerformed` are hard-coded false in Phase 8.1.

Pilot validation produces evidence for a future routing request; it cannot create a deployment, update startup, select a database, or change a feature.

## 12. Activation dependency graph

`IntegrationDependencyGraph.CreateDefault()` provides ordered tracks and prerequisites:

1. Migration readiness: unified chain, backup, rehearsal, integrity, and migration approval.
2. Security persistence: ShiftProfile/credential persistence after migration readiness, recovery, and authentication approval.
3. Snapshot validation: migration readiness plus snapshot comparison, export validation, read routing, and reporting approval.
4. Runtime/Event validation: migration readiness plus Runtime/Event comparison and read-only pilot approval.
5. Protected settings validation: security persistence plus management proof, vendor authorization, replay, and separate ESD cutover.

Each node records required earlier phases, dependency IDs, approval scopes, blockers, and numeric activation order. The validator rejects duplicate IDs, missing/out-of-order dependencies, invalid order, or absent phase/approval evidence. It plans order only; it performs no node.

## 13. Monitoring boundary

`IntegrationMonitoringSignalKind` covers:

- authentication comparison;
- report comparison;
- Runtime/Event difference;
- security failure;
- migration status;
- rollback readiness.

Signals carry a signal ID, kind, severity, evidence/correlation ID, target scope, UTC observation time, and safe result category. `IntegrationMonitoringPlan` is complete only when all six kinds plus monitoring owner and rollback escalation references are present.

`IIntegrationMonitoringSink` is a contract only. There is no telemetry, network, log, database, or monitoring-provider implementation, and Phase 8.1 emits no signal.

## 14. Rollback model

Phase 8.1 reuses Phase 8.0 rollback readiness and makes rollback-to-legacy mandatory at the pilot boundary. A valid pilot must have a positive rollback availability result. A blocked target route has no effective target mode, so current legacy behavior continues without a hidden reroute operation.

No automatic rollback exists. Future pilot procedures must define owner, trigger thresholds, evidence retention, approval, routing reversal, and validation. Database restore remains a separate explicit operational decision under Phase 7.9 contracts.

## 15. Tests

`ControlledProductionIntegrationDesignTests` covers:

- complete Phase 4–8 boundary inventory;
- `LegacyOnly` and `ShadowValidation` routing semantics;
- unknown mode rejection without hidden fallback;
- pilot blocking without approval, isolation, or rollback;
- generalized shadow comparison, differences, evidence propagation, and retained legacy authority;
- safe target-evaluation failure and zero production mutation;
- feature activation blocking without approval and allowing only complete bound evidence;
- manual review for historical adoption;
- ShiftProfile-only authentication shadow and migration-readiness blocking;
- immutable reporting shadow and snapshot-validation blocking;
- Runtime/Event read-only evidence and mutation/recalculation blocking;
- protected settings legacy authority and provisioning/cutover rejection;
- central ESD cutover prohibition;
- pilot station/shift/limited-feature/rollback validation;
- ordered dependency graph and complete monitoring plan;
- no SQLite, WinForms, startup, migration runner, executor, telemetry provider, RBAC, or Support identity in the integration layer.

The focused suite contains 19 passing cases after xUnit theory expansion. Full solution results are recorded below.

## 16. Current authority assessment

| Workflow | Active production authority | Target Phase 8.1 status |
|---|---|---|
| Login/authentication | Existing `FrmLogin`/legacy session behavior | Contracts only; no replacement |
| Reporting | Existing legacy report services/UI | Shadow/read contracts only |
| Finalized snapshots | Existing production finalized evidence | Immutable target validation boundary only |
| Runtime/Event | Existing legacy calculations/data | Read-only comparison boundary only |
| Protected settings | Existing `FrmSettings`/`app_settings` | Legacy-authoritative observation only |
| ESD | `app_settings.esd_extra_runtime_hours` | No provisioning or cutover |
| Export | Existing legacy export/report services | Snapshot artifact validation boundary only |
| Migration | No startup registration | Phase 7.9 evidence plus future executor contract only |
| Feature routing | Existing production behavior | No Phase 8.1 registration or enabled feature |

## 17. Remaining requirements before real integration

1. Perform an approved installation-specific Phase 7.9 assessment and Phase 8.0 activation evidence/approval cycle.
2. Define canonical comparison fingerprints, severity thresholds, acceptance windows, sample sizes, and sign-off owners for each workflow.
3. Build read-only adapters for legacy and target results using isolated copies or transactionally safe read boundaries; separately review any adapter before composition.
4. Define how legacy login observations map to ShiftProfile without exposing credentials or creating a second identity.
5. Validate snapshot reads and exported artifacts against legacy reports across finalized Rasht/Ramsar fixtures without changing production read routing.
6. Complete Runtime/Event comparison acceptance criteria, Persian-time cases, and representative station-specific behavior.
7. Define pilot packaging, isolated environment, selected station/shifts, rollback owner, monitoring window, and evidence retention.
8. Implement a monitoring provider only after privacy, local retention, failure handling, and offline operation are approved.
9. Implement feature executors and routing adapters only in a future phase, behind explicit approval and unchanged-by-default configuration.
10. Keep protected settings and ESD provisioning/cutover separately controlled; feature approval must never imply ESD cutover.
11. Preserve ordinary ShiftProfile Finalize, immutable finalized snapshots, legacy readability, no RBAC, and no local Support identity.
12. Resolve or explicitly accept the six existing NU1701 compatibility warnings before production qualification.

## 18. Verification record

Required verification is:

- complete Debug and Release solution builds;
- complete test suite;
- `git diff --check`;
- unchanged `Program.cs`, startup, production WinForms, and feature configuration;
- no Phase 8.1 reference from production composition;
- no production database selection/access or migration execution;
- no ESD provisioning/cutover;
- no reporting/authentication/Runtime/Event authority switch;
- no RBAC or Support role/profile/login;
- no integration executor or monitoring provider implementation.

Verification completed with these results:

- Debug solution build: succeeded with zero errors and six pre-existing NU1701 warnings.
- Release solution build: succeeded with zero errors and the same six warnings.
- Complete Release test suite: 354 passed, zero failed, zero skipped.
- Focused Phase 8.1 suite: 19 passed.
- `git diff --check`: passed; only pre-existing line-ending notices were emitted for unrelated working-tree files.
- `Program.cs`: unchanged from SHA-256 `33985F732E77AFC7249DDA0174E8BCC58601B5E0B3E22B93E31933F01ACCAA76` and has no Git diff.
- Production WinForms, `UI/Startup`, and application startup foundation: no Git diff and no Phase 8.1 reference.
- Feature state: no production configuration or feature flag changed; no integration executor exists.
- Data/migration: the integration layer has no SQLite or migration-runner dependency; no production database was selected/opened and no migration executed by Phase 8.1.
- Authority: no authentication, reporting, snapshot, Runtime/Event, settings, or export route changed.
- ESD: no provisioning method, target-authoritative state, or cutover path exists in the integration layer.
- Monitoring: `IIntegrationMonitoringSink` remains interface-only with no provider.
- Security: ShiftProfile remains the only target normal identity; no RBAC or Support role/profile/login was introduced.

Regardless of foundation verification, production routing remains legacy-only and actual integration remains a future approved phase.
