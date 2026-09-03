# Phase 9.3 Controlled Live Pilot Integration — Final Audit and Completion Report

Status: **COMPLETE** for the scoped Phase 9.3 implementation and automated validation.

Date: 2026-09-03  
Branch: `phase9-operational-readiness`  
Audited commits: `bf6fdf2` (Phase 9.3A), `2348cd0` (Phase 9.3B)

## Objective and scope

Phase 9.3 integrates the previously isolated operational Pilot into the existing
Rasht/Ramsar WinForms application as an explicitly invoked, read-only observation
surface. The audit covered the completed 9.3A production implementation, the 9.3B
targeted tests, current source wiring, package declarations, and the required
Release build, full test suite, and diff hygiene checks.

The scope is observation only. It does not include production activation, authority
cutover, migration, schema changes, RBAC, Support identity, or new production
features. Legacy remains the authoritative application throughout.

## Architectural boundaries

The live path is isolated across these layers:

```text
FrmMain explicit link
  -> confirmation dialog
  -> LivePilotCompositionRoot
  -> read-only SQLite preflight
  -> LiveSqlitePilotReadModels
  -> five live observers
  -> existing Phase 9.2 operational coordinator
  -> FrmLivePilot / PilotDashboardControl
```

Normal startup remains `Program.cs -> FrmStartup`; it does not construct, compose,
or launch the Pilot. The legacy main window remains visible as the owner of the
modal Pilot window and is restored after Pilot closure. The composition root exposes
no production executor, migration executor, settings writer, event writer, ESD
mutation, or authority-switch capability.

## Files added or modified

Phase 9.3A (`bf6fdf2`) added:

- `Application/Pilot/Live/LivePilotContracts.cs`
- `Application/Pilot/Live/LivePilotObservers.cs`
- `Application/Pilot/Live/LivePilotOperatorSession.cs`
- `Infrastructure/Database/Readiness/PilotReadOnlySqliteConnectionFactory.cs`
- `Infrastructure/Pilot/LivePilotReadOnlyPreflight.cs`
- `Infrastructure/Pilot/LiveSqlitePilotReadModels.cs`
- `UI/Composition/Pilot/FrmLivePilot.cs`
- `UI/Composition/Pilot/LivePilotCompositionRoot.cs`

Phase 9.3A modified:

- `UI/Forms/FrmMain.cs` — explicit footer entry and confirmation flow.
- `UI/Pilot/PilotDashboardControl.cs` — live read-only state presentation.
- `Application/Reporting/Export/DeterministicReportFileNamePolicy.cs` — live
  export metadata integration support.

Phase 9.3B (`2348cd0`) added:

- `Rah_Negar.Tests/Pilot/LivePilotPhase93Tests.cs` — 10 test methods, 11 executed
  cases.
- `docs/phase9.3b-targeted-validation.md` — supporting targeted-validation evidence.

The 9.3B document was reconciled during this final audit to correct its test-method
and executed-case count. It remains supporting evidence; this report is authoritative.
No production-code correction was required.

## Read-only database strategy

`PilotReadOnlySqliteConnectionFactory` canonicalizes the supplied existing database
path, requires the file to exist, and opens it with `SqliteOpenMode.ReadOnly`, private
cache, pooling disabled, and no write transaction. It creates neither directories nor
databases. Failed opens dispose the connection safely.

`LivePilotReadOnlyPreflight` reads only `sqlite_master`, application settings, and
latest dates through the read-only connection. It checks the required legacy tables,
restricts station scope to Rasht or Ramsar, derives the Persian month boundary using
the existing `PersianCalendar` convention, and returns fixed safe failure codes.
It performs no schema creation, migration, transaction, PRAGMA mutation, INSERT,
UPDATE, or DELETE. Cancellation is checked before opening and throughout async reads.

Every live read model obtains its connection from this factory. The five observers
are metadata/read-model observers; they do not expose connection strings, raw secrets,
write methods, artifact generation, finalization, ESD execution, or session creation.
No schema or database migration was added or changed.

## Live observer integration

`LivePilotCompositionRoot` wires exactly five observers:

1. authentication capability;
2. reporting;
3. runtime/event;
4. protected settings;
5. export metadata.

Each observer validates the legacy-authoritative and target-read-only boundaries,
creates a versioned deterministic fingerprint, and returns `Match` or `Difference`
for the workflow. The surfaced specification versions are:
`auth-fingerprint-v1`, `reporting-fingerprint-v1`,
`runtime-event-fingerprint-v1`, `protected-settings-fingerprint-v1`, and
`export-fingerprint-v1`.

Reporting preserves the intended calculation shape: min/max/average where defined
for main data and sum for daily unique values. Export observation validates metadata
and naming only; it does not render or write an artifact.

## Operator UI integration

The legacy footer contains an explicit `Pilot / فقط خواندنی` link. The link is not
the default application route. Selecting it opens a Persian RTL confirmation that
the surface is experimental and read-only, and that Legacy remains the operational
authority. Only after affirmative confirmation is the live composition attempted.

Pilot startup then requires the operator’s separate Start Observation action. The
Pilot form shows read-only workflow status, comparison result, fingerprint version,
monitoring, rollback-readiness evidence, lifecycle state, and safe completion/stop
messages. It contains no write action. The Return button and form close path stop or
dispose the Pilot session and return the operator to the legacy window.

The form uses DPI scaling, RTL layout, a 980x720 minimum size, and read-only controls.
Automated checks confirm the safety banner and authority messaging are present in
source and rendered control text. Manual Windows DPI/keyboard/RTL qualification is
still an operational follow-up limitation, not claimed as completed here.

## Lifecycle and cancellation behavior

`LivePilotOperatorSession` is explicit and single-attempt. It starts in `Created`,
performs preflight, approval, start, observation, and reaches `ReviewRequired`; the
operator must explicitly Complete or Stop. Terminal states include Completed,
Stopped, Failed, and Disposed. There is no automatic start, retry, timer, polling,
background worker, scheduler, or `Task.Run` path.

Cancellation is propagated through preflight and observer reads. Disposal is
idempotent, cancels the form lifetime token, disposes the session/coordinator, and
prevents subsequent operations. Form close during an active operation is deferred;
the operator is asked to stop before the Pilot window closes. No shutdown action
targets the legacy application or production database.

## Safety and prohibited capabilities

The audited implementation confirms:

- Legacy is authoritative; no authority switch or routing replacement exists.
- Pilot state and controls are read-only.
- SQLite access is strict read-only; no migration or schema mutation is in the live path.
- No background polling or automatic startup exists.
- No RBAC or Support identity was introduced by Phase 9.3.
- Passwords, hashes, salts, recovery material, tokens, private keys, and connection
  strings are not surfaced in Pilot evidence or UI.
- No production command, event write, settings write, ESD mutation, report finalization,
  export artifact generation, rollback execution, or database mutation is available.
- The end-to-end observation test compares the source database metadata before and
  after observation and verifies Legacy authority remains unchanged.

## Test coverage and fixtures

`LivePilotPhase93Tests.cs` contains 10 methods: 9 `[Fact]` methods and one `[Theory]`
with two station rows, for 11 executed Phase 9.3-specific cases. Coverage includes
strict SQLite write rejection, non-mutating preflight, cancellation, all five
observers, deterministic Match/Difference behavior, fingerprint versions, lifecycle,
shutdown/disposal, UI safety labels, explicit invocation/startup boundaries, and
end-to-end source preservation.

The reused operational fixtures cover:

- Rasht: 3 units and `RashtReadOnlyObservation` scope.
- Ramsar: 4 units and `RamsarReadOnlyObservation` scope.

The full fixture suite retains cross-day continuity, event/runtime behavior, exact
24:00 half-open handling, reporting aggregations, and daily unique sums. The Phase
9.3 SQLite fixture is temporary test data, deleted during disposal, and not a user
database.

## Build, test, and diff results

Required commands were run on 2026-09-03:

- `dotnet build Rah_Negar.sln -c Release` — **passed**, 0 errors, 12 warnings.
- `dotnet test Rah_Negar.sln -c Release` — **passed**, 637 passed, 0 failed,
  0 skipped, total 637.
- `git diff --check` — **passed**, no whitespace errors.

The 12 build warnings are NU1701 compatibility warnings for existing dependencies
restored from .NET Framework asset groups for the net8.0-windows7.0 target:
`OpenTK 3.1.0`, `OpenTK.GLControl 3.1.0`, and
`SkiaSharp.Views.WindowsForms 3.119.0`, emitted across the production and test
projects. No compiler warnings occurred, and Phase 9.3 did not alter package versions.

The no-restore vulnerability query reported no vulnerable packages for either
project from the configured sources. Fresh deprecated/outdated queries were blocked
by the environment’s denied access to the user NuGet.Config; the earlier validation
evidence records xUnit 2.9.3 as legacy and identifies newer package versions. No
dependency change is authorized by this audit.

## Findings, warnings, and limitations

No confirmed Phase 9.3 production defect was found. The only confirmed discrepancy
was the 9.3B documentation count, corrected above. No schema, production authority,
or package change was made during this audit.

Remaining limitations are intentionally outside the implementation scope: manual
Windows installation qualification, DPI/RTL/keyboard acceptance at 100/125/150%,
live-environment operational sign-off, durable approved evidence retention, and any
future authority cutover. Package compatibility and redundant SQLite dependency use
remain separate technical-debt reviews. These limitations do not invalidate the
scoped read-only integration completion, but they must be addressed before any
production authority change.

## Authority and readiness decision

**No production authority cutover occurred.** Legacy remains authoritative. Phase
9.3 adds only an explicitly invoked, confirmed, read-only observation surface.

Decision: **READY TO MOVE TO PHASE 9.4**, with Phase 9.4 responsible for the next
separately authorized qualification/governance work. This is not approval to enable
cutover, migration, authority switching, or production mutation. The final audited
Phase 9.3 totals are **637 total test cases** and **11 Phase 9.3-specific executed
cases (10 methods)**.
