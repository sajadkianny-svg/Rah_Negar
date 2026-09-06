# Phase 9.6E — Rehearsal Gap Register

| ID | Status | Evidence / boundary |
|---|---|---|
| E-G01 | CLOSED FOR ISOLATED REHEARSAL | Qualification-only Target authority transition, restart, routing ordering, fencing, reconciliation, abort, rollback safety, recovery, audit, and backup binding are covered by `Phase96ERehearsalTests` plus D3 tests. |
| E-G02 | OPEN | No Production authority executor, physical database swap, write drain, or Production rollback is implemented or authorized. |
| E-G03 | OPEN | Production governance/repair, immutable audit retention/readback, and manual MQ-07 observation remain outside automated rehearsal. |
| E-G04 | OPEN | Package compatibility remains the known six NU1701 package-condition warnings (12 emitted project warnings); dependencies were not changed. |

MQ-07 remains exactly: **BLOCKED  MANUAL OBSERVATION NOT PRACTICALLY EXERCISABLE, WITH AUTOMATED INVARIANT EVIDENCE RETAINED**.
