# Phase 6.7 Security Production Readiness and Migration Plan

> **Not production-authoritative:** Phase 7.5 supersedes the identity, finalization, and vendor-support assumptions in this historical readiness plan. It must not be used for activation. See `phase7-security-architecture-reconciliation-report.md`.

## Status and scope

Phase 6.7 is a planning deliverable only. It does not register the Phase 6 services, run the draft security migration, create production credentials, change `FrmLogin`, alter any settings form, replace a legacy service, or modify a production database.

The Phase 6 implementation remains opt-in and isolated. Production activation requires a separately approved implementation phase after every gate in this report has objective evidence and an approved rollback window.

## 1. Coexistence and migration strategy

### Coexistence principles

- The legacy login remains the sole production authentication path until an explicit cutover approval.
- The Phase 6 login may initially run only in a non-authoritative validation mode against a dedicated security database copy. Its outcome must not grant access, change `AppSession`, block a legacy user, or alter application navigation.
- Legacy passwords must never be copied as plain text or reversibly transformed. Existing hashes must not be treated as compatible unless their exact algorithm and parameters are independently verified.
- Phase 6 tables, migration history, credentials, sessions, audit records, and configuration must remain distinguishable from legacy data.
- Rasht and Ramsar ShiftProfiles and credentials must be enrolled and validated independently. No station-wide default credential is permitted.
- During coexistence, a Phase 6 failure must not silently fall back after a Phase 6 identity has been declared authoritative. Before that declaration, only the unchanged legacy result controls production access.

### Migration phases

1. **Offline rehearsal** — restore a recent production backup into an isolated environment, run the security migration explicitly, verify schema/checksum/rollback behavior, and discard the environment.
2. **Schema deployment only** — after approval and backup, create the isolated Phase 6 tables without registering login, authorization, UI adapters, or secure-operation adapters. Verify that legacy workflows remain unchanged.
3. **Profile preparation** — create and review Rasht/Ramsar ShiftProfiles. No credential is active and no production decision consumes these profiles.
4. **Credential enrollment** — enroll users through an approved local process that hashes passwords at entry time. Never import or log supplied passwords. Continue using legacy login as authoritative.
5. **Readiness observation** — validate credential coverage, audit durability, authorization decisions, backup/restore, lock handling, and operational support procedures in a controlled environment.
6. **Limited pilot** — in a separately approved release, activate Phase 6 for named pilot identities and one station/workflow at a time. Preserve a time-bounded, audited rollback route.
7. **Controlled expansion** — expand only after pilot acceptance metrics, incident review, and station-specific sign-off. Sensitive workflows remain independently gated.
8. **Legacy retirement** — remove or disable legacy authentication only in a later explicit project after all identities are migrated, rollback retention has expired, and business owners approve retirement.

No phase is implied by completion of the previous phase; each requires a recorded go/no-go decision.

### Rollback strategy

- Before activation, rollback means disabling Phase 6 composition/configuration and continuing the unchanged legacy path. Do not delete Phase 6 audit or credential evidence.
- After a pilot begins, rollback must be configuration-based, scoped to the pilot cohort or workflow, and exercised before cutover. A binary rollback must not require a schema downgrade.
- Do not drop Phase 6 tables during an operational rollback. Preserve them read-only for incident review and reconciliation.
- If schema deployment fails, rely on the migration transaction to roll back the schema and ledger together. Verify both, rather than assuming rollback succeeded.
- If credential or audit integrity is uncertain, fail Phase 6 closed, stop expansion, preserve evidence, and revert authority to the last explicitly approved state.
- Database restoration is a last-resort recovery operation and requires reconciliation of all audit and credential revisions created after the restored backup.

### Backup requirements

- Take an application-consistent, restorable SQLite backup immediately before schema deployment and every activation change.
- Record backup time, source database identity, size, checksum, application version, schema version, operator, and protected storage location.
- Keep at least one verified pre-Phase-6 backup and one post-schema/pre-activation backup according to the approved retention policy.
- Test restoration on a separate machine/path; a copied file without a successful restore test is not sufficient evidence.
- Include SQLite sidecar state correctly when backing up an active database, preferably through the existing approved SQLite backup mechanism.
- Restrict backup access because backups may contain credential hashes, salts, identities, and security audit evidence.
- Never restore a rehearsal or test database over the production database.

## 2. Production activation gates

Every gate must have an owner, evidence link, date, environment, reviewer, result, and explicit approval. A failed or unknown item blocks activation.

### Gate A — security schema validation

- The migration checksum matches the reviewed release artifact.
- Migration succeeds from a verified production-like backup and is idempotent.
- Forced migration failure demonstrates complete transactional rollback of tables and migration ledger.
- Required tables, primary keys, unique constraints, foreign keys, indexes, state checks, revisions, timestamps, and immutability triggers are present.
- No plain-password or support-code column exists.
- Credential and audit update/delete protections behave as designed.
- Backup and restore have been tested, timed, and accepted within the maintenance window.
- Legacy reads, writes, reports, startup, and shutdown pass regression tests after schema-only deployment.

### Gate B — credential readiness

- Every pilot identity maps to exactly one reviewed active ShiftProfile and correct station.
- Latest credential revisions are readable, hashes verify, salts differ, and configured work factor meets the approved baseline.
- No credential has a default, shared, documented, or hardcoded password.
- Disabled and locked credentials are denied; stale revisions cannot overwrite newer revisions.
- Enrollment, rotation, disablement, recovery, leaver, and lost-management-credential procedures have named operators and tested runbooks.
- Credential coverage and exceptions are reconciled without exposing password or hash material.

### Gate C — audit readiness

- Successful/failed login, lock, management authentication, protected setting, support authorization, and secure-operation decisions are recorded.
- Audit records include correlation, UTC timestamp, subject, station/profile, operation, decision, and credential revision where applicable.
- Audit records exclude passwords, support codes, secrets, hashes, and salts.
- Audit storage is append-only, queryable, backed up, and covered by an approved retention/access policy.
- Clock accuracy, timestamp normalization, disk-capacity thresholds, write-failure behavior, and incident export procedures are tested.
- Security operations fail according to the approved policy when mandatory audit persistence is unavailable.

### Gate D — authorization verification

- Normal, protected, management-required, and support paths have positive and negative tests using production-like roles.
- ShiftProfile mismatch, inactive profile, locked/disabled credential, invalid management credential, invalid/expired support code, and operation mismatch all fail closed.
- Rasht credentials cannot authorize Ramsar operations and vice versa.
- ESD default/zero behavior and protected non-default behavior match the approved business rule.
- Credential create/rotate/disable and report-finalization override require the intended authority.
- A denied secure operation demonstrably does not invoke the protected action.
- UI-neutral feedback does not expose internal reasons or secrets.

### Gate E — operational approval

- Pilot scope, maintenance window, support contacts, rollback owner, incident severity model, and go/no-go authority are recorded.
- Operators have completed enrollment, disablement, lock recovery, support-access, audit review, and rollback drills.
- Monitoring is active before authentication becomes authoritative.
- Security, station operations, application ownership, database ownership, and business ownership have signed off.

## 3. User migration strategy

### ShiftProfile creation

- Build a reviewed roster from authoritative local business records; do not infer profiles from usernames alone.
- Create separate Rasht and Ramsar profiles with canonical profile ID, station ID, shift code, display name, kind, enabled state, revision, and UTC creation evidence.
- Resolve duplicate shift codes, inactive shifts, temporary coverage, and support designation before credential enrollment.
- Use support profiles only for personnel with an approved support function. Operational profiles must not receive support capability implicitly.
- Require two-person review of the profile-to-station and user-to-profile mapping before activation.

### Credential enrollment

- Enroll through an approved offline workflow on the trusted workstation.
- Collect a new password directly from the user; do not copy, decrypt, display, email, or export a legacy password.
- Hash immediately with the approved Phase 6 hasher and store only algorithm, derived hash, salt, work factor, format version, state, revision, and timestamp.
- Start at revision 1 and retain subsequent revisions as immutable evidence.
- Verify the new credential before marking the identity ready, while leaving legacy login authoritative during coexistence.
- Record enrollment completion and identity/profile references without recording the password.

### Management credential issuance

- Issue management credentials separately from user credentials and only to named approved custodians.
- Prohibit shared management identities. If emergency shared custody is unavoidable, require a separately approved dual-control procedure and expiry date.
- Use independent passwords and lifecycle records; a user-password rotation must not implicitly rotate management authority.
- Test management authentication and denial paths before authorizing protected workflows.
- Maintain a sealed offline recovery procedure with access logging and two-person approval.

### Support authorization setup

- Approve support-profile membership, allowed operating window, station scope, and reviewer before enabling support access.
- Select an offline `ISupportCodeProvider` implementation in a later approved phase. Phase 6 currently supplies only the contract.
- Support codes must be single-use, short-lived, unpredictable, correlation-bound, and invalidated after success, expiry, or excessive failure.
- Never hardcode, persist in logs/audits, transmit through SMS/cloud services, or reuse support codes.
- Exercise valid, invalid, expired, replayed, wrong-correlation, and provider-unavailable scenarios before activation.

## 4. Security operational policies

### Password rotation

- Set a documented maximum age and risk-triggered rotation policy based on the approved organizational standard.
- Require immediate rotation after suspected disclosure, recovery use, administrator departure, or material hashing-policy change.
- Rotation creates a new immutable credential revision with a fresh salt; it never updates prior hash evidence in place.
- Reject stale expected revisions and verify the new credential before closing the change record.
- Do not force periodic rotation more frequently than the approved policy without a documented risk reason.

### Credential disablement and lock handling

- Disable credentials promptly for leavers, role changes, compromise, or revoked station access.
- Disablement creates a new revision and must be auditable; it must not delete historical revisions.
- Lock thresholds, observation window, duration, unlock authority, and notification thresholds must be configured and reviewed before activation.
- Unlock must not silently re-enable a disabled credential. Re-enablement, if supported later, requires a separate approved operation.
- Repeated failures against unknown identities must be monitored without disclosing whether an identity exists.

### Support access lifecycle

- Support access is time-bounded, ticket/correlation-bound, station-scoped, and independently reviewed.
- Require active support profile, valid user credential, management authorization, and one-time code for the complete support path.
- Expire support memberships and code-provider authority automatically where a future implementation supports it; otherwise require scheduled manual review.
- Review every successful support action and unusual failed attempt. Close access when the approved task ends.

### Management credential lifecycle

- Review management custodians at a defined interval and immediately after staffing or responsibility changes.
- Rotate management credentials independently and after any suspected exposure or recovery event.
- Disable rather than delete retired credentials; preserve revision and audit evidence.
- Keep issuance, rotation, disablement, recovery, and use auditable and subject to least privilege.
- Do not embed management credentials in configuration, source code, scripts, test fixtures, or support documentation.

## 5. Monitoring and response requirements

Monitoring must operate locally/offline unless a separately approved architecture changes that constraint. Dashboards or exports must read sanitized audit evidence and enforce access controls.

### Login failures

- Track failure count and rate by time window, station, profile, credential identity where known, and workstation instance.
- Alert on bursts, repeated failures across identities, failures immediately after rotation, and persistent unknown-identity attempts.
- Do not include supplied passwords or distinguish unknown identity from wrong password in user-facing feedback.

### Lock events

- Alert on every management lock and on user-lock thresholds defined by operations.
- Correlate the lock with preceding failures, credential revision, station, timestamp, and subsequent unlock/disable action.
- Detect lock storms that may indicate configuration error, clock issues, or denial-of-service attempts.

### Authorization failures

- Track protected-setting, credential-management, report-finalization, station/profile mismatch, inactive-profile, and management-required denials.
- Alert on repeated attempts against the same sensitive operation and cross-station authorization attempts.
- Reconcile denied decisions with secure-operation evidence to prove the protected delegate was not executed.

### Support actions

- Review every successful support authorization and protected action with correlation, operator, management approver, station, operation, and time.
- Alert on invalid, expired, replayed, wrong-correlation, and unavailable-provider outcomes.
- Monitor support actions outside approved windows or after profile/credential disablement.
- Preserve sanitized audit evidence for incident investigation without storing the one-time code.

### Monitoring health

- Monitor audit write failures, database integrity, available disk space, backup age, clock drift, and unreadable/corrupt audit records.
- Define alert owners, severity, acknowledgement time, escalation route, and offline contingency procedure.
- Test monitoring with synthetic events before pilot activation and after each security release.

## 6. Required cutover evidence

The production activation proposal must attach:

- approved migration artifact and checksum;
- successful rehearsal, rollback, and restore results;
- complete gate checklist with reviewers;
- pilot user/profile and station scope;
- credential enrollment readiness summary without secret material;
- audit and monitoring validation evidence;
- authorization test matrix;
- incident, disablement, recovery, and rollback runbooks;
- known risks, accepted exceptions, expiry dates, and accountable owners;
- explicit approval to modify production composition and UI in a future phase.

## 7. Current non-activation confirmation

This phase adds documentation only. The Phase 6 security migration remains an unregistered draft. No service is composed into `Program.cs`; no new login, session, presenter, protected-operation adapter, support-code provider, or authorization service controls production behavior. Legacy login, settings, reporting, credential handling, UI forms, and existing databases remain unchanged.
