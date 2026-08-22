# Legacy Report Subsystem Audit

**Repository:** `D:\Projects\RahNegar_SQLite\Rah_Negar`  
**Audit date:** 2026-08-22  
**Scope:** Read-only audit of the complete legacy reporting chain. No production source, schema, or data was changed.

## 1. Executive conclusion

The legacy reporting subsystem is a useful, functioning vertical slice, but it is not a reusable reporting architecture for the generalized RahNegar platform. It has a recognizable pipeline—parameter registry, read services, aggregation, event/runtime calculation, WinForms binding, monthly snapshotting, locking, and PDF export—and the solution builds successfully. The strongest assets are the compact report-center workflow, explicit station profiles, parameter metadata, parameterized range queries, weighted aggregation of finalized monthly averages, transactionally stored monthly summary snapshots, and database-layer lock checks used by the principal write services.

The critical limitation is that the boundary of a “finalized report” is inconsistent. Monthly finalization snapshots operational/fuel summaries, per-unit runtime/event totals, service-day totals, and recycle-change count, then locks the month. It does **not** snapshot the detailed event timeline, extreme-date evidence, daily service combinations, source-row lineage, calculation version, settings version, or a renderable report payload. The finalized UI therefore combines snapshot values with fresh calculations from mutable/live tables (`FrmReportCenter.LoadFinalizedMonthlyReportFromSnapshot`, lines 1218-1263). The PDF likewise reads snapshot totals but queries `tbl_events` again for event dates (`MonthlyFinalPdfService.GenerateMonthlyFinalPdf`, lines 20-113; `LoadEventDates`, lines 425-489). A finalized report cannot be reproduced independently of the current database and calculation code.

The event/runtime projection is the largest correctness risk. The public production path calls `CalculateLegacyCore`, not the newer state-machine core (`EventRuntimeCalculationService.Calculate`, lines 15-35). It loads only events inside the requested range and reconstructs a simplified initial state with separate “last event by type” queries (`EventReportEngineService.BuildEventReport`, lines 18-61; `EventInitialStateService.LoadInitialStates`, lines 14-69). Legacy runtime logic accepts invalid sequences, closes an active run on OH, applies ESD adjustment even if no run is active, and calculates longest run as an unclipped interval begun at the report boundary rather than replaying the authoritative chain. Those results do not satisfy the approved Event rules and must not be carried forward.

Recommendation: preserve the report-center interaction pattern, metadata-driven parameter catalogue, visual grouping, Persian period selectors, asynchronous generation, explicit incomplete-data warning, weighted finalized-period aggregation, and PDF layout concepts. Adapt station profiles, query contracts, completeness checking, locks, and snapshot storage to explicit Station/Unit identities and versioned projections. Replace runtime calculations, finalization orchestration, hybrid snapshot/live reads, form-owned SQL/calculations, and the current report persistence model. The existing UI can be reused only as a visual/workflow reference, not as the generalized platform presentation layer.

### Audit confidence and build status

- Source inspection covered all files under `Core/Reports`, `Models/Reports`, `Services/Reports`, `FrmReportCenter`, report navigation/settings integration, persistence lock call sites, and report-related schema creation.
- `dotnet build .\Rah_Negar.sln --no-restore` succeeded with **0 errors and 3 warnings**. NU1701 affects `OpenTK 3.1.0`, `OpenTK.GLControl 3.1.0`, and `SkiaSharp.Views.WindowsForms 3.119.0`, restored against .NET Framework rather than `net8.0-windows7.0`. These are build/package risks, not confirmed report-calculation failures.
- No test project or automated report tests were found. Behavioral conclusions below are code-confirmed; visual DPI and printer-specific outcomes are classified as risks requiring runtime validation where appropriate.

## 2. Exact report inventory

| Report/output | Purpose and user workflow | Sources and calculations | Output/finality | Recommendation |
|---|---|---|---|---|
| Operational Summary | User selects Persian year, optional month, and Monthly/1st Half/2nd Half/Yearly, then Generate. Displays Min/Max/Avg for configured operating measurements. | `tbl_data`; `ReportQueryService.LoadDataRows`; `ReportAggregationService.BuildSummary`; registry/profile metadata. | WinForms grid; live unless monthly snapshots are used. | **KEEP WITH ADAPTATION** |
| Fuel & Flow / Daily Unique Summary | Displays sums of `ir_f`, `turbine_fuel`, `turbine_flow`, `non_turbine_flow`, and `vent`. | `tbl_unique`; query and aggregation services. | WinForms grid; summary values snapshotted through the general summary table. | **KEEP WITH ADAPTATION** |
| Unit Event/Runtime Summary | Per-unit cumulative runtime, runtime after OH, event counts, ESD adjustment, longest run, and day/night counts. | `tbl_events`, `unit_runtime_base`, `app_settings`; Event query/initial-state/runtime services. | Grid; per-unit monthly totals snapshotted. | **REPLACE** calculations; adapt display |
| Service Days Summary | Per-unit number of days with service. | `EventReportResult.ServiceDaysByUnit`. | Grid; monthly counts snapshotted. | **REPLACE** projection; adapt display |
| Service Combination Detail | Lists each date grouped by number/identity of units in service. | Reconstructed in `FrmReportCenter.BindServiceCombinationGrid` from runtime service-day sets and a generated Persian date range. | Live grid; not snapshotted. | **REPLACE** projection; adapt UI idea |
| Event Log by Unit / Event | Chronological START/NSD/ESD/OH detail with date, time, unit, remark; display grouping changes by radio selection. | In-range `tbl_events` via `EventReportQueryService`. | Live grid; not snapshotted. | **KEEP WITH ADAPTATION** as an auditable event projection |
| Extreme Dates | Dates at which selected operational parameters reach period minimum/maximum. | Multiple direct `tbl_data` aggregate and lookup queries in `ExtremeDatesService`. | Live grid; not snapshotted. | **KEEP WITH ADAPTATION** |
| Recycle Change Count | Counts transitions of `rec` between effectively zero and nonzero. | Direct `tbl_data` query and form-owned loop in `FrmReportCenter.CalculateRecycleChanges`, lines 1964-2000. | Included in monthly service snapshot and PDF. | **REPLACE** form-owned logic with tested projection |
| Monthly Finalized Report | Freezes selected monthly summary metrics and locks source editing; selecting a locked month loads snapshot content. | Snapshot tables plus live recomputation for event logs/combinations/extremes. | Hybrid WinForms view. | **REPLACE** snapshot contract; preserve workflow |
| Finalized Half-Year/Year Report | If every included month is locked, aggregates monthly summary snapshots. | `PeriodFinalReportReadService`; event and extreme sections are recomputed from live tables. | Hybrid WinForms view. | **KEEP WITH ADAPTATION** for weighted aggregate; replace hybrid composition |
| Monthly Final PDF | Exports a locked month through Save As. Shows operational, fuel/flow, unit, service, and event-date sections. | Snapshot summary/unit/service data; live `tbl_events` event dates; QuestPDF. | PDF file, only from a locked month. | **KEEP WITH ADAPTATION** layout/export; replace source contract |
| Completeness Status/Warnings | Marks missing hourly slots and daily-unique values; blocks ordinary generation unless Shift is held and blocks finalization. | `tbl_data`, `tbl_unique`, `data_start_date`; `ReportCompletenessService`. | UI warnings/guard, not a standalone report. | **KEEP WITH ADAPTATION** |
| Pending Finalization Notification | Main-form message when the latest month appears complete and unlocked. | Latest `tbl_unique` date and distinct daily-unique dates only. | Persian notification in `FrmMain`. | **DEFECT**; incomplete definition of completeness |
| Chart Projection | `ReportResult.ChartPoints` is constructed from selected raw parameters. | `ChartDataBuilder`. | Model output only; no chart control or binding was found in `FrmReportCenter.Designer.cs`. | **REPLACE** or complete deliberately; currently incomplete/dead output |
| Daily and arbitrary custom report modes | Enum/request types support Daily and CustomRange. | `ReportRequest`. | No daily/custom-date selector exists; half-year maps internally to CustomRange. | **DEFECT/incomplete** |

There is no Excel, CSV, or print workflow in the report center. The only implemented export is monthly final PDF (`FrmReportCenter`, lines 2528-2562 and 2704-2768). Printing could be performed externally after PDF creation, but no application print command was found.

## 3. Data flow analysis

### 3.1 Main operational and daily-unique summaries

`ReportParameterRegistry.GetParameters` (`Core/Reports/ReportParameterRegistry.cs`, lines 15-165) chooses a hard-coded parameter set from the exact strings `Rasht Station` and `Ramsar Station`. `ReportStationProfileProvider.GetProfile` (`Core/Reports/ReportStationProfile.cs`, lines 22-55) supplies the unit list (Rasht U1-U3; Ramsar U1-U4). This isolation is a good legacy safeguard, although display names are being used as identity keys.

`FrmReportCenter.TryBuildReportRequest` (lines 1272-1321) derives an integer Persian date range, selects every parameter in the active profile, and enables missing-day/event flags. `ReportEngineService.BuildReport` (`Services/Reports/ReportEngineService.cs`, lines 27-98) filters requested keys to registry definitions, loads raw dictionaries, calls aggregation and chart services, then optionally checks completeness. `IncludeEvents` is not consumed by this engine; the form independently invokes `EventReportEngineService`, so the request contract overstates orchestration.

`ReportQueryService.LoadDataRows` and `LoadUniqueRows` (`Services/Reports/ReportQueryService.cs`, lines 23-123) read directly from `tbl_data`/`tbl_unique` with parameterized date bounds and explicit ordering. Column identifiers are dynamically assembled from internal registry metadata. Results are weakly typed dictionaries; conversion, null handling, and schema coupling leak into the calculation layer.

`ReportAggregationService.BuildSummary` (`Services/Reports/ReportAggregationService.cs`, lines 20-123) produces Min/Max/Avg/Sum and `ValueCount`. Values that cannot be parsed by `double.TryParse(rawValue.ToString())` under current culture are silently discarded. SQLite numeric values normally arrive as numeric CLR values, limiting the immediate failure surface, but this is not a robust generalized contract.

### 3.2 Event, runtime, service-day, and event-log flow

`EventReportEngineService.BuildEventReport` (`Services/Reports/EventReportEngineService.cs`, lines 18-61) coordinates five sources:

1. Events inside the requested range from `tbl_events`.
2. Initial running state inferred from prior event-type-specific queries or `unit_runtime_base.initial_is_running`.
3. Cumulative baselines from `unit_runtime_base.base_runtime_hours`.
4. After-OH baselines from `unit_runtime_base.base_runtime_after_oh_hours`.
5. ESD settings from `app_settings` through a separate connection-owning settings service.

`EventReportQueryService.LoadEvents` (`Services/Reports/EventReportQueryService.cs`, lines 42-101) orders by `date_rep,event_time`, normalizes values, and then orders by converted Gregorian `EventDateTime` and Unit. Invalid/blank time is silently changed to `00:00`; unsupported/blank records may be skipped. No event ID is projected, so deterministic ordering of same-unit same-time records is unavailable.

`EventInitialStateService.LoadInitialStates` (`Services/Reports/EventInitialStateService.cs`, lines 14-69) performs four last-event queries per unit. Running is determined by comparing the last START with only NSD/ESD; OH is not treated as a state transition in that comparison. This is both N+1 query behavior and a semantic mismatch with the approved three-state chain.

The result is bound by the form into event summary, service-day, service-combination, and event-log grids (`FrmReportCenter`, lines 1447-1461 and binding methods around lines 1570-1955). Consequently one defective runtime projection contaminates four visible report families and the finalized snapshot.

### 3.3 Extreme dates and recycle count

`ExtremeDatesService.Calculate` (`Services/Reports/ExtremeDatesService.cs`, lines 24-184) supports a fixed subset: inlet pressure, outlet pressure, flow, outlet temperature, and ambient temperature. It runs a Min/Max query and separate date queries per parameter using a floating tolerance. This is correct in intent and parameterized, but it creates up to three queries per parameter, is tied directly to `tbl_data`, and snapshots neither values nor source dates.

Recycle transitions are calculated inside the form from ordered `rec` values (`FrmReportCenter.CalculateRecycleChanges`, lines 1964-2000). It counts state changes between `abs(value) < 0.000001` and nonzero, skips nulls, and does not expose the definition/version in the snapshot. It is a calculation concern in the presentation layer.

### 3.4 Live generation routing

`btnGenerateReport_Click` (`FrmReportCenter`, lines 1324-1474) clears the cache, builds a request, routes locked months or fully locked periods to snapshot readers, and otherwise runs calculations in `Task.Run`. If incomplete data exists, normal generation stops; holding Shift permits an explicitly warned provisional report. Async execution and the explicit override are useful UX patterns. The connection is local to the worker, but settings access opens another connection and the several report queries do not share a read transaction, so a concurrent write can yield a mixed-time report.

## 4. Runtime and calculation audit

### 4.1 Classification against approved rules

| Calculation | Current behavior/evidence | Classification |
|---|---|---|
| Operational Min/Max/Avg | Correct conventional aggregation over available `tbl_data` values; metadata-driven. Does not assert expected sample count per metric. | **KEEP WITH ADAPTATION** |
| Daily-unique sums | Sums registered `tbl_unique` values; matches current reporting intent. | **KEEP WITH ADAPTATION** |
| Finalized multi-month Min/Max/Sum | Min of monthly minima, max of maxima, sum of sums (`PeriodFinalReportReadService`, lines 16-92). | **KEEP** algorithm, adapt types/versioning |
| Finalized multi-month Avg | Correctly weights monthly averages by `ValueCount` (`PeriodFinalReportReadService.CalculateWeightedAverage`, lines 99-120). | **KEEP** |
| Runtime authority | Uses Events and baselines, not hourly ST/RPM. | **KEEP** principle |
| Public runtime path | `Calculate` immediately calls `CalculateLegacyCore`; state-machine core is private and unused (lines 15-35, 169 onward). | **DEFECT** |
| START handling | A START while already running closes the prior run and starts another (`HandleStartEvent`, lines 597-632), accepting an invalid sequence. | **DEFECT** |
| NSD/ESD handling | Stop events are counted even while stopped. ESD adjustment is added before checking an active run (`HandleStopEvent`, lines 634-690). | **DEFECT** |
| OH handling | If running, OH closes and terminates the run, then resets after-OH (`HandleOverhaulEvent`, lines 695-729). Approved Running+OH must be rejected. | **DEFECT** |
| Physical runtime separation | `RuntimeHours` contains physical elapsed hours plus ESD extra; only `EsdExtraHoursTotal` separately exposes the adjustment. No explicit period physical/adjusted outputs. | **REPLACE** |
| RuntimeAfterOH | Physical run increments it and OH resets it; ESD extra increments it. Core concept is right, but invalid event acceptance corrupts it. | **KEEP WITH ADAPTATION** |
| ServiceDay | `AddServiceDaysForRange` marks every Gregorian calendar date touched by a positive run and converts it to Persian (lines 791-804). ESD adjustment alone does not mark a day. | **KEEP WITH ADAPTATION**; require authoritative replay and clipping tests |
| LongestRun | Physical duration only and ESD excluded (`CloseRuntimeRun`, lines 733-748), but legacy range seeding clips runs at period start without reconstructing the real run and invalid transitions are accepted. | **REPLACE** implementation |
| Day/night event counts | Correct boundaries 07:00 inclusive and 19:00 exclusive (`IsDayShiftTime`, lines 806-812). | **KEEP** |
| Monthly/daily boundaries | Persian dates are converted through `PersianCalendar`; end is next Persian date at 00:00. | **KEEP WITH ADAPTATION** |
| Initial running state | Falls back to baseline, but reconstructs prior status with separate last-type queries and ignores full state replay. | **REPLACE** |
| Service combinations | Derived from service-day sets; identity combinations and zero-unit days are display-computed. Snapshot stores only counts by number of active units, not identities/dates. | **REPLACE** projection/storage |
| Recycle transitions | Coherent thresholded state-change algorithm, but form-owned and versionless. | **KEEP WITH ADAPTATION** |

The approved target requires Events as the sole runtime authority; this principle is already present. No ST/RPM field enters `EventRuntimeCalculationService`. However, “Events authority” is insufficient without validating and replaying the full authoritative Unit chain from a trusted baseline. The dormant `CalculateStateMachineCore` and `LoadRuntimeHistoryForComparison` explicitly show an attempted newer path, but neither is used by public report generation (`EventReportEngineService`, lines 64-84). It must not be mistaken for production behavior.

## 5. Finalization and locking audit

### 5.1 Monthly finalization workflow

The user generates a complete monthly report, clicks **Finalize Month**, confirms, and the form begins a SQLite transaction (`FrmReportCenter.btnFinalizeMonthlyReport_Click`, lines 2576-2694). `MonthlyFinalReportService.FinalizeMonthlyReport` validates basic arguments and lock status, deletes any pre-existing snapshot for the period, inserts header/summary/unit-event/service rows, validates nonempty sections, and calls `MonthlyLockService.LockMonth` in the same transaction (`Services/Reports/MonthlyFinalReportService.cs`, lines 18-104 and 111-514). Rollback occurs on exception. This atomic write-and-lock boundary is a strong pattern worth preserving.

There is nevertheless a time-of-check/time-of-use defect. The report is generated earlier outside the finalization transaction. `IsGeneratedReportCurrent` checks only DateFrom, DateTo, and Granularity (`FrmReportCenter`, lines 2477-2485), not source revisions, selected keys, settings, baseline version, or a data hash. The finalization transaction stores the cached in-memory results without rereading or locking the sources (lines 2609-2673). A source edit between Generate and Finalize can create a snapshot inconsistent with the database being locked.

### 5.2 Snapshot contents and immutability

Schema creation is in `Services/StartupSetupService.cs`, lines 87-185. Snapshot/lock uniqueness is only `(year_rep,month_rep)` because each legacy database represents one station. Tables have no foreign keys, snapshot version, calculation version, source revision, checksum, report status, supersession link, or station ID. Two declared tables—`tbl_monthly_report_unique_summary` and `tbl_monthly_report_event_summary`—have no read/write references outside schema creation and are dead/abandoned schema. The implemented general summary table also stores unique sums.

`LoadFinalizedMonthlyReportFromSnapshot` explicitly documents that main numbers are stored while analytical sections are recalculated (`FrmReportCenter`, lines 1213-1263). It loads snapshot summary, event summary, and service totals, but recomputes event details and extreme dates live. `LoadFinalizedPeriodReportFromSnapshot` (lines 1163-1211) aggregates snapshot main summaries while likewise rebuilding event/extreme sections. This is a **HIGH** integrity defect: the screen labeled finalized can change after finalization if a bypass or external database edit changes live tables, and it is not independently reproducible after migration.

The PDF is also hybrid. `MonthlyFinalPdfService.GenerateMonthlyFinalPdf` loads snapshot summaries (`Services/Reports/MonthlyFinalPdfService.cs`, lines 20-113), but `LoadEventDates` reads current `tbl_events` (lines 425-489). OH dates and remarks are excluded from the timeline. The exported artifact therefore is not guaranteed to represent the data present at finalization.

### 5.3 Lock coverage and reopening

`MonthlyLockService.EnsureDateIsEditable` performs a below-UI query using the caller's connection and transaction (`Services/Reports/MonthlyLockService.cs`, lines 16-46). It is called from `DailyDateService`, `CommonRecordPersistenceService`, `RashtRecordSaveService`, and `RamsarRecordPersistenceService`; Rasht batch save checks main, unique, and each event date. This is materially better than UI-only locking.

There is no supported unlock/reopen service or UI. `LockMonth` can only set `is_locked=1` (`MonthlyLockService`, lines 76-111). Direct SQLite access can bypass application locks because the source tables have no triggers tying dates to `tbl_monthly_lock`. For the current offline trusted-client model this is defense-in-depth rather than a remote security boundary, but migration tooling and maintenance paths must explicitly honor locks.

`MonthlyFinalizeStatusService` is defective as a readiness signal. It checks only distinct dates in `tbl_unique` (`Services/Reports/MonthlyFinalizeStatusService.cs`, lines 18-94), not the 12 required hourly rows or parameter validity. It can announce a month as ready when main data is incomplete, although finalization itself later uses the stronger completeness result and refuses it.

## 6. Report UI audit

### Strengths to preserve

- A single compact `FrmReportCenter` provides period selection, generation, finalization, export, and tabbed report views.
- Persian month names and Persian integer date ranges match the existing operators' workflow (`LoadMonths`, lines 168-190; `GetDateRange`, lines 1075-1084).
- Monthly, first-half, second-half, and yearly shortcuts reduce repetitive date entry.
- Generation runs off the UI thread and freezes redraw during batch binding, reducing visible flicker (`btnGenerateReport_Click`; `BeginFormUpdate`/`RunGridUpdate`).
- Read-only, grouped grids make event and service evidence scannable.
- Incomplete data normally blocks output, while Shift override clearly labels a provisional result.
- Finalize and PDF actions are context-enabled, and Save As avoids silent overwrite.

### Weaknesses and classification

| UI behavior | Finding | Classification |
|---|---|---|
| Form architecture | `FrmReportCenter.cs` is about 2,700 lines and owns SQL, Persian conversion, calculations, cache, finalization, export routing, and grid rendering. | **REPLACE** internal architecture |
| Period selectors | Useful shortcuts, but no Daily/custom-date UI despite model enum support. Year list comes only from `tbl_unique`, excluding event-only or main-data-only years. | **ADAPT** |
| Localization | Mixed Persian and English: buttons, modes, section values, errors, grid values (“No Unit”), and PDF are predominantly English while validation dialogs vary. | **ADAPT** |
| Station/Unit filters | Station is fixed by current database settings; no station selector. Unit is not a report filter; only event-log grouping changes. Appropriate for legacy single-station deployment, insufficient for generalized platform. | **REPLACE** filtering contract |
| Export | Locked monthly PDF only; no preview, print, CSV, Excel, or accessible export for live/period reports. | **ADAPT** |
| Chart | Chart data is calculated but no visible chart control/binding exists. | **DEFECT/incomplete** |
| Cache | Cache validity ignores source/settings/baseline revision and most request fields. | **DEFECT** |
| DPI/layout | Designer uses many fixed pixel positions/sizes and compact 8–8.5pt Tahoma controls (`FrmReportCenter.Designer.cs`, lines 150-274 and grid sections). Base theming helps, but clipping at high DPI requires runtime verification. | **ADAPT** risk, not confirmed visual bug |
| Keyboard workflow | Standard tab indices exist, but no documented accelerators/shortcuts for Generate/Finalize/PDF and Shift has a hidden high-impact override behavior. | **ADAPT** |
| Error behavior | Top-level exceptions are shown, but messages mix generic Persian text and raw exception details; no structured report error model. | **ADAPT** |

## 7. Database and report-query audit

### Positive properties

- Date-bound values use SQLite parameters throughout inspected report queries.
- Main report reads explicitly order chronological sources.
- `tbl_unique` has a unique date index; event date/time/unit indexes support common filters (`Core/CommonDataSchema.cs`, lines 43-56).
- Final snapshot rows have uniqueness constraints appropriate to the current one-station-per-database deployment.
- Weighted averages retain effective `ValueCount`, enabling correct roll-up.

### Risks

1. **HIGH — Snapshot identity is not generalized.** Snapshot and lock keys omit StationId and calculation/snapshot version (`StartupSetupService`, lines 87-185). Combining stations in one database would collide.
2. **HIGH — Source/snapshot referential integrity is absent.** Snapshot tables have no foreign keys, header-child foreign key, cascading policy, or immutable trigger.
3. **HIGH — `tbl_data` permits duplicate `(date_rep,time_rep)`.** Both station schemas create only a nonunique index (`Core/RashtDataSchema.cs`, lines 10-43; `Core/RamsarDataSchema.cs`, lines 14-47). Duplicate hours bias averages and can make completeness fail unpredictably.
4. **HIGH — `tbl_events` permits duplicate same-Unit timestamps.** Its index is nonunique (`CommonDataSchema.GetCommonIndexSqlList`, lines 43-56). Runtime ordering and results can be ambiguous.
5. **MEDIUM — Report generation lacks one read snapshot.** Main, completeness, events, settings, extreme, and recycle queries execute sequentially without a shared read transaction.
6. **MEDIUM — N+1 query patterns.** Initial event state executes four queries per Unit; extreme dates use up to three per parameter.
7. **MEDIUM — Direct table coupling and dictionaries.** Query services expose column-name dictionaries rather than typed source projections, making schema evolution and station isolation fragile.
8. **MEDIUM — Silent normalization.** Invalid event times become `00:00`; invalid numeric conversions are omitted rather than reported.
9. **MEDIUM — Missing indexes for generalized queries.** There is no station dimension; `tbl_data` lacks uniqueness; events lack a unique Unit/timestamp key and canonical timestamp. Existing indexes are adequate only for small single-station legacy data.
10. **CODE QUALITY — Dead snapshot tables.** `tbl_monthly_report_unique_summary` and `tbl_monthly_report_event_summary` are created but unused.

## 8. Confirmed defects and severity

### RPT-01 — Finalized reports mix immutable snapshots with live data

- **Severity:** HIGH
- **Location:** `UI/Forms/FrmReportCenter.cs`, `LoadFinalizedMonthlyReportFromSnapshot` lines 1218-1263 and `LoadFinalizedPeriodReportFromSnapshot` lines 1163-1211; `Services/Reports/MonthlyFinalPdfService.cs`, `LoadEventDates` lines 425-489.
- **Evidence:** Snapshot totals are loaded, while event logs/service combinations/extreme dates and PDF event dates are queried or recalculated from live tables.
- **Failure scenario:** A maintenance tool, legacy bypass, or migration changes an event after finalization. The finalized screen/PDF detail changes while stored totals do not, producing internal contradiction.
- **Recommended fix:** Version and persist a complete report projection or immutable source revision; finalized UI/export must consume one snapshot identity only.

### RPT-02 — Production runtime report executes legacy calculation path

- **Severity:** CRITICAL
- **Location:** `Services/Reports/EventRuntimeCalculationService.cs`, `Calculate` lines 15-35; `CalculateLegacyCore` lines 37-166.
- **Evidence:** Public `Calculate` directly returns `CalculateLegacyCore`; newer state-machine logic is private and uncalled.
- **Failure scenario:** Reports and final snapshots publish runtime derived from invalid state sequences.
- **Recommended fix:** Replace with the approved full-chain, baseline-based Event projection and parity/audit migration; do not merely switch to the dormant core without validation.

### RPT-03 — Invalid Event sequences alter reported runtime

- **Severity:** CRITICAL
- **Location:** `EventRuntimeCalculationService.HandleStartEvent`, lines 597-632; `HandleStopEvent`, lines 634-690; `HandleOverhaulEvent`, lines 695-729.
- **Evidence:** Repeated START closes/reopens a run; stopped ESD receives adjustment; running OH closes the run.
- **Failure scenario:** A malformed historical chain inflates runtime, changes longest/service days, or terminates a run contrary to approved rules.
- **Recommended fix:** Validate/replay complete per-Unit chains and reject or quarantine invalid history before projection.

### RPT-04 — Finalization can snapshot stale in-memory calculations

- **Severity:** HIGH
- **Location:** `FrmReportCenter.IsGeneratedReportCurrent`, lines 2477-2485; `btnFinalizeMonthlyReport_Click`, lines 2609-2673.
- **Evidence:** Cache validation compares only period/granularity; final transaction persists prior results without rereading sources.
- **Failure scenario:** Data/settings/baselines change between Generate and Finalize; stale results are frozen and the newer source rows become locked.
- **Recommended fix:** Recompute or verify a source revision inside one finalization transaction.

### RPT-05 — Pending-finalization notification uses an incomplete completeness definition

- **Severity:** MEDIUM
- **Location:** `Services/Reports/MonthlyFinalizeStatusService.cs`, `IsMonthComplete` lines 55-94.
- **Evidence:** Only distinct `tbl_unique` dates are counted; required hourly rows are not checked.
- **Failure scenario:** User is told a report is ready, then report generation/finalization rejects missing hourly data.
- **Recommended fix:** Reuse one authoritative completeness service/projection.

### RPT-06 — Same-time Event order is not deterministic or protected

- **Severity:** HIGH
- **Location:** `Core/CommonDataSchema.cs`, lines 29-56; `EventReportQueryService.LoadEvents`, lines 42-101.
- **Evidence:** No unique Unit+timestamp constraint; event ID is not loaded; sorting ends at timestamp and Unit.
- **Failure scenario:** Same-Unit same-time START/NSD rows yield source-order-dependent state/runtime.
- **Recommended fix:** Detect legacy duplicates, enforce canonical unique Unit/timestamp, and include stable identity in projections.

### RPT-07 — Duplicate hourly samples can bias reports

- **Severity:** HIGH
- **Location:** `Core/RashtDataSchema.cs` and `Core/RamsarDataSchema.cs`, `GetIndexSqlList`; `ReportAggregationService.BuildSummary`.
- **Evidence:** `(date_rep,time_rep)` index is nonunique and aggregations consume every row.
- **Failure scenario:** Accidental duplicate hour changes Min/Max/Avg and makes completeness inconsistent.
- **Recommended fix:** Audit duplicates before adding a station/date/time uniqueness rule; define correction workflow.

### RPT-08 — Invalid stored event time silently becomes midnight

- **Severity:** MEDIUM
- **Location:** `EventReportQueryService.NormalizeTime`, lines 124-139.
- **Evidence:** Any unparsable time returns `00:00`.
- **Failure scenario:** Corrupt history shifts runtime, day boundary, day/night count, and chronological placement without warning.
- **Recommended fix:** Treat invalid source data as an explicit projection error requiring review.

### RPT-09 — Chart output is produced but not presented

- **Severity:** LOW
- **Location:** `Services/Reports/ChartDataBuilder.cs`; `ReportEngineService.BuildReport`; no chart in `FrmReportCenter.Designer.cs`.
- **Evidence:** Chart points are built into the result, but no report-center binding/control was found.
- **Failure scenario:** Calculation and dependency cost are paid for an inaccessible feature.
- **Recommended fix:** Either specify and implement the chart in the new platform or remove it from the report contract during implementation planning.

## 9. Historical and migration risks

- Recalculating history with the approved Event engine will legitimately disagree with stored finalized runtime where invalid transitions, running OH, stopped ESD, duplicate timestamps, or incomplete pre-range history exist. Preserve legacy published figures and label their calculation lineage; do not silently overwrite them.
- Existing finalized periods are not self-contained. Migration must retain the legacy database read-only and capture missing detail/evidence before any source cleanup.
- Legacy baseline values and initial-running flags have no effective date/version. Their relationship to each finalized month cannot be proven from snapshot rows.
- Snapshot headers contain UTC text timestamps and station display name but no stable station ID, schema version, engine version, time-zone metadata, or input checksum.
- Direct database changes can produce locked-source/snapshot divergence. Migration needs reconciliation: snapshot totals versus recalculated legacy engine versus new approved engine.
- Duplicate `tbl_data` samples and duplicate Event timestamps must be enumerated before introducing constraints.
- Invalid Persian integer dates/times may throw during conversion or be normalized silently. Validate all historical boundaries, especially Esfand leap days and year transitions.
- The one-file/one-station assumption keeps current snapshot keys workable. Consolidating Rasht and Ramsar requires explicit StationId on every fact, projection, lock, and snapshot identity.

## 10. Target reporting architecture recommendation

```text
Versioned source repositories
  (Events, hourly observations, daily values, station/unit config, baselines)
                    ↓
Domain calculation layer
  (authoritative Event runtime + typed operational aggregations)
                    ↓
Report projection layer
  (daily/monthly/period projections, completeness, evidence/lineage)
                    ↓
Presentation/export layer
  (WinForms/web-neutral view models, Persian UI, PDF/other exports)
```

Reports should consume typed, station-scoped projections, not table-shaped dictionaries or controls. Runtime projection must consume only Trusted Runtime Baseline plus the complete ordered Event chain; ST/RPM remains an independent operational observation. Operational and daily-value reports may consume their respective observations. A report request should carry StationId, period, parameter set, culture/calendar, projection version, and requested sections.

Live generation should execute against one consistent database read transaction or an explicit source revision. The calculation layer returns physical runtime, ESD adjustment, adjusted runtime, runtime after OH, service days, and longest physical period-clipped run separately. The report projection composes these into named, versioned fields.

Finalization should be an application command that, within one transaction, verifies authorization and unlocked state, validates completeness, reads a consistent source revision, generates the complete projection, stores the full snapshot/evidence plus engine/config versions and checksum, and locks the Station+period. A finalized view and every export must read only that snapshot version. Reopening, if authorized, should create an audited superseding version rather than mutate the original snapshot.

Preserve the legacy profile/parameter idea, but key it by stable station type/parameter IDs rather than English display strings. Keep Rasht/Ramsar logic isolated behind profiles. Retain Persian presentation while storing canonical instants/dates and explicit operating-date semantics.

## 11. Required automated tests before reuse

### Operational and daily reports

1. Min/Max/Avg with complete 12-slot day, multiple days, null policy, negative/zero values, and exact `ValueCount`.
2. Duplicate hourly timestamp is rejected or reported; never silently double-weighted.
3. Daily unique sums for each registered Rasht/Ramsar parameter.
4. Weighted average across months with unequal counts; null and zero-count months.
5. Registry isolation: Rasht-only fields never query Ramsar and vice versa.
6. One consistent read under a simulated concurrent source update.

### Completeness

7. Exactly the 12 odd-hour observations plus required daily value is complete.
8. Each missing hour, duplicate hour, malformed time, missing daily row, and day before `data_start_date`.
9. Partial first month beginning at `data_start_date`.
10. Pending-finalization status exactly matches finalization eligibility.

### Runtime/Event reports

11. Complete approved transition matrix for Stopped, Running, and StoppedAfterOH.
12. Full-chain reconstruction from Trusted Runtime Baseline for Add/Edit/Delete-equivalent histories.
13. Physical runtime, ESD adjustment, adjusted runtime, and runtime-after-OH asserted separately.
14. ESD adjustment only for Running→ESD; it creates no ServiceDay and does not extend LongestRun.
15. OH while stopped resets after-OH only; Running+OH rejected.
16. Longest run is physical, ESD-excluding, and period-clipped.
17. Positive overlap at 00:00 boundary creates service on the correct operating day; zero overlap does not.
18. Day/night boundaries at 06:59, 07:00, 18:59, 19:00.
19. Same Unit/timestamp rejected; different Units/same timestamp allowed.
20. Invalid legacy event type/time/date produces explicit migration error, never midnight coercion.

### Persian and historical boundaries

21. Every Persian month length, leap/non-leap Esfand, month/year transition, and multi-year range.
22. `data_start_date` on first/middle/last day of month.
23. Baseline before first Event, initially running Unit, and no-event period.
24. Recalculate fixed legacy fixtures with both legacy and approved engines and persist explained deltas.

### Finalization, locking, and snapshots

25. Complete projection and lock commit atomically; injected failure at every insert rolls everything back.
26. Source mutation between preview and finalize is detected; stale preview cannot be finalized.
27. All main/daily/Event persistence paths reject a finalized Station+month below UI.
28. Finalized report/UI/PDF remain byte/value-equivalent after live source tables or application settings change.
29. Half-year/year projection uses only one set of finalized versions and correct weighted averages.
30. Station isolation: same year/month can be finalized independently for Rasht and Ramsar.
31. Authorized reopen/supersession preserves original snapshot and audit history.
32. Snapshot carries engine, schema, baseline, settings, culture/calendar, source revision, and checksum.

### UI/export

33. Monthly/half/year/custom/daily selection and invalid-range Persian messages.
34. Keyboard-only generation, filter traversal, grouping, finalization confirmation, and export.
35. 100%, 125%, 150%, 175%, and 200% DPI with Persian/English text and minimum supported window.
36. PDF pagination, repeated headers, Persian glyphs/remarks, event evidence, units, rounding, and deterministic output from snapshot.
37. Provisional incomplete report is visibly marked in UI and exported artifact; finalized output cannot use override.

## 12. KEEP / KEEP WITH ADAPTATION / REPLACE / DEFECT inventory

| Classification | Inventory |
|---|---|
| **KEEP** | Events-only runtime authority principle; 07:00/19:00 shift boundary; parameterized query values; Persian month-length helper usage; weighted finalized-period average; Min-of-min/Max-of-max/Sum-of-sums algorithms; atomic snapshot-write plus lock concept. |
| **KEEP WITH ADAPTATION** | Report-center workflow; Persian period shortcuts; read-only grouped grids; metadata-driven parameters; station profiles; operational/daily aggregation; completeness warning and deliberate provisional override; extreme-date concept; event log; service-day presentation; QuestPDF layout/export; source-level lock checks. |
| **REPLACE** | Form-owned orchestration/SQL/calculations; dictionary-based repository result; display-name station identity; runtime projection and initial-state logic; hybrid finalized report composition; snapshot schema/identity; service-combination projection; cache validity; generalized filtering; finalization command boundary. |
| **DEFECT** | Public legacy runtime path; invalid transition acceptance; stale preview finalization; live detail in finalized UI/PDF; incomplete pending-finalization check; ambiguous duplicate Event order; nonunique hourly samples; invalid Event time coerced to midnight; chart output with no UI; declared-but-unused snapshot tables; Daily/custom request modes without corresponding UI. |

## 13. Final verdict

| Question | Verdict | Rationale |
|---|---|---|
| Can current report UI be reused? | **Yes, only as a UX/layout reference — KEEP WITH ADAPTATION.** | Compact filters, grouped grids, asynchronous Generate, explicit finalization, and PDF workflow are valuable. The form's mixed responsibilities, fixed sizing/localization, limited filtering, and hybrid snapshot routing prevent direct reuse. |
| Can current report calculations be reused? | **Partially.** | Conventional operational sums/min/max/weighted averages are reusable after typed tests. Runtime, initial-state, service-day chain integration, and form-owned calculations must be replaced. |
| Can current report database queries be reused? | **Only as query-intent references.** | Parameterization and ordering are useful, but queries are directly coupled to legacy tables, not station-scoped/versioned, and do not share a consistent read snapshot. |
| Can current finalize/lock system be reused? | **Conceptually yes; implementation no.** | Atomic snapshot-and-lock and below-UI checks are strong. The snapshot is incomplete, stale preview can be finalized, identity omits Station/version, and there is no audited reopen/supersession model. |

The generalized platform should preserve the familiar operator workflow and a small set of proven aggregation ideas, while replacing the business-critical projection, finalization, snapshot, and persistence boundaries. Legacy finalized artifacts must remain available as legacy-version evidence; they must not be silently reinterpreted as outputs of the approved Event engine.
