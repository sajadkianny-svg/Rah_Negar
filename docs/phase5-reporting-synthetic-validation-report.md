# Phase 5.5 Synthetic Reporting Validation Report

## Status

Phase 5.5 is complete as a test-only synthetic validation layer. It exercises the Phase 5.4 input adapters/composer and Phase 5.3 calculator without production data, database access, production registrations, legacy Reporting changes, UI integration, or snapshot persistence.

## Fixture architecture

The reusable fixture is located under `Rah_Negar.Tests/Reporting/Synthetic`. Its in-memory source implements the five reporting adapter contracts and supplies a self-contained synthetic Rasht profile containing:

- canonical Station identity and captured synthetic display name;
- two configured Units in deliberately non-canonical source order;
- typed pressure and daily-fuel parameter definitions;
- twelve odd-hour operational observations for a complete responsibility day;
- two daily unique values;
- validated START/NSD Event projections for both Units;
- authoritative integral-minute Runtime projections for both Units;
- source, Event, Runtime, Baseline, configuration, profile, policy, calculation, snapshot-format, and calendar version evidence.

The validated flow is:

```text
Synthetic in-memory sources
            |
            v
Five reporting adapter contracts
            |
            v
ReportInputComposer
            |
            v
NormalizedReportInput
            |
            v
ReportCalculator
            |
            v
ReportProjection
```

All synthetic adapters are private test fixture types. They are not available for production registration.

## Validation scenarios and results

### Scenario A — Fully valid report

The complete source set composes successfully and produces a `Complete` projection. Validation asserts the hourly average, daily sum, two Runtime summaries, four Event log rows, and complete finalization eligibility.

Result: passed.

### Scenario B — Missing hourly data

The fixture omits the twelfth hourly observation and supplies deterministic missing-slot completeness evidence. Composition succeeds because the source remains authoritative and typed; calculation produces an `Incomplete`, non-finalizable projection with the `hourly.slot.missing` warning.

Result: passed.

### Scenario C — Version mismatch

The fixture supplies invalid/missing required Runtime Baseline version evidence for `unit-1`. Composition preserves the authoritative adapter output. The projection core rejects calculation with `version.runtime-baseline.missing:unit-1` rather than accepting version-incompatible evidence.

Result: passed.

### Scenario D — Invalid Unit/Event alignment

The Event adapter returns `unit-x` instead of configured `unit-1`. Composition fails before calculation with both the missing configured Event Unit and unexpected Unit evidence. No partial normalized input or projection is returned.

Result: passed.

### Scenario E — Repeated calculation

The complete scenario is independently composed and calculated twice. A culture-invariant projection fingerprint plus typed Event and Runtime sequences are identical across both executions.

Result: passed.

## Additional validation

Evidence-preservation assertions confirm that source revisions, hourly/daily record counts, Station profile identity, Event Chain versions, and Runtime configuration versions survive the complete pipeline unchanged. Source collections are deliberately unordered to prove composer/calculator deterministic ordering.

Focused Phase 5.5 tests: 6 passed, 0 failed, 0 skipped.

## Limitations

- Fixtures are synthetic and do not represent production records or claim parity with legacy report output.
- No SQLite file, connection, query, transaction, repository, migration, or schema is used.
- Only an approved synthetic Rasht-shaped profile is exercised; these fixtures do not establish Ramsar production values.
- Persian calendar conversion and multi-day/month enumeration are not implemented by the fixture; canonical boundaries are supplied directly.
- The missing-hour scenario validates propagation and gating, not a future adapter algorithm for detecting all 12 odd-hour slots.
- Event validation and Runtime calculation are not reimplemented; their authoritative projections are supplied as typed fixture inputs.
- No shadow comparison, production adapter, dependency registration, UI path, finalization, locking, snapshot, or exporter is introduced.

## Isolation verification

Phase 5.5 modifies only test fixture/test files and this report. No file under legacy `Services/Reports`, `Models/Reports`, `Core/Reports`, `UI`, or `Data` is changed, and no database/schema artifact is created or modified.
