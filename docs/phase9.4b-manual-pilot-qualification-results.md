# Phase 9.4B — Actual Manual Pilot Qualification Results

## Final status

**BLOCKED**

Phase 9.4B manual qualification was not completed because the available local build was not an initialized installation. The application launched to the Startup Wizard and no prepared, disposable SQLite database copies or initialized operator account were available in the workspace. The Pilot lifecycle therefore could not be entered safely without creating setup data, which this qualification explicitly prohibits.

This record stops at Phase 9.4B. It does not authorize production cutover, authority transition, schema change, commit, push, or Phase 9.4C.

## Environment used

- Repository: `D:\Projects\RahNegar_SQLite\Rah_Negar`
- Branch: `phase9-operational-readiness`
- Starting commit: `4fe303a` (`Complete Phase 9.4A manual pilot qualification preparation`)
- Application under test: `bin\Release\net8.0-windows\Rah_Negar.exe`
- Target framework: `net8.0-windows`
- Operating environment: Windows desktop, Europe/Berlin timezone, test date 2026-09-03
- Execution mode: local/offline; no external services or production tooling used
- Initial application launch: Release executable launched successfully; visible window was `Rah_Negar Startup Wizard`
- Database/setup state: no SQLite database file was present under the project workspace/DataFiles; the application reported the uninitialized startup path by opening the Startup Wizard
- Prepared station copies: unavailable
- Prepared operator account: unavailable
- Persian period/data-start-date identity: not available
- Before-test database hashes: not available because no disposable station database was supplied
- Tester initials: Codex execution record
- Screenshots/screen recording: none retained; no application Pilot screen was reached
- DPI environments: only the current desktop scale was available; 100%, 125%, and 150% qualification runs were not separately available

## Verification evidence

The following objective evidence was inspected without changing application data:

- `Program.cs`: startup selects `FrmStartup` when `AppInitializationService.IsInitialized()` is false.
- `UI/Forms/FrmMain.cs`: explicit `Pilot / فقط خواندنی` link, keyboard tab stop, confirmation dialog, explicit Yes/No gate, and legacy-preserving error path are present.
- `UI/Composition/Pilot/LivePilotCompositionRoot.cs`: composition is constructed only after explicit caller action; the composition exposes no production, migration, settings-writer, event-writer, or ESD mutation executor; the blocked preflight view contains five workflow rows.
- `UI/Composition/Pilot/FrmLivePilot.cs`: Pilot form is read-only-labeled, RTL-enabled, DPI-scaled, has Start/Complete/Stop/Return actions, disables Complete/Stop before review, and exposes `AutomaticallyStarts == false`, `ReplacesLegacyWindow == false`, and `SwitchesAuthority == false`.
- `UI/Pilot/PilotDashboardControl.cs`: safety banner, station/session/authority/preflight/monitoring/rollback/stop/completion fields, five-workflow surface, comparison and fingerprint rendering are implemented.
- `Rah_Negar.Tests/Pilot/LivePilotPhase93Tests.cs`, `Rah_Negar.Tests/UI/PilotDashboardSurfaceTests.cs`, and `Rah_Negar.Tests/UI/PilotDashboardHardeningTests.cs`: automated safety and presentation coverage exists.
- `dotnet build Rah_Negar.sln -c Release --no-restore /m:1`: succeeded, 0 errors, 6 NU1701 warnings.
- `dotnet test Rah_Negar.sln -c Release --no-restore`: passed 637, failed 0, skipped 0.
- Startup Wizard cancellation was exercised through the UI automation tree; the application exited without an unhandled exception. This does not qualify Pilot shutdown/cancellation paths.

## Checklist results

Legend: `PASS` means directly observed or objectively verified from the current implementation/tests. `FAIL` means an observed defect. `NOT EXECUTABLE` means the required manual state or prerequisite was unavailable; it is not a simulated result.

| ID | Result | Evidence / notes |
|---|---|---|
| P9.4A-01 | NOT EXECUTABLE | Release launch was observed, but it opened Startup Wizard because the installation was not initialized; normal legacy main window could not be reached safely. Evidence: `Program.cs`; launch observation. |
| P9.4A-02 | PASS | Objective source verification: explicit `Pilot / فقط خواندنی` LinkLabel with `TabStop = true` in `UI/Forms/FrmMain.cs`. Not visually observed in the running main window. |
| P9.4A-03 | PASS | Objective source verification: `OpenReadOnlyPilot` shows a Yes/No confirmation before composition/preflight. Not manually reached. |
| P9.4A-04 | PASS | Objective source verification: confirmation text identifies Pilot/read-only operation and states Legacy remains the operating authority. Not manually reached. |
| P9.4A-05 | NOT EXECUTABLE | No initialized legacy window was available from which to select No/cancel. |
| P9.4A-06 | NOT EXECUTABLE | No initialized legacy window/database was available from which to select Yes and begin preflight. |
| P9.4A-07 | PASS | Objective source verification: `FrmLivePilot` title, RTL settings, accessible name, and `PilotDashboardControl` Persian safety banner are implemented. Not visually observed. |
| P9.4A-08 | NOT EXECUTABLE | Keyboard/RTL behavior could not be exercised because Pilot did not open. |
| P9.4A-09 | PASS | Objective source verification: dashboard declares and renders identity, station, session, authority, preflight, monitoring, rollback, stop, and completion fields. Not visually observed. |
| P9.4A-10 | NOT EXECUTABLE | No prepared Rasht/Ramsar station/session scenario was available. |
| P9.4A-11 | NOT EXECUTABLE | Pilot preflight could not be run without an initialized database. |
| P9.4A-12 | PASS | Objective source verification: Legacy authority indicator/banner and no authority-switch property are present. Evidence: `FrmLivePilot.cs`, `LivePilotCompositionRoot.cs`. |
| P9.4A-13 | PASS | Objective source verification: Start depends on ready composition; Complete and Stop start disabled; Return is available. Evidence: `FrmLivePilot.cs`. |
| P9.4A-14 | NOT EXECUTABLE | Start action could not be reached. |
| P9.4A-15 | NOT EXECUTABLE | Post-start lifecycle could not be reached. |
| P9.4A-16 | PASS | Objective source verification: exactly five `PilotValidationWorkflow` values are registered/rendered: Authentication, Reporting, RuntimeEvent, ProtectedSettings, Export. Evidence: composition root and dashboard mapping. |
| P9.4A-17 | PASS | Objective source verification: each workflow view carries an explicit status; blocked view uses `اجرا نشد` rather than a blank row. Not visually observed. |
| P9.4A-18 | PASS | Objective source verification: each workflow view carries comparison status and safe unavailable/blocked values; no authority transition is wired. Not visually observed. |
| P9.4A-19 | PASS | Objective source verification: `LivePilotDashboardView.FingerprintVersion` is rendered per workflow; expected v1 identifiers are covered by the Phase 9.3 tests/source. Not visually observed. |
| P9.4A-20 | PASS | Objective source verification: monitoring, rollback readiness, stop reason, and completion fields are part of the live dashboard; rollback is evidence-only. Not visually observed. |
| P9.4A-21 | PASS | Objective source verification: composition exposes no production/migration/settings/event/ESD writer or executor; Pilot controls are observation lifecycle actions only. No write action was offered during the blocked startup state. |
| P9.4A-22 | PASS | Objective source verification: dashboard uses safe identifiers/messages and sanitized evidence rendering; no credentials or connection strings are rendered by the Pilot surface. Pilot screen was not reached. |
| P9.4A-23 | NOT EXECUTABLE | Operator-review Stop path could not be reached. |
| P9.4A-24 | NOT EXECUTABLE | Fresh disposable lifecycle/database copy was unavailable; Complete path could not be reached. |
| P9.4A-25 | NOT EXECUTABLE | Active Pilot Return confirmation could not be reached. |
| P9.4A-26 | NOT EXECUTABLE | Pilot close/return to a live legacy window could not be reached. Startup Wizard cancellation alone is insufficient evidence. |
| P9.4A-27 | NOT EXECUTABLE | In-progress Pilot cancellation could not be reached. |
| P9.4A-28 | NOT EXECUTABLE | Pilot application-shutdown path could not be reached. Startup Wizard cancellation exited safely but is not the required Pilot check. |
| P9.4A-29 | NOT EXECUTABLE | No prepared database copy or before/after hash existed for read-only comparison. No database was modified by this qualification. |
| P9.4A-30 | NOT EXECUTABLE | A complete 100% DPI Pilot lifecycle could not be run. |
| P9.4A-31 | NOT EXECUTABLE | A complete 125% DPI Pilot lifecycle could not be run; no separate DPI environment was available. |
| P9.4A-32 | NOT EXECUTABLE | A complete 150% DPI Pilot lifecycle could not be run; no separate DPI environment was available. |
| P9.4A-33 | NOT EXECUTABLE | Rasht 3-unit prepared data/setup was not available. No station setup was created or altered. |
| P9.4A-34 | NOT EXECUTABLE | Ramsar 4-unit prepared data/setup was not available. No station setup was created or altered. |
| P9.4A-35 | NOT EXECUTABLE | No sanitized screenshots, run log, or database before/after evidence could be captured because the required Pilot state was unavailable. |

## Counts

- PASS: **14**
- FAIL: **0**
- NOT EXECUTABLE: **21**
- Total checklist items: **35**

The PASS count includes only objective implementation/test verification for rows that can be established without entering the unavailable station lifecycle. It does not convert missing manual evidence into a completed qualification.

## Defects found and corrections

### Defects found

No genuine Pilot defect was confirmed. No Pilot code defect was reproduced because the application could not reach the Pilot surface.

### Corrections made

None. No source code, database schema, database contents, configuration, or project architecture was changed. No automated regression test was added.

The Startup Wizard was not completed because doing so would create or mutate setup/database state and would not provide the required prepared disposable Rasht/Ramsar qualification scenarios.

## DPI results

No manual DPI qualification result is available. The required 100%, 125%, and 150% complete lifecycle runs were NOT EXECUTABLE because Pilot could not be opened and separate DPI environments were not available. Objective implementation evidence shows `AutoScaleMode.Dpi`, minimum/initial sizes, RTL layout, and an action panel, but that is not a substitute for visual DPI observation.

## Rasht and Ramsar results

- Rasht / 3 units: **NOT EXECUTABLE**. No prepared local copy or setup was supplied; no data was created or edited.
- Ramsar / 4 units: **NOT EXECUTABLE**. No prepared local copy or setup was supplied; no data was created or edited.

The station-specific source/profile separation was not treated as a manual station result.

## Residual limitations

- The legacy main window, Pilot entry, confirmation outcomes, and Pilot dashboard were not visually reached.
- No valid Persian period, data-start-date relationship, session identity, or station identity could be recorded from the application.
- No preflight, observation, workflow status, Match/Difference, fingerprint, monitoring, Stop, Complete, Return, Pilot close, cancellation, or Pilot shutdown lifecycle was manually exercised.
- No database integrity before/after evidence exists for a station scenario.
- RTL readability, keyboard traversal, and actual 100%/125%/150% layout behavior remain unqualified.
- The existing six NU1701 warnings remain: OpenTK 3.1.0, OpenTK.GLControl 3.1.0, and SkiaSharp.Views.WindowsForms 3.119.0 use .NET Framework compatibility assets for the net8.0-windows targets. This is a pre-existing dependency warning, not a Phase 9.4B correction.

## Operator acceptance conclusion

Operator acceptance is **not granted**. The implementation has objective automated/source evidence for several safety and presentation properties, and the available executable launched and canceled safely at the Startup Wizard. The required actual manual pilot qualification, including both station scenarios and lifecycle paths, was blocked by missing initialized test assets and DPI environments.

## Readiness decision for Phase 9.4C

**BLOCKED — do not begin Phase 9.4C.**

Before any future qualification attempt, provide an initialized local/offline installation with a verified backup or disposable database copy for Rasht (3 units), a separate restored copy for Ramsar (4 units), an authorized operator account, a valid post-data-start Persian period, before-test integrity evidence, sanitized capture capability, and 100%/125%/150% DPI environments. Then rerun the full checklist from P9.4A-01; do not infer the currently NOT EXECUTABLE rows from source or automated tests.

