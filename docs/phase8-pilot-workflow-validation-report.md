# Phase 8.8 - Controlled Pilot Workflow Validation Foundation

Status: **Implemented as an inactive, explicitly approved, read-only validation boundary; legacy remains authoritative**

Date: 2026-08-22

## 1. Outcome and phase boundary

Phase 8.8 adds a controlled validation flow for immutable observations that have already been selected by an approved pilot composition. It compares one declared legacy observation with one declared target observation, creates a minimal immutable evidence record, and stops.

```text
explicit PilotWorkflowValidationContext
        + one legacy read-only observer
        + one target read-only observer
        -> PilotWorkflowValidationCoordinator.ValidateAsync
        -> deterministic comparison
        -> immutable PilotValidationEvidence
        -> stop
```

The coordinator is not registered or invoked by production. It does not call the Phase 8.3 pilot host, execute a production workflow, discover services, read a database, perform a migration, open a form, navigate, retry, poll, schedule work, mutate an artifact, or change authority. The legacy observation is explicitly marked authoritative. A difference is evidence, not a correction instruction.

Implementation is isolated in `Application/Pilot/Validation`:

- `PilotWorkflowValidationContracts.cs` defines immutable context, scope, observation, comparison, evidence, result, safety, and workflow-specific interfaces;
- `PilotWorkflowObservers.cs` supplies explicit in-memory read-only observers for the five validation boundaries;
- `PilotWorkflowValidationCoordinator.cs` supplies deterministic comparison, evidence construction, context validation, and the fail-closed lifecycle;
- `PilotWorkflowValidationTests.cs` validates behavior and production boundaries.

No Phase 8.8 type is referenced by `Program.cs`, startup, production forms, or navigation.

## 2. Validation context and explicit scope

`PilotWorkflowValidationContext` is constructed explicitly with a validation ID, pilot ID, correlation ID, composition ID, selected workflow, UTC validation timestamp, Phase 8.7 capability evidence, immutable validation scope, and explicit approval flag. It has no ambient configuration, current-user lookup, service locator, automatic workflow discovery, production fallback, or authority-switch option.

`PilotValidationScope` binds exactly one workflow to one legacy observer ID and one target observer ID. Its subject identifiers are defensively copied, deduplicated, sorted, and exposed through a read-only collection. The scope must positively request legacy observation, target observation, and comparison. Empty subjects, unsafe identifiers, incomplete flags, workflow mismatches, or non-UTC timing fail closed.

All IDs use a restricted 128-character allow-list. Paths, control characters, SQL-like terms, exception-like terms, credential terms, hash terms, and oversized identifiers cannot enter a valid context. Failures return fixed reason codes and do not reproduce rejected input.

Capability evidence is the Phase 8.7 read-only model. It must match the context pilot and correlation IDs, predate the validation time, use UTC, and contain exactly `pilot.view`, `comparison.view`, and `evidence.view`. This is capability evidence only. It is not RBAC, a permission grant, or an authorization decision.

## 3. Workflow observation contracts

`IPilotWorkflowObserver` exposes only a descriptor and `ObserveAsync`. The method accepts the immutable validation context and a cancellation token and returns an immutable `PilotWorkflowObservationResult`. It has no execute, save, update, delete, login, session, export-write, settings-write, repository, connection, transaction, migration, or UI member.

Five marker interfaces prevent an observer for one workflow from being silently reused for another:

- `IAuthenticationPilotValidationObserver` observes legacy authentication evidence and a ShiftProfile-oriented target boundary without passwords, login replacement, sessions, RBAC, or Support identity;
- `IReportingPilotValidationObserver` observes legacy report and immutable snapshot evidence without recalculation, finalization changes, or export mutation;
- `IRuntimeEventPilotValidationObserver` observes legacy runtime/event and target projection evidence without event writes or recalculation;
- `IProtectedSettingsPilotValidationObserver` observes legacy settings and protected-setting evaluation without ESD mutation, credentials, provisioning, or persistence;
- `IExportPilotValidationObserver` observes deterministic artifact metadata, snapshot source, and checksum reference evidence without artifact mutation or authority change.

The concrete observers retain an explicitly supplied immutable result and return it when called. They do not fetch or derive data. A future approved adapter may implement a workflow marker, but the coordinator will accept it only when its descriptor declares the exact workflow, exact boundary, expected observer ID, availability, and the complete safe observation profile.

`PilotObservationSafetyProfile` makes prohibited capabilities testable. A valid observer must be read-only and must declare no production execution, password handling, session creation, recalculation, event or settings mutation, provisioning, credential execution, artifact mutation, authority change, database access, RBAC creation, or Support identity.

## 4. Observation and comparison model

An observation contains only workflow, boundary, availability status, deterministic fingerprint, safe evidence reference, UTC observation time, and allow-listed comparison metadata. It explicitly contains no raw source content or credential material. The coordinator accepts only available observations for the selected workflow and expected boundary. Fingerprints and evidence references must be opaque safe identifiers; raw SQL, paths, exception text, and secret material are rejected.

`DeterministicPilotWorkflowObservationComparer` performs an ordinal comparison of the two fingerprints. Equal fingerprints produce `Match` with severity `None`. Unequal fingerprints produce `Difference` with severity `Warning`. It does not inspect production state, recalculate values, correct the target, alter the legacy result, or invoke another component.

`PilotWorkflowComparisonResult` preserves both opaque fingerprints for the immediate comparison result, marks legacy as authoritative, and permanently reports that it neither automatically corrects a difference nor switches authority. Only `Match` and `Difference` are accepted as completed comparison classifications. Unavailable or failed inputs stop earlier and create no evidence.

This foundation defines comparison mechanics, not domain fingerprint algorithms. Authentication, reporting, runtime/event, protected-settings, and export adapters must later demonstrate deterministic, non-secret, non-database-content fingerprint construction before approval.

## 5. Immutable evidence model

`PilotValidationEvidence` contains the validation ID, pilot ID, workflow, UTC timestamp, result status, comparison status, severity, correlation ID, and safe evidence reference. Every property is get-only. It contains no password, secret, private key, hash payload, observation fingerprint, raw database content, SQL, exception, stack trace, mutable collection, callback, or authority grant.

The evidence factory maps a match to `Completed` and a difference to `DifferenceDetected`. The coordinator validates every evidence identity and status field against the approved context and comparison before returning it. A throwing factory, null evidence, mismatched evidence, unsafe reference, or incorrect status is discarded and reported with a fixed failure code. Evidence creation never writes a file or database; persistence remains outside this phase.

## 6. Coordinator and lifecycle

`PilotWorkflowValidationCoordinator` receives its observers, comparer, and evidence factory through its constructor. There is no service provider, reflection discovery, global registry, startup hook, or default dependency. Duplicate observer keys, missing comparer, missing factory, throwing descriptors, or missing selected observers become fixed configuration failures.

The lifecycle is:

1. `Create`: explicit construction leaves the coordinator in `Created`; no observer is called.
2. `Validate`: one explicit call validates context and dependencies, transitions to `Validating`, observes legacy and target sequentially, compares, and creates evidence.
3. `Complete`: a match or difference transitions to `Completed` and returns immutable results.
4. `Fail`: invalid context or any dependency failure transitions to `Failed` with a fixed safe reason code and no evidence.
5. `Dispose`: cancellation is requested, resources are released, and the permanent state becomes `Disposed`.

Validation is single-attempt. A second call does not repeat observations. There is no retry loop, timer, scheduler, polling method, automatic refresh, or background task. External cancellation and disposal during observation are contained. Disposal is idempotent. Calls after disposal return a safe failed result rather than throwing.

## 7. Failure isolation and reason codes

Observer, comparer, and evidence-factory calls each have an independent exception boundary. Exceptions are discarded. No exception message, stack trace, rejected identifier, or dependency detail enters a result. Representative codes include `validation-context-required`, `validation-approval-required`, `validation-scope-invalid`, `validation-observer-unsafe`, `validation-observer-failed`, `validation-comparison-failed`, `validation-evidence-creation-failed`, `validation-canceled`, and `validation-coordinator-disposed`.

Failure never calls a production fallback. It does not invoke the other pilot host, retry a dependency, open the legacy workflow, mutate a setting, write evidence, or switch authority. A partial legacy observation may be retained in the in-memory failure result for diagnosis, but no evidence is issued unless both observations, comparison, and evidence pass all invariants.

## 8. Test coverage and verification

The Phase 8.8 suite adds 23 focused cases. It covers immutable contracts and defensive scope copying; explicit approval and exact capabilities; all five typed workflow observers; matching and different fingerprints; legacy authority and severity mapping; evidence shape; hostile paths, SQL-like values, exception-like values, credentials, and control characters; unsafe observer rejection; observer, comparison, and evidence failures; caller cancellation and disposal during observation; create, validate, complete, fail, dispose, repeated validation, and validation after disposal; single-call behavior; and absence of retries, timers, polling, schedulers, service resolution, database, migrations, UI, host execution, and activation methods.

Boundary tests pin the established `Program.cs` hash and scan `Program.cs`, startup, and production forms for Phase 8.8 references. Reflection scans the validation namespace for database, repository, migration, WinForms, production UI, pilot-host, and execution-coordinator dependencies. Source scans verify the absence of SQLite, migration runner, `Task.Run`, timers, periodic timers, and service locators.

Verification completed with the entire Release solution building successfully with zero errors. The six existing NU1701 compatibility warnings for OpenTK, OpenTK.GLControl, and SkiaSharp.Views.WindowsForms remain unchanged and are outside this phase. NuGet reports no known vulnerable packages; its deprecation scan identifies the existing xUnit 2.9.3 test dependency as legacy and suggests xUnit v3. The focused Phase 8.8 tests and the complete test suite pass. `git diff --check` passes. `Program.cs`, startup, production WinForms, and navigation remain unchanged by Phase 8.8. No production database, migration, ESD cutover, authority switch, RBAC implementation, or Support identity was introduced or executed.

## 9. Limitations and remaining activation requirements

This phase is validation infrastructure, not a live pilot. The supplied observers contain only preconstructed immutable results. There is no production composition registration, route, menu, shortcut, UI command, database read, evidence store, workflow adapter, operator approval screen, or real authentication/reporting/runtime/settings/export invocation.

Before any real pilot, a separately approved phase must define and review the domain fingerprint specification for each workflow; prove that adapters observe only approved immutable sources; establish provenance, retention, integrity, and access rules for evidence; define expiration and revocation of composition and validation approvals; add representative Rasht/Ramsar fixtures; perform deterministic golden-vector and load validation; complete privacy and threat review; establish operator runbooks and rollback/stop procedures; validate localization, DPI, and accessibility in the actual surface; and obtain explicit security, data-owner, operations, and product approval.

Real activation would also require a separately authorized composition root and navigation decision. That work must preserve legacy authority until an independently approved cutover phase. Phase 8.8 grants no such authority and provides no activation path.
