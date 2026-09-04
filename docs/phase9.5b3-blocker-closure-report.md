# Phase 9.5B3 - Blocker Closure Report

Status: **PHASE 9.5B3 COMPLETE WITH MANUAL QUALIFICATION REQUIRED**  
Date: 2026-09-04  
Branch: `phase9-operational-readiness`  
Starting commit: `2978959`  
Scope: Phase 9.5B3 only

## 1. Authoritative B3 scope

The Phase 9.5B1 closure plan defines Phase 9.5B3 as the narrow crash-safe verified backup, restore, and rollback-copy boundary:

> Implement one ManagementCredential-bound, receipt-producing path for SQLite-consistent backup acceptance and staged restore/replacement. Create and verify an immutable rollback copy before replacement; handle WAL/journal sidecars; validate before/after swap; recover deterministically from injected failures. Do not migrate production data or change authority.

This B3 execution followed the B2 frozen decisions for exact path binding, protected-action authorization, correlation and credential-version binding, SQLite-consistent backup, quiescence, sidecar recording, staged replacement, pre/post validation, rollback-copy custody, and fail-closed failure handling. No plan redesign was performed. B4 and later phases were not started.

### Scope decisions recorded before implementation

| Item | B3 decision |
|---|---|
| Primary gates | `DB-03`, `BR-02`, `BR-03`, `BR-05`, `BR-06` |
| Supporting gates | Supports `AUTH-04` and future `BR-04`; it does not close either gate by itself. |
| Initial states | All five primary gates were `BLOCKED` after B2. |
| B2 dependencies | B2 decision contracts and the user's explicit B3 implementation authorization; exact ManagementCredential proof; explicit source, backup, destination, and rollback paths; quiesced writers; isolated/disposable data only. |
| Expected evidence | Focused authorization/binding tests; checksum, integrity, and foreign-key checks; WAL-aware receipt; same-path and destination rejection; staged swap and interruption/failure-injection evidence; rollback-copy identity; post-restore checks; isolated rehearsal; code/diff record. |
| Production code permitted | Yes, only backup/restore/protected-action composition and directly supporting code. No schema change. |
| Manual qualification | Yes. It must be performed against disposable isolated copies; no production database is required or permitted for B3 qualification. |
| Completion criteria | Exact ManagementCredential binding; verified SQLite backup and receipt; distinct paths; verified rollback copy before replacement; WAL/journal sidecar handling; staged hash/integrity validation; atomic replacement; deterministic recovery from injected failures; post-swap validation; no authority or production-data change. |

The B2 report stated that stakeholder sign-off was still a manual prerequisite. This execution uses the B2 decisions as the frozen technical baseline and records the remaining stakeholder/operator exercise below; it does not represent that manual approval as completed.

## 2. Changes made

### Protected boundary

Added `ManagedSqliteBackupRestoreBoundary`, exposed through `IManagedSqliteBackupRestoreBoundary`, with two explicit operations:

1. `CreateVerifiedBackupAsync` requires a singleton ManagementCredential proof for `ProtectedAction.BackupPolicy`. The proof is checked against the exact normalized source path, destination path, overwrite policy, correlation ID, current credential version, and validity window.
2. `RestoreAsync` requires a singleton ManagementCredential proof for `ProtectedAction.Restore`. The proof is checked against the exact normalized backup path, expected SHA-256, destination path, rollback-copy path, correlation ID, current credential version, and validity window.

The boundary rejects missing or invalid paths, invalid checksums, missing source/destination files, same-path or overlapping artifact paths, and pre-existing generated artifacts. It does not discover a database path or use an implicit production path.

### Backup receipt and verification

The managed backup operation uses the existing SQLite Backup API foundation and read-only full preflight. It records a typed, non-secret receipt containing correlation, action scope, safe explicit paths, hashes, size, journal mode, WAL/SHM presence/size/hash, integrity result, foreign-key result, status, and UTC time. Source sidecars are observed and rechecked around backup; an observed sidecar change is rejected. The shared file-hash helper now allows read sharing needed to inspect a SQLite sidecar while an isolated SQLite handle is open.

### Restore and rollback-copy boundary

The restore operation:

1. validates the exact retained backup checksum, SQLite header, full integrity, foreign-key state, and migration classification;
2. validates the existing destination read-only before mutation;
3. creates a distinct SQLite-consistent rollback copy with a new path and verified checksum before replacement;
4. copies the retained backup to a same-directory staging path, flushes it, and validates its checksum/integrity/FK state;
5. rechecks destination sidecars and moves the prior live database plus any matching WAL/SHM sidecars to a distinct prior-live archive;
6. renames the validated staged file into the destination path; and
7. validates the replacement read-only and confirms its hash equals the approved backup hash.

Only the main database file is staged. Active sidecars are never copied into the staged destination. Prior sidecars are preserved with the prior-live archive before the swap. The verified rollback copy is retained separately from the prior-live archive.

Injected failures are supported after rollback-copy creation, after staging, after moving the prior live database, and after the swap before validation. Recovery moves any replacement to a distinct failed-artifact path, restores the prior live database and its sidecars, removes only the ephemeral staging copy, and returns a receipt. If recovery itself cannot complete, the result is `RecoveryFailed` and the operation does not claim a safe outcome.

No authority state, routing decision, migration ledger, schema, finalized snapshot, report lock, event, ESD value, or production data is changed by this boundary.

## 3. Files changed

| File | Change |
|---|---|
| `Application/Database/Readiness/SqliteBackupRestoreBoundaryContracts.cs` | B3 boundary contracts, receipts, failure states, failure-injection points, exact protected-action scope binding, and interface. |
| `Infrastructure/Database/Readiness/ManagedSqliteBackupRestoreBoundary.cs` | ManagementCredential-bound verified backup and staged restore implementation, sidecar handling, pre/post checks, rollback-copy creation, and deterministic recovery. |
| `Infrastructure/Database/Readiness/SqliteBackupAndRestoreServices.cs` | Read-only hash access was widened to `ReadWrite | Delete` sharing so isolated WAL/SHM evidence can be captured without changing database contents. |
| `Rah_Negar.Tests/Database/ManagedSqliteBackupRestoreBoundaryTests.cs` | Three focused disposable-database regression tests. |
| `docs/phase9.5b3-blocker-closure-report.md` | This closure report. |

No existing business workflow, WinForms route, startup behavior, schema migration, or production database file was modified.

## 4. Tests and evidence created

The focused B3 test class contains three tests:

- exact ManagementCredential action/scope binding, WAL-mode backup content, receipt fields, and denial of a mismatched scope;
- verified rollback-copy creation, distinct artifact identity, staged replacement, pre/post validation, and preservation of the original destination in the prior-live archive; and
- injected post-swap failure, deterministic recovery of the original live database, retained rollback copy, and failed-artifact separation.

The tests use temporary SQLite files outside the application `Data` directory. They do not use production data, the production `Data/db.sys`, a production credential, or an authority transition.

Existing readiness tests continue to cover checksum mismatch, corrupt SQLite, full integrity/FK checks, committed WAL content, same-path rejection, overwrite denial, isolated rehearsal, finalized snapshot/lock preservation, and Legacy ESD authority.

## 5. Manual qualification required

Manual qualification remains required before the B3 implementation evidence can be treated as an operationally qualified gate closure. The minimum deterministic checklist is:

1. Use a newly generated Rasht or Ramsar qualification fixture, copied to a disposable directory outside `Data`; record the fixture path and station shape.
2. Confirm all writers are stopped, no Pilot process can write, the database handle is quiesced, and the source journal mode plus WAL/SHM evidence are recorded.
3. Issue a current singleton ManagementCredential proof through the approved isolated harness only. Bind it to the exact action, path binding, correlation ID, credential version, issue time, and expiry. Do not place the credential or proof secret in the report.
4. Run verified backup acceptance to a fresh destination and retain the typed receipt, SHA-256, size, integrity/FK outcome, journal mode, and sidecar evidence.
5. Repeat with a wrong action, wrong scope, wrong correlation, expired proof, wrong credential version, same source/destination, and pre-existing artifact. Each must be denied without changing the source or destination.
6. Prepare a disposable destination with known prior data. Run the allowed restore. Confirm the destination equals the approved backup, the rollback copy equals the pre-restore destination, the prior-live archive exists, sidecars were not copied into staging, and full integrity/FK checks pass before and after the swap.
7. Repeat the restore with each failure-injection point. Confirm the original destination is readable and unchanged after recovery, no ambiguous live path remains, the rollback copy remains available, and the failed artifact is separately identified.
8. Record operator, reviewer, correlation, artifact identities, timestamps, outcomes, stop conditions, and any failure. Keep secrets and raw database contents out of ordinary evidence.

This is an isolated boundary rehearsal, not a production restore. No current application UI is wired to this new B3 boundary; B4 remains responsible for the broader target security/composition work.

## 6. Production-only evidence still required

The following cannot be honestly produced from fixtures and remain unresolved:

- `DB-03`: production installation binding, approved restore owner, and production-bound restore procedure evidence;
- `BR-02`: production artifact/custody binding and retained verified receipt;
- `BR-03`: production protected-action composition and exact production authorization evidence;
- `BR-05`: production rollback-copy location, custodian, retention, and evidence binding;
- `BR-06`: production quiescence, sidecar state, replacement, and failure-recovery evidence; and
- `BR-04`: restoration of the exact selected production backup under the exact candidate binary.

No production database was accessed, copied, restored, migrated, replaced, or mutated. No production-only result is fabricated or implied.

## 7. Gate disposition

### B3 local implementation evidence addressed

The B3 implementation blockers for `DB-03`, `BR-02`, `BR-03`, `BR-05`, and `BR-06` are addressed in code and focused tests. The five gates are not promoted to final cutover `READY` because the required isolated manual qualification and later production binding remain outstanding.

### Gates closed

**Fully closed for production readiness: none.**

**B3 local implementation closure recorded:** `DB-03`, `BR-02`, `BR-03`, `BR-05`, `BR-06`.

### Gates remaining

`DB-03`, `BR-02`, `BR-03`, `BR-05`, and `BR-06` remain pending manual qualification and production-bound evidence. `AUTH-04` remains a later authority/rollback-transition dependency. `BR-04` remains production-only and conditional. No B4 gate was addressed.

## 8. Safety-boundary verification

- Legacy remains authoritative. No authority transition, target routing enablement, automatic switch, cutover, migration, or production acceptance occurred.
- The pilot remains read-only where designed. No pilot writer, restore, migration, authority, settings, ESD, finalization, or export capability was added.
- Normal authentication remains ShiftProfile-based. The boundary accepts only a previously issued singleton ManagementCredential proof for the protected operation; it does not add a login identity.
- No Administrator, Engineer, Operator, Viewer, Support, RBAC catalog, support login, backdoor, universal credential, master password, or alternate identity was introduced.
- Event types remain `START`, `NSD`, `ESD`, and `OH`.
- Finalized historical report snapshots and report locks are not rewritten or reopened by this change.
- Rasht/Ramsar station-specific logic and the supported 3-unit/4-unit production scope are unchanged.
- Qualification artifacts are temporary isolated SQLite copies outside `Data`; no production data was used.
- No SQLite schema or migration was changed. No destructive production operation was executed.

## 9. Validation record

| Validation | Result |
|---|---|
| Focused B3 tests | **PASS** - 3 passed, 0 failed. |
| `dotnet build Rah_Negar.sln -c Release` | **PASS** - build succeeded, 0 errors, 12 warnings. Warnings are existing NU1701 compatibility warnings for OpenTK/OpenTK.GLControl/SkiaSharp.Views.WindowsForms; no B3 compiler warnings remained. |
| `dotnet test Rah_Negar.sln -c Release` | **PASS** - 655 passed, 0 failed, 0 skipped. |
| `git diff --check` | **PASS**. Git emitted only a line-ending normalization warning for the pre-existing service file. |
| Production data access or mutation | **None**. |
| Commit or push | **None**. |

## 10. Change classification

| Item | Result |
|---|---|
| Production code changed | **Yes**, only the B3 backup/restore boundary and directly supporting sidecar hash access. |
| Test code changed | **Yes**, focused B3 regression coverage. |
| Qualification tooling changed | **No**. Existing isolated qualification infrastructure was not weakened or broadened. |
| Documentation changed | **Yes**, this report only. |
| Database schema changed | **No**. |
| Production data accessed | **No**. |
| Production authority changed | **No**. |
| Automatic authority switch introduced | **No**. |
| ManagementCredential identity model changed | **No**. |
| RBAC/support identity introduced | **No**. |

## 11. Readiness for Phase 9.5B4

B3 local implementation work is complete, but the B3 manual isolated qualification record and production-bound evidence are still required. B4 must not be treated as production cutover authorization. The B4 scope remains separate: target ShiftProfile-only composition, singleton ManagementCredential composition for the complete protected-action inventory, bounded recovery, vendor ESD authorization, audit wiring, and legacy bypass isolation behind the inactive boundary.

This report does not begin B4, authorize B4 implementation, enable target authority, or authorize production operations. B3 is ready for its manual qualification handoff only.

## 12. Exact final status

**PHASE 9.5B3 COMPLETE WITH MANUAL QUALIFICATION REQUIRED**
