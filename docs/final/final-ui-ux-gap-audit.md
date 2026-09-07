# Rah_Negar final UI/UX gap audit

Audit date: 2026-09-07. This is a code-and-evidence audit, not a claim of visual polish.

## Scope and validation limit

User-reachable forms found in source: `FrmStartup`, `FrmLogin`, `FrmMain`, `FrmRecords`, `FrmReportCenter`, `FrmSettings`, `FrmChangePassword`, `FrmRecovery`, `FrmPasswordConfirm`, `FrmAbout`, and hidden `FrmRuntimeSettings`; `FrmLivePilot` is reachable from the read-only Pilot link. Nested `ShamsiDatePickerLibrary.CalendarPopup` is also user-reachable. `BaseForm` only applies the application icon and does not establish a global RTL, typography, focus, or accessibility policy.

The native WinForms surface was not available to the audit tool, so a complete visual inspection at 1920×1080 for 100%, 125%, and 150% could not be completed. Existing automated DPI/layout tests and prior qualification records are evidence of resilience, not visual certification. Above 150% remains blocked by `UiScaleService` clamping (`Utils/UiScaleService.cs:11-19`).

## Inventory and findings

| ID | Severity | Visible surface / issue | Evidence | Required validation/fix |
|---|---|---|---|---|
| UI-01 | CRITICAL | `FrmRecords` exposes a production button labelled `fake database`; its handler copies a template day across a month. A second handler seeds a full year. | `UI/Forms/FrmRecords.Designer.cs:293-301`; `UI/Forms/FrmRecords.cs:3315-3343` | Remove from production composition and add a test proving no seeder control/handler is present. |
| UI-02 | HIGH | Full real visual acceptance was not performed; clipping, contrast, font substitution, keyboard/focus and minimum-window behavior are therefore unclosed. | This audit limitation; prior evidence explicitly calls manual inspection a gate (`docs/phase8-pilot-ui-hardening-validation-report.md:64,133`) | Run a controlled desktop matrix at 100/125/150% for every form and retain screenshots/observations. |
| UI-03 | HIGH | Core forms are not consistently RTL: `FrmSettings` explicitly sets `RightToLeft.No`; main, records, report center, login, startup do not establish form RTL. | `UI/Forms/FrmSettings.Designer.cs:552-563`; Designer inventory | Define and apply a reviewed RTL/Persian layout policy, then verify labels, grids, dialogs, and keyboard order. |
| UI-04 | HIGH | Normal operator UI exposes substantial English/default text: “Data Entry”, “Load”, “Register”, “Analytics Dashboard”, “Finalize Month”, “Settings”, “Reset Factory”, “Database”, and “FrmRuntimeSettings”. | `UI/Forms/*.Designer.cs`; `UI/Forms/FrmReportCenter.cs:624-635` | Replace production-facing copy with approved Persian/operator text and stable resource keys. |
| UI-05 | HIGH | Legacy event UI has non-sequential tab indices and no complete event Enter/Escape/Delete keyboard flow; duplicate validation can fail silently. | `docs/legacy-event-subsystem-audit.md:105,156,262` | Add UI integration/keyboard tests and actionable Persian error presentation. |
| UI-06 | MEDIUM | Fixed pixel layouts, fixed grid widths, and compact Tahoma controls create a clipping/readability risk, especially for Persian and 150%. | `FrmRecords.Designer.cs`, `FrmReportCenter.Designer.cs`; `docs/legacy-report-subsystem-audit.md:157` | Replace brittle fixed regions incrementally, run visual checks at all supported scales. |
| UI-07 | MEDIUM | Grid definitions and rows are rebuilt by clearing and recreating the active DataGridView. | `UI/Forms/FrmRecords.cs:1016-1087`; report-center grid configurators | Cache profile definitions and reuse controls/columns where the workflow permits; measure opening and navigation. |
| UI-08 | MEDIUM | Report-center computed “most frequent combination” is not displayed; the assignment is commented behind a TODO. | `UI/Forms/FrmReportCenter.cs:1909-1912` | Provide an approved visible summary or remove the computation and update requirements. |
| UI-09 | MEDIUM | Charts/data visualization are documented as generated but not presented in the legacy report UI. | `docs/phase0-baseline-report.md:39`; `docs/phase0-traceability-matrix.md:RPT-09` | Decide whether chart display is in current scope; if yes, surface it with empty/loading/error states. |
| UI-10 | MEDIUM | Destructive Factory Reset is present among ordinary settings controls and has no enforced verified-backup gate. | `UI/Forms/FrmSettings.cs:840-867`; `Services/DatabaseMaintenanceService.cs:142-152` | Separate maintenance UX, show data-path consequences, require verified backup receipt, and test cancel/double-click paths. |
| UI-11 | LOW | `FrmRecovery.Designer.cs` contains an unwired `btnVerify_Click_1` that throws `NotImplementedException`; it is dead code but signals unfinished surface maintenance. | `UI/Forms/FrmRecovery.Designer.cs:192-194` | Remove dead handler and add an event-wiring check. |
| UI-12 | LOW | Invalid data-start settings show a developer `NULL` MessageBox before the user-facing error. | `Services/AppSettingsService.cs:197-213` | Remove debug output and retain only localized actionable diagnostics. |

## Per-form summary

| Form | Static result |
|---|---|
| Startup/Login | Functional first-run/login path exists; mixed language, fixed sizing, and no completed visual acceptance remain. |
| Main | Navigation works in source; English titles and inconsistent RTL policy remain. |
| Records | Operationally important but contains the confirmed fake-data/year-seeding exposure; event grid scroll/keyboard/message issues remain. |
| Report Center | Functional report controls exist; legacy English labels, fixed grids, incomplete summary display, and chart presentation gap remain. |
| Settings/Recovery | Maintenance flows exist, but reset/import/recovery safety and security are not product-grade. |
| Runtime/About/Password dialogs | Some Persian/RTL treatment exists; Runtime default title and mixed control policy remain. |
| Pilot surface | Read-only/safety boundaries are strongly represented in target code; full operator visual/RTL acceptance is still pending. |

Conclusion: UI/UX is not polished or acceptance-complete. Do not represent it as such until the real desktop matrix is executed and the listed visible defects are closed.
