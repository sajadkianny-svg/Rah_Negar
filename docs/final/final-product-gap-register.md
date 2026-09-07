# Rah_Negar final product gap register

Counts in this register are open actionable product/readiness gaps; owner-deferred governance items are listed separately and are not counted as software defects.

| ID | Area | Description | Severity | Evidence | Required fix | Test required | Blocks final delivery |
|---|---|---|---|---|---|---|---|
| C-01 | Data safety | Production Records exposes month/year test-data seeders. | CRITICAL | `FrmRecords.Designer.cs:293-301`; `FrmRecords.cs:3315-3343` | Remove from production UI/assembly and isolate test fixture tooling. | Static composition + disposable data-integrity test. | YES |
| C-02 | Runtime correctness | Active public runtime calculator calls legacy calculation that violates approved Event semantics/history rules. | CRITICAL | `EventRuntimeCalculationService.cs:15-35`; legacy event/report audits | Make validated complete-chain calculation authoritative below UI. | Golden runtime/report chain suite. | YES |
| C-03 | Recovery security | Recoverable deterministic secret is embedded in the binary/source; no expiry policy. | CRITICAL | `RecoveryService.cs:14-23,154` | Managed operator-held recovery, expiry, rotation, one-time audit. | Attack/replay/expiry/rotation tests. | YES |
| C-04 | Backup security | Static backup key and unauthenticated AES-CBC allow key extraction and undetected tampering. | CRITICAL | `BackupEncryptionService.cs:18-23,47-91` | Approved key custody plus authenticated versioned encryption. | Tamper/wrong-key/restore tests. | YES |
| H-01 | Restore safety | Import declares but never creates a safety backup and overwrites the live DB non-atomically. | HIGH | `DatabaseMaintenanceService.cs:108-127` | Verified safety copy, staged validation, atomic replace, rollback/post-check. | Failure injection and power-loss simulation. | YES |
| H-02 | Security composition | Legacy protected settings use ordinary login password rather than ManagementCredential. | HIGH | `FrmSettings.cs`; phase 9.5a gate | Compose target authorization for every protected action, fail closed. | UI/action/scope/audit integration. | YES |
| H-03 | Installer/data path | No professional offline installer; DB/logs are rooted under binary directory. | HIGH | no installer project; `SqliteDatabaseHelper.cs:10-16`; `ErrorLogger.cs:18` | Build installer and migrate mutable data to safe persistent root. | Fresh/upgrade/reinstall/uninstall/standard-user tests. | YES |
| H-04 | Qualification | Phase 9.7 runner cannot start QualificationTool due nested top-level Program compilation. | HIGH | `QualificationTool/Program.cs`, `Phase98Probe/Program.cs`; failed runner log | Exclude nested probe or separate project; rerun qualification cleanly. | Clean-checkout qualification run. | YES |
| H-05 | UI acceptance | Full real visual/keyboard/RTL/DPI acceptance is not complete. | HIGH | phase 8/9 docs retain manual gate; native surface unavailable here | Execute independent 1920×1080 100/125/150% matrix and fix findings. | Retained screenshots/observations/checklist. | YES |
| H-06 | Event integrity | Legacy public Event write/validation path can bypass complete-chain rules and has silent duplicate/error paths. | HIGH | `legacy-event-subsystem-audit.md:14,156,262` | Route all mutations through one transactional validator. | Insert/edit/delete/duplicate/invalid-time tests. | YES |
| H-07 | Legacy integration | Target security/report/snapshot foundations are not the complete normal legacy operational composition. | HIGH | `FrmMain.cs:512-537`; target Application layers; phase 9.5a gate | Migrate/compose the approved target boundaries or explicitly retire legacy paths. | End-to-end Rasht/Ramsar scenarios. | YES |
| M-01 | Performance | Dynamic grids rebuild columns/rows instead of caching/reusing definitions. | MEDIUM | `FrmRecords.cs:1016-1087`; Report Center configurators | Add cache/reuse and baseline large-history performance. | Grid/navigation/memory benchmark. | YES |
| M-02 | UI quality | Mixed English/default UI, inconsistent RTL, compact/fixed layouts, and unfinished default labels remain. | MEDIUM | Designer inventory; `FrmSettings.Designer.cs:552-563` | Localize, standardize RTL/typography/layout, remove default titles. | Visual/keyboard/localization matrix. | NO |
| M-03 | Reporting UX | Most-frequent summary is computed but not displayed; legacy chart presentation is incomplete. | MEDIUM | `FrmReportCenter.cs:1909-1912`; phase 0 baseline | Finish approved display or remove from scope. | Report empty/loading/error/display tests. | NO |
| M-04 | Reset lifecycle | Factory Reset does not handle WAL/SHM sidecars and does not enforce a verified pre-reset backup. | MEDIUM | `DatabaseMaintenanceService.cs:142-152`; settings UI | Sidecar-aware, audited, backup-gated reset. | Cancel/double-click/sidecar/restart tests. | YES |
| M-05 | Configuration | Invalid data-start setting shows developer `NULL` debug output. | MEDIUM | `AppSettingsService.cs:197-213` | Remove debug dialog and harden malformed-settings path. | Malformed/read-only settings tests. | NO |
| M-06 | Logging | Full raw exception details are written to a binary-directory log and logger failure is swallowed. | MEDIUM | `Utils/ErrorLogger.cs:18-37` | Managed location, redaction, bounded diagnostics, health signal. | Logger failure/path/redaction tests. | NO |
| M-07 | Evidence | No controlled startup/form/report/memory benchmark proves performance or disposal stability. | MEDIUM | roadmap requirements; no benchmark artifact found | Run and retain representative measurements. | Automated/manual performance suite. | NO |
| M-08 | Installer metadata | Installer publisher/version/shortcut/uninstall metadata and optional desktop shortcut policy are absent. | MEDIUM | no installer infrastructure found | Define and implement installer metadata/policy. | Installer acceptance suite. | YES |
| L-01 | Code quality | Unwired `NotImplementedException` handler remains in Recovery Designer. | LOW | `FrmRecovery.Designer.cs:192-194` | Remove dead handler. | Static scan/event wiring. | NO |
| L-02 | Code quality | Stale TODO/debug comments and legacy mixed-language remnants remain. | LOW | `FrmReportCenter.cs:102,1909`; Designer inventory | Clean or explicitly track each remaining item. | Static unfinished-code gate. | NO |
| L-03 | Dependencies | Six NU1701 warning instances remain for transitive legacy rendering packages. | LOW | Release build and `dotnet list package` | Re-evaluate ScottPlot/OpenTK/Skia chain in a separate regression-tested batch. | Visual/render/package compatibility suite. | NO |

## Owner-deferred items (not counted above)

| ID | Decision | Status | Blocks current Pilot handoff |
|---|---|---|---|
| D-01 | Production Activation/Cutover | Explicitly unauthorized by Sajad Kiyani | NO; it must remain blocked |
| D-02 | Independent human review / real Production installation and custody evidence | Not performed or unavailable in current records | NO for Pilot; YES for Production authorization |
| D-03 | MQ-07 manual observation | Blocked with automated invariant evidence retained | NO under the recorded narrow Pilot decision |
| D-04 | ESD effects, recovery custody, and some cutover semantics | Pending explicit owner/domain decisions | NO for current Pilot; YES for the affected final-production capability |

Open actionable counts: CRITICAL 4, HIGH 7, MEDIUM 8, LOW 3. There are 10 unresolved CRITICAL/HIGH rows; the product is not final-delivery-ready.
