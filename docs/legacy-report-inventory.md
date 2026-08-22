# Legacy Reporting Subsystem Inventory

## 1. Purpose and scope

This document is the Phase 5.1 source inventory of the existing Rah_Negar reporting subsystem. It records what exists today, where each output obtains its data, how calculations are performed, and which portions are live or snapshotted. It is an audit artifact only; it does not define or implement replacement behavior.

Evidence was taken from the current solution and cross-checked against `docs/legacy-report-subsystem-audit.md`, `docs/reporting-architecture-specification.md`, and `docs/master-implementation-roadmap.md`. File and member references identify the evidence location. Line numbers are useful navigation aids but may move after unrelated edits.

## 2. User-visible report inventory

The production reporting entry point is `UI/Forms/FrmReportCenter.cs`, opened by `FrmMain.OpenReports`. No second dedicated report form was found. `FrmRecords` is an operational-entry form, not a report form.

| Report/view | Periods available | Data shown | Current source |
|---|---|---|---|
| Operational summary | Persian month, first half, second half, year | Min, max, average for eligible hourly parameters | Live `tbl_data`, or monthly summary snapshots when the selected period is fully locked |
| Daily unique summary | Same | Sum for configured cumulative daily values | Live `tbl_unique`, or monthly summary snapshots |
| Unit event/runtime summary | Same | Runtime, Runtime After OH, ESD addition, longest run, and event counts | Live event/runtime calculation for open periods; monthly unit-event snapshots for the locked monthly grid |
| Service-day summary | Same | Per-Unit service-day values | Live event calculation; stored service summary for a locked monthly grid |
| Event log | Same | Event date/time/type and Unit details | Always recalculated/read from live `tbl_events`, including locked views |
| Service combination | Same | UI-derived combinations of Units in service | Live event result, including locked views |
| Extreme dates | Same | Dates associated with eligible hourly minima/maxima | Live `tbl_data`, including locked views |
| Monthly final PDF | Locked Persian month only | Final operational, daily unique, runtime/event, service and event-date sections | Snapshot tables plus a live `tbl_events` query |
| Pending monthly-finalization notification | Month inferred from latest daily row | Whether a completed month appears ready to finalize | Live `tbl_unique` plus lock status |

The report center has report modes for monthly, first-half, second-half, and yearly ranges. Internally, `ReportRequest` also supports `Daily` and generic `CustomRange` granularity, but there is no separate daily-report user flow in this form. The half-year modes are represented as custom ranges.

## 3. Forms and presentation orchestration

### 3.1 `FrmReportCenter`

Primary responsibilities currently concentrated in the form are:

- selecting Persian year/month and report mode;
- validating the selected range against `AppSettingsService.GetDataStartDate()`;
- creating `ReportRequest` and selecting all station-profile parameters;
- deciding between live calculation and finalized snapshot reads;
- invoking main-data, Event/runtime, extreme-date, and recycle-change calculations;
- enforcing the incomplete-day generation gate, with Shift as an explicit live-report override;
- binding summary, unique, event, service-day, event-log, service-combination, and extreme-date grids;
- retaining generated results in `_currentGeneratedRequest`, `_currentGeneratedReportResult`, `_currentEventReportResult`, and `_currentRecycleChangeCount`;
- finalizing a monthly result and exporting a finalized monthly PDF.

Evidence: `TryBuildReportRequest` at `UI/Forms/FrmReportCenter.cs:1276`, `btnGenerateReport_Click` at line 1324, snapshot loaders at lines 1163 and 1218, finalization at line 2576, and PDF export at line 2528.

The form builds chart points through the report engine, but no chart presentation was found. The report pages are data grids rather than a chart-based output.

### 3.2 Display models

The following types in `Models/Reports` are the current data-transfer/display models:

| Model | Purpose |
|---|---|
| `ReportRequest` | Date range, granularity, selected parameter keys, Event and missing-day flags |
| `ReportResult` | Summary items, chart points, daily completeness statuses, warnings |
| `ReportSummaryItem` | Parameter metadata, aggregation kind, value, and contributing value count |
| `ReportDailyStatus` | Per-day hourly-row count, daily-unique presence, missing hours, completeness |
| `ChartPointModel` | Parameter/date/time/value chart point |
| `EventReportResult` | Unit summaries, Event log items, service-day map, warnings |
| `UnitEventSummary` | Per-Unit runtime metrics, state, Event counts, and service information |
| `EventLogItem` | Reporting representation of an Event row |
| `ExtremeDateItem` | Parameter extreme and its occurrence date(s) |
| `UnitInitialEventState` | Initial state used by legacy Event/runtime calculation |

Station and parameter metadata live in `Core/Reports`: `ReportStationProfile`, `ReportStationProfileProvider`, `ReportParameterDefinition`, and `ReportParameterRegistry`.

## 4. Service inventory

| Service | Current responsibility | Direct data dependency |
|---|---|---|
| `ReportEngineService` | Coordinates live main/daily summary, charts, completeness, warnings | Delegates to query services |
| `ReportQueryService` | Loads selected hourly and daily-unique columns as dictionaries | `tbl_data`, `tbl_unique` |
| `ReportAggregationService` | Min/max/average and sum calculations | In-memory query rows |
| `ChartDataBuilder` | Creates hourly/daily chart points | In-memory query rows |
| `ReportCompletenessService` | Checks 12 expected odd-hour rows and required daily row for each Persian day | `tbl_data`, `tbl_unique` |
| `EventReportEngineService` | Coordinates Event loading, initial state/base runtime, validation-related inputs, and legacy runtime calculation | `tbl_events`, runtime base/configuration through collaborators |
| `EventReportQueryService` | Loads Events for the requested inclusive Persian-date range | `tbl_events` |
| `EventInitialStateService` | Finds state before the report boundary | `tbl_events` |
| `UnitRuntimeBaseQueryService` | Loads Unit runtime base values | runtime base persistence |
| `EventRuntimeCalculationService` | Performs the production legacy runtime/Event calculation | In-memory Events, initial state/base/configuration |
| `EventSequenceValidationService` | Analyzes Event sequence validity | `tbl_events` |
| `ExtremeDatesService` | Calculates eligible hourly minima/maxima and occurrence dates | `tbl_data` |
| `MonthlyFinalizeStatusService` | Determines pending finalization from daily-row coverage and lock | `tbl_unique`, `tbl_monthly_lock` |
| `MonthlyFinalReportService` | Writes monthly snapshots and locks the month in one supplied transaction | monthly snapshot tables, lock table |
| `MonthlyFinalReportReadService` | Rehydrates monthly summary/Event/service snapshots | monthly snapshot tables |
| `PeriodFinalReportReadService` | Aggregates fully locked monthly summaries into a larger period | monthly snapshot reader |
| `MonthlyLockService` | Checks, enforces, and creates monthly locks | `tbl_monthly_lock` |
| `MonthlyFinalPdfService` | Produces the official locked-month PDF | snapshot readers and live `tbl_events` |
| `TestDataSeederService` | Copies/deletes sample report data for test-data support | `tbl_data`, `tbl_unique`, `tbl_events` |

`TestDataSeederService` is support tooling colocated with reports, not a calculation or export path. It must not be treated as part of the target reporting read model.

## 5. Parameter and calculation inventory

`ReportParameterRegistry` is station-scoped. Rasht defines Units U1-U3; Ramsar defines U1-U4. It keeps station-specific fields separate rather than attempting a universal profile.

Hourly `tbl_data` parameters include inlet/outlet pressures, flow/ratio/recycle, temperatures, and station-specific line or Unit status/RPM fields. Numeric analytical fields use minimum, maximum, and average. Status fields are registered but have no numeric aggregation. Daily `tbl_unique` parameters are `ir_f`, `turbine_fuel`, `turbine_flow`, `non_turbine_flow`, and `vent`; they use sum.

`ReportAggregationService.BuildSummary` parses numeric values from dictionary rows. Hourly parameters produce Min/Max/Average values; daily-unique parameters produce Sum. `ValueCount` is retained, enabling weighted recombination of averages across monthly snapshots. No evidence was found of percentile, median, standard deviation, interpolation, or missing-value imputation.

`PeriodFinalReportReadService` combines locked monthly summaries as follows:

- Min: minimum of monthly Min values;
- Max: maximum of monthly Max values;
- Sum: sum of monthly Sum values;
- Average: weighted by each monthly item’s `ValueCount`.

This combination is mathematically appropriate only if monthly snapshots contain compatible parameter definitions and semantics. The current snapshots do not carry a parameter-definition or calculation-policy version with which to prove that compatibility.

## 6. End-to-end data flows

### 6.1 Open-period operational and daily report

1. `FrmReportCenter.TryBuildReportRequest` derives an inclusive Persian date range and rejects a start before `DataStartDate`.
2. `ReportEngineService.BuildReport` resolves the station profile and selected definitions.
3. `ReportQueryService.LoadDataRows` reads selected `tbl_data` columns ordered by date/time; `LoadUniqueRows` reads `tbl_unique`.
4. `ReportAggregationService.BuildSummary` calculates hourly Min/Max/Average and daily Sum.
5. `ChartDataBuilder` creates chart points.
6. `ReportCompletenessService.CheckRange` enumerates Persian days from the applicable start boundary and checks hourly/daily coverage.
7. The form separately invokes Event/runtime, extreme-date, and recycle-change paths, then binds grids.

Event usage: none in the main/daily aggregation itself. Runtime usage: none in `ReportResult`; a separate `EventReportResult` is rendered alongside it. Daily data usage: `tbl_data` drives hourly statistics and `tbl_unique` drives summed daily values and part of completeness.

### 6.2 Open-period Event/runtime report

1. `EventReportEngineService.BuildEventReport` loads Events in the requested range.
2. It obtains initial Unit state from earlier Event/runtime-base information and current ESD configuration.
3. It invokes `EventRuntimeCalculationService.Calculate`.
4. The result supplies Unit Runtime, Runtime After OH, ESD addition, Longest Run, Event counts, final state, Event log, and service days.

The calculation is the legacy production path. It is not the isolated Phase 4 Runtime Projection Engine and is not limited to that engine’s validated-chain contract. The earlier legacy audit documents confirmed invalid-sequence and same-time ordering risks. Phase 5 must consume the future authoritative Runtime projection rather than reproduce this calculator.

### 6.3 Locked monthly report display

`LoadFinalizedMonthlyReportFromSnapshot` reads operational/daily summaries, Unit Event summaries, service days, and recycle count from monthly snapshot tables. It then recalculates an Event result and extreme dates from live tables. The locked screen is therefore a composite:

| Section | Locked display source |
|---|---|
| Operational/daily summary | Snapshot |
| Unit Event/runtime summary | Snapshot |
| Service days | Snapshot |
| Recycle count | Snapshot |
| Event log | Live `tbl_events`/legacy calculation path |
| Service combination | Live Event result |
| Extreme dates | Live `tbl_data` |

Evidence: `UI/Forms/FrmReportCenter.cs:1222-1261`.

### 6.4 Fully locked multi-month report

When every selected month is locked, `LoadFinalizedPeriodReportFromSnapshot` aggregates monthly operational/daily snapshots. Event/runtime, Event log, service days/combinations, and extreme dates are recalculated live for the whole requested period. Evidence: `UI/Forms/FrmReportCenter.cs:1163-1201`.

When only some months are locked, the “all locked” test fails and the full period follows the live path, including data inside locked months. This is a source-selection behavior, not a rewrite of the locked rows.

## 7. Finalization and locking inventory

Monthly finalization is the only finalization flow found. There is no source evidence of a reopen/unlock or snapshot-supersession workflow.

`btnFinalizeMonthlyReport_Click` requires monthly mode, an unlocked month, a matching generated request/result, and complete daily statuses. After confirmation it passes the form’s cached results to `MonthlyFinalReportService.FinalizeMonthlyReport` within a SQLite transaction. The service removes any prior same-period snapshot rows, writes header, summary, Unit Event summary, and service summary rows, validates nonempty snapshot areas, and creates the monthly lock.

The snapshot currently preserves calculated summary values, contributing counts, Unit runtime/Event fields, service totals, recycle count, station/title, data-start date, and finalization user/time. It does not preserve a source revision, source Event identity set, Runtime/Event-chain version, reporting-policy version, parameter-registry version, configuration/baseline version, snapshot schema version, completeness evidence, or override evidence.

The cached report is checked only by date range and granularity before finalization. Source tables can change after generation and before the transaction starts, so the saved result is not proven to represent the database state at lock time.

The lock is persisted in `tbl_monthly_lock`. `MonthlyLockService.EnsureDateIsEditable` is the central guard used by operational persistence paths documented in the preceding audit. The reporting subsystem itself does not expose editing.

`MonthlyFinalizeStatusService` uses distinct `tbl_unique` dates to decide whether an elapsed month is pending finalization. This differs from finalization’s `ReportCompletenessService`, which also expects 12 odd-hour `tbl_data` records per day.

## 8. Export inventory

### 8.1 PDF

PDF is the only report-file export found. `MonthlyFinalPdfService` uses QuestPDF (`QuestPDF.Fluent`, helpers, and infrastructure) with the Community license and calls `GeneratePdf`. The UI exposes it only for a locked single month via `SaveFileDialog`.

The PDF contains a title/header, operational and daily summaries, recycle information, per-Unit performance/runtime and Event counts, service distribution, and Event dates. Most values are read through monthly snapshot readers. However, `LoadEventDates` queries `tbl_events` directly for START, NSD, and ESD rows in the month (`Services/Reports/MonthlyFinalPdfService.cs:425-438`). OH is not included in that PDF event-date query. Consequently, the PDF is not derived solely from the finalized snapshot despite the UI comment saying it is.

Reusable asset: the existing QuestPDF layout and Persian-aware presentation patterns may inform a future renderer after it accepts a complete typed snapshot. The direct SQLite reads must not be reused as the target export boundary.

### 8.2 Excel

No report-center Excel export or reporting service using ClosedXML was found. The project references ClosedXML, but package presence is not evidence of a reporting export. A future Excel renderer can consume the same typed report projection/snapshot as PDF; it should not query operational tables.

### 8.3 Print

No reporting use of `PrintDocument`, `PrintDialog`, or `PrintPreviewDialog` was found. Users can print an exported PDF outside the application, but that is not an application print subsystem.

### 8.4 CSV

No CSV reporting export was found.

### 8.5 Non-report database export

`DatabaseMaintenanceService.ExportDatabase` exports a database copy. It is maintenance/backup functionality, not a report export, and must remain outside the Reporting export abstraction.

## 9. Source-to-output matrix

| Output field/section | `tbl_data` | `tbl_unique` | `tbl_events` | runtime base/settings | snapshot tables | Calculation owner |
|---|---:|---:|---:|---:|---:|---|
| Hourly min/max/average | Yes | No | No | No | Locked summary | `ReportAggregationService` |
| Daily unique sums | No | Yes | No | No | Locked summary | `ReportAggregationService` |
| Daily completeness | Yes | Yes | No | No | No | `ReportCompletenessService` |
| Runtime metrics | No | No | Yes | Yes | Locked Unit summary | legacy `EventRuntimeCalculationService` |
| Event counts/final state | No | No | Yes | Initial state | Locked Unit summary | legacy Event calculation |
| Service days | No | No | Yes | Initial state | Locked service summary | legacy Event calculation |
| Event log | No | No | Yes | No | No | Event query/calculation result |
| Extreme dates | Yes | No | No | No | No | `ExtremeDatesService` |
| Recycle changes | Yes | No | No | No | Locked service summary | `FrmReportCenter` helper |
| PDF Event dates | No | No | Yes | No | No | `MonthlyFinalPdfService.LoadEventDates` |

## 10. Inventory conclusions

The subsystem has working station-aware aggregation, Persian-period selection, completeness checks, monthly locking, monthly snapshots, multi-month summary recombination, and a QuestPDF monthly export. Its principal architectural boundary is the report form, which coordinates many direct service calls and combines separate result models.

The term “finalized report” currently covers only selected stored metrics, not a complete immutable report. This distinction is central to Phase 5 migration: the future finalized snapshot must own every displayed/exported section and all reproducibility evidence, while open projections must be explicitly live.

