# Phase 9.6D — Cutover / Abort / Rollback Design

Status: **DESIGN ONLY — PRODUCTION CUTOVER UNAUTHORIZED**

This package defines a future fail-closed operating model. It does not implement or execute a cutover, enable Target routing, change startup selection, or alter a database.

## 1. Non-negotiable current state

The current production interpretation is:

| Boundary | Current state |
|---|---|
| Legacy authority | AUTHORITATIVE |
| Target authority | NON-AUTHORITATIVE |
| Target routing | DISABLED |
| Production Activation | UNAUTHORIZED |
| Production Cutover | UNAUTHORIZED |
| MQ-07 | **BLOCKED — MANUAL OBSERVATION NOT PRACTICALLY EXERCISABLE, WITH AUTOMATED INVARIANT EVIDENCE RETAINED** |

The Phase 9.6B2 Project Owner acceptance is a narrow, auditable treatment of MQ-07's residual limitation. It is not a PASS, generic waiver, role, bypass, or activation authorization. Independent Human Review remains NOT PERFORMED / UNAVAILABLE. The supported unit boundary remains 3–5 inclusive; 35 is not supported. Rasht/Ramsar remain legacy, qualification, migration, and test fixtures, not Production selectors.

## 2. Repository truth and design implications

The current code has useful bounded foundations but no safe production cutover executor:

* `ManagedSqliteBackupRestoreBoundary` operates only on explicit paths, verifies preflight/checksums/sidecars, creates a rollback copy, stages a replacement, and has restore fault-injection points. It does not change authority or routing.
* `ProductionMigrationExecutor` binds an explicit database and verified backup, runs `MigrationRunner` transactionally, validates the ledger and preservation, and explicitly leaves Legacy authoritative and Target routing disabled. A committed migration followed by failed validation returns a rollback-required result; it does not perform an authority rollback.
* `MigrationRunner` owns the SQLite migration transaction and the checksum validator protects the migration chain. This is migration atomicity, not cross-database authority atomicity.
* `ProductionActivationEligibilityBoundary` persists an eligibility/audit evidence line and guarantees `ActivationExecuted=false`, `TargetAuthorityAccepted=false`, and `LegacyRemainsAuthoritative=true`. Its current request validation still restricts scope to `station-rasht`/`station-ramsar`, which conflicts with the frozen generic Production boundary and must be resolved before activation work.
* `TargetSecurityCompositionDescriptor.Inactive` and the target composition expose disabled routing and Legacy preservation. `IntegrationAuthorityRoutingPolicy` contains conceptual modes including `FullTarget`, but the current Production composition does not make that mode operational.
* Startup currently uses the normal explicit application database path (`Data\db.sys`) through the existing application setup/connection path. There is no canonical authority-record resolver, crash resolver, or safe startup behavior for an ambiguous transition.
* Qualification scripts create disposable Rasht/Ramsar databases outside the application `Data` directory, copy a fixture into a temporary run, and record `productionDatabaseUsed=false`/`productionAuthorityChanged=false`. They do not qualify a production transition.
* Existing event/report audit and snapshot checksums are domain-specific. The file activation evidence store appends durable JSON evidence, but it is not a same-transaction authority receipt.

These observations are repository evidence, not claims that the existing foundations are production-ready.

## 3. Future lifecycle state machine

The future implementation shall have one canonical, integrity-protected authority record and append-only transition history. It must reject impossible combinations rather than normalize them. Recommended states are:

1. `PRE_CUTOVER` — Legacy authoritative, Target non-authoritative, Target routing disabled.
2. `CUTOVER_PREPARED` — all prepare artifacts and a correlation-bound intent exist; authority has not changed.
3. `CUTOVER_COMMITTING` — a short, exclusive maintenance boundary is active; no operational write or route may proceed.
4. `CUTOVER_COMMITTED` — the durable canonical record says Target is authoritative; routing is still blocked until the committed projection is verified.
5. `POST_CUTOVER_VERIFICATION` — Target authority is recorded, route projection and all required checks are being verified under the maintenance boundary.
6. `ABORTING` — a pre-commit intent is being closed without an authority transition.
7. `ROLLBACK_PREPARED` — a post-commit rollback has been separately authorized and all data/reconciliation conditions have been demonstrated.
8. `ROLLBACK_IN_PROGRESS` — no normal operational service; the authorized rollback/reconciliation procedure is executing.
9. `RECOVERY_REQUIRED` — the last safe authority, route, database, audit, or commit outcome cannot be proven; all normal service is blocked.
10. `STABLE_TARGET_AUTHORITY` — Target authority and routing agree, verification is complete, and no dual authority exists.

`STABLE_TARGET_AUTHORITY` is a future terminal state only. It is not the current state and cannot be reached by an eligibility receipt, rehearsal, test, owner acceptance, or manual database flag.

## 4. Cutover entry conditions

Every condition below is mandatory, current, authentic, scope-bound, and correlated. Absence, expiry, contradiction, or inability to verify blocks entry:

* the Phase 9.6 prerequisite result permits an activation decision, with MQ-07 treated only under its exact 9.6B2 exception;
* explicit future Production Activation authorization and separate Project Owner/governance authorization exist, with expiry, revocation, scope, version, database, evidence, and correlation binding;
* authority-transition prerequisites and an approved runbook are satisfied;
* an immutable, verified backup exists and its restore path has passed isolated verification;
* the migration ledger, checksums, order, current/final schema version, and idempotency evidence are valid;
* Target integrity, foreign-key integrity, schema version, profile/security state, and structural fingerprint are valid;
* Target data reconciliation is complete, including main data, daily unique values, events, finalized snapshots, locks, credentials, and ESD values;
* the exact 3–5 Unit boundary is valid; no value of 35 is accepted;
* no authority ambiguity exists; Legacy is still authoritative and Target is non-authoritative;
* Target routing is still disabled;
* the audit sink is writable and read-back/verifiability is proven;
* a unique correlation ID, deployment/station scope, software/build version, schema version, explicit database identities, and operator/governance evidence are assigned and mutually bound;
* ShiftProfile initiation and fresh action-bound ManagementCredential proof validate. No RBAC, Support identity, master password, universal bypass, or generic STOP event is introduced.

The repository does not currently prove all of these conditions. In particular, the canonical authority store/executor, production crash resolver, complete audit integration, production-like reconciliation, generic activation scope, and approved rollback procedure are unresolved.

## 5. PRE-CUTOVER and CUTOVER_PREPARED

Preparation may perform only reversible, evidence-producing work:

1. acquire an exclusive transition lease and assign a correlation ID;
2. record an intent containing old authority, explicit Legacy/Target identities, scope, build/schema versions, authorization references, and expiry;
3. verify the immutable backup, WAL/SHM handling, SQLite integrity, foreign-key integrity, checksum, ledger, schema, profiles, Unit boundary, and Target reconciliation;
4. re-evaluate all prerequisites immediately before commit and reject stale eligibility;
5. run a final synchronization/fence check and record the write boundary;
6. verify audit preflight/readback and the route lock;
7. persist `CUTOVER_PREPARED` only if the complete package is internally consistent.

The current architecture writes operational data directly through Legacy services and SQLite connections; Target is a preparation/rehearsal surface, not a live synchronized replica. Therefore the recommended future strategy is an application shutdown/maintenance boundary: stop accepting operational writes, drain/close Legacy connections, capture the final Legacy backup, validate Target against that boundary, then commit. A live dual-write, delta replay, or guessed “last write” strategy is not compatible with the evidence currently present and must not be invented during implementation. The boundary must be visible in normal production timing, not a debug-only delay.

An abort before the durable authority commit is preferred whenever any check fails. Preparation must never enable Target routing or make Target authoritative.

## 6. CUTOVER_COMMITTING and atomic boundary

The smallest safe logical boundary is one canonical `AuthorityStateRecord` in a protected system-owned store, written transactionally with its transition receipt and audit record. It should include revision, state, prior state, Legacy and Target identity fingerprints, active database identity, route-projection revision, scope, application/schema versions, correlation/intent IDs, authorization references, UTC timestamps, lease, and an integrity tag/hash. Append-only history is required.

Multiple independent booleans are prohibited as the source of truth. Combinations such as Target routing enabled while Target is not authoritative, both authorities true, or neither authority true are impossible states and must enter `RECOVERY_REQUIRED`.

SQLite transaction atomicity cannot make a database-file replacement, an external audit file, application process state, and route registration one physical atomic operation. The implementation must use an exclusive maintenance lock and deterministic startup reconciliation. It must not claim stronger atomicity than the repository/filesystem can provide.

Ordering constraints:

1. Legacy remains authoritative and Target routing remains disabled through prepare.
2. Enter `CUTOVER_COMMITTING`; block operational requests.
3. Verify the final Target artifact and write the single canonical record changing authority to Target. Legacy is retained as the prior recovery source; it is not independently toggled first.
4. Persist/read back the committed receipt and integrity tag.
5. Project routing from the committed canonical record only after the record and Target validation agree.
6. If route projection cannot be proven, keep all normal routes blocked and enter `RECOVERY_REQUIRED`; never fall back by guessing.
7. Only after post-commit verification may the state become `STABLE_TARGET_AUTHORITY`.

No committed stable state may contain dual authority. No state may contain `TargetRouting=ENABLED` without valid Target authority.

## 7. Post-cutover verification

Before normal Target service, verify and receipt all of the following: canonical state/revision and integrity tag; route projection and route lock; Target openability, SHA-256, SQLite integrity, foreign-key integrity, schema/ledger/version; key data availability; report/snapshot reads and finalized locks; authentication using ShiftProfile and ManagementCredential proof boundaries; audit receipt persistence/readback; restart persistence; Legacy non-authoritative retention; no split-brain indicators; and no unexpected writes to the preserved Legacy source.

Failure classification is explicit:

* a failed check before any Target Production write and with a proven committed record is `ROLLBACK_ALLOWED` only if the rollback eligibility contract below is satisfied;
* a failed check with uncertain commit outcome, missing/corrupt receipt, route/authority mismatch, or damaged databases is `RECOVERY_REQUIRED`;
* a failed Target validation with no committed authority is an `ABORT`.

## 8. ABORT (pre-commit only)

Abort is not rollback. It is valid from `CUTOVER_PREPARED`, and from `CUTOVER_COMMITTING` only when the implementation proves the canonical authority commit did not occur. It must be idempotent and correlation-bound.

Abort closes the intent/lease, removes only uniquely identified staging artifacts, preserves the immutable source backup and evidence, releases the maintenance lock, and appends an abort receipt. It must re-read and confirm `Legacy=AUTHORITATIVE`, `Target=NON-AUTHORITATIVE`, and `Target Routing=DISABLED`. Repeated abort returns the same completed result; a conflicting or incomplete prior state enters recovery.

If a crash interrupts abort, startup must inspect the intent, staging/prior artifacts, canonical record, route projection, and receipts. It may complete a proven-safe cleanup; otherwise it enters `RECOVERY_REQUIRED`. It must never delete an unknown artifact or infer that cleanup succeeded. The intent is retained as closed/aborted evidence, not erased.

## 9. ROLLBACK after committed Target authority

Rollback is a new authority transition, not a file copy or silent fallback. It is safe only when all of the following are proven: Target authority was committed exactly once; a verified prior Legacy image and restore path exist; no unaccounted Target-authoritative Production writes occurred, or every such write is captured and reconciled; finalized reports/snapshots and audit lineage are preserved; a fresh owner/governance authorization exists; an exclusive write fence is active; and post-restore validation will prove Legacy is complete and consistent.

Definitions:

* `ROLLBACK_ELIGIBLE`: committed Target authority is proven, no post-commit write occurred or all writes are durably captured for an approved lossless reconciliation, backup/restore is verified, and the rollback authorization/scope/correlation is fresh.
* `ROLLBACK_NOT_SAFE`: Target received new operational writes, finalized reports, runtime/event records, or Target-only changes whose complete reconciliation has not been proven. Automatic rollback is forbidden. Data must not be silently discarded.
* `RECOVERY_REQUIRED`: commit outcome, authority metadata, audit receipt, route state, database integrity, or write lineage is uncertain/corrupt, or the verified restore path is unavailable/failed.

The point of no return for automatic rollback is the first accepted Target-authoritative Production write (including a finalized report or event). After that point, a write freeze and explicit reconciliation must establish a complete lineage: identify every Target delta, map it to Legacy records under the existing business rules, preserve audit/snapshot history, resolve conflicts and finalized-period locks, validate counts/checksums, and obtain governance acceptance. Only then may a new canonical transition legitimately return authority to Legacy. A stale Legacy backup is not a substitute for reconciliation.

Rollback sequence: prepare/authorize; fence writes; back up and hash the current Target; verify the prior Legacy backup; restore to a distinct staging destination using the existing protected restore contract; validate integrity/schema/ledger/data/locks/audit; commit the canonical state to Legacy while routing remains disabled; project Legacy routing only after verification; restart and re-verify. Any uncertainty stops in recovery.

## 10. RECOVERY_REQUIRED

`RECOVERY_REQUIRED` is a first-class fail-closed state. It covers corrupted authority metadata, failed/uncertain commit, route/authority mismatch, Target integrity failure after commit, missing/corrupt audit receipt, crash during cutover or rollback, invalid ledger, unavailable backup, failed restore, both databases available with uncertain authority, and neither database safely usable.

Startup must not guess from file timestamps, preferred paths, a UI flag, “newest” database, or whichever file opens. It must load and validate the canonical record/history, verify hashes and route projection, inspect both explicit database identities, and require an authorized recovery procedure. If the result cannot prove one stable authority, normal data entry, reporting mutation, and both ambiguous routes are blocked. The UI must show a safe “Recovery required; operational access is blocked” state with correlation/reference and next controlled action, without exposing secrets or offering a bypass.

If a verified package proves Legacy authoritative, the system may restore the known Legacy state under explicit ManagementCredential/governance authorization and audit. If Target authority is proven and healthy, it may complete post-cutover verification. If neither is safe, preserve both artifacts, quarantine the attempt, and await an offline recovery decision. Recovery is never an automatic fallback.

## 11. Backup / restore contract

Immediately before a future cutover, the verified backup manifest must bind: source identity and explicit path; destination identity/path; UTC creation timestamp; correlation ID; deployment/station scope; software/build and schema/migration versions; source and backup SHA-256; source/backup sizes; journal mode and WAL/SHM evidence; SQLite header/openability; full `integrity_check`; `foreign_key_check`; structural fingerprint; and immutable retention/reference.

A backup is `VERIFIED` only when the source preflight passes, sidecar/WAL handling is stable or explicitly checkpointed under a controlled boundary, the copy completes without collision, source and backup hashes are recorded, the backup opens read-only, SQLite integrity and foreign-key checks pass, schema/ledger/version and scope match, and an isolated restore/revalidation has passed. The existing MQ-01 boundary supports much of this contract, including SHA-256, preflight, sidecar evidence, explicit paths, distinct restore artifacts, staged replacement, and fault injection; it does not by itself establish authority rollback.

After rollback/recovery, verify the restored destination with the same integrity/FK/schema/ledger/checksum checks, compare the expected database identity and data reconciliation manifest, verify reports/snapshots/locks/authentication/audit, retain the pre-restore Target copy, and issue a restore receipt. Restore failure leaves `RECOVERY_REQUIRED`.

## 12. Operator control model

The operator initiates a named, scope-bound request using `ShiftProfile`. Fresh `ManagementCredential` proof authorizes the protected operation. Project Owner/governance authorization separately approves the future Production Activation/Cutover or rollback decision. The system independently validates prerequisites, database identities, scope, version, correlation, expiry, replay, integrity, routing, and audit. No actor credential silently bypasses a failed check; no role, Support identity, RBAC, master password, or universal recovery code is added.

Operator initiation, privileged proof, governance authorization, and system validation are separate evidence fields and separate failure points. Abort may be initiated by the authorized operational workflow before commit; rollback requires a new explicit authorization and reconciliation decision.

## 13. Current consistency/safety audit

The following are current conflicts or misleading historical statements requiring disposition in later work:

* Phase 9.6A/9.6B/9.6C correctly state Legacy authority, disabled routing, unauthorized activation, and MQ-07 BLOCKED; those statements govern this package.
* `ProductionActivationBoundary.ValidateRequest` contains `station-rasht`/`station-ramsar` scope validation, conflicting with the frozen generic Production boundary. It is a confirmed design inconsistency, not a license to add station branching.
* Older Phase 9/roadmap and qualification documents describe Rasht/Ramsar as current scope or preserve superseded 1–35 provisioning evidence. They are historical/fixture evidence; the current boundary is 3–5 inclusive.
* `IntegrationRoutingContracts` contains a conceptual `FullTarget` mode, but the active target composition remains inactive and routing disabled. This must not be read as enabled Target routing.
* Eligibility receipts, migration success receipts, owner acceptance, and rehearsal results are explicitly evidence-only. None says Target is authoritative.
* Historical documents may say “ready,” “closed,” or “pass” for their own phase. They do not mean Production Cutover authorized. MQ-07 must not be relabeled PASS. Independent Human Review remains unavailable.
* Repository searches found no valid generic waiver, RBAC role, or Support identity. Recovery code in the legacy service is not a cutover authorization model and must not be reused as one.

## 14. Design decision and current gaps

The recommended future model is one canonical authority record, an exclusive maintenance/write fence across the physical SQLite/routing boundary, verified immutable backups, deterministic startup reconciliation, and explicit data reconciliation after any Target write. The repository currently lacks the canonical authority store/executor, production route projection, crash resolver, complete transition audit coupling, and qualified write-boundary/reconciliation procedure.

**BLOCKED  Production-like rehearsal, canonical authority/cutover executor, crash-safe startup resolver, complete transition audit receipt, generic scope correction, write-fence/final-synchronization evidence, rollback reconciliation procedure, and future governance authorization are not yet implemented or qualified; MQ-07 remains BLOCKED and Independent Human Review remains NOT PERFORMED / UNAVAILABLE.**
