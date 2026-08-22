# Phase 4.3 — Runtime Shadow Comparison Report

**Project:** Rah_Negar  
**Status:** Complete as an isolated, read-only comparison foundation  
**Production activation:** None  
**Legacy Runtime replacement:** None  
**Database access/schema change:** None

## 1. Scope and approach

Phase 4.3 adds a deterministic shadow-comparison layer under `Core/Runtime/Comparison` and a read-only legacy-reader contract under `Application/Runtime/Comparison`. It compares normalized immutable snapshots only. It does not invoke, modify, wrap, or replace legacy Runtime; no production implementation of the reader contract exists.

The comparison boundary requires the same Station, Unit, half-open period, and Event boundary before comparing values. Duration authority is integral minutes. Display strings, localized formatting, rounded two-decimal hours, source labels, and calculation-version labels do not participate in metric equality.

```text
Legacy read-only source (future)        Phase 4.2 Runtime Projection
              |                                      |
              v                                      v
       RuntimeSnapshot                    RuntimeSnapshotNormalizer
              |                                      |
              +--------------+-----------------------+
                             v
                  RuntimeComparisonService
                  - validate input identity
                  - compare exact minute values
                  - compare count and final state
                  - classify with explicit evidence
                             |
                             v
                  RuntimeComparisonResult
```

## 2. Created files

### Core comparison

- `Core/Runtime/Comparison/RuntimeDifferenceCategory.cs`
- `Core/Runtime/Comparison/RuntimeSnapshot.cs`
- `Core/Runtime/Comparison/RuntimeComparisonResult.cs`
- `Core/Runtime/Comparison/RuntimeSnapshotNormalizer.cs`
- `Core/Runtime/Comparison/RuntimeComparisonService.cs`

### Application contract

- `Application/Runtime/Comparison/ILegacyRuntimeReader.cs`

### Tests and fixtures

- `Rah_Negar.Tests/Runtime/RuntimeShadowComparisonFixtures.cs`
- `Rah_Negar.Tests/Runtime/RuntimeShadowComparisonTests.cs`

### Documentation

- `docs/phase4-runtime-shadow-comparison-report.md`

No existing calculation, legacy Runtime, Runtime UI, Reporting, Event, database/schema, or production startup file was modified by Phase 4.3.

## 3. Comparison models and normalization

`RuntimeSnapshot` contains:

- source label;
- Station and Unit identity;
- half-open period start/end minutes;
- Event boundary version;
- Physical Runtime minutes;
- ESD Adjustment minutes;
- Adjusted Runtime minutes;
- RuntimeAfterOH minutes;
- LongestRun minutes;
- ServiceDayCount;
- final operational state;
- source CalculationVersion.

The normalizer maps a Phase 4.2 projection to this shape and provides strict construction for legacy adapters and fixtures. It rejects empty identity/version fields, invalid periods, negative values, sub-minute `TimeSpan` values, and a broken `AdjustedRuntime = PhysicalRuntime + ESDAdjustment` invariant.

Rounded display hours are deliberately absent from `RuntimeSnapshot`. A display such as `2.08 h` cannot replace the authoritative 125-minute value. Source and calculation labels remain evidence but are not compared as Runtime metrics, allowing legacy and new algorithms to use different labels without creating formatting differences.

## 4. Comparison and classification rules

The service first compares these required input boundaries:

1. StationId;
2. UnitId;
3. PeriodStartMinute;
4. PeriodEndMinute;
5. EventBoundaryVersion.

Any mismatch returns `InputMismatch` and stops metric comparison because unlike inputs cannot produce a meaningful shadow result.

For matching inputs, the service compares exactly:

- Physical Runtime;
- ESD Adjustment;
- Adjusted Runtime;
- RuntimeAfterOH;
- LongestRun;
- ServiceDayCount;
- Final State.

Minute/count equality is exact; there is no tolerance. Numeric differences retain invariant-culture legacy value, new-engine value, and `new - legacy` delta. Final State uses exact approved enum identity.

Categories are:

| Category | Rule |
|---|---|
| `Match` | All required inputs and compared values are equal. |
| `ExpectedPolicyDifference` | Values differ and the caller supplies an evidence-backed approved policy reason. |
| `LegacyDefect` | Values differ and the caller supplies evidence identifying a confirmed legacy defect. |
| `NewEngineDefect` | Values differ without an approved alternative disposition; this is the safe default and blocks acceptance. |
| `InputMismatch` | Station, Unit, period, or Event boundary differs; metric comparison is not valid. |

The service never guesses `ExpectedPolicyDifference` or `LegacyDefect`. Both require a nonempty classification reason. A caller cannot force `Match` or `InputMismatch` over actual metric differences. This prevents unexplained divergence from being hidden.

## 5. Legacy reader contract

`ILegacyRuntimeReader` is read-only and returns one normalized `RuntimeSnapshot` for an explicit Station, Unit, half-open period, and Event boundary version. Phase 4.3 supplies no implementation, registration, database connection, SQL, adapter to legacy classes, or production call site.

A future adapter must characterize the legacy source without changing it, normalize authoritative values to integral minutes, and prove it used the requested Event boundary. If legacy exposes only rounded display text for a field, that is a data limitation requiring evidence and disposition; the adapter must not pretend rounded hours are exact minute authority.

## 6. Synthetic fixtures

Fixtures are in-memory only and contain no production Station values, database records, or legacy calls.

| Fixture | Covered behavior | Expected result |
|---|---|---|
| `normal-start-nsd` | One physical START/NSD interval | Match |
| `start-esd` | Physical interval plus common current ESD Adjustment | Match |
| `oh` | Pre-OH run, valid OH reset, post-OH run | Match |
| `running-baseline` | Software-owned Running Baseline clipped to period | Match |
| `cross-midnight` | Continuous run crossing local midnight and two Service Days | Match |
| `intentional-esd-policy` | Synthetic legacy value differs from current-setting target by 20 minutes | ExpectedPolicyDifference with explicit reason |

Matching legacy snapshots are deliberate synthetic copies of independently calculated target metric values with different source/calculation labels. They test the comparison boundary, not legacy algorithm correctness. The intentional fixture changes ESD Adjustment, Adjusted Runtime, and RuntimeAfterOH consistently and verifies exact difference reporting.

## 7. Test results

New unit tests cover:

- exact whole-minute normalization;
- rejection of sub-minute authority;
- rejection of inconsistent adjusted totals;
- display/source/version-label independence;
- input mismatch precedence;
- safe default classification;
- evidence requirements for ExpectedPolicyDifference and LegacyDefect;
- all seven required metric/state comparisons.

New fixture/integration tests cover:

- deterministic matches for START/NSD, START/ESD, OH, Running Baseline, and cross-midnight scenarios;
- explicit expected-policy classification for the intentional ESD difference.

Final verification:

- Whole-solution build: passed, 0 errors.
- Automated tests: 109 passed, 0 failed, 0 skipped.
- Existing warnings: `NU1701` compatibility warnings for OpenTK/OpenTK.GLControl/SkiaSharp Windows Forms packages.
- Environment warning: `NU1900` because NuGet vulnerability metadata was unavailable in the restricted/offline environment.
- Production database access: none.

## 8. Limitations and next steps

1. There is no production `ILegacyRuntimeReader`; therefore no real legacy dataset has been compared.
2. Synthetic legacy snapshots validate comparison mechanics but cannot prove legacy equivalence or identify a real defect.
3. Evidence identifiers are currently represented by a required reason string. A later comparison harness may introduce a typed approval/defect reference without changing metric rules.
4. The comparison result is in-memory and is not persisted, displayed, exported, or connected to Reporting.
5. Batch orchestration, fixture file formats, anonymized Rasht/Ramsar datasets, and comparison reporting are deferred.
6. No tolerance is supported. Any future tolerance proposal would require explicit domain approval and must not replace integral-minute comparison authority.
7. `InputMismatch` covers all non-comparable input boundaries approved for Phase 4.3. Baseline and policy/configuration evidence should be incorporated into a future real-reader harness where available; they must not be inferred.

## 9. Safety verification

- Comparison is read-only.
- Legacy Runtime is unchanged and not invoked by production.
- Phase 4.2 Runtime behavior is unchanged.
- Runtime UI and Reporting are unchanged.
- Event behavior is unchanged.
- Production startup/composition is unchanged.
- No database was opened and no schema was changed.
- No production implementation of `ILegacyRuntimeReader` exists.
