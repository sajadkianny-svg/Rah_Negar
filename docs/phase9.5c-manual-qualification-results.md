# Phase 9.5C Consolidated Manual Qualification Results

Exact current status: **PHASE 9.5C5 MQ-08 CLOSED - REMAINING MANUAL QUALIFICATION READY**

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

The preceding C4 status text is historical. It is superseded by the C5 closure
below; the historical C4 FAIL is intentionally retained.

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

## Phase 9.5C5 superseding reconciliation

### 1. Objective and final status

This C5 continuation closes MQ-08 from the final human C4 observation, preserves
the defect chain, reconciles MQ-01 through MQ-12, records the active-session
cancellation disposition, and prepares the next human batch. It does not
authorize production cutover or change authority.

**PHASE 9.5C5 MQ-08 CLOSED - REMAINING MANUAL QUALIFICATION READY**

### 2. MQ-08 final manual PASS

Manual result: **PASS**.

The final C4 human requalification was performed on one qualification profile:
the operator used an active/incomplete Pilot session, clicked X, chose Yes, and
Pilot closed on that same close attempt. Main Form appeared; no second X was
required; no production cutover occurred; no Target authority activation
occurred; and Legacy remained authoritative.

No human Ramsar observation is claimed. Duplicate human observation is not
required for this station-independent UI/session lifecycle behavior.

The complete historical chain remains: original FAIL (Cancel/No left a bypass
state); C3 fix; human requalification after C3 passed repeated Cancel/No
warnings; a second defect showed Yes did not close on the first attempt; C4 fix;
final human C4 PASS on the same-attempt Yes path. Earlier FAIL observations are
not erased or rewritten.

### 3. Station/profile independence

`FrmLivePilot.OnFormClosing` uses lifecycle state and the dialog result. It does
not branch on `Rasht`, `Ramsar`, station name, or unit count. The directly
related regression coverage now runs repeated Cancel/No and confirmed-Yes
close behavior with both qualification fixture shapes: Rasht/3 units and
Ramsar/4 units. The new coverage is qualification-side test code only; no
production code changed.

Rasht and Ramsar are qualification fixtures, not production behavior selectors.
Production remains profile-driven and must derive operational structure from
user-defined station/profile configuration.

### 4. Exact remaining MQ inventory

| ID | Current state | Manual PASS already obtained | Human visual observation required | Command-driven/manual review only | Production-only | Ready now | Dependencies | Related gates |
|---|---|---:|---:|---:|---:|---:|---|---|
| MQ-01 | READY TO EXECUTE NOW | No | No | Yes | No | Yes | Review 3/3 support TRX and sanitized backup/restore receipts; operator/reviewer sign-off | DB-03, BR-02, BR-03, BR-05, BR-06 |
| MQ-02 | READY TO EXECUTE NOW | No | No | Yes | No | Yes | Review 7/7 support TRX and sanitized security composition/evidence | SEC-01..05, SEC-08 |
| MQ-03 | READY TO EXECUTE NOW | No | No | Yes | No | Yes | Review 7/7 support TRX and Rasht-3/Ramsar-4 manifests and negative cases | MIG-03, MIG-04, RT-01 |
| MQ-04 | READY TO EXECUTE NOW | No | No | Yes | No | Yes | Review 4/4 support TRX and migration receipt/integrity/ledger evidence | MIG-02, MIG-05 |
| MQ-05 | READY TO EXECUTE NOW | No | No | Yes | No | Yes | Review 10/10 support TRX and sanitized activation-boundary JSONL | AUTH-03, AUTH-04, MIG-06, SEC-05 |
| MQ-06 | READY TO EXECUTE NOW | No | Yes | No | No | Yes | Fresh fixture; active/review/Stop/return screenshots and hashes | UI-02, UI-06 |
| MQ-07 | BLOCKED | No | Attempted, but not practically exercisable | No | No | No | All five workflows completed before Stop could be clicked; retain automated invariant evidence | UI-03, UI-06 |
| MQ-08 | PASS | Yes | Final human observation complete on one generic profile | No | No | N/A | C3/C4 fixes and final same-attempt Yes observation | UI-04, UI-06 |
| MQ-09 | READY TO EXECUTE NOW | No | Yes | No | No | Yes | Human 100% DPI observation and screenshots | UI-05, UI-06 |
| MQ-10 | READY TO EXECUTE NOW | No | Yes | No | No | Yes | Human 125% DPI observation and screenshots | UI-05, UI-06 |
| MQ-11 | READY TO EXECUTE NOW | No | Yes | No | No | Yes | Human 150% DPI observation and fixed-grid check | UI-05, UI-06 |
| MQ-12 | READY TO EXECUTE NOW | No | Yes | No | No | Yes | Human cancel/RTL/field/traceability review | UI-06 |

MQ-07 is the only BLOCKED item. Its concrete blocker is the observed
qualification-method timing window, not a confirmed production coding defect.
Its defensible disposition is **MANUAL OBSERVATION NOT PRACTICALLY EXERCISABLE,
WITH AUTOMATED INVARIANT EVIDENCE RETAINED**. No artificial production delay or
qualification-only timing control was added.

### 5. MQ-01 through MQ-05 evidence disposition

The local ignored support evidence under `Qualification/qualification-evidence/`
passed MQ-01 3/3, MQ-02 7/7, MQ-03 7/7, MQ-04 4/4, and MQ-05 10/10. Each still
requires human inspection/sign-off of the sanitized TRX and service-level
receipt/descriptor evidence. No screenshot or visual observation is required;
the operator/reviewer action is command-driven evidence review only. Automated
PASS is not promoted to manual PASS.

Review criteria remain: MQ-01 backup/restore identity, SHA-256, SQLite/FK,
staged replacement and rollback; MQ-02 ShiftProfile/ManagementCredential
binding and no secrets/bypasses; MQ-03 both fixture shapes, disabled routes,
manifests and negative cases; MQ-04 integrity, ledger, idempotency, preservation
and Legacy-authoritative/Target-disabled fields; MQ-05
`EligibleButNotExecuted`, `ActivationExecuted=false`, blocked prerequisites,
safe categorical JSONL, and no activation executor/startup registration.

### 6. Remaining UI observations

- MQ-06: Main Form -> explicit read-only Pilot -> Start -> wait for Review ->
  Stop -> Main Form. PASS requires responsive Stop, stopped reason, retained
  evidence, unchanged hashes/authority and no raw error. Screenshot active,
  review, stopped and return states. One generic profile is sufficient.
- MQ-07: intended active-session Stop/cancellation could not be invoked before
  completion. Do not claim PASS; retain automated invariant evidence under the
  disposition above. No profile shape matters.
- MQ-09/MQ-10/MQ-11: at 100%/125%/150%, inspect Main Form, qualification/
  readiness surface, Pilot dashboard, dialogs, grid, workflow rows, monitoring/
  rollback fields, Stop, Complete and Return. Screenshot each scale. One
  representative profile is sufficient; use the 4-unit shape for density at
  150%, but station name does not select behavior.
- MQ-12: Pilot confirmation No/cancel, keyboard-only RTL/focus, identity,
  monitoring, rollback, stop-reason, completion and traceability fields. PASS
  requires no session after cancel and a reviewer-traceable evidence package.
  One generic profile is sufficient.

### 7. DPI execution plan

Set Windows Display scaling at `Settings -> System -> Display -> Scale`. Close
the app before each change. Sign-out/restart is not normally required; relaunch
after the setting applies, and sign out/restart only if Windows prompts or the
scale does not take effect.

```powershell
powershell -ExecutionPolicy Bypass -File .\Qualification\launch-qualification.ps1 -Station Ramsar -QualificationDirectory (Join-Path (Get-Location) 'Qualification\qualification-data')
```

At 100%, 125% and 150%, acceptance is no overlap, clipping, inaccessible
buttons, truncated critical labels, unreadable RTL Persian, broken grid/layout,
forbidden horizontal layout failure, or navigation failure. At 1920x1080 and
150%, the fixed operational Grid core must remain usable without forbidden
horizontal scrolling or header wrap. Record scale, resolution, OS, Release
hash, profile, UTC, screenshots, and before/after hashes. Do not mark PASS until
the operator changes scale and observes the UI. Restore the prior scale after.

### 8. Active-session cancellation disposition

The selected disposition is **B**: manual observation is not practically
exercisable because all five workflows completed before Stop could be invoked.
Automated cancellation and authority/database invariants are retained. Do not
modify production timing and do not add a qualification-only mechanism in this
batch.

### 9. Exact next human batch

1. At the current normal scale, use one disposable launch for MQ-06 and MQ-12.
   Record MQ-07's documented disposition only; do not repeat MQ-08.
2. Review and sign off MQ-01 through MQ-05 from the existing ignored TRX and
   sanitized evidence. Do not commit raw TRX.
3. Set 100%, relaunch once, execute MQ-09, and close the app.
4. Set 125%, relaunch once, execute MQ-10, and close the app.
5. Set 150%, relaunch once, execute MQ-11 with the fixed-grid check, close the
   app, and restore the previous Windows scale.

Do not begin production-only evidence collection. Excluded items are actual
production DB/binary identity, production backup/restore, real migration and
post-integrity evidence, management/GO authorization, installation evidence,
cutover timestamp, and authority transition.

### 10. Validation and final status

The directly related focused tests passed **25/25**, 0 failures, 0 skips. Only
qualification-side test coverage changed; no production code changed. Raw TRX
evidence was not modified or committed. `git diff --check` is required after
this documentation update.

**PRODUCTION CUTOVER IS NOT AUTHORIZED.**

**PHASE 9.5C5 MQ-08 CLOSED - REMAINING MANUAL QUALIFICATION READY**
