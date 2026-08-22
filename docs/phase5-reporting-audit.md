# Phase 5.1 Reporting Inventory and Gap Analysis

## 1. Executive assessment

Phase 5.1 found a functional but tightly coupled legacy reporting subsystem. It produces station-specific operational summaries, daily sums, Event/runtime summaries, service analysis, extreme dates, and a locked-month PDF. Its main calculations match the broad legacy intent: minimum/maximum/average for applicable hourly data and sum for daily unique values.

The current finalization guarantee is incomplete. A locked monthly screen and its official PDF can include live queries alongside stored snapshot values. The snapshot also lacks source and policy version evidence, and finalization accepts cached in-memory calculations without proving that source data remained unchanged. These are confirmed reproducibility gaps and are the highest-priority reporting risks before implementing the Phase 5 target architecture.

This audit did not modify production code, existing reports, or the database schema.

## 2. Audit basis and method

The assessment traced `FrmReportCenter` from request construction through live generation, snapshot selection, grid binding, finalization, locking, and PDF export. It inspected all types in `Core/Reports`, `Models/Reports`, and `Services/Reports`, then searched the solution for report forms and PDF, Excel, CSV, and print implementations.

The following references established approved direction and previously verified findings:

- `docs/legacy-report-subsystem-audit.md` — detailed legacy evidence and confirmed defects;
- `docs/reporting-architecture-specification.md` — target projection, snapshot, finalization, and export boundaries;
- `docs/master-implementation-roadmap.md` — Phase 5 sequencing and dependence on authoritative Event and Runtime domains.

No concern below is labeled a confirmed defect solely from speculation. Items that require product/domain choice are identified as migration decisions.

## 3. Current architecture

```text
FrmReportCenter
  |-- ReportEngineService
  |     |-- ReportQueryService --------> tbl_data / tbl_unique
  |     |-- ReportAggregationService
  |     |-- ChartDataBuilder
  |     `-- ReportCompletenessService --> tbl_data / tbl_unique
  |-- EventReportEngineService
  |     |-- EventReportQueryService ----> tbl_events
  |     |-- EventInitialStateService ---> tbl_events
  |     |-- UnitRuntimeBaseQueryService -> runtime base
  |     `-- EventRuntimeCalculationService (legacy production calculation)
  |-- ExtremeDatesService -------------> tbl_data
  |-- MonthlyFinalReportService --------> snapshot tables + monthly lock
  |-- MonthlyFinalReportReadService ----> snapshot tables
  |-- PeriodFinalReportReadService -----> monthly snapshots
  `-- MonthlyFinalPdfService -----------> snapshots + live tbl_events
```

The presentation layer is the composition root and also owns some analytical behavior, including recycle-change calculation and service-combination binding. Repositories are represented by static query services using `SqliteConnection`; there is no typed reporting application boundary that can switch cleanly between open projections and immutable snapshots.

## 4. Report-by-report flow findings

### 4.1 Operational and daily summaries

Inputs are selected columns from `tbl_data` and `tbl_unique`. `ReportParameterRegistry` supplies station-specific parameter definitions and aggregation kinds. `ReportAggregationService` produces Min/Max/Average for numeric hourly parameters and Sum for daily unique values. The output is `ReportResult.SummaryItems`; chart points and completeness statuses accompany it.

Events and Runtime do not participate in these aggregations. They are calculated separately and combined only in the form. This separation is useful, but the lack of an encompassing typed projection makes source consistency difficult to enforce.

Evidence: `Services/Reports/ReportEngineService.cs:27-78`, `ReportQueryService.cs:23-105`, and `ReportAggregationService.cs:20-100`.

### 4.2 Event and Runtime sections

Inputs are in-period Events, pre-period initial state, Unit runtime base, and ESD configuration. The output is `EventReportResult` with `UnitEventSummary`, Event log entries, service days, and warnings. `EventRuntimeCalculationService` is still the legacy production calculation path. The new isolated Phase 4 Runtime Projection Engine is not connected, consistent with the instruction not to replace legacy behavior yet.

The prior audit confirmed that invalid Event sequences and nondeterministic same-time ordering can alter legacy runtime results. Reporting therefore cannot become authoritative until it consumes a validated Event Chain and the approved Runtime Projection boundary. Phase 5 must integrate those outputs, not embed or duplicate their rules.

Evidence: `Services/Reports/EventReportEngineService.cs:18-50`, `EventReportQueryService.cs:40-70`, and `EventRuntimeCalculationService.cs:15`.

### 4.3 Service and extreme-date analysis

Service days and Unit combinations derive from Event/runtime results. ESD addition does not have a separate reporting projection contract; it arrives as a field of the legacy Unit summary. Extreme dates independently query `tbl_data`. Recycle-change analysis is a form helper. These parallel calculations are not captured under one source revision or transaction.

### 4.4 Locked monthly view

Operational/daily, Unit Event/runtime, service-day, and recycle fields come from snapshot tables. Event log, service combinations, and extreme dates are live. The source mixture is explicit in `LoadFinalizedMonthlyReportFromSnapshot` (`UI/Forms/FrmReportCenter.cs:1218-1261`). A locked view can therefore be internally inconsistent even if ordinary editing guards usually prevent source changes.

### 4.5 Locked half-year/year view

When every month is locked, only main/daily summary values are recombined from monthly snapshots. Event/runtime and analytical sections are recalculated live for the whole range (`UI/Forms/FrmReportCenter.cs:1163-1201`). When at least one selected month is open, the entire period takes the live path. The UI does not label this mixed source policy to the user.

## 5. Finalize, lock, and reproducibility analysis

### 5.1 Open periods

Open-period reports are on-demand calculations. Event changes, hourly/daily edits, and current configuration can change the next result. This aligns conceptually with the approved Runtime open-projection policy, but the report result has no source-revision metadata and calculations are split across independent services.

The form allows report generation with incomplete data only while Shift is held. Monthly finalization itself rejects cached results containing incomplete days. This override affects viewing, not finalization.

### 5.2 Monthly finalization

The form saves previously generated in-memory objects. `IsGeneratedReportCurrent` checks period and granularity, not source identity or revision (`UI/Forms/FrmReportCenter.cs:2477-2484`). Database records may change between generation and finalization. `MonthlyFinalReportService` writes snapshots and the lock transactionally, which protects write atomicity, but it does not recalculate or validate source freshness within that transaction.

### 5.3 Snapshot contents

Stored monthly snapshots cover summary values, Unit Event/runtime summaries, service data, and recycle count. They do not cover the Event log, extreme dates, service combinations as a complete rendered section, or PDF Event dates. Metadata is insufficient for exact replay: no Event-chain version, Runtime policy/baseline/calculation versions, reporting policy version, source revision, parameter registry version, snapshot format version, or checksum is stored.

### 5.4 Lock protection

Lock creation occurs with snapshot writes. Existing persistence paths call the monthly edit guard, as recorded by the prior audit. No reporting reopen/unlock path was found. The target architecture must define authorization, reason, audit, and supersession before adding one; Phase 5.1 does not infer those rules.

### 5.5 Completeness inconsistency

Finalization uses `ReportCompletenessService`, requiring expected hourly records plus a daily unique record. `MonthlyFinalizeStatusService` uses distinct daily unique dates only. The notification can therefore advertise readiness for a month that the finalizer rejects. Evidence: `Services/Reports/MonthlyFinalizeStatusService.cs:42-86` and `ReportCompletenessService.cs:31-139`.

## 6. Export gap analysis

### PDF

QuestPDF is the active library and provides a viable offline renderer. The monthly PDF is limited to finalized, locked months. Most content comes from snapshots, but `MonthlyFinalPdfService.LoadEventDates` reads live `tbl_events` and selects START/NSD/ESD, excluding OH. This violates complete snapshot reproducibility and can make the official export differ from the state represented at finalization.

Reuse opportunity: retain visual conventions and QuestPDF expertise, but place the renderer behind a contract accepting a complete immutable report snapshot. A renderer must have no operational-table queries.

### Excel

No report Excel export exists. ClosedXML is referenced by the project but not used by reporting code. Reuse opportunity is library-level only after dependency review; the future exporter should consume the same projection/snapshot model as PDF.

### Print

No application reporting print path exists. If required later, printing should render from the immutable snapshot or an export artifact, not recalculate data.

### CSV

No CSV report export exists. CSV is potentially appropriate for flat detail/summary datasets but not as a substitute for the complete finalized report. Encoding, Persian headings, decimal formatting, and multi-section packaging remain product decisions.

## 7. Confirmed issues and gaps

### Critical

#### REP-51-01 — Finalized output mixes immutable snapshots with live data

- Evidence: `FrmReportCenter.LoadFinalizedMonthlyReportFromSnapshot` reads snapshots at lines 1222-1232 but recalculates Events/extremes at lines 1234-1246; `MonthlyFinalPdfService.LoadEventDates` queries `tbl_events` at lines 425-438.
- Failure scenario: a finalized screen or re-exported PDF contains analytical/Event information not captured by the finalized snapshot, so the complete output cannot be reproduced solely from finalization evidence.
- Impact: finalization does not mean immutable full-report content.
- Required direction: snapshot every displayed/exported section and render finalized results only from that snapshot.

#### REP-51-02 — Finalization can persist stale generated calculations

- Evidence: `IsGeneratedReportCurrent` compares only request dates/granularity; `btnFinalizeMonthlyReport_Click` passes cached objects into the snapshot transaction.
- Failure scenario: data changes after generation but before finalization; the month is locked with values computed from the earlier state.
- Impact: locked results can disagree with their source at lock time.
- Required direction: calculate from a consistent source revision at finalization or atomically verify a captured revision before committing.

#### REP-51-03 — Reporting still depends on non-authoritative legacy Runtime calculation

- Evidence: `EventReportEngineService` invokes `EventRuntimeCalculationService.Calculate`; prior audit findings RPT-02/RPT-03/RPT-06 demonstrate invalid-chain and ordering risks.
- Failure scenario: invalid or ambiguously ordered Events alter report runtime, longest-run, service-day, and final-state results.
- Impact: Runtime-dependent report fields are not yet safe as target authoritative results.
- Required direction: after the appropriate migration gate, consume validated Event Chain and Runtime Projection outputs without changing legacy behavior prematurely.

### Architecture

#### REP-51-04 — Snapshot lacks reproducibility/version evidence

The header and detail rows lack source, Event-chain, Runtime-policy, Runtime-baseline, calculation, configuration, reporting-policy, and snapshot-format versions. A stored number cannot be tied conclusively to its input/rule set.

#### REP-51-05 — Finalized multi-month reports are only partially snapshot-based

`PeriodFinalReportReadService` aggregates monthly main/daily snapshots, while the form recomputes all Event/runtime and analysis sections live. A finalized annual/half-year presentation is not an immutable composition of finalized months.

#### REP-51-06 — UI form is the report application service

The form selects sources, runs multiple engines, computes recycle changes, composes sections, caches finalization candidates, and initiates persistence/export. This makes consistency, automated testing, and alternative export channels difficult.

#### REP-51-07 — No single typed report projection/snapshot contract

`ReportResult` and `EventReportResult` are independently populated and UI-composed. Finalized readers reconstruct partial versions of these live-oriented models. Source mode and calculation provenance are implicit.

#### REP-51-08 — Completeness policies diverge

The pending-finalization notification checks only daily rows; generation/finalization also checks hourly coverage. Readiness has two definitions.

#### REP-51-09 — Queries and calculations are not bound to one consistent read snapshot

Main summaries, Events/runtime, extreme dates, and recycle changes are separate reads. Concurrent edits can yield a report whose sections represent different source moments even before finalization.

### Maintainability

#### REP-51-10 — Dictionary-shaped query rows defer type errors

`ReportQueryService` returns `Dictionary<string, object>` and downstream services parse values dynamically. Column/profile mismatches are detected late and complicate compiler-assisted refactoring.

#### REP-51-11 — Reporting rules are distributed across UI and static services

Service combinations, recycle changes, range/source selection, formatting, and finalization eligibility are split across a large form and service classes. Rule ownership is unclear and unit isolation is limited.

#### REP-51-12 — Chart data is calculated but not presented

`ChartDataBuilder` populates `ReportResult.ChartPoints`, but no report chart output was found. This is unnecessary work in the current UI and an incomplete feature rather than evidence that charts are approved for the target.

#### REP-51-13 — PDF has its own data-access path

The renderer loads report data itself and contains direct Event SQL. Presentation, source selection, and persistence concerns are coupled.

#### REP-51-14 — Snapshot semantic compatibility is not checked

Weighted period aggregation assumes all monthly snapshots use identical parameter definitions and calculation rules, but no definition/policy version is available to validate the assumption.

### Enhancement

#### REP-51-15 — Excel, CSV, and in-application print exports are absent

Only PDF exists. These are capability gaps, not defects unless approved requirements demand them.

#### REP-51-16 — Locked/open source status is not explicit in the result model

A typed source mode, finalization identity, and “as calculated at” metadata would make UI labeling and support diagnostics clearer.

#### REP-51-17 — Long-running report/export cancellation and progress are limited

Live generation moves work off the UI thread, but no cancellation contract or staged progress model was found. This should be considered after correctness boundaries are established.

## 8. Migration considerations

1. Preserve existing report behavior during transition. Introduce target reporting behind isolated contracts and use shadow comparisons before replacement.
2. Define typed station-scoped input repositories for hourly data, daily unique data, validated Event projections, and authoritative Runtime projections. Reporting must not recalculate Event validity or Runtime.
3. Create one `ReportProjection` containing every UI/export section plus input identities, completeness result, source revision, and calculation/policy versions.
4. Treat open projections as on-demand and non-authoritative. Clearly label their calculation timestamp and source mode.
5. At finalization, calculate or freshness-verify within a consistent boundary, then store a complete immutable snapshot and lock atomically.
6. Store finalized Runtime results exactly as provided by the Runtime snapshot, including its Event-chain, policy, baseline, calculation, and timestamp evidence.
7. Make monthly snapshots self-contained. Event log, extreme dates, service combinations, and export-specific evidence must not require live operational reads.
8. Compose finalized half-year/year outputs only from compatible finalized snapshots. Reject or explicitly handle version incompatibility; do not silently combine unlike policies.
9. Move QuestPDF behind a renderer contract receiving a snapshot. Add Excel/CSV/print only after output requirements are approved.
10. Unify completeness under one approved service and preserve the evidence used at finalization.
11. Keep Rasht and Ramsar parameter profiles isolated. Do not generalize beyond current station scope without approval.
12. Define a separately authorized reopen/supersession workflow before implementing any unlock feature. None exists today.

## 9. Recommended Phase 5 sequencing

### Gate A — Contracts and evidence

Approve report identity, source revision, completeness, calculation version, snapshot version, and station/period identity. Confirm which visible sections are mandatory in a finalized snapshot and resolve export requirements.

### Gate B — Isolated projection

Implement typed read-only repositories and a pure report calculator outside existing UI paths. Consume validated Event and Runtime outputs. Add deterministic tests for Persian ranges, missing/duplicate rows, station profiles, aggregation, and version evidence.

### Gate C — Shadow validation

Compare legacy and new results on copied/synthetic data. Classify expected policy differences separately from implementation defects. Include main/daily aggregates and every Runtime/Event/service field.

### Gate D — Immutable finalization

Implement complete snapshots with freshness verification and atomic lock creation. Prove that finalized screens and exports execute no operational-table reads.

### Gate E — Presentation and export migration

Switch UI and QuestPDF only after shadow acceptance. Preserve legacy paths until explicit cutover approval. Add other formats independently through the same snapshot contract.

## 10. Verification criteria for the next phase

Before reporting implementation begins, tests should be able to prove:

- hourly Min/Max/Average and daily Sum rules for both station profiles;
- Persian month, half-year, year, DataStartDate, and leap/non-leap Esfand boundaries;
- deterministic handling or explicit rejection of duplicate hourly rows;
- one approved completeness definition;
- validated Event Chain and Runtime Projection consumption without repair/inference;
- open projection changes when authoritative inputs change;
- finalized snapshot remains byte-for-byte or value-for-value stable afterward;
- every finalized screen and exporter uses snapshot data only;
- version-incompatible monthly snapshots are not silently combined;
- legacy reporting and database schema remain unchanged until the designated migration phase.

## 11. Phase 5.1 conclusion

The existing subsystem is a useful behavioral reference and contains reusable station metadata, aggregation intent, Persian-period handling, and PDF presentation knowledge. It is not yet a reproducible reporting architecture. The immediate target is not more report formats; it is a typed, versioned projection boundary and a complete immutable snapshot whose UI and exports require no live reads. The three critical findings—mixed snapshot/live outputs, stale-cache finalization, and dependence on legacy Runtime—must be closed or explicitly gated before target reporting replaces any production path.
