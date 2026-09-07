# Rah_Negar final security and offline audit

## Positive controls verified

- The application is a local WinForms/SQLite application. Source and dependency inspection found no runtime cloud, GitHub, updater, telemetry, or external AI call path.
- Target security contracts use ShiftProfile for ordinary identity, a singleton ManagementCredential for protected actions, PBKDF2 verification, action/scope/version/expiry binding, audit dependency, and fail-closed results. `Rah_Negar.Tests/Security/Phase95B4SecurityCompositionTests.cs`, `SecurityPersistenceAtomicEsdTests.cs`, `ProductionSecurityReadinessFoundationTests.cs`, and MQ-02/MQ-05 passed.
- ECDSA P-256 vendor authorization validation rejects malformed, expired, future, wrong-device, wrong-request, wrong-action, wrong-value, unknown-key, and inactive-key envelopes. This remains an inactive/qualification boundary; it does not authorize Production.
- No Support login, RBAC surface, or master-password login was found in the target design. Production authority and routing remain disabled.
- Qualification fixtures use synthetic profiles/credentials and the qualification scripts assert no Production DB/authority mutation.

## Batch 1 status update (2026-09-07)

SEC-01 through SEC-05 are resolved in the active code paths. Legacy deterministic recovery was removed and managed recovery now enforces expiry plus durable fail-closed recovery state. Backups use versioned authenticated encryption with operator-bound DPAPI key custody. Restore is staged, integrity/FK/identity validated, rollback-backed, atomic where supported, post-validated, and recovery-marked when outcome is uncertain. Protected maintenance entry points require canonical ManagementCredential proof; the legacy Settings UI has no ordinary-password or unsafe lower-level fallback. The production authority state remains Legacy authoritative, Target non-authoritative, target routing disabled, and Activation/Cutover unauthorized.

SEC-06 is also retired from the active product path with removal of the legacy recovery service; SEC-07 is resolved for the active data-root/logging path by the Batch 2 canonical provider.

## Batch 2 status update (2026-09-07)

Mutable database, authority metadata, transition/recovery metadata, audit files, backups, and logs are centralized under `%ProgramData%\RahNegar\` through `ApplicationDataPaths`. Legacy executable-side database migration is WAL-aware, source-preserving, audited, and fails closed on conflict or corruption. The explicit inactive Target operational write boundary remains disabled; no installer or startup path grants Target authority.

## Historical baseline findings

The following table is retained for audit traceability. Batch 1/2 status updates above supersede the historical dispositions for the active product path; the current unresolved gate is H-05 human/native acceptance, not an open technical security defect.

| ID | Severity | Finding | Evidence | Disposition |
|---|---|---|---|---|
| SEC-01 | CRITICAL | Legacy recovery codes are derived from a secret assembled in the application source. | `Services/RecoveryService.cs:14-23,154`; `GenerateRecoveryCode` uses HMAC-SHA256 over station/request. | Replace with managed, operator-held recovery material; add expiry, rotation, one-time use, audit, and attack tests. |
| SEC-02 | CRITICAL | Backup encryption uses a hard-coded application key and AES-CBC without an authentication tag. | `Services/BackupEncryptionService.cs:18-23,47-56,80-91`. | Use approved key custody and authenticated encryption; version and authenticate the file format. |
| SEC-03 | HIGH | Legacy protected maintenance operations still verify the ordinary login password rather than an independent ManagementCredential proof. | `UI/Forms/FrmSettings.cs`; `docs/phase9.5a-cutover-readiness-gate.md:86`. | Compose the target authorization service through backup/import/repair/reset and fail closed when proof/audit is unavailable. |
| SEC-04 | HIGH | Restore replacement is not atomic and the declared safety backup is never made. | `Services/DatabaseMaintenanceService.cs:108-127`. | Stage, validate, safety-backup, atomically replace, verify, and retain rollback evidence. |
| SEC-05 | HIGH | Production Records exposes test seeding that can inject synthetic data. | `FrmRecords.Designer.cs:293-301`; `FrmRecords.cs:3315-3343`. | Remove from product assembly and add static/package guard. |
| SEC-06 | MEDIUM | Legacy recovery rows have no expiry field/TTL; validation checks unused state but not age. | `RecoveryService.EnsureRecoveryTable`, `ValidateRecoveryCode`. | Make expiry and replay policy explicit in the managed recovery boundary. |
| SEC-07 | MEDIUM | Legacy logger writes under the binary directory, records full exception details, and silently drops logging failures. | `Utils/ErrorLogger.cs:18-37`. | Move logs to the managed data root, redact sensitive paths/values, and expose controlled diagnostics. |

## Dependency health

Batch 3 re-assessed the six NU1701 identities. Source inspection found no `ScottPlot`, `OpenTK`, or Skia API use in the product; the rendering reference was redundant. Removing `ScottPlot.WinForms` and restoring the affected projects eliminated all six warning instances without a replacement rendering path. The Release solution build is now 0 warnings/0 errors. No vulnerable-package entries were reported by the prior local package audit.

Disposition: RESOLVED for the current offline product scope; no rendering feature was removed from the reachable UI because no chart control/button was present.

## Batch 3 security hardening

`ErrorLogger` continues to use the canonical ProgramData Logs directory, now redacts sensitive key/value material, omits raw exception/stack details, rotates the daily file at 2 MiB, and returns a non-throwing health result. Qualification-only credential preparation is excluded from the normal product assembly and the UI harness uses an isolated root.

## Boundary

No Production authority state, activation artifact, or cutover route was changed. Pilot approval does not authorize Production Activation/Cutover.
