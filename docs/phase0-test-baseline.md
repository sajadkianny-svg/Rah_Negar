# Phase 0 Test Infrastructure Baseline

## 1. Existing state

No test project, test framework package, `.runsettings`, coverage configuration, CI test job, or business test source was found. `Services/Reports/TestDataSeederService.cs` is production source, not an automated test project, and its production accessibility is a known risk.

Debug and Release solution builds succeeded with three NU1701 warnings and zero errors. No automated tests ran because none exist.

## 2. Phase 0 test shell

`Rah_Negar.Tests/Rah_Negar.Tests.csproj` was added as an empty `net8.0-windows` project shell with nullable/implicit usings, `IsTestProject=true`, `IsPackable=false`, and a project reference to Rah_Negar. It contains no test-framework dependency and no business test. It is deliberately not added to `Rah_Negar.sln`, so the approved baseline solution/build and restore graph are unchanged.

Before activation, approve xUnit, NUnit, or MSTest; compatible `Microsoft.NET.Test.Sdk`; coverage collector; package versions/advisories; fixture copying; and Windows CI/runtime requirements. Then add the project to the solution in a separately reviewed test-infrastructure batch.

## 3. Required capability

The selected stack must support deterministic unit tests without WinForms/database, SQLite integration tests in unique temporary directories, end-to-end scenario fixtures, and manual acceptance evidence. Coverage should emit machine-readable line/branch results and exclusions, but percentage alone is not a gate: all critical state transitions, failure branches, transactions, locks, Persian boundaries, and calculation rules require explicit cases.

Tests must never resolve the production `Data/db.sys`. Each database test creates a unique temporary directory/file, disables parallel access to a shared fixture, closes pools/connections, and preserves a failed copy only in an approved artifact location. Clocks, IDs, Station context, and current session must be injectable in future architecture.

## 4. Gate baseline

For every later phase require: whole-solution build, prior regression suite, new unit/integration/scenario results, affected-workflow manual acceptance, diff review, secret scan, database hash/control totals where applicable, and unresolved-risk record. Critical/high failures block phase completion.

Initial suites should follow the traceability matrix: Event transition/chain/duplicate/lock tests; runtime baseline/OH/ESD/range tests; report aggregation/completeness/snapshot tests; authentication/recovery tests; backup/Restore/Migration integrity tests; and WinForms DPI/keyboard/manual cases.

## 5. Coverage limitations

Coverage is currently unavailable. Static and legacy-audit evidence is the only baseline. The first approved testing batch must establish repeatable commands, SDK pinning, package restore access, test-result retention, and coverage reports before any database migration or subsystem replacement.

