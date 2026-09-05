# Phase 9.5 Consolidated Manual Qualification Runbook

Status: **PHASE 9.5C5 MQ-08 CLOSED - REMAINING MANUAL QUALIFICATION READY**

This runbook is isolated qualification only. It does not authorize production
cutover, migration, restore, Target authority, or production-data mutation.
Legacy remains authoritative.

## Operator rules and common setup

Run from `D:\Projects\RahNegar_SQLite\Rah_Negar`. Every item starts `UNRUN`.
Record `PASS`, `FAIL`, or `BLOCKED` only after the listed evidence is captured;
automated tests support a manual review and do not make a manual item PASS.
Never use real credentials, hashes, private keys, production paths, or data.
Stop if a route is enabled, authority is ambiguous, or a generated path is under
the application `Data` directory.

```powershell
powershell -ExecutionPolicy Bypass -File .\Qualification\prepare-qualification.ps1 -OutputDirectory (Join-Path (Get-Location) 'Qualification\qualification-data')
dotnet build Rah_Negar.sln -c Release --no-restore
```

Launch Rasht:

```powershell
powershell -ExecutionPolicy Bypass -File .\Qualification\launch-qualification.ps1 -Station Rasht -QualificationDirectory (Join-Path (Get-Location) 'Qualification\qualification-data')
```

Launch Ramsar:

```powershell
powershell -ExecutionPolicy Bypass -File .\Qualification\launch-qualification.ps1 -Station Ramsar -QualificationDirectory (Join-Path (Get-Location) 'Qualification\qualification-data')
```

The launcher copies Release output and a disposable database to
`Qualification/qualification-run`; it does not redirect production path
resolution. Capture fixture and copied-database hashes:

```powershell
$db = (Resolve-Path .\Qualification\qualification-data\Rasht\db.sys).Path
Get-FileHash -Algorithm SHA256 -LiteralPath $db
Get-FileHash -Algorithm SHA256 -LiteralPath .\Qualification\qualification-run\Data\db.sys
```

Replace `Rasht` with `Ramsar` as applicable. Cleanup only generated directories:

```powershell
if (Test-Path -LiteralPath .\Qualification\qualification-run) { Remove-Item -LiteralPath .\Qualification\qualification-run -Recurse -Force }
if (Test-Path -LiteralPath .\Qualification\qualification-data) { Remove-Item -LiteralPath .\Qualification\qualification-data -Recurse -Force }
```

## Item index

| ID | Item and exact command | Stations | Destructive? | Readiness | Gates |
|---|---|---|---|---|---|
| MQ-01 | Backup/restore support command and evidence review | Rasht/Ramsar fixtures | Fixture only | READY TO EXECUTE NOW — 3/3 support passed; operator/reviewer receipt sign-off remains | DB-03, BR-02, BR-03, BR-05, BR-06 |
| MQ-02 | Security support command and evidence review | Both fixtures | No | READY TO EXECUTE NOW — 7/7 support passed; operator/reviewer security evidence sign-off remains | SEC-01, SEC-02, SEC-03, SEC-04, SEC-05, SEC-08 |
| MQ-03 | Provisioning support command and manifest review | Rasht 3, Ramsar 4 | Fixture only | READY TO EXECUTE NOW — 7/7 support passed; manifest/negative-case review remains | MIG-03, MIG-04, RT-01 |
| MQ-04 | Migration support command and receipt review | Both fixtures | Fixture only | READY TO EXECUTE NOW — 4/4 support passed; receipt review remains | MIG-02, MIG-05 |
| MQ-05 | Activation-boundary support command and JSONL review | Both fixtures | No | READY TO EXECUTE NOW — 10/10 support passed; safe activation-readiness review remains | AUTH-03, AUTH-04, MIG-06, SEC-05 |
| MQ-06 | Stop after successful active observation; use the station launch command | One generic disposable profile | No | READY TO EXECUTE NOW — human screenshots and hashes required | UI-02, UI-06 |
| MQ-07 | Active-session cancellation | N/A for human observation under disposition B | No | BLOCKED — workflows complete before Stop can be clicked; retain automated invariant evidence; no production delay | UI-03, UI-06 |
| MQ-08 | Pilot-form close guard | One generic qualification profile | No | PASS — final C4 human same-attempt Yes close; no duplicate station observation required | UI-04, UI-06 |
| MQ-09 | Independent 100% DPI lifecycle | One representative profile | No | READY TO EXECUTE NOW — change Windows scale and capture screenshots | UI-05, UI-06 |
| MQ-10 | Independent 125% DPI lifecycle | One representative profile | No | READY TO EXECUTE NOW — independently observe and capture screenshots | UI-05, UI-06 |
| MQ-11 | Independent 150% DPI lifecycle and fixed-grid check | One representative 4-unit profile | No | READY TO EXECUTE NOW — inspect 1920x1080 Grid core for forbidden horizontal scroll/header wrap | UI-05, UI-06 |
| MQ-12 | Confirmation cancel, keyboard/RTL, fields and traceability | One generic qualification profile | No | READY TO EXECUTE NOW — human observation, screenshots and reviewer trace required | UI-06 |

## MQ-01 through MQ-05 - deterministic local boundary reviews

For each command in the index, save the TRX result and inspect the safe receipt
or descriptor. Record operator, reviewer, UTC time, station/fixture shape and
result. Required assertions:

- MQ-01: source/backup/destination identities, SHA-256, SQLite integrity,
  foreign keys, staged replacement, verified rollback copy, sidecar handling,
  deterministic fault recovery, and rejection of missing/expired/wrong-scope
  ManagementCredential proof. Failure means direct overwrite, ambiguous
  destination, missing rollback evidence, or authorization bypass.
- MQ-02: ShiftProfile is the only normal identity; singleton
  ManagementCredential is privileged proof; action/scope/correlation/version/
  expiry and audit bind exactly. Review bounded recovery and ECDSA P-256 ESD
  verification. PASS requires no RBAC, Administrator, Engineer, Operator,
  Viewer, Support, universal secret, master password, private key, or raw
  credential material in ordinary evidence.
- MQ-03: all target routes are composed only for qualification and remain
  disabled, Legacy-owned and non-mutating. Validate exactly 3 Rasht units and
  4 Ramsar units, station-bound profiles, singleton ManagementCredential,
  device/key fingerprints, baselines, allowed Events, ESD, snapshots and
  locks. Repeat the package for `AlreadyProvisioned`; inject cross-station,
  count, ESD, event and immutable-evidence conflicts. PASS requires no partial
  rows and a redacted manifest.
- MQ-04: explicit database and verified-backup paths, pre/post integrity,
  checksummed migration ledger, idempotent rerun, preservation comparison and
  unchanged original backup must be visible. Reject wrong identity/hash,
  unsupported state, lock/disk failure, cancellation and post-validation
  failure. PASS requires `LegacyRemainsAuthoritative=true` and
  `TargetRoutingDisabled=true` on successful receipts.
- MQ-05: complete prerequisites must produce `EligibleButNotExecuted`, state
  `ApprovedForActivation`, `ActivationExecuted=false`, Target not accepted and
  Legacy authoritative. Missing/failed/stale/mismatched receipt, failed
  backup/integrity/rollback, wrong station scope, missing/invalid management
  proof and missing operator intent must produce `ActivationBlocked`. Inspect
  JSONL for safe categorical evidence only. Persistence failure must be blocked.
  No activation executor or startup registration may be present.

Evidence for MQ-01--MQ-05: sanitized TRX output, receipt/descriptor text,
fixture/database hashes where applicable, failure category, station, UTC time,
and reviewer sign-off. The only destructive actions are isolated fixture writes.
Cleanup: common cleanup commands.

## MQ-06 - Stop after successful observation

Prerequisite: fresh isolated station fixture and initial copied-database hash.
For each station, sign in with the synthetic qualification account, enter Pilot
explicitly, confirm read-only mode, start observation, wait for all five
workflows to complete and reach review, then click Stop before Complete. PASS
requires responsive Stop, stopped status/reason, retained safe evidence, safe
return, no raw exception/SQL/sensitive detail, unchanged authority and
unchanged database hash. FAIL covers unavailable/hanging Stop, false
completion, lost evidence, mutation or authority change. Capture
active/review/stopped/return screenshots, hashes and sanitized stop log. No
destructive action; cleanup is common cleanup.

## MQ-07 - active-session cancellation

For each station, start Pilot observation and cancel before review. PASS
requires responsive cancellation, no false completion, no unhandled exception,
safe Legacy return, unchanged authority and unchanged before/after hashes. FAIL
covers a hang, false session, database mutation, raw error or lost Legacy route.
Capture screenshots, cancellation state text, hashes, safe log, station and UTC
time. No destructive action; cleanup is common cleanup.

## MQ-08 - Pilot-form shutdown guard requalification

This item is **READY FOR MANUAL REQUALIFICATION** after the C4 confirmed-close
fix. The historical sequence is preserved: the original manual FAIL exposed a
cancel-path guard defect; C3 fixed that defect and the Cancel/No repeated-warning
path passed requalification; then a second manual defect was observed when YES
did not close on the first attempt. MQ-08 remains FAIL / requalification
required until these steps are completed. Do not resume other manual items.

For BOTH Rasht and Ramsar, use a fresh isolated fixture and initial
copied-database hash.

### Test A - Cancel/No repeated-warning path

1. Launch the qualification station.
2. Log in.
3. Enter Pilot explicitly.
4. Start read-only observation.
5. While the session is incomplete/in review state, click X.
6. Choose Cancel/No.
7. Verify Pilot remains open.
8. Click X again.
9. Verify the unfinished-session warning appears again. Repeat the cancel/X
   cycle at least three more times; every attempt must warn and every
   cancellation must keep Pilot open.

### Test B - Confirmed-close path

1. With an incomplete Pilot session, click X.
2. Choose Yes.
3. Verify Pilot closes immediately on this same attempt.
4. Verify Main Form appears.
5. Verify no second X click was required.
6. Verify Legacy authority remained authoritative.
7. Verify no automatic Target activation occurred.
8. Re-enter Pilot and verify no stale confirmed-close state remains.

Capture screenshots of the active state, each guard response, the repeated
cancel state, the confirmed close, Main Form return, and fresh Pilot entry, plus
station, UTC time, Release binary hash, process-exit observation,
before/after hashes, and a sanitized log. Verify no hidden completion, no
unintended stop beyond the existing explicit YES semantics, and no authority
change. Record `PASS` only if the human evidence satisfies every expectation;
otherwise record `FAIL`. Do not mark these steps PASS automatically from tests.
No destructive action; cleanup is common cleanup.

Historical sequence: original defect -> C3 fix -> Cancel/No requalification
success -> confirmed-close defect discovery -> C4 fix pending manual
requalification.

## MQ-09, MQ-10 and MQ-11 - 100%, 125% and 150% DPI

Run each item independently at its named Windows display scale for both
stations; do not infer one scale from another. Check RTL, focus order, labels,
grid headers, workflow rows, monitoring/rollback fields, dialogs, Stop,
Complete and Return. Exercise explicit entry, confirmation, Start, review and
the applicable terminal action. PASS requires no clipping, overlap, hidden
controls, unreadable text, focus loss or truncated safe identifiers and no
data/authority mutation. FAIL is any such defect. Capture scale, resolution,
OS, Release binary hash, station, state screenshots, UTC time and before/after
DB hashes. Restore the workstation's previous scale after each run.

## MQ-12 - residual cancel, RTL, fields and traceability

For each station choose No/cancel on the Pilot confirmation and confirm no
session is created and Legacy remains visible. Repeat keyboard-only navigation
and record RTL/focus order. Independently check identity, monitoring, rollback,
stop-reason and completion fields. Reconcile fixture/copy hashes and a
sanitized run log for every scenario. A second reviewer must trace each
screenshot/log row to a unique safe ID, station, timestamp and outcome. PASS
requires a complete package; FAIL covers Pilot entry on cancel, unusable RTL or
focus, missing fields, missing hashes/logs or untraceable evidence.

## Recording template

```text
Qualification ID:
Station / units / DPI:
Prerequisite and fixture path:
Exact launch or test command:
Operator / independent reviewer:
Start UTC / end UTC:
Result: UNRUN | PASS | FAIL | BLOCKED
Expected PASS observed:
Expected FAIL behavior checked:
Screenshot/state text:
DB/hash/evidence preservation:
Destructive action: NO | isolated fixture only
Cleanup/reset completed:
Gate IDs:
Failure or follow-up:
```

A local PASS does not create production evidence. Any failure or missing manual
evidence keeps the gate `CONDITIONAL` or `BLOCKED` under the readiness policy.

## Phase 9.5C5 superseding runbook addendum

This addendum supersedes the earlier C4 execution status and station-duplication
instructions above. The earlier MQ-08 steps remain as historical evidence of
what was requalified; the current result is MQ-08 **PASS**.

### MQ-08 current result and history

The final C4 human observation on one qualification profile passed: active/
incomplete Pilot -> X -> Yes -> Pilot closed on the same attempt -> Main Form
appeared. No second X was required, no Target authority activated, and Legacy
remained authoritative. No human Ramsar observation is claimed or required.

Preserve the full chain: original FAIL when Cancel/No left a guard bypass; C3
fix; human C3 requalification passed repeated Cancel/No warnings; second FAIL
when Yes did not close on the first attempt; C4 fix; final human C4 PASS.

The close behavior is profile-independent. `FrmLivePilot` uses lifecycle state
and dialog result, not station name or unit count. The focused tests cover both
Rasht/3-unit and Ramsar/4-unit qualification fixtures. Rasht and Ramsar are
qualification fixtures, not production behavior selectors; production remains
profile-driven.

### Current inventory and dispositions

| ID | State | Human visual observation | Command/review only | Production-only | Ready now | Disposition |
|---|---|---:|---:|---:|---:|---|
| MQ-01 | READY TO EXECUTE NOW | No | Yes | No | Yes | Review 3/3 TRX plus sanitized backup/restore evidence and sign off |
| MQ-02 | READY TO EXECUTE NOW | No | Yes | No | Yes | Review 7/7 TRX plus sanitized security evidence and sign off |
| MQ-03 | READY TO EXECUTE NOW | No | Yes | No | Yes | Review 7/7 TRX, Rasht-3/Ramsar-4 manifests and negative cases |
| MQ-04 | READY TO EXECUTE NOW | No | Yes | No | Yes | Review 4/4 TRX plus migration receipts and sign off |
| MQ-05 | READY TO EXECUTE NOW | No | Yes | No | Yes | Review 10/10 TRX plus safe activation-boundary JSONL and sign off |
| MQ-06 | READY TO EXECUTE NOW | Yes | No | No | Yes | One generic profile: Start -> Review -> Stop -> Main Form; screenshots/hashes |
| MQ-07 | BLOCKED | Attempted but not practically exercisable | No | No | No | All five workflows finish before Stop can be clicked; retain automated invariant evidence |
| MQ-08 | PASS | Complete on one generic profile | No | No | N/A | Final C4 same-attempt Yes close observation |
| MQ-09 | READY TO EXECUTE NOW | Yes | No | No | Yes | 100% DPI human observation and screenshots |
| MQ-10 | READY TO EXECUTE NOW | Yes | No | No | Yes | 125% DPI human observation and screenshots |
| MQ-11 | READY TO EXECUTE NOW | Yes | No | No | Yes | 150% DPI human observation and fixed-grid check |
| MQ-12 | READY TO EXECUTE NOW | Yes | No | No | Yes | Cancel/RTL/focus/fields/traceability human review |

MQ-07's BLOCKED state has a concrete qualification-method blocker. Its
defensible disposition is **MANUAL OBSERVATION NOT PRACTICALLY EXERCISABLE,
WITH AUTOMATED INVARIANT EVIDENCE RETAINED**. Do not claim PASS, add artificial
production delays, or change production timing.

### MQ-01 through MQ-05 exact review rule

The support results are MQ-01 3/3, MQ-02 7/7, MQ-03 7/7, MQ-04 4/4, and MQ-05
10/10. Inspect the matching ignored TRX in
`Qualification/qualification-evidence/`, the sanitized receipt/descriptor,
and `readiness-manifest.json`; record operator, independent reviewer, UTC time,
fixture shape and sign-off. No screenshot is required and no automated PASS is
manual PASS. Review backup/restore integrity and rollback (MQ-01), security
binding/no secrets (MQ-02), both provisioning shapes and negative cases (MQ-03),
migration integrity/ledger/idempotency (MQ-04), and `EligibleButNotExecuted`,
`ActivationExecuted=false`, blocked prerequisites and no executor/startup
registration (MQ-05).

### Remaining UI actions

- MQ-06: one generic profile; Main Form -> explicit read-only Pilot -> Start ->
  wait for Review -> Stop -> Main Form. PASS requires responsive Stop, stopped
  reason, safe retained evidence, unchanged hashes/authority and no raw error;
  FAIL is a hang, false completion, lost evidence, mutation or authority change.
  Capture active/review/stopped/return screenshots.
- MQ-07: no further human launch is required under disposition B. Retain
  automated cancellation/invariant evidence; do not call completed workflows a
  manual PASS.
- MQ-09/10/11: independently observe Main Form, qualification/readiness UI,
  Pilot dashboard, dialogs, workflow rows, grid headers, monitoring/rollback
  fields, Stop, Complete and Return at 100%, 125% and 150%. PASS requires no
  overlap, clipping, inaccessible buttons, truncated critical labels, unreadable
  RTL text, broken grid/layout, forbidden horizontal failure or navigation
  failure. One profile is sufficient; use the 4-unit shape once for density at
  150%. At 1920x1080/150%, the fixed operational Grid core must remain usable
  without forbidden horizontal scrolling or header wrap.
- MQ-12: choose No/cancel, verify no session and Legacy visibility; exercise
  keyboard-only RTL/focus; inspect identity, monitoring, rollback, stop-reason,
  completion and traceability fields. PASS requires traceable evidence;
  screenshots are required.

### DPI settings and command

Use `Settings -> System -> Display -> Scale`. Close the app before changing
scale. Sign-out/restart is not normally required; relaunch after the setting
applies, and sign out/restart only if Windows prompts or the scale does not take
effect. Use the disposable 4-unit representative fixture:

```powershell
powershell -ExecutionPolicy Bypass -File .\Qualification\launch-qualification.ps1 -Station Ramsar -QualificationDirectory (Join-Path (Get-Location) 'Qualification\qualification-data')
```

Record scale, resolution, OS, Release hash, profile, UTC, screenshots and
before/after hashes. Do not record DPI PASS until Windows scaling was changed
and observed. Restore the prior scale after the batch.

### Next human batch

1. At the current normal scale, use one disposable launch for MQ-06 and MQ-12;
   record MQ-07's disposition and do not repeat MQ-08.
2. Review/sign off MQ-01 through MQ-05 from the existing ignored evidence; do
   not commit raw TRX.
3. Set 100%, relaunch once for MQ-09, then 125% for MQ-10, then 150% for MQ-11
   with the fixed-grid check; close between changes and restore the prior scale.

Do not begin production-only evidence collection. Production-only items include
production DB/binary identity, real backup/restore, real migration/post-
integrity evidence, management/GO authorization, installation evidence,
cutover timestamp, and authority transition.

**PRODUCTION CUTOVER IS NOT AUTHORIZED.**

**PHASE 9.5C5 MQ-08 CLOSED - REMAINING MANUAL QUALIFICATION READY**
