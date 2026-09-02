# Phase 9.2 — Controlled Production Pilot Operational Readiness

Status: **Implemented for explicitly invoked, deterministic, read-only operational rehearsal; manual validation and live-environment qualification remain pending**

Date: 2026-09-02

## 1. Outcome and authority boundary

Phase 9.2 makes the controlled pilot executable as an isolated operational rehearsal. It is no longer a contract-only placeholder: an explicit caller can construct an approved rehearsal context, run concrete preflight checks, execute five deterministic legacy/target observation comparisons, evaluate monitoring and stop criteria, record an operator decision, and receive a checksummed immutable evidence bundle in memory.

The implemented flow is:

```text
explicit approved operational context
    -> preflight against supplied release / Phase 9.0 / Phase 9.1 / rollback evidence
    -> explicit approve
    -> explicit start of one in-memory rehearsal session
    -> five deterministic read-only observations
    -> versioned SHA-256 fingerprints
    -> legacy/target comparisons
    -> deterministic monitoring evidence
    -> stop-policy evaluation
    -> explicit operator complete / stop / rollback-request decision
    -> immutable checksummed evidence bundle
    -> in-memory evidence destination
    -> terminal stop
```

Legacy remains authoritative throughout. `Completed` means only that the rehearsal and its evidence recording completed. `Stopped` stops only the rehearsal session. Neither outcome grants activation permission, changes routing, performs deployment, authenticates a user, creates an application session, changes a protected setting, writes an event, recalculates or finalizes a snapshot, creates an export artifact, runs a migration, executes ESD cutover, or changes production authority.

No Phase 9.2 code is registered in `Program.cs`, startup, navigation, or a production form. No production UI was changed. The only concrete evidence destination supplied by this phase is in-memory.

## 2. Concrete implementation inventory

Implementation is isolated under `Application/Pilot/Operational`:

- `ControlledPilotOperationalContracts.cs` implements the immutable rehearsal context, explicit release and Phase 9.1 prerequisite evidence, preflight result, preflight evaluator, evidence-destination abstraction, and in-memory destination.
- `ControlledPilotOperationalFingerprints.cs` implements the five safe observation models, the generic versioned fingerprint contract, canonical field writer, SHA-256 specifications, collection normalization, identifier safety, and path/checksum safety.
- `ControlledPilotOperationalObservers.cs` implements typed workflow observers, deterministic legacy/target comparison, the target Runtime/Event observation source backed by the existing `RuntimeCalculator`, and metadata-only export observation construction.
- `ControlledPilotOperationalEvidence.cs` implements the runbook, operator decisions, stop evaluation, monitoring evidence, comparison records, immutable evidence bundle, and deterministic bundle checksum.
- `ControlledPilotOperationalRehearsalCoordinator.cs` implements the explicit single-attempt lifecycle, failure isolation, cancellation, terminal evidence persistence, and rehearsal-only stop behavior.

Test-only implementation is isolated under `Rah_Negar.Tests/Pilot`:

- `ControlledPilotOperationalFixtures.cs` defines deterministic Rasht and Ramsar rehearsal fixtures and complete Phase 9.0/9.1/rollback/release evidence.
- `ControlledPilotOperationalReadinessTests.cs` contains the Phase 9.2 golden-vector, workflow, lifecycle, safety, failure, checksum, and end-to-end tests.

There are no changes to project package versions, target frameworks, schemas, migrations, database services, production services, production forms, startup, or `Program.cs`.

## 3. Operational rehearsal context

`ControlledPilotOperationalRehearsalContext` is explicitly constructed and get-only. It contains:

- `RehearsalId`, `PilotId`, `SessionId`, `CorrelationId`, and `ReleaseId`;
- `StationScope`, restricted to the existing Rasht/Ramsar read-only pilot scope enum;
- UTC `StartUtc` and `EndUtc` boundaries;
- the selected workflow set;
- the opaque operator reference;
- the Phase 9 preparation evidence reference;
- the rollback evidence reference;
- explicit approval.

The selected workflows are copied, deduplicated, sorted, and exposed read-only. Identifiers pass a narrow allow-list and unsafe values become unusable safe sentinels that preflight rejects without echoing the input. Context construction rejects non-UTC boundaries, empty/reversed windows, and windows longer than eight hours. It performs no environment, current-user, machine, registry, file, database, configuration, or service lookup.

## 4. Operational preflight

`ControlledPilotOperationalPreflight` performs a synchronous, side-effect-free evaluation and returns exactly `Ready`, `Blocked`, or `RequiresReview`, plus fixed safe reason codes.

Readiness requires all of the following:

1. An explicitly approved, currently active, structurally valid rehearsal context.
2. Explicit release evidence for branch identifier `phase9-operational-readiness`, a matching release ID, a safe runtime-release evidence reference, and `Verified` status.
3. A Phase 9.0 `ApprovedForPreparation` result with a matching evidence-package reference, no blockers or review items, legacy authority preserved, and no activation permission.
4. Phase 9.1 prerequisite evidence matching pilot and release, preserving legacy authority, and confirming a completed single observation attempt.
5. Verified rollback evidence with a matching rollback evidence reference and safe owner/reference values.
6. Exactly one available, read-only, cancellation-aware typed observer for every selected workflow.
7. A safe non-empty fingerprint specification version for each selected observer.
8. An available evidence destination that supports cancellation.
9. A non-cancelled caller token.

Reviewable release, Phase 9.1, rollback, or workflow evidence produces `RequiresReview` when no blocker exists. Missing, rejected, inconsistent, unsafe, duplicate, or unavailable evidence blocks. Exceptions are discarded and map to `operational-preflight-evaluation-failed`. Preflight neither accesses nor mutates production.

## 5. Fingerprint specifications

All five specifications use SHA-256 over UTF-8 bytes of a canonical representation. Every field is length-prefixed; strings are Unicode Form C; booleans are `0`/`1`; integers use invariant decimal; decimals use invariant `G29`; and comparison uses ordinal semantics. Collections whose business meaning is order-independent are defensively copied, deduplicated where appropriate, and sorted by stable identity before canonicalization. Event sequence order remains semantic and is ordered by event minute, explicit sequence, and event ID.

Observation boundary (`LegacyAuthoritative` or `TargetReadOnly`) is validated by observer construction but is intentionally not fingerprinted: it identifies the source, not the compared domain state. Volatile observation time is likewise excluded. Semantically meaningful period boundaries and event minutes are included.

### 5.1 Authentication — `auth-fingerprint-v1`

Included safe fields are station-scope identity, capability availability, ability to identify a ShiftProfile, personnel-number capability, station-scope enforcement, and the sorted non-secret capability-code set.

Passwords, password input, salts, credential hashes, authentication tokens, session identities, roles, private keys, and authorization decisions are absent. The observation compares intended capability only; it does not authenticate anyone and does not create a session.

Rasht golden vector:

`9CE756A65A862E6ACFE8E1DEE9C2504D67450576B326C5CEC2BFCF0AE6F7E29C`

### 5.2 Reporting — `reporting-fingerprint-v1`

Included fields are station, period identity, half-open period boundaries, summary parameter/aggregation/value/contributing-count entries, chart series/point identity/value entries, daily date/status/expected-count/actual-count entries, sorted warning codes, and optional finalized snapshot identity and SHA-256 checksum.

This covers period identity, min/max/average or sum values as applicable, chart point identity/count, daily status identity/count, warnings, and finalized checksum evidence. No raw input rows are included. A finalized snapshot is observed only; no finalization, correction, recalculation, or mutation occurs.

Rasht golden vector:

`BE13E8F15CB8283E8A47B20947F9B6EC8DB0070C2CA954D3446B934A61615550`

### 5.3 Runtime/Event — `runtime-event-fingerprint-v1`

Included fields are station and period boundaries and, for every stable unit identity: the authoritative event sequence (`EventId`, START/NSD/ESD/OH code, semantic event minute, sequence), physical runtime, separately retained ESD adjustment, adjusted runtime, RuntimeAfterOH, final state, ServiceDay count, physical LongestRun, cumulative runtime, and Trusted Runtime Baseline reference.

`TargetRuntimeEventOperationalObservationSource` invokes the existing target `RuntimeCalculator` on explicitly supplied, validated, fixture/read-only `RuntimeCalculationContext` values. It does not duplicate the runtime algorithm or read a repository. The target service preserves physical duration separately from ESD adjustment; LongestRun derives from physical intervals only; ServiceDay derives from positive physical overlap; and no generic STOP event exists.

Rasht golden vector:

`04B803482FD1291F1CE04BF5BF7B3ED347A72607E5F65D91B96A7FE93C581302`

### 5.4 Protected settings — `protected-settings-fingerprint-v1`

Included fields are station, current setting-state code, invariant ESD adjustment value, effective-evidence reference, management-protection requirement, and external-vendor-authorization requirement.

No ManagementCredential verification, external vendor authorization, ESD write, recovery, provisioning, or credential material occurs.

Rasht golden vector:

`7CC5190B12B1E3BE4AB5978FCD662C17E2A85859ED0F5AE3419C00DF329EE2ED`

### 5.5 Export — `export-fingerprint-v1`

Included fields are snapshot identity, intended renderer, deterministic filename, source SHA-256 checksum, artifact format, and a separately calculated metadata SHA-256 fingerprint. File input is filename-only and rejects directory separators and invalid filename characters.

No renderer is invoked and no artifact is generated. The phase therefore needs no temporary artifact cleanup and cannot overwrite a report or mutate its authoritative snapshot.

Rasht golden vector:

`24AAC29BD7F008F806B7ED28AF189AFD329F24D5783153F405A14C313FFA6816`

## 6. Representative operational fixtures

Fixtures are test/rehearsal-only and introduce no production station branch.

The Rasht fixture has three units:

- Unit 1 runs across an Operating Day boundary from minute 1380 to 1500, producing 120 physical minutes, a 120-minute LongestRun, and two ServiceDays.
- Unit 2 remains standby with an empty valid event sequence and zero runtime.
- Unit 3 covers OH while stopped, post-OH START/NSD, a second START/ESD sequence, separate 90-minute ESD adjustment evidence, RuntimeAfterOH reset/accumulation, and physical LongestRun behavior.

The Ramsar fixture has four units:

- Unit 1 covers the same cross-day continuity case.
- Unit 2 covers a normal START/NSD run.
- Unit 3 covers START/ESD with separate physical and adjustment totals.
- Unit 4 runs from minute 1400 through exactly minute 1440, proving the half-open 24:00 boundary does not create a second ServiceDay.

Both fixtures provide a two-day reporting period, 12 expected odd-hour records per complete day, min/max/average operational summaries, a summed daily value, chart-point identities, daily completeness statuses, immutable snapshot checksum evidence, authentication capability evidence, protected-settings evidence, export metadata, matching observations, and selectable intentional semantic differences for each workflow.

## 7. Runbook and lifecycle

The immutable standard runbook is version `operational-runbook-v1` and defines exactly one safe step for every required activity:

| Step ID | Activity | Expected outcome |
|---|---|---|
| `OPR-01-PREFLIGHT` | Preflight | Readiness evaluated |
| `OPR-02-APPROVE` | Approve | Explicit approval confirmed |
| `OPR-03-START` | Start | Rehearsal session started |
| `OPR-04-OBSERVE` | Observe | Read-only observations recorded |
| `OPR-05-COMPARE` | Compare | Fingerprints compared |
| `OPR-06-REVIEW` | Review | Operator decision recorded |
| `OPR-07-COMPLETE` | Complete | Rehearsal completed |
| `OPR-08-STOP` | Stop | Rehearsal session stopped |
| `OPR-09-ROLLBACK-REQUEST` | Rollback-request evidence | Request recorded only |

The coordinator lifecycle is `Created -> PreflightPassed -> Approved -> Started -> Observing -> ReviewRequired -> Completed`, with terminal `Stopped`, `Failed`, and `Disposed` alternatives. Every transition requires an explicit caller method. Observation can be attempted once. Completion requires an explicit operator decision. A completed, stopped, failed, or disposed instance cannot restart.

There is no automatic retry, timer, periodic timer, polling loop, scheduler, background worker, `Task.Run`, service locator, startup hook, or automatic launch. Disposal cancels an in-flight observation boundary and permanently closes the coordinator.

## 8. Monitoring and stop criteria

Monitoring is deterministic in-memory evidence, not telemetry. It records sorted safe signals for observer completion/failure, fingerprint match/difference, and rollback readiness, with `Healthy`, `AttentionRequired`, `Failed`, or `Stopped` status. It contains no raw logs and starts no subscription or loop.

`ControlledPilotOperationalStopEvaluator` applies a stable fail-closed priority:

1. security-boundary violation;
2. evidence-integrity failure;
3. rollback readiness lost;
4. cancellation;
5. observer failure;
6. fingerprint difference count above the explicitly supplied allowed policy;
7. explicit rollback request;
8. explicit operator stop.

Every stop result has a fixed safe reason code. It records evidence and stops the rehearsal coordinator only. It cannot shut down the application or production system, disable production functionality, run rollback, change a route, alter authority, or issue a compensating command.

## 9. Evidence bundle

`ControlledPilotOperationalEvidenceBundle` is immutable and contains the complete safe context identity, preflight result, sorted workflow results, workflow-to-fingerprint-version map, explicit comparison records, monitoring evidence, optional stop decision, runbook completion status, rollback readiness state, UTC completion time, and a SHA-256 bundle checksum.

The bundle checksum uses canonical length-prefixed fields and covers all exposed semantic evidence, including context references and approval flag, preflight status/reasons/time, observation fingerprints and times, specification versions, comparisons, monitoring, stop reason/reference/time, runbook state, rollback state, and completion time. `HasValidChecksum` recomputes and verifies it. Identical inputs and timestamps produce an identical checksum.

The bundle excludes passwords, credential hashes, secrets, keys, tokens, raw database rows, SQL, machine-local paths, exception text, stack traces, raw logs, delegates, mutable source collections, and authority grants. The supplied in-memory destination accepts one bundle per rehearsal ID, supports cancellation, writes no file, and accesses no database. A rejecting or throwing destination fails closed with a fixed code and retains the immutable bundle on the operation result for caller-controlled incident handling.

## 10. Automated test evidence

The Phase 9.2 suite contains 35 cases after theory expansion. Coverage includes:

- Rasht and Ramsar fixtures and unit counts;
- standby, normal run, START, NSD, ESD, OH, cross-day continuity, and exact 24:00 behavior;
- all five typed workflow observers;
- all five fixed fingerprint versions and Rasht golden vectors;
- identical-input reproducibility;
- semantic-change mismatch for every workflow;
- collection-order independence and `fa-IR`/`de-DE` culture independence;
- preflight ready, blocked, review, branch, workflow, destination, cancellation, preparation, prerequisite, and rollback rules;
- full lifecycle and single-attempt terminal behavior;
- healthy and attention-required monitoring;
- mismatch policy, observer failure, evidence integrity, rollback loss, security violation, cancellation, explicit operator stop, and rollback-request evidence;
- failure isolation and fixed reason codes;
- deterministic bundle checksum and checksum verification;
- defensive copies and get-only contracts;
- absence of password, credential-hash, session creation, roles, protected-operation execution, artifact generation, and production mutation capabilities;
- no database provider, migration runner, timer, background task, or WinForms dependency in the operational namespace;
- pinned `Program.cs` checksum and absence of Phase 9.2 references in startup and production forms.

The complete suite passes 626 of 626 tests. No tests create or mutate a production database. Runtime fixtures use only constructed in-memory domain input. Export tests generate metadata only and create no artifact.

## 11. Manual validation plan — not executed

The following plan is concrete but remains pending. This report does **not** claim manual execution.

### 11.1 Qualification setup

1. Use a Windows pilot workstation at 1920x1080.
2. Use an independently reviewed, explicitly invoked rehearsal harness; do not add it to normal startup or navigation.
3. Use an isolated copy/read-only source boundary approved for rehearsal. Confirm OS-level write denial before starting.
4. Capture release, Phase 9.0, Phase 9.1, rollback, operator, and evidence-destination references before constructing context.
5. Confirm the bounded UTC window and selected Rasht or Ramsar scope.
6. Record screen scaling, Windows version, application release, locale, keyboard layout, operator reference, and evidence destination reference without secrets or machine paths in the bundle.

### 11.2 DPI and Persian/RTL matrix

Repeat the full rehearsal at 100%, 125%, and 150% Windows scaling, always at 1920x1080. For each scale:

1. Verify every preflight status, workflow label, fingerprint match/mismatch indicator, monitoring signal, operator action, and stop reason remains visible without clipping or overlap.
2. Verify Persian text is shaped correctly, uses RTL reading order, and does not invert hashes, IDs, numeric values, event codes, or timestamps.
3. Verify focus cues and default actions remain visible.
4. Verify no horizontal truncation hides a mismatch, rollback-readiness loss, or stop reason.
5. Capture screenshots only under the approved evidence/privacy procedure; screenshots are not part of the Phase 9.2 bundle contract.

### 11.3 Keyboard navigation

1. Navigate every non-destructive rehearsal action using Tab and Shift+Tab.
2. Confirm focus order follows Preflight, Approve, Start, Observe, Compare/Review, Complete or Stop.
3. Activate buttons with Space/Enter where normal Windows conventions apply.
4. Confirm Escape does not bypass an operator decision or silently discard a required stop indication.
5. Confirm hashes and evidence references can be selected/copied without enabling edits to evidence.

### 11.4 Cancellation and close

1. Cancel before preflight; expect a fixed cancellation code and no observation.
2. Cancel during each workflow observation boundary; expect the rehearsal only to stop, no retry, no production effect, and terminal evidence where safely available.
3. Close the application during `Started`, `Observing`, and `ReviewRequired`; confirm disposal is terminal, no background work survives, normal application close is not blocked by a retry loop, and no production state changes.
4. Relaunch normally and confirm no pilot starts automatically and legacy workflows remain authoritative.

### 11.5 Difference, stop, and rollback-request paths

1. Use the approved intentional-difference fixture for each workflow in turn.
2. Verify the affected workflow is clearly identified without displaying raw protected data.
3. With allowed differences set to zero, confirm the rehearsal stops with `operational-stop-fingerprint-policy`.
4. With one allowed difference, confirm monitoring requires attention and an explicit operator decision remains mandatory.
5. Exercise explicit operator stop and confirm only the rehearsal ends.
6. Exercise rollback request and confirm it records `OPR-09-ROLLBACK-REQUEST` but executes no restore or rollback action.
7. Simulate rollback-readiness loss and security-boundary violation; confirm the correct fixed reason code and no application/production shutdown.
8. Independently verify the final bundle checksum and reconcile the bundle count in the approved evidence destination.

### 11.6 Manual acceptance record

For every matrix row, record Pass/Fail/Blocked, operator and reviewer references, release ID, scale, station fixture/scope, start/end UTC, bundle checksum, screenshot reference if approved, defect reference, and sign-off reference. Any failure remains a live-pilot blocker. Do not reinterpret a failed UI check as an accepted fingerprint difference.

## 12. Initial audit record (A–K)

### A. Architecture map

The solution contains one .NET 8 WinForms production project and one xUnit test project. Legacy production forms and services remain in `UI`, `Services`, `Data`, and `Utils`. Target foundation/domain components are separated under `Core`, `Application`, `Infrastructure`, and `Foundation`. Pilot stages are isolated under `Application/Pilot`, while production activation preparation is under `Application/Activation/Preparation`. SQLite persistence is confined to legacy data/services and target infrastructure repositories. Phase 9.2 depends on immutable Phase 9 evidence and target domain calculation only; it does not depend on UI or database infrastructure.

### B. Build status

The pre-change Release baseline built with 0 errors and 6 warning occurrences. The warnings are the same three distinct NU1701 compatibility warnings repeated for the production and test projects: OpenTK 3.1.0, OpenTK.GLControl 3.1.0, and SkiaSharp.Views.WindowsForms 3.119.0 restore from .NET Framework asset groups rather than the `net8.0-windows7.0` target. The pre-change suite passed 591/591 tests.

### C. Dependency/package health

The NuGet vulnerability scan reported no known vulnerable direct or transitive packages from the configured sources on 2026-09-02. The deprecation scan reported xUnit 2.9.3 and its v2 transitive packages as legacy, with xUnit v3 as the suggested alternative. The outdated scan found newer versions for several packages, including major-version updates; none were changed because this phase forbids silent package/framework upgrades. The direct Microsoft.Data.Sqlite reference plus a local DLL reference, SourceGear.sqlite3, and explicit SQLitePCLRaw bundle/core/native packages may be redundant or intentionally serve separate legacy/target paths; this requires a dedicated dependency-use trace before removal. It is not classified as a confirmed defect.

### D. Confirmed bugs

No new production business bug was confirmed during the Phase 9.2 audit. The NU1701 items are confirmed build compatibility warnings, not reproduced runtime failures. No speculative concern is classified as a bug.

### E. Potential bugs requiring validation

The NU1701 packages require representative Windows runtime validation for chart rendering. Multiple SQLite provider/package references require a separate use and native-loading validation. These are risks requiring validation, not confirmed Phase 9.2 failures.

### F. Incomplete functionality

Live read-only adapters, an approved external invocation harness, durable approved evidence persistence, and the manual qualification matrix remain intentionally incomplete. Production activation and cutover remain explicitly absent.

### G. Database/schema risks

No schema was changed. The existing target migrations remain draft/explicit infrastructure and are not referenced by Phase 9.2. A live pilot cannot proceed until its database boundary proves read-only access and independent non-mutation evidence against an isolated or least-privilege source.

### H. Performance problems

No Phase 9.2 performance defect was observed. Rehearsal executes five observers sequentially by design for deterministic evidence and failure isolation. Live-source latency and evidence-destination load remain unmeasured and require qualification without adding timers, polling, or automatic retries.

### I. UI/DPI problems

No production UI was changed or manually exercised. Existing Phase 8 pilot UI foundations are outside this implementation path. The required 1920x1080, 100/125/150%, Persian/RTL, and keyboard matrix remains pending; therefore no UI issue is claimed resolved.

### J. Duplication/technical debt

Safe-identifier policies exist in several Phase 8/9 namespaces with phase-specific allow-lists. Consolidation could alter accepted evidence contracts and was not attempted. The repository also retains both legacy and target runtime/report implementations by design while legacy remains authoritative. Neither was broadly rewritten.

### K. Prioritized remediation plan

1. Complete independent review of every live read-only adapter and prove write denial.
2. Execute the full manual validation matrix and resolve every failure.
3. Establish approved durable evidence retention, access, privacy, and integrity operations.
4. Capture installation-specific release, operator, approval, monitoring-owner, backup, restore-test, and rollback evidence.
5. Rehearse incident, explicit stop, application-close, and rollback-request procedures with named operators.
6. Resolve or explicitly accept runtime truth-table product decisions needed by observed target projections.
7. Qualify package/native compatibility and trace redundant SQLite dependencies in a separate reviewable change.
8. Obtain security, operations, data-owner, and product approval for a narrowly scoped live read-only pilot.
9. If separately authorized later, design an explicit invocation/composition surface; keep it outside normal startup.
10. Treat production cutover, migration, ESD authority, and any authority switch as separate future phases.

## 13. Exact blockers before a live controlled pilot

The implemented rehearsal is operationally executable with approved in-memory/fixture inputs, but a live controlled pilot is blocked by all of the following:

1. No independently reviewed live legacy authentication-capability reader or target ShiftProfile-capability reader exists.
2. No independently reviewed live reporting projection/snapshot read adapter exists.
3. No independently reviewed live Runtime/Event read adapter exists; only controlled validated input reaches the target domain calculator.
4. No independently reviewed live protected-setting read adapter exists.
5. No independently reviewed live export-metadata read adapter exists.
6. No proof exists that a live data-source principal is technically read-only and unable to mutate schema, data, events, settings, snapshots, or locks.
7. No approved durable evidence destination, retention schedule, access policy, privacy review, or operational checksum-verification process exists.
8. Installation-specific release/build provenance and target-machine qualification have not been captured.
9. Operator identity, approval authority, expiry, revocation, separation-of-duties, monitoring ownership, and escalation ownership remain external governance prerequisites.
10. Backup evidence, restore-test evidence, rollback readiness, explicit-stop procedure, and rollback-request procedure have not been rehearsed on the intended pilot installation.
11. The complete Windows/DPI/Persian/RTL/keyboard/cancellation/application-close manual matrix has not been executed.
12. Runtime truth-table items still marked Pending Product Owner Decision must be resolved or explicitly excluded from the live observed scope.
13. Security/threat review of the complete live adapter-to-evidence chain is pending.
14. There is deliberately no production startup registration, navigation entry, production UI replacement, feature activation, migration authorization, ESD cutover, or authority-switch approval.

Until every applicable blocker is closed with reviewable evidence, execution is limited to explicitly invoked, non-authoritative, read-only rehearsal. Legacy remains authoritative.

## 14. Verification record

Final verification commands completed after the final working-tree review:

- `dotnet build -c Release` — passed with 0 errors and 12 warning occurrences: the same six pre-existing NU1701 project/package warnings are emitted once during restore and once during build; there are no compiler warnings.
- `dotnet test -c Release` — passed 626/626 with 0 failed and 0 skipped.
- `git diff --check` — passed with no whitespace errors.

Protected-boundary verification pins `Program.cs` SHA-256 to `33985F732E77AFC7249DDA0174E8BCC58601B5E0B3E22B93E31933F01ACCAA76` and scans startup and production forms for Phase 9.2 references. Final source/diff checks must confirm that `Program.cs`, startup, production navigation, production forms, database schema/migrations, and package declarations are unchanged; that no production database or artifact mutation ran; and that no migration, ESD cutover, authority switch, RBAC implementation, or Support identity was introduced.
