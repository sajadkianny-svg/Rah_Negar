# RahNegar Version 1 Domain Glossary

## 1. Purpose and authority

This glossary defines the authoritative language of the generalized RahNegar Platform. Code identifiers, database documentation, configuration schemas, tests, reports, migration diagnostics, and user-facing explanations must use these terms consistently.

A definition marked **Approved terminology** fixes the meaning of the term, but does not necessarily approve every rule that affects its calculation. A rule marked **Pending Product Owner Decision** must not be embedded in production behavior until formally approved and recorded in an ADR or product-rule decision.

## 2. Runtime terminology

### 2.1 PeriodRuntimeHours

**Status:** Approved terminology; ESD inclusion is **Pending Product Owner Decision**.

The accounted runtime attributed to one unit inside the effective Reporting Period. Physical running intervals are intersected with the period; time before the period start and at or after the exclusive period end is excluded.

The physical component is unambiguous:

```text
PhysicalPeriodRuntime = sum(intersection(physical run intervals, effective period))
```

Whether `PeriodRuntimeHours` also includes an ESD Adjustment is **Pending Product Owner Decision**. Until resolved, the domain result must expose physical runtime and ESD adjustment separately and must not imply that their sum is approved.

`PeriodRuntimeHours` never includes a Trusted Runtime Baseline merely because the baseline exists. It must not be used as a synonym for lifetime or cumulative runtime. Internally, duration is represented in integral seconds; hours are a presentation/reporting conversion.

### 2.2 CumulativeRuntimeAtPeriodEnd

**Status:** Approved terminology; ESD inclusion is **Pending Product Owner Decision**.

The accounted lifetime runtime for one unit at the exclusive end of a Reporting Period, reconstructed from a Trusted Runtime Baseline and all authoritative events after that baseline through the period endpoint.

Its physical component is:

```text
Baseline cumulative runtime + physical running duration after the baseline
```

It does not reset at OH and must not decrease during valid event replay. Changing only the report start while retaining the same endpoint and the same source history must not change this result.

Whether ESD Adjustments increase cumulative runtime is **Pending Product Owner Decision**. Administrative correction or replacement of a baseline is an audited operation, not an event transition.

### 2.3 RuntimeAfterOHAtPeriodEnd

**Status:** Approved terminology; ESD inclusion is **Pending Product Owner Decision**.

The accounted runtime for one unit after the most recent authoritative OH boundary at or before the exclusive end of the Reporting Period.

OH resets this value to zero without reducing CumulativeRuntimeAtPeriodEnd. Valid physical runs after that boundary increase it. If no OH occurs after the Trusted Runtime Baseline, calculation starts with the baseline's after-OH value.

This is endpoint state, not merely runtime inside the selected period. An OH before the report start therefore remains relevant. Whether an ESD Adjustment after OH increases this value is **Pending Product Owner Decision**.

### 2.4 Physical Run Interval

**Status:** Approved terminology.

A half-open time interval `[start, end)` during which the runtime event state is Running. It begins at an authoritative START or at an explicitly running Trusted Runtime Baseline. It ends at a valid state-changing event, an evaluation boundary, or another rule explicitly approved by the product owner.

An open interval may cross operating days and reporting periods. Reporting clips it without inventing a shutdown event.

### 2.5 ServiceDay

**Status:** Definition approved; threshold and ESD interaction are **Pending Product Owner Decision**.

A Persian Operating Day for which one unit has a qualifying overlap with a physical run interval.

Recommended rule:

```text
intersection(physical run intervals, Operating Day) > 0 seconds
```

Under that recommendation, a midnight-crossing run can produce multiple ServiceDays, an event without elapsed running time creates none, and an ESD accounting adjustment alone creates none.

The minimum qualifying duration and whether an ESD Adjustment can create a ServiceDay are **Pending Product Owner Decision**. ServiceDays reported for a period are limited to applicable dates inside the effective period and never include dates before `data_start_date`.

### 2.6 LongestRunInPeriod

**Status:** Definition approved; boundary semantics are **Pending Product Owner Decision**.

The maximum duration among continuous physical run intervals relevant to a Reporting Period.

Two possible meanings remain:

1. Longest period-clipped run: measure only the intersection of each run with the period.
2. Longest whole run touching the period: include duration before or after the period.

The choice is **Pending Product Owner Decision**. The recommended Version 1 definition is the period-clipped run because the metric then describes activity inside the selected period. ESD Adjustment must remain separate unless explicitly approved to extend a run, which is also **Pending Product Owner Decision**.

### 2.7 ESD Adjustment

**Status:** Approved terminology; all accounting targets are **Pending Product Owner Decision**.

A configured accounting-duration associated with an accepted ESD event. It is not, by itself, evidence of physical running time.

The configuration must specify:

- Whether the adjustment is enabled.
- Its nonnegative duration.
- Which accepted ESD transitions qualify.
- Whether it affects PeriodRuntimeHours.
- Whether it affects CumulativeRuntimeAtPeriodEnd.
- Whether it affects RuntimeAfterOHAtPeriodEnd.
- Whether it affects ServiceDay.
- Whether it affects LongestRunInPeriod.

All five effects are **Pending Product Owner Decision**. Physical duration and adjustment duration must always remain separately auditable even if an approved report total adds them.

### 2.8 Trusted Runtime Baseline

**Status:** Approved terminology; treatment of unknown historical start time is **Pending Product Owner Decision**.

An authoritative, auditable starting point from which runtime event replay may begin. It belongs to one unit and contains:

- Effective operating timestamp.
- Cumulative runtime at that timestamp.
- Runtime after OH at that timestamp.
- Event State at that timestamp.
- Open-run start when state is Running, if known.
- Last OH boundary when known.
- Source and provenance.
- Calculation/baseline format version.

Values must be nonnegative. A StoppedAfterOH baseline must have zero runtime after OH. A Running baseline without a known run start is historically incomplete; whether runtime begins at the baseline timestamp or requires an explicit uncertainty policy is **Pending Product Owner Decision**.

A runtime checkpoint can qualify as a Trusted Runtime Baseline only when its provenance, calculation version, event watermark, and integrity are verified. An arbitrary cached value is not trusted.

## 3. Event terminology

### 3.1 START

**Status:** Approved terminology; repeated-START behavior is **Pending Product Owner Decision**.

An event requesting transition from a non-running Event State to Running and opening a physical run interval. A START at an already Running unit is a repeated START; its validity and effect are unresolved.

### 3.2 NSD

**Status:** Approved terminology; stopped-state behavior is **Pending Product Owner Decision**.

A normal shutdown event. When accepted from Running, it closes the physical run and transitions to Stopped. Whether NSD is valid while already stopped is unresolved.

### 3.3 ESD

**Status:** Approved terminology; stopped-state and adjustment behavior are **Pending Product Owner Decision**.

An emergency shutdown event. When accepted from Running, it closes the physical run and transitions to Stopped. It may cause an ESD Adjustment under an approved policy. Whether ESD is valid while stopped and whether it receives an adjustment in that state are unresolved.

### 3.4 OH

**Status:** Approved terminology; source-state validity is **Pending Product Owner Decision**.

An overhaul event establishing an overhaul boundary for a unit. An accepted OH resets RuntimeAfterOHAtPeriodEnd to zero and preserves cumulative runtime. It does not itself prove physical runtime.

Whether OH may occur while Running, while Stopped, or repeatedly while StoppedAfterOH is **Pending Product Owner Decision**. If accepted from Running, whether it closes the physical run directly or requires a preceding shutdown is also pending.

### 3.5 Event State

**Status:** Approved terminology.

The authoritative operational state of one unit at a specific point in deterministic event history.

Version 1 defines:

- **Stopped:** No open physical run; the latest state-changing fact is not an OH boundary requiring distinct after-OH treatment.
- **Running:** A physical run is open. The state must retain its start timestamp or an explicit approved uncertainty marker.
- **StoppedAfterOH:** No physical run is open, and the most recent accepted state-changing event is OH. Runtime after OH is zero at the OH boundary.

Missing, contradictory, or unreconstructable state is a data/configuration error, not a fourth normal operating state.

### 3.6 Deterministic Event Order

**Status:** Principle approved; same-time policy is **Pending Product Owner Decision**.

The total ordering used for event replay, based on Operating Day, local event time, and a stable sequence or identity. Database retrieval order is never authoritative.

Whether multiple events for the same unit may share a timestamp is **Pending Product Owner Decision**. Until resolved, configuration and persistence must not silently choose an order.

## 4. Operational data terminology

### 4.1 Operating Day

**Status:** Approved terminology.

One station's logical Persian-calendar date of operational data. It identifies the applicable station-definition version and groups scheduled observations, daily station values, and optional unit events.

An Operating Day is distinct from completeness: it may exist while incomplete. Its identity is `(StationId, PersianDate)`. It cannot precede the station's `data_start_date` in normal production data.

### 4.2 Observation

**Status:** Approved terminology.

A typed value for a configured measurement definition at a configured or recorded time and scope. Scope is either the station or a particular unit. Its business identity includes Operating Day, time, measurement, and scope.

### 4.3 Daily Station Value

**Status:** Approved terminology.

A typed station-wide value recorded once per applicable Operating Day for a configured daily-value definition, such as a daily fuel or flow total. It is not a scheduled observation.

### 4.4 Completeness

**Status:** Core definition approved; event participation and field-specific policies may be **Pending Product Owner Decision**.

The condition that an applicable Operating Day contains exactly the required, valid data specified by the station-definition version effective on that date.

Completeness evaluates:

- Applicable scheduled times.
- Required station-scoped observations.
- Required unit-scoped observations for applicable units.
- Required Daily Station Values.
- Duplicate logical observations/values.
- Data types, allowed statuses, ranges, and approved formulas.
- Structural integrity needed to interpret the day.

Dates before `data_start_date` are outside scope and are neither missing nor incomplete. A first period evaluates from `max(period start, data_start_date)`. Missing, invalid, and duplicate data are distinct failures.

Events are optional under legacy behavior. Whether a future configuration may make an event mandatory for completeness is **Pending Product Owner Decision**. Entry, missing-day checks, reporting, and finalization must use one authoritative completeness service.

## 5. Reporting and finalization terminology

### 5.1 Reporting Period

**Status:** Approved terminology.

An inclusive Persian-date range for which domain report results are requested. It has a period type such as Monthly, FirstHalfYear, SecondHalfYear, Yearly, or an internally supported custom range.

Its effective range begins at the later of the requested start and `data_start_date`. A period with no applicable dates cannot be finalized. Runtime evaluation uses an exclusive timestamp immediately after the inclusive end date.

### 5.2 Finalized Report

**Status:** Approved terminology.

An immutable, versioned domain snapshot of a completed report for one station and Reporting Period. It contains all semantic report data necessary for future reproduction without recalculating from mutable operational data or current configuration.

It records station/configuration identity, calculation versions, completeness, summaries, runtime results, event information, provenance, finalizer, and content hash. Finalization is successful only when snapshot creation and its Period Lock commit atomically.

### 5.3 Period Lock

**Status:** Approved terminology.

An authoritative database record preventing normal production modification of data or effective rules that would change a Finalized Report's period.

It references the Finalized Report that caused the lock and protects observations, daily values, events, runtime baselines, deletions, and retroactive configuration changes affecting the period. Version 1 has no ordinary unlock operation. The semantics of a future audited correction/reopen workflow are outside Version 1.

### 5.4 Finalized Multi-Period Report

**Status:** Approved terminology; persistence policy is defined in the snapshot schema.

A report semantically derived from two or more finalized monthly snapshots. It must reference the exact source snapshots and aggregation versions. It must never silently combine finalized values with live mutable calculations.

## 6. Normative unresolved-decision register

The following definitions are intentionally incomplete and marked **Pending Product Owner Decision**:

1. Whether ESD Adjustment affects each runtime metric.
2. Whether ESD Adjustment affects ServiceDay or LongestRunInPeriod.
3. ServiceDay minimum qualifying physical duration.
4. Period-clipped versus whole-run LongestRunInPeriod.
5. Repeated START handling.
6. NSD while Stopped or StoppedAfterOH.
7. ESD while Stopped or StoppedAfterOH.
8. OH while Running, Stopped, or StoppedAfterOH.
9. Same-time events for one unit.
10. Initially Running baselines with unknown historical run start.
11. Whether configuration may require events for Completeness.

No recommended behavior in another design document overrides this register without an approved decision record.
