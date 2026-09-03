# Phase 9.4A — Manual Pilot Qualification Checklist

Status: preparation only. This document is an executable checklist for a human operator on a local installation. It does not record a completed manual qualification and does not authorize production cutover, authority transition, schema change, commit, or push.

## Preparation record

- Baseline: commit `61d9425`, branch `phase9-operational-readiness`.
- Scope inspected: Phase 9.3 live Pilot implementation and tests, current Pilot UI, and directly required UI/composition dependencies only.
- UI correction: none required. The existing surface already provides explicit entry, Persian read-only/legacy warning and confirmation, preflight result, start, review, stop, complete, and return actions.
- New automated tests: none.
- Build: `dotnet build Rah_Negar.sln -c Release` — succeeded, 0 errors.
- Tests: `dotnet test Rah_Negar.sln -c Release` — passed 637, failed 0, skipped 0, total 637.
- `git diff --check`: required after this document is saved; result is recorded at the end of this file.
- Known build warnings: 12 NU1701 warnings. They concern `OpenTK 3.1.0`, `OpenTK.GLControl 3.1.0`, and `SkiaSharp.Views.WindowsForms 3.119.0` restored through .NET Framework compatibility assets for the net8.0-windows target. They are pre-existing dependency-health warnings and are not resolved by Phase 9.4A.
- Manual qualification readiness: **READY FOR MANUAL QUALIFICATION**, subject to the prerequisites and stop conditions below. This is not a statement that manual qualification has been performed.

## Prerequisites

1. Use a local installation of the build under test. Keep the application offline; do not connect external services or use production cutover tooling.
2. Start from a verified backup or disposable copy of the SQLite database. Record the database file path and a before-test file hash or equivalent read-only evidence without placing secrets in the evidence set.
3. Use an operator account authorized to open the existing application and view both supported station scenarios. Do not use or record passwords, hashes, connection strings, recovery data, or personal data.
4. Prepare two test runs or two disposable database copies:
   - Rasht: station identity Rasht, configured with the expected 3-unit scenario.
   - Ramsar: station identity Ramsar, configured with the expected 4-unit scenario.
5. Select a valid local data period at or after the configured data start date. Record the Persian period identity shown by the application. Do not edit data to make the Pilot pass.
6. Set the Windows display scale to 100%, 125%, and 150% in separate runs, or use equivalent local DPI test environments. Restart the application after changing scale when Windows requires it.
7. Have a screen-capture tool available. Capture only application state and test identifiers; redact any sensitive information before retaining evidence.
8. The tester must understand that all Pilot observations are read-only and that Legacy remains the operating authority throughout this phase.

## Test data and setup requirements

For each station scenario, record the following in the notes field of the first applicable check:

- station name and station identifier;
- database copy identifier and before-test hash/equivalent evidence;
- Persian period identity and data-start-date relationship;
- expected unit count: Rasht = 3, Ramsar = 4;
- application version/build identifier;
- Windows display scale and screen resolution;
- tester initials and test date/time.

Use the same local copy for one complete lifecycle, then restore the untouched copy before the second station scenario. If the application needs to be restarted for cancellation or DPI checks, record that as a separate run.

## Execution checklist

For every row, enter `PASS` or `FAIL` in the PASS/FAIL field and record concise evidence or deviations in Notes. A blank field means the check has not been run.

| ID | Action | Expected result | PASS/FAIL | Notes |
|---|---|---|---|---|
| P9.4A-01 | Launch the legacy application and remain on the normal main window. | The existing legacy UI opens normally; Pilot is not opened automatically and Legacy remains usable. |  |  |
| P9.4A-02 | Inspect the main-window footer or entry area. | A visible, explicit link labeled `Pilot / فقط خواندنی` is present and keyboard accessible. |  |  |
| P9.4A-03 | Open the Pilot link once. | A Persian confirmation dialog appears before Pilot preflight or Pilot observation starts. |  |  |
| P9.4A-04 | Read the confirmation dialog without selecting Yes. | The dialog clearly states Pilot is آزمایشی and فقط خواندنی, and that Legacy remains the operating authority. |  |  |
| P9.4A-05 | Select No/cancel in the confirmation dialog. | The dialog closes; Pilot does not open; the legacy window remains active and unchanged. |  |  |
| P9.4A-06 | Open the Pilot link again and select Yes. | Read-only Pilot composition/preflight begins explicitly; no workflow starts automatically. |  |  |
| P9.4A-07 | Inspect the Pilot window before pressing Start. | The title/window identifies `Pilot / فقط خواندنی`; the safety banner is Persian, visible, and states Pilot/read-only/Legacy authority. |  |  |
| P9.4A-08 | Confirm the Pilot surface is usable with keyboard focus and RTL reading. | Labels, fields, grid, and buttons have readable order, focus can move through read-only fields/actions, and no clipped critical text is visible. |  |  |
| P9.4A-09 | Inspect the initial identity fields. | Pilot/Rehearsal identity, station, session status, legacy authority, preflight, monitoring, rollback readiness, stop reason, and completion status are visible. |  |  |
| P9.4A-10 | Inspect the initial station/session values. | The displayed station and session identity correspond to the prepared local scenario; the session is waiting/created and not observing yet. |  |  |
| P9.4A-11 | Inspect the initial preflight status. | The UI shows the read-only preflight result or a clear blocked/canceled result; it does not claim a successful session if composition was blocked. |  |  |
| P9.4A-12 | Verify the legacy authority field and banner. | Both indicate that the current Legacy system remains authoritative; there is no authority-switch action. |  |  |
| P9.4A-13 | Review all visible controls before starting. | Start is available only when the composition is ready; Complete and Stop are disabled before an active review state; Return is available. |  |  |
| P9.4A-14 | Press `شروع مشاهده فقط‌خواندنی` once. | The session performs the controlled preflight, approval, start, and one observation attempt; the UI remains responsive and no write prompt appears. |  |  |
| P9.4A-15 | Observe session status after Start finishes. | The lifecycle reaches an operator review state, or displays a safe blocked/failure state with a reason; it does not silently retry or poll. |  |  |
| P9.4A-16 | Inspect the five workflow rows. | Exactly five workflows are displayed: Authentication/ورود, Reporting/گزارش‌گیری, Runtime and Event/کارکرد-رویداد, Protected Settings/تنظیمات حفاظت‌شده, and Export/فراداده خروجی. |  |  |
| P9.4A-17 | Review each workflow status. | Each row has a visible status and comparison result; status is completed/observed or a safe unavailable/blocked state, with no ambiguous blank row. |  |  |
| P9.4A-18 | Review Match/Difference values row by row. | Each workflow exposes `Match`/`مطابق` or `Difference`/`تفاوت مشاهده شد` (or a safe unavailable state); a Difference is presented for human review and does not switch authority. |  |  |
| P9.4A-19 | Review the fingerprint column. | Every workflow row displays its fingerprint specification version. Expected versions are `auth-fingerprint-v1`, `reporting-fingerprint-v1`, `runtime-event-fingerprint-v1`, `protected-settings-fingerprint-v1`, and `export-fingerprint-v1`. |  |  |
| P9.4A-20 | Review monitoring and rollback fields. | Monitoring status is visible (healthy, attention, failed, stopped, or not run as applicable); rollback readiness is evidence-only and does not offer execution. |  |  |
| P9.4A-21 | Attempt a prohibited write action, without actually changing data. | No Pilot control permits INSERT/UPDATE/DELETE, migration, settings mutation, export execution, ESD action, authority switch, or production command. If an action is offered, stop and mark FAIL. |  |  |
| P9.4A-22 | Inspect all visible Pilot text and evidence identifiers. | No password, hash, recovery secret, connection string, raw exception, sensitive personal information, or database secret is displayed. |  |  |
| P9.4A-23 | Choose the Stop path while the session is in operator review. | A stop operation is available; after confirmation/operation the session displays stopped status and a stop reason, and the window remains safe to close. |  |  |
| P9.4A-24 | Repeat the lifecycle through review on a fresh disposable copy, then choose Complete. | Completion is operator-driven; the UI displays completed status and preserves Legacy authority. No authority transition occurs. |  |  |
| P9.4A-25 | From an active, not-yet-completed session, click Return. | The UI asks for confirmation that the session will be stopped before return. Declining leaves the Pilot open; accepting stops safely and closes it. |  |  |
| P9.4A-26 | From a completed or stopped session, close the Pilot window or choose Return. | The Pilot closes and the legacy UI is shown/activated safely; the legacy window was not replaced and remains authoritative. |  |  |
| P9.4A-27 | Start a fresh Pilot session, then cancel the in-progress operation using the application’s cancellation/close route. | Cancellation is handled without an unhandled exception; the session does not falsely reach review/completed state; any safe cancellation/blocked state is visible. |  |  |
| P9.4A-28 | Start a fresh Pilot session, then close the application/window through the normal shutdown control. | Shutdown completes safely; the Pilot lifetime is canceled/disposed; no data-write prompt or authority transition occurs. |  |  |
| P9.4A-29 | After each lifecycle, compare the database copy with the before-test evidence using a read-only method. | No prohibited Pilot write or schema mutation is detected. Any unrelated application write must be explained and the Pilot result marked FAIL pending investigation. |  |  |
| P9.4A-30 | Run the complete lifecycle at 100% display scale. | Window opens, fields and five-row grid are readable, controls are reachable, and no required status is clipped. |  |  |
| P9.4A-31 | Run the complete lifecycle at 125% display scale. | DPI scaling preserves readability and action reachability; no overlapping, clipped, or inaccessible required control is observed. |  |  |
| P9.4A-32 | Run the complete lifecycle at 150% display scale. | DPI scaling preserves readability and action reachability; AutoScroll/responsive behavior does not hide required identity, workflow, status, or lifecycle information. |  |  |
| P9.4A-33 | Run the station scenario using Rasht data/setup. | Station identity is Rasht and the scenario represents 3 units; all five workflow displays, statuses, fingerprints, monitoring, review, stop/complete, and return checks pass. |  |  |
| P9.4A-34 | Run the station scenario using Ramsar data/setup. | Station identity is Ramsar and the scenario represents 4 units; all five workflow displays, statuses, fingerprints, monitoring, review, stop/complete, and return checks pass. |  |  |
| P9.4A-35 | Review the captured evidence and complete the tester notes. | Each required check has PASS/FAIL and notes; failures identify the exact screen, action, station, DPI, and evidence reference. |  |  |

## Stop conditions

Stop the run immediately, preserve the current disposable copy, and mark the relevant check `FAIL` if any of the following occurs:

- Pilot opens automatically, replaces the legacy window, or changes authority.
- The Persian read-only warning or explicit confirmation is missing, misleading, or bypassable.
- Preflight reports readiness without the expected local read-only conditions, or the session starts without explicit operator action.
- Any write, migration, schema change, settings mutation, ESD action, production command, or authority transition is offered or observed.
- A workflow result, Match/Difference status, fingerprint version, monitoring status, station/session identity, or Legacy authority indicator is missing or misleading.
- Raw exceptions, credentials, hashes, connection details, recovery information, or other sensitive information appears.
- An unhandled exception, hang, crash, unsafe shutdown, or inability to return to the legacy UI occurs.
- RTL layout, keyboard navigation, or 100%/125%/150% DPI behavior prevents reliable execution.
- The observed station/unit scenario does not match the prepared Rasht 3-unit or Ramsar 4-unit setup.

Do not continue after a stop condition by editing the database or retrying repeatedly. Record the state and restore from the untouched copy before another independent run.

## Evidence to capture

Capture only sanitized, non-sensitive evidence:

1. Main-window screenshot showing the explicit Pilot entry.
2. Confirmation-dialog screenshot showing Persian read-only and Legacy-authority text; record both Yes and No outcomes in notes.
3. Initial Pilot screenshot showing banner, station/session identity, preflight, authority, and available controls.
4. Post-observation screenshot showing all five workflow rows, status, Match/Difference, and fingerprint version.
5. Monitoring, rollback, stop-reason, and completion screenshots for the stop and completion paths.
6. Return/close/shutdown evidence showing the legacy UI safely available afterward.
7. One screenshot per DPI setting and per station scenario, or a clearly identified screen recording with timestamps.
8. Before/after database integrity evidence from a read-only comparison. Do not include the database itself, secrets, credentials, or raw sensitive rows in the qualification package.
9. A run log containing test ID, station, unit count, period identity, DPI, build, action timestamp, result, and notes.

## Final acceptance criteria

The local installation is accepted for manual qualification completion only when:

- every applicable checklist row is `PASS` for both Rasht/3-unit and Ramsar/4-unit scenarios;
- explicit entry and confirmation are demonstrated, and cancellation leaves Legacy unchanged;
- read-only/Pilot mode, Legacy authority, station/session identity, preflight, monitoring, all five workflows, Match/Difference, and fingerprint versions are visible and understandable;
- Stop, Complete, Return, cancellation, and application shutdown paths complete without an unhandled exception or authority change;
- no prohibited write action or database/schema mutation is observed;
- no sensitive information is displayed;
- RTL/readability and keyboard use are acceptable at 100%, 125%, and 150% DPI;
- evidence is complete, sanitized, traceable to each checklist ID, and any failure is resolved or formally blocks the run.

This checklist does not constitute Phase 9.4B, production cutover, authority transition, or manual qualification results.

## Preparation verification

- `dotnet build Rah_Negar.sln -c Release`: **PASS** — build succeeded; 0 errors; 12 known NU1701 warnings listed above.
- `dotnet test Rah_Negar.sln -c Release`: **PASS** — 637 passed, 0 failed, 0 skipped.
- `git diff --check`: **PASS** — verified after saving this document.
- Application manual qualification performed in this phase: **NO**.
- Phase 9.4B started: **NO**.
