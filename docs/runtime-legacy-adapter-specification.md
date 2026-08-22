# Runtime Legacy Adapter Specification

**Project:** Rah_Negar  
**Document status:** Audit-based adapter boundary specification  
**Scope:** Read-only capture and normalization of existing legacy Runtime output for shadow comparison  
**Out of scope:** Legacy changes, SQL/database adapter, production registration, UI/Reporting integration, and calculation replacement

## 1. Executive summary

The current production Event Runtime path is implemented in `Services/Reports/EventRuntimeCalculationService.cs` and orchestrated by `Services/Reports/EventReportEngineService.cs`. The public calculation method delegates directly to the private `CalculateLegacyCore` implementation. It returns an `EventReportResult` containing per-Unit `UnitEventSummary` rows, the ordered Event log, and Service Day sets.

The Phase 4.4 adapter boundary must observe that output without changing how it is produced. It must not query SQLite, load Events, reconstruct missing metrics, reinterpret invalid legacy chains, or call the Phase 4.2 engine. Its only future responsibility is to capture evidenced legacy values for one Station, Unit, period, and Event boundary, then normalize exact hour values to integral minutes for Phase 4.3 comparison.

The audit confirms that the existing legacy output does not directly expose every field required by `Core.Runtime.Comparison.RuntimeSnapshot`. Physical Runtime, period Adjusted Runtime, Final State, canonical Station identity, and Event boundary version are not all available from `UnitEventSummary`. The adapter contract therefore represents raw fields as nullable and normalization fails on missing evidence. It does not invent values or derive Physical Runtime by subtracting fields whose scopes differ.

## 2. Current legacy flow

```text
Reporting caller
    -> EventReportEngineService.BuildEventReport(connection, profile, dateFrom, dateTo)
        -> EventReportQueryService.LoadEvents(connection, dateFrom, dateTo)
        -> EventInitialStateService.LoadInitialStates(connection, profile, dateFrom)
        -> UnitRuntimeBaseQueryService.LoadBaseRuntimeHours(connection)
        -> UnitRuntimeBaseQueryService.LoadBaseRuntimeAfterOHHours(connection)
        -> AppSettingsService.GetSettings()
        -> EventRuntimeCalculationService.Calculate(...)
            -> CalculateLegacyCore(...)
                -> EventReportResult
```

`BuildEventReport` is the production orchestrator. It owns database/settings access and passes already loaded data into the static calculator. Phase 4.4 does not modify or call this path.

### 2.1 Runtime calculation location

| Responsibility | Current location | Evidence |
|---|---|---|
| Production orchestration | `Services/Reports/EventReportEngineService.BuildEventReport` | Loads Events, initial states, base values, settings, then invokes `EventRuntimeCalculationService.Calculate`. |
| Public legacy calculator | `Services/Reports/EventRuntimeCalculationService.Calculate` | Immediately delegates to `CalculateLegacyCore`. |
| Active calculation | `EventRuntimeCalculationService.CalculateLegacyCore` and helpers | Initializes summaries, tracks open runs, processes START/NSD/ESD/OH, closes open runs at period end. |
| Output models | `Models/Reports/EventReportResult.cs`, `UnitEventSummary.cs` | Holds calculated hours, counts, Event log, Service Day sets, and warnings. |
| Baseline reads | `Services/Reports/UnitRuntimeBaseQueryService.cs` | Reads `base_runtime_hours` and `base_runtime_after_oh_hours`. |
| Period-start state | `Services/Reports/EventInitialStateService.cs` | Reads earlier Event types and `initial_is_running` to infer booleans. |

`EventRuntimeCalculationService` also contains private `CalculateStateMachineCore` and `CompareLegacyAndStateMachine` methods. They are not called by the public `Calculate` path; the comparison method reports that invariant comparison is not implemented. They are audit context, not an approved adapter implementation.

### 2.2 Inputs

The active calculator receives:

- `ReportStationProfile`: Station display/name context and configured Unit strings;
- `IReadOnlyList<EventLogItem>`: period Events with Unit, string Event type, Persian date, string time, converted Gregorian `DateTime`, and remark;
- inclusive Persian `dateFrom` and `dateTo` values;
- per-Unit `baseRuntimeHours` as `double`;
- per-Unit `baseRuntimeAfterOHHours` as `double`;
- per-Unit `UnitInitialEventState` booleans at period start;
- `esdExtraEnabled` and `esdExtraHours` from application settings.

The calculator converts `dateFrom 00:00` to period start and the day after `dateTo 00:00` to the exclusive period end. Missing base dictionary entries default to zero.

### 2.3 Dependencies

The pure static calculation method depends on legacy report/domain models and `System.Globalization.PersianCalendar`. Its production caller depends on an existing `SqliteConnection`, Event/base/state query services, `AppSettingsService`, and the Station report profile.

The legacy Event query reads only `tbl_events` rows whose Persian dates are between the requested dates. Initial-state logic performs separate pre-period queries. Base values come from `unit_runtime_base`. These reads are existing behavior and are not moved into the adapter contract.

### 2.4 Event and DailyData usage

Runtime uses Event records only. The active calculation filters to profile Units and recognized `START`, `NSD`, `ESD`, and `OH` strings, orders by converted Event time and Unit, and processes them through helper methods. It does not read or consult DailyData, hourly ST/RPM observations, daily unique values, or completeness status.

`EventReportQueryService` normalizes Unit/type text and converts malformed or blank Event time to `00:00`. This is an existing data-quality behavior; the adapter must report/capture the resulting legacy output, not reproduce this coercion as a target rule.

### 2.5 Legacy START/NSD behavior

- Initial `currentRunStart` is period start when `IsRunningAtPeriodStart` is true.
- START increments counts. If a run is already open, it closes that run at the repeated START, then opens another run.
- NSD increments shutdown counts, closes any open Runtime and RuntimeAfterOH run, then clears both run starts.
- At period end, any open run is closed in memory at the exclusive boundary.
- Closed physical duration is added directly to `RuntimeHours`; positive-overlap Persian dates are added to the Service Day set; the maximum closed duration becomes `LongestRunHours`.

The calculator does not receive an authoritative validated-chain marker and does not reject all transitions forbidden by the target state machine. The future adapter must not label its input validated or repair these behaviors.

### 2.6 Legacy ESD handling

ESD follows the same run-closing behavior as NSD and increments ESD counts. When `esdExtraEnabled` is true and `esdExtraHours > 0`, each recognized ESD:

- adds the configured hours to `RuntimeHours`;
- adds the same hours to `RuntimeAfterOH`;
- adds the hours to period `EsdExtraHoursTotal`.

The adjustment is applied before checking whether a run is open. Therefore the code can apply adjustment to an ESD while the legacy tracker is already stopped. That behavior conflicts with the approved target eligibility rule but must not be changed by an audit adapter. Zero/disabled settings contribute no adjustment.

### 2.7 Legacy OH handling

OH increments total Event count, closes any open physical and RuntimeAfterOH runs at the OH time, clears the run starts, and sets `RuntimeAfterOH` to zero. Thus the legacy calculator permits OH to close a Running interval. The approved target state machine rejects `Running + OH`; this is a comparison/reconciliation difference, not authority to alter legacy code.

`EventInitialStateService` tracks whether OH has appeared before the period, but sets `IsRunningAfterOHAtPeriodStart` equal to the inferred Running boolean. Its Running inference compares the latest START only with the latest NSD/ESD; OH is not included as a stopping candidate in that comparison. This is a verified implementation gap requiring scenario characterization before a real adapter is accepted.

### 2.8 Outputs

For every configured Unit, `UnitEventSummary` exposes:

- `RuntimeHours`: base Runtime plus calculated run hours plus applied ESD extra hours;
- `RuntimeAfterOH`: base value carried/accumulated/reset by legacy logic;
- `EsdExtraHoursTotal`: ESD extra applied while processing the requested period;
- `LongestRunHours`;
- Event totals and Day/Night START/NSD/ESD counts.

`EventReportResult.ServiceDaysByUnit` exposes distinct Persian service-date sets. `EventLogItems` exposes ordered period Events. The result does not directly expose final operational state or a source/Event revision.

## 3. Target adapter boundary

`Application/Runtime/LegacyAdapter/ILegacyRuntimeAdapter` is a read-only contract:

```text
Read(StationId, UnitId, PeriodStartMinute, PeriodEndMinute, EventBoundaryVersion)
    -> LegacyRuntimeSnapshot
```

There is deliberately no implementation. A future implementation must be outside legacy calculation code, must not contain SQL, and must receive or invoke only an explicitly approved read-only legacy capture source. Database acquisition and source snapshot consistency belong to a separately approved harness/infrastructure boundary.

`LegacyRuntimeSnapshot` retains raw legacy hour values and nullable gaps. `LegacyRuntimeSnapshotNormalizer` validates the expected identity, period, and Event boundary, requires all comparison fields, converts exact hour values to integral minutes, and delegates final invariant validation to `RuntimeSnapshotNormalizer`.

The contract does not claim that every field can already be populated from `UnitEventSummary`. A future implementation cannot be considered complete until the mapping gaps below have evidence-backed solutions.

## 4. Normalization mapping

```text
Legacy Runtime capture
    -> LegacyRuntimeSnapshot (raw hours, explicit identity/boundary, nullable gaps)
        -> LegacyRuntimeSnapshotNormalizer
            -> Core.Runtime.Comparison.RuntimeSnapshot (integral minutes)
```

| Normalized field | Candidate legacy source | Mapping rule | Current status |
|---|---|---|---|
| `SourceName` | Adapter constant/version | Required nonempty evidence label | Available in future adapter |
| `StationId` | Requested Station plus trusted profile mapping | Ordinal exact match; never infer from Unit | **Gap:** legacy profile exposes StationName, not canonical StationId |
| `UnitId` | `UnitEventSummary.Unit` plus approved identity mapping | Must equal requested canonical Unit | Mapping approval required |
| `PeriodStartMinute` | Requested `dateFrom 00:00` | Central canonical Persian conversion | **Gap:** legacy output does not retain canonical minute |
| `PeriodEndMinute` | day after requested `dateTo 00:00` | Exclusive canonical minute | **Gap:** legacy output does not retain canonical minute |
| `EventBoundaryVersion` | Consistent Event source snapshot marker | Must exactly equal comparison input | **Gap:** legacy result exposes no revision/hash |
| `PhysicalRuntimeMinutes` | No direct `UnitEventSummary` field | Must be captured/derived with approved same-scope evidence | **Blocking gap:** do not subtract cumulative mixed-scope values |
| `ESDAdjustmentMinutes` | `EsdExtraHoursTotal` | Exact hours × 60, only if confirmed same period/Unit | Candidate available |
| `AdjustedRuntimeMinutes` | No direct period field | Must equal period Physical + period ESD | **Blocking gap:** `RuntimeHours` is cumulative composite, not period Adjusted Runtime |
| `RuntimeAfterOHMinutes` | `RuntimeAfterOH` | Exact hours × 60 | Candidate available, subject to legacy semantics |
| `LongestRunMinutes` | `LongestRunHours` | Exact hours × 60 | Candidate available |
| `ServiceDayCount` | `ServiceDaysByUnit[unit].Count` | Non-negative integer | Candidate available |
| `FinalState` | Internal open-run tracker | Must be captured without inference from output totals | **Blocking gap:** not returned by `EventReportResult` |
| `CalculationVersion` | Adapter constant tied to characterized legacy build | Required nonempty version | Future adapter responsibility |

All hour-to-minute conversions require a finite, non-negative value exactly representable as an integral minute within a small floating-point tolerance. Rounded report display such as `2.08 h` is not accepted as authority for 125 minutes. Missing fields fail normalization.

## 5. Known gaps and comparison implications

1. `RuntimeHours` combines Wizard base Runtime, physical run hours calculated in the requested period, and ESD extras. It cannot be mapped directly to either period Physical Runtime or period Adjusted Runtime.
2. `RuntimeAfterOH` is directly exposed, but legacy pre-period state reconstruction and Running+OH behavior can differ from target semantics.
3. Final state is internal and discarded from the production result.
4. Event source revision/boundary identity is absent. Same-period comparisons without the same Event boundary are invalid.
5. Legacy Event rows lack stable Event identity in `EventLogItem`; same-time deterministic proof is limited.
6. Legacy calculation accepts filtered string Events rather than a target `ValidatedEventChain` and can ignore unsupported types.
7. ESD adjustment can apply without an open run; target requires a valid Running-to-ESD transition.
8. OH can close a legacy open run; target rejects Running+OH.
9. Invalid/blank Event time can be coerced to midnight before calculation.
10. Existing `LoadRuntimeHistoryForComparison` can load from DataStartDate but is not used by the production calculation and is not an adapter. Phase 4.4 does not activate it.
11. No DailyData influences legacy Runtime. A future adapter must not add DailyData as corroboration or repair input.

These are verified code differences/gaps. Whether a numeric divergence is an `ExpectedPolicyDifference`, `LegacyDefect`, or `NewEngineDefect` remains an evidence-based Phase 4.3 classification decision for each fixture.

## 6. Migration and operational risks

- Treating cumulative `RuntimeHours` as a period metric would create false comparisons.
- Deriving Physical Runtime by `RuntimeHours - EsdExtraHoursTotal` would still retain Wizard base Runtime and can mix scopes.
- Reading legacy and target Events from different snapshots would produce invalid `InputMismatch` comparisons.
- Converting displayed two-decimal hours back to minutes can lose or alter authoritative values.
- Calling production report orchestration from a background harness could unintentionally couple comparison to UI/report/database lifecycle.
- Reimplementing legacy SQL or calculation inside the adapter would create a second legacy behavior that can drift.
- Normalizing malformed Event time differently would compare different effective inputs.
- Assuming legacy StationName or `U1` text is canonical identity could leak Rasht/Ramsar scope.
- Persisting comparison output as Runtime truth would violate the read-only shadow policy.

## 7. Required future implementation steps

1. Approve a read-only capture harness boundary and ensure it cannot mutate or replace production flow.
2. Create representative anonymized Rasht/Ramsar database copies and immutable Event boundary markers; never use the production database directly.
3. Characterize exact legacy outputs for approved fixtures, including repeated START, stopped ESD, Running+OH, open runs, arbitrary periods, and Persian boundaries.
4. Decide how to capture period Physical Runtime and Final State without changing legacy behavior. If instrumentation is required, it needs separate approval and must remain outside production activation.
5. Define canonical Station/Unit mapping and Event boundary version generation.
6. Implement `ILegacyRuntimeAdapter` against the approved capture source, not SQL, and return explicit missing fields when evidence is unavailable.
7. Normalize through `LegacyRuntimeSnapshotNormalizer`; never bypass its identity, period, integral-minute, or adjusted-total checks.
8. Feed normalized snapshots into `RuntimeComparisonService` with the same Unit, period, and Event boundary.
9. Record every difference and attach approved defect/policy evidence before using non-default classification.
10. Keep the harness feature-isolated, read-only, non-UI, non-Reporting, and unregistered in production startup until a separate cutover gate.

## 8. Acceptance conditions for a future adapter implementation

- No SQL or production database dependency exists in the adapter.
- Legacy Runtime source and behavior remain byte-for-byte unchanged.
- All normalized fields have documented provenance and matching scope.
- Missing fields fail rather than default to zero.
- Identity, period, and Event boundary mismatches fail before metric comparison.
- Hour values normalize exactly to integral minutes without display round-tripping.
- Representative Rasht/Ramsar copy-based fixtures reconcile or have approved classifications.
- The implementation remains read-only and is not registered into production UI, Reporting, or Runtime paths.
