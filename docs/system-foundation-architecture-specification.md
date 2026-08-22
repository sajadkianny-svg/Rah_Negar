# System Foundation Architecture Specification

**Repository:** `D:\Projects\RahNegar_SQLite\Rah_Negar`  
**Scope:** Fully offline production foundation for Rasht and Ramsar  
**Status:** Documentation only; no code or migration is authorized

## 1. Architecture decision

RahNegar has exactly two authentication concepts: `ShiftProfile` is the sole normal operational login, and one independent `ManagementCredential` authorizes privileged local actions. There is no role-based access control, role/permission catalogue, custom access bundle, or direct user grant. Management authorization is not a user role and cannot make invalid domain data valid.

The foundation supplies authentication, action-bound management authorization, settings, audit, backup/restore, import/export, migration, database health, instance coordination, logging, clock/identity generation, and transactions. It remains fully offline, has no vendor master password, backdoor, cloud identity, Internet recovery, or external AI. Rasht/Ramsar logic stays isolated.

## 2. Shift Profile

The initial Wizard creates exactly one profile per configured operating shift. It represents the current authorized supervisor for that shift.

| Field | Requirement |
|---|---|
| `ShiftProfileId` | Stable opaque identity referenced by business records/audits. |
| `StationId` | Owning Station; enforce Station isolation. |
| `ShiftNumber`, `ShiftName` | Required shift identity and display label. |
| `SupervisorFirstName`, `SupervisorLastName` | Required current supervisor name. |
| `PersonnelNo` | Login username; normalized and unique among active profiles. |
| `PasswordHash`, `PasswordSalt` | Approved KDF output and unique random salt; never plaintext. |
| `PasswordAlgorithmVersion` | Algorithm/parameter version. |
| `IsActive` | Login eligibility; deactivation preserves history. |
| `CreatedAt`, `UpdatedAt`, `RowVersion` | UTC lifecycle timestamps and optimistic concurrency. |

Supporting credential state includes `FailedAttemptCount`, `IsLocked`/`LockoutUntil`, `PasswordChangedAt`, `CredentialVersion`, `LastLoginAt`, and `MustChangePassword` when temporary resets are supported.

`PersonnelNo` comparison uses trim, Unicode normalization, and an approved case policy; Persian/Arabic normalization must not create ambiguous collisions. Database constraints enforce active uniqueness. Changing PersonnelNo changes login only: `ShiftProfileId` and historical links remain unchanged. Names, PersonnelNo, password, and policy-permitted ShiftName are editable through management-authorized maintenance and audited. A referenced profile is never physically deleted. Replacement normally updates the stable shift profile with before/after snapshots; if policy requires a successor, deactivate/retain the old profile and create a new one.

Every authenticated Shift Profile has identical normal access:

- daily/hourly entry, including 12 records at 01, 03, 05, 07, 09, 11, 13, 15, 17, 19, 21, 23;
- edit eligible unlocked data from `data_start_date` onward;
- Event Add/Edit/Delete under domain rules and view Event history;
- generate live reports, view finalized reports, and approved normal report export;
- view normal Station information.

Normal access never bypasses sequential/daily-unique/completeness rules, the Event state machine, finalized-month locks, Station isolation, concurrency, or database constraints. These are enforced below the UI, not through granular access grants.

## 3. Management Credential and authorization

Each deployment has one `ManagementCredential`, unrelated to ShiftProfile and unusable for normal login. No username is needed because it is a singleton deployment credential.

| Field | Requirement |
|---|---|
| `ManagementCredentialId` | Stable singleton/recovery binding. |
| `PasswordHash`, `PasswordSalt`, `PasswordAlgorithmVersion` | Versioned salted KDF credential. |
| `CreatedAt`, `PasswordChangedAt` | UTC lifecycle timestamps. |
| `FailedAttemptCount`, `IsLocked` | Atomic failure/lock state; optional `LockoutUntil`. |
| `CredentialVersion` | Incremented on change/recovery; invalidates proofs. |

Management verification protects Shift Profile edit/add/deactivate/replace/reset; protected general, Station, backup-policy, and security settings; Restore; Migration; finalized-report Reopen; security configuration; integrity repair; sensitive raw import/export; and emergency maintenance. Normal use never requires it.

Manual Backup is available to a Shift Profile without Management verification: it is protective, non-destructive, and time-critical offline. Destinations remain restricted; packages are verified/checksummed and audited. Backup path/retention changes, deletion, Restore, and verification override are management-protected.

Sensitive workflow: the Shift user initiates; application identifies and describes the protected action; prompts for Management password; verifies locally; and issues an opaque proof bound to action, scope, initiating ShiftProfileId, CredentialVersion, and short expiry. Proofs are single-action by default. A very short coherent administrative session may use inactivity expiry, but Restore, Migration, recovery, and Reopen require fresh verification. The command then runs all normal application/domain validation, and SystemAudit records request, authorization result, initiator, reason/correlation, and outcome. Proof is rechecked below UI and before mutation. It never bypasses Event/report/finalization/database/migration validation.

## 4. Wizard and profile maintenance

The Wizard collects number of shifts; then ShiftNumber, ShiftName, supervisor first/last name, PersonnelNo, shift password and confirmation for each; then a separate Management password/confirmation. It validates completeness, numbering, Station, password policy, confirmations, and normalized PersonnelNo uniqueness before atomic finalization. It generates high-entropy, deployment-bound, one-time Management recovery material, displays it once, and requires acknowledgment of external organizational custody.

Secrets are masked, short-lived in memory, and absent from configuration exports, logs, screenshots, diagnostics, and audit. Persist only hashes and metadata.

The protected maintenance screen requires Management verification and supports name edits, PersonnelNo change, password change/reset, activate/deactivate, supervisor replacement, and policy-permitted ShiftName management. Removal means retirement when history exists. Every change atomically writes SystemAudit with ShiftProfileId, changed fields, old/new non-secret values, timestamp, initiator, management-authorized indicator, reason where required, and correlation. Never audit password values, hashes, salts, or recovery material.

## 5. Password security and recovery

Use reviewed Argon2id after approved .NET/offline dependency and minimum-hardware validation; PBKDF2-HMAC-SHA-256 with approved high iterations is the documented fallback, never a silent downgrade. Store parameters/version and use at least 128-bit cryptographically random salt per credential. Upgrade parameters after successful verification. Minimum length is 12 for Shift and 14 for Management/recovery-established passwords; permit long Unicode passphrases and reject common or identity-derived values. Password history, if used, stores only versioned hashes/salts.

Default lock threshold is five consecutive failures per credential with protected unlock or approved escalating/timed backoff. Counters are atomic and separate. Responses do not disclose unknown/inactive/locked/wrong-password state. CredentialVersion invalidates sessions/proofs after changes. No password, temporary value, recovery code, hash, or salt is logged/exported.

Shift forgotten-password reset requires Management verification, stable target ShiftProfileId, and reason. Store only a new replacement hash or one-time short-lived temporary hash; increment CredentialVersion, reset lock state by policy, invalidate sessions, force immediate change for temporary credentials, and audit without secrets.

Management recovery uses the Wizard-generated one-time code/package stored outside the workstation; locally retain only a salted verifier, deployment/credential binding, issue status/version, and metadata. Recovery requires physical access, integrity/database-identity checks, material, and incident reason; dual control is recommended. Success permits only immediate new Management password, then atomically increments CredentialVersion, clears lock, consumes the recovery item, and audits. Used, altered, or wrong-deployment material fails.

There is no vendor backdoor, reversible password, email, or Internet recovery. If both Management password and recovery material are lost, normal Shift work may continue but privileged functions remain unavailable. Recovery requires an organization-approved verified backup containing usable credential state or a formally governed offline recovery procedure that preserves the original, validates identity/integrity, and records the incident. Without either, privileged access can be permanently lost.

## 6. Identity, audit, and logging

LoginAudit records ID, nullable ShiftProfileId, safe PersonnelNo fingerprint, login/logout UTC times, result, safe failure reason, workstation/deployment, versions, session/correlation, method, and offset. Sessions bind ShiftProfileId, StationId, CredentialVersion, and useful PersonnelNo/name snapshots. Management attempts link to initiating session/action and record success/failure/lock/recovery/expiry, never secrets.

EventAudit records stable Event, old/new canonical values, ShiftProfileId, useful PersonnelNo/name snapshots, time/reason, in the Event transaction. ReportAudit records lineage, finalization/export/Reopen/supersession, snapshots/versions/checksums/periods, Shift actor, and Management evidence for Reopen. SystemAudit covers authentication, profile/security/settings, backup/restore, import/export, migration, integrity, and emergencies. Sensitive records additionally contain successful Management authorization, initiating ShiftProfileId, time, reason/correlation. Audits survive profile edits/deactivation and are immutable through normal operations.

ApplicationLog is structured/redacted with ID, UTC timestamp/offset, level, nullable ShiftProfileId/session or system identity, module, safe message/exception, correlation, Station, and versions. It never contains credentials/recovery material or unnecessary personal/raw export content. Rotation/retention/disk policy is protected. Logging does not replace audit; mandatory-audit failure blocks sensitive mutation.

## 7. Settings, database security, and lifecycle

Use typed, validated, scoped, versioned settings: ApplicationSettings, non-authoritative preferences, StationConfiguration, system-owned DatabaseMetadata, and protected SecurityPolicy. Protected/security/calculation settings require Management authorization and old/new audit; calculation changes create ConfigurationVersion and never change finalized snapshots. Paths are canonical/restricted; secrets are not ordinary settings.

Store SQLite in an application-managed directory with OS ACLs for application identity, authorized machine administrators, and backup process. Avoid live databases on network/removable/cloud-sync/replaceable paths. Protect WAL/SHM, backup, export, log, and staging. Approved encryption needs separate key recovery.

All connections come from one factory, enable/verify foreign keys, use approved busy/journal/synchronous policy, parameterized values, explicit write transactions, identity/schema checks, and optimistic concurrency. Startup/scheduled health verifies integrity, foreign keys, schema objects, ledger, attachments, identity, and backup freshness. Fail closed/read-only by severity; never auto-repair/delete. Use a deployment mutex plus database correctness controls; Restore/Migration require exclusive maintenance.

SQLite has no server-side authentication. File holders can bypass application controls; weak hashes permit offline guessing; same-file audits are not inherently tamper-proof. Layer physical/device controls, ACLs, application validation, constraints, checksum chains, verified external backups/audit exports, and honest limitation notices. No supported direct-database bypass exists.

Startup order is DPI before controls; single-instance/path/ACL/database open; identity/schema/ledger/Migration check; typed configuration/logging/localization/backup status; incomplete setup Wizard or ShiftProfile login/narrow recovery; application launch. Never auto-migrate or overwrite a failed database. Unsupported schema, corruption, identity/configuration failures block normal launch with Persian actionable diagnostics. Authentication never falls back to anonymous/Management normal access. Shutdown rolls back/completes transactions, logs out, disposes connections, releases lease, and flushes logs; crash startup checks WAL/migration/backup state.

## 8. Backup, Restore, import, and export

Backup records ID, schema/database version, times, initiating ShiftProfileId or system identity, managed path, size, SHA-256-or-stronger checksum, status, and self-contained manifest (type, identities, Station coverage, versions, SourceRevision, integrity, encryption, notes). Types: manual, scheduled, mandatory pre-Migration, and safety pre-Restore. Use SQLite online backup or transactionally safe equivalent; never raw-copy active WAL state. Completion requires checksum, manifest, isolated open/read, integrity/foreign-key checks, and durable final rename; quarantine partials.

Restore requires fresh Management authorization/reason, exclusivity, isolated manifest/checksum/identity/version validation, verified safety backup, closed connections, recoverable atomic replacement, full integrity/schema/control-total/smoke checks, rollback to retained prior database on failure, and SystemAudit. Restore never silently migrates.

Exports distinguish backup, non-secret configuration, Station template, report projection/snapshot, sensitive raw data, and redacted diagnostics. Configuration excludes all credential/recovery secrets. Audit type, scope, versions, initiator, time, destination category, counts, checksum, result. Normal report export is Shift access; raw/config/diagnostic policy may require Management authorization.

Import, when enabled, requires Management authorization and isolated staging; validate content/size/manifest/checksum/schema/encoding/dates/identities/path, parse typed records, detect duplicates/conflicts, enforce Station/finalization/Event/provenance rules, preview, confirm/reason, revalidate, and apply through normal domain commands in a defined transaction with audit and SourceRevision. Prohibit SQL-script/table-copy, silent overwrite, lock bypass, and partial Event chains.

## 9. Migration and reporting/Event integration

Maintain authoritative SchemaVersion and append-only ledger with migration IDs/versions/checksum/application, timestamps, initiator/system identity, Management evidence, backup, status, validation. Only reviewed ordered immutable packages execute.

Migration requires fresh action-bound Management verification, exact-source/preflight checks, integrity and legacy anomaly analysis, mandatory verified backup, displayed plan/confirmation, durable Started marker, smallest transactional step, schema/constraint/count/transformation/foreign-key/Event/snapshot/smoke validation, atomic version completion, and rollback or verified-backup recovery. Preserve failed artifacts; never retry blindly. Finalized reports remain legacy evidence; downgrade only by tested reverse migration or compatible backup restore.

Event handlers consume authenticated Shift context and write EventAudit in the same transaction while preserving state machine, Station logic, locks, and constraints. Reporting uses Shift identity for live generation, eligible finalization, viewing and normal export; SourceRevision, ConfigurationVersion, complete immutable snapshots and checksums preserve reproducibility. Reopen requires Management authorization, reason, impact analysis, and supersession; never edit/delete original finalized evidence.

## 10. Sensitive-action matrix

| Action | Control |
|---|---|
| Manual Backup | Shift login, managed destination, verification/checksum/audit. |
| Restore/Migration | Fresh Management verification, reason, maintenance/exclusivity, backup, full validation. |
| Profile/reset/protected settings | Management verification, stable target, non-secret old/new audit. |
| Management recovery | One-time bound material, integrity check, forced new password, consume/audit. |
| Finalize report | Shift login, authoritative completeness, frozen SourceRevision, atomic snapshot. |
| Reopen report | Fresh Management verification, reason/impact, immutable original and supersession. |
| Import/raw export | Management policy, scope, validation/preview/checksum/audit. |

## 11. Testing strategy

Test Shift login, normalized PersonnelNo uniqueness, wrong password without enumeration, inactive/locked profiles, password change/reset, profile replacement, and historical identity preservation. Test unique salts/KDF/version upgrades, malformed metadata, five-failure concurrency/backoff, session invalidation, and no secret leakage in log/audit/config/report/diagnostic export.

Test Management verification, failed/locked attempts, action/scope/session/version binding and expiry, sensitive-action denial without it, one-time recovery binding/alteration/reuse/forced change, and loss procedure. Prove Management authorization cannot bypass data_start_date, sequential/12-hour/daily-unique completeness, Event state, Station isolation, finalized locks, report rules, DB constraints, Restore integrity, or Migration validation.

Test consistent online/manual/scheduled/pre-operation backups, WAL coordination, corrupt/wrong-identity/path-traversal rejection, Restore exclusivity/safety/atomic recovery/power loss/control totals, and drills covering Events, audits, snapshots, credentials/recovery metadata, and versions. Test import/export formats, encoding, size, duplicates, Station/lock/Event conflicts, preview revision, rollback, checksum, snapshot-only finalized PDF, and secret exclusion.

Test every supported Migration path on Rasht/Ramsar and anomalous fixtures: checksums/order/interruption/disk/SQLite compatibility, rollback/recovery, constraints/counts/Persian dates/Event chains/locks/audit/finalized legacy. Test audit atomicity/immutability/snapshots, logging redaction/rotation/disk-full, settings validation/versioning/concurrency, exact startup order, DPI, single instance, bad ACL/path/schema/config/corruption, maintenance mode, shutdown/crash. Threat-test file replacement, offline guessing, audit tampering, malicious media, traversal/reparse, and direct handler/repository invocation.

## 12. Final decisions

| Component | Decision |
|---|---|
| Normal authentication | ShiftProfile only; PersonnelNo username; equal access; stable historical ID. |
| Privileged mechanism | One independent ManagementCredential; action-bound and short-lived. |
| Recovery | Management resets Shift; one-time external material recovers Management; no master password. |
| Domain protection | Authentication never replaces Event/report/data/database validation. |
| Backup/Restore | Shift may make verified manual backup; Restore is Management-protected. |
| Import/export | Normal report export vs protected sensitive transfer; always validated/audited. |
| Audit | Stable ShiftProfileId plus useful identity snapshots and Management evidence. |
| SQLite | Central safe connections, ACL/physical controls, integrity checks, honest limitations. |
| Startup/Migration | Ordered fail-closed startup; no silent Migration; backup-first validated packages. |
| Scope | Rasht and Ramsar only; keep Station-specific logic isolated. |

Exact KDF parameters, retention, dual-control procedure, encryption product, ACL templates, recovery targets, and sensitive import/export policy require deployment approval. This document defines one coherent model only: ShiftProfile for normal operation and ManagementCredential for privileged authorization.
