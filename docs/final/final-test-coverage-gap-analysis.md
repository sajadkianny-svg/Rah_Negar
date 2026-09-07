# Rah_Negar final test coverage gap analysis

## Batch 1 coverage update (2026-09-07)

Batch 1 verification: 765/765 tests passed, zero failed/skipped, Release build 0 errors, and six known NU1701 warning identities. Focused Batch 1 security/integrity tests pass 5/5; qualification-named tests pass 16/16. New regression coverage proves removal of production seed/demo entry points, authenticated/versioned backup tamper rejection, canonical Event duplicate/transition/non-minute rejection, active runtime fail-closed behavior, and durable `RecoveryRequired` marker clearing. Existing managed SQLite boundary tests continue to cover staged restore failure injection, rollback, checksum, and restart-safe evidence. The qualification runner infrastructure defect remains open and is not represented as a product pass.

## Current evidence

Release baseline: 759/759 tests passed, zero failed/skipped, build 0 errors, and six known NU1701 warning identities (repeated across the two solution projects). The MQ-01…MQ-05 readiness support suites also passed: 3, 7, 16, 18, and 10 tests respectively. A real offline launch reached the Startup Wizard and initialized an isolated database on the prior package smoke test.

The full test suite is strongest in deterministic domain rules, target SQLite boundaries, provisioning, reporting contracts, target security, authority rejection, and pilot safety. Passing tests do not prove that the legacy WinForms path, installer, or manual operator experience is complete.

## Requirement-to-test coverage

| Area | Coverage result | Evidence | Missing coverage |
|---|---|---|---|
| Startup/first run | PARTIAL | `QualificationEnvironmentTests`, startup code, offline smoke | Automated full first-run UI flow, malformed settings, read-only directory, crash/restart. |
| Station/profile and 3/4/5 boundary | GOOD for target | `Phase95B5ProvisioningTests`, `Phase97ProductionExecutionTests` | Legacy UI profile composition and both-station end-to-end screen acceptance. |
| Dynamic grids/DPI/RTL | PARTIAL | UI contract/layout tests and prior qualification | Real 1920×1080 visual, keyboard, focus, RTL, font substitution, clipping at 100/125/150%. |
| Daily data/date/Persian calendar | GOOD at service level | daily sequence/missing-day/date tests | Read-only filesystem, malformed date input, UI correction messages. |
| Event state machine | GOOD for target; weak for legacy | runtime/event/authority tests; legacy audit | Legacy public insert/edit/delete path, silent duplicate rejection, invalid-time coercion, complete-chain integration. |
| Runtime/report calculations | PARTIAL | target runtime/report tests | Active legacy report path equivalence and historical pre-range reconstruction. |
| Finalization/snapshots/checksum | GOOD for target; partial product | provisioning/report snapshot tests | Legacy Report Center finalization integration and notification consistency. |
| Authentication/authorization | GOOD for target; partial product | security persistence/composition tests | Real login/ManagementCredential UI integration and protected maintenance action matrix. |
| ESD authorization | GOOD for target boundary | production security tests | Owner decisions on ESD effects and active product composition. |
| Backup/restore/recovery | GOOD for managed boundary; weak legacy | MQ-01 and managed boundary tests | Legacy encryption/tamper, safety-copy/atomic restore, recovery attack/expiry, active DB and power-loss tests. |
| SQLite integrity/WAL/locking | PARTIAL | helper policy and managed boundary tests | Legacy import/reset sidecars, low permissions, file locks, interrupted replacement. |
| Migration/upgrade | PARTIAL | migration/provisioning tests on disposable copies | Actual installer upgrade/reinstall/data-preservation rehearsal. |
| Crash/restart/failure injection | PARTIAL | target authority/rollback tests | Legacy forms/services, DB write interruption, logger failure, UI double-click/cancel. |
| Performance/memory | GAP | no controlled baseline | Startup, navigation, grid rebuild, report/PDF, memory/resource and long-history tests. |
| Installer/uninstall | GAP | no installer project | Full installer lifecycle and standard-user tests. |
| Offline/no-cloud | GOOD | source scan and package smoke | Repeat on final installer and clean isolated machine. |
| Manual operator acceptance | GAP | `docs/phase9.8-*` explicitly keeps real Production/manual review open | Independent screenshots, observations, runbook sign-off, and retained evidence. |

## Qualification runner result

`Qualification/run-phase9.7-final-qualification.ps1` was executed without touching Production state. It failed in its first step because `QualificationTool` compiles both `QualificationTool/Program.cs` and nested `QualificationTool/Phase98Probe/Program.cs`, producing CS8802 and duplicate generated assembly attributes. The script recorded unchanged Production pre/post state and disposable database inputs, but no Phase 9.7 step passed. This is a confirmed qualification infrastructure defect, not a product test pass.

## Coverage conclusion

The 765 passing tests establish a stronger Batch 1 service/contract baseline and no known regression. They do not establish complete product coverage. Final product acceptance still needs installer lifecycle, qualification-runner repair, UI/manual, performance, and upgrade evidence; the target-composition gate remains intentionally outside this batch.
