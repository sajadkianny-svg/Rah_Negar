# Phase 9.5A — Cutover Readiness Gate and Final Safety Preconditions

Status: **PHASE 9.5A BLOCKED**

Date: 2026-09-04

Branch: `phase9-operational-readiness`

Baseline commit: `3230fac`

> This document is a safety/readiness decision only. It does not authorize or execute production cutover, authority transition, migration, schema change, production-data access, production startup change, commit, or push. Legacy remains authoritative.

## Objective

Define a deterministic, evidence-based gate for deciding whether Rah_Negar may enter a future, separately authorized controlled production cutover phase. Phase 9.5A evaluates the current foundations, identifies exact missing capabilities and evidence, defines stop conditions and the required pre-cutover evidence package, and produces a conservative GO/NO-GO rule.

A READY result from this gate would still not authorize cutover. The current result is BLOCKED because production activation, target security composition and recovery, safe production restore/rollback, and explicit authority-transition behavior are not yet complete.

## Scope and review boundary

The review was intentionally constrained to:

- the five Phase 9.3/9.4 documents named in the task;
- directly related activation and authority contracts/policies/tests;
- explicit database targeting, read-only preflight, backup validation, restore validation, migration rehearsal, migration-chain, and preservation code/tests;
- directly related Runtime/Event calculation, state-machine, finalization, reporting, snapshot, export, authentication/security, ESD authorization, audit, recovery, and manual database-maintenance code/tests;
- the normal startup/Pilot references needed to confirm that Pilot and activation are not automatic.

This was not a full-repository audit. No production database was opened, identified, hashed, backed up, restored, migrated, or modified. No production code or test code was changed. No new automated tests were added because the review found extensive focused executable protection for the implemented foundations; the decisive gaps are absent production composition/procedures or unsafe existing operational paths, which cannot be honestly closed by a small isolated unit test.

## Authoritative current state

- Phase 9.3 controlled live read-only Pilot: COMPLETE.
- Phase 9.4 manual Pilot qualification: COMPLETE — QUALIFIED WITH LIMITATIONS.
- Rasht 3-unit and Ramsar 4-unit manual primary lifecycles completed successfully through explicit login, Pilot entry/confirmation, Start Observation, five workflow observations, Complete Pilot, and Return to Legacy.
- Reporting and Runtime/Event were `Match` for both manual station scenarios; Authentication, Protected Settings, and Export completed with visible `Difference observed` results rather than execution failures.
- Legacy remains authoritative. No production authority transition has occurred.
- Pilot is explicitly invoked, read-only, and incapable of changing authority.
- The requested automated baseline at commit `3230fac` was 652 passing tests. Phase 9.5A validation results are recorded below.
- Target migration, security persistence, Runtime/Event, snapshot reporting, and activation-control foundations exist and have focused automated coverage, but production startup and ordinary production composition do not activate them.
- `IProductionMigrationExecutor` and `IFutureFeatureActivationExecutor` are future contracts without production implementations. `FeatureActivationBoundarySnapshot.Inactive` keeps every target feature disabled.

## Gate-state definitions

Only these states are used:

- **READY** — current evidence is sufficient for this prerequisite at the Phase 9.5A review boundary. If time-sensitive, it must still be recaptured immediately before cutover.
- **CONDITIONAL** — the foundation or prior evidence is credible, but required current, installation-specific, manual, or approval evidence is missing. A mandatory CONDITIONAL gate prevents GO.
- **BLOCKED** — a required production capability, control, safe procedure, or valid implementation is absent or an unsafe/conflicting path exists. A mandatory BLOCKED gate prevents GO.
- **NOT APPLICABLE** — the prerequisite genuinely does not apply to the selected cutover scope. It may be used only with written scope evidence; it must never be used to waive a failed or missing mandatory control.

Severity is the impact if the requirement fails during cutover; it is not a defect priority for READY items.

## Readiness gate table

| Gate ID | Requirement | Current state | Evidence | Missing evidence/action | Severity | Mandatory before cutover |
|---|---|---|---|---|---|---|
| AUTH-01 | Legacy is the sole current production authority. | READY | Phase 9.3 report; Phase 9.4 final report; `LegacyAuthorityState.LegacyAuthoritative`; inactive feature snapshot. | Recapture the authority-state record immediately before cutover. | CRITICAL | Yes |
| AUTH-02 | No hidden or automatic authority switch, startup activation, or automatic promotion exists. | READY | `ProductionActivationStateTransitionPolicy` permits only explicit adjacent transitions; preparation coordinator reports `AutomaticallyRuns`, `SwitchesAuthority`, and `RegistersRoutes` as false; activation preparation tests verify startup has no preparation dependency. | Repeat binary/source identity check for the cutover build. | CRITICAL | Yes |
| AUTH-03 | Future authority transition is explicit, approved, installation-bound, audited, and executable only at the authorized decision point. | BLOCKED | Approval, evidence, state-transition, guard, and audit contracts exist. `IProductionMigrationExecutor` and `IFutureFeatureActivationExecutor` explicitly have no production implementation. | Implement and separately qualify the authorized production activation boundary, exact authority-state persistence, audit emission, and fail-closed transition procedure. | CRITICAL | Yes |
| AUTH-04 | Rollback authority behavior is defined: trigger, decision owner, target-to-Legacy routing, data boundary, audit, and terminal authority state. | BLOCKED | `RollbackReadinessEvaluator` requires backup, restore validation, owner, and manual decision boundary; activation states include `ActivationRolledBack`. No production rollback/authority adapter or complete operating procedure exists. | Approve and test a station-specific rollback runbook and authority-state implementation, including how writes after cutover are handled before Legacy is restored. | CRITICAL | Yes |
| AUTH-05 | Pilot cannot change authority or execute production operations. | READY | Read-only composition, strict SQLite read-only factory, Pilot contracts, Phase 9.3 tests, and both Phase 9.4 station runs. | Reconfirm against the final cutover binary; Pilot remains observation-only. | CRITICAL | Yes |
| DB-01 | Exact production database identity and canonical path are known and cannot be confused with another file. | CONDITIONAL | Normal application path resolves to executable-local `Data/db.sys`; explicit target inspector canonicalizes a caller-supplied existing SQLite file. No production file was inspected in 9.5A. | At pre-cutover, record canonical full path, station identity, file metadata, cryptographic hash or verified logical fingerprint, and WAL/journal state while the application is quiesced. Match all later receipts to this identity. | CRITICAL | Yes |
| DB-02 | A current, SQLite-consistent, verified backup exists and is bound to DB-01. | CONDITIONAL | `ExplicitSqliteBackupService` uses SQLite Backup API, captures committed WAL content, checks source stability and backup integrity, and emits SHA-256 evidence; focused tests pass. No real production backup was created in 9.5A. | Create a fresh approved backup immediately before cutover and retain receipt, hash, size, schema/migration classification, source identity, location, and custodian. | CRITICAL | Yes |
| DB-03 | Restore capability is proven, authorized, integrity-checked, and does not depend on an unsafe overwrite. | BLOCKED | `RestoreValidationService` validates checksum, SQLite integrity, foreign keys, and migration classification, but it validates a backup rather than executing a restore. Current `DatabaseMaintenanceService.ImportDatabase` decrypts then directly overwrites `Data/db.sys`; it does not call integrity validation. | Provide a ManagementCredential-authorized, crash-safe restore procedure/implementation and rehearse restoration to an isolated location before any live replacement. | CRITICAL | Yes |
| DB-04 | Pre-cutover database integrity and foreign-key integrity pass read-only checks. | CONDITIONAL | `ReadOnlyDatabasePreflightAnalyzer` enforces read-only mode and supports full `integrity_check` plus `foreign_key_check`; tests cover clean and fail-closed classifications. No production preflight was run. | Run full read-only preflight on the exact quiesced DB-01 identity immediately before cutover; attach complete categorical results. | CRITICAL | Yes |
| DB-05 | Post-cutover integrity, foreign-key integrity, migration ledger, station identity, row counts, and critical fingerprints are verified before authority is accepted. | CONDITIONAL | Preflight/fingerprint services and preservation comparison foundations exist. No production cutover occurred, so no post-cutover evidence exists. | Define the exact command/operator sequence, approved tolerances, failure routing, and rollback trigger; execute before accepting target authority. | CRITICAL | Yes |
| DB-06 | No destructive migration or replacement occurs without verified backup and explicit authorization. | READY | `ProductionActivationGuard`, `ActivationEvidencePackageValidator`, approval validator, and approved-context validator fail closed when backup/evidence/approval is missing; there is no production executor today. | Preserve this invariant in any future executor and add end-to-end production-boundary tests when implementation is authorized. | CRITICAL | Yes |
| DB-07 | Finalized historical snapshots, period locks, and legacy finalized evidence remain immutable through migration/reconciliation. | READY | Preservation fingerprinting; migration rehearsal; `UnifiedMigrationEsdReconciliationTests` verifies finalized snapshot bytes and lock state remain unchanged; snapshot stores use immutable/idempotent contracts. | Reprove on the verified production backup and compare pre/post hashes before GO. | CRITICAL | Yes |
| DB-08 | Qualification/test data cannot be mistaken for or copied over production data. | READY | Phase 9.4C generates disposable files outside production `Data`; launcher uses an isolated application copy; production-directory rejection and normal-path preservation are tested. | Exclude qualification credentials/files from cutover media and record station/path identity at every step. | HIGH | Yes |
| DB-09 | Station identity remains Rasht or Ramsar and unit scope does not leak across stations. | CONDITIONAL | Pilot preflight accepts only supported station types; manual Rasht 3-unit and Ramsar 4-unit runs succeeded; migration tests prevent unit-scope additions. No production identity was captured. | Verify station ID/name/type, expected unit count, and per-unit mapping against the exact production backup. | CRITICAL | Yes |
| RT-01 | Every unit has a trusted Runtime Baseline bound to station/unit, boundary minute, state, cumulative totals, RuntimeAfterOH, and version. | CONDITIONAL | Runtime services reject missing, mismatched, negative, or late baselines; Event writes reject events before the baseline; focused tests cover these failures. Production baseline provisioning and per-unit evidence are not composed or captured. | Define/provision the authoritative baseline for every production unit and reconcile it to Legacy before cutover. | CRITICAL | Yes |
| RT-02 | Event chain reconstructs deterministically from active events after the trusted baseline, with strict time order and unique same-unit timestamps. | READY | `EventChainEvaluator`, `EventApplicationService`, repositories, Runtime interval builder, and tests cover ordering, duplicate timestamps, edits/deletes that invalidate later events, and transaction rollback. | Reconcile every production unit chain and retain chain-version/fingerprint evidence. | CRITICAL | Yes |
| RT-03 | START / NSD / ESD / OH transitions follow the approved state machine. | READY | `EventStateTransitionEvaluator` permits Stopped→START, Stopped→OH, Running→NSD/ESD, and StoppedAfterOH→START; all other tested transitions fail with structured correction codes. | Execute production-data invariant verification before cutover. | CRITICAL | Yes |
| RT-04 | ESD adjustment is non-negative, applied exactly once per ESD using the current open-period value, and overflow fails closed. | READY | Runtime calculator and security exact-once ESD boundaries; tests cover one/multiple ESD behavior, invariant decimal handling, replay, concurrency, rollback, and overflow-safe failure. | Bind the reconciled production ESD value and policy version in pre-cutover evidence. | CRITICAL | Yes |
| RT-05 | Open periods recalculate when the current ESD adjustment changes; finalized periods do not. | READY | `CurrentEsdAdjustment_RecalculatesEarlierOpenPeriodEvent` and finalized snapshot simulation tests; report source modes distinguish open projection from finalized snapshot. | Prove on approved station fixtures and production shadow evidence for the final build. | CRITICAL | Yes |
| RT-06 | Finalized-period Event and report state is immutable. | READY | Event application rejects add/edit/delete when locked; atomic report finalization, lock conflict, idempotency, snapshot checksum, and finalized reader tests fail closed. | Verify production finalized-period hashes and locks immediately before and after migration. | CRITICAL | Yes |
| RT-07 | Cumulative runtime retains the trusted baseline plus physical runtime and ESD adjustments; OH resets only RuntimeAfterOH. | READY | `RuntimeCalculator`; focused tests cover running baselines, cumulative total, OH reset, cross-midnight continuity, open runs, longest run, and service-day boundaries. | Reconcile every production unit against Legacy with approved zero/explicit tolerance. | CRITICAL | Yes |
| RT-08 | Runtime/Event source-to-target results match for production data. | CONDITIONAL | Automated operational fixtures and both Phase 9.4 station runs produced Runtime/Event `Match`; these were disposable qualification datasets, not production. | Run read-only production shadow reconciliation for all in-scope units/periods and resolve every invariant difference. | CRITICAL | Yes |
| REP-01 | Target report projection preserves Legacy min/max/average for main data and sum for daily-unique values. | CONDITIONAL | Projection calculator/tests; live reporting read model maps aggregations; Rasht and Ramsar manual Pilot reporting produced `Match`. Evidence is qualification-only. | Reconcile representative and boundary production periods, including data-start boundary, incomplete/open periods, and finalized months. | CRITICAL | Yes |
| REP-02 | Finalized snapshots have complete version evidence, deterministic serialization, checksum validation, and lock/snapshot identity integrity. | READY | snapshot domain, deterministic serializer, atomic persistence, finalized reader, and related tests reject incomplete, corrupt, unsupported, or mismatched evidence. | Revalidate every migrated finalized snapshot and effective lock on the production rehearsal copy. | CRITICAL | Yes |
| REP-03 | Export format and filename are deterministic and safe. | READY | `DeterministicReportFileNamePolicy`; PDF/Excel renderer and exporter tests verify deterministic naming/order and snapshot-only generation. | Approve the exact operational destination and collision/retention procedure; hash sample outputs in rehearsal. | HIGH | Yes |
| REP-04 | Export and finalized reads use immutable snapshots and never recalculate from mutable operational sources. | READY | `SnapshotFinalizedReportReader` and `SnapshotReportExporter` depend only on integrity-checked snapshot/lock contracts; tests run with operational sources unavailable. | Verify production routing adopts this path before authority transition. | CRITICAL | Yes |
| REP-05 | Source/target report reconciliation evidence is complete and within an explicitly approved tolerance. | CONDITIONAL | Pilot fingerprint comparison is deterministic and Phase 9.4 produced Reporting `Match` for both fixtures. No production evidence or approved non-zero tolerance exists. | Default tolerance is exact equality (zero) unless a named data owner approves a documented metric-specific tolerance before evidence capture. Any unexplained difference blocks GO. | CRITICAL | Yes |
| SEC-01 | Normal target authentication uses active ShiftProfile only, with no independent user/role identity. | BLOCKED | Security domain/persistence supports ShiftProfile plus one-to-one internal credential and no roles; tests cover unique personnel number and concurrency. `ProductionReadinessComparison.CreateCurrent` states production UI/composition, provisioning, recovery, and activation remain required. | Implement and qualify production ShiftProfile authentication, credential provisioning/change/disable behavior, session identity propagation, and station scoping. | CRITICAL | Yes |
| SEC-02 | Protected actions require an action/scope/correlation-bound proof from the singleton ManagementCredential. | BLOCKED | Management proof and singleton persistence foundations are tested. Current settings Backup/Import/Repair/Factory Reset use `ConfirmLoginPassword` and legacy login-password verification, not ManagementCredential; production target composition is absent. | Compose and qualify singleton ManagementCredential verification for every `ProtectedAction`; remove legacy-login authorization as a cutover authority path. | CRITICAL | Yes |
| SEC-03 | Management recovery is documented, auditable, bounded, and does not create another authentication identity or universal secret. | BLOCKED | No target ManagementCredential recovery implementation or rehearsal exists. Current legacy `RecoveryService` derives recovery codes from an embedded application secret and station/request data, which is incompatible with the no-hidden-backdoor target requirement. | Design, approve, implement, and test management recovery with explicit audit and recovery authorization; ensure the legacy deterministic recovery mechanism is not reachable after cutover. | CRITICAL | Yes |
| SEC-04 | Post-wizard ESD changes require active ShiftProfile, current ManagementCredential proof, vendor ECDSA P-256 signature, exact device/request/action/value/time binding, and replay protection. | BLOCKED | Vendor verification, canonical payload, public-key lifecycle, atomic exactly-once receipt, and failure tests are extensive. Production provisioning/composition and authority cutover are explicitly absent. | Provision approved public key/device/management evidence and integrate the protected executor only in a separately authorized implementation phase; rehearse success and every fail-closed case. | CRITICAL | Yes |
| SEC-05 | Security and activation audit trails are durable, append-only, non-secret, and cover approvals, protected actions, transition, failure, and rollback. | BLOCKED | Target security audit persistence is append-only with allow-listed metadata; activation audit contract/validator exists. No production activation executor/audit wiring exists. | Implement end-to-end production audit emission and retention; verify failure cannot mutate data without the required durable receipt. | CRITICAL | Yes |
| SEC-06 | No RBAC roles or role-based authorization are reintroduced. | READY | Security contracts model equivalent active ShiftProfiles; schema/repositories have no roles; migration and security tests explicitly assert no RBAC. | Re-run static/schema checks on the cutover build and migrated copy. | CRITICAL | Yes |
| SEC-07 | No Support identity exists; vendor involvement is a signed authorization artifact, not a login. | READY | No Support principal in target schema/contracts; tests assert no Support identity; vendor public-key authorization is request-bound. `ISupportContactInformationProvider` is contact data only. | Verify UI/composition and migrated identities before cutover. | CRITICAL | Yes |
| SEC-08 | No hidden backdoor, master password, private signing key, universal code, or bypass is reachable in target authority. | BLOCKED | Target contracts intentionally contain no private key/master password. The current legacy recovery implementation contains an application-embedded recovery secret and deterministic recovery-code path; no target decommission/transition proof exists. | Eliminate reachability of the legacy recovery bypass from target authority and independently security-review the final binary and recovery runbook. | CRITICAL | Yes |
| BR-01 | Operator can create a manual backup without raw file copying. | READY | Existing settings UI invokes SQLite Backup API then encrypted export; the newer explicit service also uses SQLite Backup API and captures WAL commits. | Standardize the future cutover runbook on the verified explicit backup receipt, not success-message-only legacy export. | HIGH | Yes |
| BR-02 | Backup integrity is verified cryptographically and structurally before it is accepted. | BLOCKED | New explicit backup service calculates SHA-256 and runs full SQLite/foreign-key checks with tests. It is not production-wired. Existing encrypted export records last-backup time but emits no integrity receipt and has no authentication tag. | Integrate an approved verification workflow or external controlled procedure that binds ciphertext/artifact identity to a verified SQLite copy; retain hash and structural results. | CRITICAL | Yes |
| BR-03 | Restore requires explicit ManagementCredential authorization bound to the exact backup and destination. | BLOCKED | Target security classifies Restore as protected. Existing settings Import is gated by the legacy login password and confirmation only. | Implement action-bound management proof, target/backup identities, correlation, audit, and expiry for restore. | CRITICAL | Yes |
| BR-04 | A restore rehearsal proves the selected backup can be restored and opened, with integrity and application checks. | CONDITIONAL | Restore validation and isolated migration rehearsal are implemented/tested; Phase 9.4 qualification used regenerated fixtures, not a restore of the actual production backup. | Rehearse the exact selected production backup in isolation, start the exact cutover binary against the restored copy, and capture integrity, station, authentication, Runtime/Event, report, and finalized snapshot evidence. | CRITICAL | Yes |
| BR-05 | A rollback copy exists before any live replacement and its identity/location are recorded. | BLOCKED | Existing `DatabaseMaintenanceService.ImportDatabase` declares `safetyBackupPath` but never creates or uses it before overwriting the live database. | Implement or operationally enforce a verified immutable rollback copy outside the live path; test it and bind it to rollback owner/evidence. | CRITICAL | Yes |
| BR-06 | Restore/replacement failure is crash-safe and recoverable without leaving an ambiguous live database. | BLOCKED | Existing Import performs direct `File.Copy(..., overwrite: true)` after decryption/identity comparison; no staged atomic swap, post-copy integrity check, or automatic recovery exists. | Define and qualify a staged replace/rename strategy, sidecar handling, post-restore validation, failure cleanup, and manual rollback. | CRITICAL | Yes |
| MIG-01 | Explicit target inspection, read-only classification, checksummed migration ledger, contiguous chain, isolated rehearsal, idempotency, and preservation controls exist. | READY | production migration readiness contracts/policies; read-only analyzer; migration runner; unified target chain v0→v4; rehearsal and migration tests. | Re-run against the exact production backup and final binaries. | CRITICAL | Yes |
| MIG-02 | Production database migration has an approved executable boundary that validates the exact authorized context and fails safely. | BLOCKED | `ApprovedProductionMigrationContextValidator` is fail-closed, but `IProductionMigrationExecutor` is a future interface only; tests use a test double. | Implement and separately qualify the production executor, transaction/failure semantics, receipts, post-validation, and abort/rollback behavior. | CRITICAL | Yes |
| MIG-03 | Target production composition and feature routing exist but remain disabled until explicit activation. | BLOCKED | `FeatureActivationBoundarySnapshot.Inactive` disables target features; current comparison says production read routing/UI adoption/security composition remain absent. | Implement target composition behind an explicit disabled boundary, then qualify it without changing normal startup until the authorized cutover phase. | CRITICAL | Yes |
| MIG-04 | Production data/provisioning mapping is complete for ShiftProfiles, credentials, management credential, device/key material, Events/baselines, snapshots/locks, and ESD value. | BLOCKED | Unified migration creates non-destructive target schema and can reconcile ESD on rehearsal; it does not provide complete station-specific production provisioning/adoption for all target authorities. | Create an approved, repeatable, reconciled migration/provisioning plan and tests for every in-scope entity without inventing RBAC/Support identities. | CRITICAL | Yes |
| MIG-05 | Exact production migration classification and rehearsal result are current and clean. | CONDITIONAL | Classifier rejects corrupt, unknown, checksum-mismatched, newer, and historical/adoption states; no production DB was inspected in Phase 9.5A. | Classify DB-01, resolve any adoption state explicitly, rehearse the exact backup twice, and retain final version/idempotency/preservation receipts. | CRITICAL | Yes |
| MIG-06 | Authority transition and rollback transitions are implemented and coupled to successful validation, never merely to migration completion. | BLOCKED | State-machine planning exists, but no production authority adapter or executor exists. Migration rehearsal deliberately remains Legacy-authoritative. | Implement explicit two-person/management-approved decision points, durable authority state, post-validation acceptance, rollback state, and audit. | CRITICAL | Yes |
| UI-01 | Primary explicit Pilot observation/Complete/Return path works for Rasht 3-unit and Ramsar 4-unit scenarios. | READY | Phase 9.4 manual evidence: all five workflows completed for both stations; Complete and Return succeeded; Legacy remained authoritative. | Reconfirm against the final cutover build on a restored production rehearsal copy. | HIGH | Yes |
| UI-02 | Stop path after successful active observation is manually qualified. | CONDITIONAL | Stop became enabled; automated lifecycle coverage exists. Phase 9.4 did not manually execute Stop. Classification: **PRE-CUTOVER REQUIRED**. | Run Stop for both station scenarios; verify stopped status/reason, retained safe evidence, no writes/authority change, and safe close/return. | HIGH | Yes |
| UI-03 | Active-session cancellation is manually qualified. | CONDITIONAL | Cancellation is covered by focused automated tests. Phase 9.4 did not manually exercise an in-progress cancellation. Classification: **PRE-CUTOVER REQUIRED**. | Cancel in progress for both stations; verify no false review/completion, no unhandled exception, no mutation, and Legacy remains usable. | HIGH | Yes |
| UI-04 | Application shutdown during active Pilot is manually qualified. | CONDITIONAL | Automated shutdown/disposal tests exist. Phase 9.4 did not manually shut down during an active Pilot. Classification: **PRE-CUTOVER REQUIRED**. | Exercise normal shutdown during active work; verify cancellation/disposal, process exit, database safety, and unchanged authority. | HIGH | Yes |
| UI-05 | Independent 100%, 125%, and 150% DPI visual qualification is complete. | CONDITIONAL | DPI-aware implementation and automated label/source checks exist. Phase 9.4 did not independently qualify all three scales. Classification: **PRE-CUTOVER REQUIRED**. | At each scale and supported station, verify RTL readability, focus order, workflow grid, identity/status fields, controls, dialogs, Stop/Complete/Return, no clipping/overlap, and capture sanitized evidence. | HIGH | Yes |
| UI-06 | Confirmation cancel, keyboard/RTL, monitoring/rollback fields, database before/after evidence, and traceable sanitized run log are complete. | CONDITIONAL | Phase 9.4 record left these compound checklist elements not manually verified. | Close the remaining Phase 9.4A evidence rows or explicitly supersede them with an approved pre-cutover acceptance protocol that is at least as strict. | HIGH | Yes |
| OPS-01 | Named operator, management approver, data owner, rollback owner, maintenance window, monitoring plan, and local support contact are recorded. | CONDITIONAL | Activation preparation/checklist contracts require operational/security gates; no real production approvals were captured by design. | Capture current identities, scopes, UTC timestamps, evidence references, and escalation contacts immediately before cutover. | CRITICAL | Yes |
| VAL-01 | Release build and full automated test suite pass on the exact baseline under review. | READY | Phase 9.5A validation: Release build passed and 652 tests passed; details below. | Re-run on the final cutover commit and binary; any changed artifact invalidates this evidence. | CRITICAL | Yes |
| VAL-02 | Repository diff hygiene passes and the evidence identifies all Phase 9.5A changes. | READY | Phase 9.5A `git diff --check` passed; only this Markdown readiness document was created. | Repeat after any future change. | HIGH | Yes |

## Mandatory versus non-mandatory gates

All 56 gates above are mandatory before production cutover because each is either a direct safety invariant, a prerequisite for controlled operation/recovery, or required evidence for an unambiguous decision. There are no non-mandatory gates in the current cutover scope.

`NOT APPLICABLE` is not currently assigned. A future cutover authority may mark a gate NOT APPLICABLE only if the selected scope makes the requirement genuinely irrelevant and the scope/evidence is written and approved. Authority, backup, restore, integrity, authentication, Runtime/Event, reporting reconciliation, security, rollback, and the four named Phase 9.4 manual limitations cannot be waived as NOT APPLICABLE for Rasht or Ramsar production cutover.

Current mandatory-gate totals:

- Mandatory gates: **56**
- READY: **22**
- CONDITIONAL: **17**
- BLOCKED: **17**
- NOT APPLICABLE: **0**

Because at least one mandatory gate is not READY, the current decision is NO-GO. Because implementation/safety controls are absent or unsafe—not merely time-sensitive evidence—the Phase 9.5A status is BLOCKED rather than CONDITIONAL.

## Authority safety assessment

Legacy remains the only current authority. Pilot is a separate, explicit, read-only observation surface and exposes no authority switch, migration, writer, target executor, settings change, ESD mutation, report finalization, or export execution. Phase 9.4 manually confirmed visible Legacy authority in both supported station scenarios.

The activation foundation is deliberately planning/preparation-only. It models adjacent explicit state transitions, evidence packages, approvals, guards, audits, rollback readiness, and inactive feature boundaries. It does not provide the production mechanism that persists and applies authority. A future cutover must never infer target authority from any of these alone:

- a successful migration;
- a successful Pilot observation;
- an assembled evidence package;
- a `ProductionActivationReadinessResult` of `ApprovedForPreparation`;
- a completed rehearsal;
- target tables being present;
- a UI or process restart.

Target authority may begin only after an explicit approved transition, successful mandatory pre/post validation, durable audit, and a persisted unambiguous authority state. Rollback must explicitly restore Legacy routing and address data written during any target-authoritative interval; merely restoring an old file is not an adequate authority policy.

## Data and database safety assessment

Existing readiness foundations are strong for explicit read-only inspection, SQLite-consistent backup, full integrity/foreign-key checking, migration-history classification, structural/preservation fingerprints, isolated rehearsal, and non-destructive target schema creation. They do not establish that an actual production database is safe: Phase 9.5A intentionally captured no production identity or evidence.

Two current operational paths must not be used as cutover-safe restore evidence:

1. The legacy Import path validates only station type/name before overwriting the live file. It does not invoke the newer checksum/integrity/foreign-key/migration-state validation.
2. It computes a `safetyBackupPath` but never creates that copy, then performs direct overwrite. There is no staged atomic replacement, post-copy verification, or tested recovery from an interrupted copy.

The encrypted legacy export is a useful manual backup feature but its success dialog/date is not a verified cutover receipt. AES-CBC output with an application-embedded key has no authenticated integrity tag in this implementation. Cutover evidence must therefore use a separately verified SQLite-consistent copy and cryptographic artifact identity, with an approved storage/custody procedure.

For a database potentially using WAL, a hash of `db.sys` alone while the application is active is not enough. The evidence capture must quiesce writes and either checkpoint under an approved procedure or rely on SQLite Backup API output; record journal/WAL state and bind all later checks to the verified backup and logical structural fingerprints.

## Runtime and Event safety assessment

The target domain foundations executable-protect the key rules:

- trusted baseline is required and must match station/unit, initial state, responsibility boundary, and non-negative cumulative values;
- active Events are sorted chronologically and by ID, with duplicate same-unit timestamps rejected;
- the full chain is reconstructed after every add/edit/delete and an invalid later chain rolls the transaction back;
- valid transitions are exactly Stopped→START, Stopped→OH, Running→NSD/ESD, and StoppedAfterOH→START;
- intervals are half-open, open runs end at the calculation boundary without synthetic Events, and midnight/service-day boundaries are tested;
- current ESD adjustment applies once per ESD and recalculates open projections;
- finalized snapshots retain the historical ESD value and are not recalculated;
- cumulative total retains baseline total plus physical runtime and ESD adjustment; OH resets only RuntimeAfterOH;
- finalized Event periods reject add/edit/delete.

The remaining cutover risk is operational adoption, not a missing small unit invariant: production trusted baselines and complete source Event chains have not been provisioned/reconciled, and the target Runtime/Event route is inactive. Production shadow evidence must cover every supported unit, not only the deterministic qualification fixtures.

## Reporting safety assessment

Reporting projection, version/completeness validation, atomic finalization, immutable snapshots, lock/snapshot identity checks, deterministic serialization/checksums, finalized-only export, deterministic file naming, and renderer ordering all have focused automated coverage. The manual Pilot produced exact Reporting `Match` for both qualification stations.

This is not yet production parity evidence. Before GO, an approved reconciliation must compare Legacy and target projections for representative and boundary production periods and every finalized snapshot/lock in scope. Exact equality is the default tolerance. Any non-zero tolerance must identify metric, rationale, unit, rounding convention, period, approver, and maximum permitted delta; unexplained differences or aggregate-only agreement block GO.

Finalized target reads and exports must use the immutable effective snapshot, not recompute from current Events, current ESD settings, or mutable operational data. File naming evidence must include station, Persian period, period kind, schema version, format, output hash, and collision handling.

## Authentication and security readiness

The only valid target authentication concepts are:

- **ShiftProfile** — the sole normal operational identity; its internal credential is one-to-one material, not a separate user or role.
- **singleton ManagementCredential** — deployment-wide proof used only for protected actions and always bound to initiating ShiftProfile, action, scope, correlation, credential version, and expiry.

There is no valid RBAC role model and no Support identity. Vendor authorization is a signed, request-bound artifact for post-wizard ESD adjustment; it is not an account, operator, role, password, or private key in the customer application.

The target foundations enforce these concepts, but production composition is absent. Existing settings maintenance still authorizes protected operations using the legacy login password. The legacy recovery mechanism derives a recovery code using an embedded application secret and therefore cannot remain a reachable target-authority recovery/bypass path. No target ManagementCredential recovery procedure exists. These facts make security readiness BLOCKED.

Future readiness requires successful tests and manual evidence for ShiftProfile login/logout/disable/station scope; ManagementCredential success, failure, expiry, revision, and recovery; action-bound backup/restore/migration/reopen/settings authorization; vendor signature/key/device/value/time/replay failures; append-only audit; and independent proof that final binaries contain no enabled alternative identity or bypass.

## Backup and restore readiness

Already available:

- manual SQLite Backup API export;
- an explicit, testable backup service that checks source stability, captures WAL commits, calculates SHA-256, and validates backup integrity;
- read-only restore validation for checksum, SQLite integrity, foreign keys, and migration classification;
- isolated restore/migration rehearsal infrastructure;
- rollback-readiness and activation guard contracts.

Still required:

- production integration or an approved controlled operator procedure for the verified backup receipt;
- integrity binding for the retained artifact;
- ManagementCredential authorization for restore;
- an actual restore implementation/procedure rather than validation only;
- a verified rollback copy created before replacement;
- staged crash-safe replacement and sidecar handling;
- post-restore application/integrity checks;
- rehearsal using the exact production backup and final cutover binary;
- failure recovery and authority behavior proven end-to-end.

Until BR-02, BR-03, BR-05, and BR-06 are remedied and BR-04 is READY, inability to restore remains a mandatory stop condition.

## Migration and activation readiness

What exists:

- explicit, canonicalized SQLite target inspection;
- read-only header, full/quick integrity, foreign-key, schema, row-count, ESD, finalized evidence, and migration-ledger inspection;
- fail-closed migration classification for corrupt/unknown/newer/checksum/history states;
- checksummed, contiguous, transactional migration runner;
- non-destructive unified target schema chain through version 4;
- isolated backup revalidation and two-pass idempotent migration rehearsal;
- finalized snapshot/lock/legacy evidence and ESD preservation checks;
- explicit activation state, evidence, approval, guard, audit, rollback, and inactive-feature contracts;
- focused tests for those foundations.

What remains required before any production cutover:

- exact production classification and rehearsal;
- approved mapping/provisioning of every production ShiftProfile, credential, singleton ManagementCredential, device/key, trusted Runtime Baseline, Event chain, report snapshot/lock, and ESD value;
- production migration executor with transactional/failure/receipt behavior;
- target production UI/composition and disabled activation boundary;
- explicit authority-state persistence and transition executor;
- post-migration validation and reconciliation;
- rollback executor/runbook that restores both data and authority safely;
- complete management/operator/data-owner approvals and audit wiring.

Migration success must never imply activation. ESD remains Legacy-authoritative until a separate explicit approved ESD authority transition. Target tables or provisioned values alone confer no authority.

## Operator and UI readiness

Phase 9.4 provides credible manual evidence for the main read-only Pilot path for Rasht and Ramsar. It does not provide complete manual safety qualification. The four specifically required limitations are classified as follows:

| Limitation | Classification | Gate effect | Rationale |
|---|---|---|---|
| Stop path after successful active observation not manually qualified | **PRE-CUTOVER REQUIRED** | UI-02 remains mandatory CONDITIONAL and prevents GO. | Stop is a normal operator safety path. Automated evidence reduces uncertainty but cannot replace the missing manual interaction evidence. A failure during the required run becomes a CUTOVER BLOCKER. |
| Active-session cancellation not manually qualified | **PRE-CUTOVER REQUIRED** | UI-03 remains mandatory CONDITIONAL and prevents GO. | Cancellation must be proven responsive and non-mutating on the final environment. A hang, false completion, mutation, or lost Legacy availability is a CUTOVER BLOCKER. |
| Application shutdown during active Pilot not manually qualified | **PRE-CUTOVER REQUIRED** | UI-04 remains mandatory CONDITIONAL and prevents GO. | Disposal is automated-tested but process/UI/database interaction must be observed. A crash, write, authority change, or unsafe restart is a CUTOVER BLOCKER. |
| Independent 100%, 125%, 150% DPI visual qualification incomplete | **PRE-CUTOVER REQUIRED** | UI-05 remains mandatory CONDITIONAL and prevents GO. | Critical warnings, identity, status, and actions must remain visible/reachable at each scale. Any unusable supported scale is a CUTOVER BLOCKER unless product scope is explicitly changed and approved before evidence capture. |

None is classified NON-BLOCKING FOLLOW-UP. This does not claim a defect: it states that the evidence is incomplete and must become READY before GO. The additional Phase 9.4 limitations—confirmation No/cancel, independent keyboard/RTL, all monitoring/rollback fields, per-run before/after database evidence, and a complete sanitized traceable run log—are retained in UI-06 and the pre-cutover evidence package rather than silently treated as PASS.

## Stop conditions

Future production cutover is forbidden, or must stop before authority acceptance, if any mandatory condition below is true:

1. No verified current backup bound to the exact production database identity.
2. Backup checksum, structural verification, SQLite integrity, or foreign-key integrity fails.
3. Restore validation/rehearsal fails, restore authorization is invalid, or the team cannot restore the selected rollback copy.
4. Production database path, station, file identity, journal/WAL state, or source/backup binding is unknown or ambiguous.
5. Pre-cutover or post-migration integrity fails.
6. Migration history is corrupt, unknown, checksum-mismatched, unsupported/newer, incomplete, or unresolved adoption/review is required.
7. Any migration error remains unresolved, the rerun is not idempotent, preservation checks fail, or the original backup changes.
8. A destructive operation is pending without exact target, scope, explicit authorization, verified backup, and rollback owner.
9. Authority state is not unambiguously Legacy-authoritative before transition or not unambiguously persisted/audited at the decision point.
10. Any automatic/implicit authority switch, feature activation, startup migration, fallback-to-production, or Pilot authority capability is found.
11. Required operator, ManagementCredential, data-owner, security, maintenance-window, or rollback authorization is missing, invalid, expired, mismatched, or unavailable.
12. ShiftProfile authentication, ManagementCredential protection/recovery, vendor-signed ESD authorization, replay protection, or security audit gate fails.
13. RBAC, a Support identity, master password, hidden recovery/bypass, customer-held vendor private key, or unapproved identity is reachable.
14. Any trusted Runtime Baseline is missing, mismatched, unversioned, negative, or after the responsibility boundary.
15. Any Event chain violates ordering, unique timestamp, station/unit identity, START/NSD/ESD/OH state, or finalized-period immutability.
16. Runtime physical, ESD, adjusted, cumulative, RuntimeAfterOH, longest-run, service-day, open-period recalculation, or finalized-period invariant fails.
17. Report reconciliation exceeds the approved tolerance; absent explicit metric-specific approval, tolerance is zero.
18. Finalized snapshot checksum/version/completeness fails, lock and snapshot differ, historical snapshot/lock bytes change, or export reads mutable sources.
19. Station identity/unit count differs from the approved Rasht 3-unit or Ramsar 4-unit cutover scope.
20. Qualification/test database, credentials, application copy, output directory, or evidence is confused with production.
21. Any mandatory gate is CONDITIONAL, BLOCKED, missing, stale, or contradictory.
22. Build/tests/diff hygiene fail, the source commit differs from the binary identity, or the evidence package is not bound to the exact artifacts.
23. Stop, cancellation, active shutdown, DPI, keyboard/RTL, confirmation, or critical status/action visibility qualification fails.
24. Monitoring cannot determine health, rollback triggers are ambiguous, or decision owners cannot be contacted.

A stop condition may not be waived verbally. Resolution requires corrected evidence and a new deterministic gate evaluation; data must not be edited merely to force a pass.

## Required pre-cutover evidence package

Phase 9.5A defines but does not capture this package. The future package must be immutable, sanitized, internally cross-referenced, retained locally/offline, and include:

1. **Decision identity** — package ID, correlation ID, station identity, supported station scenario/unit count, UTC capture timestamp, local timezone, maintenance-window ID, and package schema/version.
2. **Source identity** — Git commit, branch/tag if used, clean/approved source state, Release configuration, .NET/runtime version, and dependency lock/assets identity.
3. **Binary identity** — executable and all deployed managed/native dependency filenames, sizes, versions, and SHA-256 hashes; proof the tested binary set equals the proposed cutover binary set.
4. **Database identity** — canonical absolute path, station type/name/ID, expected units, size, last-write time, SQLite header result, journal mode/WAL state, cryptographic hash captured under a consistent procedure, and logical structural fingerprint.
5. **Backup identity** — source database fingerprint, backup path/media/custodian, creation UTC, size, SHA-256, SQLite Backup API receipt, integrity/foreign-key results, migration classification/version, overwrite policy, and proof source did not change during capture.
6. **Rollback-copy identity** — immutable rollback artifact path/location, SHA-256, custodian, retention protection, restore owner, decision boundary, and verified accessibility during the window.
7. **Schema and migration identity** — current `user_version`/schema version, migration ledger schema/current version, ordered migration IDs/from/to/checksums, expected unified final version, classifier result, adoption decision if any, rehearsal receipt, applied IDs, idempotent rerun, and original-backup unchanged result.
8. **Integrity evidence** — pre-cutover full `integrity_check`, `foreign_key_check`, header/read-only enforcement, schema-object fingerprints, row counts, critical Event/Runtime/finalized-report fingerprints, and the exact planned post-cutover comparison.
9. **Preservation evidence** — hashes/counts for all finalized snapshots, finalized locks, legacy finalized evidence, ESD authority/value, critical Event/Runtime tables, and explicit no-RBAC/no-Support results before rehearsal and after migration.
10. **ShiftProfile readiness** — complete in-scope active profile mapping, station/shift/personnel uniqueness, credential provisioning/revision/enablement status without secret material, successful/failed login evidence, disable/session behavior, and identity propagation to audit/actions.
11. **Management security readiness** — singleton credential provision/revision/status, successful and failed action-bound proof tests, protected-action inventory, expiry/version mismatch behavior, approved management recovery procedure and rehearsal, and proof no legacy password path can authorize target protected actions.
12. **Vendor ESD readiness** — device ID reference, active public-key ID/version/lifecycle, canonical payload/algorithm version, request lifetime, exact value/action/device binding, replay/atomic receipt tests, audit results, Legacy-authoritative pre-state, reconciled ESD value, and separate authority-transition approval. Never include private keys, passwords, verifier bytes, recovery secrets/codes, or signed production authorizations outside their controlled artifact boundary.
13. **Runtime/Event evidence** — per unit trusted baseline identity/version/boundary/state/cumulative totals/RuntimeAfterOH; full Event chain version/fingerprint and invariant result; ESD policy/value/version; open projection results; finalized-period immutability; cumulative/RuntimeAfterOH reconciliation; and all difference disposition.
14. **Reporting evidence** — source/target inputs and versions, per-metric min/max/average/sum reconciliation, approved tolerance record, boundary/open/finalized periods, snapshot/lock/checksum/version/completeness integrity, deterministic export filenames, export hashes/metadata, and proof finalized export uses only snapshot data.
15. **Restore/rollback rehearsal** — exact backup restored to isolated destination, checksum/integrity/foreign-key/application-start results, station/auth/runtime/report/snapshot verification, elapsed time, operator steps, injected/observed failure recovery, rollback copy restore, and explicit authority outcome.
16. **Operator/UI evidence** — final Phase 9.4A-equivalent record for Rasht and Ramsar, including Stop, active cancellation, active shutdown, confirmation No, keyboard/RTL, critical status/monitoring/rollback fields, 100/125/150% DPI, sanitized screenshots/logs, application responsiveness, and database before/after non-mutation evidence.
17. **Operational approvals** — initiating ShiftProfile/operator, singleton ManagementCredential authorization reference, management approver, data owner, security reviewer, rollback owner, maintenance-window owner, monitoring owner, local support contact, approval scopes, UTC timestamps, expiry, and exact DB/evidence/binary correlation bindings.
18. **Runbook and stop record** — numbered commands/actions, expected safe categorical output, no-secret logging rule, two-person checkpoints where approved, stop conditions, monitoring thresholds, rollback triggers, maximum decision times, contact/escalation plan, and signed rehearsal result.
19. **Validation results** — exact Release build errors/warnings, exact tests passed/failed/skipped/total, `git diff --check`, manual test results, unresolved warnings, production-code change inventory, and hashes of all evidence manifests.
20. **Final decision record** — every mandatory Gate ID with READY evidence reference, explicit statement that none is CONDITIONAL/BLOCKED, GO/NO-GO outcome, decision-makers, UTC timestamp, and the warning that the evidence package itself grants no permission without the separately authorized cutover decision.

Secrets and raw personal data must not be copied into ordinary Markdown/screenshots/logs. Evidence should use safe identifiers and hashes, with sensitive source artifacts retained only in their authorized protected location.

## Deterministic GO / NO-GO rules

Evaluate in this order:

1. Confirm the selected scope is exactly one supported station installation (Rasht or Ramsar) and bind all evidence to one database, binary set, correlation ID, and maintenance window.
2. Confirm all required Gate IDs are present exactly once and every mandatory flag remains Yes.
3. Reject the package if any evidence is missing, stale, unsafe, internally inconsistent, belongs to qualification/test data, or is bound to a different commit, binary, database, backup, station, approval, or correlation ID.
4. Evaluate every stop condition. If any is true or cannot be determined false, decision = **NO-GO**.
5. Evaluate all mandatory gates. If any gate is BLOCKED, decision = **NO-GO**. If any gate is CONDITIONAL, decision = **NO-GO**. If a mandatory gate is NOT APPLICABLE without approved scope proof, decision = **NO-GO**.
6. A future **GO** is possible only when every mandatory gate is **READY**, every stop condition is false, every approval is current and exact-match bound, rollback is executable, and the final decision record is explicitly signed/recorded at the authorized decision point.
7. GO authorizes only the separately specified single cutover operation for the bound station/database/window. It does not authorize broader migration, another station, schema changes outside the approved chain, RBAC, Support identity, or future automatic activation.
8. Any artifact/evidence/approval change after GO invalidates GO and requires a fresh evaluation.

Current deterministic result: **NO-GO** — 17 mandatory gates are CONDITIONAL and 17 are BLOCKED.

## Existing automated test coverage review

Focused executable coverage already exists for the claims made READY in this document:

- Activation: explicit adjacent transitions, complete/incomplete evidence, exact approval bindings/expiry, guard failures, rollback requirements, all checklist items, inactive feature boundaries, no production dependency, and preparation-only behavior.
- Database/migration: explicit path/header, non-SQLite rejection, read-only preflight, full integrity/foreign-key checks, clean and hostile ledger classification, checksum/history tampering, SQLite-safe/WAL-aware backup, same-path/overwrite rejection, restore checksum/corruption rejection, isolated rehearsal, preservation, ESD conflict, bounded lock retry/cancellation, capacity states, transactional rollback, idempotency, and no RBAC/Support identity.
- Event/Runtime: complete START/NSD/ESD/OH transition matrix, duplicate timestamp, chain-invalidating edit/delete rollback, audit atomicity, baseline boundary, finalized lock, half-open intervals, open runs, cross-midnight, service-day boundary, ESD recalculation, cumulative runtime, RuntimeAfterOH/OH reset, overflow, version metadata, and shadow comparison.
- Reporting: deterministic projection/order, completeness/version gates, min/max/average/sum inputs, atomic finalization/conflicts/idempotency, deterministic snapshot serialization/checksum, immutable finalized reader, lock/snapshot mismatch, unsupported/corrupt formats, deterministic PDF/Excel naming/order, and snapshot-only export.
- Security: ShiftProfile model/no roles, unique personnel and credential concurrency, singleton ManagementCredential, public-only vendor keys, canonical ECDSA payload/signature validation, exact binding/expiry/replay, audit allow-list/append-only persistence, atomic exactly-once ESD adjustment, failure rollback, and finalized snapshot non-mutation.
- Pilot/operator support: strict read-only SQLite, non-mutating preflight/observers, Rasht/Ramsar scopes, deterministic five-workflow fingerprints, explicit start, complete/stop/disposal/cancellation/shutdown lifecycle, no prohibited UI actions, Legacy authority, source preservation, and qualification regression paths.

No focused tests were added in Phase 9.5A. Tests cannot supply the missing production executor, security composition, recovery design, safe restore replacement, real production identity/rehearsal, manual UI observations, or human approvals. When those capabilities are implemented in a separately authorized phase, focused end-to-end tests must be added at the new boundaries rather than duplicating existing domain tests.

## Residual risks

- The production database may have an unknown migration/adoption state, integrity issue, WAL condition, station mismatch, legacy data anomaly, or Event/report difference because it was intentionally not inspected.
- The target foundations may behave correctly in isolation while production UI/composition/routing remains incomplete or inconsistent.
- Restoring an encrypted legacy backup currently lacks authenticated artifact integrity and safe atomic replacement.
- Recovery and protected-action authorization remain coupled to legacy password behavior; the target ManagementCredential recovery model is absent.
- Production baseline/Event/history mapping may expose edge cases not represented by the deterministic qualification fixtures.
- Package compatibility warnings (NU1701 for OpenTK/OpenTK.GLControl/SkiaSharp Windows Forms compatibility assets) remain known technical debt; they did not fail the build but must remain visible in final release risk acceptance.
- Manual Pilot safety paths and independent DPI/keyboard/RTL evidence remain incomplete.
- Any future authority implementation creates new high-risk code that must be separately reviewed and tested; this document cannot pre-approve it.

## Exact blockers

The following 17 mandatory BLOCKED gates must be remediated before pre-cutover verification can produce GO:

1. AUTH-03 — no production explicit authority-transition implementation.
2. AUTH-04 — no complete rollback authority/data behavior implementation and runbook.
3. DB-03 — restore execution is not safely implemented/qualified; current Import bypasses integrity validation.
4. SEC-01 — ShiftProfile production authentication/composition/provisioning/recovery is absent.
5. SEC-02 — protected actions still use legacy login-password confirmation rather than singleton ManagementCredential.
6. SEC-03 — target ManagementCredential recovery is absent; legacy deterministic embedded-secret recovery is incompatible.
7. SEC-04 — vendor-signed ESD authorization is foundation-only and not production composed/provisioned.
8. SEC-05 — production security/activation audit wiring is absent.
9. SEC-08 — no proof that legacy recovery/bypass behavior is unreachable under target authority.
10. BR-02 — production backup artifact integrity receipt is not integrated/operationally established.
11. BR-03 — restore lacks ManagementCredential-bound authorization.
12. BR-05 — rollback copy is not created by the existing Import path despite a declared path variable.
13. BR-06 — live replacement is direct overwrite, not staged/crash-safe/post-validated.
14. MIG-02 — no production migration executor.
15. MIG-03 — no production target composition/routing behind an activation boundary.
16. MIG-04 — complete production data/security/baseline/snapshot provisioning and mapping is absent.
17. MIG-06 — migration is not coupled to an implemented explicit authority/rollback transition.

In addition, every CONDITIONAL gate must receive current exact evidence and become READY. In particular, actual production DB/backup/integrity/migration-rehearsal evidence, Runtime/Event/report reconciliation, the four named manual limitations, remaining Phase 9.4 evidence, and operational approvals all prevent GO today.

## Validation record

Commands executed from repository root after saving this document:

- `dotnet build Rah_Negar.sln -c Release` — **PASS**; 0 errors; 12 existing NU1701 warnings.
- `dotnet test Rah_Negar.sln -c Release` — **PASS**; **652 passed, 0 failed, 0 skipped; total 652**.
- `git diff --check` — **PASS**; no whitespace errors.

Change record:

- Production code changed: **NO**.
- Test code changed: **NO**.
- Database/schema/data/startup/authority changed: **NO**.
- Documentation changed: **YES** — this Phase 9.5A gate document only.
- Commit/push performed: **NO**.

## Recommendation for Phase 9.5B

**Do not begin production cutover, authority transition, or migration.** Phase 9.5B may proceed only as a separately authorized remediation and pre-cutover verification effort, not as cutover execution.

Before Phase 9.5B can seek GO, separately authorized implementation work must close the 17 BLOCKED gates, with priority on: (1) explicit authority/rollback semantics; (2) ShiftProfile/ManagementCredential production composition and safe recovery with no hidden bypass; (3) verified, authorized, crash-safe backup/restore/rollback; (4) production migration and activation executors; and (5) complete production provisioning/mapping and audit wiring. Then Phase 9.5B must capture the defined evidence package on the exact station backup and final binary, close all 17 CONDITIONAL gates including the four manual UI limitations, rerun the full validation suite, and reevaluate every Gate ID.

Phase 9.5B must return NO-GO if any mandatory gate remains CONDITIONAL or BLOCKED. It must not treat this document, Phase 9.4 qualification, a migration rehearsal, or a prepared evidence package as authority to cut over.

## Final Phase 9.5A decision

**PHASE 9.5A BLOCKED**

This is an evidence-based readiness result, not an authorization. Legacy remains authoritative. Stop here; do not begin Phase 9.5B, production migration, activation, or authority transition under this task.
