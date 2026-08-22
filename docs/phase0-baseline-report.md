# Phase 0 Baseline and Safety Report

**Baseline ID:** `phase0_baseline_001`  
**Captured:** 2026-08-22, Asia/Tehran  
**Scope:** Read-only production-code assessment plus approved documentation/test shell

## 1. Repository state

RahNegar is a single-project C# WinForms solution. `Rah_Negar.sln` contains `Rah_Negar.csproj`; the project uses SDK-style `Microsoft.NET.Sdk`, `WinExe`, `net8.0-windows`, nullable reference types, implicit usings, WinForms, and AnyCPU/x64 configurations. Source is organized under `Core`, `Data`, `Models`, `Services`, `UI`, and `Utils`; `DataFiles` holds native/binary/package artifacts. Static inventory found 144 C# and 14 RESX files.

Git baseline before Phase 0 writes was `?? docs/`; the architecture documents were untracked. No tracked production modification was present. Generated `bin`/`obj` existed before this task. Phase 0 adds five requested documents and an empty `Rah_Negar.Tests` project shell, intentionally not added to the solution and containing no tests/packages.

## 2. Build baseline

| Configuration | Command | Result | Warnings | Errors |
|---|---|---|---:|---:|
| Debug | `dotnet build Rah_Negar.sln --configuration Debug --no-restore` | Success | 3 | 0 |
| Release | `dotnet build Rah_Negar.sln --configuration Release --no-restore` | Success | 3 | 0 |

Both builds produced `Rah_Negar.dll`. The three repeated NU1701 warnings are transitive compatibility warnings for OpenTK 3.1.0, OpenTK.GLControl 3.1.0, and SkiaSharp.Views.WindowsForms 3.119.0 restored for .NET Framework rather than `net8.0-windows7.0`. They are baseline risks, not fixed here.

Environment: Windows 10 build 26200; x64 .NET SDK 10.0.400/MSBuild 18.9.6; .NET 8.0.30 Windows Desktop runtime is installed; no `global.json`; no workloads. A reproducible build should pin an approved SDK later. Current successful build depended on existing restored assets because NuGet configuration at the user profile was inaccessible in this sandbox.

## 3. Dependency baseline

Direct packages: ClosedXML 0.105.0; Dapper 2.1.79; Microsoft.Data.Sqlite 10.0.9; QuestPDF 2026.6.0; ScottPlot.WinForms 5.1.58; Serilog 4.3.1; Serilog.Sinks.File 7.0.0; SourceGear.sqlite3 3.50.4.5; SQLitePCLRaw.bundle_e_sqlite3 2.1.11; SQLitePCLRaw.core 3.0.3; SQLitePCLRaw.lib.e_sqlite3 2.1.13; System.Runtime.CompilerServices.Unsafe 6.0.0. The project also directly references `DataFiles/Microsoft.Data.Sqlite.dll` while referencing the NuGet package.

Dependency risks requiring Phase 1/2 validation, not immediate upgrades:

- NU1701 compatibility warnings in the ScottPlot/OpenTK/SkiaSharp graph.
- Multiple SQLite distribution/provider versions and a loose Microsoft.Data.Sqlite DLL create native-provider/version ambiguity.
- `DataFiles` contains older/different local nupkg versions (including QuestPDF 2024.3.0 and SQLitePCLRaw variants) that may be redundant deployment artifacts.
- Automated vulnerability/deprecation/outdated queries were not completed: `dotnet list package` attempted to read inaccessible `%AppData%\NuGet\NuGet.Config`. Existing assets were inspected, but they do not constitute a current advisory check. Repeat with authorized NuGet access in internal validation; do not silently upgrade.

## 4. UI and application baseline

Startup calls `ApplicationConfiguration.Initialize()` then `SQLitePCL.Batteries.Init()` and chooses `FrmLogin` or `FrmStartup`. WinForms designer files generally use `AutoScaleMode.Font`; `UiScaleService` uses DeviceDpi/96 with a capped scale and `DataGridViewUiService` centralizes several fixed grid dimensions. Report and record forms create/configure multiple DataGridViews at runtime; Report Center enables double buffering and repeatedly builds columns.

Known UI constraints from code and approved audits: Event-heavy grids can fail to scroll appropriately; fixed sizes/disabled resizing and mixed Fill/fixed column policies are DPI risks; high-DPI behavior is not initialized through an explicit documented per-monitor policy before controls; layouts need 100/125/150/200% and small-screen validation; Report Center has numerous grids and large update paths; chart data exists but legacy UI does not present it; mixed Persian/English labels and weak correction messaging exist. These are recorded only—no redesign occurred.

## 5. Existing risks and limitations

- Confirmed Event/report defects remain as cataloged in the two legacy audits; Phase 0 does not fix them.
- Startup database open uses ReadWriteCreate, so running the application against a missing path can create a database. Fixture/test launch must isolate paths.
- Static schema creation exists without an explicit versioned migration ledger.
- Backup encryption contains an application-embedded key; this is a documented legacy security risk, not changed here.
- Production-accessible report test seeding exists and must be isolated in the Event phase.
- No automated test framework, coverage collector, CI definition, or test project existed at baseline.
- No SQLite CLI was available; live database schema was not opened. Database results are static-code discovery plus file metadata.
- No performance benchmark, UI automation, restore acceptance evidence, or approved production-data anonymization process exists yet.

## 6. Feature-switch and recovery strategy

Future components must be introduced behind typed, protected configuration with legacy behavior as default. New Event/runtime/report repositories and services use separate namespaces/contracts and additive storage until accepted. A switch selects one complete authority per workflow; do not uncontrolled dual-write. Shadow reads/calculations may compare results without becoming production authority. Switch changes are management-authorized, versioned, logged, audited, and bound to application/schema compatibility.

Rollback disables the new route and restores the compatible legacy application/database pair. Before any schema or authority activation, create a verified checksummed backup and retain the prior package/configuration. Post-activation writes require an explicit reconciliation plan; never silently discard or merge them. Database Migration/Restore follows the foundation specification and production cutover runbook.

## 7. Baseline conclusion

The existing solution builds in Debug and Release with three compatibility warnings and no compiler errors. Production behavior, source, solution membership, packages, and database were not changed. Phase 0 is ready for documentation review; dependency advisory verification, representative approved fixtures, restore drill evidence, and test-framework selection remain prerequisites before implementation.

