# Phase 4.4 — Runtime Legacy Adapter Design Report

**Project:** Rah_Negar  
**Status:** Audit and contract design complete  
**Production implementation:** None  
**Database/SQL adapter:** None

## 1. Audit result

The active legacy Runtime calculation is `EventRuntimeCalculationService.Calculate`, which delegates to `CalculateLegacyCore`. `EventReportEngineService.BuildEventReport` supplies period Events, inferred period-start state, Wizard base Runtime values, and current ESD settings, then returns `EventReportResult` to the existing Reporting flow.

The calculator uses START, NSD, ESD, and OH Events; it does not use DailyData or hourly observations. It accumulates hour-based `double` outputs. ESD extra is added to Runtime and RuntimeAfterOH for every recognized ESD when enabled and positive, even when no run is open. OH closes an open legacy run and resets RuntimeAfterOH. Open runs close in memory at the exclusive report boundary.

The legacy output provides cumulative/composite `RuntimeHours`, RuntimeAfterOH, period ESD extra, LongestRun, Service Days, and Event counts. It does not directly provide same-scope period Physical Runtime, period Adjusted Runtime, Final State, canonical identity/boundaries, or an Event source revision. Those gaps prevent a safe production adapter today and are documented in `runtime-legacy-adapter-specification.md`.

No legacy file was modified during the audit.

## 2. Design delivered

The new read-only boundary consists of:

- `ILegacyRuntimeAdapter`: contract for capturing one Unit/period/Event-boundary legacy snapshot;
- `LegacyRuntimeSnapshot`: nullable raw hour-based evidence model;
- `LegacyRuntimeSnapshotNormalizer`: strict identity/period/boundary validation and exact integral-minute conversion.

There is no adapter implementation, SQL, database connection, service registration, production call site, or change to existing calculation behavior.

## 3. Files created

- `Application/Runtime/LegacyAdapter/ILegacyRuntimeAdapter.cs`
- `Application/Runtime/LegacyAdapter/LegacyRuntimeSnapshot.cs`
- `Application/Runtime/LegacyAdapter/LegacyRuntimeSnapshotNormalizer.cs`
- `Rah_Negar.Tests/Runtime/LegacyRuntimeAdapterContractTests.cs`
- `docs/runtime-legacy-adapter-specification.md`
- `docs/phase4-runtime-legacy-adapter-report.md`

## 4. Test coverage and results

Contract tests cover:

- raw legacy snapshot creation;
- exact hour-to-integral-minute normalization;
- failure on missing comparison fields;
- Station/Unit identity mismatch rejection;
- period mismatch rejection;
- rejection of rounded display hours that cannot prove an integral-minute value.

Final verification:

- Whole-solution build: passed, 0 errors.
- Automated tests: 115 passed, 0 failed, 0 skipped.
- Existing warnings: `NU1701` compatibility warnings for OpenTK/OpenTK.GLControl/SkiaSharp Windows Forms packages.
- Environment warning: `NU1900` because NuGet vulnerability metadata was unavailable in the restricted/offline environment.
- Production database access: none.

## 5. Limitations

1. No real legacy snapshot is read because no production or SQL implementation is authorized.
2. The current legacy result lacks required normalized fields; nulls intentionally block unsafe comparison.
3. Canonical Station/Unit identity and Event boundary version generation are not implemented.
4. Legacy `double` hours are accepted only when they resolve exactly to integral minutes; display-only values are rejected.
5. No Runtime UI, Reporting, startup, persistence, or shadow batch orchestration is included.
6. No conclusions about real Rasht/Ramsar numeric equivalence are claimed without copy-based fixtures.

## 6. Future implementation plan

1. Approve a non-production, read-only capture harness over representative database copies.
2. Establish consistent Event boundary markers and canonical Station/Unit mappings.
3. Characterize missing period Physical Runtime and Final State without changing legacy behavior.
4. Implement `ILegacyRuntimeAdapter` against the approved capture source, not SQL within the adapter.
5. Normalize complete evidence and compare through Phase 4.3 `RuntimeComparisonService`.
6. Classify every difference with an approved rule/defect reference; unexplained differences remain `NewEngineDefect`.
7. Keep all work isolated until a separate integration/cutover approval.

## 7. Safety verification

- Audit was read-only.
- Legacy Runtime files are unchanged.
- Phase 4.2 and Phase 4.3 calculation/comparison behavior is unchanged.
- No production database was opened.
- No SQL adapter or schema change was created.
- Runtime UI and Reporting are unchanged.
- Existing calculation paths and production startup are unchanged.
