# RahNegar Version 1 Implementation Roadmap

## 1. Roadmap principles

- No milestone begins while its listed product decisions remain unresolved.
- Automated testing begins with the foundation and is not deferred to stabilization.
- Prefer reviewable vertical slices that exercise domain, persistence, application, and UI together.
- Rasht, Ramsar, and a synthetic future station are continuous acceptance configurations.
- Legacy migration never mutates a source database.
- A milestone is complete only when its acceptance criteria and documentation are satisfied.
- Production implementation must not encode a recommended rule marked **Pending Product Owner Decision**.

## 2. Milestone dependency map

```text
M0 Decision Freeze
 └─► M1 Architecture Foundation
      ├─► M2 Domain + Persian Date/Time
      │    ├─► M3 Configuration System
      │    │    ├─► M4 SQLite Schema + Migration Framework
      │    │    │    ├─► M5 Station Initialization + Security Baseline
      │    │    │    │    ├─► M6 Observation Entry Vertical Slice
      │    │    │    │    └─► M7 Event Validation Vertical Slice
      │    │    │    └─► M8 Runtime Engine
      │    │    └─► M9 Completeness
      │    └─► M10 Reporting
      ├─► M11 Finalization + Locking
      ├─► M12 Backup + Restore
      ├─► M13 Security + Audit Completion
      ├─► M14 Legacy Migration
      └─► M15 Cross-Station Acceptance + Release Readiness
```

Some work may overlap after contracts are frozen, but acceptance follows these dependencies.

## 3. M0 — Product decision freeze

### Objective

Resolve rules that would otherwise produce incompatible runtime, completeness, storage, or snapshot behavior.

### Required decisions

- Runtime transition decisions listed in `02-runtime-truth-table.md`.
- All ESD Adjustment targets.
- Same-time event policy.
- ServiceDay qualifying duration.
- LongestRunInPeriod boundary definition.
- Initially Running baseline with unknown start.
- Whether `data_start_date` may be any Persian date.
- Meaning, validation, and reporting role of legacy statuses S/M/A/OH.
- Engineering units, valid ranges, precision, and zero semantics for legacy fields.
- Named-user/role model.
- Configuration changes spanning a report month.
- Finalized multi-period persistence and cross-month longest-run policy.
- Snapshot version retention policy sufficient for Version 1 support commitments.

### Deliverables

- Approved glossary.
- Approved runtime truth table.
- Approved ADR set.
- Approved Rasht and Ramsar field catalog.
- Approved synthetic station requirements.
- Decision log with owners and dates.

### Acceptance criteria

- No blocking item is merely assigned an undocumented default.
- All Version 1 runtime scenarios have expected outcomes.
- Product owner signs off on domain terminology and calculations.

## 4. M1 — Architecture foundation

### Objective

Establish logical modules, dependency rules, build pipeline, testing conventions, and versioning policy.

### Dependencies

- ADR-004 revision approved.
- Technology baseline approved.

### Work scope

- Establish initial physical projects without requiring one project per logical module.
- Establish namespaces and logical module APIs.
- Configure nullable reference types and static analysis.
- Establish dependency injection only in composition root.
- Establish structured logging contracts.
- Add architecture tests for prohibited dependencies.
- Establish unit/integration/acceptance test categories.
- Establish version identifiers for schema, configuration, runtime, reporting, and snapshots.
- Establish CI build/test process that does not require network access after dependency acquisition.

### Initial vertical proof

Create an executable shell that loads no station data but demonstrates Domain tests running without UI/SQLite dependencies and infrastructure composition occurring only at startup.

### Acceptance criteria

- Debug and Release builds succeed.
- Architecture tests detect deliberate prohibited dependency samples in test fixtures.
- Domain tests run without SQLite, configuration parser, or UI initialization.
- No Rasht/Ramsar name appears in production branching.
- Module ownership document matches ADR-004.

## 5. M2 — Domain and Persian date/time model

### Objective

Implement and prove the stable domain vocabulary, identities, Persian dates, local times, Operating Days, and Reporting Periods.

### Dependencies

- M1.
- `data_start_date` policy frozen.
- Station time-zone convention frozen.

### Work scope

- Stable identity value objects.
- PersianDate validity, comparison, addition, month boundaries, leap years.
- LocalOperatingTime and deterministic operating timestamp.
- Reporting Period validation/effective range.
- Station, unit, definition-version, and Operating Day core models.
- Domain validation issues with stable codes.
- Integral duration type and hour-conversion policy.

### Automated tests

- Persian leap and non-leap Esfand.
- Month/year boundaries.
- Invalid dates/times.
- `max(period start, data_start_date)`.
- Empty effective periods.
- Date ordering and serialization round trips.
- Duration conversion and rounding.

### Acceptance criteria

- No direct Persian-calendar arithmetic is required outside the date/time module.
- Domain cannot represent an invalid PersianDate.
- Reporting period boundary behavior is documented and tested.

## 6. M3 — Configuration system

### Objective

Load, validate, version, and persist generalized station definitions.

### Dependencies

- M2.
- Approved configuration concepts and formula safety policy.
- Field metadata for Rasht/Ramsar.

### Work scope

- Versioned configuration document schema.
- Structural and semantic validators.
- Stable IDs/keys, units, measurements, daily values, statuses, units, schedules, events, transitions, runtime policies, reports, and paste mappings.
- Constrained calculated-field expression policy.
- Configuration canonicalization and hash.
- Effective-date and non-overlap validation.
- Rasht and Ramsar definition packages.
- Synthetic station definition deliberately differing in unit count, fields, and schedule.

### Automated tests

- Valid/invalid configuration fixtures.
- Duplicate keys/orders/times.
- Unknown references.
- Unsafe formulas and divide-by-zero policies.
- Version overlap.
- Round-trip canonical hash stability.
- All three acceptance station packages.

### Acceptance criteria

- All station behavior needed by entry/reporting can be resolved without station-name branches.
- Invalid definitions fail before operational use.
- Used definition versions are immutable.

## 7. M4 — SQLite schema and migration framework

### Objective

Create the generalized database, integrity constraints, repository contracts, and forward-only schema migration mechanism.

### Dependencies

- M2 domain identities.
- M3 configuration persistence contract.
- ERD and typed-value strategy approved.
- Same-time event uniqueness policy frozen.

### Work scope

- Schema metadata/version table.
- Station/configuration/unit metadata tables.
- Operating days, observations, daily values, and unit events.
- Runtime baseline/checkpoint tables.
- reporting periods, snapshots, and locks.
- users/roles/audit/recovery metadata required by V1.
- Foreign keys, uniqueness, checks, and indexes.
- Transaction and connection policy.
- Forward migration, backup-before-migration, and failed-migration recovery.
- Repository integration-test harness using isolated temporary databases.

### Automated tests

- Constraint violations.
- Unique logical observation identities including station scope.
- Transaction rollback.
- Concurrent revision mismatch.
- Schema upgrade from every released schema fixture, beginning with V1 seed.
- Foreign-key enforcement on every connection path.

### Acceptance criteria

- Database rejects structural duplicates and orphan references.
- Failed writes leave no partial Operating Day.
- Migration is repeatable against supported prior fixtures.
- Query plans for expected date/event access use intended indexes.

## 8. M5 — Station initialization and security baseline

### Objective

Create a usable station database from a validated definition and establish the first authorized user and runtime baselines.

### Dependencies

- M3 and M4.
- Named-user/role decision frozen.
- Password and recovery security policy approved.
- Trusted Runtime Baseline requirements frozen.

### Vertical slice

Definition import → validation preview → station identity → units/baselines → administrator → transactional initialization → reopen and verify.

### Automated tests

- Initialization rollback at each failure stage.
- Duplicate station prevention.
- Invalid baseline rejection.
- Password hash verification without plaintext retention.
- Effective data-start boundary.
- Rasht, Ramsar, synthetic initialization.

### Acceptance criteria

- Initialization is atomic.
- Result contains no station-specific tables.
- Every unit has a valid Trusted Runtime Baseline.
- First login works under approved security policy.

## 9. M6 — Observation and data-entry engine

### Objective

Deliver generalized creation, loading, editing, paste mapping, derived values, and atomic persistence for one Operating Day.

### Dependencies

- M5.
- Measurement precision/range/zero semantics frozen for acceptance configurations.
- Formula and sequential-entry policies frozen.

### Vertical slices

1. Open a new date and generate configured fields/schedule.
2. Enter and validate station/unit observations.
3. Enter Daily Station Values.
4. Paste using configuration mapping.
5. Calculate approved derived fields.
6. Save atomically.
7. Load, enter explicit edit mode, compare revision, and replace atomically.

### Automated tests

- Required/optional fields.
- Numeric/text/status typing.
- Range/precision and culture parsing.
- Duplicate prevention.
- Formula correctness and divide-by-zero.
- Paste row/column mapping for all three stations.
- Unsaved/optimistic-concurrency behavior.
- Transaction rollback and locked-write contract stub.

### Acceptance criteria

- UI contains no hard-coded station field indexes as business identity.
- Identical application service handles all station packages.
- One failed component prevents the whole-day save.

## 10. M7 — Event validation vertical slice

### Objective

Deliver generalized event entry and chronological validation, without yet producing authoritative runtime reports.

### Dependencies

- M4 and M5.
- Complete runtime truth table frozen.
- Same-time policy frozen.

### Work scope

- Event type/configuration resolution.
- Deterministic ordering.
- Previous-day and next-event chain validation.
- Stable diagnostic codes.
- Production rejection behavior.
- Diagnostic representation for migrated anomalies.
- Atomic integration with daily replacement.

### Automated tests

- Every state/event cell in the truth table.
- Same-time rules.
- Cross-day edits.
- First event after baseline.
- Invalid edit affecting a later event.
- Events for separate units at same time.

### Acceptance criteria

- Truth-table tests are exhaustive and generated from approved cases.
- Invalid production events never persist.
- No event ordering depends on query return order.

## 11. M8 — Runtime engine

### Objective

Deliver one deterministic, infrastructure-independent runtime replay engine.

### Dependencies

- M2 and M7.
- All runtime semantics and ESD targets frozen.
- ServiceDay, LongestRun, baseline uncertainty, and shift policies frozen.

### Work scope

- Baseline validation.
- Pre-period replay.
- In-period metrics.
- Physical/adjustment separation.
- Period, cumulative, after-OH outputs.
- Service days, longest run, state endpoints, and statistics.
- Calculation-version contract.
- Optional checkpoint validation, only after correctness.

### Automated tests

- Every truth-table transition.
- Unit running before `dateFrom`.
- Cross-midnight/month/year runs.
- OH before/inside period.
- ESD adjustment permutations under approved policy.
- No START, repeated START, orphan stops, ambiguous migration histories.
- First report after installation.
- Same endpoint/different start cumulative invariance.
- Property tests for nonnegative and monotonic cumulative physical runtime.

### Acceptance criteria

- Engine runs without SQLite/UI.
- Golden scenarios match signed-off manual calculations.
- Physical and adjustment seconds remain distinguishable.
- Cumulative endpoint is independent of report start.

## 12. M9 — Completeness

### Objective

Provide one authoritative completeness engine used by entry, missing-day checks, reporting, and finalization.

### Dependencies

- M3 and M6.
- Event participation in completeness frozen.
- Definition-version boundary behavior frozen.

### Work scope

- Expected field/schedule resolution per day.
- Missing, duplicate, invalid, and structural issue categories.
- Date-range evaluation using effective start.
- Monthly/half/year missing-day queries.
- Stable results and diagnostics.

### Automated tests

- First partial month.
- Dates before data start.
- Missing time versus missing field versus missing daily value.
- Duplicate observation.
- Invalid value.
- Definition change within period.
- Persian leap month.

### Acceptance criteria

- All workflows call the same engine.
- A date before data start never appears missing.
- Exact reasons are available for every incomplete day.

## 13. M10 — Reporting

### Objective

Produce semantic live reports independent of UI layout.

### Dependencies

- M8 and M9.
- Aggregation, rounding, zero, extremes, recycle, and report-section rules frozen.

### Work scope

- Monthly, half-year, and yearly requests.
- Measurement min/max/average/count.
- Daily-value sums/counts.
- Runtime/event sections.
- Service days and active-unit combinations.
- Event log, extremes, recycle transitions where configured.
- Diagnostic incomplete-report mode.
- Report domain result and rendering adapters.

### Automated tests

- Exact aggregation fixtures.
- Weighted averages.
- Tied extreme dates.
- Zero/null policies.
- Cross-month event/run cases.
- Rasht, Ramsar, synthetic station reports.

### Acceptance criteria

- Domain report contains no UI/grid/PDF artifacts.
- Same input revisions and calculation versions produce identical canonical results.
- Incomplete diagnostics cannot be mistaken for finalizable completeness.

## 14. M11 — Finalization and locking

### Objective

Create complete immutable snapshots and enforce production locks.

### Dependencies

- M10.
- `03-final-snapshot-domain-schema.md` approved.
- Snapshot canonicalization/hash and multi-period policies frozen.

### Vertical slice

Generate complete monthly report → revalidate revisions/completeness → snapshot every semantic section → hash → create lock in same transaction → view/export from snapshot only → reject every production write path.

### Automated tests

- Finalization rollback at each stage.
- Concurrent change before finalization.
- Lock enforcement for observations, daily values, events, baselines, deletions, and retroactive configuration.
- Snapshot hash stability.
- Snapshot view after raw data mutation in an isolated adversarial test.
- Versioned snapshot golden fixtures.
- Finalized multi-period aggregation rules.

### Acceptance criteria

- No finalized semantic section is recomputed from mutable data.
- Lock and snapshot cannot exist independently after commit.
- No normal production repository bypasses lock enforcement.

## 15. M12 — Backup and restore

### Objective

Deliver consistent, authenticated, recoverable station backup and restore.

### Dependencies

- M4 schema/version framework.
- M5 authorization.
- Encryption key/recovery ownership policy frozen.

### Work scope

- SQLite-consistent snapshot.
- Versioned manifest, authenticated encryption, integrity authentication.
- Staged restore and compatibility validation.
- Automatic pre-restore safety copy.
- Atomic replacement and rollback.
- Backup/restore audit history.

### Automated tests

- Round trip.
- Wrong key/tampering/truncation.
- Foreign station and unsupported schema.
- Failed replacement rollback.
- Snapshot with WAL activity.
- Recovery after simulated post-replacement validation failure.

### Acceptance criteria

- No hard-coded application-wide secret.
- Active database is not overwritten before staged validation.
- Restore failure leaves the prior database usable.

## 16. M13 — Security and audit completion

### Objective

Complete roles, recovery, audit coverage, and destructive-operation authorization.

### Dependencies

- M5 baseline security.
- Authorization matrix and recovery authority frozen.

### Work scope

- User/role administration.
- Expiring single-use recovery.
- Attempt controls.
- Audit for all material actions.
- Factory reset authorization and recoverability requirements.
- Sensitive-data logging review.

### Automated tests

- Capability matrix.
- Recovery expiry/use/race/attempt limits.
- Finalizer identity.
- Audit append-only behavior through application services.
- No plaintext credentials/secrets in logs or database.

### Acceptance criteria

- Every privileged action is attributable to a stable user.
- Recovery cannot be reproduced from compiled public constants.
- Audit coverage matches ADR-020.

## 17. M14 — Legacy migration

### Objective

Create a repeatable, read-only conversion from supported Rasht/Ramsar legacy databases with full reconciliation.

### Dependencies

- M3–M13 target contracts stable.
- Legacy anomaly policies frozen.
- Legacy finalized-snapshot reconstruction policy frozen.

### Work scope

- Source detection/integrity inventory.
- Exact field mappings.
- Settings, units, baselines, data, daily values, events, locks, and snapshots.
- Legacy event ID ordering evidence.
- Runtime side-by-side comparison.
- Row/value reconciliation and source hashes.
- Explicit anomaly workflow.
- No source writes.

### Automated tests

- Sanitized Rasht/Ramsar fixtures.
- Duplicate/missing/corrupt/anomalous fixtures.
- Locked and partially snapshotted months.
- Repeat migration to fresh targets with identical canonical output.
- Source hash unchanged before/after.

### Acceptance criteria

- Every source row is mapped, intentionally excluded with reason, or diagnosed.
- Finalized legacy values are preserved with provenance.
- No ambiguous runtime correction is silent.
- Migration report is product-owner reviewable.

## 18. M15 — Cross-station acceptance and release readiness

### Objective

Demonstrate generalized behavior and operational readiness.

### Dependencies

- All prior milestones.

### Acceptance configurations

1. Rasht: three units and line-pressure measurements.
2. Ramsar: four units without line-pressure measurements.
3. Synthetic future station: different unit count, schedule, field set, units, optional values, and report eligibility, with no production-code changes.

### Validation

- End-to-end initialization, entry, paste, events, runtime, completeness, reporting, finalization, backup, restore, and authorization.
- Legacy side-by-side report samples.
- DPI/layout and keyboard workflow testing.
- Performance against approved maximum years/units/fields.
- Failure recovery and database integrity.
- Offline installer/update/rollback.
- Threat and privacy review.

### Acceptance criteria

- Synthetic station is supported by configuration only.
- Rasht/Ramsar accepted report scenarios reconcile or have approved defect corrections.
- No open critical/high defects.
- All unresolved decision markers affecting Version 1 are closed or explicitly removed from scope.
- Release, backup, restore, and migration runbooks are approved.

## 19. Continuous quality gates

Every milestone requires:

- Debug and Release build.
- Relevant unit and integration tests.
- Architecture tests.
- No new station-name production branches.
- Database migration review when schema changes.
- Snapshot fixture review when semantic schema changes.
- Threat review for security/backup changes.
- Documentation update and decision traceability.
- Git diff review with no unrelated changes.
