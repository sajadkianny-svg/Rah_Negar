# Phase 9.5C Consolidated Manual Qualification Results

Exact current status: **PHASE 9.5C4 FIX COMPLETE — MANUAL REQUALIFICATION REQUIRED**

**PRODUCTION CUTOVER IS NOT AUTHORIZED.** Legacy remains authoritative. No
production database, migration, restore, Target authority transition, commit,
or push was performed.

## 1. Objective

Execute the Phase 9.5 runbook items that are safe and locally exercisable,
without treating automated tests or historical observations as manual PASS.

## 2. Qualification environment

Branch: `phase9-operational-readiness`; requested continuation point:
`3878253`. Disposable fixtures were prepared at
`Qualification/qualification-data` for Rasht (3 units) and Ramsar (4 units).
No production path or data was used. The available computer-use surface
reported no native desktop apps, so the WinForms launch surface was not
available for manual observation or screenshots.

Preparation command:
`powershell -ExecutionPolicy Bypass -File .\Qualification\prepare-qualification.ps1 -OutputDirectory (Join-Path (Get-Location) 'Qualification\qualification-data')`

Fixture SHA-256 evidence was captured in the operator session for both
`Rasht/db.sys` and `Ramsar/db.sys`; values are intentionally not reproduced
here. No copied qualification-run database was created because the launcher
requires an interactive desktop lifecycle that was unavailable.

## 3. Qualification item inventory and classification

| ID | Gates | Station/scope | Classification | Result | Evidence |
|---|---|---|---|---|---|
| MQ-01 | DB-03, BR-02, BR-03, BR-05, BR-06 | Rasht/Ramsar fixtures | EXECUTABLE NOW | BLOCKED | `Rah_Negar.Tests/TestResults/mq-01.trx`; automated assertions passed, but no manual receipt review/sign-off surface was available |
| MQ-02 | SEC-01..05, SEC-08 | Both fixtures | EXECUTABLE NOW | BLOCKED | `Rah_Negar.Tests/TestResults/mq-02.trx`; automated assertions passed, manual evidence review not completed |
| MQ-03 | MIG-03, MIG-04, RT-01 | Rasht 3 / Ramsar 4 | EXECUTABLE NOW | BLOCKED | `Rah_Negar.Tests/TestResults/mq-03.trx`; automated assertions passed, manual manifest review not completed |
| MQ-04 | MIG-02, MIG-05 | Both fixtures | EXECUTABLE NOW | BLOCKED | `Rah_Negar.Tests/TestResults/mq-04.trx`; automated assertions passed, manual receipt review not completed |
| MQ-05 | AUTH-03, AUTH-04, MIG-06, SEC-05 | Both fixtures | EXECUTABLE NOW | BLOCKED | `Rah_Negar.Tests/TestResults/mq-05.trx`; automated assertions passed, manual JSONL/evidence review not completed |
| MQ-06 | UI-02, UI-06 | Rasht 3 / Ramsar 4 | BLOCKED BY LOCAL TOOLING | BLOCKED | No native desktop app surface; no manual PASS claimed |
| MQ-07 | UI-03, UI-06 | Rasht 3 / Ramsar 4 | BLOCKED BY LOCAL TOOLING | BLOCKED | No native desktop app surface; no manual PASS claimed |
| MQ-08 | UI-04, UI-06 | Rasht 3 / Ramsar 4 | C3 FAIL plus C4 confirmed-close defect | READY FOR MANUAL REQUALIFICATION | C4 fix validated by focused automation; no manual PASS claimed |
| MQ-09 | UI-05, UI-06 | Rasht 3 / Ramsar 4, 100% DPI | BLOCKED BY LOCAL TOOLING | BLOCKED | DPI could not be manually exercised |
| MQ-10 | UI-05, UI-06 | Rasht 3 / Ramsar 4, 125% DPI | BLOCKED BY LOCAL TOOLING | BLOCKED | DPI could not be manually exercised |
| MQ-11 | UI-05, UI-06 | Rasht 3 / Ramsar 4, 150% DPI | BLOCKED BY LOCAL TOOLING | BLOCKED | DPI could not be manually exercised |
| MQ-12 | UI-06 | Rasht 3 / Ramsar 4 | BLOCKED BY LOCAL TOOLING | BLOCKED | Cancel/RTL/traceability UI review could not be exercised |

Current counts: **executed 5; PASS 0; FAIL 0; BLOCKED 12**. The five executed
items are test commands run for qualification support; they are not promoted
to manual PASS because the runbook requires operator evidence review.

Phase 9.5C4 update: MQ-08 remains **FAIL / requalification required** until
the C4 manual steps are completed. The C4 fix is complete and the item is
**READY FOR MANUAL REQUALIFICATION**; no manual PASS is claimed.

## 4. Executed items

All five commands used `dotnet test Rah_Negar.Tests\Rah_Negar.Tests.csproj -c
Release --no-restore` with the runbook filters and TRX logger names. Results:

| Item | Tests | Result |
|---|---:|---|
| MQ-01 | 3 passed | Supporting automation PASS; manual item BLOCKED |
| MQ-02 | 7 passed | Supporting automation PASS; manual item BLOCKED |
| MQ-03 | 7 passed | Supporting automation PASS; manual item BLOCKED |
| MQ-04 | 4 passed | Supporting automation PASS; manual item BLOCKED |
| MQ-05 | 10 passed | Supporting automation PASS; manual item BLOCKED |

No failure was observed. The blocker is evidence capture/review capability,
not a confirmed production defect.

## 5. Rasht and Ramsar results

No new manual Rasht or Ramsar UI result was obtained in Phase 9.5C. The prior
Phase 9.4 functional lifecycle observations remain historical and are not
reused as evidence for MQ-06–MQ-12. No station-specific leakage was observed
in the executed automated fixtures.

## 6. Pilot residual and DPI qualification

MQ-06–MQ-12 are BLOCKED except MQ-08, which is READY FOR MANUAL
REQUALIFICATION. Stop, active cancellation, the C4 active close paths,
keyboard/RTL, field/traceability inspection, and the 100%/125%/150% DPI
lifecycles have not been manually requalified in this phase. No screenshot or
manual PASS is claimed.

## 7. Backup/restore, security/recovery, provisioning, migration, activation

MQ-01–MQ-05 automated suites completed with zero test failures. The required
operator inspection of sanitized receipts/descriptors, isolated launch state,
and evidence-store output was not completed; therefore each manual item is
BLOCKED. No production authorization, restore, migration, or activation was
attempted. Target remains non-authoritative and inactive.

## 8. Failures and blocked items

There were no qualification test failures. The repeated local blocker is the
absence of an available native WinForms interaction/evidence-capture surface.
This is classified as an environment/local-tooling limitation. It was not
silently retried and was not converted into a code defect.

## 9. Evidence references

- `Rah_Negar.Tests/TestResults/mq-01.trx` through `mq-05.trx` — sanitized test-result files.
- `Qualification/qualification-data/Rasht/db.sys` and `Ramsar/db.sys` — disposable fixtures; hashes captured but not printed.
- No screenshots, receipts, raw credentials, private keys, raw SQL, stack traces, or sensitive values were included.

## 10. Production-only pre-cutover evidence

Still outstanding and not attempted: actual production DB identity/hash and
backup/hash; installed station binary hash; real restore rehearsal; real
management authorization; real migration and post-migration integrity receipt;
operator/management GO authorization; and cutover timestamp.

## 11. Gate reconciliation

| Gate group | Pre-qualification | MQ result | Final state | Mandatory before cutover |
|---|---|---|---|---|
| DB-03, BR-02, BR-03, BR-05, BR-06 | Conditional | MQ-01 blocked | BLOCKED | Manual isolated backup/restore evidence plus production evidence where applicable |
| SEC-01..05, SEC-08 | Conditional | MQ-02 blocked | BLOCKED | Manual security/recovery evidence and production authorization evidence |
| MIG-02..04, MIG-05 | Conditional | MQ-03/MQ-04 blocked | BLOCKED | Manual provisioning/migration evidence plus production migration evidence |
| AUTH-03, AUTH-04, MIG-06 | Conditional | MQ-05 blocked | BLOCKED | Manual activation-readiness evidence; never actual authority transition |
| UI-02..06 | Conditional | MQ-06..MQ-12 blocked | BLOCKED | Complete station lifecycle, residual, and DPI evidence |
| DB-01, DB-02, DB-04, DB-05, DB-09, RT-01, RT-08, REP-01, REP-05, BR-04, OPS-01 | Conditional/production-only | Not executed | CONDITIONAL | Real pre-cutover evidence, approvals, and hold-point observations |

## 12. Remaining mandatory manual items

MQ-01–MQ-12 remain mandatory for closure because no item has a manual PASS
package in this run. The highest-priority unblock is a native desktop session
with screenshot and operator/reviewer capture capability.

## 13. Explicit authority state

Legacy remains authoritative. Target remains non-authoritative, non-activated,
and routing-disabled. No authority state changed.

**PRODUCTION CUTOVER IS NOT AUTHORIZED.**

## 14. Validation

Phase 9.5C4 validation: the focused Pilot suite passed **23/23 tests** with
0 failures and 0 skips. `dotnet build Rah_Negar.sln -c Release` passed with
0 errors and 6 NU1701 compatibility warnings. `dotnet test Rah_Negar.sln -c
Release` passed with **695/695 tests**, 0 failures and 0 skips. `git diff
--check` passed. No production data or authority state was changed.

## 15. Exact final status

**PHASE 9.5C4 FIX COMPLETE — MANUAL REQUALIFICATION REQUIRED**

## Phase 9.5C2 superseding update

The consolidated qualification harness now prepares isolated Rasht/Ramsar
fixtures, runs MQ-01 through MQ-05 support suites, and emits sanitized TRX
files plus `Qualification/qualification-evidence/readiness-manifest.json`.
Those five items are READY TO EXECUTE NOW pending operator/reviewer inspection;
their automated PASS results are not manual PASS. MQ-06 through MQ-12 are also
READY TO EXECUTE NOW because the exact launcher path and deterministic steps are
documented; they still require human desktop observation and screenshots.

Current C2 counts: READY TO EXECUTE NOW 12; EXECUTED PASS 0; EXECUTED FAIL 0;
BLOCKED 0. Production-only evidence remains outside these 12 items and is not
executed. Legacy remains authoritative; Target remains inactive and routing
disabled. **PRODUCTION CUTOVER IS NOT AUTHORIZED.**

## Phase 9.5C3 pilot shutdown-guard defect update

### Original manual FAIL

During manual qualification, an active/incomplete Pilot session was closed
with the form X. The first warning appeared and the operator declined closing,
leaving the form open. A second X produced no warning, closed the Pilot form,
and returned to Main Form without exiting the application. Legacy authority
remained unchanged. This is a confirmed manual qualification FAIL for MQ-08.

### C3 disposition

`FrmLivePilot` now clears the modal `DialogResult` whenever a close attempt is
cancelled, including the asynchronous stop-then-close handoff. Focused
regression coverage exercises first, second, and repeated cancelled X
attempts, plus completed/stopped terminal sessions and authority invariants.
The code fix is complete; MQ-08 remains **READY FOR MANUAL REQUALIFICATION**.
The exact requalification procedure is in the C3 fix report. No other manual
qualification item is resumed by this update.

## Phase 9.5C4 confirmed-close defect update

### Second manual defect

After the C3 fix, manual qualification confirmed that the Cancel/No path was
correct: the first X warned and stayed open, and the next X warned again. A
second defect was then observed for an active/incomplete session: selecting YES
on the first warning did not close Pilot; a second X closed it without another
warning and returned to Main Form. MQ-08 is therefore **NOT PASS** and remains
**FAIL / requalification required**.

### C4 disposition

The YES path now performs the existing explicit stop semantics within the same
FormClosing callback and allows that exact close event to complete. It no
longer sets `e.Cancel` and re-enters `Close()`. This removes the re-entrant
second-close dependency and leaves no confirmed-close bypass state. Completion
is never recorded by this path; Legacy remains authoritative and Target stays
inactive. Focused coverage now verifies same-attempt close, one warning, no
completion, explicit stop behavior, authority invariants, and fresh-form state.

The exact C4 manual procedures for both Rasht and Ramsar are in the runbook and
`docs/phase9.5c4-pilot-confirmed-close-fix-report.md`. They are not marked PASS
automatically.
