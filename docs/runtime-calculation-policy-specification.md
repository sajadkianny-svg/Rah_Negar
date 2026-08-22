# Runtime Calculation Policy Specification

**Project:** Rah_Negar  
**Document status:** Pre-implementation domain policy specification; unresolved decisions require domain approval  
**Production scope:** Rasht and Ramsar only  
**Implementation status:** Documentation only; this document does not activate or implement Runtime calculation  
**Source basis:** `phase4-runtime-domain-foundation-report.md`, `event-subsystem-architecture-specification.md`, `event-database-schema-specification.md`, and `master-implementation-roadmap.md`

## 1. Executive overview

Runtime is a deterministic projection over a trusted Runtime Baseline and a complete, ordered, already validated Event Chain for one Station and one Unit. Events are the sole operational authority. Hourly ST/RPM observations, daily values, UI state, and legacy cached totals neither establish nor correct operating state.

The Runtime Projection Engine must reject an invalid chain, identity mismatch, conflicting baseline/chain state, missing required history, or invalid period. It must not reorder ambiguous Events, synthesize START or shutdown Events, infer an Event from observations, ignore a forbidden transition, or otherwise repair Event history. Repair belongs to the audited Event workflow.

For identical canonical inputs and versions, calculation must produce identical outputs. Calendar conversion, chronological ordering, period clipping, adjustments, and rounding must therefore be centralized and independent of machine culture, current date, UI formatting, SQLite row order, or execution path.

Runtime policy is business data, not an unversioned constant. Every calculation must identify the Event Chain, current configuration, Runtime policy, and Baseline used, and record when it was calculated. An open-period projection uses the current ESD Adjustment and is calculated on demand. A finalized report instead preserves an immutable snapshot and the exact configuration evidence used at finalization; later changes must never reinterpret it.

This document approves rules already supported by the source specifications and makes conservative engine-level decisions needed for an implementable contract. Items whose business values or station-specific meaning are unknown are marked **REQUIRES DOMAIN APPROVAL**. No such item may be guessed from legacy code or shared between Rasht and Ramsar without evidence.

## 2. Runtime definition

### 2.1 Physical Runtime

Physical Runtime is elapsed chronological minutes during which the Unit is in `Running` state. A valid `START` opens a run. A valid `NSD` or `ESD` closes it. For a closed run, duration is:

```text
PhysicalRuntime = shutdown Event minute - START Event minute
```

The closing Event is either a valid `NSD` or a valid `ESD`. There is no generic STOP Event. Event types remain exactly `START`, `NSD`, `ESD`, and `OH`. The opening minute is included and the closing minute is excluded. Consequently, START and shutdown at the same minute would yield zero duration, but same-Unit/same-minute Event uniqueness and chain rules are authoritative and may reject that Event pair before calculation.

An open `Running` state at the calculation end is not an error when established by a valid chain or a trusted Running Baseline. Its physical interval is clipped at the requested period end. The engine must not create a synthetic shutdown Event. The projection must preserve the resulting `Running` state and the software-owned open-run start for continuation into a later calculation. For a Running Baseline, that software-owned start is the Baseline boundary; pre-baseline run-start provenance is neither required nor invented.

At period boundaries, physical intervals are intersected with the half-open requested interval `[PeriodStart, PeriodEnd)`. Time before PeriodStart and at or after PeriodEnd contributes zero to the period value, while pre-range history is still replayed to establish the correct state and cumulative values.

### 2.2 ESD Adjustment and Adjusted Runtime

ESD Adjustment is a policy-defined number of non-physical minutes attached exactly once to an accepted `Running + ESD -> Stopped` transition. It is always separately traceable to its source Event and policy version. An ESD in any other state is invalid upstream and contributes zero; the Runtime engine rejects the invalid chain rather than continuing with zero as if the chain were valid.

```text
AdjustedRuntime = PhysicalRuntime + ESDAdjustment
```

Adjusted Runtime never changes the length of a physical run and does not create physical overlap, a Service Day, or a Longest Run. Period ESD Adjustment belongs to the period containing the ESD Event timestamp under the half-open boundary rule.

### 2.3 Runtime After OH

RuntimeAfterOH is adjusted runtime accumulated since the latest valid OH, initialized from the trusted Baseline when no later OH exists. It increases by physical Running minutes after the reset and by eligible ESD adjustments after the reset.

A valid `Stopped + OH -> StoppedAfterOh` sets RuntimeAfterOH to zero at the OH timestamp. OH does not change historical or cumulative Physical Runtime, ESD Adjustment, or Adjusted Runtime. Values calculated for an interval before the OH remain historical facts; the reset changes the state carried forward from the OH instant only.

Period output `RuntimeAfterOH` is a state-at-PeriodEnd value, not a duration clipped and summed only inside the period. This is consistent with its cumulative-since-reset meaning. Its Baseline or latest OH provenance must be retained in calculation trace data.

### 2.4 Longest Run

LongestRun is the greatest duration among continuous physical Running intervals after each interval is intersected with `[PeriodStart, PeriodEnd)`. ESD Adjustment is excluded.

- A run starting before the period is clipped to PeriodStart.
- A run ending after the period, or still open, is clipped to PeriodEnd.
- A run spanning midnight or multiple Persian days remains one continuous run; midnight does not split it for LongestRun comparison.
- A shutdown followed by a later START creates separate runs.
- If there is no positive physical overlap, LongestRun is zero.

When equal maximum durations occur, the numeric output is unambiguous. If diagnostic run identity is exposed later, the earliest clipped start, then source EventId, is the deterministic tie-breaker; this does not alter the required projection fields.

## 3. Time model

Operator Event dates use the Persian calendar and the existing RahNegar Persian-date convention. Date validation and addition must use one centralized Persian-calendar service, including real month lengths, year transitions, and leap/non-leap Esfand. Numeric `yyyyMMdd + 1`, host-culture parsing, and silent normalization are forbidden.

Internal ordering and duration arithmetic use a canonical integer chronological local minute key derived from the Persian Event date and minute-of-day. Its epoch, local-wall-clock convention, supported range, and conversion algorithm must be fixed in versioned metadata. It is a calendar chronology, not an audit UTC timestamp, and it must not apply the machine’s current UTC offset to historical Events.

Operational calculation and storage precision is one integral minute. Event seconds are prohibited. Runtime reports display hours with exactly two decimal places, converted only from authoritative minutes. For example, 90 minutes displays as `1.50 h`, and 125 minutes displays as `2.08 h`. Rounded display hours must never be stored as Runtime authority or fed back into a calculation.

All intervals are half-open:

```text
[StartBoundary, EndBoundary)
```

The start minute is included and the end minute is excluded. `PeriodEnd` must be later than `PeriodStart`. For a Persian date-range request whose UI end date is described as “inclusive,” the application layer converts it to the next local midnight and passes that exclusive instant to the engine. The engine contract itself never accepts an ambiguous inclusive end.

For equal timestamps, same-Unit active Events are invalid because the Event store requires uniqueness. EventId may provide deterministic diagnostics for corrupted legacy data, but cannot legalize or order conflicting same-minute Unit Events for calculation.

## 4. Service Day policy

The approved default service-day boundary is local `00:00`; a Service Day is `[local midnight, next local midnight)`. Midnight belongs to the new Persian date. A run ending exactly at midnight does not count the new day, while a run beginning at midnight can count it if it has positive overlap.

`ServiceDayCount` is the number of distinct service days having positive physical Running overlap with the requested period. ESD Adjustment alone never creates a Service Day. A cross-day run contributes one day for every intersected service day with positive physical overlap, regardless of how few minutes overlap. Calendar transitions are found through Persian calendar addition, including month/year changes and leap/non-leap Esfand.

The policy model may support a versioned Station-specific boundary, but production currently has no approved non-midnight value. Therefore:

- Rasht and Ramsar use `00:00` unless a separately versioned and approved Station policy states otherwise.
- Unit-specific service-day boundaries are not approved.
- A future boundary change must have an effective chronological minute and must not be back-applied silently.

**REQUIRES DOMAIN APPROVAL:** whether Station-specific non-midnight boundaries are a real business requirement. Until approved, implementation must reject such configuration rather than expose an unused configurable feature.

## 5. ESD Adjustment policy

### 5.1 Ownership and initial configuration

There is one common ESD Adjustment value for the Station/deployment. It is not Unit-specific. The initial Wizard collects this value once, and the value applies equally to every configured Unit in that deployment. Zero is a valid configured value. No Rasht or Ramsar default may be invented.

Every accepted `Running + ESD -> Stopped` transition receives the common current value exactly once. An invalid ESD transition receives no adjustment and makes the Event Chain invalid; the Runtime engine does not calculate a partial result around it.

The authoritative value is stored in integral minutes even if the Wizard or Settings UI also displays hours. Input conversion and validation must preserve exact minute authority and use the approved two-decimal-hour presentation rule only for display. **REQUIRES DOMAIN APPROVAL:** the allowed minimum and maximum configured ESD Adjustment and the exact Wizard/Settings input unit if those constraints are not already specified by the Foundation configuration contract.

### 5.2 Change and recalculation semantics

The common ESD Adjustment may later be changed through protected Settings. For every open or unlocked reporting period, Runtime is calculated on demand using the **current** configured ESD Adjustment for all valid ESD Events in that requested open period. The value is not selected from event-time effective-dated history.

For example, if the old setting was 100 hours and the current setting is 120 hours, an earlier valid ESD in an unlocked period is recalculated using 120 hours. A change affects the next open-period calculation without rewriting Events or persisting an authoritative live Runtime total.

For a finalized or locked report, the stored Runtime result and its configuration evidence remain immutable. A later ESD change does not update, reinterpret, or recompute that snapshot. Finalization must preserve at least the ESD Adjustment value and configuration version used, along with the Event, Baseline, Runtime policy, and Calculation versions.

Configuration changes are audited. The audit must retain old value, proposed/accepted new value, actor, timestamp, reason, authorization request/outcome, and applicable configuration version. Version history supports audit and finalized-snapshot reproduction; it is not used to choose an old value by ESD Event time for an open-period projection.

### 5.3 Sensitive-setting authorization requirement

Changing ESD Adjustment after Wizard completion is a sensitive action requiring Support Authorization. Normal ShiftProfile access cannot directly authorize it.

The later Settings workflow must:

1. Show a Persian message explaining that a one-time security code must be obtained from software support.
2. Display the deployment Device ID and request information needed by support.
3. Allow the user to send that Device ID/request information to software support outside this Runtime calculation workflow.
4. Require a one-time authorization code produced by a separate professional support-side code-generation tool.
5. Validate the code before permitting the configuration change.
6. Bind authorization specifically to the ESD Adjustment change and audit both successful and failed authorization outcomes and the resulting sensitive change.

The authorization must not be a reusable master password, shared static code, or hidden universal backdoor. The preferred later Security/Foundation design binds authorization at minimum to `DeviceId`, a unique `RequestId`/nonce, `Action = ChangeEsdAdjustment`, the proposed new value, and expiry. The support-side secret or private signing capability must never be embedded in the customer installation.

Cryptography, code generation, code validation implementation, transport to support, and the support tool are outside Phase 4 Runtime scope. They are explicit future Security/Foundation implementation requirements and must be threat-modeled, versioned, tested for replay/action/value/device binding, and audited before protected Settings can activate this change.

## 6. OH policy

OH has no effect on Physical Runtime, cumulative ESD Adjustment, or cumulative Adjusted Runtime. Its only numeric effect is to reset RuntimeAfterOH to zero at the valid OH timestamp.

The Event vocabulary remains exactly `START`, `NSD`, `ESD`, and `OH`; this policy does not introduce a generic STOP Event.

OH is valid only for `Stopped + OH -> StoppedAfterOh`. `Running + OH`, `StoppedAfterOh + OH`, and all other forbidden transitions make the Event Chain invalid. The Runtime engine rejects that chain and emits no successful projection. It does not insert a shutdown, move the OH, ignore it, or partially calculate around it.

Historical OH records that violate the target state machine remain migration/reconciliation issues. They may be compared under labeled legacy behavior, but cannot enter an authoritative target projection until corrected or otherwise dispositioned outside the Runtime engine through the approved migration/reconciliation process. The target Runtime policy has no historical OH exception and must not be weakened to accommodate invalid legacy records.

## 7. Baseline policy

### 7.1 DataStartDate and software responsibility

`DataStartDate` and `RuntimeBaseline` are different concepts.

The initial Wizard asks for a Persian year and month. `DataStartDate` is the first day of that Persian month at local `00:00`. It establishes the beginning of software responsibility for operational data entry. Data entry begins on that day and follows the separately approved sequential-entry rules. `DataStartDate` must not itself be named or treated as a Runtime Baseline.

The application has no responsibility to reconstruct Runtime before `DataStartDate`. Events before this boundary are outside the software-owned Event Chain and are not required to justify the trusted starting facts. The boundary must be derived with the centralized Persian calendar service, not host-culture parsing.

### 7.2 Per-Unit Runtime Baseline

A separate Trusted Runtime Baseline exists for every configured Unit and is effective exactly at `DataStartDate 00:00`. During the initial Wizard, the operator supplies trusted starting values corresponding to the legacy business concepts:

- **Total Runtime** — the authoritative cumulative Runtime fact at the responsibility boundary;
- **Runtime After OH** — the authoritative cumulative post-OH Runtime fact at that boundary;
- **Initial Operational State** — `Stopped`, `Running`, or the approved target representation corresponding to stopped after OH.

Target code may use canonical domain names, but it must preserve these meanings. The Wizard values are authoritative starting facts. If they do not decompose pre-baseline Total Runtime into historical Physical Runtime and historical ESD Adjustment, the target must not invent that decomposition. Period projection after the Baseline still exposes physical and adjustment components for software-owned calculations, while cumulative values carry forward the trusted starting facts according to the approved domain model.

Each Baseline records Station, Unit, effective boundary, the three trusted starting values, immutable version, and provenance. Provenance records that it came from the initial Wizard or an approved controlled correction, actor, timestamp, source/evidence reference when available, approval/authorization evidence when required, and superseded version. **REQUIRES DOMAIN APPROVAL:** the protected correction workflow, authorization level, and audit rules for changing a Baseline after Wizard completion.

### 7.3 Initial state and report-start behavior

No historical START Event before `DataStartDate` is required or invented.

If Initial Operational State is Running, physical Runtime accrual under software responsibility begins at `DataStartDate 00:00`. That boundary is the software-owned open-run start. A later valid NSD or ESD closes the interval. For first-period Physical Runtime and LongestRun, the observable run is clipped to the software responsibility boundary; its unknown pre-baseline duration is never reconstructed or included as a physical interval.

If a later report starts while the Unit is Running, replay from the Baseline establishes the software-owned open run and its true start within the owned history, possibly the Baseline boundary. The report clips physical contribution and LongestRun to PeriodStart without losing continuity needed for state or cumulative calculation.

If the report starts while Stopped or StoppedAfterOh, no physical minutes accrue until a later valid START. A Baseline `StoppedAfterOh` state carries the Wizard-entered Runtime After OH starting fact; the domain must not overwrite that trusted value merely because the state name contains “AfterOh.” A valid OH after the Baseline resets RuntimeAfterOH to zero.

The Event Chain begins at or after the Baseline boundary and its declared initial state must match the Baseline Initial Operational State. Missing Baseline, multiple Baselines for a Unit at the same responsibility boundary, invalid trusted values, Station/Unit mismatch, or state conflict fails calculation. A Running Baseline without a pre-baseline `OpenRunStartedAtMinute` is valid and must not fail.

## 8. Period calculation rules

### 8.1 Common algorithm

For one Station, Unit, and half-open requested period:

1. Resolve DataStartDate, the immutable per-Unit Baseline, Runtime policy version, and current configuration version.
2. Load the complete active Event Chain from the Baseline through PeriodEnd, plus the version marker needed to prove dataset consistency.
3. Require successful authoritative Event-chain validation and identity/state agreement.
4. Replay chronologically from the Baseline, preserving state and cumulative values.
5. Intersect physical runs with the requested period for period Physical Runtime and LongestRun.
6. For an open period, assign the same current deployment ESD Adjustment to every valid in-period ESD; for finalized output, use the immutable stored snapshot rather than recalculating it.
7. Enumerate distinct service days with positive physical overlap.
8. Apply valid OH resets to the carried RuntimeAfterOH state.
9. Return the projection and complete version/provenance metadata atomically from a consistent read snapshot.

An Event at PeriodStart belongs to the period. An Event at PeriodEnd does not. It may be read only when necessary to close an interval for a broader cumulative query, but it has no period effect under the contract.

### 8.2 Daily calculation

A daily request is `[Persian day 00:00, next Persian day 00:00)`. A run begun earlier contributes from day start; a run continuing later contributes through day end. ServiceDayCount is either zero or one under the approved midnight policy. LongestRun is the longest physical segment inside that day.

### 8.3 Monthly calculation

A monthly request is `[first day of Persian month 00:00, first day of next Persian month 00:00)`. The next boundary is calendar-derived, including Esfand in leap and non-leap years. Runs are not split conceptually at day boundaries, though daily overlap is enumerated for ServiceDayCount. Finalized monthly output must be read from its immutable snapshot and original versions; a current-engine recalculation is a labeled comparison, not a replacement.

### 8.4 Arbitrary date range

An arbitrary date range is normalized to explicit chronological minute boundaries. If expressed as whole Persian dates, it begins at the first date’s midnight and ends at midnight after the last included date. If future UI supports partial-day ranges, it must pass explicit minute boundaries using the same half-open rule.

### 8.5 Boundary-spanning runs

- Start before period, stop inside: count `[PeriodStart, Stop)`.
- Start inside period, stop after: count `[Start, PeriodEnd)`.
- Start before and stop after: count the entire requested period.
- Open at PeriodEnd: count through PeriodEnd and retain Running state; do not synthesize NSD, ESD, or any generic shutdown Event.
- Span multiple days: count continuous physical minutes once, enumerate each positively overlapped Service Day, and treat the continuous clipped interval as one LongestRun candidate.

## 9. Data-quality rules

The Runtime engine consumes only a validated Event Chain and does not downgrade errors to warnings.

| Condition | Required behavior |
|---|---|
| Missing shutdown / open run | Valid only when the chain legitimately ends in Running state. Clip at PeriodEnd, retain the software-owned open state/start, and do not synthesize an Event. |
| Duplicate Event time for same Unit | Reject as invalid/ambiguous. Database uniqueness should prevent active target data; legacy duplicates require remediation. |
| Invalid chain | Reject the entire projection with structured validation evidence; no partial totals. |
| Missing Baseline | Reject; never infer initial state or zero totals. |
| Conflicting state | Reject when Baseline state, chain initial state, replayed state, or declared resulting state disagree. |
| Event before Baseline/DataStartDate | Outside software responsibility and excluded from the authoritative target chain. It is not required to justify the Baseline and must never mutate trusted starting facts. Migration may retain it as legacy evidence. |
| Unknown Event type or invalid Persian time/date | Reject upstream and reject calculation input if encountered. No coercion or midnight fallback. |
| Missing current ESD configuration | Reject an open-period calculation when configuration cannot be resolved; zero is valid only when explicitly configured. Do not fall back to a Unit or Station default. |
| Station/Unit mismatch | Reject and report identity mismatch; never merge or reuse another Unit’s history. |
| Observation/Event contradiction | Ignore the observation for Runtime authority; report through a separate data-quality process if required. Do not repair Events. |

Errors must include stable code, Station, Unit, period, relevant Event/Baseline/policy identity, reason, and corrective ownership. A failed calculation produces no authoritative projection or finalized snapshot.

## 10. Runtime Projection model

The required projection output for one Station, Unit, and requested period contains:

| Field | Type and meaning |
|---|---|
| `PhysicalRuntime` | Non-negative integral minutes of physical Running overlap in the period. |
| `ESDAdjustment` | Non-negative integral configured minutes assigned to valid ESD Events in the period. Zero is valid. The maximum allowed value remains subject to the approved configuration-validation contract. |
| `AdjustedRuntime` | `PhysicalRuntime + ESDAdjustment` for the period. |
| `RuntimeAfterOH` | Non-negative adjusted cumulative minutes since latest valid OH, as of PeriodEnd. |
| `LongestRun` | Non-negative integral minutes of the longest period-clipped continuous physical run. |
| `ServiceDayCount` | Non-negative count of distinct service days with positive period physical overlap. |
| `CalculationVersion` | Immutable identifier for engine algorithm/code semantics. |
| `PolicyVersion` | Immutable identifier or manifest hash covering every policy revision used. |

The contract should also carry StationId, UnitId, PeriodStart, PeriodEnd, resulting operational state, EventChainVersion, BaselineVersion, CalculationTimestamp, and trace references even if these are metadata rather than the eight requested metric fields. `AdjustedRuntime` must be checked exactly against its components before return.

For an open period, this projection is calculated on demand and is not persisted as authoritative business truth. The inputs are the trusted Baseline, authoritative validated Event Chain, and current applicable configuration. Event or ESD-setting changes naturally affect the next calculation. A future performance cache is permitted only as a disposable, non-authoritative optimization keyed by all input/version identities and safely invalidated on every relevant change.

For a finalized or locked report, the Runtime results are stored inside the immutable finalized report snapshot with the necessary Event, Baseline, policy, configuration, and calculation evidence. Later Event or configuration changes never rewrite that result.

## 11. Versioning and reproducibility

Every successful calculation must identify:

- `EventChainVersion`: a consistent dataset marker/hash or equivalent source revision for the active Station/Unit Events read;
- `RuntimePolicyVersion`: a version identifying service-day, ESD semantics, OH, boundary, display, and calendar policies used;
- `ConfigurationVersion`: the current configuration used for an open-period calculation, including the common deployment ESD Adjustment, or the configuration evidence captured by a finalized snapshot;
- `BaselineVersion`: the exact immutable trusted Baseline;
- `CalculationVersion`: the engine algorithm version;
- `CalculationTimestamp`: UTC instant from the trusted application clock.

Reproduction requires the canonical inputs and policy/configuration artifacts, not merely the application release number. The trace must include period boundaries, DataStartDate, Station/Unit, Event identities and canonical timestamps, adjustment contributions, the common ESD value/configuration version used, calendar/conversion version, Baseline provenance, and output metrics.

Version identifiers are content-stable and immutable. Editing a policy, configuration, or Baseline creates a new version/audit record; it never mutates old evidence. Open-period projections use the one current ESD configuration at calculation time even for earlier in-period ESD Events. Finalized snapshots retain their original Event, policy, configuration, Baseline, and Calculation versions and are never silently recalculated.

## 12. Comparison with Legacy Runtime

Before activation, the new engine runs in shadow comparison against representative Rasht and Ramsar data without changing production results. Each comparison must use:

- the same Station and Unit;
- the same normalized half-open input period;
- the same source Event boundary/dataset snapshot;
- documented Baseline and policy inputs for the new engine;
- the legacy calculation/version identity and raw legacy output;
- component-level new output, not only a final total.

Differences are classified as:

1. **MATCH** — equal after display-only formatting.
2. **EXPECTED_POLICY_DIFFERENCE** — caused by an approved rule change, such as rejecting `Running + OH` or stopped-state ESD adjustment; requires rule reference and owner approval.
3. **LEGACY_CONFIRMED_DEFECT** — evidence proves legacy behavior is wrong; requires defect record, regression test, and approval before cutover.
4. **NEW_ENGINE_DEFECT** — target result violates this specification or approved fixture; blocks activation.
5. **INPUT_OR_VERSION_MISMATCH** — period, Unit, Events, Baseline, or policy differs; comparison is invalid and must be rerun.
6. **UNRESOLVED_DOMAIN_DIFFERENCE** — neither behavior is approved; blocks activation for the affected scope.

No tolerance, averaging, or unexplained “close enough” category is permitted for integral-minute authoritative values. Display rounding differences are compared at authoritative minute precision first. Finalized legacy reports are preserved and not overwritten by shadow results.

## 13. Testing requirements

Tests must be deterministic, table-driven where practical, and cover unit, SQLite integration, full projection scenarios, and approved Rasht/Ramsar fixtures without inventing Station values.

Minimum cases include:

- START followed by NSD; START followed by ESD; multiple separate runs; zero-overlap period.
- Running Baseline accruing from DataStartDate, plus valid open runs originating at and inside later periods; ensure no pre-baseline START and no synthetic shutdown Event.
- Every valid and forbidden state transition; invalid chains produce no projection.
- Common deployment ESD value applied equally to multiple Units, zero value, exactly-once application, missing current configuration, and no Unit-specific fallback.
- Open-period recalculation of earlier valid ESD Events after the current setting changes, proving the new value applies without event-time historical selection.
- Finalized snapshot retains the older ESD value/evidence after a later setting change and is not rewritten.
- Sensitive ESD-setting authorization contract: DeviceId, nonce, action, proposed value, expiry, replay rejection, action/value/device mismatch, success/failure audit, and absence of support-side secret material from the customer installation (implemented in the future Security/Foundation phase).
- OH while Stopped, START after OH, multiple valid OH cycles, Running+OH rejection, and RuntimeAfterOH historical preservation.
- Runs crossing midnight, ending exactly at midnight, starting exactly at midnight, and spanning several service days.
- Persian month and year transitions, all 29/30/31-day month rules, leap Esfand day 30, non-leap Esfand rejection, and Farvardin boundary.
- Period clipping for runs beginning before and ending after daily, monthly, and arbitrary ranges.
- Wizard-derived DataStartDate at first Persian month day `00:00`, distinct per-Unit Baselines at the same instant, and sequential-entry boundary behavior.
- Baseline Stopped, StoppedAfterOh, and Running; missing/duplicate Baseline; valid Running Baseline without pre-baseline open-run provenance; state mismatch.
- Wizard Total Runtime and Runtime After OH carried as authoritative facts without inventing a pre-baseline Physical/ESD decomposition.
- Same-Unit duplicate timestamp rejection and different-Unit same-minute isolation.
- Rasht/Ramsar Station and Unit isolation with no policy leakage.
- Multiple Runtime policy, configuration, Baseline, Event Chain, and Calculation versions; current-setting open recalculation and exact replay of old finalized snapshots.
- Physical/adjusted component invariant, ESD exclusion from ServiceDay/LongestRun, and positive-overlap ServiceDay rule.
- Shadow comparison classifications, including invalid comparison inputs and approved expected differences.
- Culture/timezone independence and repeatability across machines using the canonical local-minute model.
- Integral-minute authority and exact two-decimal hours presentation, including `90 -> 1.50 h` and `125 -> 2.08 h`, with no rounded-value feedback.

Golden fixtures must state every input Event, Baseline, boundary, policy version, expected component in minutes, resulting state, and provenance. Any fixture relying on an unresolved Station value remains pending and cannot be converted into an assumed production expectation.

## 14. Decision table

| Decision | Approved Rule | Reason | Implementation Impact |
|---|---|---|---|
| Runtime authority | Trusted Baseline plus complete validated Event Chain only | Event architecture makes Events authoritative | Engine accepts validated chain; never queries ST/RPM for state |
| Invalid Events | Reject; do not repair, skip, reorder, or synthesize | Validation belongs to Event subsystem | Structured failure and no projection |
| Physical Runtime | Elapsed minutes in valid START-to-NSD/ESD or valid open Running interval | Separates physical operation from business adjustment | Build and clip physical intervals |
| Period interval | Half-open `[start, end)` at minute precision | Eliminates boundary double-counting | Start included; end excluded everywhere |
| Event vocabulary | Exactly START, NSD, ESD, OH; no generic STOP | Preserves approved Event state machine | Closing Events are NSD or ESD only |
| DataStartDate | First day of Wizard-selected Persian month at local `00:00`; starts software responsibility | Data entry boundary differs from Runtime state | Apply sequential-entry rules from this boundary |
| Runtime Baseline | Separate per configured Unit, effective exactly at DataStartDate | Each Unit needs trusted state when responsibility begins | Wizard captures Total Runtime, Runtime After OH, and Initial Operational State |
| Pre-baseline Runtime | Wizard cumulative values are authoritative; no reconstruction or invented decomposition | Software is not responsible before DataStartDate | Carry starting facts without inventing history |
| Running Baseline | Accrual starts at DataStartDate; no historical START or pre-baseline provenance required | Responsibility boundary supplies observable start | Later NSD/ESD closes interval; first LongestRun clips to boundary |
| Open run | Clip at PeriodEnd and preserve Running/software-owned start | A valid chain may legitimately end Running | No synthetic shutdown Event; continuing state returned |
| Adjusted Runtime | Physical Runtime plus ESD Adjustment | Source architecture separates metrics | Output components and validate sum |
| ESD validity | Apply exactly once only on accepted `Running -> ESD` | Prevents adjustment while stopped | State acceptance precedes contribution |
| ESD period ownership | Period containing ESD timestamp | Deterministic boundary behavior | Use half-open Event timestamp test |
| ESD ownership | One common Station/deployment value for every configured Unit | Approved deployment-wide setting | No per-Unit configuration, precedence, or fallback |
| ESD initial value | Entered in Wizard; zero valid; no Station default invented | Configuration is deployment evidence | Missing configuration fails |
| Open-period ESD change | Recalculate every valid in-period ESD with the current value | Approved current-setting semantics | Do not select an historical value by Event time |
| Finalized-period ESD change | Preserve snapshot and original configuration evidence | Locked output is immutable evidence | Later changes cannot reinterpret or rewrite it |
| ESD authorization | Post-Wizard change requires one-time action-scoped Support Authorization and audit | ESD is sensitive | Future security binds DeviceId, nonce, action, value, expiry; no embedded support secret |
| OH effect | Reset only RuntimeAfterOH; cumulative Runtime unchanged | Approved Event architecture semantics | OH changes carried after-OH state only |
| OH validity | Only `Stopped -> StoppedAfterOh` | State machine rejects Running+OH and duplicate OH | Invalid OH rejects calculation |
| Historical invalid OH | Reconciliation/migration issue; target rules remain strict | Legacy invalidity cannot redefine target domain | Resolve outside Runtime projection |
| RuntimeAfterOH | Adjusted cumulative minutes since latest valid OH, value as of PeriodEnd | Includes post-reset physical and valid ESD effects | Carry state across period boundary |
| LongestRun | Longest continuous physical interval intersected with period | Adjustment is not physical operation | Clip at report boundaries, not midnight |
| Service day | Local midnight-to-midnight Persian day with positive physical overlap | Approved architecture boundary and meaning | ESD alone never creates a day |
| Station boundary variation | Default `00:00`; non-midnight needs domain approval | No Station-specific value is known | Reject unapproved configuration |
| Persian calendar | Central deterministic converter and calendar addition | Prevents culture and boundary errors | Test Esfand/year transitions |
| Internal chronology | Canonical integer local epoch-minute | Supports exact arithmetic and sorting | Never use display text or current UTC offset |
| Daily period | Persian midnight to next Persian midnight | Matches service-day convention | Calendar-derived exclusive end |
| Monthly period | First Persian month midnight to next month midnight | Handles variable Persian months | No fixed day counts |
| Arbitrary range | Explicit normalized half-open minute boundaries | Avoids inclusive-end ambiguity | UI normalizes before engine call |
| Duplicate timestamp | Same Unit/same minute is invalid | Ordering would be ambiguous | Unique constraint plus rejection |
| Missing shutdown | Valid open run only when state replay proves Running | Open state is distinct from corruption | Clip, preserve state, never repair |
| Output precision/display | Authority is integral minutes; reports show hours with exactly two decimals | Prevents rounding from changing truth | Presentation conversion only; never feed rounded hours back |
| Open projection persistence | Calculate on demand; do not persist as authoritative truth | Current Events/configuration affect next result | Future cache is disposable and non-authoritative |
| Finalized persistence | Store Runtime in immutable report snapshot with full evidence | Locked reports must remain reproducible | Later Events/configuration do not rewrite it |
| Reproducibility | Record Event Chain, policy, configuration, Baseline, calculation, calendar, and timestamp versions | Results must be auditable | Versioned trace accompanies every result |
| Legacy comparison | Same Unit, period, Event boundary; classify every difference | Prevents invalid comparisons and hidden defects | Unexplained differences block activation |

## Approval gate

The Runtime decisions required for Phase 4.2 are approved: DataStartDate responsibility boundary, per-Unit Wizard Baseline meaning, Running Baseline behavior, common deployment ESD ownership, current-setting recalculation for open periods, immutable finalized ESD evidence, on-demand live projection, and integral-minute/two-decimal-hour presentation.

Only these genuine follow-up details remain unresolved at the boundary where they are needed:

- allowed minimum/maximum ESD Adjustment and whether Wizard/Settings input is entered as minutes or another exactly convertible unit;
- protected correction workflow and authorization/audit requirements for changing a trusted Runtime Baseline after Wizard completion;
- whether a Station-specific non-midnight service-day boundary will ever be required;
- detailed cryptographic protocol, support code-generation tool, secure key custody, request transport, and support process for sensitive ESD changes, owned by the future Security/Foundation phase.

Phase 4.2 may implement the approved projection semantics without waiting for speculative boundary variation or the later support-security mechanism, provided it does not expose an unauthorized post-Wizard ESD-change path and does not guess unresolved validation or security details. Any newly discovered business behavior requires a versioned amendment; it must not be inferred from legacy code.
