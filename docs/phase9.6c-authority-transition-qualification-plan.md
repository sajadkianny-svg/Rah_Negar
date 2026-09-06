# Phase 9.6C - Authority Transition Qualification Plan

Status: **FUTURE PLAN - NO TEST CLAIMED PASS**

This plan qualifies a future implementation against disposable, production-like copies. It must never mutate Production authority or use the Production DB as a qualification database. Each case produces immutable/hash-bound evidence with correlation ID, UTC timestamp, scope, build/schema versions, old/new state, and result.

| ID | Qualification case | Method |
|---|---|---|
| QAT-01 | Happy-path prepared-to-Target transition simulation | BOTH |
| QAT-02 | Abort before commit on each preflight failure | AUTOMATED |
| QAT-03 | Failure injection at intent, validation, lease, commit, audit, and routing stages | AUTOMATED |
| QAT-04 | Process/machine crash and deterministic restart recovery at every stage | BOTH |
| QAT-05 | Reject every impossible state and contradictory flag projection | AUTOMATED |
| QAT-06 | Prove dual-authority prevention under concurrency and replay | AUTOMATED |
| QAT-07 | Prove zero-authority prevention and controlled recovery-required path | BOTH |
| QAT-08 | Prove Target routing cannot precede committed Target authority | AUTOMATED |
| QAT-09 | Audit failure blocks commit or produces recovery-required state | AUTOMATED |
| QAT-10 | Target checksum mismatch aborts and preserves Legacy | AUTOMATED |
| QAT-11 | Migration-ledger tamper/gap/duplicate/order mismatch is rejected | AUTOMATED |
| QAT-12 | Schema/version mismatch is rejected before route or authority change | AUTOMATED |
| QAT-13 | Unauthorized ShiftProfile or missing ManagementCredential is rejected | BOTH |
| QAT-14 | Invalid, expired, wrong-version, or wrong-scope ManagementCredential proof | AUTOMATED |
| QAT-15 | Stale/replayed governance authorization and consumed correlation | AUTOMATED |
| QAT-16 | Deployment/station scope mismatch is rejected | AUTOMATED |
| QAT-17 | Application/build/schema version mismatch is rejected | AUTOMATED |
| QAT-18 | Correlation mismatch across evidence, DB, approval, and receipt | AUTOMATED |
| QAT-19 | Authorized rollback before/after Target writes, including write fence | BOTH |
| QAT-20 | Recovery-required state, repair authorization, and evidence completion | BOTH |
| QAT-21 | Production isolation: path, file identity, hashes, and negative writes | BOTH |
| QAT-22 | Qualification DB is distinct, disposable, and cannot resolve Production authority | AUTOMATED |
| QAT-23 | Restart persistence of canonical state and monotonic revision | AUTOMATED |
| QAT-24 | Idempotent retry of safe abort/restart and rejection of duplicate commit | AUTOMATED |
| QAT-25 | Evidence hash, canonical receipt, audit readback, and tamper detection | AUTOMATED |
| QAT-26 | Backup invalid/unavailable and restore failure paths | BOTH |
| QAT-27 | Target unavailable/corrupt after commit; no silent fallback | AUTOMATED |
| QAT-28 | Manual DB edits to state/history/flags fail closed | BOTH |
| QAT-29 | Finalized snapshot/checksum/lock preservation through rehearsal | AUTOMATED |
| QAT-30 | Generic profile boundary: 3/4/5 accepted; 2/6/35 rejected; no station selector | BOTH |

## Required assertions

The implementation cannot be considered trustworthy until every applicable case has a current, scope-bound result and no Critical/High failure remains unexplained. A test double, planning enum, eligibility receipt, or isolated rehearsal is not evidence of a production executor. Automated cases must not be labeled manual; manual cases must record the operator, observed state, environment, and exact receipt. Negative tests must verify no database mutation, no route activation, no authority change, no credential leakage, and no hidden fallback.

## Qualification gates

1. **Model gate:** one canonical state record, valid transitions, impossible-state rejection, monotonic revision, integrity protection, and correlation binding.
2. **Transaction gate:** atomic or explicitly fenced authority commit, audit behavior, route ordering, and deterministic crash resolution.
3. **Recovery gate:** verified backup/restore, abort boundary, post-commit rollback limits, `RECOVERY_REQUIRED`, and named owners.
4. **Security gate:** ShiftProfile initiation, action-bound ManagementCredential, explicit governance artifact, replay/expiry/scope/version controls, and no roles or universal secret.
5. **Isolation gate:** independent qualification DB and proof that rehearsal cannot touch or become authoritative over Production.
6. **Evidence gate:** immutable/hash-bound artifacts, audit readback, exact environment identity, and reproducible receipts.
7. **Manual gate:** controlled observation of restart/recovery/rollback where human judgment is required, without artificial Production timing or debug-only behavior.

No gate authorizes Production Activation or Cutover. A later governance package must separately accept the qualified evidence and authorize any implementation or execution phase. MQ-07's Phase 9.6B-only residual exception is not inherited by this plan or by any test case; MQ-07 remains BLOCKED.
