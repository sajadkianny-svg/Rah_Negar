# Rah_Negar final test coverage gap analysis

## Batch 1 and Batch 2 coverage update (2026-09-07)

Batch 2 adds data-root migration, inactive Target write-boundary, UI policy/DPI, payload, and installer lifecycle coverage. The clean Release build and full suite are rerun after these changes. Real desktop UI acceptance remains intentionally unclaimed because no native WinForms surface is available in this environment.

## Current evidence

Batch 3 validation: 781/781 tests passed, zero failed/skipped, Release build 0 errors/0 warnings, and the clean installer payload audit passed with 500 files. The MQ-01…MQ-05 readiness support suites and Phase 9.7 qualification also passed. Native visual acceptance remains intentionally unclaimed because it requires a human on the supported Windows workstation.

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
| Performance/memory | SOURCE COMPLETE / NATIVE FOLLOW-UP | Batch 3 cache probe: 1 miss/1,000 hits; repeated column definitions do not rebuild | Native startup, navigation, report/PDF, memory/resource observation remains H-05. |
| Installer/uninstall | COMPLETE FOR PAYLOAD / NATIVE FOLLOW-UP | Self-contained publish and payload validator passed; lifecycle evidence is retained from the isolated installer harness | Final Setup.exe designation awaits H-05; restricted-account/interrupted-install observation remains follow-up. |
| Offline/no-cloud | GOOD | source scan and package smoke | Repeat on final installer and clean isolated machine. |
| Manual operator acceptance | GAP | `docs/phase9.8-*` explicitly keeps real Production/manual review open | Independent screenshots, observations, runbook sign-off, and retained evidence. |

## Qualification runner result

`Qualification/run-phase9.7-final-qualification.ps1` now builds `QualificationTool` explicitly before running it and excludes the nested probe sources from the parent project. A clean execution completed PASS with isolated artifacts and no Production database access. The earlier CS8802/duplicate-attribute failure is resolved.

## Batch 3 coverage update

Added `Rah_Negar.Tests/Batch3/Batch3CompletionTests.cs` with meaningful coverage for report-summary empty/tie behavior, grid-cache reuse statistics, qualification-root isolation/product-assembly exclusion, redacted logger output, installer source invariants, and the dead-handler/debug-message source gate. `Qualification/run-batch3-performance.ps1` records the cache probe and focused test result as machine-readable evidence. `Qualification/run-final-ui-acceptance.ps1` prepares a separate database/data root and records native-session isolation without attempting authentication or marking human visual results.

## Coverage conclusion

The passing suite and qualification run establish the automated Batch 3 baseline. Native UI/manual acceptance remains H-05; technical MEDIUM/LOW gaps are closed. Target composition is intentionally inactive and is covered only by its safety boundary, not by activation or cutover tests.
