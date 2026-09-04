# Phase 9.5B2 — Blocker Closure Report

Status: **PHASE 9.5B2 COMPLETE WITH MANUAL QUALIFICATION REQUIRED**
Date: 2026-09-04
Branch: `phase9-operational-readiness`
Starting commit: `14d05c1`
Authority: `docs/phase9.5b1-cutover-blocker-closure-plan.md`

## 1. Executive result

Phase 9.5B2 was executed as a documentation/specification-only task, exactly as assigned by the Phase 9.5B1 closure plan. The report freezes the B2 operational decisions in a reviewable form and provides numbered runbook specifications, an owner matrix, no-secret evidence rules, and testable acceptance criteria for the next separately authorized implementation work.

No production code, test code, database schema, production data, startup behavior, authority state, or package declaration was changed. No production database was accessed. No migration, restore, replacement, rollback, target routing, automatic activation, commit, or push was performed.

The B2 contract work is complete as an evidence artifact. The related Phase 9.5A gates are not marked READY or closed by this report. They still require the implementation, isolated tests/rehearsals, production-bound evidence, and human approvals identified below. This is why the final status is **PHASE 9.5B2 COMPLETE WITH MANUAL QUALIFICATION REQUIRED** rather than plain COMPLETE.

## 2. Authoritative Phase 9.5B2 scope

The following scope is copied faithfully from Section 10 of the Phase 9.5B1 plan:

> ### Phase 9.5B2 — Authority, recovery, restore, and rollback decision contracts
>
> **Narrow scope:** Approve and freeze the operational decisions needed before implementation: authority states and decision owners; target-to-Legacy routing; target-authoritative write boundary; rollback triggers and maximum decision time; backup/restore custody; quiescence and SQLite sidecar handling; protected-action inventory; ManagementCredential recovery policy; vendor public-key/device custody; audit retention; station-specific provisioning ownership. Produce numbered runbook specifications and testable acceptance criteria only.
>
> **Primary gates advanced:** AUTH-03, AUTH-04, DB-03, SEC-02–SEC-05, SEC-08, BR-02–BR-06, MIG-04, MIG-06, OPS-01 template.
>
> **Expected evidence:** Approved decision record; threat/safety review; action and route inventory; state/rollback diagrams; exact owner matrix; no-secret evidence rules; acceptance-test matrix; explicit statement that Legacy remains authoritative.
>
> **Production code may change:** **No.** Documentation/specification only.
>
> **Human manual testing required:** **No application testing**, but **yes** for management, data-owner, security, rollback-owner, and operator review/approval.
>
> **Expected risk:** **Medium** procedural risk; no runtime/data risk. Incorrect decisions would propagate into critical implementation, so unresolved decisions stop the sequence.

The B1 plan also states that the tasks in its Section 10 are not authorized by B1 itself and require their own scope/approval. This report records B2 decisions and evidence only; it does not authorize B3 or any production operation.

## 3. B2 gate scope and initial states

The initial states below are taken from the Phase 9.5A gate inventory as summarized by the B1 plan. “Advanced” in the B1 wording means that B2 supplies prerequisite decisions and acceptance criteria; it does not mean the Phase 9.5A gate is closed.

| Gate ID | Initial state | B2 treatment | State after B2 | Why the gate is not closed here |
|---|---|---|---|---|
| AUTH-03 | BLOCKED | Authority state, decision point, owner, routing, and acceptance contract | BLOCKED | Production authority adapter/executor, durable persistence, audit wiring, and later production binding are absent. |
| AUTH-04 | BLOCKED | Rollback trigger, owner, data boundary, maximum decision time, and runbook | BLOCKED | Rollback implementation, target-interval data handling, rehearsal, and production evidence are absent. |
| DB-03 | BLOCKED | Restore authorization, custody, quiescence, sidecar, and staged-replacement decisions | BLOCKED | A restore implementation/procedure and isolated execution evidence are still required. |
| SEC-02 | BLOCKED | Complete protected-action inventory and ManagementCredential proof requirements | BLOCKED | Production composition and negative/atomicity qualification are still required. |
| SEC-03 | BLOCKED | Bounded ManagementCredential recovery/reset policy and audit requirements | BLOCKED | Recovery implementation, rehearsal, and human authorization are still required. |
| SEC-04 | BLOCKED | Vendor public-key/device custody and ESD authorization boundary | BLOCKED | Production provisioning, protected executor composition, and fail-closed rehearsal are still required. |
| SEC-05 | BLOCKED | Audit event, durability, retention, and failure-atomicity decisions | BLOCKED | End-to-end production audit wiring and retention operation are still required. |
| SEC-08 | BLOCKED | Forbidden-secret, forbidden-identity, and legacy-recovery isolation rules | BLOCKED | Final-binary review and target-routing isolation are still required. |
| BR-02 | BLOCKED | Verified backup acceptance, artifact identity, custody, and receipt requirements | BLOCKED | Production wiring or an approved controlled procedure and retained evidence are still required. |
| BR-03 | BLOCKED | Restore ProtectedAction binding and authorization evidence | BLOCKED | ManagementCredential-bound restore execution is still required. |
| BR-04 | CONDITIONAL | Rehearsal entry criteria and exact evidence contract | CONDITIONAL | The exact selected production backup and final binary have not been restored/tested in isolation. |
| BR-05 | BLOCKED | Immutable rollback-copy identity, custody, and creation-before-replacement rule | BLOCKED | Creation, verification, fault testing, and rehearsal are still required. |
| BR-06 | BLOCKED | Crash-safe staged replacement, sidecar handling, and recovery decision contract | BLOCKED | Implementation and interruption/failure-injection evidence are still required. |
| MIG-04 | BLOCKED | Station-specific mapping/provisioning ownership and manifest requirements | BLOCKED | Repeatable implementation, validation, reconciliation, and owner approval are still required. |
| MIG-06 | BLOCKED | Coupling rule between validation, explicit authority acceptance, and rollback | BLOCKED | Production authority adapter/executor and end-to-end rehearsal are still required. |
| OPS-01 | CONDITIONAL | Operational owner matrix/template and required approval fields | CONDITIONAL | Named current production participants, contacts, window, and approvals are time-sensitive manual evidence. |

### Gate accounting

- B2 primary scope: 16 gate IDs.
- Initial BLOCKED: 14 gates.
- Initial CONDITIONAL: BR-04 and OPS-01, totaling 2 gates.
- Primary-gate status closed by this report: **none**.
- B2 prerequisite-contract work item: **complete as documentation evidence, pending human sign-off**.

The shorthand ranges in the B1 plan expand to 14 BLOCKED and 2 CONDITIONAL rows in this B2 scope: AUTH-03, AUTH-04, DB-03, SEC-02, SEC-03, SEC-04, SEC-05, SEC-08, BR-02, BR-03, BR-04, BR-05, BR-06, MIG-04, MIG-06, and OPS-01. The B1 plan’s gate table is the controlling source for each row; no gate state is promoted by this report.

## 4. Frozen B2 decisions

These decisions are the B2 specification baseline. They are not a production implementation and do not change the current application. A future implementation must either conform to them or produce a separately reviewed change to the decision record before qualification.

### 4.1 Authority states and decision owners

The existing `ProductionActivationState` vocabulary remains the reference planning vocabulary. B2 does not add a new application state or infer authority from an existing state. The operational meaning is:

| Planning state or condition | Operational authority | Allowed meaning |
|---|---|---|
| `NotPrepared`, `AssessmentReady`, `BackupVerified`, `RehearsalVerified`, `ApprovalPending` | Legacy authoritative | Preparation and evidence collection only. Target data, if present in an isolated copy, has no production authority. |
| `ApprovedForActivation` | Legacy authoritative | Approval exists for a specific future action, but target authority has not begun. This state cannot itself switch routing. |
| `ActivationInProgress` | Legacy authoritative until explicit post-validation acceptance | The activation procedure is executing under a separate authorization. No target-authoritative write is allowed before the DB-05 hold point passes and acceptance is recorded. |
| `Activated` | Target authoritative only after explicit acceptance | This state is not reachable in B2 and is not reachable merely because migration, rehearsal, restart, or a readiness result succeeded. |
| `ActivationBlocked` | Legacy authoritative | Fail closed, preserve evidence, and stop the sequence. No retry may bypass the failed prerequisite. |
| `ActivationRolledBack` | Legacy authoritative | Target routing is disabled, rollback evidence is retained, and target-interval data has an explicit disposition. This state is not equivalent to deleting evidence or silently restoring an old file. |

The default and all B2/pre-acceptance outcomes are Legacy-authoritative. The only permitted target-authority entry is an explicit, installation-bound, audited acceptance after all mandatory pre/post checks pass. There is no automatic startup activation, migration-triggered activation, Pilot activation, fallback-to-target behavior, or authority inference from a restart.

Decision ownership is represented by human responsibility references in the evidence package, not by application roles or identities:

| Responsibility reference | Required decision or evidence | Application identity rule |
|---|---|---|
| Initiating operator / active ShiftProfile | Starts an approved operational procedure and supplies station scope | ShiftProfile is the sole normal operational identity. It is not a management approver or RBAC role. |
| Management authorization reference | Authorizes a protected action with action, scope, correlation, credential version, and expiry binding | Singleton ManagementCredential is proof only, never a normal login identity. |
| Management approver | Approves the bound decision record and scope | Human reference only; do not create a management user or role. |
| Data owner | Approves source/target preservation, Runtime/Event, report, ESD, and target-interval data disposition | Human reference only; no data-owner role is introduced in the application. |
| Security reviewer | Reviews authentication, recovery, vendor authorization, audit, and forbidden-bypass evidence | Human reference only; no security/RBAC role is introduced. |
| Rollback owner | Owns rollback readiness, trigger decision, artifact accessibility, and recovery record | Human reference only; no rollback account is introduced. |
| Maintenance-window owner | Confirms quiescence, scope, timing, and stop authority | Human reference only. |
| Monitoring owner | Confirms health observations and escalates trigger conditions | Human reference only; no monitoring identity is added. |
| Local support contact | Provides offline contact/escalation information | Contact reference only; there is no Support identity. |

Each human reference must be a safe identifier in ordinary evidence. Names, credentials, passwords, recovery codes, private keys, signed authorizations, and raw personal data must remain in their authorized protected record, not in this Markdown report.

### 4.2 Target-to-Legacy routing

1. Legacy is the current and sole production authority.
2. Pilot remains a read-only observation surface. It cannot write data, execute migration, execute restore, change settings, finalize reports, export authority-bearing artifacts, or switch routing.
3. Target tables, target composition, migration completion, assembled evidence, `ApprovedForPreparation`/readiness outcomes, and process restart cannot activate target authority.
4. Before explicit target acceptance, all production operational reads and writes remain on the established Legacy route. Isolated copies may be used for validation and rehearsal only.
5. A failed, stale, ambiguous, mismatched, expired, or missing decision returns the procedure to a blocked Legacy-authoritative condition. It does not fall back to an unapproved target route.
6. Any future target route must be installation-bound, scope-bound, persisted, audited, and accepted at the authorized decision point. B2 does not implement or exercise that route.

### 4.3 Target-authoritative write boundary

The write boundary is explicit:

- **Before acceptance:** no target-authoritative writes. Migration/provisioning writes, if later implemented, occur only on an approved isolated copy or within a separately controlled non-authoritative preparation boundary.
- **During activation in progress:** no target-authoritative writes. Legacy remains authoritative while validation and the DB-05 hold point are evaluated.
- **After explicit acceptance:** only the exact approved target station, scope, and supported operational entities may write. Events remain limited to `START`, `NSD`, `ESD`, and `OH`. Finalized historical report snapshots and locks remain immutable.
- **On rollback trigger:** stop target writers, preserve the target state and evidence, and prevent new target writes. Do not merge, discard, rewrite, or replay target-interval writes automatically.
- **Before Legacy resumes after a target-authoritative interval:** the target interval must be classified and its data disposition recorded by the data owner. Any approved reconciliation must be explicit, reviewable, and separately verified. If disposition is unresolved, the system remains blocked and the evidence is preserved.

The default rollback data boundary is the last verified Legacy-authoritative restore point plus a preserved target-interval evidence copy. Restoring an old file alone is never considered a complete rollback because authority state, audit, and writes after the boundary must also be addressed.

### 4.4 Rollback triggers and maximum decision time

Rollback or pre-acceptance stop is mandatory when any of the following is true:

1. Backup identity, checksum, SQLite integrity, foreign-key integrity, source stability, custody, or retention proof fails.
2. Exact database path, station scope, journal/WAL condition, binary identity, evidence package, approval, correlation ID, or maintenance window is unknown or mismatched.
3. Restore validation, isolated restore start, migration rehearsal, idempotent rerun, preservation comparison, or post-validation fails.
4. A destructive action lacks exact target, explicit authorization, ManagementCredential proof, verified backup, or rollback owner.
5. Audit cannot be durably recorded before a protected mutation or transition, or an audit failure makes the result ambiguous.
6. Authority state is missing, contradictory, stale, not durably persisted, or not unambiguously Legacy-authoritative before transition.
7. Any automatic/implicit switch, hidden bypass, RBAC/Support identity, master password, universal recovery code, customer-held vendor private key, or unapproved route is discovered.
8. Required ShiftProfile scope, ManagementCredential version/expiry, vendor signature/device/request/value/time binding, or replay protection fails.
9. Monitoring cannot determine health, an approved threshold is exceeded, a required owner cannot be contacted, or the decision boundary is unavailable.
10. Any mandatory gate remains BLOCKED/CONDITIONAL, or any evidence is stale, contradictory, production/test-confused, or not bound to the exact artifacts.

For a future authorized target-authoritative interval, the maximum time from a validated rollback trigger to a recorded rollback/NO-GO decision is **15 minutes**. The timer begins when the monitoring or operator record contains the trigger and correlation ID. If a decision owner cannot be reached or no decision is recorded before 15 minutes, the result is an automatic **fail-closed stop**: no new target writes, escalation to the rollback owner and management approver, and preservation of evidence. This timeout does not authorize an automatic database replacement or an automatic authority switch; the restore/replacement action still requires its separately authorized protected procedure.

### 4.5 Backup and restore custody

The canonical B2 custody decision is local and offline:

- The selected source, verified backup, rollback copy, isolated restore, and evidence package receive separate safe identifiers and one correlation ID.
- The retained verified backup is created from an explicitly selected database path under a quiesced procedure, preferably through the existing SQLite-consistent Backup API foundation. A raw hash of an active `db.sys` is not accepted as sufficient evidence.
- The receipt records source identity, backup identity, SHA-256, size, UTC creation time, SQLite header, journal mode/WAL condition, schema/migration classification, integrity result, foreign-key result, source-stability result, destination, custodian, retention location, and overwrite policy.
- The backup artifact is retained outside the live database path. The destination must not be the source, and an existing retained artifact is not overwritten without an explicit approved policy and new identity.
- The rollback copy is created and verified before any live replacement. It is immutable for the evidence window, has a separate identity and location, and is accessible to the rollback owner without relying on the live database.
- Secrets, private keys, passwords, recovery codes, raw signed authorizations, and raw verifier material are never placed in receipts, logs, screenshots, or ordinary Markdown.

### 4.6 Quiescence and SQLite sidecar handling

The future procedure must use this sequence:

1. Identify the exact station and canonical database path; stop if identity is ambiguous.
2. Announce the maintenance window and stop application writers. Confirm no Pilot or other process can write.
3. Close application database connections and confirm the process is quiesced. Record the journal mode and the presence/size/hash of `-wal` and `-shm` sidecars without deleting them.
4. Produce the verified backup using the approved SQLite-consistent method. Prefer Backup API output that incorporates committed WAL content. If consistency cannot be established, stop.
5. Validate the standalone backup before acceptance: header, full integrity check, foreign-key check, schema/migration classification, source stability, and SHA-256.
6. For restore/replacement, stage the validated standalone database at a distinct destination. Do not copy active sidecars into the staged destination.
7. Preserve the prior live database and any sidecars as identified rollback evidence before replacement. Do not delete ambiguous sidecars as a recovery shortcut.
8. Perform the eventual atomic replacement only through the separately implemented and approved restore boundary while all writers remain quiesced. Validate after opening the replacement and record the result.

An inability to quiesce, an unexpected journal/WAL state, a busy writer, a failed checkpoint/backup, a sidecar identity mismatch, or an interrupted file operation is a stop condition. Sidecar handling must be implemented and fault-tested in B3; B2 only freezes the decision boundary.

### 4.7 Protected-action inventory

The existing `ProtectedAction` values are the complete B2 inventory. Every listed action requires an active initiating ShiftProfile plus a current singleton ManagementCredential proof bound to the exact action, station/scope, correlation ID, credential version, issue time, and expiry. A legacy login-password confirmation is not an acceptable target-authority substitute.

| ProtectedAction | B2 authorization boundary |
|---|---|
| `EditShiftProfiles` | ManagementCredential proof bound to the exact station/profile scope; no role administration. |
| `ChangeProtectedSettings` | ManagementCredential proof bound to exact settings scope; no unbounded settings route. |
| `BackupPolicy` | ManagementCredential proof bound to backup policy scope; ordinary safe backup evidence remains independently verified. |
| `Restore` | ManagementCredential proof bound to exact backup identity, destination, station, and correlation ID. |
| `Migration` | ManagementCredential proof bound to exact database identity, migration/evidence package, and approved context. |
| `ReopenFinalizedReport` | ManagementCredential proof bound to exact finalized period/scope; immutable historical snapshots remain protected. |
| `SecurityConfiguration` | ManagementCredential proof bound to exact security configuration scope; no identity/role creation. |
| `IntegrityRepair` | ManagementCredential proof bound to exact database/repair scope; preservation evidence required before mutation. |
| `SensitiveRawImportExport` | ManagementCredential proof bound to exact artifact and destination; no ordinary export receipt substitutes for authorization. |
| `EmergencyRecovery` | ManagementCredential proof plus the bounded offline recovery procedure and audit; no universal secret. |
| `ChangeEsdAdjustment` | ManagementCredential proof plus signed offline vendor ECDSA P-256 authorization, exact binding, and replay protection. |

The existing ordinary `OperationalAction.ManualBackup` remains a normal operational action under an active ShiftProfile, but a backup intended for restore, migration, or cutover evidence must satisfy the verified receipt/custody rules above. Changing backup policy or invoking restore remains protected. Events remain only `START`, `NSD`, `ESD`, and `OH`; ESD authority does not create a new event type or user identity.

### 4.8 ManagementCredential recovery policy

Management recovery is a bounded reset/rotation ceremony, not password disclosure or identity creation:

1. A recovery request is opened with a reason, station/deployment reference, correlation ID, current time, initiating ShiftProfile reference, and protected evidence location.
2. Protected actions are blocked while the current credential is unavailable, disabled, or under recovery review.
3. A management approver and security reviewer approve the request using human references and an offline protected record. These labels are not application roles.
4. The future recovery executor creates a new singleton ManagementCredential revision from a one-time secret supplied during the approved ceremony. It never displays, logs, derives, or stores a universal recovery code or application-wide master secret.
5. The old revision is retired atomically only after the new verifier is durably written and the recovery audit receipt is durable. Credential version increases monotonically.
6. The new secret is delivered through the approved offline custody channel and is never placed in ordinary evidence. If the ceremony cannot complete atomically, no credential or protected data mutation is accepted.
7. The recovery record contains only safe references, revision numbers, outcome category, timestamps, approver references, and correlation ID.

The legacy deterministic, application-secret-derived recovery path must not be reachable through target authority. B2 does not remove or implement that path; this is an explicit B4 implementation and review dependency.

### 4.9 Vendor public-key and device custody

- Vendor support authorization remains a signed offline ECDSA P-256 artifact. It is not a user, role, Support identity, password, or customer-held private key.
- The application stores only the approved public key reference/material required for verification, with key ID, version, lifecycle state, activation/retirement bounds, and provisioning receipt.
- The customer application never stores or emits the vendor private signing key.
- The provisioned device identity is bound to the deployment/station scope and has a safe identity, provisioning time, and revision. A device record does not create an operator.
- The signed request binds exact device ID, request ID, action `ChangeEsdAdjustment`, proposed ESD value, issued time, expiry, and canonical payload version. The ManagementCredential proof independently binds ShiftProfile, action, scope, correlation, credential version, and expiry.
- Replay consumption and the ESD mutation must be atomic and receipt-producing. Verification, audit, replay consumption, and mutation failures fail closed.
- Key/device provisioning is owned by the security reviewer with data-owner and management approval references, and is later reconciled per Rasht or Ramsar station. No provisioning decision authorizes target routing by itself.

### 4.10 Audit retention

Security and activation audit records are local, offline, append-only, non-secret, and durable. The minimum retention decision is **24 months after the event, or longer where an approved local operational/legal retention rule applies**. No record may be purged during an active evidence, incident, rollback, or review window.

The retained record must support authentication, protected-action allow/deny, proof failure, approval, transition request, transition rejection, guard result, restore/backup receipt, ESD authorization, mutation outcome, rollback decision, and recovery outcome. Metadata is limited to approved safe identifiers and result categories. Passwords, secrets, private keys, recovery codes, raw signed envelopes, and raw verifier bytes are excluded.

Any future retention purge requires an offline retention manifest containing the covered record range, correlation/evidence references, owner reference, timestamp, and approval reference. Purge is not part of B2 execution and is not an application authority switch.

### 4.11 Station-specific provisioning ownership

The production scope remains limited to the two supported station shapes:

| Station | Expected unit scope | Provisioning and reconciliation owner |
|---|---:|---|
| Rasht | 3 units | Data owner, with security review for credentials/key material and management approval for the manifest. |
| Ramsar | 4 units | Data owner, with security review for credentials/key material and management approval for the manifest. |

Provisioning must be repeatable, idempotent, station-bound, and complete for active ShiftProfiles, internal credentials, the singleton ManagementCredential, device/public-key references, trusted Runtime Baselines, Events, ESD value, finalized report snapshots, and finalized locks. It must validate no cross-station mapping, no duplicate personnel/profile assignment, no missing unit, no invented RBAC/Support identity, and no mutation of finalized historical snapshots.

The manifest must contain safe entity references, revisions, counts, hashes/fingerprints where applicable, source/target mapping disposition, approver references, and correlation ID. It must not contain credentials, secrets, private keys, or raw personal data. Exact production mapping and provisioning remain future production-bound evidence.

## 5. Numbered runbook specifications

These are specifications for future separately authorized execution. They are not executable commands and do not authorize live database operations.

### RB-01 — Decision package and authority boundary

**Preconditions:** One supported station scope; one candidate binary set; one database/backup identity; one correlation ID; current owners and approvals; Legacy visibly authoritative.

1. Create the package ID and bind station, database, binary, maintenance window, correlation ID, and UTC timestamps.
2. Verify every mandatory gate and stop condition has a current evidence reference.
3. Verify the authority state is Legacy-authoritative and target features are disabled.
4. Obtain the approved management decision reference and exact scope/expiry binding.
5. Record the decision. Do not activate target authority; the package is evidence, not permission.

**Stop:** Any missing/mismatched/stale artifact, approval, owner, identity, or gate.
**Expected result:** `DecisionPackageAcceptedForProcedure` or `DecisionPackageBlocked`; neither result changes authority.
**Owner:** Management approver with operator and security reviewer references.

### RB-02 — Quiescence, verified backup, and custody

**Preconditions:** RB-01 accepted; maintenance window open; source path and station identity confirmed.

1. Stop writers and confirm application/Pilot quiescence.
2. Record database file identity, size, timestamps, header, journal mode, and sidecar state.
3. Create the SQLite-consistent backup through the approved method.
4. Verify source stability, SHA-256, size, header, full integrity, foreign keys, schema/migration classification, and backup identity.
5. Create the immutable rollback copy before any replacement and verify its identity and accessibility.
6. Store receipts in the approved local custody location and cross-reference the package.

**Stop:** Busy writer, unknown WAL state, failed verification, source change, same-path destination, untrusted custody, or missing rollback owner.
**Expected result:** `VerifiedBackupAccepted` and `RollbackCopyReady`, or a blocked result with no live mutation.
**Owner:** Backup operator and rollback owner; data owner confirms source identity.

### RB-03 — Isolated restore and staged replacement qualification

**Preconditions:** Verified backup and rollback copy; valid ManagementCredential proof for exact Restore action; isolated destination available.

1. Validate exact backup identity, checksum, scope, destination, correlation ID, credential version, and expiry.
2. Restore to a distinct isolated destination and run full integrity/foreign-key/schema/migration checks.
3. Start the exact candidate binary against the isolated result and verify station, authentication, Runtime/Event, reporting, finalized snapshots, locks, and Legacy-authority behavior.
4. Inject or observe the approved interruption/failure cases in the isolated copy.
5. Confirm no source or retained backup changed.
6. Record the staged-replacement and recovery result. Do not replace a live production database in B2.

**Stop:** Invalid proof, destination collision, failed post-restore checks, source/backup mutation, ambiguous sidecar handling, or failed recovery.
**Expected result:** `IsolatedRestoreQualified` or `RestoreBlocked`; no production authority effect.
**Owner:** Rollback owner with security reviewer and operator references.

### RB-04 — Protected action and management recovery review

**Preconditions:** Protected-action inventory and recovery policy approved for implementation; no legacy password route accepted for target authority.

1. Review each `ProtectedAction` exactly once against the inventory in Section 4.7.
2. Verify the proof fields and rejection conditions for each action.
3. Review the singleton ManagementCredential recovery ceremony and no-secret evidence rule.
4. Verify the legacy deterministic recovery/bypass is not reachable in the future target route.
5. Record security review findings and unresolved implementation dependencies.

**Stop:** Any unlisted protected action, unbound proof, alternate identity, universal secret, reachable bypass, or missing audit requirement.
**Expected result:** `ProtectedActionInventoryAcceptedForImplementation` or `SecurityBoundaryBlocked`.
**Owner:** Security reviewer; management approver records acceptance.

### RB-05 — Vendor key/device and ESD custody review

**Preconditions:** Approved offline vendor authorization process; station/device mapping available; no vendor private key in customer custody.

1. Verify public key ID/version/lifecycle and safe device identity.
2. Verify canonical P-256 signed request fields and lifetime.
3. Verify ManagementCredential proof is independent and exact-bound.
4. Verify replay receipt and atomic mutation requirements.
5. Record public-key/device custody and reconciliation references without copying sensitive artifacts.

**Stop:** Private key exposure, wrong device/station, invalid signature/time/value/action/request binding, replay ambiguity, or missing atomic receipt.
**Expected result:** `VendorBoundaryAcceptedForImplementation` or `VendorBoundaryBlocked`.
**Owner:** Security reviewer with data owner and management approval references.

### RB-06 — Rollback decision and target-interval data disposition

**Preconditions:** Trigger observed and correlated; target writers can be stopped; rollback owner reachable; rollback copy verified.

1. Record trigger, first-observed UTC time, station, authority state, and correlation ID.
2. Stop target writes and preserve target and Legacy evidence.
3. Start the 15-minute decision timer and notify the rollback owner and management approver.
4. Decide rollback/NO-GO or continued investigation within the timer. Unreachable owners produce a fail-closed stop and escalation.
5. If target-authoritative writes occurred, preserve them and obtain data-owner disposition; never silently merge or discard them.
6. Under a separately authorized protected restore procedure, restore the approved Legacy recovery point if required.
7. Validate Legacy routing, integrity, audit, and restart behavior; record `ActivationRolledBack` only after the state and evidence are durable.

**Stop:** Missing rollback copy, unknown writes, unresolved data disposition, failed restore validation, or ambiguous authority state.
**Expected result:** Legacy authoritative, target route disabled, target interval retained/dispositioned, and an immutable rollback record.
**Owner:** Rollback owner; management approver owns the decision, data owner owns data disposition.

### RB-07 — Station provisioning manifest review

**Preconditions:** One station selected; source inventory and target schema version known; security and data owners available.

1. Select Rasht/3-unit or Ramsar/4-unit scope and reject any other shape.
2. Map all required entities and revisions using safe references.
3. Validate idempotency, uniqueness, station isolation, preservation, no-RBAC, and no-Support results.
4. Record management, data-owner, and security approvals.
5. Keep target composition disabled and Legacy authoritative.

**Stop:** Missing entity, duplicate mapping, wrong station/unit count, secret in manifest, finalized-data mutation, or implicit activation.

**Expected result:** `StationManifestAcceptedForImplementation` or `StationManifestBlocked`.

## 6. Owner matrix and approval record

The following fields are mandatory for a future signed/offline approval record. They are intentionally empty in this B2 report because no real production participants or production window were supplied or contacted.

| Field | Required content | B2 state |
|---|---|---|
| Package/correlation ID | Safe unique package and correlation references | Template defined; no production package created. |
| Station scope | `Rasht`/3 units or `Ramsar`/4 units | Template defined; no production station selected. |
| Database identity | Canonical path, fingerprint, SQLite/WAL evidence | Not captured; production access was forbidden in B2. |
| Candidate binary identity | Commit, Release binary hashes, dependency hashes | Not captured for production binding. |
| Initiating ShiftProfile | Safe personnel/profile reference and station scope | No production identity captured. |
| ManagementCredential proof | Protected reference only; action/scope/version/expiry/correlation | No proof issued or used. |
| Management approver | Human reference, scope, UTC approval, expiry | Manual approval required. |
| Data owner | Human reference and preservation/data-disposition approval | Manual approval required. |
| Security reviewer | Human reference and security-boundary approval | Manual approval required. |
| Rollback owner | Human reference, restore access, escalation contact | Manual approval required. |
| Maintenance-window owner | Window, quiescence owner, stop authority | Manual approval required. |
| Monitoring owner | Thresholds, observation source, escalation | Manual approval required. |
| Local support contact | Offline contact/escalation reference | Manual evidence required for OPS-01. |
| Backup custodian | Artifact location, retention, access, immutability | Procedure defined; no artifact created. |
| Vendor key/device custodian | Public-key/device safe references and lifecycle | Procedure defined; no production material captured. |
| Audit custodian | Local retention location and 24-month minimum policy | Procedure defined; operator confirmation required. |

Approval rules:

- Approval references are human/process references only and must not become application identities or roles.
- The ManagementCredential proof is not an approver identity and is never used for normal login.
- Every approval is exact-bound to scope, evidence package, database identity, correlation, timestamp, and expiry where applicable.
- Approval of this report does not authorize production cutover, production migration, live restore, target authority, or destructive action.

## 7. No-secret and safe-evidence rules

Evidence may include safe identifiers, hashes, categorical outcomes, versions, timestamps, counts, and references to protected artifacts. It must not include:

- passwords, password verifiers, salts, KDF secrets, recovery secrets, recovery codes, or universal codes;
- vendor private signing keys, customer-held private keys, raw signed production authorizations, or raw verifier bytes;
- unrestricted personal data, raw personnel records, or unredacted contact details;
- live database contents beyond the minimum approved sanitized evidence;
- a claim that a generated fixture is production evidence;
- a claim that a readiness package or migration completion grants authority.

If an evidence item cannot be safely sanitized, retain it only in the authorized protected offline location and reference it by safe identifier in ordinary documentation.

## 8. Threat and safety review

| Threat or failure | B2 control decision | Residual state |
|---|---|---|
| Migration completion activates target authority | Explicit acceptance after validation; migration is never an authority switch | Requires B6/B7 implementation and tests. |
| Restore overwrites the live database unsafely | ManagementCredential-bound action, verified rollback copy, staged replacement, sidecar procedure | Requires B3 implementation and fault tests. |
| WAL/sidecar contents are omitted or mixed | Quiescence, record sidecars, SQLite-consistent backup, no copied active sidecars into staged restore | Requires B3 implementation/rehearsal. |
| Rollback restores bytes but not authority or audit | Coupled state transition, Legacy routing, audit, target-interval disposition | Requires B7 implementation and rehearsal. |
| Legacy password authorizes target protected action | Complete ProtectedAction inventory requires singleton ManagementCredential proof | Requires B4 composition and negative tests. |
| Recovery creates a backdoor or alternate identity | Bounded offline reset/rotation; no universal secret; no new principal | Requires B4 implementation and review. |
| Vendor support becomes an application identity | Signed offline ECDSA P-256 artifact only; public key only in customer app | Requires B4 provisioning and rehearsal. |
| Audit exposes secrets or can be skipped | Allow-listed non-secret metadata; durable receipt before mutation; append-only retention | Requires B4 wiring and retention rehearsal. |
| Station mapping leaks between Rasht and Ramsar | Explicit 3-unit/4-unit manifests, station scope, idempotency, and reconciliation | Requires B5 provisioning implementation. |
| Human decision is unavailable during rollback | 15-minute decision deadline, escalation, and fail-closed stop | Requires named OPS-01 participants and rehearsal. |
| Finalized history is changed during recovery | Immutable snapshot/lock boundary; no automatic merge or rewrite | Requires B3/B4/B5 preservation tests. |

The review found no reason to broaden B2 into a universal platform, RBAC, Support identity, cloud service, production cutover, schema change, or production-data operation. Those items remain outside scope.

## 9. Testable acceptance-criteria matrix

No executable tests were added because B1 expressly prohibits production-code changes and requires no application testing in B2. The matrix below is the required acceptance contract for later implementation and human review.

| AC ID | Related gates | Acceptance criterion | Evidence required | B2 disposition |
|---|---|---|---|---|
| AC-B2-01 | AUTH-03, MIG-06 | Every pre-acceptance state is demonstrably Legacy-authoritative; migration/restart/readiness cannot activate target. | State/route matrix and negative tests in later implementation. | Contract defined; not tested. |
| AC-B2-02 | AUTH-03 | Target acceptance requires exact station, database, binary, evidence, approval, correlation, and expiry binding. | Approval and state-transition tests plus durable audit. | Contract defined; not tested. |
| AC-B2-03 | AUTH-04, MIG-06 | Every rollback trigger stops target writes and leads to explicit Legacy-routing decision; no automatic replacement occurs. | Fault/trigger tests, state/audit receipt, isolated rehearsal. | Contract defined; not tested. |
| AC-B2-04 | AUTH-04 | A rollback decision is recorded within 15 minutes of a validated trigger, or fail-closed escalation is recorded. | Timestamped runbook record and owner contact evidence. | Human/manual qualification required. |
| AC-B2-05 | DB-03, BR-02, BR-05 | Backup, rollback copy, custody, hash, integrity, FK, and source stability are bound to one package/correlation ID. | Verified receipt and immutable artifact manifest. | Procedure defined; no artifact created. |
| AC-B2-06 | DB-03, BR-06 | WAL/journal state is captured; quiescence is proven; staged restore does not use uncontrolled sidecars. | Sidecar record, isolated restore tests, interruption results. | Procedure defined; not tested. |
| AC-B2-07 | BR-03, SEC-02 | Restore is denied without current action/scope/correlation/version/expiry-bound singleton ManagementCredential proof. | Positive/negative authorization and audit tests. | Contract defined; not tested. |
| AC-B2-08 | BR-04 | The exact selected production backup opens in isolation under the exact candidate binary and passes application checks. | Production-bound isolated rehearsal record. | Not executable in B2; remains CONDITIONAL. |
| AC-B2-09 | SEC-02 | Every existing ProtectedAction is inventoried exactly once and uses ManagementCredential proof; no legacy password route authorizes target actions. | Route inventory, code review, negative tests, manual review. | Inventory defined; composition absent. |
| AC-B2-10 | SEC-03, SEC-08 | Recovery rotates the singleton credential without creating an identity, universal secret, or reachable legacy bypass. | Recovery rehearsal, audit, binary/security review. | Policy defined; implementation absent. |
| AC-B2-11 | SEC-04 | Vendor ECDSA P-256 authorization binds device/request/action/value/time and is replay-safe and atomic. | Key/device manifest, cryptographic tests, fail-closed rehearsal. | Custody contract defined; production wiring absent. |
| AC-B2-12 | SEC-05 | Security and activation audit is durable, append-only, non-secret, complete, and retained at least 24 months unless a longer approved rule applies. | Persistence tests, retention manifest, review evidence. | Policy defined; wiring absent. |
| AC-B2-13 | MIG-04 | Rasht and Ramsar manifests are station-bound, complete, idempotent, reconciled, and introduce no RBAC/Support identity. | Synthetic station rehearsals, manifest validation, owner approval. | Ownership/criteria defined; implementation absent. |
| AC-B2-14 | AUTH-03, AUTH-04, DB-03, SEC-02–SEC-05, SEC-08, BR-02–BR-06, MIG-04, MIG-06, OPS-01 | B2 decision record is reviewed and approved by management, data, security, rollback, and operator stakeholders with exact references. | Signed/offline approval record and owner matrix. | Manual approval required. |

## 10. Evidence produced in B2

This file is the B2 evidence artifact and contains:

1. Faithful B1 scope and gate inventory.
2. Initial gate states and explicit post-B2 state treatment.
3. Authority state/routing/write-boundary decisions.
4. Rollback triggers, 15-minute decision limit, escalation, and data-boundary rules.
5. Backup/restore custody, quiescence, and SQLite sidecar decisions.
6. Complete protected-action inventory and ManagementCredential recovery policy.
7. Vendor public-key/device custody and ESD boundary.
8. Audit retention and no-secret evidence rules.
9. Rasht/Ramsar station-specific provisioning ownership.
10. Seven numbered runbook specifications.
11. Owner/approval template.
12. Threat/safety review.
13. Testable acceptance-criteria matrix.
14. Explicit Legacy-authority and no-cutover statement.

No application test result, production identity, production backup, production approval, production receipt, or manual qualification result is fabricated or implied.

## 11. Manual evidence still required

The B1 plan requires human review/approval for B2. The following must be completed offline by the appropriate current participants before B2 decisions can be treated as approved for implementation:

- management approval of authority states, decision boundary, owners, routing, write boundary, and rollback policy;
- data-owner approval of preservation rules, station mapping ownership, and target-interval data disposition;
- security-review approval of the ProtectedAction inventory, ManagementCredential recovery, vendor key/device custody, no-secret rules, and legacy-bypass isolation requirements;
- rollback-owner approval of backup custody, rollback-copy custody, sidecar handling, trigger list, 15-minute decision limit, escalation, and restore ownership;
- operator and maintenance-window-owner review of quiescence steps, stop conditions, contact/escalation sequence, and evidence capture;
- confirmation that the owner references are human/process references and do not become application identities, RBAC roles, or a Support identity;
- completion of the approval record with safe references, UTC timestamps, scope, correlation ID, evidence version, and expiry where applicable.

These are manual qualification/approval requirements, not application test failures. Until they are recorded, the B2 evidence is complete as a draftable decision contract but not an approved production implementation authority.

## 12. Gates actually closed

**Primary Phase 9.5A gates closed by B2: none.**

B2 closes the documentation/specification prerequisite for the listed gates only after the required human review is recorded. The report does not relabel a gate READY because the missing capabilities are still implementation, test, rehearsal, or production-bound evidence items.

The following B2 work items are complete as documentation evidence, pending the manual approval record:

- authority/routing/write-boundary decision contract;
- rollback trigger, owner, time limit, escalation, and data-boundary contract;
- backup/restore custody and sidecar-handling contract;
- protected-action and ManagementCredential recovery contract;
- vendor public-key/device custody contract;
- audit retention/no-secret evidence contract;
- Rasht/Ramsar provisioning ownership contract;
- numbered runbooks and acceptance matrix.

## 13. Gates still unresolved

The following gate IDs remain unresolved after B2:

`AUTH-03`, `AUTH-04`, `DB-03`, `SEC-02`, `SEC-03`, `SEC-04`, `SEC-05`, `SEC-08`, `BR-02`, `BR-03`, `BR-04`, `BR-05`, `BR-06`, `MIG-04`, `MIG-06`, and `OPS-01`.

Reasons are unchanged in substance from B1:

- **AUTH-03 / AUTH-04 / MIG-06:** no production authority/rollback adapter or executor, durable transition audit, target-interval handling implementation, or isolated end-to-end rehearsal exists.
- **DB-03 / BR-02 / BR-03 / BR-05 / BR-06:** no production-wired verified receipt and ManagementCredential restore path, staged crash-safe replacement, verified pre-replacement rollback copy implementation, or interruption recovery evidence exists.
- **BR-04:** no exact production backup has been restored and checked in isolation with the final candidate binary.
- **SEC-02 / SEC-03 / SEC-04 / SEC-05 / SEC-08:** target security composition, recovery implementation, vendor provisioning/executor, complete audit wiring/retention operation, and final-binary forbidden-bypass review are absent.
- **MIG-04:** repeatable station-specific provisioning/mapping implementation, reconciliation, and owner approval are absent.
- **OPS-01:** real current operator/approver/owner/contact/window evidence is intentionally not captured in B2.

No unresolved gate is waived, marked NOT APPLICABLE, or converted into a speculative defect by this report.

## 14. Production-code-change summary

| Item | Result |
|---|---|
| Production code changed | **No** |
| Test code changed | **No** |
| Documentation changed | **Yes — this file only** |
| Database schema changed | **No** |
| Production data accessed | **No** |
| Production data migrated/restored/replaced | **No** |
| Production authority changed | **No** |
| Target routing enabled | **No** |
| Automatic authority switching introduced | **No** |
| RBAC roles or Support identity introduced | **No** |
| Normal authentication changed | **No** |
| ManagementCredential changed or used | **No** |
| Vendor key/private material handled | **No** |
| Commit performed | **No** |
| Push performed | **No** |

## 15. Safety-boundary verification

The B2 work preserves all stated boundaries:

- Legacy remains authoritative and is explicitly the default in every pre-acceptance state.
- No production cutover, production migration, production restore, live replacement, or destructive production operation occurred.
- No production authority transition, persisted authority state, automatic switching, startup migration, or target fallback was introduced.
- Pilot remains read-only and has no authority, writer, migration, restore, settings, ESD, finalization, or export execution capability.
- Normal authentication remains ShiftProfile-based; no normal login identity was added.
- ManagementCredential remains a singleton privileged proof for protected actions and is not a normal login identity.
- No Administrator, Engineer, Operator, Viewer, Support, or other RBAC identity/role was introduced.
- Vendor authorization remains signed offline ECDSA P-256 where applicable; customer private keys are excluded.
- Event types remain `START`, `NSD`, `ESD`, and `OH` only.
- Finalized report snapshots and locks remain immutable; no recalculation or rewrite authority was added.
- Rasht and Ramsar remain the only production station scope, with separate 3-unit and 4-unit ownership/mapping decisions.
- No cloud service, external AI dependency, universal platform redesign, package modernization, or unrelated refactor was performed.

## 16. Dependency on Phase 9.5B3

This report does not begin Phase 9.5B3. B3 is the separately scoped crash-safe verified backup, restore, and rollback-copy boundary. Its work is now informed by the B2 decisions for custody, exact authorization, quiescence, sidecars, rollback-copy identity, staged replacement, post-validation, and failure handling.

B3 remains required before the following B2-advanced gates can progress through implementation evidence: `DB-03`, `BR-02`, `BR-03`, `BR-04`, `BR-05`, and `BR-06`. B3 must remain isolated, use disposable/isolated copies for qualification, preserve Legacy authority, and must not perform production migration or cutover. B4, B5, B6, B7, B8, and future production-bound verification remain separate dependencies identified by the B1 plan; no work from those phases was performed here.

## 17. Validation record

Because only documentation changed, the requested validation was `git diff --check`. No build or test suite was run under the B1/B2 documentation-only boundary.

| Validation | Result |
|---|---|
| Focused application tests | Not applicable; no production/test code changed and B1 requires no application testing in B2. |
| `dotnet build Rah_Negar.sln -c Release` | Not run; documentation-only change. |
| `dotnet test Rah_Negar.sln -c Release` | Not run; documentation-only change. |
| `git diff --check` | **PASS**. |
| Production database access | **No**. |
| Production cutover/migration/authority operation | **No**. |
| Files changed | `docs/phase9.5b2-blocker-closure-report.md` only. |
| Production code changed | **No**. |
| Gate IDs closed | **None**. |
| Gate IDs still unresolved | `AUTH-03`, `AUTH-04`, `DB-03`, `SEC-02`, `SEC-03`, `SEC-04`, `SEC-05`, `SEC-08`, `BR-02`, `BR-03`, `BR-04`, `BR-05`, `BR-06`, `MIG-04`, `MIG-06`, `OPS-01`. |

## 18. Exact final status

**PHASE 9.5B2 COMPLETE WITH MANUAL QUALIFICATION REQUIRED**

The B2 documentation/specification deliverable is saved. Manual stakeholder review and approval remain required before the decisions can be treated as approved implementation inputs. The Phase 9.5A gates remain unresolved, Legacy remains authoritative, and Phase 9.5B3 has not begun.
