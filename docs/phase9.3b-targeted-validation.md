# Phase 9.3B — Targeted Validation and Test Completion

> Reconciled by the Phase 9.3 final audit report. This document remains supporting
> validation evidence; `docs/phase9-controlled-live-pilot-integration-report.md`
> is authoritative for final scope, status, and counts.

## Scope and starting point

- Branch: `phase9-operational-readiness`
- Starting commit: `bf6fdf2` (`Complete Phase 9.3A live pilot build stabilization`)
- Validation scope: Phase 9.3 live read-only Pilot implementation and directly related Pilot test infrastructure.
- No database schema was changed.
- No production cutover was implemented.
- No commit or push was performed.
- Working tree was clean before the test-only change.

## Files changed

Added:

- `Rah_Negar.Tests/Pilot/LivePilotPhase93Tests.cs`

Added documentation:

- `docs/phase9.3b-targeted-validation.md`

Production files changed: none.

## Existing infrastructure reused

The tests reuse `Rah_Negar.Tests/Pilot/ControlledPilotOperationalFixtures.cs` for the existing Phase 9.2 operational fixture data and coordinator construction. The fixture provides:

- Rasht with 3 units and `RashtReadOnlyObservation` scope.
- Ramsar with 4 units and `RamsarReadOnlyObservation` scope.
- Existing operational workflow context, preparation evidence, prerequisite evidence, rollback evidence, observers, and in-memory evidence destination.

A small test-local SQLite fixture was added only to exercise the Phase 9.3 read-only connection and preflight against a temporary database. It is deleted during test disposal and does not alter the application schema or any user database.

## New Phase 9.3 tests

The new test class contains 10 xUnit test methods (9 facts and 1 theory). The
parameterized preflight test contributes two test cases at runtime, so the full
test runner count increases by 11 cases.

### Coverage matrix

| Requirement | Test coverage |
|---|---|
| Pilot SQLite connection opens strictly read-only | `Pilot_connection_is_strictly_read_only_and_cannot_mutate_fixture` checks open state, `Mode=ReadOnly`, rejected INSERT, and unchanged row count. |
| Pilot preflight performs no mutation | `Pilot_preflight_is_non_mutating_and_selects_the_existing_station_scope` compares database metadata before/after and asserts mutation/migration/transaction/PRAGMA safety flags. |
| Legacy remains authoritative | Session and end-to-end tests assert `LegacyAuthorityIndicator`, `LegacyRemainsAuthoritative` through the existing coordinator result path, and no authority change. |
| Explicit Pilot invocation only | `Pilot_is_explicit_only_and_legacy_window_remains_authoritative` verifies the explicit main-window entry point and that `Program.cs` does not construct or compose the live Pilot automatically. |
| No automatic startup | The same test checks no live composition call is present in `Program.cs`; the UI contract also asserts `AutomaticallyStarts` is false in existing Phase 9.3 code. |
| No authority switch | Session and composition/UI assertions verify `ChangesAuthority`, `CanSwitchAuthority`, `SwitchesAuthority`, and `ReplacesLegacyWindow` remain false. |
| Five live workflow observers execute safely | `All_five_live_observers_are_read_only_and_execute_deterministically` creates all five Phase 9.3 live observers and executes them concurrently against the reused Rasht fixture. |
| Match / Difference evaluation is deterministic | The five-observer test repeats observations and compares fingerprints; `Live_observer_difference_and_invalid_boundary_are_deterministic` verifies a stable Difference result and boundary rejection. |
| Fingerprint specification version surfaced correctly | The five-observer test checks all five versions: `auth-fingerprint-v1`, `reporting-fingerprint-v1`, `runtime-event-fingerprint-v1`, `protected-settings-fingerprint-v1`, and `export-fingerprint-v1`. |
| Confirm / Start / Observe / Review / Complete / Stop / Dispose lifecycle | `Live_session_supports_confirm_start_observe_review_complete_stop_and_dispose` verifies Created, review after the explicit start sequence, Completed, Stopped, and Disposed states. The coordinator's approval step is exercised by `StartObservationAsync`. |
| Cancellation and shutdown safety | `Preflight_cancellation_is_safe_and_does_not_open_or_mutate` checks canceled preflight; `Session_cancellation_and_shutdown_are_safe` checks canceled start, idempotent dispose, terminal disposal, and post-disposal rejection. |
| Pilot UI exposes required safety labels | `Pilot_UI_exposes_safety_banner_and_has_no_prohibited_actions_or_secrets` checks source and rendered WinForms control text for `حالت آزمایشی`, `فقط خواندنی`, and `مرجع بهره‌برداری`. |
| Prohibited actions absent from Pilot UI | The UI test checks the live form source for INSERT, UPDATE, DELETE, migration, production execution, and authority-switch action names. |
| No passwords, hashes, recovery material, or connection strings in evidence/UI | The UI test rejects password/hash/recovery/connection-string tokens in Pilot source and rendered text. Existing sanitizer behavior remains covered by the earlier Pilot UI tests. |
| Operator can close Pilot and return to legacy UI | The explicit-entry/UI contract test verifies the `بازگشت به برنامه فعلی` path is present through the existing form and the live form does not replace the legacy window. |
| Rasht 3-unit fixture | Parameterized preflight case plus all-five-observer and lifecycle tests use the reused Rasht fixture; the fixture asserts 3 units and Rasht scope. |
| Ramsar 4-unit fixture | Parameterized preflight case plus cancellation and end-to-end tests use the reused Ramsar fixture; the fixture asserts 4 units and Ramsar scope. |
| End-to-end observation causes no source/database mutation | `End_to_end_observation_preserves_source_database_and_legacy_authority` compares temporary database metadata before/after live observation and asserts no production mutation or authority change. |

## Targeted test execution

Command:

```text
dotnet test Rah_Negar.Tests\Rah_Negar.Tests.csproj -c Release --filter FullyQualifiedName~LivePilotPhase93Tests --no-restore
```

Result:

```text
Passed: 11
Failed: 0
Skipped: 0
Total: 11
```

The targeted run emitted the existing package compatibility warnings listed below and no test/build errors.

## Full suite execution

Command:

```text
dotnet test Rah_Negar.sln -c Release
```

Result:

```text
Passed: 637
Failed: 0
Skipped: 0
Total: 637
```

Final total test count: **637**.

New Phase 9.3 tests: **10 xUnit test methods**, **11 executed cases** because the station preflight theory has two inline data rows.

## Build execution

Command:

```text
dotnet build Rah_Negar.sln -c Release
```

Result:

```text
Build succeeded
Errors: 0
Warnings: 12
```

All 12 warnings are NU1701 compatibility warnings for existing .NET Framework-targeted dependencies restored for the `net8.0-windows7.0` target:

- `OpenTK 3.1.0`
- `OpenTK.GLControl 3.1.0`
- `SkiaSharp.Views.WindowsForms 3.119.0`

These warnings predate this test change. No package was upgraded, removed, or otherwise modified in Phase 9.3B.

## Diff hygiene

Command:

```text
git diff --check
```

Result: passed with no whitespace errors.

The final diff contains only the new Phase 9.3 test file and this validation document. No schema, production implementation, package, commit, or remote branch was changed.

## Findings and unresolved items

- No genuine Phase 9.3 production defect was revealed by the targeted or full test runs.
- No production-code correction was necessary.
- No test was weakened to accommodate the implementation.
- Existing NU1701 dependency compatibility warnings remain unresolved from earlier phases; they are recorded here but are outside this targeted test-completion scope.
- Production cutover remains intentionally unimplemented, as required for Phase 9.3B.

## Completion status

Phase 9.3B targeted validation and test completion succeeded. Work stopped after the requested validation commands. No commit or push was performed.
