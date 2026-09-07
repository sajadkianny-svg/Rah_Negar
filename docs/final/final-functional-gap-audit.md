# Rah_Negar final functional gap audit

## End-to-end trace

`Program.cs` initializes the application. An empty database enters `FrmStartup`; a configured database enters `FrmLogin`; successful login opens `FrmMain`. Main navigation opens records, reports, settings, and the read-only Pilot surface. Records use the legacy repositories/services for daily data, unique values, Events, and locks. Report Center uses legacy report services and exports/finalizes reports. Settings exposes password/user controls, while protected maintenance, recovery, reset, and ESD-setting actions fail closed unless the canonical management proof composition is available. This trace is important because the newer target contracts are not automatically evidence that the reachable legacy path is correct.

## Confirmed functional findings

## Batch 1 status update (2026-09-07)

F-01 and F-02 are resolved: production seeding is absent, and the public runtime path now validates complete canonical Event chains before calculation. F-03 and F-04 are resolved by the transactional below-UI Event authority validator, canonical START/NSD/ESD/OH enforcement, exact-minute rejection, and fail-closed malformed-time handling. F-05 is resolved for safety: active protected maintenance controls no longer use ordinary login password or call unsafe lower-level operations; they fail closed until canonical ManagementCredential proof is available. F-08 is resolved in the maintenance service with proof-gated, sidecar-aware, integrity-checked reset requiring a separate compatible backup. Focused Batch 1 regressions and the full suite pass.

| ID | Severity | Finding | Evidence / failure scenario |
|---|---|---|---|
| F-01 | CRITICAL | Production Records screen exposes test-data seeding. | `FrmRecords` handlers call `TestDataSeederService.CopyTemplateDayToFullMonth` and `CopyTemplateDayToFullYear` with fixed 1405 values. An operator can overwrite/create synthetic history from the normal form. |
| F-02 | CRITICAL | Active public runtime/report path uses legacy calculation rather than the approved complete-chain state machine. | `EventRuntimeCalculationService.Calculate` immediately calls `CalculateLegacyCore`; documented failures include Running+OH, stopped ESD, and omitted pre-range history. This can produce incorrect runtime/report results. |
| F-03 | HIGH | Legacy Event persistence/validation is not a mandatory below-UI authority boundary. | `docs/legacy-event-subsystem-audit.md` records public `InsertEvents`/legacy paths that can bypass normalization, complete-chain validation, and deletion/unit-change checks. |
| F-04 | HIGH | Duplicate timestamp rejection can be silent and malformed event time can be coerced. | `docs/legacy-event-subsystem-audit.md:156,262,456`; UI returns without displaying the validation message and invalid time strings can become `00:00`. |
| F-05 | HIGH | Legacy protected maintenance operations authenticate with ordinary login password, not independent ManagementCredential proof. | `UI/Forms/FrmSettings.cs`; `docs/phase9.5a-cutover-readiness-gate.md:86`. Target proof services are not composed through these active operations. |
| F-06 | MEDIUM | Finalization readiness notification and finalizer do not share one completeness definition. | `docs/legacy-report-subsystem-audit.md:39,218`; notification checks daily unique dates while finalization also requires hourly coverage. |
| F-07 | MEDIUM | “Most frequent combination” is computed but no production control displays it. | `FrmReportCenter.cs:1909-1912` leaves the assignment commented. |
| F-08 | MEDIUM | Factory Reset deletes only the main database file and relies on an informal manual backup instruction. | `DatabaseMaintenanceService.FactoryReset`; WAL/SHM sidecars and verified backup enforcement are absent. |
| F-09 | LOW | Unwired NotImplementedException and stale TODOs remain in production source. | `FrmRecovery.Designer.cs:192-194`; `FrmReportCenter.cs:102,1909`; dead code is not a live path today but is unfinished maintenance debt. |

## Scope boundaries

No evidence supports adding a future universal platform, cloud service, updater, Support login, RBAC, 35-unit operation, or Production activation to the current product. The 35-unit value is an explicit rejected boundary test, not a feature. Production authority/routing remains disabled and unauthorized by owner decision.

## Functional conclusion

The Pilot can launch with the Batch 1 safety defects contained and resolved in the active legacy paths, but installer/data-root work, the qualification runner, real UI acceptance, and full target composition remain open. Production authority/routing remains unchanged and no activation or cutover is authorized.
