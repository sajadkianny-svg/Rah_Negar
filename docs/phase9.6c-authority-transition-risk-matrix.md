# Phase 9.6C - Authority Transition Risk Matrix

Scope: future design and qualification only. Current state is `LEGACY_AUTHORITATIVE`; no risk below authorizes a transition.

| ID | Failure scenario / trigger | Consequence | Detection | Fail-closed / recovery | Evidence and qualification | Sev. | Phase |
|---|---|---|---|---|---|---|---|
| AT-01 | Dual authority; conflicting commit or flags | Split brain/data divergence | Canonical record plus source/route reconciliation | `RECOVERY_REQUIRED`; freeze both routes; restore known state | Inject contradiction; receipt/hash | Critical | 9.6F |
| AT-02 | No authority after failed commit | No service or unsafe writes | Stable-state validator | Block operations; authorized recovery | Power-loss test | Critical | 9.6F |
| AT-03 | Target routing before authority; route flag races | Target serves uncommitted data | Route projection vs canonical revision | Disable service; retain Legacy | Ordering test | Critical | 9.6F |
| AT-04 | Legacy disabled too early | Loss of known source | Commit-order assertion | Abort if pre-commit; recovery if ambiguous | Fault injection | Critical | 9.6F |
| AT-05 | Target corrupted | Wrong operational data | SQLite integrity, checksum, counts | Abort; post-commit authorized rollback/recovery | Corrupt target fixture | Critical | 9.6F |
| AT-06 | Migration ledger mismatch/tamper | Wrong schema/data chain | Ledger checksum/order/version validation | Block migration/transition | Tamper test | High | 9.6F |
| AT-07 | Checksum mismatch | Untrusted artifact | Hash verification | Block and preserve source | Mismatch receipt | High | 9.6F |
| AT-08 | Schema mismatch | Runtime/query failure or silent loss | Exact schema/version fingerprint | Abort; no route | Wrong schema test | High | 9.6F |
| AT-09 | Backup invalid | Unrecoverable transition | Isolated open, integrity, checksum, manifest | Block before commit | Invalid backup test | Critical | 9.6F |
| AT-10 | Restore unavailable | Recovery cannot complete | Restore rehearsal and capability check | Block; `RECOVERY_REQUIRED` after ambiguity | Restore outage test | Critical | 9.6F |
| AT-11 | Process/machine crash during transition | Partial state | Lease, journal, revision and restart resolver | Roll back DB transaction or enter recovery; never infer | Crash at each stage | Critical | 9.6F |
| AT-12 | Audit write failure | Unreconstructable action | Same-transaction receipt/readback | Roll back commit or enter recovery | Disk-full/fault test | High | 9.6F |
| AT-13 | Authority metadata corrupted | Wrong source selected | MAC/hash chain, schema and revision checks | `RECOVERY_REQUIRED`; verified restore | Byte-tamper test | Critical | 9.6F |
| AT-14 | Unauthorized request | Privilege bypass | Shift + Management proof + governance binding | Reject and audit; no mutation | Missing/invalid proof | Critical | 9.6F |
| AT-15 | Replayed request | Duplicate transition | Unique correlation/request and consumed authorization | Reject idempotently | Replay test | High | 9.6F |
| AT-16 | Stale authorization | Old approval used | Expiry, revision, DB/scope/version binding | Reject; re-preflight | Expiry/stale test | High | 9.6F |
| AT-17 | Scope mismatch | Wrong station/deployment affected | Exact scope fingerprint | Reject; no mutation | Cross-scope test | Critical | 9.6F |
| AT-18 | Version mismatch | Incompatible code/data | App/schema/build binding | Reject; remain Legacy | Version test | High | 9.6F |
| AT-19 | Correlation mismatch | Evidence mixed across runs | Correlation on every artifact/record | Reject and quarantine package | Mixed-receipt test | High | 9.6F |
| AT-20 | Restart after partial transition | Nondeterministic route/authority | Startup resolver | Block until last commit/recovery proven | Restart matrix | Critical | 9.6F |
| AT-21 | Target DB unavailable | Unavailable service or fallback | Open/identity/integrity check | Abort; no silent Legacy/Target guess | Availability test | High | 9.6F |
| AT-22 | Rehearsal touches Production DB | Production mutation/corruption | Path/identity and before/after hash | Stop, quarantine, incident audit | Isolation proof | Critical | 9.6E/F |
| AT-23 | Manual DB flag manipulation | Bypass controls | Integrity tag, append-only history, reconciliation | `RECOVERY_REQUIRED`; no accepted flag | Direct-edit negative test | Critical | 9.6F |
| AT-24 | Target accepted while Legacy remains authoritative | Ambiguous authority | State transition invariant | Reject impossible combination | Model/property test | Critical | 9.6F |
| AT-25 | Audit finalization missing after committed state | Missing accountability | Receipt reconciliation on restart | Recovery/maintenance lock; no silent proceed | Audit omission test | High | 9.6F |
| AT-26 | Rollback after Target writes loses data | Data loss or split history | Write fence, reconciliation and snapshot lineage | No automatic rollback; governance decision/recovery | Post-commit rollback test | Critical | 9.6D/F |
| AT-27 | Legacy fallback silently serves stale data | Undetected divergence | Route must equal canonical state | Block rather than fallback | Route mismatch test | Critical | 9.6F |
| AT-28 | Invalid MQ-07 exception generalized | Uncontrolled waiver | Exception ID/scope allow-list | Reject non-MQ-07 exception | Governance negative test | High | 9.6B/F |

Severity uses Critical/High for safety or authority ambiguity. Each evidence package must include correlation ID, UTC time, scope, versions, old/new state, result, failure reason, fingerprints/checksums, and recovery outcome. Historical Rasht/Ramsar fixtures may be used only in isolated qualification; they do not prove generic Production behavior.
