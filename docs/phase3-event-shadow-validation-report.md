# Phase 3.3 — Event Shadow Validation and Legacy Comparison Report

**Project:** RahNegar modernization  
**Phase:** 3.3 — Event Shadow Validation and Legacy Comparison  
**Status:** Implemented as an isolated, read-only comparison foundation  
**Production activation:** None

## 1. Scope and approach

Phase 3.3 adds a source-neutral comparison boundary for evaluating a legacy Event snapshot against a target Event snapshot without changing either source. It does not query `Data/db.sys`, implement a production legacy reader, write Event data, register a migration, or connect the target Event path to WinForms, Runtime, or Reporting.

The comparison workflow is:

1. A caller obtains immutable source records through a read-only reader.
2. `EventNormalizer` converts recognized legacy presentation variants to canonical Event values and records every formatting normalization.
3. Each `EventSequenceSnapshot` retains source order, canonical chronology, baseline state, and optional source-reported validity/final state.
4. `EventComparisonService` independently replays both snapshots through the approved Phase 3 state-transition evaluator.
5. The service compares count, source ordering, chronological positions, Event types, chain validity, and resulting state.
6. It returns a typed classification and machine-readable difference identifiers. It performs no persistence and has no mutation dependency.

Normalization is deliberately conservative. Trimming/case normalization of an otherwise canonical Event type and zero-padding of a valid hour/minute are recorded as formatting differences. Unknown Event types, invalid minute values, and invalid Persian dates are rejected. They are not coerced into valid target Events.

## 2. Implemented artifacts

### Core comparison models

- `Core/Event/Comparison/DifferenceCategory.cs` defines the approved classifications: `Equivalent`, `FormattingDifference`, `LegacyDataIssue`, `RuleDifference`, and `CriticalStateDifference`.
- `Core/Event/Comparison/NormalizedEvent.cs` defines canonical comparison values, source order, source identity, and normalization notes. It also contains the transport-neutral raw `EventSourceRecord`.
- `Core/Event/Comparison/EventSequenceSnapshot.cs` defines a Station/Unit-scoped immutable sequence, trusted comparison baseline, and optional legacy-reported result.
- `Core/Event/Comparison/EventComparisonResult.cs` exposes the final category, difference codes, counts, replay validity, and resulting states.

### Application comparison boundary

- `Application/Event/Comparison/ILegacyEventReader.cs` is a read-only asynchronous snapshot contract. No production SQLite or legacy-table implementation was created.
- `Application/Event/Comparison/EventNormalizer.cs` performs explicit Event type, Persian date, and minute-time normalization and reports invalid source values through the existing foundation `Result<T>` contract.
- `Application/Event/Comparison/EventComparisonService.cs` compares immutable snapshots and supports comparison through an injected `ILegacyEventReader`. It reuses only the pure approved state-transition contract.

### Test-only fixtures and tests

- `Rah_Negar.Tests/Event/Comparison/EventComparisonFixtures.cs` contains synthetic normal lifecycle, OH, ESD, duplicate, invalid-sequence, and missing-Event datasets. These fixtures contain no production data.
- `EventNormalizerTests.cs` verifies canonicalization, visible formatting notes, invalid type rejection, and invalid time rejection.
- `EventComparisonServiceTests.cs` verifies equivalence, formatting, ordering, type/rule, invalid-chain, and reported-state classifications.
- `EventShadowComparisonIntegrationTests.cs` runs all required fixture datasets and verifies the asynchronous read-only legacy snapshot boundary using a test-only in-memory reader.

## 3. Difference classification policy

Classification uses the highest safety significance observed:

| Category | Meaning | Typical evidence |
|---|---|---|
| `Equivalent` | Canonical sequences and replay outcomes agree. | Same count, chronology, types, validity, and final state. |
| `FormattingDifference` | Business meaning agrees but recognized source presentation was non-canonical. | Type whitespace/case or non-zero-padded valid time. |
| `LegacyDataIssue` | Source data shape or order differs without a demonstrated resulting-state change. | Count, chronology, or source-order discrepancy. |
| `RuleDifference` | Events at an aligned chronology use different canonical types while replay validity and final state remain the same. | NSD versus ESD at the same stop boundary. |
| `CriticalStateDifference` | Replay validity or resulting state differs, or a source-reported result disagrees with approved replay. | Duplicate timestamps, invalid chains, missing state-changing Events, or incorrect legacy final state. |

A critical resulting-state difference overrides lower-level observations. Differences are not suppressed merely because another difference exists.

## 4. Fixture comparison results

| Dataset | Expected result | Test result |
|---|---|---|
| Normal START → NSD lifecycle | Equivalent | Passed |
| OH → START lifecycle | Equivalent | Passed |
| START → ESD lifecycle | Equivalent | Passed |
| Duplicate same-minute Events | CriticalStateDifference | Passed |
| START → START invalid chain | CriticalStateDifference | Passed |
| Missing shutdown Event | CriticalStateDifference | Passed |

These results validate the harness, not real legacy production data. No claim of production reconciliation is made in this phase.

## 5. Verification evidence

- Whole solution build: succeeded with zero errors.
- Existing warnings: six inherited `NU1701` warnings for `OpenTK`, `OpenTK.GLControl`, and `SkiaSharp.Views.WindowsForms`; unchanged by this phase.
- Automated tests: 76 passed, 0 failed, 0 skipped.
- Databases used by Phase 3.3 tests: none.
- Production database connection: none.
- Legacy Event implementation changes: none.
- Runtime and Reporting changes: none.
- UI/composition activation: none.

## 6. Known differences and limitations

- There is intentionally no production `ILegacyEventReader`. A later approved validation activity must implement or adapt a strictly read-only reader against an authorized non-production copy.
- This phase uses synthetic fixtures only. Production-like anonymized fixtures must be approved before broader reconciliation.
- Comparison starts from the baseline supplied with each snapshot. Baseline provenance and boundary equality must be verified by the future reader/orchestrator before interpreting results.
- Formatting recognition is intentionally narrow. Unknown aliases remain invalid and require explicit mapping approval; the harness does not silently bless legacy spellings.
- A type difference such as NSD versus ESD may preserve immediate state but can change Runtime adjustment. It is therefore a `RuleDifference` and must be reviewed again during Phase 4 Runtime comparison.
- The service identifies difference categories and stable codes; human-readable operational reconciliation reports and UI are outside this phase.
- No parallel production execution, dual write, feature switch, or cutover behavior is implemented.

## 7. Approval criteria for shadow validation

Phase 3 Event shadow validation can be approved for progression only when:

1. The read source is an authorized disposable/anonymized database copy and is opened read-only.
2. Snapshot Station, Unit, boundary, baseline, Persian chronology, and deterministic ordering are demonstrably equivalent.
3. Every comparison run is reproducible from versioned fixture metadata without storing credentials or personal data.
4. All `CriticalStateDifference` results are investigated and resolved or explicitly accepted as confirmed legacy defects by the domain owner.
5. Every `RuleDifference` is traced to an approved Event rule and, where relevant, carried into Runtime reconciliation.
6. `LegacyDataIssue` cases are quarantined or mapped through an approved migration rule; no silent coercion is permitted.
7. Formatting-only differences are covered by an explicit deterministic import/normalization rule.
8. Normal, OH, ESD, duplicate, invalid, missing, Persian-boundary, and finalized-period fixture suites pass.
9. The full regression suite remains green and the legacy application path remains unchanged.
10. No unexplained difference remains before Event authority or migration activation.

## 8. Rollback

Rollback is deletion of the new Phase 3.3 comparison, test, and report files. No existing application composition, legacy Event source, migration registration, database schema, production database, Runtime component, Reporting component, or UI file was changed by this phase. Because the feature is unreferenced by the production startup path and performs no writes, rollback requires no data recovery.
