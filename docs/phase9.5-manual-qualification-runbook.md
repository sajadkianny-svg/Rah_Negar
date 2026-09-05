# Phase 9.5 Consolidated Manual Qualification Runbook

Status: **MQ-08 READY FOR MANUAL REQUALIFICATION** (Phase 9.5C4)

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
| MQ-01 | Backup/restore tests: `dotnet test Rah_Negar.Tests\Rah_Negar.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~ManagedSqliteBackupRestoreBoundaryTests" --logger "trx;LogFileName=mq-01.trx"` | Rasht, Ramsar fixtures | Fixture only | BLOCKED — test support passed; manual evidence review unavailable; see `docs/phase9.5c-manual-qualification-results.md` | DB-03, BR-02, BR-03, BR-05, BR-06 |
| MQ-02 | Security tests: `dotnet test Rah_Negar.Tests\Rah_Negar.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~Phase95B4SecurityCompositionTests" --logger "trx;LogFileName=mq-02.trx"` | Both fixtures | No | BLOCKED — test support passed; manual evidence review unavailable; see `docs/phase9.5c-manual-qualification-results.md` | SEC-01, SEC-02, SEC-03, SEC-04, SEC-05, SEC-08 |
| MQ-03 | Provisioning tests: `dotnet test Rah_Negar.Tests\Rah_Negar.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~Phase95B5ProvisioningTests" --logger "trx;LogFileName=mq-03.trx"` | Rasht 3, Ramsar 4 | Fixture only | BLOCKED — test support passed; manual evidence review unavailable; see `docs/phase9.5c-manual-qualification-results.md` | MIG-03, MIG-04, RT-01 |
| MQ-04 | Migration tests: `dotnet test Rah_Negar.Tests\Rah_Negar.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~Phase95B6ProductionMigrationExecutorTests" --logger "trx;LogFileName=mq-04.trx"` | Both fixtures | Fixture only | BLOCKED — test support passed; manual evidence review unavailable; see `docs/phase9.5c-manual-qualification-results.md` | MIG-02, MIG-05 |
| MQ-05 | Activation tests: `dotnet test Rah_Negar.Tests\Rah_Negar.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~Phase95B7ActivationBoundaryTests" --logger "trx;LogFileName=mq-05.trx"` | Both fixtures | No | BLOCKED — test support passed; manual evidence review unavailable; see `docs/phase9.5c-manual-qualification-results.md` | AUTH-03, AUTH-04, MIG-06, SEC-05 |
| MQ-06 | Stop after successful active observation; use the station launch command | Rasht 3, Ramsar 4 | No | BLOCKED — native desktop surface unavailable; see results document | UI-02, UI-06 |
| MQ-07 | Cancel during active observation; use the station launch command | Rasht 3, Ramsar 4 | No | BLOCKED — native desktop surface unavailable; see results document | UI-03, UI-06 |
| MQ-08 | Requalify Pilot-form close guard during active observation; use the station launch command | Rasht 3, Ramsar 4 | No | READY FOR MANUAL REQUALIFICATION — C4 fix applied; no manual PASS claimed | UI-04, UI-06 |
| MQ-09 | Independent 100% DPI lifecycle; use the station launch command | Rasht 3, Ramsar 4 | No | BLOCKED — native desktop surface unavailable; see results document | UI-05, UI-06 |
| MQ-10 | Independent 125% DPI lifecycle; use the station launch command | Rasht 3, Ramsar 4 | No | BLOCKED — native desktop surface unavailable; see results document | UI-05, UI-06 |
| MQ-11 | Independent 150% DPI lifecycle; use the station launch command | Rasht 3, Ramsar 4 | No | BLOCKED — native desktop surface unavailable; see results document | UI-05, UI-06 |
| MQ-12 | Confirmation cancel, keyboard/RTL, fields and traceability; use the station launch command | Rasht 3, Ramsar 4 | No | BLOCKED — native desktop surface unavailable; see results document | UI-06 |

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
