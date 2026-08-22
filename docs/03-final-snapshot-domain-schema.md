# RahNegar Version 1 Final Snapshot Domain Schema

## 1. Purpose

A Finalized Report is an immutable domain record, not a saved screen, grid, or PDF. Its snapshot contains everything required to reproduce the report's semantic content without querying mutable operational data, replaying current runtime rules, or resolving current station configuration.

The snapshot excludes rendering artifacts such as coordinates, column widths, selected tabs, colors, fonts, pagination, control state, and PDF layout instructions.

## 2. Core invariants

1. A snapshot belongs to exactly one station and Reporting Period.
2. It is created only from a complete applicable period under the approved finalization policy.
3. It records the station-definition and calculation versions used.
4. It contains all domain sections included in the finalized report.
5. It is immutable after transaction commit.
6. Its Period Lock is created in the same transaction.
7. Its content hash covers the canonical semantic payload and immutable metadata defined below.
8. Viewing or exporting it does not consult mutable operational tables for semantic values.
9. A renderer may change presentation without changing snapshot meaning.
10. Unknown future fields must not cause old snapshots to be rewritten.

## 3. Aggregate structure

```text
FinalReportSnapshot
├── SnapshotIdentity
├── ReportHeader
├── StationIdentity
├── DefinitionProvenance
├── ReportingPeriod
├── CalculationProvenance
├── CompletenessResult
├── MeasurementSummaries[]
├── DailyValueSummaries[]
├── UnitRuntimeSummaries[]
├── EventStatistics
├── ServiceDaySummaries[]
├── ActiveUnitCombinations[]
├── EventLog[]
├── ExtremeDateSummaries[]
├── RecycleTransitionSummary?
├── Diagnostics[]
├── FinalizationMetadata
└── IntegrityAndProvenance
```

Collections are stored in a canonical deterministic order for hashing, while their business identity must not depend on display order alone.

## 4. Snapshot identity

### `SnapshotIdentity`

| Field | Meaning |
|---|---|
| `FinalizedReportId` | Stable unique identity of the finalized report |
| `SnapshotSchemaVersion` | Version of this domain serialization contract |
| `SnapshotKind` | Monthly or multi-period finalized domain snapshot |
| `CreatedAtUtc` | Snapshot creation instant |
| `LegacyImported` | Whether values originate from a legacy finalized artifact |
| `SourceSnapshotIds` | Exact monthly source snapshots for a multi-period snapshot |

`FinalizedReportId` and source identities are stable IDs, not display names.

## 5. Report header

### `ReportHeader`

| Field | Meaning |
|---|---|
| `ReportTitle` | Semantic title frozen at finalization |
| `ReportType` | Monthly, FirstHalfYear, SecondHalfYear, Yearly, or approved type |
| `Language` | Language/culture of frozen labels when labels are part of the report record |
| `GeneratedAtUtc` | Time calculations completed |
| `FinalizedAtUtc` | Time finalization committed |
| `ReportStatus` | Finalized or LegacyImportedFinalized |

The header contains no pagination, filename, or control state.

## 6. Station identity

### `StationIdentity`

| Field | Meaning |
|---|---|
| `StationId` | Stable station identity |
| `StationCode` | Operational code frozen for human reconciliation |
| `StationDisplayName` | Display name at finalization |
| `DataStartDate` | Data applicability boundary used |
| `TimeZoneId` | Time-zone convention used for event/report interpretation |

Changing a station's current display name cannot alter an old snapshot.

## 7. Definition provenance

### `DefinitionProvenance`

| Field | Meaning |
|---|---|
| `StationDefinitionId` | Stable definition identity |
| `StationDefinitionVersion` | Exact effective version |
| `ConfigurationSchemaVersion` | Configuration-document contract |
| `ConfigurationHash` | Hash of canonical validated definition |
| `EffectiveFrom` | Definition start date |
| `EffectiveTo` | Definition end date, if bounded |
| `UnitDefinitions[]` | Frozen unit IDs, codes, names, and order relevant to report |
| `MeasurementDefinitions[]` | Frozen keys, labels, scopes, units, precision, aggregation meaning |
| `DailyValueDefinitions[]` | Frozen daily-value semantics |
| `EventTypeDefinitions[]` | Frozen codes, labels, and semantic classifications |
| `ShiftPolicy` | Boundaries used for event shift statistics |

If a Reporting Period legitimately spans multiple definition versions, `DefinitionProvenance` becomes an ordered collection with effective subranges. Whether Version 1 permits finalizing such a period as one monthly snapshot is **Pending Product Owner Decision**. The schema must represent it without loss even if finalization initially rejects it.

## 8. Reporting period

### `ReportingPeriodSnapshot`

| Field | Meaning |
|---|---|
| `RequestedDateFrom` | User/domain requested inclusive start |
| `RequestedDateTo` | Requested inclusive end |
| `EffectiveDateFrom` | `max(requested start, data_start_date)` |
| `EffectiveDateTo` | Inclusive effective end |
| `PeriodType` | Calendar/reporting classification |
| `PersianYear` | Calendar year where applicable |
| `PersianMonth` | Month for monthly snapshots |
| `ApplicableDayCount` | Count of dates evaluated for completeness |

A period with no applicable days cannot produce a normal finalized snapshot.

## 9. Calculation provenance

### `CalculationProvenance`

| Field | Meaning |
|---|---|
| `ReportCalculationVersion` | Aggregation, extremes, combinations, recycle rules |
| `RuntimeCalculationVersion` | Event-state/runtime semantics |
| `CompletenessCalculationVersion` | Completeness semantics |
| `FormulaVersions` | Calculated measurement formulas used |
| `RoundingPolicyVersion` | Conversion/rounding rules |
| `RuntimePolicySnapshot` | Approved ESD, ServiceDay, LongestRun, transition policies |
| `EventWatermark` | Highest authoritative event identity/revision considered |
| `OperatingDataRevisionSetHash` | Hash/provenance of source revisions |

Version identifiers are immutable semantic versions or content-addressed identifiers, not application build numbers alone.

## 10. Completeness result

### `PeriodCompletenessSnapshot`

| Field | Meaning |
|---|---|
| `IsComplete` | Final period result |
| `EvaluatedDateFrom/To` | Effective range checked |
| `ApplicableDayCount` | Expected days |
| `CompleteDayCount` | Days passing all rules |
| `IncompleteDayCount` | Days failing at least one rule |
| `DayResults[]` | Per-day semantic result |
| `CompletenessPolicyVersion` | Exact policy |

### `DayCompletenessSnapshot`

Includes:

- Operating Day.
- Definition version.
- Complete/incomplete result.
- Expected and actual scheduled observation counts.
- Missing observation identities.
- Duplicate observation identities.
- Missing Daily Station Values.
- Invalid values with stable validation codes.
- Structural integrity issues.

Normal finalization requires every applicable day complete. Legacy-imported finalized snapshots may record unavailable or reconstructed completeness explicitly rather than claiming normal completeness.

## 11. Measurement summaries

### `MeasurementSummarySnapshot`

| Field | Meaning |
|---|---|
| `MeasurementDefinitionId` | Stable identity |
| `MeasurementKey` | Frozen reconciliation key |
| `DisplayName` | Frozen semantic label |
| `EngineeringUnit` | Frozen unit |
| `Scope` | Station or unit and applicable UnitId |
| `AggregationResults[]` | One entry per supported aggregation |

### `AggregationResultSnapshot`

Includes:

- Aggregation type: Min, Max, Average, Sum, or approved future type.
- Exact numeric value in canonical decimal representation.
- Effective value count.
- Null/missing count where meaningful.
- Rounding policy reference.

Raw display formatting is not stored as the authoritative value.

## 12. Daily-value summaries

### `DailyValueSummarySnapshot`

Contains stable definition identity, frozen key/name/unit, aggregation result (normally Sum), effective count, missing count, and calculation-version reference. Daily values remain a separate collection from scheduled measurements even where their rendered report sections are adjacent.

## 13. Runtime summaries

### `UnitRuntimeSummarySnapshot`

For each applicable unit:

- Unit identity, code, and frozen display name.
- State at effective period start.
- State at effective period end.
- Physical period runtime seconds.
- Period ESD adjustment seconds.
- Approved PeriodRuntimeHours component total, if the product decision defines one.
- Cumulative runtime seconds at period end.
- Runtime-after-OH seconds at period end.
- Longest run value and boundary-semantics identifier.
- Trusted Runtime Baseline identity/provenance.
- Last OH boundary at period end, if known.
- Open-run start at period end, if Running.
- Runtime warnings/uncertainty markers.

Physical and adjustment durations must never be irreversibly collapsed.

## 14. Event statistics

### `EventStatisticsSnapshot`

Includes:

- Per-unit counts by stable event type.
- Total accepted events per unit.
- Station totals where meaningful.
- Day/night counts by event type under frozen shift policy.
- Count of excluded invalid/migration-anomalous events.
- Statistics policy version.

Invalid raw evidence, if retained, is not mixed into accepted counts without an explicit labeled statistic.

## 15. Service days

### `ServiceDaySummarySnapshot`

For each unit:

- Count of ServiceDays.
- Ordered set of Persian Operating Days qualifying.
- ServiceDay policy/version.
- Any uncertainty marker.

Storing the actual date set permits semantic reproduction and verification, not merely the total.

## 16. Active-unit combinations

### `ActiveUnitCombinationSnapshot`

For each applicable Operating Day:

- Date.
- Ordered stable UnitIds active under the ServiceDay/runtime policy.
- Active unit count.
- Combination identity derived from UnitIds.

An aggregate frequency list may also be stored, but it must be derivable from the frozen daily entries. `No Unit` is represented as an empty unit set, not a localized string.

## 17. Event log

### `EventLogEntrySnapshot`

Includes:

- Stable event identity.
- Unit identity/code/name.
- Event-type identity/code/name.
- Operating Day.
- Local event time.
- Stable sequence.
- Remark.
- Accepted/diagnostic status.
- Source provenance for migrated entries.

Grouping by unit or event type is rendering behavior and is not stored as snapshot domain state.

## 18. Extreme dates

### `ExtremeDateSummarySnapshot`

For every configured eligible measurement/scope:

- Measurement and optional unit identity.
- Minimum value.
- Ordered distinct dates attaining the minimum.
- Maximum value.
- Ordered distinct dates attaining the maximum.
- Effective value count.
- Numeric comparison tolerance/policy version.

Which fields qualify is frozen through definition provenance.

## 19. Recycle transitions

### `RecycleTransitionSummarySnapshot`

Optional and present only when configured for the station. Contains:

- Measurement identity.
- Transition policy/version.
- Total transition count.
- Ordered transition entries with date/time, prior classified state, next classified state, and source observation identities.

The legacy zero/nonzero definition may be carried by Rasht/Ramsar configuration, but whether it is the approved generalized meaning is **Pending Product Owner Decision**.

## 20. Warnings and diagnostics

### `SnapshotDiagnostic`

Includes stable diagnostic code, severity, affected entity/date/unit/measurement, structured parameters, source, and whether it affects authority. Localized rendered messages are not the only stored meaning.

Normal finalized reports should contain no error-level completeness/runtime diagnostics. Legacy-imported reports may contain explicit limitations, reconstruction notices, and unavailable-section diagnostics.

## 21. Finalization metadata

### `FinalizationMetadata`

Includes:

- Finalizer UserId and frozen display identity.
- Finalized time in UTC and station-local convention.
- Authorization/role used.
- Finalization transaction/correlation ID.
- Finalization reason/comment where required.
- Application release/build identifier for operational traceability.
- Database schema version.
- Whether finalization was native or legacy-imported.

The Windows environment username alone is insufficient as the authoritative finalizer identity.

## 22. Integrity and provenance

### `IntegrityAndProvenance`

Includes:

- Canonicalization algorithm identifier.
- Cryptographic hash algorithm identifier.
- Content hash.
- Optional signature/authentication metadata if later approved.
- Source data revision-set hash.
- Configuration hash.
- Source monthly snapshot hashes for aggregation.
- Legacy source database hash and row references where applicable.
- Migration tool and policy versions for imported reports.

The content hash is calculated over canonical semantic content excluding the hash field itself. Which operational metadata is included must be fixed by the snapshot schema version.

## 23. Immutability rules

After commit, none of these may change:

- Snapshot semantic payload.
- Report/station/period identity.
- Definition and calculation provenance.
- Completeness results.
- All summaries, date sets, logs, and diagnostics.
- Finalizer and finalization timestamp.
- Content hash and provenance.
- Link between snapshot and Period Lock.

Corrections create a new explicitly versioned artifact under a future approved correction workflow; they do not update the original. Version 1 provides no correction/reopen operation.

PDFs and screens are projections. Re-rendering does not mutate the snapshot. A renderer may improve layout but must not reinterpret values.

## 24. Schema evolution

1. Every snapshot carries `SnapshotSchemaVersion`.
2. Readers support known old versions through version-specific deserializers/adapters.
3. Old stored payloads are never rewritten merely to match a new schema.
4. Additive fields must have defined absence semantics.
5. A semantic change requires a new schema and/or calculation version.
6. Unknown versions are preserved and reported as unsupported, not partially interpreted.
7. Up-conversion may create an in-memory view but cannot claim unavailable historical data.
8. Tests maintain golden fixtures for every supported snapshot version.

Retention duration and the minimum supported historical snapshot versions are **Pending Product Owner Decision**.

## 25. Finalized multi-period reports

A finalized half-year/year result must use finalized monthly snapshots only when the policy requires finalized inputs. It references exact source `FinalizedReportId` and content hashes.

Aggregation rules:

- Min: minimum of source minima.
- Max: maximum of source maxima.
- Sum: sum of source sums.
- Average: weighted by effective value counts.
- Event counts: sum accepted source counts.
- Service-day count: union source date sets, then count.
- Active-unit combinations: concatenate source daily entries with duplicate-date rejection.
- Event log: merge entries in deterministic order with identity de-duplication.
- Extreme dates: compare source extrema, then union dates only from sources attaining the combined extreme.

Runtime endpoint fields cannot generally be summed:

- Period physical runtime may be summed across contiguous non-overlapping months.
- ESD adjustment may be summed when policy versions are compatible.
- CumulativeRuntimeAtPeriodEnd is taken from the chronologically last compatible source endpoint.
- RuntimeAfterOHAtPeriodEnd is taken from the last endpoint, subject to provenance continuity.
- LongestRunInPeriod uses the maximum only if source month clipping cannot hide a run crossing a month boundary; otherwise a boundary-continuation representation is required.

How cross-month LongestRunInPeriod and configuration/calculation-version incompatibilities are resolved is **Pending Product Owner Decision**. Until approved, finalization must reject an aggregation it cannot reproduce exactly rather than approximate it.

A multi-period snapshot may be persisted or constructed on demand from immutable monthly sources. This persistence choice is **Pending Product Owner Decision**; either approach must retain exact source references and deterministic semantics.
