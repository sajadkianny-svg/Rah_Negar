# Phase 8.3 - Controlled Pilot Host and Read-Only Adapter Implementation

Status: **Implemented as an inactive, explicitly constructed read-only host; legacy authority remains unchanged**

Date: 2026-08-22

## 1. Executive conclusion

Phase 8.3 provides the first practical host over the Phase 8.2 pilot contracts. An explicitly constructed `PilotExecutionCoordinator` can accept a previously approved `PilotExecutionContext` and `PilotExecutionPermit`, select one configured workflow, invoke a legacy observation adapter followed by a target read-only adapter, retain safe comparison evidence, and return an immutable UI-neutral result.

The implementation connects to real application behavior only through read boundaries:

- current `AppSession.IsLoggedIn` state can be observed without calling login or logout;
- finalized report snapshots are read through `IFinalizedReportReader`;
- snapshot export eligibility is evaluated by `IReportExportValidator` without rendering or writing an artifact;
- existing Phase 4 Runtime shadow and Event comparison result contracts are consumed as read-only evidence;
- protected settings are read through `IProtectedSettingsReader`;
- protected-settings target decisions are evaluated with the Phase 8.1 legacy-authoritative policy.

Legacy reporting and Runtime/Event production services still expose database-oriented/raw-data paths. Phase 8.3 deliberately does not call those paths or open the application's default SQLite connection. Instead, an isolated future host must explicitly provide safe read models that convert already retrieved legacy results into non-secret section or projection fingerprints. This preserves the requirement against hidden database selection while creating a concrete adapter point around existing behavior.

This phase performs no production composition or cutover. It does not modify `Program.cs`, startup, WinForms, feature flags, routes, sessions, finalized snapshots, settings, ESD state, migration history, or a database. There is no pilot host registration, automatic execution, database locator, migration executor, feature executor, UI implementation, or authority switch.

The implemented runtime path is:

```text
explicit caller
        -> PilotExecutionContext
        -> Phase 8.2 permit validation
        -> exact feature/input routing
        -> legacy read-only observation (authoritative)
        -> target read-only observation
        -> comparison evidence
        -> immutable PilotExecutionResult
        -> future presenter contract
        -> stop
```

## 2. Implementation layout

Phase 8.3 is isolated under `Foundation.Application.Pilot.Hosting`:

- `PilotHostContracts.cs` defines workflow inputs, adapter descriptors/evidence, normalized observations, comparison and execution results, host/executor contracts, and the future presenter boundary.
- `PilotExecutionCoordinator.cs` implements explicit validation, exact workflow routing, failure isolation, safe result construction, and evidence propagation.
- `PilotWorkflowExecutors.cs` adapts the five Phase 8.2 workflow services into the normalized host result model.
- `ReadOnlyPilotAdapters.cs` implements concrete legacy and target read adapters.
- `ControlledPilotHostAdapterTests.cs` contains focused host, adapter, failure, and non-activation tests.

The host namespace contains no `Microsoft.Data.Sqlite` dependency, connection string, database path, repository writer, transaction, migration runner, finalization service, settings writer, ESD executor, session setter, feature configuration writer, or WinForms type.

## 3. Pilot host runtime boundary

`IPilotHost` exposes one method: `ExecuteAsync(PilotHostRequest, CancellationToken)`. A request must explicitly carry:

- the immutable Phase 8.2 context;
- a Phase 8.2 permit;
- one known `PilotFeature`;
- an exact typed input for that feature.

The coordinator constructor accepts an injected clock and an explicit finite set of workflow executors. It performs no assembly scanning, dependency-injection lookup, environment lookup, path discovery, or default executor creation. Duplicate feature executors, null executors, and unknown feature declarations are rejected at construction. A host may be constructed with no executors, but then every workflow is blocked as unconfigured.

The host advertises four invariant facts:

- `AutomaticallyRuns = false`;
- `RegisteredInProductionStartup = false`;
- `SelectsDatabase = false`;
- `ActivatesFeatures = false`.

These are descriptive safety properties, not mutable flags. There is no `Start`, `Enable`, `Register`, or background scheduling API.

At execution, the host validates the complete context at the injected UTC time, requires a known feature, requires an input, finds only the exact configured feature executor, requires the executor's declared input type to equal the runtime input type, and revalidates every Phase 8.2 permit binding. That revalidation covers pilot ID, station, exact ShiftProfile set, feature, evidence package, correlation, rollback reference, context creation time, context expiration, issue time, legacy authority, target read-only status, mutation prohibition, and ESD-cutover prohibition.

An invalid request returns `Blocked` without invoking an executor. Unknown features do not fall back to another route. An input for one feature cannot be used with another feature's executor. Constructing the host does not execute anything.

## 4. Adapter architecture and metadata

The existing Phase 8.2 observer interfaces remain the narrow workflow seams. Phase 8.3 implementations add `IPilotAdapterDescriptorProvider`, which exposes immutable metadata:

- adapter ID;
- adapter implementation version;
- source/read-model version;
- read-only assertion;
- legacy-authority preservation assertion.

Every workflow executor requires both adapter safety assertions to be true. An observer that lacks a descriptor, lacks version metadata, claims write capability, or does not preserve legacy authority is rejected at executor construction. This prevents an arbitrary Phase 8.2 test observer or production writer from being silently installed as a Phase 8.3 host adapter.

Adapter outputs remain the immutable Phase 8.2 observation records. Executors normalize them into `PilotObservationResult`, containing only a result fingerprint, safe status, and `PilotAdapterEvidenceMetadata`. The host independently rejects an observation unless its fingerprint is exactly a 64-character hexadecimal value, its category is a bounded identifier rather than free-form text, its timestamp is UTC, its metadata is complete, and both read-only/legacy-authority assertions are true. An allowed workflow must also contain valid legacy, target, and context-bound Phase 8.2 evidence. The host result never receives raw report rows, event rows, settings dictionaries, snapshots, rendered artifact bytes, passwords, verifiers, salts, private keys, or database connections.

`PilotSafeFingerprint` builds deterministic SHA-256 comparison identifiers from explicit UTF-8, culture-invariant, length-prefixed safe fields. The fingerprint helper is internal. Callers do not submit credentials or arbitrary raw rows to it. Fingerprints are evidence correlation values, not authentication hashes.

## 5. Workflow execution and result model

Five typed inputs are supported:

| Feature | Host input | Required executor |
|---|---|---|
| Authentication | selected ShiftProfile ID | `AuthenticationPilotWorkflowExecutor` |
| Reporting | report scope and snapshot ID | `ReportingPilotWorkflowExecutor` |
| Runtime/Event | projection scope | `RuntimeEventPilotWorkflowExecutor` |
| Protected settings | settings scope and explicit prohibited-operation indicators | `ProtectedSettingsPilotWorkflowExecutor` |
| Export | snapshot ID and export format | `ExportPilotWorkflowExecutor` |

Each executor invokes its Phase 8.2 service. Consequently, legacy observation occurs first, target observation occurs second, and comparison evidence is created by the already-tested Phase 8.2 policy. The executor adds safe adapter/version metadata and returns a normalized internal execution record to the host.

`PilotExecutionResult` is immutable and includes:

- pilot ID;
- feature;
- status;
- normalized legacy result;
- normalized target result, when available;
- comparison result and severity;
- evidence ID, when comparison completed;
- correlation ID;
- UTC start and completion timestamps;
- safe blocked reasons.

Statuses are:

- `Completed` for a successful match;
- `CompletedWithDifference` for successful comparison evidence that differs;
- `Blocked` for validation, safety, or adapter-invariant rejection;
- `TargetFailed` when legacy observation was retained but target observation failed;
- `Failed` for an unexpected host/executor failure.

Every result exposes `LegacyAuthorityPreserved = true`, `ProductionMutationAllowed = false`, and `AuthoritySwitchPerformed = false`. A successful target match is evidence only. A mismatch is evidence only. Neither status promotes routing or alters the legacy result.

`PilotComparisonResult` defensively copies, de-duplicates, and ordinally sorts safe difference codes. If Phase 8.2 evidence exists, the host carries its safe operator message and severity. If target observation fails, the comparison states only that the target observation failed and legacy authority was preserved. Exception messages are never returned.

## 6. Authentication adapters

`AppSessionAuthenticationStateReader` is the direct legacy connection. It reads `AppSession.IsLoggedIn` and exposes a legacy source version. It has no `Login`, `Logout`, password, form, or session-creation method. `LegacyAuthenticationObservationAdapter` converts that boolean into a safe category and fingerprint.

The target boundary is `IShiftProfileAuthenticationReadModel`. It accepts only station ID and ShiftProfile ID and returns:

- success/failure;
- ShiftProfile ID;
- station ID;
- credential version number;
- safe category;
- source version.

The contract deliberately has no password, password hash, salt, credential verifier, credential record, session factory, or login replacement. `ShiftProfileAuthenticationObservationAdapter` validates no credentials and creates no session; it only translates an already safe target observation into Phase 8.2 evidence.

There is currently no production target authenticator suitable for this boundary. The inactive security persistence repository returns credential material and therefore is intentionally not used by this read-only pilot adapter. A future phase must provide a reviewed authentication observation service that performs credential handling within its own protected boundary and emits only this safe read model. Phase 8.3 does not weaken the separation to claim a live authentication connection.

ShiftProfile remains the only normal target operational identity. The host defines no role, permissions, RBAC, Support identity, ManagementCredential login, second login screen, or session replacement.

## 7. Reporting snapshot adapter

`SnapshotReportObservationAdapter` directly consumes `IFinalizedReportReader` and `IReportExportValidator` from the Phase 5 target architecture. For reporting observation it:

1. loads only the explicitly supplied snapshot ID;
2. requires `FoundValid` and a non-null finalized snapshot;
3. requires exact snapshot ID and station binding;
4. treats the finalized snapshot as immutable;
5. performs no recalculation;
6. performs no snapshot or lock mutation;
7. creates a structural section fingerprint;
8. separately validates export eligibility with the pure export validator.

The structural fingerprint covers identity, operational-summary count, daily-summary count, Runtime-summary count, Event-summary and Event-log counts, service-summary count, extreme-date-summary count, and the snapshot's existing integrity checksum. Deterministic section ordering allows an explicitly supplied legacy report read model to create comparable evidence without passing raw rows to the host.

`ILegacyReportReadModel` is the safe legacy bridge. It returns readability, a dictionary of named section fingerprints, source version, and safe category. `LegacyReportObservationAdapter` deterministically combines those sections. It has no database dependency. A future isolated composition may wrap the result of current legacy report retrieval, but it must do so after retrieval and before UI presentation; it may not let the pilot host discover or open the production database.

The reporting executor rejects target read failure, identity mismatch, non-immutable assertion, recalculation, mutation, invalid export eligibility, or export mutation. It cannot finalize, unlock, update, or delete a report.

## 8. Runtime/Event adapters

`ILegacyRuntimeEventReadModel` supplies only authoritative legacy Runtime and Event fingerprints, a source version, and safe category for an explicit station and projection scope. It provides no connection, row collection, mutation method, cache method, or recalculation callback.

`IRuntimeEventTargetReadModel` supplies the existing `RuntimeShadowExecutionResult` and `EventComparisonResult` contracts from Phase 4. This is the concrete target architecture connection. `TargetRuntimeEventObservationAdapter` accepts only usable shadow states (`Match` or `DifferenceDetected`) with evidence, then derives safe Runtime and Event fingerprints from:

- station/unit/period identity;
- shadow status;
- calculation version and source version;
- Event difference category;
- legacy/target Event counts;
- legacy/target final states;
- ordered safe Event difference codes.

The returned target observation asserts read-only execution and hard-codes insert, update, delete, cache rebuild, and recalculation attempts to false because the source contract exposes only completed shadow/comparison results. Any unusable shadow state is blocked by the Phase 8.2 invariant.

The adapter does not run `RuntimeShadowRunner` against a production source. A future source may invoke that runner only with its existing `IsReadOnly = true` and `IsProductionSource = false` copy boundary, then provide the result to this adapter. No cache rebuild or production recalculation is introduced.

## 9. Protected-settings adapters

`LegacyProtectedSettingsObservationAdapter` wraps the existing `IProtectedSettingsReader`. It validates station identity and creates a fingerprint from:

- station ID;
- ESD enabled state;
- culture-invariant ESD hours;
- sorted display-setting keys.

Display-setting values are not returned to the host and are not included in the fingerprint. This avoids accidentally carrying a sensitive value from an arbitrary future settings dictionary.

`TargetProtectedSettingsDecisionAdapter` uses `ProtectedSettingsIntegrationPolicy` from Phase 8.1 in `ProtectedSettingsShadow` mode. It constructs a read-only, mutation-prohibited, legacy-authoritative routing decision and evaluates only the requested scope and prohibited-operation indicators.

The host input and Phase 8.2 service reject settings mutation, target provisioning, and ESD cutover before either settings observer runs. The target adapter exposes no vendor authorization, replay consumption, ManagementCredential execution, settings repository, or ESD executor. Its returned observation always records vendor-authorization consumption and ManagementCredential execution as false.

The existing production settings service is not used because it discovers the default SQLite path, includes write methods, and is coupled to legacy UI behavior. Using it would violate this phase's explicit-selection and read-only boundaries. `IProtectedSettingsReader` is the safe connection point for a future explicitly constructed host.

## 10. Export adapters

`SnapshotExportObservationAdapter` reads the exact finalized snapshot through `IFinalizedReportReader`, validates identity and station, calls the pure `IReportExportValidator`, and fingerprints the validated snapshot structure, requested format, and validation status. It does not call `IReportExporter`, PDF/Excel renderers, file APIs, or an output path. Therefore it validates future export behavior without creating or overwriting an artifact.

`ILegacyExportReadModel` supplies only an already safe legacy artifact fingerprint, readability, version, and category. `LegacyExportObservationAdapter` carries that into the comparison. No artifact bytes, file path, database row, or UI object enters `PilotExecutionResult`.

Export failure or mismatch affects evidence only. The current legacy export remains authoritative and available outside this host.

## 11. Failure isolation

The layered failure behavior is intentionally closed:

- invalid context, permit, feature, or input: executor is not invoked;
- unconfigured feature: no fallback and no adapter call;
- legacy adapter failure: safe blocked result, no target authority;
- target adapter failure after a legacy observation: legacy result is retained, target result is absent, status is `TargetFailed`;
- target mismatch: `CompletedWithDifference`, evidence retained, no production effect;
- target safety assertion failure: `Blocked`, legacy authority retained;
- unexpected host/executor exception: `Failed` with `pilot-workflow-failed` only;
- explicit cancellation: propagated to the caller.

Phase 8.2 services catch non-cancellation adapter failures and emit fixed safe categories. The host does not use exception text, nested messages, stack traces, raw error payloads, or adapter-provided exception details in UI-facing results.

Target failure cannot call a legacy mutation, replace a session, overwrite a displayed report, update a cache, change a setting, consume an authorization, or switch a route. The host possesses none of those capabilities.

## 12. UI preparation boundary

`IPilotHostPresenter` is an interface only. `PilotHostPresentation` supports future rendering of:

- pilot and feature;
- execution status;
- comparison summary and severity;
- evidence ID/state;
- warnings;
- blocked reasons;
- correlation ID.

There is no presenter implementation and no WinForms reference in the host namespace. Existing production forms are unchanged. A future UI adapter must remain a consumer of immutable results; it must not turn an evidence result into feature activation or target authority.

## 13. Tests

`ControlledPilotHostAdapterTests` adds 15 focused tests covering:

- host inactivity and absence of hidden selection/activation;
- missing and wrong-feature permit rejection before routing;
- exact workflow/input routing;
- legacy and target adapter execution;
- current AppSession read-only observation;
- ShiftProfile authentication observation without password/session creation;
- target authentication failure isolation with retained legacy evidence;
- finalized snapshot observation, structural comparison, and export validation;
- missing/nonfinalized snapshot rejection;
- Phase 4 Runtime/Event observation;
- protected-settings read and target-policy evaluation;
- settings mutation, provisioning, and ESD-cutover suppression before reads;
- export validation without render/write;
- required read-only adapter metadata;
- result secret/raw-row exclusion and interface-only presenter;
- absence of database writers, migration execution, RBAC, or Support identity.

The report/export tests use an in-memory finalized snapshot object produced by the existing synthetic reporting fixture and a test reader. They do not open a database. Runtime/Event tests use immutable Phase 4 result objects. Settings and authentication tests use safe in-memory read models. No Phase 8.3 test opens, migrates, or mutates a production database.

The focused Phase 8.3 rerun passes all 15 tests. The complete Release suite, rerun during Phase 8.4 after Windows Application Control accepted the deterministic assembly, passes all 410 tests. The earlier assembly-load policy block is therefore cleared and did not represent a product or assertion failure.

## 14. Limitations and next activation requirements

Phase 8.3 is an application host foundation, not a production pilot deployment. Deliberate exclusions include:

- startup/dependency-injection registration;
- pilot configuration persistence or default feature state;
- a production database target or connection factory;
- live legacy report/Runtime read-model composition;
- live target ShiftProfile authentication implementation;
- UI forms or modifications;
- persistent evidence/audit storage;
- monitoring provider;
- migration, adoption, or schema changes;
- report finalization, unlock, recalculation, or write routing;
- Runtime/Event writes, cache rebuild, or production recalculation;
- settings writes or target provisioning;
- vendor authorization or ManagementCredential execution;
- ESD authority cutover;
- production authority switching.

Before a real isolated pilot host can be approved, a future phase must provide:

1. an explicit non-startup composition entry point and operator invocation boundary;
2. installation-specific Phase 8 evidence, approval, permit, expiration, and rollback evidence;
3. safe legacy report and Runtime/Event read models over an explicitly selected, reviewed source;
4. a protected ShiftProfile authentication observation service that handles credentials internally and returns no credential material;
5. a reviewed `IProtectedSettingsReader` implementation that is demonstrably read-only and explicitly scoped;
6. read-only finalized snapshot infrastructure bound to an isolated pilot target;
7. adapter timeouts, cancellation, resource limits, and concurrency policy;
8. durable safe evidence/audit storage with allow-listing;
9. a reviewed UI presenter that cannot activate a feature or replace legacy output;
10. human comparison acceptance criteria and rollback procedures;
11. operational monitoring and maintenance ownership;
12. a separate future approval for every authority switch.

Any live pilot composition must prove that the legacy observation itself does not introduce a second query with side effects or affect timing-sensitive production behavior. It must also prove that all target sources are isolated or genuinely read-only. These are deployment/infrastructure facts and are not claimed by this application-only phase.

Authentication, reporting, Runtime/Event, export, protected settings, migration, and ESD authority remain separate activation tracks. Approval for a read-only pilot observation is never approval for production routing or ESD cutover.

## 15. Verification

Verification for the Phase 8.3 batch includes:

- complete solution Release build;
- focused Phase 8.3 test run;
- complete Release test suite;
- `git diff --check`;
- protected `Program.cs`, startup, and WinForms checks;
- source scans for production registration, SQLite/write APIs, migration execution, ESD execution, RBAC, and Support identity in the host namespace.

The Release build succeeds with no errors. The repository's six existing NU1701 compatibility warnings for legacy `OpenTK`, `OpenTK.GLControl`, and `SkiaSharp.Views.WindowsForms` packages remain unchanged; Phase 8.3 adds or upgrades no dependency.

The focused Phase 8.3 command passes 15 of 15 tests. The complete Release suite subsequently passes 410 of 410 tests. Windows Application Control initially delayed assembly loading, but a later unmodified deterministic Release assembly was accepted without disabling or bypassing policy. The successful reruns clear that environmental verification blocker.

`git diff --check` reports no whitespace errors (only existing Git line-ending conversion notices). The protected `Program.cs` SHA-256 remains `33985F732E77AFC7249DDA0174E8BCC58601B5E0B3E22B93E31933F01ACCAA76`; it has no tracked diff and no pilot-host reference. Production WinForms files have no tracked diff. Static scans of the Phase 8.3 namespace find no SQLite connection/write API, SQL mutation, migration runner, RBAC type, or Support role/profile/login. All new tests use synthetic or in-memory data and contain no production database path.

Completed protected-boundary verification confirms:

- `Program.cs` matches its protected baseline and has no pilot reference;
- startup and production WinForms have no tracked changes;
- no host or adapter is registered or run automatically;
- no default pilot route or feature is enabled;
- no production database path is selected or opened;
- no database mutation or migration is executed;
- no finalized snapshot, report lock, Runtime/Event state, cache, session, or setting is mutated;
- no ESD provisioning, vendor authorization execution, ManagementCredential execution, or authority cutover occurs;
- legacy remains authoritative for every result and failure;
- no RBAC or Support role/profile/login is introduced.

Phase 8.3 stops at immutable results for future UI consumption. It does not make a production pilot active or authorize a production switch.
