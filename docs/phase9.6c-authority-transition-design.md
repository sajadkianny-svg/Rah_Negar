# Phase 9.6C - Authority Transition Design

Status: **DESIGN PACKAGE COMPLETE - NO TRANSITION EXECUTED OR AUTHORIZED**

This design is future-facing. The real current state remains `LEGACY_AUTHORITATIVE`: Legacy is authoritative, Target is non-authoritative, Target routing is disabled, and Production Activation/Cutover are unauthorized.

## 1. Scope and architecture evidence

The repository is a .NET 8 WinForms application with SQLite, Application/Core domain contracts, Infrastructure migration/backup/provisioning/readiness boundaries, UI startup/forms, and isolated Qualification/Pilot surfaces. `Application/Activation/ProductionActivationBoundary.cs` evaluates eligibility and writes an evidence receipt; it explicitly never grants Target authority. `ProductionActivationPreparationContracts.cs` and `ProductionCutoverPlanning.cs` are planning contracts. `IntegrationRoutingContracts.cs` and `TargetOperationalComposition.cs` preserve Legacy and keep production mutation disabled. `ProductionMigrationExecutor.cs`, `SQLiteTargetStationProvisioningBoundary.cs`, backup/restore services, migration runner, ManagementCredential proof, snapshots, locks, and audit-related contracts are reusable foundations or rehearsal/test boundaries, not a production authority executor.

Current activation state is represented in runtime enums (`ProductionActivationState`) and immutable receipts plus projections/booleans such as `LegacyRemainsAuthoritative`, `TargetAuthorityAccepted`, `ActivationExecuted`, and `TargetRoutingDisabled`. No canonical persisted authority-state record, atomic transition executor, startup recovery resolver, or production route switch was found. The current eligibility validator also hard-codes `station-rasht`/`station-ramsar`, which conflicts with the frozen generic Target production boundary and must be resolved before activation readiness.

## 2. Explicit authority states

| State | Meaning | Routing | Stable authority |
|---|---|---|---|
| `LEGACY_AUTHORITATIVE` | Current normal operation; Target may be staged/read-only only | Legacy only | Legacy |
| `ACTIVATION_PREPARED_NOT_EXECUTED` | All preparation evidence is bound and current; no commit started | Legacy only | Legacy |
| `TRANSITION_IN_PROGRESS` | A single, lease/correlation-bound commit is durably in flight | No independently changeable route; controlled transaction only | Last committed state, never ambiguous |
| `TARGET_AUTHORITATIVE` | Target commit completed and verified; routing points to Target | Target only | Target |
| `ROLLBACK_IN_PROGRESS` | Authorized restoration from Target toward a known prior state | Controlled maintenance only | Last committed state until completion |
| `RECOVERY_REQUIRED` | Deterministic resolution cannot be proven or integrity is damaged | All operational routing blocked | None operationally usable; maintenance/recovery only |

`ABORTED` may be an auditable terminal outcome that returns to `LEGACY_AUTHORITATIVE`; it is not an authority state. Qualification/rehearsal uses a separate database identity and can never mutate this state.

## 3. Non-negotiable invariants

1. Exactly one operational data source is authoritative at every committed stable point.
2. Legacy and Target are never both authoritative.
3. Target routing cannot be active before a valid, committed Target authority record.
4. Legacy authority is not removed until Target readiness, integrity, authorization, and transaction requirements pass.
5. Failure cannot commit an ambiguous state; inability to prove state enters `RECOVERY_REQUIRED` and blocks operations.
6. Restart resolves from the last durable committed record, never from UI flags, process memory, or timestamps alone.
7. Manual DB editing is never a valid transition and must fail integrity/reconciliation checks.
8. The canonical record is integrity protected: authenticated checksum/MAC or equivalent local integrity chain, with strict schema and monotonic revision validation. This protects accidental/corrupt state, not the SQLite file-holder limitation.
9. Every request, decision, commit, abort, rollback, and recovery is auditable and correlation-bound.
10. Failed/aborted work preserves or restores a known authoritative source.
11. Qualification and rehearsal cannot open, write, replace, or become authoritative over Production DB.
12. No silent fallback is allowed: a routing/state mismatch blocks rather than selecting whichever DB opens.

## 4. Future transition transaction

### 4.1 Preflight

Create one correlation ID and bind it to: prerequisite evaluation and aggregate; explicit Project Owner/governance authorization; ShiftProfile session; action-bound ManagementCredential proof where applicable; Target schema/data and migration-ledger checksums; source/target/backup fingerprints; recovery readiness; application and schema versions; deployment and station scope; expiry; and audit capacity. Verify the 3-5 profile boundary, no Rasht/Ramsar production selector, finalized snapshots/checksums/locks, event model, date/time conventions, and no Production DB identity collision. Validate backup restore in isolation and prove the recovery owner and decision boundary. Preflight is read-only.

### 4.2 Prepare

Write a durable `ACTIVATION_PREPARED_NOT_EXECUTED` intent containing the correlation ID, exact old state, target state, scope, versions, fingerprints, evidence references, authorization references, and expiry. Use a uniqueness key on correlation/request and reject replay. Re-read current authority and Target readiness under a transaction/lock. Confirm routing remains disabled and Legacy remains authoritative. If any check changes, abort and retain Legacy.

### 4.3 Commit ordering

Independent booleans are unsafe. Do not set `TargetAuthorityAccepted`, `TargetRoutingEnabled`, `ActivationExecuted`, and `LegacyRemainsAuthoritative` in separate application operations. Use one canonical state record with a monotonic revision and a transactional commit protocol.

The recommended SQLite transaction is: acquire exclusive transition lease; verify the expected revision, fingerprints, authorization, and intent; validate Target one last time; write the new canonical committed state as `TARGET_AUTHORITATIVE` together with target identity, commit revision, and receipt; write the same-transaction audit receipt; commit; then make the route a pure projection of the committed state and verify it. If the application requires a physical route pointer, it must be changed within the same controlled commit boundary or be a deterministic projection that refuses to serve until it matches the record. A route enable operation must never precede the authority commit. Legacy de-authoritization is a consequence of the single committed state, not an independent flag.

If SQLite cannot make database replacement and routing atomic across their physical boundaries, the state machine must use a maintenance lock: no operational requests are served while the commit/projection is reconciled; mismatch becomes `RECOVERY_REQUIRED`. Never claim atomicity that SQLite/filesystem boundaries cannot provide.

### 4.4 Post-commit

Re-read the canonical record; verify its integrity, Target identity/schema/checksums, route projection, application/version/scope binding, and audit receipt. Perform controlled restart verification before declaring operational completion. Confirm Legacy is retained as the prior source/rollback artifact, not silently re-enabled as a fallback. Produce a receipt with old/new state, commit revision, hashes, and result.

## 5. Failure and restart behavior

| Point of failure | Required result |
|---|---|
| Before start or during validation | No intent/state mutation; remain Legacy; audit blocked/aborted result if a request existed |
| After intent written | On restart, verify intent and lease. Expired/unverifiable intent is aborted; valid intent is revalidated; never infer commit |
| During validation | Abort intent; Target remains non-authoritative |
| During authority commit | SQLite transaction rolls back. If commit outcome cannot be proven, `RECOVERY_REQUIRED`; block both normal routes until integrity/recovery resolves it |
| Immediately after commit | On restart, canonical committed record is authoritative; verify route projection and receipt before serving |
| Before audit finalization | If audit is same transaction, commit rolls back. If external receipt is missing after commit, enter controlled `RECOVERY_REQUIRED`/audit-repair state; never silently proceed |
| During routing projection | Keep service blocked until projection equals canonical state; no Legacy/Target guess or fallback |
| Corrupted authority metadata | Fail closed to `RECOVERY_REQUIRED`; restore verified metadata/DB package under management authorization and audit |
| Target integrity failure/unavailable | Abort before commit; after commit, block Target and use authorized rollback/recovery procedure, not automatic fallback |

The first restart must acquire the transition lease, validate the canonical record and hash chain, reconcile any prepared/in-progress state, verify the route, and emit a restart-recovery receipt. It must be deterministic and idempotent.

## 6. Abort, rollback, recovery, and point of no return

`ABORT` is valid before the authority commit when preflight, intent, lease, or validation fails; it leaves Legacy authoritative and Target non-authoritative. `ROLLBACK` is an explicitly authorized transition after Target has committed, using a verified prior backup/state and a tested boundary; it is not an automatic exception or generic undo. `RECOVERY` is required when the committed outcome, metadata, audit, route, or database integrity cannot be proven. Recovery blocks operational service until a human-authorized, evidence-backed known state is restored.

The point of no return is the durable commit of `TARGET_AUTHORITATIVE` plus its integrity receipt. Before it, abort is safe. After it, a rollback may be unsafe if Target has accepted new writes; it requires a defined write freeze, data reconciliation/loss decision, verified backup, authorization, and a new correlation. No silent fallback is permitted. If a future implementation can use an atomic versioned snapshot and a write fence that preserves every post-commit write, it may make rollback reversible, but that must be proven by qualification rather than assumed.

## 7. Storage model recommendation

Recommend one canonical `AuthorityStateRecord` (likely a dedicated system-owned table or equivalent protected store) with: record ID, revision, state, prior state, active operational database identity, target identity, route projection version, schema/application versions, scope, correlation/request/intent IDs, authorization/evidence references, commit UTC, integrity tag/hash, and recovery/lease fields. Append-only history and immutable audit receipts should accompany the current record. The record must be written under one transaction and read with strict validation.

Independent booleans create impossible combinations: `ActivationExecuted=true` with `TargetAuthorityAccepted=false`; `TargetAuthorityAccepted=true` with `LegacyRemainsAuthoritative=true`; or `TargetRoutingEnabled=true` with `TargetAuthorityAccepted=false`. All must be rejected as corrupt/ambiguous and enter `RECOVERY_REQUIRED`; they must never be normalized by choosing a flag or silently falling back. Existing booleans remain evidence projections only until a future approved implementation replaces them. Do not implement this model in Phase 9.6C.

## 8. Authorization and audit

Preserve the current architecture: an authenticated ShiftProfile initiates the operational request; fresh action-bound ManagementCredential proof authorizes the protected transition; and an explicit, scope-bound Project Owner/governance authorization artifact is required. ManagementCredential proves the protected action and is not a role. Vendor ESD authorization remains limited to its ESD purpose; it is not reused for activation without separate architectural justification. No activation credential, RBAC role, Support identity, master password, or universal secret is introduced.

Audit records for eligibility, request, authorization, preflight, prepare, commit, abort, rollback, recovery, restart, and final state include correlation ID, UTC time, deployment/station scope, software and schema versions, ShiftProfile where relevant, Management proof result without secret, old/new state, result, failure reason, checksums/fingerprint/evidence references, revision, and actor/source. Audit failure is fail-closed for a commit unless a same-transaction immutable receipt is already guaranteed.

## 9. Implementation gap analysis

| Area | Classification | Evidence / likely future area |
|---|---|---|
| Eligibility receipt and safety projections | EXISTS_AND_REUSABLE | `Application/Activation/ProductionActivationBoundary.cs`; decision-only and non-authoritative |
| Preparation/state transition planning | EXISTS_BUT_REQUIRES_HARDENING | `ProductionActivationContracts.cs`, `ProductionActivationPolicies.cs`; current enum is not the canonical authority state |
| Migration checksums/ledger/rehearsal | EXISTS_AND_REUSABLE | `Infrastructure/Database/Migrations/*`, `Readiness/MigrationRehearsalService.cs`; production binding/executor evidence incomplete |
| Production migration executor | EXISTS_BUT_REQUIRES_HARDENING | `Readiness/ProductionMigrationExecutor.cs`; explicitly no startup/authority activation |
| Backup/restore and safety replacement | EXISTS_BUT_REQUIRES_HARDENING | `Readiness/ManagedSqliteBackupRestoreBoundary.cs`; needs transition integration and recovery qualification |
| Target provisioning/schema | EXISTS_BUT_REQUIRES_HARDENING | `Provisioning/SQLiteTargetStationProvisioningBoundary.cs` and draft migrations; not authority switching |
| Routing policy/composition | EXISTS_BUT_REQUIRES_HARDENING | `Application/Integration/IntegrationRoutingContracts.cs`, `TargetOperationalComposition.cs`; Target production route disabled |
| Canonical authority store/atomic executor | NOT_IMPLEMENTED | Future `Application/Activation` contract plus Infrastructure SQLite store/executor |
| Startup crash resolver and route reconciliation | NOT_IMPLEMENTED | Future `UI/Startup`/composition boundary; current startup must remain Legacy |
| Management proof primitives | EXISTS_AND_REUSABLE | `Application/Security/*`, `Core/Foundation/Identity/*`; bind exact future action/scope/correlation |
| Append-only transition audit/receipt | EXISTS_BUT_REQUIRES_HARDENING | System audit/snapshot/receipt foundations; no complete transition lifecycle |
| Finalized snapshots/checksums/locks | EXISTS_AND_REUSABLE | `Core/Reporting/Snapshot/*` and target schema tests; cutover preservation remains unqualified |
| Qualification isolation | EXISTS_AND_REUSABLE | `Qualification/*`, Pilot and isolated SQLite factories; must be a release gate |
| Generic activation scope | EXISTS_BUT_REQUIRES_HARDENING | `ProductionActivationBoundary.ValidateRequest` currently restricts Rasht/Ramsar |
| Transition negative/restart/crash qualification | TEST_ONLY | Existing tests cover preparation/safety, not the future production transaction |
| Operator cutover/rollback runbook | DOCUMENTATION_ONLY | Phase 9.6D/9.6G future work; no approved execution procedure |

## 10. Design conclusion

This package is sufficient to define the future controlled transition boundary and its required evidence, but it does not authorize implementation or execution. It must feed Phase 9.6D, where exact cutover/abort/rollback mechanics, data-write fencing, physical SQLite replacement limits, and recovery ownership are specified.

**READY TO BEGIN PHASE 9.6D**
