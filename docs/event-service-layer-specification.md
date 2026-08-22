# Event Service Layer Architecture Specification

**Project:** Generalized RahNegar platform  
**Document status:** Target service-layer architecture  
**Source basis:** `docs/legacy-event-subsystem-audit.md`, `docs/event-subsystem-architecture-specification.md`, and `docs/event-database-schema-specification.md`  
**Scope:** Application services between the Event UI, domain behavior, runtime projection, SQLite persistence, transactions, audit, errors, and testing  
**Out of scope:** Production implementation, executable interfaces, SQL, migrations, UI redesign, and changes to the legacy repository

## 1. Executive overview

The redesigned Event subsystem uses a service boundary between WinForms and SQLite. The UI can collect a Unit, Event type, Persian date, minute time, remark, and mutation reason, but it must never directly insert, update, tombstone, or reconstruct Event database records. An Event mutation is not a single-row CRUD operation: changing or deleting one earlier Event can invalidate any later Event in that Unit's authoritative history and can change runtime projections across day, month, and report boundaries. Direct UI persistence would duplicate rules, bypass complete-chain validation, weaken transaction safety, and recreate the confirmed defects of the legacy subsystem.

Event behavior belongs in domain services because the approved state machine and runtime semantics are business facts independent of controls and SQLite. `Stopped + START`, `Running + ESD`, `Stopped + OH`, forbidden Running + OH, ESD adjustment, physical ServiceDays, and period-clipped LongestRun must behave identically whether invoked by WinForms, an approved import, a test, or a future offline maintenance workflow. A deterministic domain model also permits exhaustive unit testing without opening a database or displaying a `MessageBox`.

The architecture separates six concerns:

| Concern | Owner | Boundary |
|---|---|---|
| UI | Presentation Layer | Collects input, sends commands, displays committed data and structured Persian errors. It owns no Event business rule. |
| Application Commands | Application Layer | Defines Add/Edit/Delete use cases, opens the transaction, loads required state, coordinates domain services and repositories, and commits or rolls back. |
| Domain Validation | Domain Layer | Validates ownership facts supplied by the application, baseline boundaries, duplicate timestamps, finalized/operating-day policy results, and the entire Event chain through the pure state machine. |
| Runtime Projection | Domain Layer | Reconstructs physical runtime and approved derived metrics from Trusted Runtime Baseline plus validated Events only. |
| Persistence | Infrastructure Layer | Reads and writes canonical Event/baseline/audit data through the active transaction and enforces SQLite constraints. It makes no state-transition decision. |
| Audit | Application orchestration plus infrastructure repository | Creates an immutable operational EventAudit record for every committed ADD, EDIT, and DELETE in the same transaction. |

The central invariant is: **every accepted command leaves every affected Unit's complete authoritative chain valid from its Trusted Runtime Baseline through its last active Event**. Validation and mutation share one transaction. A failure at any stage rolls back Event and EventAudit changes together.

## 2. Layer architecture

### 2.1 Presentation Layer

The Presentation Layer contains the WinForms Event editor, chronological Event grid, presenters/view models, and Persian message formatting/display.

**Responsibilities**

- Present the compact editor: Station context, Unit selector, fixed Event selector, Persian date, `HH:mm` input, remark, reason, and Add/Edit/Delete actions.
- Load display models through application queries; never query raw Event tables from form code.
- Build command DTOs from validated control values. Parsing errors such as malformed Persian date or time may be caught early, but the command remains authoritative.
- Display a read-only chronological grid keyed by stable EventId, not visual row number.
- Display `EventValidationError` and other application failures in structured Persian form.
- Ask for confirmation and mutation reason before submitting a command.
- Disable duplicate submissions while a command is executing and refresh from committed data after success.
- Preserve keyboard, RTL, DPI, focus, and accessibility behavior.

**No business-rule ownership**

The Presentation Layer does not decide whether START, NSD, ESD, or OH is allowed; infer Unit state from the visible day; calculate runtime; check only the preceding grid row; enforce finalized-month authority; or write Event/audit rows. UI enablement and preflight validation improve usability but never grant permission.

There is no user interaction while a database transaction is open. Confirmations and required reasons are gathered first. The command result is displayed after commit or rollback.

### 2.2 Application Layer

The Application Layer exposes one command handler per mutation. It owns use-case order and the unit-of-work boundary, not the transition formulas.

#### AddEventCommandHandler

Receives `AddEventCommand`, verifies command shape and user context, begins a transaction, loads Station/Unit/baseline/complete chain, creates a candidate Event in memory, invokes validation, inserts the Event, creates ADD audit, and commits. It returns the canonical committed Event and resulting state or a structured failure.

#### EditEventCommandHandler

Receives target EventId, expected RowVersion, replacement Event values, reason, and user context. Inside one transaction it loads the current active Event, determines both old and new affected Units and periods, loads every required baseline/chain, replaces the Event in memory, validates both complete chains, updates the same stable EventId, writes EDIT audit with old/new snapshots, and commits. Neither Unit changes if either chain fails.

#### DeleteEventCommandHandler

Receives target EventId, expected RowVersion, reason, and user context. Inside one transaction it loads the active Event, treats its Unit as affected even if deletion leaves no Event on the day, removes it from the in-memory active chain, validates through every later Event, tombstones the row, writes DELETE audit, and commits.

#### Shared handler responsibilities

- Authenticate/accept a trusted caller context and enforce authorization at the use-case boundary.
- Start the SQLite transaction before authoritative baseline/chain reads.
- Use only repositories bound to that transaction.
- Coordinate Unit ownership, finalized-period, operating-day, duplicate, baseline, and state-chain validation.
- Translate expected domain/infrastructure outcomes into stable application results.
- Generate canonical EventId/AuditId, UTC metadata, EventDateTime, audit snapshots, and RowVersion changes through injected services.
- Commit exactly once on success; roll back on every failure.
- Never display UI messages and never contain duplicated state-machine switch logic.

Application queries are separate from mutation handlers. Read services may return station/date-range grids, Event history, and runtime projections, but they cannot expose mutable persistence entities to the UI.

### 2.3 Domain Layer

The Domain Layer is persistence-agnostic and UI-agnostic. It contains immutable/value-based Event models, the three states, closed Event type, validation results, runtime projection models, and pure services.

#### EventStateMachine

- Accepts current `EventState` and one canonical Event.
- Returns either a valid next state plus transition effects or a structured transition failure.
- Implements the five approved transitions and seven forbidden transitions exactly.
- Does not query SQLite, inspect ST/RPM, use current UI selection, log technical exceptions, or display messages.

#### EventValidationService

- Accepts Trusted Runtime Baseline, complete candidate chains for every affected Unit, ownership and policy facts, and mutation context.
- Checks duplicate timestamps, baseline boundary, Unit/Station ownership, finalized-period and operating-day results, and complete state-machine replay.
- Returns a deterministic validation result with the first invalid Event and later affected Event where applicable.
- Does not write data, show Persian dialogs, or produce reporting totals.

#### RuntimeProjectionService

- Accepts a Trusted Runtime Baseline, a validated chronological Event chain, requested period, and versioned ESD-adjustment policy.
- Produces physical runtime, ESD adjustment, adjusted runtime, RuntimeAfterOH, ServiceDays, and LongestRun.
- Uses Events only as operational authority and never uses hourly ST/RPM.
- Calculates projections; it does not store runtime in Event rows.

### 2.4 Infrastructure Layer

The Infrastructure Layer implements database-facing ports and SQLite transaction/connection behavior.

#### EventRepository

- Loads active Events by Unit in canonical chronological order.
- Loads station/date-range Events for UI and reporting.
- Loads a target Event by stable EventId and expected Station scope.
- Inserts, updates, and tombstones canonical rows using the current transaction.
- Checks exactly-one-row effects and database conflicts.
- Does not decide transition legality, calculate runtime, or format Persian messages.

#### EventAuditRepository

- Inserts immutable EventAudit records in the current transaction.
- Reads audit history by Event and authorized station context.
- Does not infer old/new values or mutation reason; the application supplies canonical snapshots.

#### TrustedRuntimeBaselineRepository

- Loads the authoritative baseline for a Station/Unit under the current transaction/snapshot.
- Fails explicitly for missing/ambiguous baseline; never synthesizes START.

#### TransactionManager and connection handling

- Creates/opens a SQLite connection through one configured connection factory.
- Enables foreign keys on every connection and applies approved busy timeout/WAL settings.
- Begins a short write transaction appropriate to prevent validation/mutation races.
- Supplies transaction-scoped repository implementations or a unit-of-work context.
- Commits or rolls back and disposes resources deterministically.
- Never asks the user questions or retries a business failure automatically.

Infrastructure also maps constraint/locking/concurrency failures into typed technical outcomes. The Application Layer translates them into stable command results.

## 3. Command workflow

### 3.1 Shared exact execution order

Every Add, Edit, and Delete follows this required order:

1. **Receive command.** Parse basic DTO shape, obtain trusted user/Station context, verify required reason and expected RowVersion where applicable, and gather UI confirmation before transaction start.
2. **Open transaction.** Acquire one SQLite connection and begin the mutation transaction. All following reads/writes use it.
3. **Load Trusted Runtime Baseline.** Load one baseline for every affected Unit. Missing or invalid baseline fails the command; no historical START is invented.
4. **Load affected Event chain.** Load every active Event from the baseline boundary through the last Event for all affected Units, plus the target row for Edit/Delete and ownership/finalized/operating-day facts.
5. **Apply mutation in memory.** Add candidate, replace exact EventId, or remove exact EventId from the active candidate chain. No Event row is changed yet.
6. **Validate complete chain.** Canonically sort, detect same-Unit timestamp duplicates, validate baseline/ownership/date/finalized policies, and replay every Event through EventStateMachine. Any later Event made invalid rejects the command.
7. **Save Event changes.** Insert, update with expected RowVersion, or tombstone the exact Event. Database foreign/check/unique constraints provide final defense.
8. **Save Audit record.** Insert one matching ADD/EDIT/DELETE EventAudit record using committed-form canonical snapshots, actor, UTC timestamp, and reason.
9. **Commit.** Commit only after Event and audit writes both succeed. Return success using canonical persisted values.

If any stage fails, the handler does not continue. It rolls back, disposes transaction/connection, logs technical failures as appropriate, and returns a structured failure. A rejected operation writes neither an Event change nor a successful operational audit row.

### 3.2 Add Event workflow

1. Receive StationId from trusted context and UnitId/type/Persian date/minute time/remark/reason from command.
2. Open transaction and check authorization, Unit ownership, Event date lock, and operating-day eligibility.
3. Load the new Unit's Trusted Runtime Baseline.
4. Load its complete active chain, not merely the prior Event or selected day.
5. Create canonical candidate with new EventId, derived EventDateTime, CreatedAt/CreatedBy, RowVersion 1, and active tombstone state; insert into in-memory chain.
6. Validate duplicate timestamp and replay complete chain through all later Events.
7. Insert Event. A unique constraint race becomes a duplicate/conflict failure.
8. Insert ADD audit: OldValue null, NewValue canonical Event.
9. Commit and return EventId, canonical Event, audit id, and resulting Unit state.

### 3.3 Edit Event workflow

1. Receive EventId, expected RowVersion, replacement values, reason, and trusted context.
2. Open transaction; load target active Event scoped to Station; reject missing/tombstoned/stale rows.
3. Identify affected Units: old Unit always, new Unit as well if Unit changes. Identify old and new Persian months if date changes; both must be editable.
4. Load baseline and complete chain for every affected Unit.
5. Remove the old candidate from its chain and place the replacement with the same EventId in the proper candidate chain; recompute EventDateTime. Preserve StationId, CreatedAt, and CreatedBy.
6. Validate all affected complete chains. A failure in old or new Unit, including a much later Event, rejects the whole Edit.
7. Update exactly one active Event matching EventId and expected RowVersion; set UpdatedAt and increment RowVersion.
8. Insert EDIT audit with complete canonical OldValue and NewValue plus reason/user.
9. Commit and return the unchanged EventId, new RowVersion, affected Unit states, and audit id.

### 3.4 Delete Event workflow

1. Receive EventId, expected RowVersion, required reason, and trusted context.
2. Open transaction; load target active Event scoped to Station; reject missing/tombstoned/stale rows.
3. Treat the stored Unit as affected regardless of how many Events remain. Check the Event's Persian month is not finalized and load its baseline.
4. Load that Unit's complete active chain from baseline through the last Event.
5. Remove the target Event from the in-memory chain even if this produces an empty selected day or an otherwise empty chain.
6. Replay the complete remaining chain. If deletion makes any later Event invalid, return that Event in the failure and roll back.
7. Tombstone exactly one row matching EventId and expected RowVersion; set DeletedAt/DeletedBy and increment RowVersion.
8. Insert DELETE audit with OldValue and null NewValue.
9. Commit and return deleted Event identity, audit id, and resulting Unit state.

### 3.5 Rollback behavior

Rollback is mandatory for validation failure, authorization/ownership failure, finalized-period failure, date-sequence failure, missing baseline, duplicate timestamp, invalid downstream Event, concurrency conflict, unexpected affected-row count, database constraint error, audit insert error, cancellation, or technical exception. Rollback itself is attempted once and any rollback/disposal error is technically logged without masking the original user-safe failure. The handler never reports success until commit succeeds.

## 4. Repository contracts

Contracts below are conceptual. Names express required behavior rather than executable C# signatures.

### 4.1 IEventRepository

```text
GetEventsByUnit(stationId, unitId, baselineBoundary, includeTombstoned = false)
    -> chronologically ordered Event collection

GetEventsByStationAndDateRange(stationId, dateFrom, dateTo, includeTombstoned = false)
    -> chronologically ordered Event collection

GetById(stationId, eventId, includeTombstoned = false)
    -> Event or NotFound

Insert(event, transactionContext)
    -> inserted canonical Event / persistence conflict

Update(event, expectedRowVersion, transactionContext)
    -> updated canonical Event / concurrency conflict

Tombstone(eventId, expectedRowVersion, deletedAt, deletedBy, transactionContext)
    -> tombstoned Event / concurrency conflict
```

`GetEventsByUnit()` guarantees explicit order by EventDateTime and deterministic EventId diagnostics. It does not validate the state chain. `GetEventsByStationAndDateRange()` supports grids/report logs; runtime reconstruction must still request all history required from baseline rather than assume a selected range establishes starting state.

Insert/Update/Tombstone accept already canonical domain/persistence models. They enforce row counts and surface foreign/check/unique failures but do not decide whether an Event transition is legal. No public “delete all Events for date” contract exists.

### 4.2 IEventAuditRepository

```text
AddAudit(auditRecord, transactionContext)
    -> committed-pending AuditId / persistence failure

GetHistory(stationId, eventId)
    -> immutable audit entries ordered by Timestamp and AuditId
```

`AddAudit()` only inserts. There are no ordinary Update/Delete methods. `GetHistory()` enforces Station authorization through the application/query boundary even though EventAudit is keyed by EventId.

### 4.3 ITrustedRuntimeBaselineRepository

```text
GetBaseline(stationId, unitId, transactionContext)
    -> exactly one TrustedRuntimeBaseline / missing-or-ambiguous failure
```

The baseline includes its effective boundary, initial state, cumulative values required by approved reporting semantics, and version. It never manufactures an Event or chooses a default Stopped state when data is missing.

### 4.4 ITransactionManager

```text
Begin(mode = EventMutation)
    -> transaction-scoped context

Commit(transactionContext)
    -> success / commit failure

Rollback(transactionContext)
    -> completion / rollback failure for technical logging
```

An implementation may expose an execution helper that guarantees rollback/disposal, but the architectural semantics remain Begin -> coordinated reads/validation/writes -> Commit or Rollback. Nested independent Event transactions are forbidden. Repositories called by one command share the same context.

### 4.5 Policy/query ports used by handlers

The Application Layer also requires conceptual read ports for Station/Unit ownership, finalized-period status, operating-day eligibility, user authorization, id/clock generation, and audit serialization. These are facts/services coordinated by the handler. They must participate in the same transaction snapshot where their underlying state can change.

### 4.6 Repository rule boundary

Repositories contain persistence knowledge only: table mapping, parameters, explicit ordering, active-tombstone filters, constraints, expected row counts, and transaction association. They must not:

- implement the Event transition matrix;
- infer state from “previous Event type”;
- apply ESD adjustment;
- decide ServiceDays or LongestRun;
- display/log user validation messages;
- silently normalize malformed values;
- start their own unrelated transaction inside a command;
- bypass Station scope or finalized-period application policy.

## 5. EventValidationService

### 5.1 Inputs

The service receives fully loaded, in-memory facts rather than repositories:

- command/mutation kind and canonical candidate Event;
- affected Unit identities, including old/new Unit for Edit and stored Unit for Delete;
- one Trusted Runtime Baseline per affected Unit;
- complete candidate active chain per affected Unit after mutation;
- Station/Unit ownership result and relevant configuration validity facts;
- finalized-period eligibility for every old/new affected date;
- operating-day eligibility for introduced/moved dates;
- optional validation policy/version.

### 5.2 Responsibilities

1. **Event transition validation.** Replay each candidate chain from baseline through EventStateMachine and return the earliest forbidden transition.
2. **Duplicate timestamp validation.** Reject more than one active Event for the same Unit/EventDateTime. Different Units may share a timestamp.
3. **Baseline boundary validation.** Reject Events before the baseline effective boundary and missing/ambiguous baseline facts.
4. **Operating-day validation.** Require Event dates to satisfy approved sequential daily-entry rules while preserving Event optionality for completeness.
5. **Finalized-period validation.** Reject Add/Delete in finalized months and Edit when either old or new affected month is finalized. The application obtains the lock facts; validation applies them consistently.
6. **Unit ownership validation.** Require Unit to belong to Station and be valid for the Event date. Edit validates both sides of Unit reassignment.
7. **Canonical structural validation.** Ensure EventType/date/minute/EventDateTime/remark are canonical before replay; invalid input is rejected, never coerced.
8. **Downstream impact reporting.** Identify the first later Event invalidated by an earlier Add/Edit/Delete.

### 5.3 Determinism and ordering

Chains are sorted per Unit by EventDateTime. EventId is a diagnostic final key only; a same-Unit timestamp tie is invalid and cannot be made valid by id/order. Failure precedence is stable and documented so the same candidate state produces the same error independent of UI or repository enumeration.

### 5.4 Explicit exclusions

EventValidationService must not:

- insert, update, tombstone, or query database rows;
- begin, commit, or roll back transactions;
- call MessageBox or compose UI layout;
- decide logging destinations;
- calculate runtime reporting totals;
- consult hourly ST/RPM;
- invent missing START/shutdown Events;
- silently skip malformed Events or convert bad time to 00:00;
- treat a nearest previous/next check as complete-chain proof.

It returns typed facts/errors. Presentation localization may use a domain-supplied message key and context; it does not alter validity.

## 6. EventStateMachine

### 6.1 States and inputs

States:

- `Stopped`
- `Running`
- `StoppedAfterOH`

Inputs:

- `START`
- `NSD`
- `ESD`
- `OH`

The machine accepts one current state plus one canonical Event and returns either `TransitionAccepted(nextState, effects)` or `TransitionRejected(errorCode, context)`. It has no database, clock, UI, user, repository, logger, or configuration dependency. ESD adjustment amount belongs to RuntimeProjectionService; the state machine may mark an accepted transition as ESD but does not add hours itself.

### 6.2 Transition matrix

| Current state | START | NSD | ESD | OH |
|---|---|---|---|---|
| `Stopped` | **Running** | Forbidden | Forbidden | **StoppedAfterOH** |
| `Running` | Forbidden | **Stopped** | **Stopped** | Forbidden |
| `StoppedAfterOH` | **Running** | Forbidden | Forbidden | Forbidden |

This is the authoritative transition matrix defined in the subsystem architecture specification. In particular, Running + OH is rejected; OH never performs an implicit shutdown.

### 6.3 Deterministic effects

- Stopped + START opens a physical running interval.
- Stopped + OH resets RuntimeAfterOH projection state and enters StoppedAfterOH without changing cumulative runtime.
- Running + NSD closes the physical interval with no adjustment.
- Running + ESD closes the physical interval and marks eligibility for versioned ESD adjustment.
- StoppedAfterOH + START opens a new physical interval after the OH reset.
- Forbidden transitions produce no state or runtime effect.

The exhaustive matrix and pure API make the service fully unit-testable. Tests cover all 12 state/input combinations, baseline initialization, repeated calls, and invariant that rejected transitions leave state unchanged.

## 7. RuntimeProjectionService

### 7.1 Inputs and output contract

Inputs:

- one Trusted Runtime Baseline with effective boundary and version;
- one validated active Event chain in canonical chronological order;
- requested half-open reporting period;
- versioned ESD-adjustment policy/settings;
- calculation algorithm version.

Outputs per Unit:

| Output | Definition |
|---|---|
| `Physical Runtime` | Actual elapsed time in Running state, with cumulative and period-clipped values distinguished. |
| `ESD Adjustment` | Separately itemized additive hours from valid Running -> ESD transitions only. |
| `Adjusted Runtime` | Explicit Physical Runtime + ESD Adjustment for the same scope. |
| `RuntimeAfterOH` | Adjusted accumulation since latest valid OH reset, using baseline value until a later OH. |
| `ServiceDays` | Persian operating dates with any positive physical Running overlap in `[00:00, next 00:00)`. |
| `LongestRun` | Longest continuous physical Running overlap clipped to the requested period; ESD adjustment excluded. |

The result also carries Event/calculation/settings/baseline versions required for snapshot traceability and, for ESD adjustments, source EventId.

### 7.2 Authority rules

- Events are the only operational authority.
- Hourly ST/RPM is never an input, validator, correction source, or fallback.
- Baseline Running state is used directly; no historical START is invented.
- Projection reconstructs from baseline through report end so state and cumulative totals at an arbitrary later period are correct.
- Unsupported or invalid persisted chain data produces an integrity failure. Projection never ignores an Event to make a report succeed.

### 7.3 Calculation rules

- Physical runtime accrues only while state is Running.
- ESD adjustment applies once only after an accepted Running + ESD transition, increases approved adjusted/cumulative/after-OH totals, and remains separate from physical duration.
- ESD adjustment alone creates no ServiceDay and does not extend LongestRun.
- OH is valid only while Stopped, leaves cumulative runtime unchanged, and resets RuntimeAfterOH to zero.
- Runtime day boundary is local 00:00; positive physical overlap is required.
- Day/Night Event counts use Day `07:00 <= time < 19:00`; Night otherwise.

### 7.4 Storage boundary

Runtime projections are calculated, not stored in Events rows. Finalized-report snapshots may store outputs with calculation/baseline/settings/Event-set versions. Such snapshots are reporting records, not alternate Event authority. Live projections may be cached only through a separately specified invalidation/version strategy; cache values never replace complete-chain source Events.

## 8. Transaction strategy

### 8.1 One transaction for validation and mutation

The handler begins the Event mutation transaction before loading target, lock state, baseline, and chains. Every authoritative read and write uses the same transaction context. This removes the legacy time-of-check/time-of-use gap in which validation occurred on a different connection before mutation.

The preferred SQLite mutation mode is a short write transaction, commonly `BEGIN IMMEDIATE` after compatibility/load testing, so writer contention is discovered before expensive validation and competing commands cannot both validate the same stale chain and then commit. Exact provider syntax belongs to implementation design.

### 8.2 No UI interaction during transaction

The UI confirms OH/delete intent and collects Edit/Delete reason before command dispatch. No message box, modal form, network call, report rendering, or operator wait occurs in the transaction. Domain/application errors are returned after rollback. This keeps SQLite locks short and the offline UI responsive.

### 8.3 Optimistic concurrency

Edit and Delete carry expected `RowVersion`. The repository update/tombstone matches EventId, Station scope, active status, and expected RowVersion, increments it atomically, and requires exactly one affected row. Zero rows means stale data, deletion, or ownership mismatch; the handler rolls back and returns a reload/retry correction rather than overwriting.

Complete-chain validation protects semantic concurrency; the database unique constraint protects timestamp races; RowVersion protects target-row races. All are required.

### 8.4 Rollback rules

Rollback occurs on:

- command cancellation before commit;
- authorization, ownership, baseline, finalized-period, or operating-day failure;
- malformed/noncanonical Event;
- duplicate timestamp;
- forbidden transition anywhere in any affected complete chain;
- later Event invalidated by the mutation;
- stale RowVersion or unexpected affected-row count;
- SQLite foreign/check/unique constraint failure;
- EventAudit insert failure;
- commit failure or unexpected exception.

No partial multi-Unit Edit, Event-without-audit, or audit-without-Event is permitted. Commit is the only success boundary.

## 9. Error handling design

### 9.1 Structured application error

Conceptual `EventValidationError` fields:

| Field | Purpose |
|---|---|
| `ErrorCode` | Stable machine-readable code such as `EVENT_RUNNING_OH_FORBIDDEN`, `EVENT_DUPLICATE_UNIT_TIMESTAMP`, `EVENT_LATER_CHAIN_INVALID`, or `EVENT_FINALIZED_PERIOD`. |
| `PersianMessage` | Localized complete user-facing message or resolved presentation text. Prefer message key + arguments internally so wording can evolve without changing domain codes. |
| `Unit` | Unit identity and display context affected by the failure. |
| `EventDate` | Persian date of proposed or invalid Event when applicable. |
| `EventTime` | Canonical/display `HH:mm` minute. |
| `EventType` | START, NSD, ESD, or OH involved. |
| `SuggestedCorrection` | Concrete action the operator should take; never vague “invalid Event” text. |

Recommended additional context includes StationId, target EventId, current state, mutation kind, later invalid Event details, expected/actual RowVersion, and correlation id. These additions improve diagnosis without forcing UI to infer domain facts.

### 9.2 Error categories

- `Validation`: canonical value, state transition, duplicate, baseline boundary, operating day.
- `Authorization/Ownership`: user/Station/Unit access failure.
- `FinalizedPeriod`: old/new month locked or cross-period finalized dependency.
- `Concurrency`: stale RowVersion or chain changed during command.
- `NotFound`: target absent/tombstoned in authorized scope.
- `Integrity`: missing baseline, invalid persisted chain, database constraint inconsistency.
- `Technical`: connection, I/O, SQLite/provider, serialization, or unexpected failure.

Expected errors do not use exception text as user content. Technical exception details are logged, while the user gets a safe Persian message and correlation id.

### 9.3 UI behavior

The UI only displays/positions errors. It may focus the implicated control or row using structured context, but it cannot reinterpret a rejection as success, offer an automatic invented Event, or bypass correction. For a later-chain failure it identifies both proposed mutation and first later invalid Event. Running + OH explicitly instructs the user to record the actual NSD/ESD shutdown first.

## 10. Logging and audit

### 10.1 Operational Audit: EventAudit

EventAudit is durable business evidence of accepted Event mutations. It answers who changed which Event, when, why, and what canonical values existed before/after. It is written atomically with the Event command and retained after tombstone. It contains ADD/EDIT/DELETE only for committed operations.

Operational audit is user-visible/administratively reviewable and participates in migration/finalized-report traceability. It is not optional merely because technical logs are enabled.

### 10.2 Technical Logging: application logs

Technical logs diagnose software operation: command start/end metadata, duration, SQLite errors, constraint names, serialization failures, rollback/commit failures, correlation ids, and stack traces according to security policy. They may record rejected command codes and sanitized identifiers, but must not duplicate sensitive audit snapshots or expose credentials.

Technical logs can rotate or expire under operational policy and are not the legal/business record of Event changes. A log line does not make a mutation auditable, and an EventAudit row does not replace exception diagnostics.

### 10.3 Separation rules

| Aspect | EventAudit | Technical log |
|---|---|---|
| Trigger | Successful committed ADD/EDIT/DELETE | Technical lifecycle, failures, performance, diagnostics |
| Transaction | Same database transaction as Event mutation | Usually outside business transaction; logging failure must not fabricate audit success |
| Content | Canonical old/new values, actor, reason, commit time | Sanitized operational details, error/stack/correlation/timing |
| Retention | Long-lived, append-only business policy | Rotating diagnostic policy |
| User meaning | Explains an authorized historical change | Helps support diagnose system behavior |

A rejected command has no successful EventAudit mutation row. If rejected-attempt auditing is later required, it must use a separately named security/attempt audit contract rather than overloading EventAudit.

## 11. Testing architecture

### 11.1 Unit tests

**EventStateMachine**

- All 12 state/input combinations.
- Five allowed next states/effects and seven forbidden codes/corrections.
- Running + OH rejection with no state/runtime effect.
- Deterministic result independent of order outside supplied chain.

**EventValidationService**

- Complete-chain replay from each baseline state.
- Same Unit/same minute duplicate rejection; different Units/same minute success.
- Events before baseline rejection.
- Unit/Station ownership and temporal validity.
- Operating-day sequencing and optional Event completeness.
- Old/new Unit validation on Edit.
- Delete validation with no Events remaining on selected day.
- Later invalid Event identification.
- Finalized old/new month failure.
- Invalid time/date/type rejects without coercion.

**RuntimeProjectionService**

- Physical, adjustment, adjusted, after-OH, ServiceDays, and LongestRun independently asserted.
- Baseline Running without invented START.
- Valid ESD adjustment exactly once; invalid ESD never reaches projection.
- OH reset with cumulative preserved.
- Period clipping and full-history cumulative reconstruction.
- ST/RPM absent from API and unable to alter results.

Unit tests use no SQLite, MessageBox, system clock, or production files.

### 11.2 Integration tests

**Repositories**

- Canonical mapping and explicit order.
- Active/tombstone filters.
- Station scoping and composite Unit ownership FK.
- ULID identity persistence and immutable fields.
- RowVersion update/tombstone conflicts.
- EventAudit append/read order and retained FK after tombstone.

**Transactions**

- Event plus audit commit together.
- Failure after Event write rolls back Event and audit.
- Audit failure rolls back Event mutation.
- Multi-Unit Edit is all-or-nothing.
- Concurrent/stale commands cannot both commit.
- Foreign keys enabled on every connection.

**Database constraints**

- Closed EventType, EventTime range, tombstone consistency, and audit action shapes.
- Active same-Unit timestamp uniqueness.
- Different Unit same timestamp allowed.
- Invalid Station/Unit rejected.
- Append-only/immutable protections if implemented.

Integration tests use temporary isolated SQLite databases and never user data.

### 11.3 Scenario tests

- Add at beginning/middle/end of chain and later-Event impact.
- Edit type/time/date/Unit across day and month boundaries; both Unit chains validated.
- Delete only Event on day and delete Event required by a later shutdown.
- Run across midnight: positive physical overlap marks both applicable ServiceDays; exact-midnight end excludes next day.
- Persian 31-to-01, year, and leap/non-leap Esfand boundaries.
- Stopped + OH, START after OH, repeated OH, and Running + OH rejection.
- Valid Running + ESD with enabled/disabled adjustment; no ServiceDay/LongestRun contribution from adjustment.
- Finalized month protection for Add/Edit/Delete, including Edit old/new month and earlier-period effect on locked later reports.
- Finalized snapshot calculation/baseline/settings/Event-set versions remain consistent.
- Persian error presentation contains Unit, date, time, type, reason, and suggested correction.

## 12. Dependency direction

### 12.1 Allowed logical call flow

```text
UI / Presentation
        ↓ commands and queries
Application Layer
        ↓ invokes pure policies/services
Domain Layer
        ↓ persistence ports are fulfilled at the boundary
Infrastructure
```

At runtime, the Application Layer coordinates both Domain services and repository interfaces. The downward diagram describes the allowed use flow, not permission for domain source code to reference SQLite.

For source-code dependency inversion:

- Presentation references Application contracts/result models, not Infrastructure.
- Application references Domain types/services and repository/transaction abstractions.
- Domain references only domain primitives and abstractions that contain no UI/database types.
- Infrastructure references/implements the repository and transaction abstractions and maps Domain/application persistence models to SQLite.
- Composition root wires implementations; no layer locates dependencies through global service state.

### 12.2 Forbidden dependencies

- UI -> SQLite connection, SQL command, EventRepository implementation, or EventAudit table.
- UI -> private state-machine duplication or runtime calculation.
- Domain -> WinForms, MessageBox, UI localization controls, Microsoft.Data.Sqlite, connection strings, filesystem, or technical logger implementation.
- Repository -> EventStateMachine decisions, Persian UI messages, ESD adjustment, ServiceDays, LongestRun, or independent business transaction.
- RuntimeProjectionService -> hourly ST/RPM repositories.
- Infrastructure -> presentation controls or user confirmation.
- EventValidationService -> repositories or database writes.
- Command handler -> duplicated transition switch statements.
- Technical logger -> authority to approve/reject or alter an Event.
- Any import/test/maintenance path -> direct write bypass around command validation and finalized locks.

Cycles are forbidden. Infrastructure implementation details never leak into command/domain APIs. A future UI or database replacement should not require rewriting the state machine.

## 13. Final architecture decision table

| Component | Responsibility | Allowed dependencies | Forbidden responsibilities |
|---|---|---|---|
| Event Entry Form / Presenter | Collect input, submit command, display canonical result/Persian error, refresh chronological grid | Application command/query contracts, presentation localization/UI services | SQL, repositories, state decisions, runtime calculations, authoritative locks |
| AddEventCommandHandler | Coordinate validated creation and ADD audit in one transaction | Transaction manager, repositories, policies, domain validation/state machine, id/clock/serializer ports | UI messaging, duplicated transition rules, direct ST/RPM use |
| EditEventCommandHandler | Load stable target, validate old/new Units and periods, update with RowVersion, write EDIT audit | Same application/domain/infrastructure abstractions as Add | Partial Unit update, Station transfer, overwrite stale row, delete/reinsert day |
| DeleteEventCommandHandler | Validate complete chain without target, tombstone stable Event, write DELETE audit | Transaction manager, repositories, policies, validation | Skipping Unit because day becomes empty, hard-delete audit cascade, unvalidated restore |
| EventValidationService | Canonical/ownership/baseline/date/duplicate/full-chain validation and structured errors | Domain value types, EventStateMachine, supplied policy facts | Database I/O, transactions, UI messages, runtime reports, ST/RPM |
| EventStateMachine | Deterministic five allowed/seven forbidden transitions | Domain state/Event types only | Database, clock, settings, UI, logging, ESD-hour arithmetic |
| RuntimeProjectionService | Calculate physical, ESD adjustment, adjusted, after-OH, ServiceDays, LongestRun from baseline + validated Events | Domain models, calendar/time abstraction, versioned ESD policy | Event persistence, UI, ST/RPM authority, silent data repair |
| IEventRepository / EventRepository | Canonical Event reads and exact Insert/Update/Tombstone with ordering/concurrency/constraints | Transaction context, SQLite in implementation, domain persistence models | State validation, runtime, localization, starting unrelated transactions |
| IEventAuditRepository / EventAuditRepository | Insert append-only audit and retrieve authorized history | Transaction context, SQLite implementation, canonical audit models | Editing/deleting audit, inventing reason/actor/snapshots, approving mutations |
| ITrustedRuntimeBaselineRepository | Return exactly one authoritative baseline/version per Station/Unit | Transaction context, SQLite implementation | Defaulting missing baseline, inventing START, runtime reporting |
| ITransactionManager | Own connection, Begin/Commit/Rollback/disposal and shared transaction scope | SQLite provider in Infrastructure, connection factory/configuration | User interaction, business validation, automatic partial retry |
| Ownership/Finalized/Operating-Day policy ports | Supply authoritative policy facts inside transaction | Domain/application contracts and infrastructure reads | UI-only enforcement, state transition calculation |
| EventValidationError / result mapping | Carry stable error code, factual context, Persian correction data | Domain/application result models, presentation localization | Showing dialogs inside Domain, exception text as user message |
| EventAudit | Durable business record of committed Event mutations | Event command transaction, audit repository | Technical stack traces, rejected-command success records, routine deletion |
| Technical Logger | Diagnose execution/failures/performance with correlation | Application/infrastructure logging abstraction | Business audit authority, storing unsanitized snapshots/secrets, changing outcomes |
| Composition Root | Wire handlers, domain services, repositories, transaction manager, clocks, ids | All concrete implementations at application startup | Business decisions or service-locator use from Domain/UI |

