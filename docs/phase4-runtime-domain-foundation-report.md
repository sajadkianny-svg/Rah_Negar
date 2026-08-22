# Phase 4.1 — Runtime Engine Domain Foundation Report

**Project:** RahNegar modernization  
**Status:** Complete as an isolated domain and contract foundation  
**Production activation:** None

## 1. Scope

Phase 4.1 establishes typed Runtime models, policy inputs, application contracts, and a deliberately limited calculation boundary. It does not replace or call the legacy Runtime implementation, alter current calculations, query a database, register a migration, connect to WinForms, or integrate with Reporting.

Runtime remains a projection over a trusted baseline and a complete, already validated Event chain. The foundation does not repair Event history or independently declare an invalid chain valid.

## 2. Design decisions

### Metric separation

`RuntimeProjection` exposes physical runtime, ESD adjustment, adjusted runtime, runtime after OH, service-day count, and longest run as distinct values. Period and cumulative totals are separate. Adjusted values are derived sums, preventing ESD adjustment from being represented as physical operating time.

### Explicit operational state

`UnitOperationalState` contains `Stopped`, `Running`, and `StoppedAfterOh`. `RuntimeState` records the state at an effective chronological minute, cumulative values, post-OH runtime, and an optional open-run start. A baseline adds version and provenance rather than inventing a historical START Event.

### Validated Event-chain input

`ValidatedEventChain` records Station, Unit, ordered normalized Events, initial state, resulting state, validity, and validation errors. `RuntimeCalculatorFoundation` rejects an invalid chain, identity mismatch, baseline-state mismatch, and invalid calculation period before creating any projection.

Phase 4.1 does not replay transitions to override the Event authority. The supplied chain validity and state boundary are treated as inputs from the approved Event validation layer.

### Policy neutrality and versioning

`RuntimeCalculationPolicy` groups versioned placeholders for ESD adjustment, OH handling, and service-day boundary. No production adjustment duration, station rule, calendar setting, or selected OH policy is hard-coded. Tests use explicit non-production placeholder policies.

### Safe calculation boundary

The only completed calculation is the unambiguous empty-chain case: period physical runtime, adjustment, service days, and longest run are zero; cumulative and post-OH values remain those of the trusted baseline. A non-empty chain fails explicitly with `runtime.projection.nonempty-not-implemented`. This prevents Phase 4.1 from returning plausible but incomplete production totals before time clipping and policy rules are approved and implemented.

## 3. Created files

### Core Runtime

- `Core/Runtime/UnitOperationalState.cs`
- `Core/Runtime/RuntimeState.cs`
- `Core/Runtime/RuntimeProjection.cs`
- `Core/Runtime/RuntimeCalculationResult.cs`
- `Core/Runtime/RuntimePolicies.cs`

### Application Runtime

- `Application/Runtime/IRuntimeCalculator.cs`
- `Application/Runtime/IRuntimeProjectionRepository.cs`
- `Application/Runtime/IRuntimePolicyProvider.cs`
- `Application/Runtime/RuntimeCalculatorFoundation.cs`

### Tests

- `Rah_Negar.Tests/Runtime/RuntimeDomainFoundationTests.cs`

No existing Runtime, Event, UI, Reporting, database, migration, or composition file was modified.

## 4. Tests

The new unit tests cover:

- creation of separated physical and adjustment projection metrics;
- preservation of validated-chain initial/resulting state input;
- rejection of an invalid Event chain;
- rejection of a baseline/chain state mismatch;
- unchanged-baseline and zero-period behavior for an empty chain;
- explicit rejection of non-empty calculation until the projection engine is implemented.

Verification result:

- Solution build: passed, 0 errors.
- Automated tests: 82 passed, 0 failed, 0 skipped.
- Warnings: six pre-existing `NU1701` compatibility warnings involving OpenTK and SkiaSharp Windows Forms packages.
- Database access by new tests: none.

## 5. Unresolved Runtime policy and implementation decisions

The following must be approved or supplied before implementing non-empty projections:

1. Effective ESD adjustment values for Rasht and Ramsar, including Unit scope, enabling conditions, effective dates, and version history.
2. Confirmation of the approved OH policy representation and whether any historical exception requires explicit migration treatment. The target architecture states that valid OH resets only `RuntimeAfterOH`.
3. Authoritative local service-day boundary configuration and Persian calendar conversion/version contract.
4. Exact requested-period inclusion convention and chronological minute representation at start/end boundaries.
5. Rules for an open run at projection end and a baseline that begins in `Running` state, including required `OpenRunStartedAtMinute` provenance.
6. How physical duration before the report range contributes to cumulative and post-OH values without contributing to period values.
7. Application of ESD adjustment to period totals at exact range boundaries.
8. Service-day enumeration across Persian midnight, leap/non-leap Esfand, and runs spanning multiple days.
9. Longest-run clipping where a run begins before or ends after the reporting period.
10. Persistence authority for projections: transient calculation versus cached/versioned results and invalidation rules.
11. Calculation-policy lookup rules when policy changes within a requested range.
12. Trace/provenance requirements linking every adjustment to Event identity and configuration version.

These items must not be inferred from current legacy code without evidence and domain approval.

## 6. Isolation and rollback

The new classes are not referenced by production startup, UI, legacy Runtime, Event commands, or Reporting. The repository contracts have no implementation and no database connection. Rollback consists of removing the Phase 4.1 Core, Application, test, and report files; no schema or data recovery is required.
