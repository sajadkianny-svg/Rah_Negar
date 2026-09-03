# Phase 9.4C — Qualification Environment Enablement

## Final status

**READY TO RERUN PHASE 9.4B**

This phase prepares a disposable, local-only qualification environment. It does not perform the Phase 9.4B manual qualification, change production authority, migrate production data, or alter normal startup behavior.

## Blockers addressed

The Phase 9.4B report recorded only environmental blockers. They were addressed as follows:

| Reported blocker | Phase 9.4C provision |
|---|---|
| No initialized qualification database | Repeatable generator creates initialized SQLite files outside the application `Data` directory. |
| No operator account/profile setup | Each generated file contains one qualification-only legacy login and one active ShiftProfile/operator row. |
| No Rasht 3-unit fixture | Deterministic Rasht file contains station identity, 3 units, two complete daily periods, odd-hour main data, daily unique data, and events. |
| No Ramsar 4-unit fixture | Deterministic Ramsar file contains the equivalent 4-unit scenario. |
| No controlled launch path | `launch-qualification.ps1` creates an isolated copy of the Release application and places the selected database in that copy's local `Data/db.sys`. |
| DPI environments unavailable | Exact human procedures are provided below; no DPI result is marked PASS here. |

No additional application defect was inferred from the Phase 9.4B report.

## Architecture and isolation

The qualification generator is `Qualification/QualificationEnvironment.cs`, hosted only for preparation by `QualificationTool`. It reuses the existing Rasht/Ramsar schema builders and creates fixed test content. The production `SqliteDatabaseHelper` is unchanged and always resolves `Data/db.sys` below the executable directory.

The launcher validates the existing Release executable and selected generated database, recreates `Qualification/qualification-run`, copies the complete Release output into it, creates its local `Data` directory, copies only the selected `db.sys` into that directory, and starts only the copied executable with that directory as its working directory. The production executable and production `Data/db.sys` are never selected or overwritten by the launcher. Pilot reads still use `PilotReadOnlySqliteConnectionFactory` and SQLite read-only mode; no qualification path adds a writer, migration, authority switch, RBAC, Support identity, or hidden bypass.

Generated files are disposable. The generator intentionally recreates only the selected qualification output directory's `Rasht/db.sys` and `Ramsar/db.sys` files. Never point it at a production `Data` directory.

## Exact setup steps

Run from the repository root in a PowerShell window:

```powershell
dotnet build Rah_Negar.sln -c Release
powershell -ExecutionPolicy Bypass -File .\Qualification\prepare-qualification.ps1 -OutputDirectory .\Qualification\qualification-data
```

The output is:

```text
Qualification/qualification-data/Rasht/db.sys
Qualification/qualification-data/Ramsar/db.sys
```

The preparation project is intentionally not part of the main solution; the script invokes `QualificationTool/QualificationTool.csproj` directly.

If a prior qualification run exists, preparation resets only those two generated files. It does not inspect or import real data. Keep the directory disposable and exclude it from any evidence package unless a sanitized checksum is specifically required.

## Exact launch steps

For Rasht (the script recreates the disposable application copy first):

```powershell
powershell -ExecutionPolicy Bypass -File .\Qualification\launch-qualification.ps1 -Station Rasht -QualificationDirectory .\Qualification\qualification-data
```

For Ramsar, close the first run and run the script again; it recreates the disposable copy and uses the Ramsar database:

```powershell
powershell -ExecutionPolicy Bypass -File .\Qualification\launch-qualification.ps1 -Station Ramsar -QualificationDirectory .\Qualification\qualification-data
```

The copied application still opens its ordinary login flow. Use the qualification-only legacy login below, then use the existing explicit Pilot entry and confirmation. The normal application executable and its ordinary `Data/db.sys` are not used for these runs.

## Qualification-only login and setup

Legacy login password for both generated files: `Qualification-9.4C!`

The generated ShiftProfile is present for Pilot authentication observation but is not a new application login route. Its non-secret identifiers are `qualification-rasht` / `Q-9.4C` for Rasht and `qualification-ramsar` / `Q-9.4C` for Ramsar. No password hash, recovery secret, or credential blob is documented or displayed. These values are valid only for regenerated disposable files and must not be used in production.

The generated data-start date is Persian `1405/01/01`. Both files contain data for `1405/01/01` and `1405/01/02`; the latest Pilot period is therefore `1405-01`. Each day has the twelve main records at `01,03,05,07,09,11,13,15,17,19,21,23`, one daily-unique row, and two optional events per unit. The deterministic station/unit IDs are `station-rasht-unit-1..3` and `station-ramsar-unit-1..4`.

## Rasht scenario

Expected station: Rasht Station; expected units: 3. The Pilot should reach the normal review lifecycle after explicit Start and expose exactly five workflows. Reporting has two complete days, pressure-like main values, daily unique values, and event data. Runtime observation has a deterministic two-event chain per unit and stable initial OFF/runtime bases. Authentication observes an initialized legacy source and an active qualification ShiftProfile. Export metadata is deterministic and read-only. The expected observation is stable across regeneration; any visual or lifecycle result still requires the Phase 9.4B human checklist.

## Ramsar scenario

Expected station: Ramsar Station; expected units: 4. The same five workflows and two complete days are present, with Ramsar's four-unit table shape and one deterministic event pair per unit. The expected observation is stable across regeneration and distinct from Rasht by station identity, unit count, schema shape, and unit IDs. Run it on a fresh generated/reset copy after the Rasht lifecycle.

## Cleanup and reset

Close the application normally. Delete the disposable `Qualification/qualification-data` directory only when no application process is using either database, or regenerate it with the preparation script. The application production database remains at its ordinary `Data/db.sys` path and is not part of cleanup. If a run is interrupted, terminate only the qualification-launched application, confirm no process still has the disposable file open, and regenerate the disposable directory.

## DPI manual procedure

No DPI check is PASS in Phase 9.4C. A human must run the complete applicable checklist at each scale:

1. Set Windows display scale to 100%, restart the qualification-launched application if Windows requests it, and run the selected station lifecycle from P9.4A-01 through P9.4A-35.
2. Repeat at 125% on a fresh/reset qualification database. Inspect RTL order, keyboard focus, five-row workflow grid, identity fields, action buttons, and return/close paths. Record only observed PASS/FAIL evidence.
3. Repeat at 150% on another fresh/reset qualification database. Pay particular attention to clipping, overlap, hidden fields, AutoScroll, and action reachability.

Capture sanitized screenshots and record scale, resolution, station, unit count, build, database-copy identifier, and timestamps. If only one scale is available to the operator, leave the other two rows `NOT EXECUTABLE`; do not infer them from `AutoScaleMode.Dpi` or automated tests.

## Automated validation added

`Rah_Negar.Tests/Qualification/QualificationEnvironmentTests.cs` proves deterministic regeneration, expected Rasht/Ramsar unit and source-data counts, active qualification profile presence, production-directory rejection, preservation of the normal default database path, isolated-copy launcher targeting, and the narrowly scoped Git exclusions. The existing source-integrity tests continue to verify that production startup navigation does not reference Pilot composition layers. The launcher does not alter the ordinary startup branch.

## Remaining blockers

No Phase 9.4C preparation blocker remains. Actual human evidence remains intentionally outstanding: the full Phase 9.4B lifecycle, visual RTL/keyboard observations, database before/after evidence, sanitized screenshots/run log, and separate 100%/125%/150% DPI observations. These are not claimed as completed here.

## Readiness decision

**READY TO RERUN PHASE 9.4B**

The environment is ready for a human rerun of the Phase 9.4B checklist. Stop after the qualification run if any report stop condition occurs. Do not use this environment or this decision to authorize production cutover, authority transition, Phase 9.5, commit, or push.
