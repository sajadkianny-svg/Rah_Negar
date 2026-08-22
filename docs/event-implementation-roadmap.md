# Event Subsystem Implementation Roadmap

**Project:** Generalized RahNegar platform  
**Document status:** Phased implementation plan  
**Source basis:** `docs/legacy-event-subsystem-audit.md`, `docs/event-subsystem-architecture-specification.md`, `docs/event-database-schema-specification.md`, and `docs/event-service-layer-specification.md`  
**Scope:** Safe replacement of the legacy Event subsystem through preparation, domain, persistence, commands, runtime, UI, migration/coexistence, and cutover  
**Out of scope:** Production implementation, executable migrations, current-schema changes, package upgrades, or authorization to alter user data

## 1. Executive overview

The Event replacement must be phased because Event entry, database history, runtime reports, finalized months, and operator workflow are coupled in the legacy application even though the target architecture separates them. A direct replacement would change several authorities at once: row identity, schema constraints, Add/Edit/Delete semantics, complete-chain validation, runtime reconstruction, reporting totals, and the user interface. If a defect appeared, the team could not reliably determine whether it came from migration, state transitions, persistence, runtime projection, UI mapping, or report integration.

Direct replacement is especially risky because legacy `tbl_events` may contain data that the new schema correctly rejects: same-Unit duplicate timestamps, malformed time text, unsupported or inconsistent Unit/type values, chains that permit Running + OH, missing Trusted Runtime Baselines, and Events whose correction could conflict with finalized reports. A big-bang migration would either fail, silently discard history, or force unapproved data correction. The legacy public runtime path is also known to use `CalculateLegacyCore`; comparing old and new totals without separating approved corrections from regressions would be misleading.

The transition therefore protects legacy functionality until each replacement responsibility is independently verified. The existing Event UI and `tbl_events` remain the production write authority through Phases 0–5. New domain and runtime components begin as isolated, test-driven code. New persistence is introduced beside, not over, the legacy table. Migration first produces read-only inventories and validation reports. Coexistence uses feature flags and comparison tooling; it does not silently dual-write user Events. Cutover occurs only after architecture, database, domain, runtime, migration, UI, and recovery approvals are complete.

Rollback is designed before every change. Until cutover, disabling the feature flag returns the application to the unchanged legacy path. During cutover, verified backups and a preserved legacy database provide recovery. Legacy data is never deleted or destructively normalized as part of this roadmap. Final retirement or archival is a later explicitly approved activity after the monitoring period and reconciliation are complete.

## 2. Implementation principles

### 2.1 No big-bang rewrite

Each layer is introduced behind a stable seam. Domain behavior, schema/repositories, commands, runtime, UI, migration, and activation are separate deliverables. A phase does not opportunistically rewrite unrelated daily-entry or reporting architecture.

### 2.2 One responsibility per phase

Every phase has one primary change axis:

- Phase 0 establishes evidence and recovery.
- Phase 1 establishes pure Event language/state behavior.
- Phase 2 establishes storage and transaction mechanics.
- Phase 3 establishes mutation orchestration and complete-chain validation.
- Phase 4 establishes runtime projection.
- Phase 5 establishes operator workflow.
- Phase 6 establishes migration evidence and controlled coexistence.
- Phase 7 activates the verified replacement.

Cross-phase discoveries update the relevant specification and tests rather than being hidden in the current phase.

### 2.3 Production changes only after tests exist

Before modifying an existing production path, add characterization/contract tests that describe the behavior being protected and new failing tests that describe the approved replacement behavior. New implementation follows test-first where practical. Passing tests do not automatically approve cutover; they are one gate alongside manual workflow, migration, and business review.

Characterization tests distinguish “legacy behavior observed” from “approved behavior to retain.” They must not freeze confirmed legacy defects such as Running + OH, stopped-state ESD adjustment, silent invalid-time coercion, or incomplete history reconstruction as desired rules.

### 2.4 Preserve legacy behavior until replacement is verified

The legacy production UI, persistence path, reports, and `tbl_events` remain unchanged and enabled by default until Phase 7 approval. New components are unreachable from normal production commands or run only in read-only comparison mode. Operators always have one clearly identified write authority.

### 2.5 Isolate database changes

New tables use new names and versioned schema ownership. No early phase alters or constrains `tbl_events`. Schema creation, repository tests, migration staging, and rollback are exercised against copies/temporary databases. The new unique constraint is enabled only after duplicate review and validated import.

### 2.6 Every phase is reversible

Each phase documents its disable/removal method, data impact, and recovery evidence before merge. Code-only phases revert by feature flag/commit without data conversion. Database phases create additive structures that can remain unused; rollback does not drop user data. Migration is copy-based and source-preserving. Cutover retains a verified pre-activation backup and an explicit recovery decision tree.

### 2.7 Additional safety rules

- No invented historical START or shutdown Events.
- No automatic deletion, merge, retime, or semantic rewrite of legacy Events.
- No UI-only validation or lock enforcement.
- No direct repository write path outside application commands.
- No ST/RPM input into runtime projection.
- No changes to finalized reports without explicit reopen/re-finalization governance.
- No framework/package major upgrade bundled with Event replacement.
- Every modification batch builds the entire solution, runs all tests, exercises affected workflows, and reviews the git diff.

## 3. Phase breakdown

### Phase 0 — Preparation and safety

#### Objective

Create a reproducible baseline, test/recovery assets, and delivery controls before Event production code changes.

#### Work

**Branch strategy**

- Create one long-lived integration branch for the Event replacement only if team workflow requires it; prefer short-lived phase branches merged through reviewed pull requests.
- Use one reviewable branch/PR per bounded deliverable: characterization tests, domain primitives, state machine, schema tests, repositories, each command handler, runtime metrics, UI slice, migration analyzer, and cutover wiring.
- Protect the production branch. Require successful build/tests and at least one reviewer familiar with Event rules and Persian-date behavior.
- Do not mix unrelated refactors, formatting sweeps, dependency upgrades, or station generalization into Event branches.
- Tag or record the exact pre-Event baseline commit and application/database versions.

**Backup strategy**

- Define the production SQLite location and all station database variants without writing to them during preparation.
- Create an operator-approved backup procedure using application-safe database backup/checkpoint behavior, not an unsafe copy during an active write.
- Verify restore into an isolated location and run SQLite integrity/foreign-key checks appropriate to the legacy schema.
- Record checksum, size, creation time, station identity, application version, and restore result.
- Never use a production backup as a test database without approved anonymization/access controls.

**Baseline build verification**

- Build the entire solution using the repository target framework and record errors/warnings.
- Record current NuGet vulnerability/deprecation/compatibility state without upgrading packages.
- Record startup and Event/report smoke-test environment.
- Capture repository status/diff so later phases can distinguish pre-existing work.

**Existing test capture**

- Inventory all test projects; the audit found no automated Event test suite, so create a dedicated test strategy before implementation.
- Add characterization tests around seams that will later be replaced where they can run without changing production behavior.
- Capture approved UX behavior: fixed types, configured Unit selector, `HH:mm`, read-only grid, Add/Apply/Cancel/Delete confirmations, Persian date conventions, Event optionality, and lock behavior.
- Create curated legacy data fixtures for Rasht and Ramsar without copying user data.
- Capture legacy runtime outputs separately as comparison evidence and label known-invalid cases so they are not treated as expected target results.

#### Deliverables

- Recorded baseline build/package report.
- Test plan and isolated fixture databases.
- Verified backup/restore runbook.
- Feature-flag and branch/review strategy.
- Traceability matrix from approved rules to future tests.

#### Required tests and checks

- Full solution build.
- Existing tests, if any.
- Manual legacy Add/Edit/Delete/load/report smoke tests on test data.
- Backup restore and database-open/integrity verification in isolation.
- No production data/schema modification.

#### Exit gate

Phase 0 is complete only when baseline commit/build, backup/restore, known legacy defects, approved preserved behaviors, and test ownership are recorded and reviewed. If backup restoration is not proven, no database phase may begin.

#### Reversal

Documentation/test-only additions can be reverted. No production runtime behavior or database is changed.

### Phase 1 — Create the new Event domain model

#### Objective

Implement the complete Event language and state behavior as pure code with no database, WinForms, filesystem, or report dependency.

#### Work

- Define `Event` domain entity with stable EventId and canonical fields.
- Define closed `EventType`: START, NSD, ESD, OH.
- Define `EventState`: Stopped, Running, StoppedAfterOH.
- Define value objects for StationId, UnitId, Persian EventDate, minute-of-day EventTime, derived EventDateTime, Remark, and RowVersion as appropriate.
- Define Trusted Runtime Baseline domain contract/effective boundary without database implementation.
- Define typed domain validation/result/error models.
- Build `EventStateMachine` skeleton, then implement exactly five valid and seven forbidden transitions.
- Centralize Persian-calendar conversion and minute formatting as a pure tested service/value object.
- Keep runtime arithmetic beyond state transition effects out of this phase.

#### Required tests

- All 12 state/input matrix cases.
- Rejected transitions leave state/effects unchanged.
- Running + OH always rejects.
- Stopped + OH enters StoppedAfterOH and signals after-OH reset without changing cumulative runtime.
- Valid Running + ESD marks ESD eligibility but does not itself add adjustment hours.
- Canonical EventType rejects aliases/unknown values.
- EventTime accepts 00:00/23:59, rejects seconds/24:00/malformed input, and round-trips minute-of-day.
- Persian date validity across all month lengths, leap/non-leap Esfand, and year boundary.
- EventDateTime ordering across midnight/Persian month/year boundaries.
- Value equality, immutability, and stable error codes.

#### Exit gate

The domain project has no dependency on WinForms, Microsoft.Data.Sqlite, repositories, technical logging implementations, or ST/RPM models. All transition/value tests pass and the matrix is reviewed against the architecture specification.

#### Reversal

New domain code is unreferenced by the legacy production path and can be removed without data or UI impact.

### Phase 2 — Create the persistence foundation

#### Objective

Provide additive new Event storage, repositories, transactions, and audit storage without replacing UI or legacy writes.

#### Work

- Define versioned new schema creation for `Events` and `EventAudit` according to the database specification.
- Add Station/Unit ownership relationships and Trusted Runtime Baseline repository mapping required by the generalized platform.
- Implement canonical Event type/date/time/timestamp checks, active Unit+EventDateTime partial uniqueness, foreign keys, tombstone consistency, and indexes.
- Implement connection factory behavior with `foreign_keys = ON` on every connection and approved busy timeout/WAL settings.
- Implement `ITransactionManager` with one shared transaction context and deterministic disposal.
- Implement `IEventRepository`: target lookup, full Unit chain, station/date range, Insert, Update with RowVersion, Tombstone.
- Implement `IEventAuditRepository`: AddAudit and GetHistory; no ordinary update/delete.
- Implement `ITrustedRuntimeBaselineRepository.GetBaseline()`.
- Keep new tables empty except test databases. Do not migrate or dual-write production Event data.

#### Required tests

- Schema creates successfully in a temporary empty database and is idempotent according to chosen versioning policy.
- Foreign keys are enforced on every connection.
- Same active Unit/timestamp rejects; different Units/same timestamp succeeds.
- Tombstoned Event permits a corrected active replacement timestamp while preserving audit FK.
- Invalid EventType, minute range, ownership, tombstone/audit action shape rejects.
- Repositories return explicit chronological order and exclude tombstones by default.
- Stable ULID identity survives Update; immutable fields do not change.
- RowVersion stale Update/Tombstone affects zero rows and maps to concurrency conflict.
- Event and audit write rollback together under injected failure.
- No repository starts an independent nested transaction.
- SQLite query-plan checks confirm chain/date/audit indexes are usable for representative volumes.

#### Exit gate

All persistence integration tests pass on supported SQLite/provider versions. Schema and repository review confirms no state machine, runtime, UI message, or unvalidated public write API exists in repositories. Legacy tables and UI remain untouched.

#### Reversal

Disable schema initialization/use through configuration. Additive empty new tables may remain harmless; do not drop them automatically. No legacy data changes occurred.

### Phase 3 — Create the application command layer

#### Objective

Implement authoritative Add/Edit/Delete orchestration, complete-chain validation, audit, locks, and rollback on top of Phase 1–2 contracts.

#### Work

- Implement `EventValidationService` as a pure service receiving loaded facts.
- Implement duplicate, baseline boundary, operating-day, finalized-period, Unit ownership, and canonical-value validation.
- Implement complete chain replay for every affected Unit.
- Implement `AddEventCommandHandler`.
- Implement `EditEventCommandHandler`, always validating old Unit and new Unit when changed, plus both old/new periods.
- Implement `DeleteEventCommandHandler`, always validating stored Unit even when no selected-day Events remain.
- Generate structured `EventValidationError` including error code, Persian message key/context, Unit, Event date/time/type, later invalid Event, and suggested correction.
- Write Event and EventAudit atomically inside the same transaction used for baseline/chain reads.
- Add command-level authorization, Station isolation, RowVersion conflicts, and correlation ids.
- Do not connect handlers to the legacy production UI yet.

#### Required tests

- Add at beginning, middle, and end of chain; all later Events replayed.
- Edit type/date/time/Unit; old and new Unit chains validated atomically.
- Delete the only Event on a day; Unit still validated.
- Delete/edit an earlier START that makes a later NSD/ESD invalid; entire command rejected and later Event identified.
- Duplicate same-Unit minute rejected in application and database race path.
- Different Units sharing minute succeeds.
- First/next sequential Persian operating day rules and future/out-of-sequence rejection.
- Missing/ambiguous baseline fails without invented START/default state.
- Finalized month Add/Delete rejection and Edit old/new month rejection.
- Running + OH correction instructs actual shutdown first.
- Every accepted command creates exactly one correct ADD/EDIT/DELETE audit snapshot.
- Every rejected/injected-failure command leaves Event/audit state unchanged.
- Stale RowVersion and concurrent conflicting commands return reload/conflict result.
- No handler or validator consults ST/RPM.

#### Exit gate

Command scenario suite passes against temporary SQLite. Traceability covers every approved transition and Add/Edit/Delete invariant. Review proves validation begins and mutation ends inside one transaction and no UI interaction occurs while it is open.

#### Reversal

Handlers remain behind an off feature flag and are not invoked by legacy forms. Remove/disable the new application registration without changing legacy data.

### Phase 4 — Create the runtime projection engine

#### Objective

Replace legacy runtime semantics with an independently tested projection from Trusted Runtime Baseline and validated Events.

#### Work

- Implement `RuntimeProjectionService` with explicit calculation version.
- Integrate Trusted Runtime Baseline effective state and cumulative/after-OH values without historical synthetic START.
- Reconstruct from baseline through report end, while clipping period metrics separately.
- Produce distinct Physical Runtime, ESD Adjustment, Adjusted Runtime, RuntimeAfterOH, ServiceDays, and LongestRun.
- Apply ESD adjustment only to accepted Running -> ESD and link it to source EventId/settings version.
- Implement OH reset of RuntimeAfterOH without cumulative reset or implicit running shutdown.
- Implement `[00:00, next 00:00)` operating-day overlaps and `07:00 <= Day < 19:00` reporting shift rules.
- Provide new report projection DTOs/adapters in isolation; do not switch finalized reports or legacy report UI yet.

#### Required tests

- Baseline Stopped/Running states and reports beginning after baseline.
- START 08:00 -> NSD 10:30 = 2.5 physical hours, one ServiceDay, 2.5 LongestRun.
- 23:30 -> 00:30 physical run marks two days; exact 00:00 end marks prior day only.
- Open run at report end and run spanning entire period are clipped correctly.
- Valid ESD enabled/disabled/zero setting; adjustment changes approved totals only.
- ESD adjustment alone creates no ServiceDay and does not extend LongestRun.
- OH retains cumulative, resets RuntimeAfterOH, and START after OH accrues anew.
- Running + OH cannot reach projection from a validated chain; corrupt input yields integrity failure.
- Persian month/year/leap boundaries and shift boundary 06:59/07:00/18:59/19:00.
- Full-history cumulative result for later arbitrary report ranges.
- ST/RPM models/repositories are absent from projection API.
- Golden target scenarios reviewed against hand calculations, not legacy defect outputs.

#### Comparison policy

Legacy/new report comparisons are categorized:

- **Expected match:** behavior already approved and correctly implemented by legacy fragments.
- **Expected correction:** difference caused by documented legacy defect, such as range-history omission, Running + OH, or stopped ESD adjustment.
- **Unexplained difference:** blocks progression until resolved.

#### Exit gate

Runtime specification traceability is complete; target hand-calculated cases pass; every legacy comparison difference is classified and reviewed. No finalized production snapshot is changed.

#### Reversal

Projection remains unused by the production report path or runs read-only under comparison flag. Disable it without database mutation.

### Phase 5 — Create the new Event UI workflow

#### Objective

Integrate the verified commands into a new operator workflow while preserving the worthwhile legacy interaction pattern and keeping old UI available during testing.

#### Work

- Build compact Event editor with configured Station Unit selector, fixed EventType selector, Persian date, HH:mm input, remark, and reason.
- Build read-only chronological grid keyed by EventId with vertical scroll and stable selection.
- Wire Add/Edit/Delete to application handlers only; no SQL/repository access in forms.
- Display structured Persian errors with Unit, Event date/time/type, exact reason, later invalid Event, and correction.
- Gather OH/delete/edit confirmations and reason before submitting command; no dialog during transaction.
- Implement predictable keyboard order/shortcuts and staged-versus-committed wording.
- Test DPI/RTL/localization at supported scales.
- Keep Events independently saveable from hourly ST/RPM/daily unique data while enforcing operating-day policy.

#### Coexistence with old UI

- New UI is behind a development/test feature flag and not default.
- Only one UI has write authority for a given test database/session. Do not let both screens edit concurrently.
- In early Phase 5, new UI uses disposable new-schema databases populated with curated valid fixtures.
- On read-only copies, operators compare workflow and display with legacy UI. New UI never writes `tbl_events`.
- Feedback is recorded against explicit UX acceptance criteria; business-rule requests update specifications/tests before implementation changes.

#### Required tests and checks

- UI command mapping for every field and stable EventId/RowVersion.
- Add/Edit/Delete success refreshes committed chronological data.
- All forbidden transitions and duplicate/later-chain errors display correct Persian details/correction.
- Cancel/confirmation behavior performs no command prematurely.
- Keyboard-only Unit -> type -> date/time -> remark/reason -> action flow.
- Grid scroll, sorting, selected identity after refresh, and tombstone exclusion.
- DPI 100%, 125%, 150%, 175%, 200%; RTL; Persian text clipping/focus.
- Rasht/Ramsar Unit isolation, including valid Ramsar U4 behavior where configured.
- Manual operator acceptance on non-production fixtures.

#### Exit gate

UI automation and manual acceptance pass. Review confirms no business rule or persistence implementation exists in form code. Legacy UI remains the production default.

#### Reversal

Disable the new UI feature flag/registration. No legacy UI or `tbl_events` behavior has been changed.

### Phase 6 — Migration and coexistence

#### Objective

Measure legacy data quality, validate a non-destructive mapping, compare behavior, and prove recovery before production activation.

#### Legacy table protection

- `tbl_events` remains untouched and is the source of truth initially.
- Migration reads from a verified backup/copy first.
- No constraints, triggers, normalization updates, delete, or row replacement are applied to `tbl_events`.
- New Events/EventAudit data is written only to isolated target/staging structures after validation and approval.

#### Data comparison strategy

1. Inventory source database fingerprint, Station mapping, row counts, legacy ids, raw values, and finalized-period membership.
2. Strictly map candidate Station, Unit, EventType, Persian date, minute time, EventDateTime, remark, and migration provenance.
3. Detect duplicate canonical same-Unit timestamps before target unique constraint/import.
4. Load each Trusted Runtime Baseline and replay every complete candidate chain.
5. Produce row-level exception and chain-level exception reports; do not silently coerce/drop.
6. On valid candidate data, calculate new runtime and compare with legacy report outputs using expected-match/expected-correction/unexplained categories.
7. Compare per-Unit Event counts, min/max timestamps, canonical row hashes, chain terminal state, physical/adjustment totals, ServiceDays, and LongestRun.
8. Review finalized reports separately; never overwrite snapshots during comparison.

#### Migration validation

- Station and Unit ownership verified, including Rasht/Ramsar isolation.
- Baseline exists exactly once and effective boundary is valid.
- All values canonicalize without guesswork.
- Duplicate groups have documented disposition.
- Complete chains pass approved state machine.
- Old/new counts reconcile after excluding only explicitly reviewed non-importable rows.
- EventId/audit migration provenance is complete.
- Target foreign-key/constraint/integrity checks pass.
- Target runtime outputs have no unexplained differences.

#### Coexistence rules

- Avoid long-term dual write: it creates two failure points and can diverge after one side commits. Before cutover, legacy remains sole production writer.
- Read-only shadow comparison may run against snapshots/copies and must not block or alter operator saves.
- A short, controlled migration rehearsal freezes a copy at a known boundary, imports it, validates it, and records duration.
- Feature flags distinguish legacy UI/write, new UI/write, and optional comparison telemetry. Invalid combinations are prevented.

#### Rollback plan

- Migration is copy-forward into new tables/database structures; source remains unchanged.
- Failed validation discards/recreates only isolated target staging after exact path verification, never source.
- Record target schema version and import run id so partial runs cannot be mistaken for complete.
- Restore rehearsal proves a pre-migration backup can reopen under the legacy application.
- Document maximum acceptable write freeze, operator notification, and who can authorize abort/cutover.

#### Exit gate

At least one representative Rasht and Ramsar rehearsal completes with reviewed exception dispositions, zero unexplained runtime differences, passing target integrity checks, and proven rollback. Migration and business owners sign off. Unresolved invalid chains, duplicates, missing baselines, or finalized conflicts block cutover.

### Phase 7 — Cutover

#### Objective

Make the verified new subsystem the only Event write authority through controlled activation, with monitoring and recoverable failure handling.

#### Pre-cutover requirements

- All prior phase gates complete and approvals recorded.
- Full solution build/tests pass on release candidate.
- Production backup is created and restore-verified according to runbook.
- Operator write window/freeze is communicated.
- Final migration dry run duration and disk capacity are acceptable.
- Feature-flag combinations and recovery package are tested offline.
- Finalized snapshot/version handling is approved.

#### Cutover sequence

1. Stop/disable Event writes and verify no active Event transaction.
2. Create final consistent backup/checkpoint and record checksum.
3. Run approved non-destructive import from legacy source into target structures.
4. Run counts, hashes, duplicates, full-chain replay, foreign-key/integrity, baseline, and runtime reconciliation checks.
5. If any required check fails, abort before activation and restore legacy write availability.
6. Disable the legacy Event write path in application configuration/feature flags.
7. Enable the new Event command/UI path as the sole writer.
8. Run controlled smoke tests for Add/Edit/Delete/read/runtime/report on approved test/operational scenario without modifying locked history.
9. End the freeze only after success criteria and responsible approver confirmation.

#### Monitoring period

For a defined period, monitor:

- command success/rejection/technical failure counts and durations;
- duplicate/constraint/concurrency/rollback failures;
- missing baseline or invalid-chain integrity errors;
- Event/audit one-to-one mutation reconciliation;
- station/date/unit query performance;
- operator Persian-message/UX feedback;
- live versus expected runtime/report results;
- backup success and database integrity checks.

Monitoring uses sanitized technical logs. EventAudit remains the operational change record. Legacy write code stays disabled but available for recovery build/configuration until exit approval; `tbl_events` remains preserved read-only.

#### Recovery plan

- **Before new writes:** disable new flag, restore legacy write flag; target can be discarded/retained for diagnosis because source is unchanged.
- **After new writes:** do not simply re-enable legacy writes, which would lose/diverge new Events. Freeze writes, preserve both databases, export committed new Events/audits, assess forward repair versus approved reverse mapping, and restore only under incident authority.
- Never overwrite the only copy of either history. Every recovery action records actor/time/reason and validation results.
- A recoverable technical defect should prefer forward fix if data integrity is intact. Data-integrity uncertainty triggers write freeze and incident review.

#### Exit gate

Monitoring window completes without unresolved integrity errors; audits reconcile; runtime/report approvals remain valid; backups succeed; operators accept workflow; recovery assets are retained per policy. Only then may legacy write code retirement be planned as a separate change.

## 4. Recommended project file and class structure

The structure should fit the existing solution with small projects/folders rather than forcing a broad rewrite. Exact namespaces may be adapted, but ownership boundaries must remain visible.

```text
Domain/
  Events/
    Event.cs
    EventType.cs
    EventState.cs
    EventStateMachine.cs
    EventValidationService.cs
    EventValidationError.cs
    ValueObjects/
      EventId.cs
      StationId.cs
      UnitId.cs
      PersianEventDate.cs
      EventMinute.cs
      EventTimestamp.cs
    Runtime/
      RuntimeProjectionService.cs
      RuntimeProjectionResult.cs
      EsdAdjustmentPolicy.cs

Application/
  Events/
    Commands/
      AddEventCommand.cs
      AddEventCommandHandler.cs
      EditEventCommand.cs
      EditEventCommandHandler.cs
      DeleteEventCommand.cs
      DeleteEventCommandHandler.cs
    Queries/
      EventQueryService.cs
      RuntimeQueryService.cs
    Contracts/
      IEventRepository.cs
      IEventAuditRepository.cs
      ITrustedRuntimeBaselineRepository.cs
      ITransactionManager.cs

Infrastructure/
  Events/
    SqliteEventRepository.cs
    SqliteEventAuditRepository.cs
    SqliteTrustedRuntimeBaselineRepository.cs
    SqliteTransactionManager.cs
    EventPersistenceMapper.cs
    MigrationAnalysis/

Persistence/
  Schema/
    EventSchemaDefinition.cs
    EventSchemaVersion.cs
  Migrations/
    [created only in an explicitly approved implementation phase]

UI/
  Events/
    FrmEvents.cs or embedded Event control
    EventEditorPresenter.cs
    EventGridViewModel.cs
    PersianEventErrorPresenter.cs

Tests/
  Domain.Tests/
  Application.Tests/
  Infrastructure.IntegrationTests/
  UI.Tests/
  Migration.Tests/
  Fixtures/
```

### Folder ownership

- `Domain/` owns pure business meaning and calculations. It references no UI or SQLite.
- `Application/` owns use cases, command/query contracts, transaction orchestration, and dependency ports.
- `Infrastructure/` owns implementations for SQLite, clocks/ids as needed, technical logging adapters, and migration analyzers.
- `Persistence/` owns versioned schema definitions and, only after explicit approval, migration implementation. It does not own state rules.
- `UI/` owns WinForms interaction/localization/DPI only.
- `Tests/` mirrors layer ownership and keeps fixtures isolated from user databases.

If separate assemblies would cause disproportionate disruption in the legacy solution, these may begin as namespaces/folders with architecture tests that enforce dependency rules. Separation into projects can occur only as a reviewed small change, not an automatic broad rewrite.

## 5. Dependency implementation order

The exact coding order is:

1. **Domain primitives/value objects.** Establish canonical identifiers, Event type/state, Persian date, minute time, timestamp, and errors.
2. **EventStateMachine.** Define the smallest authoritative transition behavior.
3. **Domain unit tests.** Exhaustively prove the matrix and value boundaries before any database/UI can hide defects.
4. **Complete-chain validation service and tests.** Prove affected-Unit semantics with in-memory chains.
5. **Database schema definition and constraint integration tests.** Build structural defense after canonical domain shapes are stable.
6. **Transaction manager and connection factory tests.** Establish the atomic boundary before repositories/commands rely on it.
7. **Repositories and repository integration tests.** Provide ordered, scoped data access without business logic.
8. **Application command DTOs/results.** Freeze use-case contracts and structured error shape.
9. **AddEventCommandHandler and scenario tests.** Establish the simplest mutation end to end.
10. **EditEventCommandHandler and scenario tests.** Add old/new Unit/date and RowVersion complexity.
11. **DeleteEventCommandHandler and scenario tests.** Add tombstone and empty-day/downstream validation.
12. **RuntimeProjectionService and hand-calculated tests.** Consume already valid chains; keep runtime out of write persistence.
13. **Read/query adapters and report comparison harness.** Verify data/runtime outputs without switching production.
14. **New UI and UI tests.** UI consumes stable command/query contracts rather than driving architecture.
15. **Migration analyzer, rehearsal importer, and migration tests.** Map real legacy variability only after target invariants exist.
16. **Feature-flag cutover wiring and deployment/recovery tests.** Activate last.

This order matters because downstream layers should depend on stable, tested upstream contracts. Starting with UI or schema would encode legacy strings/row operations before state meaning is correct. Starting runtime before state validation would force it to repair invalid chains. Starting migration before constraints/commands exist would move bad data without a trustworthy target. Cutover last ensures production behavior changes only after every component and recovery mechanism is proven.

## 6. Testing gate strategy

### Phase-by-phase merge gates

| Phase | Required automated tests before merge | Required manual checks | Acceptance criteria |
|---|---|---|---|
| 0 | Full build and all existing tests; backup verification automation where feasible | Legacy Event Add/Edit/Delete/load/report smoke; restore isolated backup | Baseline reproducible, backup restorable, known defects/preserved behaviors documented, no production mutation |
| 1 | 12 transition cases; value/date/time/error tests; dependency tests | Architecture review of matrix and Persian conventions | Pure domain has zero UI/SQLite dependency and all approved state behavior passes |
| 2 | Schema, FK, constraints, indexes, repository ordering, RowVersion, transaction/audit rollback | Inspect schema/query plans on supported SQLite; confirm legacy tables untouched | Additive target storage passes integrity tests; no business rules in repositories |
| 3 | Complete Add/Edit/Delete scenario/rollback/concurrency/lock/ownership/audit tests | Review Persian error context and transaction trace | Every affected chain is fully replayed; Event+audit atomic; no validation bypass |
| 4 | Hand-calculated physical/ESD/OH/ServiceDays/LongestRun/boundary/history tests | Business review of classified old/new differences | Zero unexplained differences; Events-only authority; calculation versioned |
| 5 | UI mapping, command result, localization, keyboard, sorting, identity, DPI automation where feasible | Operator acceptance at supported DPI/RTL on Rasht/Ramsar fixtures | UI contains no business/persistence logic and workflow is approved; legacy default unchanged |
| 6 | Migration mapping, duplicate/invalid-chain detection, provenance, counts/hashes, rollback/rehearsal tests | Review every exception category and representative Station rehearsal | Source untouched; all unresolved risks block import; zero unexplained runtime differences; rollback proven |
| 7 | Release full suite, activation flag matrix, upgrade/import, smoke, recovery drills | Controlled cutover rehearsal and approval | Sole writer is explicit; all checks pass before unlock; monitoring/recovery ready |

### Common gate required after every modification batch

1. Build the entire solution.
2. Run all available tests, not only the changed project.
3. Run focused affected-workflow tests.
4. Inspect git diff for unrelated changes, generated artifacts, secrets, and accidental schema scripts.
5. Confirm no user/production database was used.
6. Record exact changes, warnings, unresolved findings, and reversal instructions.
7. Update requirements-to-tests traceability.

A phase cannot waive a failing prior-phase test. Flaky tests block the gate until stabilized or explicitly quarantined with owner, evidence, and deadline; they are not silently rerun until green.

## 7. Legacy protection strategy

### 7.1 What remains untouched until cutover

- Legacy `tbl_events` schema and rows.
- Legacy Event UI as production default.
- Legacy normal daily save transaction and monthly lock behavior.
- Legacy reports/finalized snapshots as official current outputs.
- Rasht/Ramsar station profiles and isolation.
- Existing Persian date conventions and user data.

### 7.2 What can be reused

- Compact editor interaction pattern.
- Configured Unit and fixed Event type selectors.
- HH:mm minute entry.
- Read-only grid with explicit selection/Edit/Cancel/Delete.
- OH warning concept.
- Persian date navigation conventions.
- Atomic transaction and below-UI monthly lock patterns.
- Correct formula fragments: Events-only authority, 00:00 boundary, 07:00/19:00 shifts, physical-only ServiceDays, physical period-clipped LongestRun.

Reuse means behavior/pattern adaptation, not copying legacy persistence/validation/runtime classes wholesale.

### 7.3 What must be replaced

- Full-day delete/reinsert Event persistence and unstable identity.
- Unconstrained legacy Event schema.
- UI-callable/unvalidated persistence paths.
- Previous/new Event-type alternation validation and Running + OH acceptance.
- Empty deletion/Unit reassignment chain bypass.
- Invalid-time coercion and silent row omission.
- Legacy public runtime calculation/range-history behavior.
- ESD adjustment on invalid stopped ESD.
- UI-only or pre-transaction validation.
- Production-reachable test seeding bypass.

### 7.4 Rollback by stage

- **Phases 1–4:** new code is inactive; disable registration/flag or revert phase commit.
- **Phase 5:** disable new UI flag; legacy UI remains unchanged/default.
- **Phase 6:** discard only isolated target/staging after validation; legacy source remains authoritative and intact.
- **Phase 7 before new writes:** disable new path and re-enable legacy path after checks.
- **Phase 7 after new writes:** freeze both writers and reconcile; never toggle back casually. Preserve new Event/audit history and legacy backup, then follow incident-approved forward fix or reverse migration.

No rollback deletes the only copy of Event or audit history.

## 8. Migration risks and mitigations

| Risk | Failure mode | Mitigation | Blocking condition |
|---|---|---|---|
| Existing invalid Event chains | New state machine rejects sequences accepted/bypassed by legacy; runtime state becomes ambiguous | Strict staging replay from Trusted Baseline; row/chain exception report; human disposition; never invent Event | Any unresolved chain for a Unit prevents that Unit/database cutover |
| Duplicate timestamps | Target active unique constraint fails; same-Unit order is unknowable | Group by canonical Station/Unit/EventDateTime before constraint/import; preserve raw ids/values; business review | Any unresolved duplicate group blocks constraint activation/import |
| Missing baseline | Cannot determine initial state or reconstruct cumulative/after-OH runtime | Inventory baseline per Station/Unit; require exactly one valid effective baseline; recover from approved authoritative source only | Missing/ambiguous baseline blocks Unit migration and runtime approval |
| Finalized month conflicts | Correcting/migrating Events could change immutable snapshot results | Identify locks/snapshots and calculation versions; compare without overwrite; define explicit reopen governance | Any proposed mutation to locked history or unexplained snapshot difference blocks cutover |
| Historical data inconsistencies | Malformed date/time/type/Unit/remark encoding or pre-start Events cannot canonicalize safely | Strict parser, no 00:00 fallback/truncation; Station-aware ownership validation; review queue with raw source | Unresolved semantic mapping blocks affected row/chain |
| Station logic leakage | Unit alias maps to wrong Rasht/Ramsar Station or U4 handling differs | Source fingerprint + explicit Station mapping + composite ownership FK + station fixtures | Ambiguous Station/Unit ownership blocks import |
| Legacy/new runtime differences | Approved corrections mixed with new regression | Classify expected match/correction/unexplained; hand calculations; business sign-off | Any unexplained difference blocks Phase 4/6 exit |
| Audit provenance gap | Legacy lacks created/updated/user/delete history | Use migration identity/time with explicit provenance and unknown markers; never guess actors | Missing provenance schema/process blocks import, though historical actor may remain explicitly unknown |
| Partial migration | Target contains incomplete run mistaken as live | Migration run id/status, transaction boundaries, count/hash checks, feature flag off until complete | Incomplete/failed run cannot be activated |
| Disk/SQLite failure | Copy/import or constraint creation fails and risks data loss | Verified backup, free-space check, transaction, integrity checks, source-preserving copy-forward, rollback rehearsal | Failed backup/integrity/space check blocks operation |
| Concurrent writes during final import | Source changes after snapshot, creating divergence | Controlled Event write freeze/checkpoint; record boundary; verify no active transaction | Cannot establish consistent boundary blocks cutover |

## 9. Deployment strategy

### 9.1 Development

- New subsystem is registered behind an explicit feature flag defaulting OFF.
- Developers use temporary/fixture SQLite databases only.
- Domain and application components are testable without UI.
- New schema is additive and versioned; legacy `tbl_events` remains unchanged.
- Feature flag states are validated centrally so unsupported dual-writer combinations cannot start.
- Debug comparison tooling is read-only and visibly labels legacy, target, and known correction categories.

Suggested conceptual flags/configuration states:

- `LegacyEventWriteEnabled = true`, `NewEventWriteEnabled = false` — default before cutover.
- `NewEventUiVisible = true` only in development/test with an isolated target database.
- `EventComparisonEnabled = true` for read-only comparison.
- Exactly one write flag may be true; both false is permitted during freeze; both true is forbidden.

### 9.2 Testing

- CI runs pure unit, SQLite integration, scenario, migration, UI, and dependency tests.
- Test environments exercise both Station configurations with approved synthetic datasets.
- Old/new runtime results are compared where legacy behavior is valid; known defects are compared to approved hand-calculated target results.
- Migration rehearsals use protected copies with source fingerprints.
- Operators perform acceptance tests in an environment whose write authority is explicit.
- Upgrade, interrupted migration, insufficient disk, stale RowVersion, database busy, audit failure, and rollback/recovery are deliberately tested.

### 9.3 Production

- Deploy code with new path OFF before data cutover if operational policy permits, validating startup/legacy behavior first.
- Schedule controlled activation with backup, write freeze, migration, validation, smoke test, and named approvers.
- Enable only the new writer after target validation passes; disable legacy writer in the same controlled configuration change.
- Monitor technical and operational integrity for the defined window.
- Keep legacy data read-only and recovery artifacts protected.
- Do not remove legacy code/table in the activation release. Retirement is a later separately reviewed phase after successful monitoring.

## 10. Final implementation checklist

Every item requires named owner, evidence link/artifact, approval date, and status. “Not applicable” requires written justification.

### Governance and architecture

- [ ] Architecture approved.
- [ ] Database design approved.
- [ ] Service-layer contracts approved.
- [ ] State transition matrix approved.
- [ ] Runtime metric definitions approved.
- [ ] Persian date/time conventions approved.
- [ ] Finalized-report dependency/reopen policy approved.
- [ ] Security/authorization and audit retention approved.

### Engineering readiness

- [ ] Baseline build recorded and reproducible.
- [ ] Backup and restore procedure proven.
- [ ] Test projects/fixtures isolated from user data.
- [ ] Domain primitives and state machine tests approved.
- [ ] Database constraints/repositories/transaction tests approved.
- [ ] Add/Edit/Delete command tests approved.
- [ ] Runtime projection tests and hand calculations approved.
- [ ] Dependency boundaries verified.
- [ ] Full solution build has no new unexplained warnings/errors.

### Migration readiness

- [ ] Migration analyzer approved.
- [ ] Legacy inventory complete for every target Station database.
- [ ] Duplicate timestamp review complete.
- [ ] Invalid chain review complete.
- [ ] Trusted Runtime Baselines verified.
- [ ] Historical mapping inconsistencies resolved/documented.
- [ ] Finalized month conflicts resolved through approved policy.
- [ ] Migration provenance and EventAudit strategy approved.
- [ ] Representative Rasht and Ramsar rehearsals passed.
- [ ] Counts/hashes/constraints/foreign-key/integrity checks passed.
- [ ] No unexplained runtime/report differences remain.
- [ ] Migration approved.

### UI and operational readiness

- [ ] UI workflow approved.
- [ ] Persian messages approved.
- [ ] Keyboard, RTL, DPI, scrolling, and accessibility checks passed.
- [ ] Rasht/Ramsar Unit isolation approved.
- [ ] Runtime approved.
- [ ] Finalized report snapshot/version behavior approved.
- [ ] Feature-flag matrix and sole-writer guard tested.
- [ ] Monitoring dashboards/log queries/runbook ready.
- [ ] Operator/support training and recovery contacts ready.

### Activation

- [ ] Release candidate full suite passed.
- [ ] Production backup created and restore verified.
- [ ] Write freeze and cutover window approved.
- [ ] Recovery drill passed.
- [ ] Production activation approved.
- [ ] Final import validation passed.
- [ ] Legacy write path disabled.
- [ ] New Event path enabled as sole writer.
- [ ] Post-activation smoke tests passed.
- [ ] Monitoring period completed.
- [ ] Final operational acceptance recorded.

## 11. Final decision table

| Phase | Deliverable | Dependencies | Risk | Completion criteria |
|---|---|---|---|---|
| 0 — Preparation and safety | Baseline evidence, branch strategy, characterization plan, fixtures, verified backup/restore runbook | Approved audit/specifications; access to safe build/test environment | Incomplete baseline or unproven recovery makes later failures unrecoverable | Build/package state recorded; legacy smoke captured; restore proven; no production mutation |
| 1 — Domain model | Pure Event entity/types/states/value objects, errors, Persian chronology, EventStateMachine | Phase 0 traceability/test setup | Encoding wrong rule early propagates everywhere | All matrix/value/date tests pass; no UI/SQLite/ST-RPM dependency; domain review approved |
| 2 — Persistence foundation | Additive Events/EventAudit schema, repositories, transaction manager, baseline repository | Stable Phase 1 canonical types; database specification | Constraint/index/transaction defect can corrupt or reject valid data | Temporary SQLite integration suite passes; FK/unique/audit/RowVersion/rollback proven; legacy untouched |
| 3 — Application commands | Add/Edit/Delete handlers and complete-chain EventValidationService | Phase 1 domain; Phase 2 persistence/transactions | Incomplete affected-Unit replay or validation bypass | All scenario/concurrency/lock/audit tests pass; one transaction; structured errors; feature OFF |
| 4 — Runtime projection | Versioned physical/ESD/adjusted/after-OH/ServiceDays/LongestRun engine | Valid domain chains/baselines; Phase 1 and command fixtures | Incorrect cumulative/history or boundary results affect official reports | Hand-calculated/boundary suites pass; Events-only authority; zero unexplained comparisons |
| 5 — New UI workflow | Compact command-driven editor/grid and Persian error presentation | Stable Phase 3 command results and queries; Phase 4 display models as needed | UI could duplicate rules or confuse staged/committed state | UI automation/operator/DPI/RTL approval; no SQL/business logic; legacy remains default |
| 6 — Migration and coexistence | Read-only analyzer, reviewed mapping/exceptions, rehearsal import, comparisons, rollback proof | Phases 1–5; source backups; business reviewers | Invalid legacy history, duplicates, missing baseline, finalized conflicts | Source unchanged; all blocking exceptions resolved; constraints/counts/hashes pass; rollback proven; migration approved |
| 7 — Cutover | Sole new writer, validated final import, monitoring and recovery operations | Every prior exit gate and named activation approval | Divergence/data loss if legacy/new writers overlap or recovery is improvised | Backup/validation/smoke pass; legacy writer disabled; new writer enabled; monitoring closes without unresolved integrity issue |

