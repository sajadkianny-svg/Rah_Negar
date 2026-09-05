# Phase 9.5C4 Pilot Confirmed-Close Defect Fix Report

Status: **PHASE 9.5C4 FIX COMPLETE — MANUAL REQUALIFICATION REQUIRED**

**PRODUCTION CUTOVER IS NOT AUTHORIZED.** This focused change does not change
production authority, migrate production data, or perform cutover. Legacy
remains authoritative; Target remains inactive and routing-disabled.

## Scope

This report covers only the Pilot form close guard in
`UI/Composition/Pilot/FrmLivePilot.cs`, its directly related session-close
regression tests, and the Phase 9.5 manual qualification records. No unrelated
UI or repository area was audited.

## Observed manual behavior

After the Phase 9.5C3 fix, manual qualification confirmed the Cancel/No path:
the first X showed the unfinished-session warning, Cancel/No kept Pilot open,
and the next X showed the warning again. A second defect was then observed for
an active/incomplete Pilot session:

1. The operator clicked X.
2. The unfinished-session warning appeared.
3. The operator selected YES.
4. Pilot did not close.
5. A second X produced no warning and closed Pilot.
6. Main Form appeared and Legacy authority was unchanged.

This confirmed-close defect means MQ-08 is **NOT PASS**. MQ-08 remains
**FAIL / requalification required** until the C4 manual procedure is completed.

## Exact root cause

The C3 YES branch called `KeepOpenAfterCloseAttempt(e)`, which set
`e.Cancel = true`, and then started `StopThenCloseAsync()`. The asynchronous
method stopped the session, armed `_closingAfterStop`, and called `Close()`.

With the existing in-memory Pilot stop path completing synchronously, that
`Close()` call was re-entrant: it occurred while the original `FormClosing`
event was still being processed. The original event had already been cancelled,
and WinForms did not complete the nested close as the current close. The form
therefore remained open while `_closingAfterStop` stayed true. The next user X
then entered `OnFormClosing`, saw `_closingAfterStop`, bypassed the unfinished
session guard, and closed without a second warning.

In short, the YES decision was applied to a future close event rather than the
exact event that had been confirmed. The C3 `DialogResult.None` reset correctly
fixed the Cancel/No stale-modal-result defect, but it did not fix this separate
re-entrant YES path.

## Implementation change

For an active/incomplete session, `OnFormClosing` now behaves deterministically:

- operation in progress or NO/CANCEL: set `e.Cancel = true` and restore
  `DialogResult.None`;
- YES: execute the existing explicit stop semantics synchronously in the current
  close callback, then call the base close handling without setting
  `e.Cancel`;
- completed/stopped/created/terminal sessions: retain normal close behavior.

The YES path no longer arms `_closingAfterStop`, starts a deferred close, or
causes a second `FormClosing` event. It does not complete the session. The
existing warning explicitly asks whether the Pilot session should be stopped
and the window closed, so the existing stop semantics are preserved. No
production authority or Target state is reachable from this form.

## Close behavior matrix

| State / decision | Before C4 | After C4 |
|---|---|---|
| Active/incomplete + NO/CANCEL | C3 reset kept form open and re-warning worked | Current event cancelled; form stays open; every next X warns |
| Active/incomplete + YES | Current event cancelled; deferred/re-entrant close could leave form open; later X bypassed guard | Existing explicit stop completes in current callback; current close succeeds exactly once |
| Active/incomplete + second X after YES | Bypassed guard and closed without warning | No second X is needed; no second warning is generated |
| Active/incomplete + YES lifecycle | Could leave a stale close bypass state; no deterministic first-close result | Becomes `Stopped` under the existing explicit stop semantics, never `Completed` |
| Completed | Normal close, no unfinished warning | Unchanged: normal close, no warning |
| Stopped | Normal close, no unfinished warning | Unchanged: normal close, no warning |
| New Pilot form | Could inherit behavior only through a form instance's stale flag | Fresh instance has no confirmed-close bypass state |

## Files changed

- `UI/Composition/Pilot/FrmLivePilot.cs`
- `Rah_Negar.Tests/Pilot/LivePilotPhase93Tests.cs`
- `docs/phase9.5c-manual-qualification-results.md`
- `docs/phase9.5-manual-qualification-runbook.md`
- `docs/phase9.5c4-pilot-confirmed-close-fix-report.md`

## Focused regression tests

Command:

```powershell
dotnet test Rah_Negar.Tests\Rah_Negar.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~LivePilotPhase93Tests" --logger "console;verbosity=normal"
```

The focused suite passed **23/23 tests** with 0 failures and 0 skips. The
preserved C3 coverage verifies first Cancel, repeated Cancel, completed close,
stopped close, and authority invariants. C4 coverage verifies:

- active session + first close + Cancel stays open;
- the next close warns again;
- active session + first close + YES closes on that same attempt;
- exactly one close attempt and one warning, with no second X;
- YES preserves the existing explicit stop semantics without completion;
- Legacy authority and Target inactivity remain unchanged; and
- a fresh Pilot form has no stale confirmed-close state.

## Full validation

Full validation completed successfully:

- `dotnet build Rah_Negar.sln -c Release`: **PASS** — 0 errors, 6 NU1701
  compatibility warnings
- `dotnet test Rah_Negar.sln -c Release`: **PASS** — 695 passed, 0 failed,
  0 skipped, 695 total
- `git diff --check`: **PASS**

No production database, production authority, migration, or cutover action was
performed.

## Manual requalification — Rasht and Ramsar

Run MQ-08 only, with a fresh isolated fixture for each station. Do not resume
other manual qualification items. Do not infer PASS from the automated tests.

### Test A — Cancel/No repeated-warning path

For Rasht, then repeat from a fresh fixture for Ramsar:

1. Launch the qualification station.
2. Log in.
3. Enter Pilot.
4. Start read-only observation.
5. While the session is incomplete/in review state, click X.
6. Choose Cancel/No.
7. Verify Pilot remains open.
8. Click X again.
9. Verify the warning appears again.

Repeat the cancel/X cycle at least three more times. Every attempt must warn,
and every cancellation must keep Pilot open. Capture the active state, warning,
and repeated-cancellation evidence.

### Test B — Confirmed-close path

For Rasht, then repeat from a fresh fixture for Ramsar:

1. With an incomplete Pilot session, click X.
2. Choose Yes.
3. Verify Pilot closes immediately on this same attempt.
4. Verify Main Form appears.
5. Verify no second X click was required.
6. Verify Legacy authority remained authoritative.
7. Verify no automatic Target activation occurred.
8. Re-enter Pilot and verify no stale confirmed-close state remains.

Also verify no hidden completion, no unintended stop beyond the existing
explicit YES stop semantics, no database mutation, and no whole-application
exit. Capture station, UTC time, Release binary hash, process state,
before/after fixture and copied-database hashes, screenshots, and a sanitized
log. Record `PASS` only after human review satisfies every expectation;
otherwise record `FAIL`.

Historical sequence: original defect → C3 fix → Cancel/No requalification
success → confirmed-close defect discovery → C4 fix pending manual
requalification.

## Authority-state verification

For both stations and both tests, explicitly verify:

- Legacy remains the visible and authoritative operational source;
- the Pilot surface remains read-only;
- no Target activation or routing occurs;
- no production database or schema is changed; and
- Main Form returns without whole-application shutdown after confirmed close.

**PRODUCTION CUTOVER IS NOT AUTHORIZED.**
