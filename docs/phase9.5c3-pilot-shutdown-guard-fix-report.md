# Phase 9.5C3 Pilot Shutdown Guard Defect Fix Report

Status: **PHASE 9.5C3 FIX COMPLETE — MANUAL REQUALIFICATION REQUIRED**

Production cutover is not authorized. This focused change does not change
authority, migrate production data, or perform production cutover. Legacy
remains authoritative.

## Observed manual defect

The original manual qualification observed this exact failure in an active,
incomplete Pilot session:

1. The operator clicked the Pilot form X.
2. The unfinished-session warning appeared.
3. The operator declined/cancelled closing; the Pilot form remained open.
4. The operator clicked X again.
5. The second attempt produced no warning, closed the Pilot form, returned to
   Main Form, and did not exit the application.
6. Legacy authority remained unchanged.

This is recorded as the original manual qualification **FAIL** for MQ-08. It
is not being marked PASS automatically.

## Root cause

`FrmLivePilot.OnFormClosing` set `FormClosingEventArgs.Cancel = true` when the
operator declined the warning, but it did not clear the modal form's
`DialogResult`. In the `ShowDialog` flow, the X close path can leave the
implicit `DialogResult.Cancel` on the form even though the close was rejected.
That stale non-`None` modal result allowed the next X attempt to bypass the
unfinished-session guard. Close-attempt state was therefore confused with the
session lifecycle state; declining a close did not stop the session, but it
left the modal result looking resolved.

## Files changed

- `UI/Composition/Pilot/FrmLivePilot.cs`
- `Properties/AssemblyInfo.cs` (test visibility for the focused UI regression)
- `Rah_Negar.Tests/Pilot/LivePilotPhase93Tests.cs`
- `docs/phase9.5c-manual-qualification-results.md`
- `docs/phase9.5-manual-qualification-runbook.md`
- `docs/phase9.5c3-pilot-shutdown-guard-fix-report.md`

## Fix summary

`FrmLivePilot` now uses one close-cancellation path that sets
`e.Cancel = true` and restores `DialogResult.None`. It is used for an active
operation, a declined unfinished-session warning, and the confirmed
stop-then-close handoff. The session lifecycle is not changed by a declined
close. Completed and stopped sessions continue through normal close handling;
the explicit return path remains separate and Legacy authority is unchanged.

## Focused regression tests

Command:

```powershell
dotnet test Rah_Negar.Tests/Rah_Negar.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~LivePilotPhase93Tests"
```

Result: **18 passed, 0 failed, 0 skipped, 18 total**. This includes seven new
tests covering first guard, first cancellation/open state, second guard,
repeated cancellations, completed close, stopped close, and authority
invariance. The modal regression drives the form on an STA thread with a
deterministic No response.

## Full validation

- `dotnet build Rah_Negar.sln -c Release`: **PASS** — 0 errors, 12 NU1701
  compatibility warnings
- `dotnet test Rah_Negar.sln -c Release`: **PASS** — 690 passed, 0 failed,
  0 skipped, 690 total
- `git diff --check`: **PASS**

## Exact manual requalification steps

Requalify MQ-08 only; do not resume other manual items.

1. From the repository root, prepare a fresh isolated qualification fixture
   and build Release output using the commands in the runbook.
2. Launch Rasht with `Qualification/launch-qualification.ps1`, enter Pilot
   explicitly, confirm read-only mode, and start observation. Repeat the full
   scenario with a fresh Ramsar fixture.
3. Wait until the Pilot session is active/incomplete and the review state is
   visible. Record the station, UTC time, process state, and initial copied
   database hash.
4. Click the Pilot form X. Verify the unfinished-session warning appears.
5. Select No/cancel. Verify the Pilot form remains open and the session remains
   incomplete; no stop, completion, authority transition, or data mutation may
   occur.
6. Click X again. Verify the warning appears again. Cancel it. Repeat this
   cycle at least three more times and verify every attempt warns and every
   cancellation keeps Pilot open.
7. Use the explicit allowed stop/close path. Verify deterministic return to
   Main Form, no whole-application exit, Legacy remains authoritative, and the
   before/after fixture/copied-database hashes are unchanged.
8. Capture screenshots and a sanitized log for the active state, each warning,
   the repeated cancellation state, and final return. Record PASS or FAIL only
   after human review. Do not infer manual PASS from these automated tests.

Manual disposition: **READY FOR MANUAL REQUALIFICATION**. Do not mark PASS
automatically.

**PRODUCTION CUTOVER IS NOT AUTHORIZED.**
