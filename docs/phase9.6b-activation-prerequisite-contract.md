# Phase 9.6B - Activation Prerequisite Contract

Status: **RECONCILED AND CLOSED - ACTIVATION NOT AUTHORIZED**

This documentation-only contract does not implement, invoke, or authorize Production Activation, Production Cutover, Target routing, authority transition, database mutation, startup registration, or qualification behavior.

## 1. Frozen boundary

- Legacy = **AUTHORITATIVE**; Target = **NON-AUTHORITATIVE**.
- Target Routing = **DISABLED**; Production Activation and Production Cutover = **UNAUTHORIZED**.
- Product is general, dynamic, profile-driven, offline, local-first, and extensible.
- Supported unit boundary: 3-5 inclusive. 3, 4, 5 are valid; 2, 6, 35 are invalid.
- Rasht/Ramsar are legacy, qualification, migration, and test fixtures only; no station-specific Production branching.
- ShiftProfile is the only normal identity. ManagementCredential is a singleton privileged proof, not a role. No RBAC, Support identity, backdoor, master password, universal credential, or hidden recovery identity.
- Events are START, NSD, ESD, and OH; there is no generic STOP.
- Qualification is isolated from Production DB. Finalized reports are immutable through canonical JSON plus checksum.
- Qualification DPI support is 100%, 125%, and 150%; above 150% is blocked.

Readiness receipts, migration rehearsals, owner acceptance, and eligibility results are evidence only. None changes authority or permits a production write.

## 2. Evaluation and aggregation

Every prerequisite has exactly one result: `SATISFIED`, `NOT_SATISFIED`, `NOT_EVALUATED`, or `BLOCKED`. The normal aggregate is fail-closed: `ELIGIBLE_FOR_ACTIVATION_DECISION` requires every mandatory prerequisite to be `SATISFIED`, current, authentic, in scope, and internally consistent. Any other mandatory result produces `NOT_ELIGIBLE_FOR_ACTIVATION_DECISION`.

### 2.1 Sole MQ-07 governance exception

MQ-07 qualification status remains exactly:

**BLOCKED  MANUAL OBSERVATION NOT PRACTICALLY EXERCISABLE, WITH AUTOMATED INVARIANT EVIDENCE RETAINED**

**PROJECT OWNER DECISION: ACCEPT RESIDUAL LIMITATION**
**Timestamp: 2026-09-06T12:11:37Z**
**Attribution: Sajad Kiyani / Project Owner**
**Traceability: `docs/phase9.6b2-mq07-project-owner-governance-decision.md`, Phase 9.6B2 / MQ-07 governance decision**

This is a separate, explicit, auditable governance exception. It applies only to MQ-07. It is not a qualification PASS, `SATISFIED`, resolved, or closed status. It may prevent MQ-07 from acting as an automatic hard activation-readiness blocker only because explicit technical analysis, owner acceptance, immutable/auditable evidence, timestamp, attribution, and traceability exist. It is not a generic waiver, override, reusable bypass, or automatic exception inheritance.

PR-03 retains MQ-07 as `BLOCKED`; PR-05 records the owner decision as `SATISFIED` only for the narrow question of whether this residual treatment was explicitly governed. All other mandatory results remain subject to normal aggregation. Contrary safety, integrity, authority, routing, completion, or recovery evidence invalidates this treatment and restores MQ-07's blocking effect.

The exception does not authorize Production Activation, Production Cutover, Target authority, Target routing, Legacy de-authoritization, artificial production delay, debug-only workflow timing, or weakened production semantics. It does not substitute for Independent Human Review, which remains **NOT PERFORMED / UNAVAILABLE**; AI-assisted review is not organizationally independent.

## 3. Evidence record contract

Each evaluation record includes prerequisite ID, evaluation run ID, correlation ID, scope and database identity where applicable, UTC timestamp, evaluator/source, software/build context, evidence references, result, and safe reason. Exact artifacts are immutable or SHA-256 hashed where appropriate. Evidence binds approvals, database fingerprints, build hash, scope, and validity. Secrets, passwords, private keys, and raw production data are excluded.

## 4. Mandatory prerequisite register

All entries are mandatory. Evaluation mode is automatic, manual, or both.

| ID | Purpose and minimum evidence | Failure result | Mode |
|---|---|---|---|
| PR-01 | Governance owner, scope, expiry, approval, database/evidence/correlation binding | Missing/expired/ambiguous/unbound = `NOT_SATISFIED`; unavailable = `NOT_EVALUATED` | Both |
| PR-02 | Phase 9.5 package, runbook, results, hashes, receipts, fixtures, build context, C5-C10 reconciliation | Altered/contradictory/unsupported = `NOT_SATISFIED`; incomplete = `NOT_EVALUATED` | Both |
| PR-03 | MQ-01..MQ-12 statuses and evidence-class reconciliation | MQ-07 stays exactly BLOCKED; failed MQ or misrepresented evidence = `NOT_SATISFIED` | Both |
| PR-04 | Per-MQ evidence matrix mapped to gates | Automated cannot be labeled manual/independent; contradiction = `NOT_SATISFIED` | Both |
| PR-05 | Exact Phase 9.6B2 MQ-07-only owner decision and traceability | Valid exact decision = `SATISFIED` only here; missing/invalid/expired/invalidated = `NOT_SATISFIED` or unavailable = `NOT_EVALUATED` | Manual |
| PR-06 | Checksummed target migration chain, ledger, backup binding, idempotency, preservation, rollback rehearsal | Missing executor/procedure or failed preservation = `NOT_SATISFIED`; absent exact rehearsal = `NOT_EVALUATED` | Both |
| PR-07 | Verified backup, isolated restore, immutable rollback copy, crash/fault recovery, owner, receipt | Direct overwrite, failed restore, missing recovery/authorization = `NOT_SATISFIED` | Both |
| PR-08 | Target schema/data, identities/counts/mappings, events, baselines, snapshots, locks, credentials, ESD | Any mismatch, partial write, or integrity failure = `NOT_SATISFIED`; absent production-like evidence = `NOT_EVALUATED` | Both |
| PR-09 | Generic profiles; 3/5 acceptance; 2/6/35 rejection; no station selector | 35 accepted, station selector, or partial provisioning = `NOT_SATISFIED` | Both |
| PR-10 | ShiftProfile, singleton ManagementCredential, scoped proof, recovery, expiry, concurrency, audit | RBAC/support/backdoor/raw credential/bypass = `NOT_SATISFIED`; incomplete composition = `NOT_EVALUATED` | Both |
| PR-11 | Vendor-signed ESD key/envelope binding, signature, expiry/replay, Management proof | Weak/unavailable/mismatched verification = `NOT_SATISFIED` | Both |
| PR-12 | Strict event chain, date/time/baseline rules, duplicate handling, audit | Invariant failure = `NOT_SATISFIED`; absent reconciliation = `NOT_EVALUATED` | Both |
| PR-13 | Canonical snapshots/checksums, locks, immutable finalization, export/read tests | Mutation/recalculation/lock/checksum/export bypass = `NOT_SATISFIED` | Both |
| PR-14 | Separate qualification DB path/identity, production before/after hash, negative path tests | Production touch or ambiguous identity = `NOT_SATISFIED` | Both |
| PR-15 | 100/125/150 DPI screenshots and RTL/focus/grid/control evidence | Failure/unsupported claim = `NOT_SATISFIED`; unavailable = `NOT_EVALUATED`; MQ-07 remains separate | Manual + automatic |
| PR-16 | Versioned operator runbook, stops, receipts, abort triggers, training | Missing/ambiguous = `NOT_SATISFIED`; no trained evaluator = `NOT_EVALUATED` | Manual |
| PR-17 | Approved cutover/abort/rollback procedure, rehearsal, restore, authority/routing sequence | Missing executor, unsafe replacement, undefined rollback, ambiguity = `NOT_SATISFIED` | Both |
| PR-18 | Append-only audit/retention, hashes, UTC/correlation, readback, owner | Missing/secret-bearing/unrecoverable audit = `NOT_SATISFIED` | Both |
| PR-19 | 9.6C authority model, transaction, routing boundary, negative tests, binary review | Missing model/bypass = `NOT_SATISFIED`; unperformed review = `NOT_EVALUATED` | Both |
| PR-20 | Future decision approval bound to owner, scope, evidence, DB, expiry, limitations | Cannot waive unrelated prerequisites; invalid/unavailable = `NOT_SATISFIED` or `NOT_EVALUATED` | Manual + automatic |

## 5. Anti-bypass and current re-evaluation

No hidden override, Support identity, master password, universal activation code, manual database flag editing, direct Target route enablement, partial waiver, silent fallback, or undocumented emergency path is valid. Evaluators cannot relabel a result or rerun a different scope to create `SATISFIED`.

Repository re-evaluation confirms the owner decision closes only the MQ-07 treatment question. PR-06 through PR-20 remain unresolved for a future production activation decision: the production transition executor and canonical authority store are absent; restore/rollback/audit wiring and exact production-like evidence are incomplete; activation scope contains `station-rasht`/`station-ramsar` validation conflicting with generic Target boundaries; Target production composition/adoption and data reconciliation are not established; and the cutover/runbook package is not approved. Existing migration/provisioning/backup/readiness code is a foundation or rehearsal boundary, not an activation executor.

## 6. Non-authorization

This contract does not authorize Production Activation, Production Cutover, Target authority, Target routing, Legacy de-authoritization, production DB mutation, startup changes, qualification changes, or any authority-state transition.

## Phase 9.6B result

**BLOCKED  Independent Human Review remains NOT PERFORMED / UNAVAILABLE and AI-assisted review is not organizationally independent; PR-06 through PR-20 still contain unresolved production executor, restore/rollback, audit, production-like evidence, generic station-scope, authority-transition, runbook, and governance prerequisites. MQ-07 remains BLOCKED, with its residual limitation accepted only by the single auditable exception above.**
