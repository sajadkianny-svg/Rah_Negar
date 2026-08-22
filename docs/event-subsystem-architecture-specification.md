# Event Subsystem Architecture Specification

**Project:** Generalized RahNegar platform  
**Document status:** Target architecture specification  
**Source basis:** Approved Event business rules and `docs/legacy-event-subsystem-audit.md`  
**Scope:** Event entry, mutation, validation, persistence, audit, runtime reconstruction, reporting integration, and UI boundaries  
**Out of scope:** Production implementation, complete platform redesign, destructive database migration, and changes to the current legacy application

## 1. Executive overview

The redesigned Event subsystem is the authoritative record of operational state changes for station units. Its architecture must guarantee that every accepted Add, Edit, or Delete leaves the complete Event history of every affected Unit valid from its Trusted Runtime Baseline through its latest Event. Events are optional for daily completeness, but any Event that exists must be structurally valid, chronologically unique for its Unit, allowed by the state machine, eligible under sequential operating-day rules, and auditable.

The legacy subsystem cannot be reused directly because its critical invariants are not enforced at one authoritative boundary. It replaces all Events for a day rather than preserving stable Event identity, validates before opening the mutation transaction, exposes persistence methods that bypass chain validation, has no database uniqueness or domain constraints, and does not validate the old Unit when an Event changes Unit or when deletion leaves no same-day Events. Its transition table incorrectly permits `Running + OH`. Its public runtime calculation path still invokes legacy logic, accepts invalid transitions, applies ESD adjustment without proving a valid Running-to-ESD transition, and reconstructs arbitrary report ranges without the complete history from the Trusted Runtime Baseline.

The old UX is preserved as an interaction pattern. The new UI retains a compact Event editor, configured Unit selector, fixed Event-type selector, minute-level `HH:mm` time input, explicit Add/Edit/Delete actions, a read-only chronological grid, clear selection/cancel behavior, and a prominent OH consequence warning. These elements are familiar, efficient, and reduce free-text input errors.

The business and persistence layers are replaced. The new subsystem uses explicit commands with stable Event identity, one transactional command handler, complete-chain reconstruction, a pure state-machine validator, structured failures, database constraints, and append-only audit records. Runtime is recomputed from Events and the Trusted Runtime Baseline only; hourly ST/RPM observations neither determine nor validate runtime.

The intended layering is:

```text
WinForms UI
    -> Event application commands
        -> transactional Event command handler
            -> repositories + finalized-period policy + operating-day policy
            -> complete-chain validator/state machine
            -> Event and EventAudit persistence

Reporting
    -> authoritative Event-chain reader
        -> runtime projection engine
            -> physical runtime, adjustment, adjusted runtime,
               RuntimeAfterOH, ServiceDays, LongestRun
```

The UI may perform early validation for responsiveness, but it is never the authority. Only a successful command transaction changes Event state.

## 2. Domain model

### 2.1 Event entity

`Event` represents one user-recorded operational state transition for one Unit at one minute. It has stable identity and is the only event-history input to runtime reconstruction.

| Field | Type/shape | Purpose | Lifecycle and invariants |
|---|---|---|---|
| `EventId` | Stable opaque identifier | Identifies one Event across reads, edits, audit entries, reports, and synchronization inside the offline database. | Created once during Add; never changes. Edit preserves it. Delete removes or retires the Event according to the persistence strategy while audit history retains the identifier. It must not be derived from row position or date. |
| `StationId` | Foreign-key identifier | Owns the Event at station scope and prevents Rasht/Ramsar or future-station leakage. | Assigned from authenticated/current station context on Add; immutable. Edit commands must not move an Event between stations. It references an existing enabled Station. |
| `UnitId` | Foreign-key identifier | Identifies the Unit whose operational state changes. | Assigned on Add. It may change through an explicit Edit if authorized, but both the old and new Unit chains must be reconstructed and validated. It must belong to `StationId` and be valid for the Event date according to station configuration. |
| `EventType` | Closed enum/value object | Expresses exactly one of `START`, `NSD`, `ESD`, `OH`. | Required on Add and editable only through an explicit Edit. Canonical uppercase representation is persisted. Unknown aliases or arbitrary strings are rejected, not normalized silently. |
| `EventDate` | Persian operating-date value object | Preserves the application's Persian operating-date convention and supports sequential daily-entry/finalized-month policies. | Required and validated as a real Persian date. Stored canonically, never inferred from display text. An Edit that changes the date validates old and new finalized periods and replays the affected chain. |
| `EventTime` | Minute-precision local time value object | Captures the operator-entered time within the operating day. | Required, range `00:00` through `23:59`, seconds prohibited. Persisted in canonical `HH:mm` form or an equivalent minute integer. Invalid values are rejected; they never fall back to midnight. |
| `EventDateTime` | Derived canonical chronological value | Provides unambiguous chronological ordering from `EventDate` and `EventTime`. | Computed by one Persian-calendar conversion service/value object. It must exactly correspond to `EventDate` and `EventTime`; callers cannot supply a conflicting value. It is used for chain order and Unit/timestamp uniqueness. |
| `Remark` | Bounded Unicode text, nullable/empty | Stores operator context without participating in state or runtime calculation. | Trimmed according to an explicit policy, length validated, and never silently truncated. Whether remarks are allowed for all types or only selected types is a configurable/product rule that must be decided before implementation; it must not alter state transitions. |
| `CreatedAt` | UTC timestamp | Records when the Event record was created, independently of when the operational Event occurred. | Set by the application/domain persistence boundary on Add; immutable. Not trusted from ordinary UI input. |
| `CreatedBy` | User identifier | Identifies the user who created the Event. | Captured from authenticated session on Add; immutable. It must reference the local user/account identity used by the offline application. |
| `UpdatedAt` | UTC timestamp, nullable | Records the most recent accepted Edit. | Null at creation or equal to policy-defined creation value; updated only after a successful Edit. Delete does not rewrite this field merely to simulate deletion. |

`EventDateTime` is a domain value derived from Persian date and minute time, not a second independent user field. If the database stores both components and a sortable timestamp, the persistence layer must guarantee they cannot disagree. In an offline local-time application, conversion rules and calendar conventions must be centralized and deterministic; no cloud or external time service is involved.

### 2.2 EventAudit entity

`EventAudit` is an append-only history of accepted Event mutations. Audit creation occurs in the same transaction as the Event mutation. A rejected command creates no successful-change audit row; operational error logging may record the attempt separately, but it is not an `EventAudit` change record.

| Field | Type/shape | Purpose | Lifecycle and invariants |
|---|---|---|---|
| `AuditId` | Stable opaque identifier | Uniquely identifies an audit entry. | Created once for each accepted Add/Edit/Delete action; immutable. |
| `EventId` | Identifier referencing the Event identity | Connects the audit entry to the Event being changed. | Required for all actions. It must remain meaningful after Event deletion; therefore database design must not cascade-delete audit rows. A strict foreign key may be nullable/deferrable or point to a retained/tombstoned Event depending on the selected deletion model. |
| `ActionType` | Closed enum | Identifies `ADD`, `EDIT`, or `DELETE`. | Set by command handler; immutable. Other actions require an explicit architecture revision rather than arbitrary strings. |
| `OldValue` | Versioned serialized snapshot, nullable | Preserves the complete canonical Event value before mutation. | Null for Add; required for Edit/Delete. Serialization includes schema/version metadata and all business fields needed to reconstruct what changed. |
| `NewValue` | Versioned serialized snapshot, nullable | Preserves the complete canonical Event value after mutation. | Required for Add/Edit; null for Delete. It must reflect the committed value, not raw UI input. |
| `User` | User identifier/snapshot | Identifies the actor responsible for the mutation. | Captured from trusted command context. It should retain a stable identifier and, if required for historical display, a non-authoritative display-name snapshot. |
| `Timestamp` | UTC timestamp | Records when the mutation committed. | Generated inside the command transaction from the application clock abstraction; immutable. |
| `Reason` | Required or policy-governed bounded text | Explains why an Event was changed, especially for Edit/Delete. | Add may use a standard reason or optional operator note. Edit/Delete should require a meaningful reason. It is validated and never silently truncated. |

### 2.3 Supporting domain concepts

The Event subsystem depends on, but does not redefine, these contracts:

- `TrustedRuntimeBaseline`: per Station/Unit effective boundary, initial state (`Stopped`, `Running`, or a baseline representation mapped to a valid state), cumulative physical/adjusted values as approved, and RuntimeAfterOH baseline. It is authoritative and must not be replaced with an invented historical START.
- `Station` and `Unit`: authoritative ownership and station-specific isolation. A Unit belongs to exactly one Station within the relevant validity period.
- `OperatingDayPolicy`: determines whether an Event date is eligible under sequential daily-entry rules. Event optionality does not waive date sequencing.
- `FinalizedPeriodPolicy`: determines whether the Event's operating month is immutable.
- `EventState`: the closed set `Stopped`, `Running`, `StoppedAfterOH`.
- `DomainValidationError`: structured code plus Unit, Event identity/type/date/time, reason, correction, and optionally the later Event invalidated by the proposed mutation.

## 3. Event state machine

### 3.1 States

- `Stopped`: the Unit is not physically running and has not most recently entered the special post-overhaul stopped state.
- `Running`: physical runtime accrues continuously until a valid NSD or ESD transition. OH is forbidden while in this state.
- `StoppedAfterOH`: the Unit is stopped after an accepted OH. RuntimeAfterOH is zero and remains zero until a valid START begins new physical runtime.

The state machine is pure: given a current state and Event type, it either returns the next state and approved effects or a structured validation error. It does not access UI controls, display messages, write the database, or silently ignore invalid Events.

### 3.2 Valid transitions

| Current state | Event | Next state | State/runtime effect |
|---|---|---|---|
| `Stopped` | `START` | `Running` | Opens physical running interval at EventDateTime. |
| `Stopped` | `OH` | `StoppedAfterOH` | Leaves cumulative runtime unchanged and resets RuntimeAfterOH to zero. No physical run is closed because the Unit is already stopped. |
| `Running` | `NSD` | `Stopped` | Closes physical running interval at EventDateTime. No ESD adjustment. |
| `Running` | `ESD` | `Stopped` | Closes physical running interval and applies the approved ESD adjustment exactly once when enabled/configured. |
| `StoppedAfterOH` | `START` | `Running` | Opens a new physical run; RuntimeAfterOH begins accruing from zero. |

### 3.3 Forbidden transitions and correction-message contract

Every forbidden transition returns a failure; it never mutates state, runtime projections, Event rows, or audit rows. The Persian user-facing message must identify the Unit, proposed Event type, Persian date and `HH:mm`, current state in operator-friendly language, exact reason, and a concrete correction. If the error is discovered because an Edit/Delete invalidates a later Event, the message must also identify that later Event and explain that the earlier change cannot be accepted until the chain is corrected.

| Current state + Event | Why forbidden | Required correction guidance |
|---|---|---|
| `Stopped + NSD` | NSD is a shutdown transition, but the Unit is already stopped. | Tell the user not to enter NSD in the stopped state. If the Unit actually started earlier, enter/correct the missing START at its real time first. Do not suggest inventing history. |
| `Stopped + ESD` | ESD is valid only for a running Unit and cannot create an ESD adjustment while stopped. | Tell the user not to enter ESD while stopped; correct the preceding Event chain or the proposed type/time. State that no ESD runtime adjustment will be applied. |
| `Running + START` | A running Unit cannot start again; a repeated START would split or double-count a run. | Tell the user the Unit is already running and identify the START/open state that established it. Correct/remove the duplicate START or record the actual shutdown first if one occurred. |
| `Running + OH` | OH cannot directly terminate a running Unit. Shutdown must be recorded first. | Tell the user to record the actual NSD or ESD at its real time before OH, then place OH after the valid shutdown. Never auto-create the shutdown. |
| `StoppedAfterOH + NSD` | The Unit is already stopped after OH; NSD cannot close a non-existent run. | Tell the user that START is the only valid next Event. Correct/remove NSD or enter the real START first if the Unit ran. |
| `StoppedAfterOH + ESD` | The Unit is already stopped after OH; ESD cannot close a run or earn an adjustment. | Tell the user that START is the only valid next Event and that ESD adjustment requires a valid Running-to-ESD transition. |
| `StoppedAfterOH + OH` | A second OH is not valid until the Unit has returned to Running and later stopped through a valid chain. | Tell the user the Unit is already in post-OH stopped state; correct/remove the duplicate OH. |

Messages are presentation-layer translations of structured domain errors. The domain supplies machine-readable reason/correction codes and factual context; it does not call `MessageBox`.

## 4. Event command architecture

All mutations enter through one application boundary. UI code, imports, maintenance tools, and tests must use these commands or an explicitly isolated migration pathway. Repositories do not expose public “insert arbitrary Event” operations that bypass validation.

### 4.1 Shared command result contract

A success result contains:

- command/action type;
- committed `EventId`;
- canonical committed Event for Add/Edit, or deleted Event identity/snapshot for Delete;
- affected Unit ids;
- audit identifier;
- optional refreshed chronological rows/state summary for the UI.

A failure result contains:

- stable error code;
- Persian-ready structured context: Station, Unit, proposed/affected Event, date/time;
- exact reason;
- required correction;
- later invalid Event when chain replay fails downstream;
- conflict/finalized/duplicate classification;
- no partial changes.

Expected business failures return results rather than general exceptions. Infrastructure failures are logged and translated to a safe failure result at the application boundary; the transaction is rolled back.

### 4.2 AddEventCommand

**Input**

- `StationId`, `UnitId`, `EventType`, `EventDate`, `EventTime`, optional `Remark`;
- trusted command context containing current user and correlation/request identifier;
- optional `Reason` if policy requires one for Add.

The UI does not provide `EventId`, `EventDateTime`, `CreatedAt`, `CreatedBy`, or audit fields. Those are assigned/derived by the authoritative handler.

**Validation pipeline**

1. Parse canonical Event values without silent coercion.
2. Verify Station/Unit ownership and authorization.
3. Begin transaction and enforce finalized-period lock.
4. Execute the complete validation pipeline defined in section 5 for the new Unit.
5. Enforce database constraints when inserting.
6. Insert Event and ADD audit snapshot.

**Transaction boundary**

The lock check, baseline/chain read, in-memory mutation/replay, uniqueness decision, Event insert, and EventAudit insert occur in one transaction. Commit occurs only after all writes succeed.

**Success result**

Returns stable Event id, canonical Event, audit id, and resulting Unit state. The UI refreshes the chronological grid from committed data.

**Failure result**

Returns structured validation, duplicate, operating-day, finalized-period, authorization, conflict, or infrastructure failure. No Event/audit change is committed.

### 4.3 EditEventCommand

**Input**

- target `EventId`;
- intended replacement values: `UnitId`, `EventType`, `EventDate`, `EventTime`, `Remark`;
- required `Reason`;
- trusted user/correlation context;
- concurrency token/version or expected `UpdatedAt` where supported.

`StationId` is taken from the stored Event and command context; an Edit cannot transfer an Event to another Station.

**Validation pipeline**

1. Load the current Event and verify it exists, belongs to Station, and matches concurrency expectation.
2. Parse replacement values and determine affected Units: always old Unit; also new Unit if different.
3. Determine affected finalized periods: old and new Event months when date changes. Both must be editable.
4. Run the complete section 5 pipeline over both affected Unit chains after replacing the Event in memory.
5. Update the stable Event row and insert an EDIT audit with full old/new canonical snapshots.

**Transaction boundary**

Current-row load, lock checks, all affected-chain reads/replays, uniqueness validation, update, and audit insert are atomic in one transaction. A failure in either Unit chain rejects the entire Edit.

**Success result**

Returns the same EventId, updated canonical Event, audit id, affected Units, and their resulting states.

**Failure result**

Includes the proposed Event and the precise first invalid transition in either old or new Unit chain. If a later Event becomes invalid, it is identified. Concurrency conflict reports that the row changed and instructs reload/retry. No changes commit.

### 4.4 DeleteEventCommand

**Input**

- target `EventId`;
- required `Reason`;
- trusted user/correlation context;
- concurrency token/version where supported.

**Validation pipeline**

1. Load the Event and verify Station ownership/authorization/concurrency.
2. Enforce the finalized-month lock for the stored Event date.
3. Mark the stored Unit as affected even if deletion leaves no Events on that day or no Events after baseline.
4. Remove the target Event in memory and execute the complete section 5 pipeline through every later Event.
5. Delete/retire the Event according to the chosen persistence strategy and insert DELETE audit with old snapshot and null new value.

**Transaction boundary**

Load, lock, complete-chain validation, delete/retire, and audit insertion occur in one transaction. Audit preservation must survive Event deletion.

**Success result**

Returns deleted Event identity/snapshot, audit id, affected Unit, and resulting Unit state.

**Failure result**

Identifies any later Event invalidated by the deletion and tells the user which chain inconsistency must be corrected. No row or audit mutation commits.

## 5. Validation pipeline

The authoritative pipeline order for Add/Edit/Delete is fixed:

1. **Load Trusted Runtime Baseline.** Load the baseline effective at the start of authoritative Event history for every affected Station/Unit. Missing, ambiguous, or invalid baseline is a blocking integrity failure. Never invent a historical START.
2. **Load complete Event chain for affected Units.** Read every authoritative Event from the baseline boundary through the latest stored Event, including Events before and after the proposed mutation date. The query uses canonical chronological ordering and stable identity as a diagnostic tie-breaker.
3. **Apply proposed mutation in memory.** Add the proposed Event, replace the exact EventId, or remove it. No database Event mutation has occurred yet.
4. **Sort by EventDateTime.** Sort independently per Unit by canonical minute timestamp. EventId may be a deterministic presentation/diagnostic key, but it must never legitimize two same-Unit Events at the same timestamp.
5. **Replay complete state machine.** Initialize from the baseline state and apply every Event in order. Stop on the first forbidden transition and return its full context. Runtime side effects may be projected during the same replay, but validation must remain deterministic and pure.
6. **Validate duplicate timestamps.** Reject more than one Event for the same Unit at the same EventDateTime. Different Units may share a timestamp. This application check provides a clear message; the database unique constraint provides final defense. Implementations may detect duplicates before replay for efficiency, but the externally defined validation stages and failure precedence must remain deterministic.
7. **Validate operating-day rules.** Confirm all newly introduced or moved Event dates are allowed by sequential daily-entry policy and are not before the data/baseline start. Event optionality means a day can be complete without an Event; it does not permit future/out-of-sequence Event entry.
8. **Commit only if valid.** Recheck/enforce database constraints and finalized locks inside the transaction, write the Event mutation plus audit row, and commit atomically. Any failure rolls back everything.

Structural parsing, Station/Unit ownership, authorization, target existence, concurrency, and finalized-period eligibility are command preconditions performed inside the same transaction before the eight domain stages. They do not replace any stage.

### Affected-Unit rules

- Add affects the new Unit.
- Edit always affects the old Unit. If `UnitId` changes, it also affects the new Unit. Both complete chains must pass.
- Delete always affects the stored Unit, even when no Events remain on the selected day or after deletion.
- Changing Event date/time/type can invalidate Events arbitrarily far later. Checking only the previous and next Event is insufficient; all later Events through the end of the chain are replayed.
- A failure in any affected chain rejects the entire multi-Unit operation. There is no partial Edit.

### Failure determinism

Validation returns the earliest chronological violation according to a documented precedence: malformed command/precondition, finalized/authorization conflict, duplicate timestamp, then first invalid state transition, then operating-day violation if not already a command precondition. The exact implementation may optimize queries, but the user must receive consistent results for the same authoritative state.

## 6. Runtime calculation architecture

Runtime is a projection over the Trusted Runtime Baseline and complete, already validated Event chains. The runtime engine does not repair bad Events, ignore unsupported values, or decide transition validity independently. If invalid persisted history is encountered during migration or diagnostics, calculation fails explicitly with an integrity result.

### 6.1 Metric separation

| Metric | Definition | Included inputs |
|---|---|---|
| `Physical Runtime` | Actual elapsed time while state is Running, clipped to the requested reporting period when reported as a period metric. | Baseline state and valid START-to-NSD/ESD/open-period running intervals only. |
| `ESD Adjustment` | Configured additive runtime applied exactly once to a valid Running-to-ESD transition. | Valid ESD transitions only; separately itemized by EventId/configuration version. |
| `Adjusted Runtime` | Physical Runtime plus ESD Adjustment for the specified scope. | Explicit sum; never stored or presented as though all time were physical. |
| `RuntimeAfterOH` | Adjusted runtime accumulated since the latest valid OH reset, starting from the Trusted Baseline value when no later OH exists. | Physical running after the reset plus valid ESD adjustments after the reset. OH sets it to zero. |
| `ServiceDays` | Set/count of operating days with any positive physical Running overlap in `[00:00, 24:00)`. | Physical Runtime overlap only. ESD adjustment alone never creates a day. |
| `LongestRun` | Longest continuous physical Running interval intersected with the reporting period. | Physical Runtime only, period-clipped. ESD adjustment excluded. |

The architecture distinguishes cumulative values from period values. Reconstruction begins at the Trusted Runtime Baseline effective boundary and advances through all Events to the report end. Cumulative Runtime includes baseline plus all subsequent physical runtime and approved adjustments. Period Physical/Adjusted Runtime, ServiceDays, and LongestRun are clipped to the requested period without discarding history needed to establish state at period start.

### 6.2 Authority and exclusions

- Events are the sole operational runtime authority.
- Hourly ST/RPM observations do not start, stop, adjust, corroborate, reject, or repair runtime.
- No runtime service queries hourly observations as a state input.
- Initial Running Units are represented by Trusted Runtime Baseline state. The engine does not synthesize or persist historical START Events.

### 6.3 ESD rules

- ESD adjustment is considered only after the state machine accepts `Running + ESD -> Stopped`.
- When enabled and positive, it increases Period Adjusted Runtime when the ESD belongs to the period, Cumulative Runtime, and RuntimeAfterOH when applicable.
- It never creates ServiceDay and never extends LongestRun.
- Physical elapsed duration and adjustment remain separately auditable, including source EventId and effective adjustment setting/version.
- Stopped/StoppedAfterOH + ESD is rejected and produces zero adjustment.

### 6.4 OH rules

- OH is valid only from `Stopped` and transitions to `StoppedAfterOH`.
- OH does not reset or reduce cumulative physical/adjusted runtime.
- OH resets RuntimeAfterOH to zero at its EventDateTime.
- OH never closes a running interval because `Running + OH` is rejected before calculation/mutation.
- A later START begins a physical run and new after-OH accumulation.

### 6.5 Time boundaries

- Runtime operating-day boundary is local `00:00`.
- Positive overlap is required for ServiceDay; a run ending exactly at midnight does not mark the new day.
- Reporting shift classification is Day for `07:00 <= time < 19:00`; Night for `19:00 <= time < 07:00`.
- Persian date conversion is centralized and tested across month, year, and leap-year boundaries.

## 7. Database design principles

This specification defines constraints and relationships, not final migration SQL.

### Canonical values

- `EventType` is persisted only as canonical `START`, `NSD`, `ESD`, or `OH`, preferably via a constrained code column or lookup with a closed application enum.
- Event time has minute precision only. Canonical representation is either `HH:mm` with a strict database check or integer minute-of-day `0..1439`. Seconds and invalid strings are impossible.
- Event date is stored in one validated Persian-date representation consistent with platform conventions. A sortable canonical timestamp/key is derived consistently and cannot conflict with date/time components.
- Remarks and reasons have explicit Unicode length limits and reject overflow; they are not silently truncated.

### Keys and ownership

- `EventId` and `AuditId` are stable primary keys.
- `StationId` references Station.
- `UnitId` references Unit, and database/application ownership ensures the Unit belongs to the Event's Station. Where SQLite cannot express a temporal ownership rule directly, the transaction handler enforces it and schema uses the strongest composite foreign key available.
- Audit rows are never cascade-deleted with Event rows. If hard deletion is selected, audit design retains EventId without a destructive cascade; a retained/tombstoned Event design is preferable when it simplifies referential audit integrity.

### Uniqueness and checks

- A unique constraint/index enforces `(UnitId, EventDateTime)`; Station may be included if Unit identity is only station-local.
- Different Units may have identical EventDateTime.
- Database checks enforce allowed EventType, minute range/format, nonempty required identifiers, and valid serialized audit action type.
- Database constraints are final defense, not a substitute for complete-chain validation and Persian messages.

### Indexes

At minimum:

- unique Unit/timestamp index;
- `(StationId, EventDateTime)` for chronological station grids/reports;
- `(UnitId, EventDateTime)` for full-chain reconstruction;
- `(EventDate, StationId)` or equivalent for operating-day and finalized-period queries;
- `EventAudit(EventId, Timestamp)` for history;
- finalized-period lookup indexes defined by the locking subsystem.

Indexes must support deterministic ordering without relying on SQLite row return order or incidental rowid. EventId is the final diagnostic/display tie-breaker only where timestamps belong to different Units or while reporting corrupt legacy data.

### Audit preservation

- Every committed Add/Edit/Delete writes exactly one corresponding EventAudit record in the same transaction.
- OldValue/NewValue are versioned canonical snapshots, not ad hoc UI JSON.
- Audit rows are append-only through normal application APIs.
- Runtime adjustment audit links an ESD contribution to its Event and calculation/settings version.
- Finalized report snapshots record calculation version so corrected future logic does not silently reinterpret locked results.

## 8. UI integration principles

### Preserve

- Compact editor rather than inline grid editing.
- Unit selector populated from the active Station's configured Units.
- Fixed Event selector for START, NSD, ESD, OH.
- Minute-only `HH:mm` input using keyboard-friendly spinner behavior.
- Explicit Add and Edit/Apply workflow with clear cancel/selection reset.
- Read-only Event grid as the committed/staged chronological view.
- Prominent confirmation for OH because it resets RuntimeAfterOH.
- Persian operating-date conventions and familiar daily navigation.

### Improve

- Call the command/domain validation service for immediate preflight feedback while retaining authoritative transactional validation at commit.
- Show structured Persian messages containing Unit, proposed/affected Event, Persian date/time, exact state/reason, and corrective action. When a later Event is invalidated, show that Event explicitly.
- Sort the grid chronologically by canonical EventDateTime, then configured Unit display order/EventId where needed. The visual row number is not identity.
- Clearly distinguish staged changes from committed changes. Delete text must not call an uncommitted removal irreversible.
- Provide predictable keyboard order: Unit -> type -> date/time -> remark -> Add/Apply. Enter acts in Event-editor context, Escape cancels selection, and Delete requires explicit selected-row confirmation.
- Enable vertical scrolling and preserve selected EventId after refresh/re-sort.
- Use Persian/localized labels consistently while keeping canonical codes visible where operationally useful.
- Support DPI scaling at 100%, 125%, 150%, 175%, and 200%; avoid fixed widths that clip Persian messages or action labels. Verify RTL reading and focus indicators.
- Never silently clear a nonempty remark when changing Event type; warn or preserve until the command validates the final model.
- Allow Event persistence independently from mandatory hourly/daily unique entry, subject to sequential operating-day eligibility.

The UI does not calculate state from the visible day alone. It may display the pre-Event state returned by a query/application service, but the command handler reconstructs it authoritatively.

## 9. Transaction and locking rules

1. Each Add/Edit/Delete is one atomic transaction containing target read, Station/Unit checks, finalized-period checks, baseline read, complete affected-chain reads, in-memory mutation/replay, duplicate and operating-day validation, Event mutation, and EventAudit insert.
2. Validation reads and mutation must share the same connection/transaction snapshot. Validation performed on an earlier connection is insufficient.
3. Finalized months are immutable. Add is rejected if its Event month is finalized. Delete is rejected if the stored Event month is finalized. Edit is rejected if either old or new Event month is finalized.
4. Lock checks occur below UI and inside the transaction. UI disabling is convenience only.
5. Event validation exists in the domain/application command boundary. Repository methods are internal implementation details and cannot be used to bypass it.
6. Database unique/foreign/check constraints remain active as defense in depth. A constraint conflict rolls back Event and audit writes and is translated to a structured failure.
7. Concurrency is explicit. Edit/Delete use a version or expected UpdatedAt; stale commands fail with reload guidance.
8. Imports and maintenance operations use the same command boundary. A special migration importer may report legacy violations, but it cannot write unvalidated rows into the production target schema without an approved, auditable exception process.
9. No partial success is permitted for an Edit affecting old and new Units. Both chains pass or neither changes.
10. Read-only reporting may use independent transactions/snapshots, but finalized snapshot generation and month locking must atomically bind the calculation result to the exact Event version set it represents.

## 10. Automated test requirements

The Event architecture is not reusable until pure unit tests, SQLite integration tests, transaction/concurrency tests, and UI workflow tests cover these critical groups.

### State transition matrix

- Assert all five valid transitions and all seven forbidden transitions from baseline-derived and Event-derived states.
- Assert forbidden results contain stable error code, Unit, Event date/time, reason, and correction.
- Assert Running+OH never changes state/runtime and instructs shutdown first.

### Add/Edit/Delete chain reconstruction

- Add at beginning, middle, and end of a long chain.
- Edit Event type/date/time and replay every later Event.
- Edit Unit and validate both old and new Unit chains atomically.
- Delete the only same-day Event and still validate the Unit.
- Delete an earlier Event that invalidates an Event days/months later; reject and identify the later Event.
- Confirm stable EventId on Edit and complete ADD/EDIT/DELETE audit snapshots.

### Duplicate timestamps

- Same Unit/same minute rejects regardless of Event type.
- Different Units/same minute succeeds.
- Database constraint independently blocks a bypass attempt.
- Invalid or second-precision time rejects and never becomes `00:00`.

### OH

- Stopped+OH resets only RuntimeAfterOH and reaches StoppedAfterOH.
- Running+OH rejects.
- StoppedAfterOH+OH rejects.
- START after OH accumulates new RuntimeAfterOH while cumulative runtime remains continuous.

### ESD

- Adjustment applies exactly once only to valid Running->ESD.
- It increases adjusted period/cumulative/after-OH values as applicable.
- It does not create ServiceDay or extend LongestRun.
- Stopped/StoppedAfterOH+ESD rejects with zero adjustment.
- Physical and adjustment components remain separately traceable to EventId.

### Time and Persian date boundaries

- Runs spanning 23:59/00:00 mark every and only days with positive physical overlap.
- A run ending exactly at 00:00 does not mark the new day.
- Day/Night boundaries test 06:59, 07:00, 18:59, and 19:00.
- Persian month/year boundaries and leap/non-leap Esfand are tested for sorting, sequential eligibility, locks, runtime duration, and ServiceDays.
- Baseline Running and period clipping produce correct LongestRun.

### Operating day and optionality

- First eligible Event day, next sequential day, and future/out-of-sequence rejection.
- Day completeness succeeds without Events.
- Event command can commit independently of hourly observations when day eligibility passes.
- Contradictory ST/RPM data has no effect on Event validation or runtime.

### Finalized-month protection and transactions

- Add/Edit/Delete through UI, command handler, repository boundary, import, and maintenance routes cannot modify finalized months.
- Edit across months checks both old and new periods.
- Injected failure after validation, Event write, or audit write rolls back all changes.
- Concurrent conflicting commands cannot both commit an invalid or duplicate chain.
- Finalized snapshot records the calculation version and exact consistent Event state.

### UI/DPI and message behavior

- Chronological grid ordering, stable selection, scroll accessibility, Add/Edit/Delete keyboard workflow, and cancel behavior.
- Persian structured messages for every forbidden transition, duplicate, later-invalid Event, lock, date sequence, and concurrency conflict.
- Layout at supported DPI scales and RTL rendering without clipping.

## 11. Migration considerations

Legacy Event data is untrusted input until audited. Migration is a controlled data-quality and business-review process, not a direct table copy.

1. Inventory every legacy `tbl_events` row by database/station. Preserve source database identity and original row id for traceability.
2. Parse and report noncanonical Unit, EventType, Persian date, and time values without silently coercing them.
3. Detect duplicate canonical `(Station, Unit, EventDateTime)` groups before creating the unique constraint. Different Units at the same timestamp are not duplicates.
4. Load each Unit's Trusted Runtime Baseline and replay the complete legacy chain. Report every forbidden transition, including Running+OH, repeated START, stopped NSD/ESD, and repeated OH.
5. Identify chain-order ambiguity caused by duplicate timestamps or malformed textual time. Row id may assist investigation but cannot be treated as approval of simultaneous same-Unit Events.
6. Identify Events in finalized periods and preserve the relationship to finalized report snapshots/calculation versions.
7. Produce a non-destructive exception report with source row, canonical candidate, reason, and required human decision. Do not delete, merge, retime, invent START/shutdown Events, or rewrite user remarks automatically.
8. Obtain explicit review/approval for every remediation policy and database migration. No destructive schema or data change is authorized by this specification.
9. After data remediation, import through a versioned migration transaction, create stable EventIds, create migration audit/provenance records, validate every complete chain, then enable foreign/check/unique constraints.
10. Reconcile legacy versus new runtime projections without overwriting locked reports. Differences require a documented disposition and calculation-version policy.
11. Back up and verify each source database before migration; rollback must restore the exact pre-migration state.

## 12. Final architecture decisions

| Decision | Reason | Implementation impact |
|---|---|---|
| Events are the sole runtime authority. | Approved business rule; hourly ST/RPM are observations, not state transitions. | Runtime and Event validation never query ST/RPM as authority. |
| Use explicit Add/Edit/Delete commands. | Prevents bypass and gives each mutation a clear contract. | UI/import/maintenance routes depend on one application command boundary. |
| Preserve stable Event identity. | Enables precise edits, deletes, concurrency, reporting, and audit. | No full-day delete/reinsert; EventId retained on Edit. |
| Validate complete chains from Trusted Runtime Baseline. | Earlier mutations can invalidate arbitrarily later Events. | Full per-affected-Unit chain reads and deterministic replay inside transaction. |
| Edit validates old and new Unit. | Unit reassignment changes two authoritative histories. | Multi-Unit atomic validation; no partial success. |
| Delete validates Unit even when no same-day Events remain. | Empty proposed lists must not bypass downstream validation. | Affected Unit derives from stored target, not proposed rows. |
| Implement explicit three-state machine. | Event-type alternation is insufficient, especially for OH. | Pure domain transition function with five valid and seven forbidden cases. |
| Reject Running+OH. | OH cannot terminate a Running Unit; shutdown must occur first. | Structured correction message; no runtime side effect. |
| Enforce same-Unit timestamp uniqueness in application and database. | Prevents ambiguous ordering and duplicate state changes. | Unique constraint plus early Persian validation message. |
| Use minute-precision canonical time. | Matches operator input and approved rule. | `HH:mm`/minute-of-day value object; seconds and malformed values rejected. |
| Keep validation and mutation in one transaction. | Eliminates pre-validation race and partial writes. | Shared connection/transaction for locks, reads, replay, Event and audit writes. |
| Enforce finalized periods below UI. | UI controls are bypassable and finalized history is protected. | Old/new month lock checks in every command. |
| Write append-only EventAudit with every accepted mutation. | Required traceability for corrections and deletions. | Atomic ADD/EDIT/DELETE snapshots, actor, time, and reason. |
| Separate physical runtime and ESD adjustment. | Adjustment is not physical operation and has different ServiceDay/LongestRun effects. | Projection models expose physical, adjustment, and adjusted totals separately. |
| Apply ESD adjustment only on valid Running->ESD. | Prevents adjustment on an already stopped Unit. | State acceptance precedes adjustment; source EventId retained. |
| OH resets only RuntimeAfterOH. | Approved runtime semantics. | Cumulative remains unchanged; post-OH state is explicit. |
| ServiceDays require positive physical overlap. | Adjustment alone is not service. | Midnight-clipped physical interval projection. |
| LongestRun is physical and period-clipped. | Approved reporting definition. | ESD adjustment excluded; history retained to establish period-start state. |
| Preserve Trusted Baseline; never invent START. | Initial Running is baseline truth, not an inferred historical Event. | Baseline state initializes replay and runtime projections. |
| Preserve compact legacy UX with adaptation. | It is efficient and familiar despite business-layer defects. | Reuse interaction pattern, not legacy persistence/validation code. |
| Display structured Persian corrective messages. | Rejection must be actionable and identify affected Unit/Event. | Domain error codes/context mapped by UI localization layer. |
| Keep Events optional and independently saveable. | Daily completeness does not require an Event. | Event commands are decoupled from mandatory hourly/daily-unique save bundle. |
| Centralize Persian date/time conversion. | Prevents divergent normalization and boundary defects. | Shared tested value objects/services across commands and reporting. |
| Version runtime/finalized calculations. | Corrected logic must remain auditable for locked reports. | Snapshot metadata records calculation version and source consistency boundary. |
| Audit legacy data before adding constraints. | Existing duplicates/malformed chains may violate the target schema. | Non-destructive discovery and reviewed remediation precede migration. |
| No destructive migration without explicit review. | Protects production history and finalized outputs. | Migration tooling reports conflicts and requires approved disposition/rollback. |

