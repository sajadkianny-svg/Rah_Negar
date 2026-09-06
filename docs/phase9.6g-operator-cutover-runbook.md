# Phase 9.6G — Future Operator Cutover Runbook

Status: **FUTURE CONTROLLED PROCEDURE — NOT CURRENTLY AUTHORIZED**  
Applies only after Phase 9.6H records a permitted decision and separate,
explicit Production Activation authorization exists. This document does not
execute or authorize Production Cutover.

## 1. Purpose and authority boundary

This runbook is the operator-facing procedure for a future, explicitly
governed Production transition. It converts the 9.6C–9.6F technical controls
into ordered gates, receipts, stop conditions, and handoff evidence.

The current state is unchanged and is not an outcome of this runbook:

| Item | Current value |
|---|---|
| Legacy | **AUTHORITATIVE** |
| Target | **NON-AUTHORITATIVE** |
| Target Routing | **DISABLED** |
| Production Activation | **UNAUTHORIZED** |
| Production Cutover | **UNAUTHORIZED** |

Qualification and rehearsal evidence is isolated, non-authoritative evidence.
No readiness receipt, test pass, owner acceptance, or runbook approval alone
changes the table above. No operator may begin a Production transition from
this document without all GO gates below.

## 2. Procedural actors and evidence

The actor model is procedural and preserves the product authentication
architecture; it does not add a role system or RBAC.

| Actor | Permitted responsibility | Required evidence |
|---|---|---|
| **ShiftProfile operator** | Normal operational identity; initiates the named, scoped workflow and records observations | Active ShiftProfile, station/deployment scope, session/request and correlation binding |
| **Project Owner / governance authority** | Approves the future Production Activation/Cutover decision and any separately governed rollback/recovery decision | Signed or otherwise auditable authorization with scope, expiry, version, database identity, limitations, and correlation binding |
| **ManagementCredential privileged proof** | Fresh singleton proof for protected actions where the contract requires it | Action-bound proof result; never store or expose the secret |
| **System validation** | Independently checks state, scope, generation/epoch, version, schema, data, backup, reconciliation, audit, routing, and replay/fencing invariants | Machine-readable validation receipt and durable audit record |

`ShiftProfile` is the only normal operational identity. `ManagementCredential`
is singleton privileged proof: it has no username, is not a normal account, and
is not a role. There is no Support identity, master credential, backdoor,
universal activation code, or hidden recovery identity.

## 3. Global rules and evidence contract

Use one fresh correlation ID for the complete future attempt. Bind every intent,
authorization, database fingerprint, backup receipt, generation/epoch, software
version, deployment/station scope, audit entry, abort, rollback, recovery, and
handoff receipt to it. Record UTC timestamps. Do not record secrets or raw
credentials.

The system must fail closed. A missing, stale, contradictory, unreadable,
unbound, or unverifiable item is **NO-GO** or **RECOVERY_REQUIRED**, never a
manual bypass. A displayed success is not evidence until its durable receipt
can be read back.

## 4. Strict future cutover sequence

Every step has the same required fields. The “next permitted step” is the only
allowed forward movement; any STOP condition ends the sequence or selects the
specified recovery path.

| Step ID | Responsible actor | Prerequisite | Exact action | Expected result | Evidence to capture | STOP condition | Next permitted step |
|---|---|---|---|---|---|---|---|
| G-01 | ShiftProfile operator + system validation | Authorized change window and fresh correlation ID | Read canonical authority and route projection; confirm Legacy authoritative, Target non-authoritative, Target routing disabled | Exact pre-state is proven | Pre-state receipt, authority revision/epoch, route readback, correlation | Any ambiguity, dual authority, Target route enabled, stale metadata | G-02; otherwise RECOVERY_REQUIRED |
| G-02 | Project Owner/governance authority | Current 9.6H decision path and valid authorization package | Present future Production Activation authorization and separate governance authorization; verify scope, expiry, DB, version, limitations, and correlation | Authorizations are explicit, current, and bound | Authorization references and validation receipt | Missing, expired, unbound, or contradictory authorization | G-03; otherwise NO-GO |
| G-03 | System validation | G-01/G-02 | Evaluate every mandatory GO/NO-GO item in Section 5, including exact MQ-07 treatment and no new blocker | Aggregate permits an activation decision; no bypass applied | Full prerequisite matrix and aggregate receipt | Any item other than validly satisfied, or new blocker | G-04; otherwise NO-GO |
| G-04 | ShiftProfile operator | G-03 GO | Acquire controlled maintenance/write fence and confirm normal writes are drained under the approved procedure | No unaccounted write can cross the transition boundary | Fence/lease receipt, open-write count, drain result | Fence unavailable, writer active, timeout, or scope mismatch | G-05; otherwise ABORT or RECOVERY_REQUIRED |
| G-05 | System validation | G-04 | Create and validate transition intent with source state, requested future state, scope, application/schema version, correlation, evidence references, expiry, expected revision, and expected generation/epoch | Intent is durable, unique, and still says Legacy authoritative / routing disabled | Intent receipt, hash/reference, validation result | Replay, source mismatch, invalid target, stale generation/epoch, write failure | G-06; otherwise ABORT |
| G-06 | System validation | Valid intent and active fence | Re-read authority, verify generation/epoch/fencing, schema, migration ledger, Target integrity, profile, Unit count 3–5 inclusive, and deployment/station/version binding | State is unchanged and all bindings match | Validation receipt and fingerprints | Any mismatch, stale writer, unsupported 35, station selector, or unavailable evidence | G-07; otherwise ABORT |
| G-07 | System validation | G-06 success | Perform final read-only reconciliation and divergence evaluation | Reconciliation is acceptable and divergence is `SYNCHRONIZED` or explicitly accepted by the approved contract; no unaccounted Target write exists | Component reconciliation, divergence result, final-sync receipt | `MISMATCHED`, `BLOCKED`, `NOT_EVALUATED`, `DIVERGED`, `DELTA_PENDING` without approved completion | G-08; otherwise ABORT or RECOVERY_REQUIRED |
| G-08 | System validation + operator | G-07 success | Verify backup receipt, SHA-256/integrity/FK/schema/ledger/scope/correlation, isolated restore readiness, custody, and rollback destination | Verified backup and restore evidence is current and bound | Backup receipt, restore-readiness receipt, hashes, paths, custody record | Missing/changed/failed/unreadable backup or restore path | G-09; otherwise ABORT or RECOVERY_REQUIRED |
| G-09 | ManagementCredential proof + Project Owner authority + system | G-08 success | Reconfirm fresh protected proof and governance authorization; execute the separately approved authority commit under the canonical transaction/fence | Durable committed Target-authority record and same-boundary audit receipt exist; routing remains disabled until projection verification | Commit receipt, old/new state, revision/epoch, generation, audit readback | Commit failure, audit failure, crash with unknown outcome, route mismatch, or ambiguous authority | G-10; otherwise RECOVERY_REQUIRED |
| G-10 | System validation | G-09 success | Re-read canonical state and prove Target authoritative, Legacy de-authorized by the single committed state, route still disabled, identities/versions/scope match | Target authority is proven; no route is yet enabled | Post-commit authority receipt and audit readback | Target not authoritative, Legacy still authoritative, dual state, or any mismatch | G-11; otherwise RECOVERY_REQUIRED |
| G-11 | Controlled routing component + system validation | G-10 success only | Project/enable Target routing through controlled transition logic; do not manually toggle a flag | Target route is enabled only after committed Target authority and exact projection match | Routing-change receipt and route health/readback | Route enable requested before G-10, projection mismatch, or uncontrolled/manual route | G-12; otherwise RECOVERY_REQUIRED |
| G-12 | System validation + operator | G-11 success | Run post-cutover validation: reads, writes under approved scope, reports, finalized locks, event/runtime rules, station/profile isolation, and audit durability | Target-authoritative operations behave as qualified and no invariant fails | Validation results, checksums, audit receipt, exception log | Any data, lock, calculation, scope, audit, or route defect | G-13; otherwise STOP and evaluate rollback/recovery |
| G-13 | System validation | G-12 success | Restart under the controlled procedure and verify canonical authority, generation/epoch, route projection, audit readback, and no replay | Restart persistence is deterministic and Target remains the proven authority | Restart receipt, state/route readback, audit receipt | Corrupt/missing metadata, changed authority, route mismatch, or replay | G-14; otherwise RECOVERY_REQUIRED |
| G-14 | ShiftProfile operator + Project Owner/governance | G-13 success | Complete operator handoff, evidence manifest, open-issue review, and acceptance of residual limitations | Handoff is complete and auditable; no authorization is inferred beyond the approved package | Signed handoff receipt, evidence index, unresolved-gap disposition | Missing evidence, unowned gap, or disagreement | End; otherwise STOP/escalate |

### 4.1 Routing ordering rule

Target routing **must not** be enabled before Step G-09 has durably committed
Target authority and Step G-10 has independently confirmed it. A route flag,
configuration edit, database edit, or operator action cannot substitute for the
controlled projection. Legacy de-authoritization is a consequence of the
canonical committed state, not a separate manually editable flag.

## 5. Mandatory GO / NO-GO checklist

The future system validation must mark every field `YES` with evidence. Any
`NO`, blank, stale, unavailable, or contradictory result is **NO-GO**. There is
no bypass.

- [ ] Phase 9.6 prerequisite aggregate allows an activation decision.
- [ ] Explicit future Production Activation authorization exists and is valid.
- [ ] MQ-07 residual limitation acceptance is present, exact, current, and valid.
- [ ] No new blocking prerequisite exists.
- [ ] Verified backup receipt exists and is bound to this attempt.
- [ ] Restore readiness and custody are proven.
- [ ] Migration ledger is valid and bound.
- [ ] Schema is valid and bound.
- [ ] Target integrity is valid.
- [ ] Profile is valid; Unit count is **3–5 inclusive**; 35 is rejected.
- [ ] Reconciliation is acceptable.
- [ ] Divergence is acceptable and fully accounted for.
- [ ] Legacy is authoritative before preparation.
- [ ] Target is non-authoritative before preparation.
- [ ] Target routing is disabled before preparation.
- [ ] Audit is writable, durable, and readable before commit.
- [ ] Correlation ID is valid, unique, and bound.
- [ ] Generation/epoch is current and fenced.
- [ ] Deployment/station scope binding is valid; no Rasht/Ramsar Production selector is used.
- [ ] Software/application/schema version binding is valid.
- [ ] ManagementCredential proof is valid where required.
- [ ] Project Owner/governance authorization is valid and separately evidenced.
- [ ] Recovery procedure and owner are available.
- [ ] Operator understands every STOP condition.

Record `GO` only after all boxes are `YES`; otherwise record `NO-GO`, retain
the evidence, and do not create a commit intent.

## 6. Abort, rollback, and recovery decision model

| Observed condition | Required disposition | Operator meaning |
|---|---|---|
| Failure before authority commit | **ABORT** | Close the intent/lease under controlled logic, preserve evidence and backups, and re-confirm Legacy authoritative, Target non-authoritative, and routing disabled. |
| Target authority committed, but no divergent Target-authoritative writes | Evaluate **ROLLBACK_ELIGIBLE** | A fresh rollback authorization, verified prior Legacy backup, complete receipt, matching generation/correlation, write fence, and acceptable reconciliation are still required. Eligibility is not execution. |
| Target-authoritative writes, finalized reports/events, or unaccounted divergence exist | **ROLLBACK_NOT_SAFE**, unless valid reconciliation explicitly proves complete, lossless handling | Do not discard Target writes or restore blindly. Freeze, preserve both artifacts, reconcile and escalate under a new governed decision. |
| Authority, commit outcome, generation, route, audit, database identity, or restore state is ambiguous/corrupt | **RECOVERY_REQUIRED** | Block normal operations and both ambiguous routes; do not guess. |

Prohibited actions: blind rollback; manual database flag editing; silent
authority switching; deleting or discarding Target-authoritative writes;
manual routing enablement outside controlled transition logic; and any hidden
recovery credential or bypass.

### 6.1 Safe rollback procedure (future, separately authorized)

1. Stop and retain the correlation, generation, current Target copy, audit,
   and all receipts.
2. Obtain a new explicit rollback authorization bound to scope, version,
   database identities, reason, expiry, and reconciliation evidence.
3. Establish and verify a write fence; confirm no Target-authoritative writes
   or unresolved divergence exist.
4. Re-verify the prior Legacy backup and restore it to a distinct controlled
   destination. Never overwrite the only evidence copy.
5. Validate SQLite integrity/foreign keys, schema, migration ledger, profile,
   records, events/runtime, finalized snapshots, locks, credentials, audit,
   hashes, and scope.
6. Commit the canonical authority state to Legacy while routing remains
   disabled; write and read back the rollback receipt.
7. Project Legacy routing only after the committed state and validation match.
8. Restart, verify persistence, retain the Target artifact, and complete a
   rollback handoff. Any failed or uncertain step becomes `RECOVERY_REQUIRED`.

## 7. RECOVERY_REQUIRED operator procedure

When `RECOVERY_REQUIRED` is displayed or returned:

1. Stop normal operational workflow, data entry, reporting mutation, and
   transition retries.
2. Do not guess which database or route is authoritative.
3. Keep Target routing disabled; do not manually change route or authority
   metadata.
4. Capture the recovery reason/code, correlation ID, generation/epoch,
   authority/transition references, UTC time, and visible system message.
5. Preserve both databases, backups, sidecars, logs, audit, hashes, and all
   receipts without deleting or replacing evidence.
6. Do not edit authority metadata, repair SQLite flags, or use a credential
   that is not part of the approved procedure.
7. Escalate to the authorized governance/recovery procedure and named owner.
8. Resume only after a canonical authority state is proven, integrity and
   route projection are validated, and a durable recovery decision/receipt is
   available.

No Support login, master credential, bypass code, hidden recovery path, or
generic STOP event is part of this procedure.

## 8. Audit and evidence capture

At minimum retain the pre-state, GO/NO-GO matrix, authorization references,
intent, generation/epoch checks, schema/ledger/profile/integrity evidence,
reconciliation/divergence results, backup and restore receipts, commit or
abort/rollback/recovery outcome, route readbacks, restart proof, operator
observations, and final handoff. Every record is UTC, correlation-bound,
scope-bound, version-bound, and readable after the operation. Retention and
readback must be governed before any future production decision.

## 9. Final operator handoff

The operator must hand over:

- the complete evidence manifest and hashes where generated;
- the final canonical authority and routing receipt;
- unresolved gaps, owners, expiry dates, and next decision;
- the exact MQ-07 status and residual limitation acceptance;
- the Independent Human Review status (**NOT PERFORMED / UNAVAILABLE**);
- confirmation that AI-assisted review is not organizationally independent;
- confirmation that no production authorization is inferred beyond the signed
  decision package.

## 10. Printable future checklist

| Check | Yes/No | Evidence/reference |
|---|---|---|
| Authority pre-state: Legacy authoritative / Target non-authoritative | [ ] YES [ ] NO | |
| Routing pre-state: Target routing disabled | [ ] YES [ ] NO | |
| Prerequisite aggregate permits activation decision | [ ] YES [ ] NO | |
| Explicit authorization and MQ-07 acceptance | [ ] YES [ ] NO | |
| Backup receipt verified | [ ] YES [ ] NO | |
| Restore readiness verified | [ ] YES [ ] NO | |
| Reconciliation acceptable | [ ] YES [ ] NO | |
| Divergence acceptable | [ ] YES [ ] NO | |
| Audit writable/durable/readable | [ ] YES [ ] NO | |
| Scope/version/correlation/generation valid | [ ] YES [ ] NO | |
| Transition intent created and validated | [ ] YES [ ] NO | |
| Authority commit completed | [ ] YES [ ] NO | |
| Target authority confirmed after commit | [ ] YES [ ] NO | |
| Target routing enabled only after commit confirmation | [ ] YES [ ] NO | |
| Legacy de-authoritization confirmed by canonical state | [ ] YES [ ] NO | |
| Restart persistence verified | [ ] YES [ ] NO | |
| Post-cutover validation complete | [ ] YES [ ] NO | |
| Abort / rollback / recovery decision recorded | [ ] YES [ ] NO | |
| Final operator handoff complete | [ ] YES [ ] NO | |

Operator: ____________________  Project Owner/governance: ____________________  
Correlation ID: ____________________  Date/UTC: ____________________  
Final disposition: `[ ] GO`  `[ ] NO-GO`  `[ ] ABORT`  `[ ] ROLLBACK_NOT_SAFE`  `[ ] RECOVERY_REQUIRED`

## 11. Current-state reminder

This is future procedure documentation only. At Phase 9.6G completion:
Legacy remains **AUTHORITATIVE**, Target remains **NON-AUTHORITATIVE**, Target
Routing remains **DISABLED**, Production Activation remains **UNAUTHORIZED**,
and Production Cutover remains **UNAUTHORIZED**.
