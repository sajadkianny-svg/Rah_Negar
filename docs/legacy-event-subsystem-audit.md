# Legacy Event Subsystem Audit

**Repository:** `D:\Projects\RahNegar_SQLite\Rah_Negar`  
**Audit date:** 2026-08-22  
**Scope:** Complete legacy Event-entry, persistence, validation, runtime, and reporting subsystem for the current Rasht/Ramsar production application  
**Method:** Read-only source audit, full solution build, NuGet health checks, and end-to-end static workflow tracing. Production code and user data were not modified. No Event test project or automated Event tests exist in the repository, so findings identified as confirmed are established directly by reachable code paths; items that need interactive or production-data reproduction are separated explicitly.

## 1. Executive conclusion

The current Event UI is the only major part suitable for reuse, and even it should be reused as an interaction concept rather than copied unchanged. Its compact unit/type/time/remark editor, read-only event grid, explicit row selection, Add/Apply modes, Clear Selection action, destructive-delete confirmation, minute-level spinner, and special OH warning form a practical operator workflow. These are **KEEP WITH ADAPTATION**. The generalized platform should preserve the fast staged-entry experience and familiar visual structure while localizing labels, improving keyboard flow and validation feedback, adding chronological display guarantees, and decoupling Event saving from mandatory hourly/daily values.

The Event business logic is not safe to reuse. The current persistence API performs delete-all-for-day followed by insert-all, has no Event-chain validation inside the persistence layer, and relies on callers to pass a transaction and invoke validation beforehand. The schema has no `CHECK` constraints for units, types, dates, or times and, critically, no unique constraint on `(date_rep, event_time, unit)`. Thus direct service calls, test seeding, external SQL, or future callers can store duplicates and invalid Event rows. Persistence is **REPLACE**.

The validation service contains useful intent—normalization, same-unit/same-minute duplicate detection, baseline fallback, previous/next lookup, Persian messages, and cross-day awareness—but fails the approved authoritative-chain requirement. It validates only units present in the proposed list, so deleting every Event for a unit/day or changing an Event's unit can bypass validation for the old unit. An unused method appears intended to load those old units. It reconstructs only the immediately previous Event, proposed daily Events, and immediately next Event, not the complete chain from Trusted Runtime Baseline through all later Events. It also incorrectly allows `Running + OH`, is coupled to `MessageBox`, silently returns on same-time failures in the current caller, coerces invalid times to `00:00`, and is not enforced below UI orchestration. Validation is **REPLACE**.

Runtime logic is also **REPLACE**. The public `EventRuntimeCalculationService.Calculate` method explicitly calls `CalculateLegacyCore` (`EventRuntimeCalculationService.cs:15-35`). A newer state-machine core exists but is private and unused; its comparison helper is also private and states that invariant comparison is not implemented (`:176-321`). The active legacy calculator permits invalid events to alter runtime: a repeated START closes/restarts a run, an ESD while stopped still receives an ESD adjustment, and OH while running ends the run and resets RuntimeAfterOH. The report engine reads only Events inside the requested report interval and adds Trusted Runtime Baseline values directly, rather than reconstructing from the baseline/data-start boundary to the report end (`EventReportEngineService.cs:27-55`; `EventReportQueryService.cs:20-37`). This makes arbitrary later-period cumulative runtime dependent on omitted history. Some formulas are worth retaining as specifications: midnight-exclusive period boundaries, 07:00/19:00 shift split, physical-only service-day marking, and ESD adjustment kept out of LongestRun.

Overall verdict: preserve the Event-entry UX pattern and several message/confirmation ideas; replace the persistence contract, schema guarantees, chain validator, and runtime authority implementation before reuse.

## 2. Exact files, classes, forms, and methods involved

### Direct Event-entry implementation

| File | Class/member | Role | Audit disposition |
|---|---|---|---|
| `UI/Forms/FrmRecords.cs:88-104` | `FrmRecords` Event state fields | Holds Add/Apply mode and loaded Event snapshot. | KEEP WITH ADAPTATION |
| `UI/Forms/FrmRecords.cs:115-170` | constructor | Configures `HH:mm` spinner, read-only grid, combos, remarks, and event handlers. | KEEP WITH ADAPTATION |
| `UI/Forms/FrmRecords.cs:313-362` | `SetFormMode`, `CanModifyEvents` | Enables Event editing only in Empty/Pasted/Editing modes. | KEEP WITH ADAPTATION |
| `UI/Forms/FrmRecords.cs:851-905` | `LoadEventComboBoxes`, `LoadUnits` | Fixed types; units loaded from `unit_runtime_base`. | KEEP WITH ADAPTATION |
| `UI/Forms/FrmRecords.cs:1725-1737` | `ConfirmOverhaulEvent` | Warns that OH resets RuntimeAfterOH. | KEEP |
| `UI/Forms/FrmRecords.cs:1933-2029` | `ExecuteSave` | Validates and replaces all daily data, unique data, and Events in one transaction. | REPLACE for Event persistence; KEEP transaction concept |
| `UI/Forms/FrmRecords.cs:2246-2265` | `HasAnyChanges` | Detects changes against loaded snapshots. | KEEP WITH ADAPTATION |
| `UI/Forms/FrmRecords.cs:2295-2342` | `BuildDailyEventsSaveModel` | Converts grid rows to database models. | REPLACE as a validation boundary |
| `UI/Forms/FrmRecords.cs:2427-2764` | Event operations | Add, Apply/edit, select, clear selection, delete, renumber, remarks, reset. | KEEP WITH ADAPTATION |
| `UI/Forms/FrmRecords.cs:2845-2925`, `:3075-3096` | Event loading | Loads selected Persian day and populates grid. | KEEP WITH ADAPTATION |
| `UI/Forms/FrmRecords.cs:3287-3309` | month lock UI check | Prevents entry into save/edit for locked month. | KEEP WITH ADAPTATION |
| `UI/Forms/FrmRecords.Designer.cs:337-579` | Event tab controls | Layout, tab order, drop-down styles, grid columns and click hook. | KEEP WITH ADAPTATION |
| `Core/EventEntryMode.cs` | `EventEntryMode` | Add versus Apply state. | KEEP |
| `Models/DailyEventRowModel.cs:12-19` | `DailyEventRowModel` | Legacy string-based Event transfer model. | REPLACE |
| `Services/EventNormalizationService.cs:12-50` | normalization | Maps three units and four Event types. It omits U4. | DEFECT / REPLACE |
| `Services/UnitMapper.cs` | `UnitMapper` | Display/database unit mapping used by Event UI. | KEEP WITH ADAPTATION |

### Persistence, sequence, schema, and locking

| File | Class/member | Role | Audit disposition |
|---|---|---|---|
| `Services/CommonRecordPersistenceService.cs:82-137` | `DeleteExistingEvents`, `InsertEvents` | Deletes all Events for a date, then inserts passed rows; lock checks but no chain validation. | REPLACE |
| `Services/CommonRecordQueryService.cs:78-114` | `LoadDailyEvents` | Reads a day's Events ordered only by textual `event_time`. | KEEP WITH ADAPTATION |
| `Services/CommonRecordComparisonService.cs:31-53` | `HasEventsChanges` | Order-sensitive snapshot comparison. | KEEP WITH ADAPTATION |
| `Services/DailySaveSequenceService.cs:43-126` | new/edit date validation | Sequential day entry based on `tbl_data`; edit requires `tbl_unique`. | KEEP WITH ADAPTATION |
| `Core/CommonDataSchema.cs:33-56` | `tbl_events` DDL/indexes | Defines unconstrained text fields and only non-unique Event indexes. | DEFECT / REPLACE |
| `Core/StationSchemaBuilderService.cs:23-40` | `Build` | Creates common Event table and indexes inside setup transaction. | KEEP WITH ADAPTATION |
| `Services/Reports/MonthlyLockService.cs:16-44` | `EnsureDateIsEditable` | Persistence-layer lock check inside caller transaction. | KEEP |
| `Services/Reports/MonthlyLockService.cs:49-123` | lock lookup/message | UI lock lookup and generic Persian lock message. | KEEP WITH ADAPTATION |
| `Services/Reports/TestDataSeederService.cs:19-69`, `:119-213`, `:284-342` | copy day/month/year | Directly deletes/copies `tbl_events`, bypassing lock and Event validation; reachable from `FrmRecords`. | DEFECT |

### Validation

| File | Class/member | Role | Audit disposition |
|---|---|---|---|
| `Services/Reports/EventSequenceValidationService.cs:39-102` | `ValidateDailyEvents` | Normalizes proposed day, detects duplicates, loads one previous/next Event, validates adjacent types. | REPLACE |
| `EventSequenceValidationService.cs:106-140` | `LoadInitialStateAsEvent` | Synthesizes initial pseudo-Event from baseline status. | KEEP WITH ADAPTATION; do not persist invented START |
| `EventSequenceValidationService.cs:149-176` | `LoadUnitsHavingEventsOnDate` | Loads old affected units but is never called. | DEFECT / incomplete implementation |
| `EventSequenceValidationService.cs:220-232` | `ValidateSameTimeEvents` | Correct same-unit/same-minute in-memory duplicate predicate. | KEEP WITH ADAPTATION |
| `EventSequenceValidationService.cs:237-347` | chain/transition validation | Adjacent event-type rules; incorrectly permits Running+OH. | REPLACE |
| `EventSequenceValidationService.cs:354-410` | previous/next checks | Cross-day nearest-neighbor lookup ordered by date/time/id. | KEEP WITH ADAPTATION |
| `EventSequenceValidationService.cs:424-460` | normalization/date display | Invalid time silently becomes midnight. | DEFECT |

### Runtime and reporting

| File | Class/member | Role | Audit disposition |
|---|---|---|---|
| `Services/Reports/EventReportQueryService.cs:25-104` | Event reads | `LoadRuntimeHistory` exists but production reports use range-only `LoadEvents`; no Event id in output/order. | DEFECT / REPLACE |
| `Services/Reports/EventInitialStateService.cs:14-70` | report initial state | Derives running status from separate last-event-per-type queries or baseline. | REPLACE |
| `Services/Reports/UnitRuntimeBaseQueryService.cs:21-63` | Trusted baseline values | Reads baseline cumulative and after-OH runtime. | KEEP WITH ADAPTATION |
| `Services/Reports/EventReportEngineService.cs:18-58` | production Event report | Wires range-only Events and legacy calculator. | DEFECT / REPLACE |
| `Services/Reports/EventRuntimeCalculationService.cs:15-174` | public and legacy core | Active production calculation. | DEFECT / REPLACE |
| `EventRuntimeCalculationService.cs:176-595` | private state-machine core | Unused partial replacement; no enforcing state errors and comparison incomplete. | REPLACE |
| `EventRuntimeCalculationService.cs:597-863` | legacy handlers/date helpers | Runtime, after-OH, service-day, shift, Persian conversion. | Mixed; inventory below |
| `Models/Reports/EventLogItem.cs:13-47` | report Event model | Omits database Event id and physical/adjustment audit structure. | REPLACE |
| `Models/Reports/UnitInitialEventState.cs:8-31` | initial report state | Boolean initial state without baseline timestamp/state enum. | REPLACE |
| `Models/Reports/UnitEventSummary.cs:12-43` | report output | Aggregates runtime and counts, including separate ESD total. | KEEP WITH ADAPTATION |
| `Services/Reports/ReportCompletenessService.cs:60-83` | completeness | Completeness depends on 12 hourly rows plus unique row, not Events. | KEEP |
| `UI/Forms/FrmReportCenter.cs:1163-1261`, `:1551-1809` | report UI | Builds Event reports and binds summary, service days and Event logs. | KEEP WITH ADAPTATION |
| `Services/Reports/MonthlyFinalReportService.cs:27-83` | final snapshot | Stores Event/runtime/service-day summaries before locking. | KEEP WITH ADAPTATION after engine replacement |
| `Services/Reports/MonthlyFinalReportReadService.cs:121-193` | final snapshot read | Reads finalized Event/runtime summaries. | KEEP WITH ADAPTATION |
| `Services/Reports/MonthlyFinalPdfService.cs:27-102`, `:290-340` | PDF integration | Presents snapshot Event/runtime/service-day output. | KEEP WITH ADAPTATION |

## 3. Current Event UX walkthrough

1. The operator works on the second `FrmRecords` tab, currently titled `Fuel & Flow & Events` (`FrmRecords.Designer.cs:337-347`). Events are not a standalone aggregate editor; they share a daily record screen and save lifecycle with the 12 hourly observations and daily unique values.
2. A custom Persian date picker supplies `date_rep`. Enter loads the day; outside Editing mode, Left/Right move dates and Enter loads (`FrmRecords.cs:774-844`). In Editing mode these form-level shortcuts are disabled by returning to base processing (`:820-821`).
3. Unit is a non-editable drop-down populated from `unit_runtime_base`, ordered by numeric `unit_no`, and displayed as `Unit N` (`:872-899`). This avoids free-text units and adapts to configured unit count. Event type is another non-editable drop-down containing exactly Start, NSD, ESD, OH (`:851-866`).
4. Time uses a spinner-style `DateTimePicker`, custom formatted `HH:mm`, and emitted as exactly `HH:mm` (`:136-138`, `:2477-2481`). Seconds are not presented or persisted through this UI.
5. Remarks are enabled only for NSD/ESD; selecting START or OH clears and disables them. They are limited to 55 characters in the control and truncated again during model construction (`:146-152`, `:2733-2743`, `:2314-2321`).
6. Add stages a row in the read-only grid, selects the new row, clears input controls, and resets time to 00:00 (`:2468-2516`, `:2749-2764`). The Event is not persisted yet.
7. Clicking a grid row copies it to the editor and changes the Add button text to `Apply`; `Clear Selection` exits this state (`:2575-2634`). Apply edits the staged row, then returns to Add mode (`:2522-2569`). There is no double-click ambiguity or inline grid editing.
8. Delete removes only the staged row after a Persian danger confirmation, renumbers the visual rows, and selects an adjacent row (`:2640-2727`). Despite saying the operation cannot be reversed, a loaded day's deletion is not committed until Save Edit and can still be canceled—so the wording is inaccurate.
9. Adding OH, or changing a non-OH row into OH, displays a specific warning that RuntimeAfterOH will reset (`:1725-1737`, `:2483-2484`, `:2557-2560`). Re-applying an already-OH row does not repeat the warning.
10. Existing days load in read-only mode. The user must click Edit; a locked month is rejected before entering Editing. Editing disables the date picker and enables Event controls. Save Edit replaces the entire selected day's daily data, unique row, and Event set; Cancel Edit reloads the database snapshot (`:266-307`, `:2078-2131`).
11. A new day's Save button is effectively usable only after hourly data has been pasted: `btnSave_Click` returns unless the mode is Pasted, and `ValidateBeforeSave` demands all hourly cells and mandatory unique fields (`:1744-1805`, `:2037-2045`). Consequently an Event cannot be independently recorded, even though Events are optional for completeness.

### Keyboard and mouse assessment

The grid-selection-to-editor pattern is clear and safe for mouse users. DropDownList controls prevent accidental free-text values. The date shortcuts and spinner are efficient. However, no Event-specific Enter/Escape/Delete shortcuts are wired, the global Enter key triggers day Load outside Editing and can conflict with expected form traversal, tab indices are non-sequential (`cmbUnits` 3, type 4, time 5, Add 6, Delete 7, remark 31, Clear 33), and the mixed English UI labels (`Add`, `Apply`, `Delete`, `Clear Selection`, tab title) conflict with Persian messages. The grid disables scroll bars (`FrmRecords.Designer.cs:575`), which becomes a usability defect for event-heavy days. Event validation happens only at the final daily save, not when Add/Apply stages the row, so errors may be discovered long after entry.

## 4. Current persistence workflow

### New day

`btnSave_Click` calls `ExecuteSave(false)` only from Pasted mode. `ExecuteSave` validates mandatory hourly and unique fields, checks month lock and data-start date, enforces the next sequential `tbl_data` day, builds the full daily Event list, and calls `EventSequenceValidationService.ValidateDailyEvents` (`FrmRecords.cs:1933-1991`). Only then does it open a connection and transaction. Within the same transaction it inserts station daily data, deletes/reinserts the daily unique row, deletes all Events for the selected date, inserts every staged Event, and commits (`:1993-2012`). This atomic daily-bundle transaction is a strength.

### Edit day

The same method uses `ValidateEdit`, which only confirms a `tbl_unique` row exists, confirms the replacement, and then executes the identical delete/reinsert transaction. Individual Event database ids are never loaded into the UI model, so Add/Edit/Delete are UI concepts only; the database sees full-day replacement. Any unchanged Event receives a new autoincrement id. This destroys stable Event identity and audit provenance.

### Delete Event

There is no standalone persisted delete. A row is removed from the grid and becomes a database deletion only when the whole day's replacement transaction commits. Cancel Edit restores it. `CommonRecordPersistenceService.DeleteExistingEvents` deletes by `date_rep` alone (`:82-95`).

### Transaction and lock boundaries

The normal `ExecuteSave` path is atomic across `tbl_data`, `tbl_unique`, and `tbl_events`. The persistence methods call `MonthlyLockService.EnsureDateIsEditable` using the same connection and transaction (`CommonRecordPersistenceService.cs:82-104`; `MonthlyLockService.cs:16-44`). This is substantially better than UI-only locking and should be preserved.

The chain validation is executed before the write connection/transaction opens. There is therefore a time-of-check/time-of-use window: another connection could change Events between validation and replacement. SQLite's single-writer behavior does not protect a read performed on a prior connection. In the current single-user desktop this race may be uncommon, but the approved invariant requires validation and mutation in one authoritative transaction.

### Bypass paths

`InsertEvents` is public and accepts arbitrary models but does not normalize, validate types/times/units, enforce sequential entry, or validate the chain (`CommonRecordPersistenceService.cs:101-137`). Schema constraints do not compensate. `TestDataSeederService` directly deletes and copies Events and bypasses both monthly locks and validation (`TestDataSeederService.cs:119-213`, `:284-342`). Buttons invoking this test seeder remain in `FrmRecords` (`FrmRecords.cs:3315-3343`), so the bypass is reachable in the production form if those designer controls are exposed. Direct SQLite access is another bypass because the database has no domain constraints.

## 5. Current validation workflow

The UI validates only nonempty unit/type and remark applicability at Add/Apply. OH gets a warning, but there is no transition, duplicate, chronological, or chain validation at this stage (`FrmRecords.cs:2468-2569`). All staged rows remain in insertion order; they are not sorted in the grid.

At final save, grid rows are converted to models. Invalid/blank rows are silently skipped rather than rejected (`FrmRecords.cs:2295-2339`). Since the UI drop-downs normally prevent invalid values, this mainly affects malformed loaded data, future callers, or partially corrupted grid state. Silent omission is unsafe because the user can believe a visible row will be saved.

`ValidateDailyEvents` then:

1. Filters incomplete rows again, normalizes values, and sorts by textual normalized time then unit (`EventSequenceValidationService.cs:43-58`).
2. Rejects duplicate `(Unit, EventTime)` pairs in the proposed daily list (`:220-232`). Different units at the same time are allowed.
3. For each unit appearing in the proposed list, loads the immediately previous Event before the selected date, or creates a pseudo initial-state Event from `unit_runtime_base` (`:68-86`, `:106-140`).
4. Appends that unit's proposed daily Events, ordered by time.
5. Appends only the immediately next Event after the selected date (`:88-95`, `:385-410`).
6. Checks adjacent Event types through `IsTransitionAllowed` (`:237-347`).

This is cross-day nearest-neighbor validation, but not full authoritative reconstruction. `LoadUnitsHavingEventsOnDate` was evidently written to include units whose old rows are being removed, but is never used (`:144-176`). Therefore:

- deleting the only Event(s) on the day produces an empty proposed list; no unit group is visited and validation returns success;
- changing an Event from U1 to U2 validates U2 but not the chain created by removing it from U1;
- deleting all U1 rows while leaving U2 rows validates only U2;
- the algorithm does not replay every Event from the Trusted Runtime Baseline through the last stored Event, so it cannot prove the complete authoritative chain is valid;
- validation is performed outside the mutation transaction and can be bypassed through the public persistence API.

The transition table is closer to an event-alternation table than the approved explicit three-state machine. It allows `__INITIAL_RUNNING__ -> OH` and `START -> OH` (`:335-343`), directly violating Running + OH rejection. It correctly rejects repeated START after START, rejects NSD/ESD after stopped states, allows stopped + OH, and allows only START after OH.

Message quality is mixed. Invalid adjacent transitions show Persian unit, prior/new event, date, and time, using RTL MessageBox options (`:254-305`). They do not state what the operator must correct. Duplicate-time validation returns a Persian message but never displays it; `ExecuteSave` merely returns when `IsValid` is false (`FrmRecords.cs:1987-1991`). This makes a duplicate-timestamp save fail silently. Invalid time strings are coerced to `00:00` instead of rejected (`EventSequenceValidationService.cs:441-450`), potentially generating a misleading duplicate or chronology failure.

## 6. Current runtime interaction

### Production call path

`FrmReportCenter` calls `EventReportEngineService.BuildEventReport` for live, finalized-period analytical, and finalized-month analytical views (`FrmReportCenter.cs:1163-1261`, `:1394-1417`). The engine calls range-only `EventReportQueryService.LoadEvents`, derives start-of-report state, reads baseline runtimes, reads ESD adjustment settings, and invokes public `EventRuntimeCalculationService.Calculate` (`EventReportEngineService.cs:27-55`).

The public method unequivocally returns `CalculateLegacyCore` (`EventRuntimeCalculationService.cs:15-35`). The alternative `CalculateStateMachineCore` is private and has no production caller. `CompareLegacyAndStateMachine` is also private and returns the literal warning `Invariant comparison has not been implemented` (`:278-321`). `LoadRuntimeHistory` exists and its comment states it is not used by production (`EventReportQueryService.cs:20-37`); `LoadRuntimeHistoryForComparison` likewise states it does not alter production (`EventReportEngineService.cs:60-80`).

### Legacy behavior

- Events are filtered to configured units and four supported types, then sorted by `EventDateTime` and unit. No database id is available as a final ordering key (`EventRuntimeCalculationService.cs:98-103`; `EventReportQueryService.cs:50-55`).
- A START always increments counts. If already running, it closes the current run at the repeated START and immediately opens another (`EventRuntimeCalculationService.cs:597-632`). Total physical duration may remain continuous, but LongestRun is incorrectly split and an invalid Event is accepted.
- NSD/ESD always increment counts. ESD adjustment is added before checking whether a run is open; therefore a stopped-unit ESD increases runtime (`:634-693`).
- OH increments events, closes any open run, stops both runtime trackers, and resets RuntimeAfterOH (`:695-729`). Thus Running + OH directly terminates the unit, contrary to the approved rule.
- Physical runs add RuntimeHours, update LongestRun, and mark each Gregorian-midnight day with positive overlap; using `end.AddTicks(-1)` correctly excludes a zero-duration next day at exactly midnight (`:731-804`). Persian conversion is performed per marked day.
- Shift classification correctly uses day `07:00 <= time < 19:00`, night otherwise (`:806-812`).
- Period end is the next Persian day at 00:00 and is exclusive (`:49-52`, `:857-862`).
- ESD extra is represented separately in `EsdExtraHoursTotal` and is not included in LongestRun or service-day marking. However it is added for invalid ESD transitions.

### Range-history defect

The engine reads baseline cumulative values but only Events between `dateFrom` and `dateTo`. For a report starting after `data_start_date`, physical runtime between the Trusted Runtime Baseline and `dateFrom` is absent from `RuntimeHours`; only the fixed baseline plus runtime occurring inside the requested range is returned. Initial running status is inferred from Events before the report, but elapsed runtime is not reconstructed. The unused history loader confirms the intended corrective direction. This is a confirmed code-path defect, although its numerical impact depends on how the UI labels/consumers interpret `RuntimeHours` for arbitrary periods.

### Hourly observation isolation

Runtime calculation reads only `tbl_events`, baseline tables, and settings. No ST/RPM query is present in the Event engine; `FrmReportCenter.BuildActiveUnitsByDayFromEvents` explicitly documents Events as the source (`FrmReportCenter.cs:2067-2075`). Thus approved Event authority and no cross-validation against hourly ST/RPM are already met in report calculation.

## 7. Rule-by-rule comparison against approved rules

| # | Approved rule | Existing behavior and evidence | Classification |
|---:|---|---|---|
| 1 | Types fixed to START, NSD, ESD, OH | UI drop-down has exactly four values (`FrmRecords.cs:851-859`), and normalization recognizes them (`EventNormalizationService.cs:40-49`). Database has no type `CHECK`; report reader accepts arbitrary text then filters unsupported rows. | KEEP WITH ADAPTATION |
| 2 | Events are sole runtime authority; no ST/RPM cross-validation | Event engine consumes Events/baseline/settings only (`EventReportEngineService.cs:27-55`); report UI explicitly calls Event-derived service-day mapping (`FrmReportCenter.cs:2067-2075`). | KEEP |
| 3 | Exact three-state transitions | Legacy validator rejects several bad alternations but permits Running+OH from initial-running and START (`EventSequenceValidationService.cs:335-343`). Runtime calculators also accept rather than reject invalid transitions. | DEFECT |
| 4 | Validation state-based, not merely different from previous | Current logic keys permission on the previous Event type, approximating state, but encodes the wrong Running+OH rule and has no explicit StoppedAfterOH state (`:328-347`). | REPLACE |
| 5 | Every Add/Edit/Delete reconstructs complete chain from baseline | Validation occurs only at daily Save, not Add/Apply/Delete; uses one previous and one next Event, and only proposed units (`:39-102`). | DEFECT |
| 6 | Reject operation if any later Event becomes invalid | One next Event is checked only for proposed units. Empty deletion/unit reassignment bypasses old-unit check; complete later chain is not replayed. | DEFECT |
| 7 | Unique timestamp per Unit; different Units may share | In-memory daily grouping implements exactly this (`:220-232`), but no database unique constraint exists (`CommonDataSchema.cs:36-56`) and duplicate failure is silent in UI. | DEFECT |
| 8 | User time precision is minutes | UI displays/emits `HH:mm` and spinner has no seconds (`FrmRecords.cs:136-138`, `:2480`, `:2539`). Database accepts arbitrary text/precision. | KEEP WITH ADAPTATION |
| 9 | Prohibit future/out-of-sequence operating days | Normal full-day new-save path enforces next date using Persian calendar and `tbl_data` maximum (`DailySaveSequenceService.cs:43-109`). Persistence and seeder bypass it; Events cannot be saved independently. | KEEP WITH ADAPTATION |
| 10 | Event optional for completeness | Completeness checks only 12 hourly times plus unique row (`ReportCompletenessService.cs:60-83`). Empty Event list persists successfully as part of a complete daily save. | KEEP |
| 11 | OH does not reset cumulative; resets after-OH; cannot stop running | Both calculators retain cumulative and reset after-OH, but validator permits Running+OH and calculators close/stop the run (`EventRuntimeCalculationService.cs:695-729`, `:440-451`). | DEFECT |
| 12 | ESD adjustment only valid Running->ESD; increases period/cumulative/after-OH; no service day/LongestRun; auditable | Legacy and private cores add cumulative/after-OH and separate ESD total without service-day/LongestRun impact, but apply it even when stopped (`:634-693`, `:407-438`). Physical/adjustment are aggregated fields, not a durable calculation ledger. | DEFECT |
| 13 | ServiceDay is positive physical running overlap in 00:00 day; ESD alone excluded | `AddServiceDaysForRange` uses physical runs and `end.AddTicks(-1)` (`:731-804`); ESD extra alone does not call it. | KEEP |
| 14 | LongestRun physical only, period-clipped, ESD excluded | Legacy starts initial running at period start and closes at period end; ESD extra is outside `CloseRuntimeRun`. Repeated invalid START splits a run, so result relies on valid input. | KEEP WITH ADAPTATION |
| 15 | Runtime boundary 00:00; shifts 07:00/19:00 | Next Persian date 00:00 exclusive and shift predicate are correct (`:49-52`, `:806-812`, `:857-862`). | KEEP |
| 16 | Initial running represented by Trusted Baseline; no invented historical START | Database stores baseline status/runtime (`StartupSetupService.cs:75-82`); validator creates only an in-memory pseudo-state Event, not a stored START (`EventSequenceValidationService.cs:106-140`). | KEEP WITH ADAPTATION |
| 17 | Rejections need clear Persian unit/Event/reason/correction | Transition messages identify unit and event details but not correction. Duplicate failure is not shown. Generic UI messages omit unit/details. English control labels persist. | DEFECT |
| 18 | Validation below UI layer | A separate validation service exists, but persistence does not call it and exposes unvalidated public writes; seeder/direct SQL bypass it. The validator itself shows UI MessageBoxes. | DEFECT |

## 8. Confirmed defects, with severity

### HIGH-01 — Running + OH is accepted and terminates runtime

- **Classification:** DEFECT
- **Files/methods:** `Services/Reports/EventSequenceValidationService.cs:328-347`, `EventRuntimeCalculationService.cs:149-158`, `:695-729`, and private core `:440-451`.
- **Evidence:** Transition rules permit both `__INITIAL_RUNNING__ -> OH` and `START -> OH`. Both calculators close an active run and set it stopped on OH.
- **Failure scenario:** U1 is Running; operator enters OH without a preceding NSD/ESD. Save succeeds. Runtime ends at OH and RuntimeAfterOH resets, manufacturing an unapproved shutdown.
- **Required correction:** Explicit state machine must reject Running+OH with Persian unit/timestamp/recovery guidance, at the domain/persistence boundary and UI.

### HIGH-02 — Deleting or reassigning Events can bypass downstream-chain validation

- **Classification:** DEFECT
- **Files/methods:** `EventSequenceValidationService.cs:39-102`, unused `LoadUnitsHavingEventsOnDate` at `:149-176`; `FrmRecords.cs:2640-2709`.
- **Evidence:** The validation loop groups only units in the proposed Event list. Empty lists never enter the loop; old affected units are not unioned into the validation set.
- **Failure scenario:** Existing U1 START on day D supports U1 NSD on D+1. User edits D and deletes START. Proposed U1 list is empty, validation returns success, and D+1 begins with invalid NSD. Changing the START to U2 has the same effect on U1.
- **Required correction:** Determine all affected units from before/after mutation and replay each complete chain in the same transaction before committing.

### HIGH-03 — No database uniqueness or domain constraints for Events

- **Classification:** DEFECT
- **Files/methods:** `Core/CommonDataSchema.cs:33-56`.
- **Evidence:** `(date_rep,event_time,unit)` index is non-unique; all domain values are unconstrained `TEXT`/`INTEGER` with only NOT NULL.
- **Failure scenario:** Two U1 Events at 08:15 are inserted through a bypass path; invalid type `STOP`, malformed time, or unsupported unit is also accepted. Ordering/runtime then depends on non-authoritative row/source order.
- **Required correction:** Add non-destructive migration after data-quality remediation, with a unique same-unit timestamp constraint and checks/canonical storage appropriate to the new schema.

### HIGH-04 — Public persistence bypasses Event validation and sequential rules

- **Classification:** DEFECT
- **Files/methods:** `CommonRecordPersistenceService.cs:82-137`; `TestDataSeederService.cs:119-213`, `:284-342`.
- **Evidence:** Public delete/insert methods enforce only monthly lock; seeder enforces neither lock nor Event chain.
- **Failure scenario:** Any caller inserts invalid transition, duplicate time, future date, or bad text without using `ExecuteSave`.
- **Required correction:** Make a single application/domain command the mutation boundary. It must normalize, load/replay, validate, and mutate atomically; raw data helpers must not be production mutation APIs.

### HIGH-05 — Production reports use legacy runtime calculation and omit pre-range runtime history

- **Classification:** DEFECT
- **Files/methods:** `EventRuntimeCalculationService.cs:15-35`; `EventReportEngineService.cs:27-55`; `EventReportQueryService.cs:20-37`.
- **Evidence:** Public Calculate calls `CalculateLegacyCore`. Engine loads only selected-range Events. Full runtime history loader is explicitly unused.
- **Failure scenario:** Baseline is at data start; U1 runs/stops before a later requested month. A report starting in that later month adds the unchanged baseline but not elapsed historical runtime, so cumulative runtime is understated.
- **Required correction:** Reconstruct authoritative state/runtime from Trusted Runtime Baseline through report end, while clipping period metrics separately.

### HIGH-06 — ESD adjustment applies to invalid stopped-state ESD

- **Classification:** DEFECT
- **Files/methods:** legacy `EventRuntimeCalculationService.cs:634-693`; private core `:407-438`.
- **Evidence:** Adjustment is added without first proving `IsRunning`/open run. Private core likewise adds before setting stopped.
- **Failure scenario:** Corrupt/imported/bypassed chain contains stopped U1 + ESD; report increases cumulative and after-OH runtime despite no valid Running->ESD transition.
- **Required correction:** Apply adjustment only as part of an accepted Running->ESD transition and preserve physical versus adjustment components separately.

### MEDIUM-01 — Duplicate-timestamp rejection is silent

- **Classification:** DEFECT
- **Files/methods:** `EventSequenceValidationService.cs:220-232`; `FrmRecords.cs:1987-1991`.
- **Evidence:** Duplicate validator returns a message but does not call the service's MessageBox function; caller returns without displaying `eventValidation.Message`.
- **Failure scenario:** Operator presses Save and nothing appears to happen.
- **Required correction:** Return structured validation error and have UI display a precise Persian correction message.

### MEDIUM-02 — Invalid Event rows/times are silently omitted or coerced

- **Classification:** DEFECT
- **Files/methods:** `FrmRecords.cs:2295-2339`; `EventSequenceValidationService.cs:441-450`; `EventReportQueryService.cs:130-145`.
- **Evidence:** Incomplete rows are skipped; invalid times normalize to midnight.
- **Failure scenario:** Corrupt loaded row disappears on Save Edit, or malformed time becomes 00:00 and changes ordering/runtime.
- **Required correction:** Reject noncanonical values explicitly; never repair semantic input silently during validation/reporting.

### MEDIUM-03 — Event identities and audit history are destroyed on edit

- **Classification:** DEFECT
- **Files/methods:** `FrmRecords.cs:1998-2005`; `CommonRecordPersistenceService.cs:82-137`; `DailyEventRowModel.cs:12-19`.
- **Evidence:** Model omits id; every daily edit deletes all Event rows then inserts all again.
- **Failure scenario:** An unchanged Event receives a new id; it is impossible to audit which Event was edited/deleted or distinguish correction from recreation.
- **Required correction:** Stable Event identity, explicit commands, and auditable changes in generalized platform.

### MEDIUM-04 — Ordering lacks a deterministic supported tie-breaker

- **Classification:** DEFECT
- **Files/methods:** `CommonRecordQueryService.cs:82-91`; `EventReportQueryService.cs:50-55`, `:101-104`; `EventRuntimeCalculationService.cs:226-238`.
- **Evidence:** Daily/report queries order by date/time without id/unit consistently. `EventLogItem` omits id. Private core contains a TODO for id and falls back to source order.
- **Failure scenario:** Existing duplicate same-unit timestamps, permitted by schema, are processed according to SQLite/source return order; results can differ after delete/reinsert.
- **Required correction:** Prevent same-unit ties at database level and use stable Event id as final presentation/import diagnostic ordering key.

### MEDIUM-05 — Event capture is incorrectly coupled to mandatory daily observations

- **Classification:** DEFECT relative to generalized-platform behavior
- **Files/methods:** `FrmRecords.cs:1744-1805`, `:1933-2005`, `:2037-2045`.
- **Evidence:** New Event persistence cannot occur unless the full hourly grid is in Pasted mode and mandatory unique fields validate.
- **Failure scenario:** Operator needs to record a real Event promptly but hourly/daily values are not ready; the system cannot save it independently.
- **Required correction:** Preserve sequential operating-day policy while allowing optional Events to be persisted through their own validated transaction/workflow.

### MEDIUM-06 — Production-accessible test seeding can rewrite locked or authoritative Event history

- **Classification:** DEFECT
- **Files/methods:** `FrmRecords.cs:3315-3343`; `TestDataSeederService.cs:19-69`, `:119-213`, `:284-342`.
- **Evidence:** Form handlers invoke bulk month/year copy. Seeder deletes and inserts Event rows without `MonthlyLockService` or chain validation.
- **Failure scenario:** If buttons are reachable, template Events are replicated over months/year, including finalized periods, creating invalid repeated transitions.
- **Required correction:** Remove production reachability in future work; test utilities must use isolated databases and cannot bypass domain rules.

### LOW-01 — Delete confirmation inaccurately says staged deletion is irreversible

- **Classification:** DEFECT
- **Files/methods:** `FrmRecords.cs:2668-2682`, cancel at `:2110-2126`.
- **Evidence:** Message says deletion cannot be reversed, but no database deletion occurs until Save Edit and Cancel reloads the row.
- **Failure scenario:** User makes an unnecessarily fearful decision or misunderstands save state.
- **Required correction:** Say the row is removed from pending changes and will be committed on Save.

### LOW-02 — Event-heavy grids cannot scroll

- **Classification:** DEFECT
- **Files/methods:** `FrmRecords.Designer.cs:530-579`.
- **Evidence:** `ScrollBars = None` while grid height is finite.
- **Failure scenario:** Later Events become inaccessible when rows exceed visible space.
- **Required correction:** Enable vertical scrolling and retain fixed, readable columns.

### LOW-03 — Mixed English/Persian labels and weak correction guidance

- **Classification:** DEFECT
- **Files/methods:** `FrmRecords.Designer.cs:337-347`, `:401-436`; `FrmRecords.cs:2606-2607`; validation messages `EventSequenceValidationService.cs:254-305`.
- **Evidence:** English action/type surface coexists with Persian errors; transition errors lack corrective action.
- **Failure scenario:** Reduced clarity for Persian-first operators.
- **Required correction:** Localize the full interaction and include precise next action.

### LOW-04 — U4 normalization is inconsistent

- **Classification:** DEFECT
- **Files/methods:** `EventNormalizationService.cs:17-34`; `EventSequenceValidationService.cs:202-213`; Ramsar supports U4 through baseline-loaded UI.
- **Evidence:** `EventNormalizationService.NormalizeUnitForDatabase` handles U1-U3 only, while validation and configured Ramsar paths handle U4. `FrmRecords` happens to use `UnitMapper` for grid unit conversion, limiting current exposure, but the public normalization service is incomplete.
- **Failure scenario:** A future/direct caller normalizing U4 receives empty string and may drop the Event.
- **Required correction:** One canonical station-aware unit identifier policy; eliminate duplicate normalizers.

### CODE QUALITY-01 — Duplicate normalization/Persian conversion implementations

- **Classification:** DEFECT (CODE QUALITY)
- **Files:** `EventNormalizationService.cs`; `EventSequenceValidationService.cs:424-460`; `EventReportQueryService.cs:107-172`; `EventInitialStateService.cs:159-220`; `EventRuntimeCalculationService.cs:819-863`.
- **Evidence:** Unit/type/time/date normalization is independently reimplemented with different supported units and invalid-input behavior.
- **Impact:** Drift is already present (U4 handling), and malformed data can be interpreted differently by entry, validation, and reporting.
- **Required correction:** In future platform, one pure canonical parser/value object used at every boundary.

### CODE QUALITY-02 — Incomplete state-machine migration remains dead production code

- **Classification:** DEFECT (CODE QUALITY)
- **Files/methods:** `EventRuntimeCalculationService.cs:176-595`.
- **Evidence:** Private state machine and comparison method have no public caller; comparison invariants are explicitly TODO.
- **Impact:** Creates false confidence that approved runtime logic is active and duplicates calculation paths.
- **Required correction:** Do not reuse either path wholesale; implement and test the approved model, then maintain one production calculator.

## 9. Data-integrity risks

1. **Duplicate Events already in databases:** schema permits them. Before adding any future unique constraint, run a read-only inventory grouped by canonical unit/date/minute. Do not delete automatically.
2. **Malformed text values:** arbitrary unit/type/time and invalid Persian `date_rep` are storable. Report readers may skip unsupported type/unit or convert bad time to midnight; invalid Persian dates can throw during report conversion.
3. **Chain corruption after deletion/unit reassignment:** confirmed bypass described in HIGH-02.
4. **Locked-month bypass:** normal persistence checks locks inside its transaction, but direct SQL and `TestDataSeederService` do not. SQLite has no trigger preventing writes to locked months.
5. **Lost identity/auditability:** full-day replacement changes ids for unchanged rows and records no actor, created/updated timestamp, reason, or supersession history.
6. **Time-of-check/time-of-use:** chain validation uses a different connection before the write transaction.
7. **Ordering:** textual `event_time` works only when canonical `HH:mm`; schema does not guarantee it. Existing `8:5`, `08:05:30`, or invalid strings break lexical chronology.
8. **Baseline semantics:** `unit_runtime_base` has no Event-chain foreign relationship or explicit baseline effective timestamp in Event models. Correct reconstruction depends on external `data_start_date` convention.
9. **Report snapshots:** finalized summaries preserve results produced by the legacy engine. Correcting raw Events/runtime later must not silently rewrite already finalized output; migration/reconciliation policy is required.
10. **No automated safety net:** repository contains no test project. The only test-named code is a data seeder, not assertions.

## 10. UX strengths worth preserving

- Compact one-line entry: Unit, type, minute time, contextual remark, Add.
- Drop-down-only unit/type selection minimizes spelling errors.
- Units derive from trusted configured baseline and therefore match station configuration.
- Read-only grid plus explicit row-to-editor Apply flow avoids accidental inline edits.
- Clear visual distinction between Add and Apply, with a visible Clear Selection escape.
- Destructive delete confirmation and special high-salience OH confirmation.
- Minute-precision `HH:mm` spinner aligns with approved operator precision.
- Contextual remarks enabled only when the legacy workflow expects them; 55-character visible limit keeps grid readable. The generalized product should re-confirm whether START/OH remarks truly must be forbidden before retaining that restriction.
- Loaded versus Editing modes protect stored data from casual clicks.
- Cancel Edit restores the authoritative persisted snapshot.
- Event grid is double-buffered and resizes columns through shared UI services (`FrmRecords.cs:121-162`, `:983-1006`), supporting responsive display.
- Persian date picker and Persian-calendar day stepping correctly cross Persian month/year boundaries through `PersianDateHelper.AddDays` (`DailySaveSequenceService.cs:91`; `PersianDateHelper.cs:13-31`).
- Atomic save of the daily bundle and persistence-layer monthly lock check are sound safety patterns, even though the future Event workflow should be independently saveable.

## 11. UX weaknesses

- Validation is deferred until final daily save; Add/Apply accepts transition and duplicate errors.
- Same-time failure can be completely silent.
- Generic messages such as “complete Event information” do not identify unit/type/time or corrective action.
- Transition errors describe prior/new events but not the allowed next Events from the current state.
- UI mixes English labels/actions with Persian messaging.
- No vertical scrolling in Event grid.
- Event date is implicit in the page, not repeated near each staged Event; this increases cross-day context errors.
- Rows remain in entry order, while save validation sorts them. The operator may see an order different from execution order.
- Clicking any cell selects and enters Apply mode; there is no dirty-state indication for the selected Event.
- Delete confirmation calls a staged action irreversible.
- Tab order is irregular and there are no explicit Event-entry keyboard accelerators.
- Time resets to midnight after every Add/Apply, which is safe but slows clustered Event entry and increases accidental 00:00 entries.
- Remarks are silently cleared when type changes away from NSD/ESD, without undo.
- Event entry cannot be persisted until unrelated hourly and unique data are complete.
- Full-day replacement hides whether an individual edit/delete has actually been committed and eliminates Event-level auditability.

## 12. KEEP / KEEP WITH ADAPTATION / REPLACE / DEFECT inventory

Each item below has exactly one requested classification.

### KEEP

- Fixed Event vocabulary at the product/UI level: START, NSD, ESD, OH.
- Events-only runtime authority; no ST/RPM runtime cross-validation.
- Minute-precision operator entry concept.
- Events excluded from daily completeness criteria.
- OH high-salience confirmation concept.
- Physical-only service-day range marking and midnight-exclusive end behavior.
- 07:00 inclusive / 19:00 exclusive day-shift predicate.
- Trusted Runtime Baseline concept without persisted synthetic historical START.
- Use of one transaction for a committed mutation bundle.
- Persistence-layer check for finalized month in the normal path.
- Read-only Event grid with explicit editor selection and cancel/reload behavior.

### KEEP WITH ADAPTATION

- Compact Event editor and grid layout: localize, scroll, show date context and validation state.
- Unit population from trusted station configuration: use generalized station/unit model rather than raw baseline query in the form.
- Add/Apply/Clear Selection interaction: validate immediately through the domain service and improve keyboard handling.
- Delete confirmation: accurately describe staged versus committed deletion and downstream validation.
- Persian date picker/navigation: keep conventions but centralize parsing/value types and test boundaries.
- Nearest previous/next queries: useful as diagnostics/optimization only after complete-chain correctness is guaranteed.
- Same-unit/same-minute in-memory duplicate predicate: retain as early feedback, backed by database uniqueness.
- Event snapshot change detection: compare canonical identity/value sets, not incidental row order.
- Runtime/report summary models: retain useful outputs but separate cumulative, physical period runtime, adjustment runtime, and audit evidence.
- Monthly final snapshot/report presentation: reuse after calculations are corrected and versioned.
- Grid theming, double buffering, and column fitting.
- Remark field/limit: retain usability pattern only after generalized business rules confirm which types accept remarks.
- Sequential daily-entry policy: enforce at domain layer for operating-day eligibility without coupling Event persistence to full hourly data.

### REPLACE

- `DailyEventRowModel` string-only persistence contract.
- Full-day delete/reinsert Event persistence and lack of stable identity.
- `BuildDailyEventsSaveModel` as a silent-filtering validation boundary.
- `EventSequenceValidationService` orchestration and transition table.
- UI-coupled `MessageBox` calls inside validation service.
- Event schema and indexes as the sole data-integrity model.
- Range-only Event report reconstruction.
- `EventInitialStateService` separate last-event-per-type inference.
- Public legacy runtime calculator and unused private state-machine core.
- Multiple inconsistent normalization/time/Persian conversion implementations.
- Production test-seeder mutation route.

### DEFECT

- Running+OH accepted and treated as shutdown.
- Empty deletion and unit reassignment old-chain bypass.
- No unique same-unit timestamp constraint.
- No database constraints for type/unit/time/date.
- Public Event writes without chain validation.
- Same-time validation message not shown.
- Invalid times coerced to 00:00.
- Invalid/incomplete grid rows silently dropped.
- ESD adjustment on invalid stopped ESD.
- Public `Calculate` still executing legacy logic.
- Cumulative runtime report omitting history before arbitrary `dateFrom`.
- Unstable ordering for duplicate/same-time Event rows.
- U4 omitted by one public normalizer.
- Event grid scroll bars disabled.
- Inaccurate irreversible-delete wording.
- Mixed-language labels and insufficient corrective messages.
- Test seeder bypassing locks/validation from form handlers.
- No automated Event tests.

## 13. Recommended behavior to carry into the generalized platform

This is a behavior boundary, not a full platform redesign.

1. Carry forward the compact staged editor, configured-unit drop-down, four fixed Event types, minute spinner, read-only chronological list, explicit edit/apply/cancel, and OH warning.
2. Treat each Add/Edit/Delete as an explicit Event command with stable Event id. Before commit, open one database transaction, load the Trusted Runtime Baseline and the complete canonical Event chain for every affected unit, apply the proposed mutation in memory, sort by timestamp plus stable id for diagnostics, reject duplicate timestamps, replay the approved three-state machine, and commit only if the entire resulting chain is valid.
3. Validate both old and new unit when editing unit assignment. Validate the affected unit even when deletion leaves no Events on the selected day.
4. Keep early UI validation for responsiveness, but make the same pure domain validator mandatory in the application/persistence command. Return structured error codes/details; the UI formats a clear Persian message with unit, Event/date/time, exact current state/reason, allowed correction, and any later Event invalidated.
5. Back correctness with database constraints: canonical type, canonical minute timestamp, valid unit reference, unique `(unit,timestamp)`, and appropriate date validity. Existing data must be audited/remediated before constraints are introduced; no destructive migration is authorized by this audit.
6. Keep Events optional for day completeness and allow them to be saved independently of the 12 observations/daily unique record, while still enforcing the approved sequential operating-day eligibility.
7. Runtime reconstruction must begin at the Trusted Runtime Baseline effective boundary, not synthesize START. Events alone advance state. Compute and expose separately: physical period runtime, ESD adjustment, cumulative runtime, RuntimeAfterOH, physical service days, and physical period-clipped LongestRun.
8. OH is accepted only in Stopped, creates StoppedAfterOH, leaves cumulative unchanged, and resets RuntimeAfterOH. Running+OH message must tell the operator to record the actual NSD/ESD shutdown first.
9. ESD adjustment is applied only during accepted Running->ESD. It increases the three approved runtime totals where applicable but does not mark service day or extend LongestRun. Preserve it as a separate auditable component, not just an opaque aggregate.
10. Preserve Persian operating-date conventions and the 00:00 day boundary. Store an unambiguous canonical timestamp while displaying Persian date and `HH:mm`.
11. Display the grid in authoritative chronological order. Different units may share a minute; same unit may not. Provide vertical scroll and retain stable selection after refresh.
12. Version runtime calculation logic used for finalized snapshots so historical finalized reports remain explainable after correction.

## 14. Exact automated test cases required before reuse

All tests should run against a temporary SQLite database with real schema/constraints and a pure domain test layer. Each mutation integration test must assert both result/message and post-transaction database state. For rejected commands, assert zero changes to Events and dependent persisted outputs.

### A. State transition matrix

For each Unit independently, seed baseline state and assert:

1. Stopped + START succeeds and yields Running.
2. Stopped + NSD rejects with Persian unit, Event timestamp, reason “already stopped,” and correction.
3. Stopped + ESD rejects equivalently.
4. Stopped + OH succeeds, yields StoppedAfterOH, cumulative unchanged, after-OH zero.
5. Running + START rejects and leaves chain/runtime unchanged.
6. Running + NSD succeeds and yields Stopped.
7. Running + ESD succeeds and yields Stopped, with conditional adjustment assertions.
8. Running + OH rejects and instructs shutdown first.
9. StoppedAfterOH + START succeeds and yields Running.
10. StoppedAfterOH + NSD rejects.
11. StoppedAfterOH + ESD rejects.
12. StoppedAfterOH + OH rejects.
13. Execute all 12 cases for baseline-initial state and for state reached through stored Events.

### B. Add validation and duplicate behavior

14. Add canonical START at 08:05 stores minute precision exactly.
15. Same Unit/date/time with different type rejects.
16. Same Unit/date/time identical type rejects.
17. Different Units at identical date/time both succeed.
18. Same Unit same time across different dates succeeds.
19. Input with seconds is rejected or explicitly canonicalized before command construction according to chosen UI contract; database must contain minute precision only.
20. Invalid time `24:00`, negative, non-time, empty, or second-bearing direct API input rejects; none becomes 00:00.
21. Invalid type, unit, Persian date, date before baseline/data start, and nonexistent configured unit reject below UI.
22. Database unique constraint independently rejects a duplicate inserted through the lowest permitted repository path.

### C. Edit chain reconstruction

23. Edit type while chain remains valid succeeds and preserves Event id/audit history.
24. Edit timestamp earlier/later within day and assert chronological replay.
25. Edit across midnight and Persian month boundary; reject if any later Event becomes invalid.
26. Edit unit U1->U2 validates both U1 removal and U2 insertion; reject if either chain fails.
27. Edit START to NSD when next-day NSD depends on START; reject and identify the later affected Event.
28. Edit the earliest Event after baseline and replay through the final Event, not only the next row.
29. Edit remark only and assert no runtime/state change but auditable update.
30. Edit Event in finalized month rejects below UI, with no partial write.

### D. Delete chain reconstruction

31. Delete an Event whose removal leaves later Event valid succeeds.
32. Delete the only Event on a day when next-day Event becomes invalid; reject.
33. Delete all Events for an affected unit/day; still validate that unit.
34. Delete one unit's Events while another unit retains Events; validate both affected chains correctly.
35. Delete across Persian month/year boundary and report exact later invalid Event.
36. Delete in finalized month rejects below UI.
37. Delete command failure rolls back Event and audit rows atomically.

### E. Sequential operating-day rules and optionality

38. First eligible Event operating day equals `data_start_date`.
39. Future/out-of-sequence Event operating day rejects with selected and required Persian dates.
40. Next Persian day succeeds across 31->01 month boundary, Esfand leap/non-leap boundary, and year boundary.
41. Day without Events remains complete when 12 hourly + unique requirements are satisfied.
42. Event can be saved when hourly observations are not yet available, if the operating day is otherwise eligible under approved sequencing.
43. Event save never creates/fakes hourly ST/RPM observations and never cross-validates them.

### F. Runtime physical calculations

44. Baseline Stopped, START 08:00, NSD 10:30 => 2.5 physical hours, one ServiceDay, 2.5 LongestRun.
45. Run 23:30->00:30 => 1.0 hour, two ServiceDays, correct Persian days.
46. Run ending exactly 00:00 => prior day only.
47. Run starting exactly 00:00 and positive duration => new day only.
48. Zero-duration invalid chain cannot create ServiceDay.
49. Baseline Running at period start with stop inside period clips physical period runtime and LongestRun to period start.
50. Run starts before period and ends after period; LongestRun equals clipped report duration.
51. Open run at period end closes at next-day 00:00 exclusive.
52. Multi-month/year and Persian leap-boundary physical run produces exact duration/service days.
53. Hourly ST/RPM contradictions do not alter any runtime result.

### G. OH and RuntimeAfterOH

54. Stopped + OH resets after-OH to zero, cumulative unchanged.
55. Physical runtime before OH remains in cumulative.
56. START after OH accumulates after-OH from zero.
57. Second OH without intervening START rejects.
58. Running + OH rejects and leaves all runtime totals unchanged.
59. OH outside selected period establishes correct state/after-OH baseline for later report reconstruction.

### H. ESD adjustment and auditability

60. Valid Running->ESD with adjustment disabled: physical runtime only.
61. Valid Running->ESD with positive adjustment: period, cumulative, and after-OH each increase exactly once.
62. Same after an OH+START chain increases RuntimeAfterOH.
63. Stopped+ESD rejects and adds zero adjustment.
64. ESD adjustment alone never creates ServiceDay.
65. ESD adjustment never changes LongestRun.
66. Physical runtime and adjustment values are independently queryable/auditable and sum to displayed adjusted total.
67. Zero/negative configured adjustment produces no increase and is validated according to settings rules.

### I. Reporting and ordering

68. Report beginning at data start reconstructs cumulative runtime from baseline.
69. Report beginning after data start includes all prior physical/adjustment history in cumulative totals while clipping period metrics.
70. Event at 06:59 is Night; 07:00 and 18:59 are Day; 19:00 is Night.
71. Event log is ordered by timestamp; simultaneous different-unit Events use deterministic unit/id presentation order without affecting per-unit state.
72. Finalized snapshot records corrected runtime/service-day/LongestRun/ESD components.
73. Reading finalized snapshot returns exactly stored values and calculation version.
74. Unsupported/corrupt database Event causes explicit integrity failure, never silent omission or 00:00 substitution.

### J. UI automation/accessibility

75. Unit -> type -> time -> remark/Add tab order is predictable; Enter adds/applies only in Event editor context and Escape clears selection.
76. Selecting NSD/ESD enables remark; changing away warns before discarding nonempty remark.
77. Add/Apply immediate validation focuses the offending control/row and shows Persian structured message.
78. Duplicate message includes Unit, Persian date, time, exact reason, and correction.
79. Running+OH message includes shutdown-first correction.
80. Delete message distinguishes staged change from committed deletion; Cancel restores row.
81. More rows than viewport remain accessible through vertical scroll.
82. Editing selection remains stable after chronological re-sort.
83. Locked month disables mutation and direct command still rejects.
84. Rasht and Ramsar configured units remain isolated; U4 works only for the station configuration that owns it.

### K. Transaction/concurrency and bypass resistance

85. Validation and insert execute in one transaction; injected failure after validation rolls back.
86. Full-chain load and concurrent conflicting writer cannot both commit an invalid chain.
87. Direct repository command cannot bypass type, duplicate, state, date-sequence, or lock rules.
88. Import/test fixture path uses the same validation or is isolated from production database.
89. Unique constraint migration detects and reports existing duplicates without deleting user data.
90. Finalized-period mutation attempt through every exposed write API rejects.

## 15. Final verdict

| Component | Verdict | Answer |
|---|---|---|
| Event UI | **Can reuse with adaptation** | Yes—the compact staged editor/grid, configured drop-downs, minute spinner, explicit Apply/Clear/Delete, read-only loaded mode, and OH warning are worth preserving. Do not copy its validation timing, language inconsistency, scrolling/tab-order limitations, or coupling to full daily save. |
| Event persistence | **Cannot reuse** | No—the delete/reinsert day replacement, missing stable identity/audit, missing database constraints, public validation bypass, and pre-transaction validation must be replaced. Preserve only atomic transactions and below-UI monthly lock checking as patterns. |
| Event validation | **Cannot reuse** | No—the current service is incomplete and wrong for Running+OH, misses deletion/unit-change chains, does not reconstruct the full baseline-to-end chain, can reject silently, coerces bad times, and is not mandatory below UI. Small pure predicates/message ideas may be adapted. |
| Event runtime logic | **Cannot reuse** | No—the public path is confirmed legacy, range history is incomplete for cumulative reconstruction, invalid transitions affect calculations, ESD adjustment can apply while stopped, and OH can terminate Running. Retain only verified formula fragments (midnight boundary, shift boundary, physical-only service-day and ESD-excluded LongestRun) as tested specifications. |

## Build and dependency audit record

- Full command: `dotnet build .\Rah_Negar.sln --no-restore`.
- Result: **succeeded**, 0 errors, 3 warnings.
- Warnings: NU1701 for transitive `OpenTK 3.1.0`, `OpenTK.GLControl 3.1.0`, and `SkiaSharp.Views.WindowsForms 3.119.0`, restored for .NET Framework rather than `net8.0-windows7.0`. These are compatibility risks, not confirmed Event defects.
- `dotnet list ... package --vulnerable --include-transitive`: no known vulnerable packages from configured sources as of audit date.
- `dotnet list ... package --deprecated`: no deprecated packages from configured sources.
- Outdated check reports minor updates for ClosedXML, Microsoft.Data.Sqlite, QuestPDF, ScottPlot.WinForms, Serilog and SourceGear.sqlite3, plus major-version updates for SQLitePCLRaw packages. No upgrade is recommended or performed in this read-only audit.
- Dependency redundancy risk: the project references `Microsoft.Data.Sqlite` both as a NuGet package and an explicit DLL HintPath, and carries multiple SQLite provider/native packages (`Rah_Negar.csproj:12-35`). Whether assemblies are actually redundant at publish/runtime requires a controlled packaging test; it is not classified as a confirmed Event bug.
- No automated test project was found; `TestDataSeederService` is a mutation utility, not a test suite.

## Potential issues requiring validation (not classified as confirmed bugs)

1. Interactive DPI/render quality of the Event tab at 125%, 150%, and RTL display needs visual testing. Code uses DPI scaling and shared sizing, but fixed control coordinates and widths may clip localized text.
2. Whether the hidden/visible state of the test-seeder buttons makes them reachable in the shipped build must be confirmed from the actual executable UI; handlers are present and therefore remain a serious reachable-code risk.
3. Existing production databases may predate current schema builder/indexes or contain legacy formats/duplicates. This audit did not open or mutate user databases.
4. The exact business interpretation currently shown to operators for `RuntimeHours` (period-only versus cumulative) should be confirmed with report labels and users. The code definitely combines fixed baseline plus selected-range runtime; the severity assumes the approved cumulative meaning.
5. Remark restriction to NSD/ESD is current behavior but is not stated in the 18 approved rules. Preserve only after product confirmation.

