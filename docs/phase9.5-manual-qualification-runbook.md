# Phase 9.5 Consolidated Manual Qualification Runbook

Status: **PHASE 9.5 ALTERNATIVE GOVERNANCE CLOSED - AI-ASSISTED TECHNICAL REVIEW PASS WITH DOCUMENTED LIMITATION; PROJECT OWNER ACCEPTANCE ACCEPTED**

This runbook is isolated qualification only. It does not authorize production
cutover, migration, restore, Target authority, or production-data mutation.
Legacy remains authoritative. Independent Human Review = **NOT PERFORMED /
UNAVAILABLE**. AI-Assisted Technical Review is not organizationally independent.
Project Owner Acceptance is **ACCEPTED** by Sajad Kiyani at
**2026-09-05T21:16:42Z**; see `docs/phase9.5-ai-assisted-technical-review.md`
and `docs/phase9.5-project-owner-acceptance.md`.

The current governance overlay is authoritative for Phase 9.5 closure:

| Items | Current governance status |
|---|---|
| MQ-01 | Technical/Operator Review PASS; TRX 3/3; AI-Assisted Technical Review PASS |
| MQ-02 | Technical/Operator Review PASS; TRX 7/7; AI-Assisted Technical Review PASS |
| MQ-03 | Technical/Operator Review PASS; TRX 16/16; Supported product boundary = 3-5 units inclusive; AI-Assisted Technical Review PASS |
| MQ-04 | Technical/Operator Review PASS; TRX 18/18; AI-Assisted Technical Review PASS |
| MQ-05 | Technical/Operator Review PASS; TRX 10/10; AI-Assisted Technical Review PASS |
| MQ-06 | PASS |
| MQ-07 | BLOCKED — MANUAL OBSERVATION NOT PRACTICALLY EXERCISABLE, WITH AUTOMATED INVARIANT EVIDENCE RETAINED; do not convert to PASS |
| MQ-08 | PASS |
| MQ-09 | PASS — 1920x1080 / 100% DPI |
| MQ-10 | PASS — 1920x1080 / 125% DPI |
| MQ-11 | PASS AFTER C9 REMEDIATION — 1920x1080 / 150% DPI |
| MQ-12 | PASS |

Project Owner Acceptance is **ACCEPTED** for this overlay. The historical
prepared independent-review wording below is retained as historical evidence
and does not claim that an independent human review occurred.

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
| MQ-01 | Backup/restore support command and evidence review | Rasht/Ramsar fixtures | Fixture only | TECHNICAL/OPERATOR REVIEW PASS; AI-ASSISTED TECHNICAL REVIEW PASS; Project Owner Acceptance ACCEPTED; Independent Human Review not performed/unavailable | DB-03, BR-02, BR-03, BR-05, BR-06 |
| MQ-02 | Security support command and evidence review | Both fixtures | No | TECHNICAL/OPERATOR REVIEW PASS; AI-ASSISTED TECHNICAL REVIEW PASS; Project Owner Acceptance ACCEPTED; Independent Human Review not performed/unavailable | SEC-01, SEC-02, SEC-03, SEC-04, SEC-05, SEC-08 |
| MQ-03 | Provisioning support command and manifest review | Profile-driven generic MQ-03 product boundary; Rasht 3/Ramsar 4 fixtures | Fixture only | TECHNICAL/OPERATOR REVIEW PASS; AI-ASSISTED TECHNICAL REVIEW PASS; Project Owner Acceptance ACCEPTED; Independent Human Review not performed/unavailable | MIG-03, MIG-04, RT-01 |
| MQ-04 | Migration support command and receipt review | Both fixtures | Fixture only | TECHNICAL/OPERATOR REVIEW PASS; AI-ASSISTED TECHNICAL REVIEW PASS; Project Owner Acceptance ACCEPTED; Independent Human Review not performed/unavailable | MIG-02, MIG-05 |
| MQ-05 | Activation-boundary support command and JSONL review | Both fixtures | No | TECHNICAL/OPERATOR REVIEW PASS; AI-ASSISTED TECHNICAL REVIEW PASS; Project Owner Acceptance ACCEPTED; Independent Human Review not performed/unavailable | AUTH-03, AUTH-04, MIG-06, SEC-05 |
| MQ-06 | Stop after successful active observation; use the station launch command | One generic disposable profile | No | PASS — manual observation complete | UI-02, UI-06 |
| MQ-07 | Active-session cancellation | N/A for human observation under disposition B | No | BLOCKED — MANUAL OBSERVATION NOT PRACTICALLY EXERCISABLE, WITH AUTOMATED INVARIANT EVIDENCE RETAINED; do not convert to PASS | UI-03, UI-06 |
| MQ-08 | Pilot-form close guard | One generic qualification profile | No | PASS — final C4 human same-attempt Yes close; no duplicate station observation required | UI-04, UI-06 |
| MQ-09 | Independent 100% DPI lifecycle | One representative profile | No | PASS — 1920x1080, Windows Scale 100% | UI-05, UI-06 |
| MQ-10 | Independent 125% DPI lifecycle | One representative profile | No | PASS — 1920x1080, Windows Scale 125% | UI-05, UI-06 |
| MQ-11 | Independent 150% DPI lifecycle and fixed-grid check | One representative 4-unit profile | No | PASS AFTER C9 REMEDIATION — 1920x1080, Windows Scale 150% | UI-05, UI-06 |
| MQ-12 | Confirmation cancel, keyboard/RTL, fields and traceability | One generic qualification profile | No | PASS — keyboard-only Tab + Enter and RTL/traceability verified | UI-06 |

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
  disabled, Legacy-owned and non-mutating. Confirm production provisioning has
  no `TargetStationCode`, Rasht/Ramsar support check, or station-name unit-count
  switch. Inspect the supplied Station Profile identity and profile unit count,
  including arbitrary synthetic 3-unit and 5-unit success proofs and 2-unit,
  6-unit, and 35-unit rejection proofs; Rasht 3 and Ramsar 4 are fixture inputs
  only. Validate station-bound ShiftProfiles, singleton
  ManagementCredential, device/key fingerprints, baselines, allowed Events,
  ESD, snapshots and locks. Repeat the package for `AlreadyProvisioned`; review
  explicit cross-station, unit-count mismatch, ESD, Event record, immutable
  snapshot, and immutable lock conflicts. Each rejection must show no partial
  rows; the manifest must remain redacted.
- MQ-04: explicit database and verified-backup paths, pre/post integrity,
  checksummed migration ledger, idempotent rerun, preservation comparison and
  unchanged original backup must be visible. Reject wrong identity/hash,
  unsupported state, lock/disk failure, cancellation and post-validation
  failure. The support evidence must additionally show the approved executor
  using an explicit disposable copy, immutable receipt, exact final version,
  applied migration IDs/history, deterministic contiguous chain, checksum and
  checksum-tamper rejection, malformed/unknown/newer history rejection,
  intermediate schema/history rollback, preservation and unchanged verified
  backup. PASS requires `LegacyRemainsAuthoritative=true` and
  `TargetRoutingDisabled=true` on successful receipts, with no RBAC or Support
  identity and hostile context/backup/capacity/cancellation rejection without
  mutation.
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
| MQ-03 | READY FOR RENEWED HUMAN REVIEW | No | Yes | No | Yes | Review 16/16 TRX, profile-derived manifests, arbitrary 3/5 success, 2/6/35 rejection and transactional negative cases |
| MQ-04 | READY FOR RENEWED HUMAN/OPERATOR REVIEW | No | Yes | No | Yes | Review 18/18 TRX plus migration receipts, integrity/rollback evidence and sign off |
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

The support results are MQ-01 3/3, MQ-02 7/7, MQ-03 16/16, MQ-04 18/18, and MQ-05
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

## Phase 9.5C6 MQ-03 provisioning requalification addendum

Historical note: C7 supersedes every unit-range, test-count, synthetic-proof,
and review instruction in this C6 section. C6 correctly removed station-name
branching and made the supplied Station Profile authoritative for unit count,
but it independently defined the generic product boundary incorrectly as
1 through 35. Use the C7 addendum below for current MQ-03 review.

This addendum supersedes every earlier MQ-03 count, station-support statement,
and review instruction in this runbook. During manual review of the original
7-test MQ-03 evidence, production provisioning was found to derive support and
unit count from `TargetStationCode.Rasht` and `TargetStationCode.Ramsar`. That
evidence is invalid for the locked general, dynamic, profile-driven product
architecture.

The production provisioning contract now receives a Station Profile definition
containing an opaque profile identity and its unit count. C6 incorrectly
described the supported product range as 1 through 35 units. Validation compares the package's dynamic
unit structure with the supplied profile count; it never derives support, unit
count, fields, routing, or behavior from a station name. `TargetStationCode`,
the Rasht/Ramsar support predicate, and the 3/4 unit-count switch have been
removed from the provisioning path. The SQLite boundary was already generic
and required no schema change.

Run the consolidated support command:

```powershell
powershell -ExecutionPolicy Bypass -File .\Qualification\run-readiness-qualification.ps1 -SkipPreparation
```

The harness selects MQ-03 through the explicit `Qualification=MQ-03` trait.
The superseded C6 evidence reported 12 executed, 12 passed, 0 failed, and 0
skipped tests and included all of the following:

- Rasht 3-unit and Ramsar 4-unit legacy/qualification fixtures;
- the now-invalid station-not-named-Rasht-or-Ramsar 35-unit success proof;
- redacted profile-derived manifest evidence, runtime baselines, singleton
  ManagementCredential, and device/vendor-key metadata;
- `AlreadyProvisioned` and immutable snapshot/lock preservation;
- cross-station mapping and ESD conflict rejection;
- explicit `unit-count-mismatch`, `event-record-conflict`,
  `snapshot-record-conflict`, and `lock-record-conflict` rejections; and
- transactional assertions showing zero initial mutation or rollback of a
  deliberately inserted probe record, with the pre-existing Event, finalized
  snapshot, and finalized lock unchanged.

The operator and independent reviewer must compare the safe manifest/profile
identity, supplied unit count, TRX test names and outcomes, and conflict
preservation assertions. C6 automated 12/12 support was not a manual PASS and is
now superseded by C7. No production path,
credential, data, migration, activation, or authority transition may be used.

MQ-03 current status: **READY FOR RENEWED HUMAN/OPERATOR REVIEW**.

**PRODUCTION CUTOVER IS NOT AUTHORIZED.**

**PHASE 9.5C6 MQ-03 REMEDIATED - RENEWED HUMAN REVIEW REQUIRED**

## Phase 9.5C7 MQ-03 generic unit-count boundary addendum

Renewed human review found an independent defect after C6: profile-driven does
not mean unlimited unit count. The supplied profile remains authoritative and
station names remain irrelevant, but `TargetStationProfileRules` now enforces
the named generic product boundary `MinimumUnitCount = 3` and
`MaximumUnitCount = 5`.

Run the consolidated support command:

```powershell
powershell -ExecutionPolicy Bypass -File .\Qualification\run-readiness-qualification.ps1 -SkipPreparation
```

Inspect `Qualification/qualification-evidence/MQ-03.trx`. The C7 file must
report exactly 16 executed, 16 passed, 0 failed, and 0 skipped tests. Confirm:

- arbitrary station names with supplied 3-unit and 5-unit profiles provision;
- supplied 2-unit, 6-unit, and 35-unit profiles are rejected with
  `profile-unit-count-out-of-range` and leave all target tables empty;
- Rasht 3-unit and Ramsar 4-unit shapes remain qualification fixtures only;
- unit-count/persisted mapping, Event, finalized snapshot, finalized lock, ESD,
  and cross-station conflicts retain their rollback/preservation evidence;
- idempotency, manifest redaction, and inactive Target routes remain proven.

The operator and independent reviewer must inspect the regenerated 16-test TRX,
safe profile-derived evidence, boundary cases, and preserved negative cases
before assigning a manual result. Automated remediation is not manual PASS.

MQ-03 current status: **READY FOR RENEWED HUMAN/OPERATOR REVIEW**.

**PRODUCTION CUTOVER IS NOT AUTHORIZED.**

**PHASE 9.5C7 MQ-03 UNIT BOUNDARY REMEDIATED - RENEWED HUMAN REVIEW REQUIRED**

## Phase 9.5C8 MQ-04 migration evidence closure

The prior MQ-04 harness selected only
`Phase95B6ProductionMigrationExecutorTests`, producing 4 tests and leaving the
framework, ledger-classification, unified-chain, rollback, and no-authority
evidence outside the generated MQ-04 TRX. The harness now selects the explicit
`Qualification=MQ-04` trait on the minimum existing tests needed for the full
MQ-04 boundary. No migration logic or production safeguard was changed.

Run the isolated support command from the repository root:

```powershell
dotnet test .\Rah_Negar.Tests\Rah_Negar.Tests.csproj -c Release --no-restore `
  --filter "Qualification=MQ-04" `
  --logger "trx;LogFileName=$((Resolve-Path .\Qualification\qualification-evidence).Path)\MQ-04.trx"
```

Review the regenerated `Qualification/qualification-evidence/MQ-04.trx` for
exactly 18 executed and 18 passed tests, with no failed, skipped, error,
timeout, aborted, inconclusive, or not-executed results. The 18 tests cover:

- approved executor on an explicit disposable copy, immutable receipt, exact
  final version, applied IDs/history, unchanged verified backup and preservation;
- deterministic contiguous inventory, checksum validation and tamper rejection;
- malformed ledger, checksum, unknown migration and unsupported newer-version
  fail-closed classification;
- idempotent rerun/no-op and migration/schema/ledger rollback after failure; and
- Legacy authority, disabled Target routing, no RBAC/Support identity, and
  hostile context/backup/capacity/cancellation rejection without mutation.

This automated evidence supports review only. MQ-04 status is **READY FOR
RENEWED HUMAN/OPERATOR REVIEW**, not manual PASS. The operator and independent
reviewer must inspect the TRX and sanitized migration receipt, record fixture,
UTC time and sign-off, and confirm that no production database, production
backup, authority transition or cutover was used.

**PRODUCTION CUTOVER IS NOT AUTHORIZED.**

**PHASE 9.5C8 MQ-04 EVIDENCE COVERAGE COMPLETE - RENEWED HUMAN/OPERATOR REVIEW REQUIRED**

## Phase 9.5C9 MQ-11 150% DPI Pilot Dashboard remediation

MQ-11 required this remediation after manual observation at 1920x1080 with
Windows scaling at 150%, which confirmed
that the Pilot Dashboard was fully usable and correctly rendered when
maximized, while its normal window size did not expose the complete dashboard
vertically. `FrmLivePilot` now opens with `WindowState = Maximized`; Main Form,
RTL/DPI behavior, read-only Pilot behavior, qualification isolation, and
Legacy-authoritative behavior are unchanged.

MQ-11 remains **PENDING MANUAL REQUALIFICATION** after this code change. Do
not mark MQ-11 manual PASS automatically from the focused or full automated
tests. Repeat the 1920x1080/150% observation and fixed-grid check using the
Release build, and record screenshots and reviewer evidence before assigning a
manual result.

**PRODUCTION CUTOVER REMAINS NOT AUTHORIZED.**

## Phase 9.5C10 DPI and MQ-12 manual closure

This C10 addendum supersedes the prior pending/ready status for MQ-09 through
MQ-12 and records the verified manual results below. It does not change
production authority or authorize production cutover.

| ID | Current result | Verified manual evidence |
|---|---|---|
| MQ-01 | **Technical/Operator Review PASS; AI-Assisted Technical Review PASS** | TRX 3/3; Project Owner Acceptance ACCEPTED; Independent Human Review not performed/unavailable. |
| MQ-02 | **Technical/Operator Review PASS; AI-Assisted Technical Review PASS** | TRX 7/7; Project Owner Acceptance ACCEPTED; Independent Human Review not performed/unavailable. |
| MQ-03 | **Technical/Operator Review PASS; AI-Assisted Technical Review PASS** | TRX 16/16; supported product boundary = 3-5 units inclusive; Project Owner Acceptance ACCEPTED; Independent Human Review not performed/unavailable. |
| MQ-04 | **Technical/Operator Review PASS; AI-Assisted Technical Review PASS** | TRX 18/18; Project Owner Acceptance ACCEPTED; Independent Human Review not performed/unavailable. |
| MQ-05 | **Technical/Operator Review PASS; AI-Assisted Technical Review PASS** | TRX 10/10; Project Owner Acceptance ACCEPTED; Independent Human Review not performed/unavailable. |
| MQ-06 | **PASS** | Manual observation PASS. |
| MQ-07 | **BLOCKED / MANUAL OBSERVATION NOT PRACTICALLY EXERCISABLE, WITH AUTOMATED INVARIANT EVIDENCE RETAINED** | Preserve the existing disposition; do not convert MQ-07 to PASS. |
| MQ-08 | **PASS** | Existing manual observation PASS. |
| MQ-09 | **PASS** | 1920x1080, Windows Scale 100%; Main Form and Pilot Dashboard visually verified with no clipping, overlap, unintended horizontal scrolling, or unusable controls. |
| MQ-10 | **PASS** | 1920x1080, Windows Scale 125%; Main Form and Pilot Dashboard visually verified; controls remained visible and usable, with no qualification-blocking DPI/layout defect. |
| MQ-11 | **PASS AFTER C9 REMEDIATION** | 1920x1080, Windows Scale 150%; Main Form verified; Pilot Dashboard opens automatically Maximized; full dashboard, table, Blocked reasons, Warnings, and bottom buttons visible; no clipping or overlap. C9 remediation manually requalified successfully. |
| MQ-12 | **PASS** | Keyboard-only Tab + Enter verified; No/Cancel works without mouse; RTL and visible field/status traceability verified. Esc does not dismiss the dialog, recorded as a nonblocking observation because keyboard-only No/Cancel remains fully accessible. |

MQ-01 through MQ-05 have Technical/Operator Review PASS and AI-Assisted
Technical Review PASS. Project Owner Acceptance is ACCEPTED. Independent
Human Review was not performed and is unavailable; AI-Assisted Technical Review
is not organizationally independent. MQ-07 remains blocked under its
documented qualification-method disposition. Legacy remains production
authority; Target remains non-authoritative and routing remains disabled. No
production cutover authorization is granted by these results.

**PHASE 9.5C10 DPI AND MQ-12 MANUAL QUALIFICATION CLOSED - INDEPENDENT
REVIEWER SIGN-OFF FOR MQ-01--MQ-05 AND PRODUCTION-ONLY EVIDENCE REMAIN**

INDEPENDENT REVIEW PACKAGE PREPARED  SIGN-OFF PENDING (HISTORICAL ARTIFACT;
INDEPENDENT HUMAN REVIEW NOT PERFORMED / UNAVAILABLE)

## Phase 9.5 alternative governance closure

The current alternative governance path is **AI-Assisted Technical Review +
Project Owner Acceptance** and is **PHASE 9.5 ALTERNATIVE GOVERNANCE CLOSED**.
The technical decision is **AI-ASSISTED TECHNICAL REVIEW: PASS WITH DOCUMENTED
LIMITATION**. The documented limitation is MQ-07 plus the absence of independent
human review. Project Owner Acceptance was explicitly **ACCEPTED** by Sajad
Kiyani at **2026-09-05T21:16:42Z**.

Legacy remains authoritative. Target remains non-authoritative. Target routing
remains disabled. Production cutover remains unauthorized, and no production
activation is authorized.
