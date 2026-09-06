# Phase 9.6D — Cutover / Abort / Rollback Qualification Plan

Status: **FUTURE PLAN — NO TEST CLAIMED PASS**

All tests use disposable production-like copies, explicit paths, unique correlation IDs, and immutable/hash-bound evidence. No test may use or mutate the Production DB, enable Production routing, or change current authority. Rasht/Ramsar fixtures are permitted only as isolated fixtures. MQ-07 remains BLOCKED; automated evidence is not manual PASS.

| ID | Area | Qualification case | Class |
|---|---|---|---|
| QD-01 | Cutover | Happy-path rehearsal through prepared, commit, verification, stable Target state | BOTH |
| QD-02 | Cutover | Each entry precondition fails closed | AUTOMATED |
| QD-03 | Cutover | Eligibility invalidated by expiry, revision, scope, DB hash, or version change | AUTOMATED |
| QD-04 | Cutover | Canonical authority commit atomicity and impossible-state rejection | AUTOMATED |
| QD-05 | Cutover | Routing cannot enable before valid Target authority | AUTOMATED |
| QD-06 | Cutover | Restart persistence after every committed state | BOTH |
| QD-07 | Abort | Abort at every valid pre-commit stage | AUTOMATED |
| QD-08 | Abort | Repeated abort is idempotent and leaves no unsafe staging artifacts | AUTOMATED |
| QD-09 | Abort | Crash during abort and deterministic restart cleanup | BOTH |
| QD-10 | Abort | Legacy authority and disabled routing confirmed after cleanup | BOTH |
| QD-11 | Rollback | Rollback immediately after commit before Target writes | BOTH |
| QD-12 | Rollback | Rollback after controlled Target writes with lossless reconciliation | BOTH |
| QD-13 | Rollback | Unsafe rollback after unaccounted Target writes is rejected | AUTOMATED |
| QD-14 | Rollback | Reconciliation-required case preserves every delta, report, event, lock, and audit lineage | BOTH |
| QD-15 | Rollback | Restore verification, staged replacement, prior-copy retention, and checksum validation | AUTOMATED |
| QD-16 | Rollback | Restart during rollback at every failure point | BOTH |
| QD-17 | Recovery | Corrupted authority record/history | AUTOMATED |
| QD-18 | Recovery | Impossible authority/routing state | AUTOMATED |
| QD-19 | Recovery | Missing Legacy, missing Target, both present but ambiguous, neither usable | BOTH |
| QD-20 | Recovery | Integrity, FK, checksum, schema, or migration-ledger failure | AUTOMATED |
| QD-21 | Recovery | Missing/corrupt audit receipt and commit interruption | BOTH |
| QD-22 | Recovery | Routing projection mismatch blocks service | AUTOMATED |
| QD-23 | Security | Invalid ManagementCredential proof | AUTOMATED |
| QD-24 | Security | Missing governance authorization | BOTH |
| QD-25 | Security | Replay, stale authorization, wrong scope, wrong version, wrong correlation | AUTOMATED |
| QD-26 | Security | ShiftProfile initiation, Management proof, governance approval, and system validation remain separate | BOTH |
| QD-27 | Isolation | Rehearsal never touches Production DB; Production hash is unchanged before/after | BOTH |
| QD-28 | Isolation | Qualification database is independent, disposable, explicit-path, and independently integrity-verified | AUTOMATED |
| QD-29 | Isolation | No station-specific Production branch and no unsupported Unit count; 3/4/5 accepted, 2/6/35 rejected | BOTH |
| QD-30 | Evidence | Correlation, UTC, scope, build/schema, database identity, hashes, receipts, and audit readback are complete | AUTOMATED |
| QD-31 | Data boundary | Operational writes are fenced/drained before final synchronization | BOTH |
| QD-32 | Backup | Source/WAL handling, immutable backup, SHA-256, SQLite integrity, FK integrity, schema, ledger, and isolated restore | BOTH |

## Qualification gates

1. Model: one canonical record, monotonic revision, integrity protection, valid transitions, and impossible-state rejection.
2. Transaction/route: exclusive write fence, commit ordering, route projection, audit coupling, and deterministic crash resolution.
3. Data: final synchronization, no unaccounted writes, report/snapshot/lock preservation, and reconciliation evidence.
4. Recovery: verified backup/restore, abort boundary, rollback limits, named ownership, and safe startup blocking.
5. Security: ShiftProfile plus action-bound ManagementCredential plus explicit governance authorization; no roles or bypasses.
6. Isolation/evidence: production identity protection, independent qualification DB, immutable receipts, hash/readback, and reproducibility.
7. Manual observation: only where human observation is genuinely required; no artificial Production timing and no claim that automated invariant evidence is manual PASS.

No listed test has been implemented or executed by Phase 9.6D. A future result must record PASS/FAIL/BLOCKED, exact environment/database identities, operator/reviewer, build/schema, correlation, evidence hashes, and authority/routing before and after. A passing rehearsal does not authorize Production Activation or Cutover.
