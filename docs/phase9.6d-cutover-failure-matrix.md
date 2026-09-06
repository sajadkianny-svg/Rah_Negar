# Phase 9.6D — Cutover Failure Matrix

Future design/qualification matrix only. Current state remains Legacy authoritative, Target non-authoritative, Target routing disabled, and Production Cutover unauthorized. `Recovery` means no normal operational route and no database guessing.

| ID | Stage | Failure | Detection | Immediate fail-closed action | Authority | Routing | Classification | Evidence | Qualification | Severity |
|---|---|---|---|---|---|---|---|---|---|---|
| CD-01 | Prepare | Prerequisite changed after eligibility | Re-evaluate revision/expiry/scope | Abort intent; retain Legacy | Legacy | Disabled | ABORT | stale receipt, correlation | Automated | High |
| CD-02 | Prepare | Backup invalid | preflight/hash/FK/WAL checks | Block; preserve source | Legacy | Disabled | ABORT | invalid manifest | Both | Critical |
| CD-03 | Prepare | Target integrity failure | integrity/FK/fingerprint | Block Target | Legacy | Disabled | ABORT | Target preflight | Automated | Critical |
| CD-04 | Prepare | Migration ledger mismatch | checksum/order/version classifier | Do not migrate/commit | Legacy | Disabled | ABORT | ledger receipt | Automated | High |
| CD-05 | Prepare | Target data divergence | final reconciliation/write-boundary check | Stop writes; do not commit | Legacy | Disabled | ABORT or RECOVERY | delta/reconciliation report | Both | Critical |
| CD-06 | Commit | Authority commit fails | transaction/receipt outcome | rollback transaction if proven; else recovery | Legacy or unknown | Disabled | ABORT or RECOVERY | transaction and restart receipt | Both | Critical |
| CD-07 | Commit | Routing enable fails | route projection vs canonical revision | keep all normal routes blocked | Target proven or unknown | Disabled | RECOVERY_REQUIRED | route projection error | Automated | Critical |
| CD-08 | Commit | Audit receipt fails | same-transaction readback | rollback if atomic; else recovery | Legacy or Target proven | Disabled | RECOVERY_REQUIRED | audit failure/readback | Automated | High |
| CD-09 | Prepare | Process crash before commit | startup intent + canonical record | complete proven abort; otherwise recovery | Legacy or unknown | Disabled | ABORT or RECOVERY | crash point, hashes | Both | Critical |
| CD-10 | Commit | Crash during commit | canonical revision/history | never infer; block and resolve | Unknown | Disabled | RECOVERY_REQUIRED | power-loss evidence | Both | Critical |
| CD-11 | Verify | Crash after commit | canonical record + route | resume verification under lock | Target if proven | Disabled until verified | RECOVERY_REQUIRED | restart receipt | Both | Critical |
| CD-12 | Verify | Restart during post-cutover verification | state/lease/readback | block until deterministic completion | Target or unknown | Disabled | RECOVERY_REQUIRED | restart/route receipt | Both | Critical |
| CD-13 | Recovery | Legacy unavailable | explicit identity/open/integrity | do not substitute Target or guess | Unknown | Disabled | RECOVERY_REQUIRED | availability/preflight | Both | Critical |
| CD-14 | Recovery | Target unavailable | explicit identity/open/integrity | no fallback; recover/abort by proven state | Legacy or unknown | Disabled | ABORT or RECOVERY | Target availability | Automated | High |
| CD-15 | Recovery | Both DBs available, authority ambiguous | canonical/history/route mismatch | block both and quarantine | Unknown | Disabled | RECOVERY_REQUIRED | ambiguity receipt | Both | Critical |
| CD-16 | Rollback | Target received new Production writes | write fence/lineage scan | reject automatic rollback; reconcile | Target | Target route blocked | ROLLBACK_NOT_SAFE | write-set and audit | Both | Critical |
| CD-17 | Recovery | Restore fails | staged restore/integrity checks | retain artifacts; block service | Unknown | Disabled | RECOVERY_REQUIRED | restore receipt | Both | Critical |
| CD-18 | Prepare | Checksum mismatch | SHA-256 compare | reject artifact | Legacy | Disabled | ABORT | expected/actual hashes | Automated | High |
| CD-19 | Prepare | Schema mismatch | exact schema/version | reject migration/route | Legacy | Disabled | ABORT | schema fingerprint | Automated | High |
| CD-20 | Entry | Unauthorized cutover attempt | Shift/Management/governance validation | reject, audit, no mutation | Legacy | Disabled | ABORT | redacted auth result | Both | Critical |
| CD-21 | Entry | Replayed authorization | unique consumed ID/correlation | reject idempotently | Legacy | Disabled | ABORT | replay receipt | Automated | High |
| CD-22 | Entry | Stale authorization | expiry/revision/version | reject and re-evaluate | Legacy | Disabled | ABORT | stale authorization | Automated | High |
| CD-23 | Entry | Wrong deployment/station scope | exact scope fingerprint | reject, quarantine package | Legacy | Disabled | ABORT | scope comparison | Both | Critical |
| CD-24 | Entry | Wrong application version | build/version binding | reject | Legacy | Disabled | ABORT | version evidence | Automated | High |
| CD-25 | Entry | Correlation mismatch | all artifacts bound to one ID | reject/quarantine mixed evidence | Legacy | Disabled | ABORT | mismatch fields | Automated | High |
| CD-26 | Any | Manual DB flag manipulation | integrity tag/history/reconciliation | enter recovery; never accept flags | Unknown | Disabled | RECOVERY_REQUIRED | tamper hashes | Both | Critical |
| CD-27 | Qualification | Qualification points at Production | path/identity + before/after hash | stop run; incident evidence; no cleanup guess | Legacy | Disabled | RECOVERY_REQUIRED | production hash proof | Both | Critical |
| CD-28 | Abort | Crash during abort | intent/artifact/authority scan | complete only proven cleanup | Legacy or unknown | Disabled | ABORT or RECOVERY | abort/restart receipt | Both | High |
| CD-29 | Rollback | Crash during rollback | canonical state + staged artifacts | block; resume only proven step | Legacy/Target/unknown | Disabled | RECOVERY_REQUIRED | rollback crash receipt | Both | Critical |
| CD-30 | Verify | Target finalized report/audit lineage missing | snapshot/lock/audit readback | reject stable state; reconcile/recover | Target | Disabled | ROLLBACK_NOT_SAFE or RECOVERY | lineage evidence | Both | Critical |

No row authorizes a transition. “Legacy” in the Authority column means the known current state is preserved; “unknown” requires recovery, not fallback.
