# Phase 9.5C5 MQ-08 Closure and Next Manual Batch

Status: **PHASE 9.5C5 MQ-08 CLOSED - REMAINING MANUAL QUALIFICATION READY**

**PRODUCTION CUTOVER IS NOT AUTHORIZED.** Legacy remains authoritative; Target
remains inactive, non-authoritative, and routing-disabled.

## 1. Objective

Close MQ-08 from the final C4 human observation, preserve its complete defect
and requalification history, reconcile MQ-01 through MQ-12, document the
active-session cancellation disposition, prepare DPI checks, and provide the
next executable human batch without changing production authority or production
data.

## 2. MQ-08 historical defect chain

1. Original shutdown/close qualification: first X showed an unfinished-session
   warning; cancelling left Pilot open; second X bypassed the guard.
2. Phase 9.5C3 fixed the repeated-Cancel guard defect.
3. Human requalification after C3 confirmed first X warning, Cancel/No keeping
   Pilot open, and a second warning on the second X. Repeated Cancel/No passed.
4. A second defect was discovered: active/incomplete Pilot, X, warning,
   Yes/confirm; Pilot did not close on that attempt; second X then closed it
   without another warning.
5. Phase 9.5C4 fixed the confirmed-close defect.
6. Final human requalification after C4 on one qualification profile confirmed
   active/incomplete Pilot, X, Yes, immediate same-attempt Pilot close, Main
   Form return, and no second X.

Earlier FAIL observations remain historical qualification results; they are not
erased or rewritten as PASS.

## 3. C3 result

C3 cleared the modal close result whenever a close was cancelled. Human
requalification after C3 passed repeated Cancel/No warnings and kept Pilot
open. The remaining confirmed-Yes path still required a separate requalification.

## 4. C4 result

C4 performed the existing explicit stop semantics in the current FormClosing
callback for Yes. It removed the deferred/re-entrant second-close dependency.
Focused automation verified same-attempt close, one warning, no completion,
stopped lifecycle, fresh-form state, Legacy authority, and Target inactivity.

## 5. Final human MQ-08 PASS

Manual result: **PASS**.

The final human observation confirmed:

- X on active/incomplete Pilot showed the unfinished-session warning;
- Yes closed Pilot on that same close attempt;
- Main Form appeared;
- no second X was required;
- no production cutover occurred;
- no Target authority activation occurred; and
- Legacy remained authoritative.

This was one generic qualification profile. No human Ramsar observation is
claimed or required for this station-independent UI/session lifecycle behavior.

## 6. Authority-state verification

MQ-08 final observation verified read-only Pilot behavior, Legacy authority,
Target inactivity, Main Form return, no production database access, and no
authority transition:

```text
Legacy: authoritative
Target: inactive, non-authoritative, routing-disabled
Production cutover: not authorized
```

## 7. Station/profile independence

`FrmLivePilot.OnFormClosing` makes its decision from lifecycle state and dialog
result. It does not branch on `Rasht`, `Ramsar`, station name, or unit count.
The focused tests now run repeated Cancel/No and confirmed-Yes close behavior
against both qualification fixture shapes: Rasht/3 units and Ramsar/4 units.
Only qualification-side test coverage changed; production code was not changed.

Rasht and Ramsar are qualification fixtures, not production behavior selectors.
Production remains profile-driven and must derive operational structure from
user-defined station/profile configuration.

## 8. Remaining MQ inventory

| ID | State | Manual PASS already obtained | Visual observation | Command/review only | Production-only | Ready now | Dependencies | Gates |
|---|---|---:|---:|---:|---:|---:|---|---|
| MQ-01 | READY TO EXECUTE NOW | No | No | Yes | No | Yes | Review 3/3 support TRX and sanitized backup/restore evidence | DB-03, BR-02, BR-03, BR-05, BR-06 |
| MQ-02 | READY TO EXECUTE NOW | No | No | Yes | No | Yes | Review 7/7 support TRX and sanitized security evidence | SEC-01..05, SEC-08 |
| MQ-03 | READY TO EXECUTE NOW | No | No | Yes | No | Yes | Review 7/7 support TRX, Rasht-3/Ramsar-4 manifests and negative cases | MIG-03, MIG-04, RT-01 |
| MQ-04 | READY TO EXECUTE NOW | No | No | Yes | No | Yes | Review 4/4 support TRX and migration receipts | MIG-02, MIG-05 |
| MQ-05 | READY TO EXECUTE NOW | No | No | Yes | No | Yes | Review 10/10 support TRX and activation-boundary JSONL | AUTH-03, AUTH-04, MIG-06, SEC-05 |
| MQ-06 | READY TO EXECUTE NOW | No | Yes | No | No | Yes | One generic Pilot observation with Stop, screenshots and hashes | UI-02, UI-06 |
| MQ-07 | BLOCKED | No | Attempted, not practically exercisable | No | No | No | All five workflows completed before Stop could be clicked; retain automated invariants | UI-03, UI-06 |
| MQ-08 | PASS | Yes | Complete on one generic profile | No | No | N/A | C3/C4 fixes and final same-attempt Yes observation | UI-04, UI-06 |
| MQ-09 | READY TO EXECUTE NOW | No | Yes | No | No | Yes | Human 100% DPI observation and screenshots | UI-05, UI-06 |
| MQ-10 | READY TO EXECUTE NOW | No | Yes | No | No | Yes | Human 125% DPI observation and screenshots | UI-05, UI-06 |
| MQ-11 | READY TO EXECUTE NOW | No | Yes | No | No | Yes | Human 150% DPI observation and fixed-grid check | UI-05, UI-06 |
| MQ-12 | READY TO EXECUTE NOW | No | Yes | No | No | Yes | Human cancel/RTL/fields/traceability review | UI-06 |

MQ-07 is the only BLOCKED item. Its concrete blocker is the observed
qualification-method timing window, not a confirmed production coding defect.
The defensible disposition is **MANUAL OBSERVATION NOT PRACTICALLY EXERCISABLE,
WITH AUTOMATED INVARIANT EVIDENCE RETAINED**. No artificial production delay or
qualification-only timing control was added.

## 9. MQ-01-MQ-05 evidence disposition

The local ignored support evidence passed MQ-01 3/3, MQ-02 7/7, MQ-03 7/7,
MQ-04 4/4, and MQ-05 10/10. Each still requires human inspection/sign-off;
none is a manual PASS.

| ID | Human inspection/sign-off | Screenshot | Actual operator action | Service-level review |
|---|---|---:|---:|---:|
| MQ-01 | TRX plus backup/restore identity, SHA-256, SQLite/FK, staged replacement, rollback and authorization criteria | No | Review/sign-off | Yes |
| MQ-02 | TRX plus ShiftProfile/ManagementCredential binding, bounded recovery and no-secret/no-bypass criteria | No | Review/sign-off | Yes |
| MQ-03 | TRX plus both fixture shapes, redacted manifests and cross-station/count/ESD/event negative cases | No | Review/sign-off | Yes |
| MQ-04 | TRX plus integrity, migration ledger, idempotency, preservation and authority fields | No | Review/sign-off | Yes |
| MQ-05 | TRX plus `EligibleButNotExecuted`, `ActivationExecuted=false`, blocked prerequisites and no activation executor/startup registration | No | Review/sign-off | Yes |

Raw TRX files under `Qualification/qualification-evidence/` remain ignored and
must not be committed. Automated PASS is not converted to manual PASS.

## 10. Active-session cancellation disposition

The selected disposition is **B**. Multiple manual attempts found that all five
Pilot workflows completed before Stop could be clicked. Retain automated
cancellation and authority/database invariant evidence. Do not claim MQ-07
PASS, do not add artificial delays to normal production Pilot behavior, and do
not modify production timing for qualification.

## 11. Remaining UI observations

- MQ-06: Main Form -> explicit read-only Pilot -> Start -> Review -> Stop ->
  Main Form. PASS requires responsive Stop, stopped reason, retained evidence,
  unchanged hashes/authority and no raw error. Capture active/review/stopped/
  return screenshots. One generic profile is sufficient.
- MQ-09: at 100%, inspect Main Form, qualification/readiness UI, Pilot
  dashboard, dialogs, grid and navigation. Capture screenshots.
- MQ-10: repeat independently at 125%.
- MQ-11: repeat independently at 150%; use the 4-unit representative shape once
  for density and verify the 1920x1080 fixed Grid core has no forbidden
  horizontal scrolling or header wrap.
- MQ-12: choose No/cancel, verify no session, exercise keyboard-only RTL/focus,
  inspect identity/monitoring/rollback/stop-reason/completion fields, and obtain
  reviewer trace. Capture screenshots.

For these generic UI behaviors, one human profile is sufficient. Profile shape
matters only as a representative density check for the fixed grid; station name
does not select production behavior.

## 12. DPI execution plan

Set Windows Display scaling at `Settings -> System -> Display -> Scale`. Close
the app before changing scale. Sign-out/restart is not normally required;
relaunch after the setting applies, and sign out/restart only if Windows prompts
or the scale does not take effect.

```powershell
powershell -ExecutionPolicy Bypass -File .\Qualification\launch-qualification.ps1 -Station Ramsar -QualificationDirectory (Join-Path (Get-Location) 'Qualification\qualification-data')
```

At 100%, 125% and 150%, inspect Main Form, the qualification/readiness surface,
Pilot dashboard, dialogs, workflow rows, grid headers, monitoring/rollback
fields, Stop, Complete and Return. Acceptance requires no overlap, clipping,
inaccessible buttons, truncated critical labels, unreadable RTL Persian,
broken grid/layout, forbidden horizontal failure, or navigation failure. At
150% and 1920x1080 the fixed operational Grid core must remain usable without
forbidden horizontal scrolling or header wrap.

Record scale, resolution, OS, Release hash, profile, UTC, screenshots and
before/after hashes. Do not mark DPI PASS until the operator changes Windows
scale and observes the UI. Restore the prior scale afterward.

## 13. Exact next human batch

1. At the current normal scale, use one disposable launch for MQ-06 and MQ-12;
   record MQ-07's documented disposition and do not repeat MQ-08.
2. Review and sign off MQ-01 through MQ-05 from the existing ignored TRX and
   sanitized evidence. Do not commit raw TRX.
3. Set 100%, relaunch once, execute MQ-09, and close the app.
4. Set 125%, relaunch once, execute MQ-10, and close the app.
5. Set 150%, relaunch once, execute MQ-11 with the fixed-grid check, close the
   app, and restore the previous Windows scale.

## 14. Production-only items excluded

Do not begin production-only evidence collection. Excluded items are actual
production DB/binary identity and hashes, real backup/restore, real migration
and post-integrity evidence, management/GO authorization, production
installation evidence, cutover timestamp, and authority transition.

## 15. Validation and final status

The directly related focused tests passed **25/25**, with 0 failures and 0
skips. Only qualification-side test coverage changed; no production code
changed. Raw TRX evidence was not modified or committed. Documentation
validation is `git diff --check`.

**PRODUCTION CUTOVER IS NOT AUTHORIZED.**

**PHASE 9.5C5 MQ-08 CLOSED - REMAINING MANUAL QUALIFICATION READY**
