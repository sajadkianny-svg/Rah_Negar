# Phase 4.2 — Runtime Projection Engine Core Report

**Project:** Rah_Negar  
**Status:** Complete as an isolated Runtime calculation core  
**Production activation:** None  
**Database access/schema change:** None

## 1. Scope and outcome

Phase 4.2 implements a deterministic Runtime Projection Engine under `Core/Runtime/Calculation`. It consumes only the existing `ValidatedEventChain` contract plus explicit trusted Baseline, period, current ESD configuration, and version metadata supplied through `RuntimeCalculationContext`.

The implementation is isolated. It does not replace the legacy Runtime implementation, register with production startup, change Event/UI/Reporting behavior, access SQLite, persist projections, or infer/repair Events. The Phase 4.1 foundation remains present and unchanged for compatibility; Phase 4.2 uses a separate namespace because its approved Baseline contract must preserve the Wizard-entered cumulative Total Runtime without inventing a historical Physical/ESD decomposition.

## 2. Architecture

```text
ValidatedEventChain + trusted per-Unit Baseline facts
                    + current deployment ESD Adjustment
                    + half-open requested period and versions
                                |
                                v
                    RuntimeCalculationContext
                                |
                                v
                    RuntimeIntervalBuilder
                    - validates input contract
                    - replays approved transitions
                    - emits physical intervals
                    - retains open Running state
                                |
                                v
                       RuntimeCalculator
                    - clips physical intervals
                    - calculates period metrics
                    - replays RuntimeAfterOH
                    - attaches deterministic metadata
                                |
                                v
                    RuntimeCalculationResult
                    - projection or structured failure
```

### Domain contracts

- `RuntimeInterval` represents one half-open physical Running interval. Its end is a valid NSD/ESD minute or the calculation boundary for an open run. It contains no synthetic Event and no generic STOP concept.
- `RuntimeCalculationContext` carries the validated chain, software responsibility/Baseline boundary, trusted Wizard totals, requested period, current ESD Adjustment, and all required versions/timestamp.
- `RuntimeProjection` exposes integral-minute Physical Runtime, ESD Adjustment, Adjusted Runtime, RuntimeAfterOH, LongestRun, cumulative trusted Total Runtime, ServiceDayCount, final state, clipped physical intervals, and reproducibility metadata.
- `RuntimeCalculationResult` returns either a projection or a structured error. Invalid input never produces partial totals.

### Boundary and isolation choices

`Core.Runtime.Calculation.RuntimeProjection` is intentionally separate from the earlier `Core.Runtime.RuntimeProjection`. The earlier record assumes decomposed cumulative Physical and ESD history. The approved Phase 4.2 policy says Wizard Total Runtime is an authoritative starting fact and that a pre-baseline decomposition must not be invented.

The calculator is not wired to `IRuntimeCalculator`, `RuntimeCalculatorFoundation`, any repository, WinForms, Reporting, or startup composition. Production behavior is therefore unchanged.

## 3. Calculation flow

1. Reject a chain not marked validated.
2. Validate period order, Baseline boundary/state, non-negative trusted/configured values, required version metadata, Station/Unit identity, strict chronological ordering, uniqueness, and the supplied Event boundary.
3. Replay only `START`, `NSD`, `ESD`, and `OH` from the Baseline state.
4. A Running Baseline opens software-owned accrual at the Baseline/DataStartDate minute; no earlier START or provenance is required.
5. `START` opens a physical interval. Valid NSD or ESD closes it. A calculation ending Running closes only the in-memory interval at the exclusive calculation boundary and marks it open; it does not create an Event.
6. Reject any transition conflicting with the approved state machine even when a caller incorrectly labels the chain valid. Confirm replayed final state equals the chain’s declared resulting state.
7. Intersect physical intervals with `[PeriodStart, PeriodEnd)`. Sum clipped durations for Physical Runtime and select their maximum for LongestRun.
8. Count each valid ESD timestamp inside the period and multiply by the one current deployment ESD Adjustment. Add this to Physical Runtime for Adjusted Runtime. ESD does not affect intervals, ServiceDayCount, or LongestRun.
9. Replay RuntimeAfterOH from its trusted Baseline fact. Physical Running minutes and valid ESD adjustments accrue; valid OH resets only RuntimeAfterOH to zero.
10. Count distinct local midnight-to-midnight buckets having positive physical overlap. An interval ending exactly at midnight does not count the new day. Cross-midnight runs stay continuous for LongestRun.
11. Carry trusted Baseline Total Runtime forward with all software-owned physical minutes and current-setting ESD adjustments through PeriodEnd.
12. Return exact input metadata: EventChainVersion, BaselineVersion, PolicyVersion, CalculationVersion, and CalculationTimestamp.

All authoritative values use integral minutes. Two-decimal hour display remains a presentation responsibility and is not implemented in this core.

## 4. Created files

### Runtime calculation core

- `Core/Runtime/Calculation/RuntimeInterval.cs`
- `Core/Runtime/Calculation/RuntimeProjection.cs`
- `Core/Runtime/Calculation/RuntimeCalculationContext.cs`
- `Core/Runtime/Calculation/RuntimeCalculationResult.cs`
- `Core/Runtime/Calculation/RuntimeIntervalBuilder.cs`
- `Core/Runtime/Calculation/RuntimeCalculator.cs`

### Tests

- `Rah_Negar.Tests/Runtime/RuntimeProjectionEngineTests.cs`

### Documentation

- `docs/phase4-runtime-projection-engine-report.md`

No legacy Runtime, Event, UI, Reporting, database/schema, project composition, or production startup file was modified by Phase 4.2.

## 5. Deterministic test coverage

The new tests cover:

- START to NSD half-open physical interval;
- START to ESD with exactly one current adjustment;
- multiple runs and physical-only LongestRun;
- Running Baseline accrual from the software responsibility boundary;
- open Running interval clipped without a synthetic Event;
- OH resetting only RuntimeAfterOH while cumulative Total Runtime remains continuous;
- one continuous cross-midnight run;
- positive-overlap ServiceDayCount and exact-midnight exclusion;
- recalculation of an earlier open-period ESD using a changed current setting;
- invalid-chain rejection without a projection;
- immutable finalized-snapshot simulation after the current ESD value changes;
- deterministic projection metadata.

Final verification result:

- Whole-solution build: passed, 0 errors.
- Automated tests: 94 passed, 0 failed, 0 skipped.
- Existing warnings: `NU1701` compatibility warnings for OpenTK/OpenTK.GLControl/SkiaSharp Windows Forms packages.
- Environment warning: `NU1900` because vulnerability metadata at NuGet.org was unavailable in the restricted/offline environment.
- Production database access: none.

## 6. Known limitations and deferred integration

1. The engine is not registered with application dependency injection/startup and does not replace `RuntimeCalculatorFoundation` or legacy Runtime. Activation belongs to a later explicitly approved integration phase.
2. Only the approved local `00:00` service-day boundary is implemented through canonical 1,440-minute day buckets. A future non-midnight Station boundary remains unapproved and is not configurable here.
3. The engine assumes the canonical chronological minute key aligns day boundaries at multiples of 1,440. The future centralized Persian calendar converter must guarantee this documented epoch invariant; Persian parsing/conversion is outside this pure minute-arithmetic core.
4. ESD Adjustment authorization, Settings UI, audit persistence, cryptography, and the support-side code-generation tool belong to future Security/Foundation work. This phase accepts an already authorized current value as input.
5. Allowed maximum ESD Adjustment and Wizard/Settings entry units remain domain/configuration decisions. The core accepts non-negative integral minutes and treats zero as valid.
6. Live projections are not persisted. Finalized report snapshot storage is not implemented; the immutability test retains the earlier immutable projection value and verifies recalculation produces a separate value.
7. Baseline creation/correction workflow and persistence are not implemented. The context consumes trusted per-Unit Wizard facts and does not invent pre-baseline history.
8. The input chain must contain exactly the ordered active Events from the Baseline through, but not including, PeriodEnd. Repository loading and consistent EventChainVersion generation are future application/infrastructure responsibilities.
9. Runtime hour formatting is not implemented in Core; authoritative results remain integral minutes.
10. No production Rasht/Ramsar values, Unit lists, Baselines, or ESD defaults were invented.

## 7. Safety verification

- Legacy Runtime replacement: none.
- Existing Event/UI/Reporting path modification: none.
- Production startup/composition modification: none.
- Database connection or migration: none.
- Event repair/inference: none.
- Event vocabulary: unchanged (`START`, `NSD`, `ESD`, `OH`); no generic STOP Event introduced.
- Projection persistence: none; results are in-memory values only.
