# Phase 8.2 - Controlled Pilot Implementation Foundation

Status: **Implemented as an inactive, read-only pilot application layer; legacy production authority remains unchanged**

Date: 2026-08-22

## 1. Executive conclusion

Phase 8.2 implements the first application-level pilot layer above the Phase 8.0 activation controls and Phase 8.1 integration design. It can validate an explicitly selected pilot scope, evaluate the existing activation evidence and feature approval, require rollback readiness, issue a narrowly bound read-only permit, run observation adapters, and return safe comparison evidence for human evaluation.

The implementation does not activate a pilot in production. It does not register a service, alter startup, change a feature flag, replace a WinForms screen, open or mutate a production database, execute a migration, consume vendor authorization, execute a ManagementCredential, cut over ESD authority, replace an authenticated session, or switch reporting or Runtime/Event authority. Every workflow result explicitly retains legacy authority and prohibits production mutation.

The implemented boundary is:

```text
explicit pilot scope
        -> complete Phase 8 activation evidence
        -> feature-specific approval
        -> verified rollback readiness
        -> fail-closed pilot gateway
        -> observation-only permit
        -> legacy observation (authoritative)
        -> target read-only observation
        -> immutable safe comparison evidence
        -> human evaluation in a future UI
        -> stop
```

No Phase 8.2 type is referenced by `Program.cs` or any current production composition path. There is no default pilot activation and no pilot executor.

## 2. Implementation layout

The application foundation is isolated in `Foundation.Application.Pilot`:

- `PilotContracts.cs` defines immutable pilot context, context validation, feature registry, and pilot evidence.
- `PilotGateway.cs` defines the fail-closed gateway and the observation-only execution permit.
- `PilotWorkflowServices.cs` defines authentication, reporting, Runtime/Event, and protected-settings observation services.
- `ExportPilotService.cs` defines isolated read-only export artifact comparison.
- `PilotPresentationAndMonitoring.cs` defines future UI presentation, monitoring hooks, and non-destructive rollback planning.
- `ControlledPilotImplementationFoundationTests.cs` contains test-only observers and the Phase 8.2 behavioral tests.

The namespace depends on application contracts from Phases 7.9, 8.0, and 8.1. It has no WinForms, SQLite connection, repository writer, migration runner, feature-configuration writer, authentication session setter, ESD executor, or telemetry provider dependency.

## 3. Pilot execution context

`PilotExecutionContext` is an immutable, UI-neutral scope description. It contains:

- `PilotId`;
- an explicitly supplied `StationId`;
- a defensive, ordinal, deterministic copy of selected ShiftProfile IDs;
- a defensive, deterministic copy of enabled pilot features;
- activation evidence package ID;
- correlation ID;
- rollback reference;
- creation timestamp in UTC;
- optional expiration timestamp in UTC.

The context does not discover a station, infer a station from a database, accept a default station, or derive scope from the environment. An empty station, empty shift list, empty feature list, unknown feature, `*`, or `all` wildcard fails validation. A context created in the future, with malformed lifetime, or expired at the evaluation instant also fails. Expiration is exclusive: a pilot is expired at or after `ExpiresAtUtc`.

The context exposes `EnabledByDefault`, `ProductionRegistrationAllowed`, and `ProductionMutationAllowed` as constant `false` safety facts. Constructing or validating a context performs no routing action. Collections are copied during construction so later caller mutations cannot widen the pilot.

The model intentionally scopes normal operational identity only by ShiftProfile ID. It contains no role list, permission list, RBAC grant, Support identity, ManagementCredential login, or credential material. Station selection is deployment-neutral: current production can explicitly select Rasht or Ramsar identifiers, but the application contract has no station-name branching and cannot silently select either station.

## 4. Pilot feature registry

`PilotFeatureRegistry` defines exactly five features:

| Pilot feature | Phase 8.1 integration feature | Required approval scope | Risk | Core dependencies |
|---|---|---|---|---|
| `AuthenticationPilot` | Authentication | Authentication workflow activation | High | security persistence readiness, migration readiness, legacy login observation |
| `ReportingPilot` | Snapshot reporting | Snapshot reporting activation | Moderate | immutable snapshot validation and legacy report readability |
| `RuntimeEventPilot` | Runtime projection | Runtime/Event projection activation | Moderate | projection validation and read-only target adapter |
| `ProtectedSettingsPilot` | Protected settings | Protected settings activation | Critical | legacy settings authority and ESD-cutover prohibition |
| `ExportPilot` | Report export | Report export activation | Moderate | immutable snapshot and export artifact validation |

Each definition includes a stable feature ID, dependency IDs, required approval descriptions, risk level, and rollback requirement. Every definition has `RollbackRequired = true` and `EnabledByDefault = false`.

The registry is metadata only. It has no mutable enabled state, configuration binding, feature-flag reader, environment lookup, service resolver, or activation callback. Registering a definition cannot turn on a pilot.

## 5. Fail-closed pilot gateway

`PilotGateway` is the only application service capable of issuing a `PilotExecutionPermit`. The permit constructor is internal; workflow callers cannot obtain a valid public-constructor permit merely by selecting a feature.

The gateway evaluates, in order:

1. context presence and full context validation;
2. known feature and exact inclusion in the context feature set;
3. registry guarantee that the feature is not default-enabled and requires rollback;
4. complete Phase 8.0 activation evidence;
5. exact evidence-package and correlation binding to the context;
6. rollback status `Ready` with no blockers;
7. valid isolated Phase 8.1 pilot boundary for the explicit station and shifts;
8. feature-specific approval through the Phase 8.1 activation coordinator;
9. exact feature, station target scope, database identity, evidence package, correlation, approval scope, and approval lifetime.

Any missing approval, incomplete evidence, binding mismatch, unavailable rollback, unknown feature, out-of-scope feature, expired context, or failed integration decision returns `Blocked` with no permit. A manual-review integration result is preserved as `RequiresManualReview` only when no independent blocker exists. Neither `Blocked` nor `RequiresManualReview` silently falls back, invokes a workflow, or promotes the context.

An allowed result contains a narrowly bound permit with pilot, station, exact ShiftProfile set, feature, evidence, correlation, rollback, approval, context creation time, issue time, and optional expiry. Permit safety fields state that legacy remains authoritative, the target is read-only, production mutation is prohibited, and ESD cutover is prohibited. Workflow services revalidate these bindings and expiry before invoking either adapter, preventing a permit from being reused for another pilot, station, widened ShiftProfile set, feature, evidence package, correlation, lifetime, or rollback context.

The gateway is an evaluator, not a router executor. An `Allowed` result means only that an explicitly invoked read-only observation may proceed.

## 6. Authentication pilot

The authentication pilot flow is:

```text
existing legacy login outcome
        -> authoritative legacy observation
        -> ShiftProfile target observation
        -> success-category comparison
        -> safe pilot evidence
```

`AuthenticationPilotRequest` contains the pilot context, issued permit, and selected ShiftProfile ID. It has no password, credential secret, hash, salt, verifier, login form, or session object. The selected ShiftProfile must be one of the context's explicit ShiftProfile IDs.

`ILegacyAuthenticationPilotObserver` observes the authoritative legacy result. `IShiftProfileAuthenticationPilotObserver` returns only ShiftProfile ID, Station ID, credential version, success/failure, a non-secret result fingerprint, and a safe category. The service rejects an invalid credential version or target observation bound to a different ShiftProfile or station.

The service compares observations but never authenticates a production session itself. `LegacyRemainsAuthoritative` is always true, `ProductionMutationAllowed` is always false, `ReplacesLegacySession` is false, and `RequiresSecondLoginScreen` is false. There is no RBAC, Support identity, ManagementCredential operational login, or automatic replacement of the current login screen.

## 7. Reporting and export pilots

`ReportingPilotService` requires a reporting permit, explicit report scope, and explicit snapshot ID. It then:

1. observes the legacy report, which must remain readable;
2. reads the target snapshot through an observation-only interface;
3. requires the exact snapshot ID and an immutable finalized-snapshot assertion;
4. rejects any recalculation or mutation attempt;
5. validates the export artifact through a read-only validator;
6. rejects an invalid artifact or any artifact mutation attempt;
7. compares the legacy and target result fingerprints;
8. returns safe evidence while leaving the legacy display available and authoritative.

The target adapter cannot be used by this contract to update a snapshot, unlock a report, recalculate a finalized month, or replace the displayed report. A target observation that reports any of those behaviors fails closed.

`ExportPilotService` provides the separately approved `ExportPilot` boundary. It requires an explicit snapshot and format, compares the authoritative legacy artifact with a target artifact, and requires the target to assert that the snapshot is immutable, validation is read-only, and no mutation was attempted. The service does not write an export destination or publish an artifact; future infrastructure may supply isolated artifact observers after separate review.

## 8. Runtime/Event pilot

`RuntimeEventPilotService` accepts an explicit projection scope and a `RuntimeEventPilot` permit. Its legacy observer returns authoritative Runtime and Event fingerprints. Its target observer returns separate Runtime and Event fingerprints plus safety assertions.

The target observation is rejected if any of the following is true:

- the target is not read-only;
- insert was attempted;
- update was attempted;
- delete was attempted;
- cache rebuild was attempted;
- production recalculation was attempted;
- either result fingerprint is missing.

The service contains no table name, connection string, SQLite connection, repository writer, cache writer, or recalculation callback. It only compares returned observation evidence. A difference produces warning evidence for human evaluation; it never changes production Runtime/Event output.

## 9. Protected settings pilot

`ProtectedSettingsPilotService` is observation-only and preserves legacy settings authority. Before an adapter is invoked it rejects any request that asks for:

- settings mutation;
- target settings provisioning;
- ESD authority cutover;
- an implicit or missing settings scope.

For an observation request, the legacy adapter reads the authoritative legacy view and the target adapter evaluates a decision without execution. The target observation is rejected if it reports mutation, provisioning, ESD cutover, vendor-authorization consumption, or ManagementCredential execution.

This phase does not provision target ESD state, issue or verify a vendor request for execution, consume replay state, execute the exactly-once ESD mutation boundary, validate a live ManagementCredential, or alter the legacy ESD setting. Phase 7 security rules remain intact and post-Wizard ESD Adjustment remains impossible through the pilot layer.

## 10. Pilot evidence

`PilotEvidenceRecord` is immutable and includes:

- evidence ID;
- pilot ID;
- feature;
- UTC timestamp;
- correlation ID;
- legacy result fingerprint;
- target result fingerprint;
- comparison severity;
- operator-visible safe message;
- rollback status.

The fingerprints are caller-provided non-secret comparison identifiers; raw workflow values are not copied into evidence. The model excludes passwords, password hashes, salts, credential verifiers, credential secrets, private keys, vendor authorization bytes, recovery codes, database rows, and exception text. `ContainsCredentialMaterial` is a constant false safety assertion.

`PilotEvidenceValidator` requires all safe fields, defined enum values, UTC time, exact pilot and correlation bindings, and membership of the feature in the context. It distinguishes complete, incomplete, and context-blocked evidence. Evidence cannot grant a permit or activate a feature; it is an output for future human review.

## 11. UI-neutral presentation and monitoring

`PilotPresentationModel` provides future UI data for pilot status, comparison outcome, warnings, blocked reasons, evidence state, evidence ID, correlation ID, and safe summary. It always marks legacy authority as retained and offers no production activation. `IPilotPresentationSink` is an interface only; there is no WinForms implementation and no existing form references it.

`IPilotMonitoringHook` is also an interface only. Its safe signals cover authentication differences, report differences, Runtime/Event differences, security failures, and pilot health. Signals carry pilot/evidence/correlation references, severity, safe category, and UTC time. There is no telemetry provider, network dependency, cloud service, background worker, startup registration, or audit persistence in Phase 8.2.

## 12. Pilot rollback boundary

`PilotRollbackCoordinator` produces a plan; it does not modify routing or stored state. A valid request must explicitly request all four operations:

1. disable the pilot;
2. return to legacy;
3. preserve pilot evidence;
4. close the pilot session.

It also requires a rollback reference and UTC request time. A complete request returns an allowed, non-destructive plan stating that legacy authority is restored, evidence is preserved, and the session may be closed. A missing element blocks the plan. `DestructiveActionAllowed` is always false.

Production is already legacy-authoritative in this phase, so “return to legacy” is a control-plane statement for a future pilot host rather than a database or UI rollback. No database restoration, file replacement, transaction reversal, feature-flag write, or automatic rollback is implemented.

## 13. Failure semantics

The pilot foundation fails closed at each boundary:

- invalid, implicit, wildcard, future, or expired context: no permit;
- missing or invalid evidence: no permit;
- wrong feature, station, approval scope, evidence ID, or correlation: no permit;
- missing approval or unavailable rollback: no permit;
- missing, wrong, future, or expired permit: no adapter call;
- out-of-scope ShiftProfile: no authentication observer call;
- requested settings mutation/provisioning/ESD cutover: no settings observer call;
- target mutation, recalculation, cache rebuild, authorization consumption, or privileged execution signal: blocked result;
- adapter exception: safe blocked category without exception text;
- caller cancellation: cancellation is propagated rather than converted to success.

No failure promotes target authority. A target mismatch is evidence, not permission to repair production data. An `Allowed` comparison result still permits no production mutation.

## 14. Tests

`ControlledPilotImplementationFoundationTests` adds 23 focused cases. Coverage includes:

- explicit context validation and defensive collection copies;
- wildcard station and ShiftProfile rejection;
- pilot expiration;
- the exact five-feature registry and default-disabled state;
- approval and rollback blocking;
- unknown and out-of-scope feature rejection;
- successful permits for all five features;
- permit safety invariants and evidence propagation;
- authentication comparison and legacy session preservation;
- absence of credential inputs and secret evidence fields;
- finalized snapshot immutability and recalculation/mutation blocking;
- Runtime/Event read-only enforcement for insert, update, delete, cache, and recalculation boundaries;
- protected-settings mutation, provisioning, ESD cutover, vendor-consumption, and ManagementCredential-execution blocking;
- export snapshot/artifact immutability;
- expired permit rejection before adapter invocation;
- rejection of permit reuse with a widened ShiftProfile scope;
- evidence context binding and rollback state;
- explicit non-destructive rollback planning;
- interface-only presentation and monitoring boundaries;
- absence of a pilot database writer, migration method, RBAC, or Support identity.

Test doubles are local to the test assembly and implement observation interfaces only. They use no database. Phase 8.2 tests do not create, discover, open, copy, migrate, or mutate a production database.

Focused result: **23 passed, 0 failed, 0 skipped**.

Full solution verification is recorded in section 16.

## 15. Known limitations and deliberate exclusions

Phase 8.2 is not deployable pilot composition. The following remain deliberately absent:

- production dependency-injection or startup registration;
- real adapters for legacy login, ShiftProfile authentication, reports, snapshots, Runtime/Event, settings, or exports;
- UI screens or modifications to current WinForms;
- persistence for pilot context, permits, evidence, monitoring, or rollback sessions;
- operator approval UI and identity provisioning;
- a pilot enable/disable executor or mutable feature registry;
- database selection, production connection, migration execution, or schema adoption;
- live authentication session integration;
- production report/export routing;
- target Runtime/Event routing or cache integration;
- protected-settings provisioning or execution;
- ESD authority cutover;
- vendor authorization consumption;
- ManagementCredential execution;
- telemetry or remote services.

Pilot permits are in-process immutable capability records, not cryptographic authorization tokens and not durable leases. A future host must keep permit construction inaccessible across trust boundaries, re-evaluate approval and expiry at each entry point, and persist audit/evidence safely before any broader activation is considered.

The comparison fingerprints supplied by future adapters must be designed so they do not encode credentials or sensitive source data. Phase 8.2 validates presence and binding, not the future adapter's fingerprint algorithm.

## 16. Verification

Verification for this implementation batch includes:

- complete solution Release build;
- focused Phase 8.2 test run;
- complete Release test suite;
- `git diff --check`;
- protected-file and activation-boundary inspection;
- source scan of the pilot namespace for prohibited production dependencies and identities.

The Release build succeeds with no errors. The six NU1701 messages for legacy `OpenTK`, `OpenTK.GLControl`, and `SkiaSharp.Views.WindowsForms` compatibility are pre-existing dependency warnings and are unrelated to the Phase 8.2 files.

The complete Release suite passes **377 tests, 0 failed, 0 skipped**. `git diff --check` reports no whitespace errors; its only output is the repository's existing line-ending conversion notices for unrelated working-tree files.

Final checks confirm:

- `Program.cs` is unchanged from the protected baseline;
- startup and production WinForms are unchanged;
- no pilot or target feature is enabled by default;
- no Phase 8.2 service is registered in production;
- no production database is selected, opened, or mutated;
- no migration is registered or executed;
- no ESD provisioning or authority cutover exists;
- legacy authentication, reporting, Runtime/Event, settings, and export authority remains unchanged;
- ShiftProfile remains the sole modeled normal operational identity;
- no RBAC contract or Support role/profile/login is introduced;
- no telemetry provider or remote dependency is introduced.

## 17. Requirements before any production pilot or activation

Before a future phase may compose even a limited real pilot, it must explicitly provide and validate:

1. an approved pilot host that is not production startup and cannot widen scope;
2. an explicitly selected station and finite ShiftProfile list;
3. a complete, installation-specific Phase 8 activation evidence package;
4. unexpired feature-specific operator approval bound to database, evidence, correlation, station, and feature;
5. verified rollback ownership, backup, and restore evidence;
6. reviewed read-only adapters for each selected workflow;
7. evidence fingerprint designs proven to exclude credentials and sensitive raw data;
8. safe audit-before-observation and local evidence retention policy;
9. human-facing pilot presentation reviewed without replacing existing WinForms;
10. cancellation, timeout, resource, and failure testing against isolated non-production copies;
11. an explicit maintenance and monitoring plan;
12. a separate approval for any future authority switch.

Authentication replacement additionally requires migrated and provisioned ShiftProfile credential persistence, recovery procedures, and session integration review. Reporting authority requires snapshot parity, immutable read routing, export parity, and legacy readability. Runtime/Event authority requires long-running parity evidence and proof that recalculation does not alter finalized evidence. Protected settings require the Phase 7 management proof, vendor authorization, audit, replay, and exactly-once persistence chain to be production-ready. ESD cutover must remain a separate, explicit future operation and cannot be implied by general feature activation.

Phase 8.2 therefore completes the controlled pilot implementation foundation but does not make any installation ready or approved for production activation.
