# Phase 0 Legacy Finding Traceability Matrix

This matrix links confirmed legacy findings to the phase that owns remediation and the evidence required before acceptance. Finding identifiers come from the approved Event/report audits.

| Legacy audit finding | Future phase | Required validation/test |
|---|---|---|
| HIGH-01 Running + OH accepted/terminates runtime | 3 Event; 4 Runtime | Transition rejection; correction message; projection unchanged after rejected command |
| HIGH-02 edit/delete/reassign bypasses downstream chain | 3 Event | Full chain reconstruction for edit/delete; transaction rollback; finalized lock |
| HIGH-03 no Event uniqueness/domain constraints | 2 Database; 3 Event | Constraint/index cases; duplicate/same-time policy; legacy anomaly preflight |
| HIGH-04 public persistence bypasses rules | 3 Event | Direct repository/handler misuse denial; all mutations use command/transaction/audit |
| HIGH-05 reports omit pre-range runtime history | 4 Runtime; 5 Reporting | Prior-event/baseline range fixtures; approved runtime/report totals |
| HIGH-06 ESD adjustment on invalid stopped-state data | 3 Event; 4 Runtime | ESD state matrix and audit; invalid sequence cannot affect projection |
| MEDIUM-01 silent duplicate rejection | 3 Event; 6 UI | Structured error and Persian correction; no partial mutation |
| MEDIUM-02 invalid Event time omitted/coerced | 2 Database; 3 Event; 7 Migration | Strict parsing/constraints; quarantine; never coerce to midnight |
| MEDIUM-03 Event identity/audit destroyed on edit | 2 Database; 3 Event; 7 Migration | Stable ID; append-only before/after audit; historical-link reconciliation |
| MEDIUM-04 nondeterministic ordering | 2 Database; 3 Event; 4 Runtime | Canonical timestamp/tie-breaker sorting and repeatable projection |
| MEDIUM-05 Events coupled to mandatory observations | 3 Event; 6 UI | Event-only day scenario; daily completeness remains independently enforced |
| MEDIUM-06 production test seeding rewrites authority | 3 Event; 8 Cutover | Production build/path cannot invoke seeder; lock/authorization tests |
| LOW-01 inaccurate delete confirmation | 6 UI | Message matches staged/committed behavior; cancellation leaves data unchanged |
| LOW-02 Event-heavy grids cannot scroll | 6 UI | Large-chain keyboard/mouse/scroll test at supported DPI/resolutions |
| LOW-03 mixed labels/weak guidance | 6 UI | Persian terminology and actionable correction acceptance |
| LOW-04 U4 normalization inconsistent | 3 Event; 7 Migration | Canonical Unit mapping and Rasht/Ramsar fixtures |
| CODE QUALITY-01 duplicate normalization/date conversion | 3 Event; 4 Runtime | One approved service per rule; cross-path equivalence regression |
| CODE QUALITY-02 dead/incomplete state-machine migration | 3 Event; 8 Cutover | New authority proven; unreachable legacy path inventory before retirement |
| RPT-01 finalized reports mix snapshots/live data | 5 Reporting | Finalized reads use snapshot only; source changes do not alter output/checksum |
| RPT-02 runtime report uses legacy calculation | 4 Runtime; 5 Reporting | Report consumes authoritative RuntimeProjectionService/version |
| RPT-03 invalid Event sequences alter runtime report | 3 Event; 4 Runtime; 5 Reporting | Invalid chain rejected/quarantined and excluded with explicit failure |
| RPT-04 stale in-memory finalization | 5 Reporting | SourceRevision concurrency check; atomic regenerate/snapshot/finalize |
| RPT-05 incomplete pending-finalization definition | 5 Reporting | 12-hour, daily-unique, Station/date and all section completeness matrix |
| RPT-06 same-time order unprotected | 2 Database; 3 Event; 4 Runtime | Deterministic key/ordering and repeatable report checksum |
| RPT-07 duplicate hourly samples bias results | 2 Database; 5 Reporting; 7 Migration | Duplicate prevention/detection; min/max/average controls; quarantine report |
| RPT-08 invalid stored time becomes midnight | 3 Event; 5 Reporting; 7 Migration | Strict canonical parsing; explicit invalid-data outcome; no silent coercion |
| RPT-09 chart generated but not presented | 6 UI | Chart visibility, empty/error states, DPI/export acceptance |
| Finalized lock/reopen weaknesses | 1 Foundation; 5 Reporting | Management-authorized Reopen only; immutable original/supersession/audit |
| SQLite/local-file bypass limitation | 1 Foundation; 2 Database; 8 Cutover | ACL/path checks, integrity/identity, direct-command denial, backup/audit custody |
| No automated test/coverage baseline | 0 Baseline; all later phases | Approved test framework, repeatable commands, four-level gates, coverage evidence |
| Dependency/provider ambiguity and NU1701 | 0 Baseline; 1–2 Foundation/Database | Authorized advisory scan, compatibility/native-load tests, reviewed dependency decision |

## Phase acceptance use

Every implementation work item references one or more rows. A finding closes only when its authoritative specification is implemented, required automated/manual evidence passes, and any intended legacy-output change is approved. “Not reproduced” without a representative fixture is not closure. New confirmed findings are added with owner phase and test before remediation.

