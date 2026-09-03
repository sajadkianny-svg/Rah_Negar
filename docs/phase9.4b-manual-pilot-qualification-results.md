# Phase 9.4B — Actual Manual Pilot Qualification Results (RERUN)

## Final status

**READY FOR MANUAL RERUN**

This current status supersedes the earlier blocked execution record retained below. The
Authentication item is not marked PASS; it requires a new manual run after the focused
correction documented here.

## Phase 9.4B Authentication defect record - 2026-09-03

### Observed manual failure

In the isolated Rasht qualification environment, login, explicit Pilot entry,
confirmation, dashboard creation, Rasht station display, read-only preflight, Legacy
authority indication, and the five workflow/fingerprint rows all succeeded. Selecting
the read-only observation action deterministically stopped the session. Monitoring and
Authentication were failed, the stop reason was a read-only observation error, no usable
Authentication result/evidence was displayed, and the other four workflows remained
pending. The operator reproduced this behavior twice.

### Root cause

The UI was not the cause, and the Rasht/Ramsar qualification Authentication rows satisfy
the live read model's requirements. `LiveSqlitePilotReadModels` emitted the Legacy
capability code `legacy-password-capability`. `OperationalText` deliberately rejects
evidence identifiers containing `password`; therefore
`AuthenticationOperationalObservation.IsValid` was false. The live observer returned no
result before fingerprint generation, and the coordinator converted that invalid result
to failed observer evidence and stopped the single-attempt session. A focused regression
test reproduced the null result for both station fixtures and the Rasht `Stopped`
lifecycle before correction.

### Correction

The single unsafe capability label was changed to the semantically equivalent,
non-sensitive `legacy-login-capability`. No validation, authentication/security rule,
database query, fixture row, schema, transaction, authority, credential handling, or
other Pilot workflow changed. Pilot access remains read-only and Legacy remains
authoritative.

### Regression coverage

`QualificationAuthenticationPilotRegressionTests` now exercises the real generated
Rasht and Ramsar databases through `LiveSqlitePilotReadModels` and
`LiveAuthenticationPilotObserver`. It verifies deterministic successful observations,
safe SHA-256 fingerprints/evidence, absence of password, credential, hash, recovery,
qualification password, salt, or verifier material from exposed evidence, byte-for-byte
source database immutability, and a five-workflow Rasht session reaching operator review
instead of aborting at Authentication.

### Automated validation

- Focused regression tests: **PASS** - 3 passed, 0 failed.
- `dotnet build Rah_Negar.sln -c Release`: **PASS** - 0 errors; 12 existing
  `NU1701` package-compatibility warnings.
- `dotnet test Rah_Negar.sln -c Release`: **PASS** - 647 passed, 0 failed,
  0 skipped.
- `git diff --check`: **PASS** - no whitespace errors; informational LF-to-CRLF
  working-copy notices only.

Authentication remains awaiting manual verification and is not marked PASS.

**Status: READY FOR MANUAL RERUN**

The Phase 9.4B rerun was started using the Phase 9.4C isolated qualification environment. The generator produced both station fixtures and both launchers reached the initialized local application login/main flow. The actual Pilot surface could not be exercised reliably in this execution environment: UI Automation exposed the login controls but not the main WinForms child controls, and foreground keystroke injection was denied. No Pilot PASS was inferred from source code, automated tests, or fixture contents.

The first Phase 9.4B run was blocked before Phase 9.4C because no initialized disposable installation, operator account, or station database copies were available. Phase 9.4C addressed those preparation blockers; this rerun remains blocked only because the current execution session cannot perform the required visible Pilot interactions and DPI display checks.

This record does not authorize production cutover, authority transition, schema change, Phase 9.5, commit, or push.

## Qualification environment used

- Repository: `D:\Projects\RahNegar_SQLite\Rah_Negar`
- Branch: `phase9-operational-readiness`; requested continuation point: `b397505`
- Application: isolated copy of `bin\Release\net8.0-windows\Rah_Negar.exe`
- Qualification copy: `Qualification\qualification-run`
- Qualification data: `Qualification\qualification-data\Rasht\db.sys` and `Qualification\qualification-data\Ramsar\db.sys`
- Execution: local/offline; no external service or production cutover tooling
- Generator: `Qualification\prepare-qualification.ps1`, successful on 2026-09-03
- Launcher: `Qualification\launch-qualification.ps1`, Rasht and Ramsar both reached the local application
- Rasht fixture: `Rasht Station`, 3 units, two prepared Persian daily periods, odd-hour records, daily-unique rows, and events
- Ramsar fixture: `Ramsar Station`, 4 units, equivalent prepared data shape
- Each generated database: 65,536 bytes before launch
- Screenshots/recording: none retained; this session had no reliable visual capture/control surface for native WinForms
- Tester: Codex execution record; 2026-09-03; Europe/Berlin

## Historical note

The first Phase 9.4B run was blocked before Phase 9.4C because the available build was uninitialized and no disposable station databases or operator account were available. This rerun supersedes that result for the prepared-environment attempt, while preserving that history.

## Tooling correction observed during rerun

The first Rasht launcher attempt failed before application startup because `Copy-Item -LiteralPath (Join-Path $release '*')` treated the wildcard literally and did not copy the Release payload. The smallest safe correction was applied in `Qualification/launch-qualification.ps1`: the intended wildcard source now uses `Copy-Item -Path`. After regeneration, both Rasht and Ramsar launch attempts reached the application. No production application code, database schema, or authority behavior was changed.

## Direct execution evidence

- Before preparation, `Data\db.sys` was absent (`PROD_DB_ABSENT`); no production hash was applicable.
- After the Rasht launch and after the Ramsar launch, `Data\db.sys` remained absent.
- The corrected launcher created only `Qualification\qualification-run\Data\db.sys` and copied the Release application into that run directory.
- Rasht reached a top-level `Rah_Negar Login` window; after the qualification password was entered through exposed login controls, the top-level window became `Rah_Negar`.
- Ramsar reached the same local login/main sequence.
- No Pilot window opened automatically during either launch/login sequence.
- The login field and login button were exposed to UI Automation. The main-window child control tree was not exposed; focus/injection attempts returned `Target element cannot receive focus` and `Access is denied`.
- No database write or schema operation was performed by operator actions. The generator was rerun only against its disposable qualification output directory.

## Checklist results

Each row has exactly one result. `NOT EXECUTABLE` means the check was not directly observable or operable; it is not a PASS inferred from source or automated coverage.

| ID | Result | Evidence / limitation |
|---|---|---|
| P9.4A-01 | PASS | Both isolated launches reached the ordinary login/main flow; no Pilot window appeared automatically. |
| P9.4A-02 | NOT EXECUTABLE | Main-window child controls and explicit Pilot link were not exposed to this UI Automation session. |
| P9.4A-03 | NOT EXECUTABLE | Pilot link could not be activated to observe confirmation. |
| P9.4A-04 | NOT EXECUTABLE | Confirmation-dialog text was not displayed to this session. |
| P9.4A-05 | NOT EXECUTABLE | No/cancel outcome could not be exercised. |
| P9.4A-06 | NOT EXECUTABLE | Yes outcome and explicit preflight start could not be exercised. |
| P9.4A-07 | NOT EXECUTABLE | Pilot title/banner could not be displayed and inspected. |
| P9.4A-08 | NOT EXECUTABLE | Pilot keyboard focus and RTL behavior could not be exercised; keystroke injection was denied. |
| P9.4A-09 | NOT EXECUTABLE | Pilot identity, station, session, authority, preflight, monitoring, rollback, stop, and completion fields could not be inspected. |
| P9.4A-10 | NOT EXECUTABLE | Prepared station/session values were not observed on Pilot. |
| P9.4A-11 | NOT EXECUTABLE | Pilot preflight result was not observed. |
| P9.4A-12 | NOT EXECUTABLE | Legacy-authority field/banner and absence of an authority switch were not directly inspected. |
| P9.4A-13 | NOT EXECUTABLE | Pilot control enabled/disabled states were not observable. |
| P9.4A-14 | NOT EXECUTABLE | Start action and observation attempt could not be performed. |
| P9.4A-15 | NOT EXECUTABLE | Post-start lifecycle state was not observed. |
| P9.4A-16 | NOT EXECUTABLE | Five workflow rows were not displayed to this session. |
| P9.4A-17 | NOT EXECUTABLE | Individual workflow statuses were not observed. |
| P9.4A-18 | NOT EXECUTABLE | Match/Difference values were not observed row by row. |
| P9.4A-19 | NOT EXECUTABLE | Fingerprint specification versions were not observed. |
| P9.4A-20 | NOT EXECUTABLE | Monitoring and rollback fields were not observed. |
| P9.4A-21 | NOT EXECUTABLE | Pilot controls could not be reviewed for prohibited write actions; no write action was executed. |
| P9.4A-22 | NOT EXECUTABLE | Pilot text/evidence identifiers could not be visually inspected for sensitive data. |
| P9.4A-23 | NOT EXECUTABLE | Stop path could not be exercised. |
| P9.4A-24 | NOT EXECUTABLE | Fresh lifecycle through operator-driven Complete could not be exercised. |
| P9.4A-25 | NOT EXECUTABLE | Active-session Return confirmation path could not be exercised. |
| P9.4A-26 | NOT EXECUTABLE | Completed/stopped close/return to Legacy could not be exercised. |
| P9.4A-27 | NOT EXECUTABLE | In-progress cancellation path could not be exercised. |
| P9.4A-28 | NOT EXECUTABLE | Pilot/application shutdown path could not be exercised from active Pilot. |
| P9.4A-29 | NOT EXECUTABLE | Production isolation was verified separately, but no completed Pilot lifecycle existed for the required after-each-lifecycle comparison. |
| P9.4A-30 | NOT EXECUTABLE | 100% display-scale Pilot lifecycle could not be performed. |
| P9.4A-31 | NOT EXECUTABLE | 125% display-scale Pilot lifecycle could not be performed. |
| P9.4A-32 | NOT EXECUTABLE | 150% display-scale Pilot lifecycle could not be performed. |
| P9.4A-33 | NOT EXECUTABLE | Rasht fixture and login/main launch were verified, but required 3-unit Pilot lifecycle/five-workflow checks were not executable. |
| P9.4A-34 | NOT EXECUTABLE | Ramsar fixture and login/main launch were verified, but required 4-unit Pilot lifecycle/five-workflow checks were not executable. |
| P9.4A-35 | NOT EXECUTABLE | Complete sanitized evidence set and per-row visual evidence could not be captured. |

### Counts

- PASS: 1
- FAIL: 0
- NOT EXECUTABLE: 34
- Total: 35

## DPI results

| Scale | Result | Exact limitation |
|---|---|---|
| 100% | NOT EXECUTABLE | Session could not switch or independently display requested Windows scaling and could not operate native Pilot. |
| 125% | NOT EXECUTABLE | No separate Windows scaling environment or reliable native UI interaction was available. |
| 150% | NOT EXECUTABLE | No separate Windows scaling environment or reliable native UI interaction was available. |

DPI results were not faked and did not prevent independent launcher, fixture, startup, and isolation checks.

## Production database isolation evidence

| Checkpoint | Evidence |
|---|---|
| Before preparation | `Data\db.sys` absent from normal application path. |
| After generator | Only `Qualification\qualification-data\Rasht\db.sys` and `...\Ramsar\db.sys` were generated. |
| After Rasht launch | Isolated run used `Qualification\qualification-run\Data\db.sys`; normal `Data\db.sys` remained absent. |
| After Ramsar launch | Isolated run was recreated with Ramsar copy; normal `Data\db.sys` remained absent. |
| Schema/production mutation | None observed or performed. |

Because the normal production database was absent at both checkpoints, SHA-256/size comparison is not applicable. Objective evidence is the unchanged absent state and the resolved isolated paths.

## Defects found and corrections

One directly observed qualification-tool defect was corrected: the launcher’s wildcard source used `-LiteralPath`, preventing Release files from being copied. It now uses `-Path`. The affected launcher was rerun for Rasht and Ramsar, and both reached the application. No focused application regression test was added because this was a PowerShell launcher defect and existing qualification-environment tests cover launcher targeting/isolation.

No Pilot UI defect was directly observed. No application behavior was changed.

## Residual limitations

- Explicit Pilot entry/confirmation and every Pilot lifecycle action remain unobserved.
- Pilot/read-only labeling, Legacy authority, station/session identity, preflight, five workflow statuses, Match/Difference, fingerprint versions, monitoring, completion, Stop, Return, cancellation, shutdown, sensitive-data display, and RTL/readability remain unqualified.
- Rasht 3-unit and Ramsar 4-unit fixture generation succeeded, but required Pilot observations were not performed.
- 100%, 125%, and 150% DPI checks remain NOT EXECUTABLE for the exact reasons above.
- No production cutover, authority transition, or Phase 9.5 work was started.

## Build and test validation

The launcher script correction is the only code/tooling modification in this rerun. Final command results are recorded below after execution:

- `dotnet build Rah_Negar.sln -c Release`: **PASS** — 0 errors, 12 NU1701 warnings.
- `dotnet test Rah_Negar.sln -c Release`: **PASS** — 644 passed, 0 failed, 0 skipped.
- `git diff --check`: **PASS** — only line-ending normalization warnings were reported by Git status plumbing.
- `git status --short`: `M Qualification/launch-qualification.ps1`; `M docs/phase9.4b-manual-pilot-qualification-results.md`.

## Operator qualification conclusion

Operator qualification is **not granted**. Authentication remains unpassed until the
corrected isolated Pilot is manually rerun. The focused automated validation establishes
only that the deterministic observer failure is corrected without changing authority or
write boundaries.

## Readiness decision for Phase 9.4 finalization

**READY FOR MANUAL RERUN** — rerun the isolated manual Authentication observation; do not
mark it PASS until that rerun succeeds. Do not begin Phase 9.5 or any authority transition.
