# RahNegar Version 1 Runtime Truth Table

## 1. Authority and status

This document enumerates every combination of the Version 1 Event States (`Stopped`, `Running`, `StoppedAfterOH`) and event semantics (`START`, `NSD`, `ESD`, `OH`).

`Valid` means the transition is sufficiently established for the proposed Version 1 domain. `Pending Decision` means **Pending Product Owner Decision** and must not be implemented as approved production behavior. `Invalid` may be assigned only after the product owner approves the rule; until then unresolved combinations remain pending rather than silently invalid.

## 2. Global rules

- Replay order is Persian Operating Day, local event time, then stable sequence/identity.
- Pre-period valid events reconstruct state but do not increment period event statistics.
- In-period valid events increment total, type, and configured shift counts.
- Invalid production events are rejected atomically, contribute no runtime or counts, and leave persisted history unchanged.
- Migrated anomalous events retain source identity and diagnostics; they do not silently invent a START, STOP, duration, or ordering.
- Closing a physical run adds only positive elapsed duration. Negative or reversed intervals are errors.
- Physical period runtime is clipped to the effective Reporting Period.
- Effects of ESD Adjustment on runtime outputs, ServiceDay, and LongestRunInPeriod are **Pending Product Owner Decision**.

## 3. Complete transition matrix

| Current state | Event | Status | Next state | Physical runtime effect | PeriodRuntime effect | CumulativeRuntime effect | RuntimeAfterOH effect | ESD Adjustment | Event-count effect | ServiceDay effect | LongestRun effect | Production validation | Migrated-history diagnostic |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Stopped | START | Valid | Running | Open run at event timestamp; add no instantaneous duration. | Future positive overlap contributes. | Future run duration contributes. | Future run duration contributes without resetting existing value. | None. | In period: total +1, START +1, applicable shift +1. | None until positive run overlap occurs. | New open run becomes a candidate when closed/clipped. | Require applicable unit/type, valid time/order, no date before baseline/data start. | If structurally invalid, report source identity and reason; do not open run. |
| Running | START | Pending Decision | **Pending Product Owner Decision** | Reject, ignore, or close/reopen are unresolved; close/reopen would split a physical run. | Policy-dependent. | Physical elapsed time must not be double-counted under any policy. | Must not reset merely because of repeated START unless explicitly approved. | None. | Rejected event: zero; tolerated event policy unresolved. | Existing open run may already establish days; repeated event alone creates none. | Split-versus-continuous result is policy-dependent. | Until approved, reject as `RepeatedStartWhileRunning`. | Record `RepeatedStartWhileRunning`; preserve event and order; exclude from authoritative replay unless a reconciliation policy is selected. |
| Stopped | NSD | Pending Decision | **Pending Product Owner Decision**; provisionally Stopped if tolerated | No open run exists; never invent duration. | Zero physical contribution. | Zero physical contribution. | No physical contribution or reset. | None. | Rejected: zero; count-only notification is unresolved. | None. | None. | Until approved, reject as `NSDWithoutOpenRun`. | Preserve and report `NSDWithoutOpenRun`; never infer START. |
| Running | NSD | Valid | Stopped | Close open run at NSD timestamp. | Add report-clipped physical interval. | Add full positive physical interval since open-run start/replay boundary. | Add physical interval when after latest OH; do not reset. | None. | In period: total +1, NSD +1, applicable shift +1. | Add every qualifying overlapped Operating Day within period. | Compare qualifying run duration using approved boundary rule. | Require open run and closing timestamp not earlier than start. | Invalid chronology: preserve, flag `NegativeOrReversedRun`, and do not invent duration. |
| Stopped | ESD | Pending Decision | **Pending Product Owner Decision**; provisionally Stopped if tolerated | No open run exists; never invent physical duration. | Zero physical contribution; adjustment effect pending. | Zero physical contribution; adjustment effect pending. | Zero physical contribution; adjustment effect pending. | Eligibility while stopped is **Pending Product Owner Decision**. | Rejected: zero; count-only behavior unresolved. | None physically; adjustment-only effect pending. | None physically; adjustment effect pending. | Until approved, reject as `ESDWithoutOpenRun`. | Preserve and flag `ESDWithoutOpenRun`; do not apply adjustment automatically. |
| Running | ESD | Valid transition; adjustment pending | Stopped | Close open run at ESD timestamp. | Add report-clipped physical interval. Additional adjustment inclusion pending. | Add physical interval. Additional adjustment inclusion pending. | Add physical interval after latest OH. Additional adjustment inclusion pending. | Amount and targets are **Pending Product Owner Decision**. | In period: total +1, ESD +1, applicable shift +1. | Physical interval establishes days; adjustment-only effect pending. | Physical interval is a candidate; adjustment extension pending. | Require open run, valid chronology, and nonnegative configured adjustment. Preserve physical/adjustment quantities separately. | Invalid chronology or policy: flag; do not silently adjust or invent duration. |
| Stopped | OH | Pending Decision | Provisionally StoppedAfterOH; **Pending Product Owner Decision** | No run to close. | Zero physical contribution. | Preserve cumulative value. | If accepted, reset to zero at OH boundary. | None. | If accepted and in period: total +1, OH +1; otherwise zero. | None. | None. | Until approved, reject or gate as unresolved `OHWhileStopped`; require explicit confirmation if accepted. | Preserve and flag `OHWhileStopped`; do not reset authoritative value without selected policy. |
| Running | OH | Pending Decision | Provisionally StoppedAfterOH; **Pending Product Owner Decision** | Proposed behavior closes run at OH; alternative requires preceding shutdown. | If direct close approved, add clipped physical interval. | If direct close approved, add physical interval; never reset cumulative. | If accepted, account interval then reset to zero at OH. | None. | If accepted and in period: total +1, OH +1. | Direct-close interval establishes qualifying days. | Direct-close interval is a candidate. | Until approved, reject as unresolved `OHWhileRunning`; require confirmation and valid chronology under accepted policy. | Preserve and flag `OHWhileRunning`; do not choose direct-close versus prerequisite-stop silently. |
| StoppedAfterOH | START | Valid | Running | Open first post-OH physical run at START. | Future positive period overlap contributes. | Future physical duration contributes. | Begins accumulation from zero after OH. | None. | In period: total +1, START +1, applicable shift +1. | None until positive duration. | New run becomes candidate when closed/clipped. | Require an authoritative OH/baseline boundary and START not earlier than it. | Contradictory chronology: preserve and flag; do not open authoritative run. |
| StoppedAfterOH | NSD | Pending Decision | **Pending Product Owner Decision**; provisionally StoppedAfterOH if tolerated | No run exists; never invent duration. | Zero. | Zero. | Remains zero. | None. | Rejected: zero; count-only behavior unresolved. | None. | None. | Until approved, reject as `NSDAfterOHWithoutStart`. | Preserve and flag `NSDAfterOHWithoutStart`; never infer START. |
| StoppedAfterOH | ESD | Pending Decision | **Pending Product Owner Decision**; provisionally StoppedAfterOH if tolerated | No run exists; never invent physical duration. | Zero physical contribution; adjustment pending. | Zero physical contribution; adjustment pending. | Physical value remains zero; adjustment could make it positive only if explicitly approved. | Eligibility is **Pending Product Owner Decision**. | Rejected: zero; count-only behavior unresolved. | None physically; adjustment-only effect pending. | None physically; adjustment effect pending. | Until approved, reject as `ESDAfterOHWithoutStart`. | Preserve and flag; never infer run or silently apply adjustment. |
| StoppedAfterOH | OH | Pending Decision | Provisionally StoppedAfterOH; **Pending Product Owner Decision** | No physical duration. | Zero. | Preserve cumulative value. | Remains/resets zero; accepted OH would replace last boundary timestamp. | None. | If accepted and in period: total +1, OH +1; otherwise zero. | None. | None. | Until approved, reject or require documented justification as `RepeatedOHWithoutStart`. | Preserve all source OH events and flag `RepeatedOHWithoutStart`; do not collapse them silently. |

## 4. Transition-specific decisions

### 4.1 Repeated START

**Pending Product Owner Decision** among:

1. Reject as invalid.
2. Retain as a countable notification without changing the open run.
3. Close and reopen the run, intentionally splitting LongestRunInPeriod.
4. Apply another documented operational meaning.

Recommended architecture safeguard: the engine must require an explicit policy value and have no implicit default.

### 4.2 NSD while non-running

NSD from Stopped and StoppedAfterOH is **Pending Product Owner Decision**. If accepted as a notification, it can affect counts but cannot add physical runtime, ServiceDay, or LongestRunInPeriod. The system must never infer a missing START time.

### 4.3 ESD while non-running

ESD from Stopped and StoppedAfterOH is **Pending Product Owner Decision**. Separate decisions are required for validity, event counting, and ESD Adjustment eligibility. These must not be coupled implicitly.

### 4.4 OH source states

OH while Running, Stopped, and StoppedAfterOH are each **Pending Product Owner Decision**. Approval of one does not approve the others. For Running + OH, the product owner must decide whether OH closes the run or requires a prior shutdown.

### 4.5 ESD Adjustment targets

For an accepted ESD, each target is independently **Pending Product Owner Decision**:

| Target | Decision required |
|---|---|
| PeriodRuntimeHours | Add adjustment or expose separately only |
| CumulativeRuntimeAtPeriodEnd | Add adjustment or physical-only |
| RuntimeAfterOHAtPeriodEnd | Add when after OH or physical-only |
| ServiceDay | Adjustment can/cannot create a day |
| LongestRunInPeriod | Adjustment can/cannot extend a run |

Regardless of decisions, physical and adjustment seconds remain separately stored in calculation results and finalized provenance.

## 5. Same-time events

Same-time behavior for multiple events on one unit is **Pending Product Owner Decision**.

Options:

- Prohibit more than one event for a unit at a timestamp using a database unique constraint.
- Permit explicit sequence numbers and validate every sequential transition.
- Permit a limited set of compound events defined by configuration.

Until decided, production entry must reject ambiguity. Migration must use legacy source ID only as deterministic provenance, report the ambiguity, and must not claim that source ID represents approved business order.

Events for different units may share a timestamp because each unit has an independent state machine.

## 6. Event statistics

For an accepted event inside the effective Reporting Period:

| Event | Total | Type count | Shift count |
|---|---:|---:|---:|
| START | +1 | START +1 | Day or night START +1 |
| NSD | +1 | NSD +1 | Day or night NSD +1 |
| ESD | +1 | ESD +1 | Day or night ESD +1 |
| OH | +1 | OH +1 | OH shift classification is **Pending Product Owner Decision** |

Pre-period accepted events affect state only. Rejected events contribute zero. Whether diagnostically preserved invalid legacy events appear in a separate raw-event count is **Pending Product Owner Decision**; they must never be mixed invisibly into authoritative valid-event counts.

Day/night boundaries are station configuration. Retaining the legacy 07:00-inclusive to 19:00-exclusive day shift for Rasht/Ramsar is **Pending Product Owner Decision** until confirmed as configuration data.

## 7. Production validation contract

For every proposed event, validation must:

1. Resolve the station-definition version applicable on the Operating Day.
2. Verify station, unit, event type, date, time, and authorization.
3. Load the last authoritative pre-change state.
4. Order all events deterministically.
5. Validate the complete replacement day's chain.
6. Validate its connection to the first subsequent authoritative event.
7. Reject unresolved transitions until their policies are approved and configured.
8. Reject changes to a locked period inside the write transaction.
9. Preserve the existing valid history if validation fails.
10. Return stable diagnostic codes rather than relying on localized message text.

## 8. Migrated invalid-history contract

Migration diagnostics must include:

- Source database identity and row ID.
- Station and unit.
- Event date, time, and raw type.
- Previous reconstructed state.
- Proposed event.
- Anomaly code.
- Whether the event was preserved as raw evidence.
- Whether it was included in authoritative replay.
- Selected reconciliation policy and approver, if any.

Migration must not silently reorder same-time events, infer missing events, apply ESD Adjustment to unresolved transitions, or rewrite finalized legacy values. Authoritative runtime remains unavailable or explicitly provisional where unresolved anomalies materially affect state.

## 9. Blocking decision register

Runtime implementation cannot be declared production-ready until these are frozen:

1. Running + START.
2. Stopped + NSD.
3. StoppedAfterOH + NSD.
4. Stopped + ESD.
5. StoppedAfterOH + ESD.
6. Running + OH.
7. Stopped + OH.
8. StoppedAfterOH + OH.
9. Same-time event policy.
10. All ESD Adjustment targets.
11. ServiceDay duration threshold.
12. LongestRunInPeriod boundary definition.
13. Initially Running baseline with unknown start.
14. OH day/night statistics.
15. Treatment of invalid migrated events in non-authoritative raw statistics.
