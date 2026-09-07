# Rah_Negar final functional gap audit

## End-to-end trace

`Program.cs` initializes the application. An empty database enters `FrmStartup`; a configured database enters `FrmLogin`; successful login opens `FrmMain`. Main navigation opens records, reports, settings, and the read-only Pilot surface. Records use the legacy repositories/services for daily data, unique values, Events, and locks. Report Center uses legacy report services and exports/finalizes reports. Settings exposes password/user controls, while protected maintenance, recovery, reset, and ESD-setting actions fail closed unless the canonical management proof composition is available. This trace is important because the newer target contracts are not automatically evidence that the reachable legacy path is correct.

## Historical baseline findings

The following table is retained for audit traceability. Batch 1/2/3 status updates above supersede the historical dispositions for the current Legacy-authoritative product path; the current unresolved gate is H-05 human/native acceptance, not an open technical M/L defect.

## Batch 1 status update (2026-09-07)

F-01 and F-02 are resolved: production seeding is absent, and the public runtime path now validates complete canonical Event chains before calculation. F-03 and F-04 are resolved by the transactional below-UI Event authority validator, canonical START/NSD/ESD/OH enforcement, exact-minute rejection, and fail-closed malformed-time handling. F-05 is resolved for safety: active protected maintenance controls no longer use ordinary login password or call unsafe lower-level operations; they fail closed until canonical ManagementCredential proof is available. F-08 is resolved in the maintenance service with proof-gated, sidecar-aware, integrity-checked reset requiring a separate compatible backup. Focused Batch 1 regressions and the full suite pass.

## Batch 2 status update (2026-09-07)

The normal runtime now uses the canonical ProgramData data root with fail-closed legacy migration. Target operational composition has an explicit disabled write boundary and remains unreachable from Legacy-authoritative operation. Qualification and installer lifecycle checks pass in disposable environments. No activation, cutover, or Target operational write routing is enabled.

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

The current product acceptance identity is a generic/profile-driven station with a supported unit boundary of 3–5 inclusive. Counts 2, 6, and 35 remain explicit rejected boundary tests, not features. Legacy station fixtures are compatibility-only; no cloud service, updater, Support login, RBAC expansion, or Production activation is introduced. Production authority/routing remains disabled and unauthorized by owner decision.

## Batch 3 evidence update

The remaining reachable operator gaps were addressed: unavailable reset/recovery actions are explicit and disabled, the report-center frequent-combination summary is displayed, reviewed legacy captions are localized, invalid-setting and normal error paths avoid developer details, and qualification-only login/data preparation is excluded from the product assembly. No Production routing or authority decision changed.

## Functional conclusion

The Pilot can launch with Batch 1 safety defects contained, Batch 2 persistence/installer/qualification boundaries implemented, and Batch 3 technical M/L gaps closed. Real native UI observation remains H-05; Production authority/routing remains unchanged and no activation or cutover is authorized.
