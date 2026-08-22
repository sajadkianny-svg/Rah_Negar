# Phase 6.0 User Identity, Shift Profile and Authorization Foundation Report

> **Superseded security model:** Phase 7.5 reconciliation is authoritative. Local support-enabled profiles and support actions described here are retired and are not active target-architecture contracts. See `phase7-security-architecture-reconciliation-report.md`.

## Status

Phase 6.0 introduces isolated Core and Application security contracts. It does not replace or call the legacy login, register production services, add UI, persist credentials, migrate SQLite, or activate authorization in any production workflow.

## Identity model

`Core/Security/Identity` defines three intentionally small identity concepts:

- `ShiftProfile` identifies a Station shift and whether it is operational or support-enabled;
- `UserCredential` binds a user identity to one ShiftProfile and immutable password-hash metadata;
- `ManagementCredential` represents the separate credential required for protected management actions.

Credentials expose only hash metadata, lock state, and revision. Plain-text passwords are never stored in domain objects. Constructors require canonical non-empty identities and positive revisions/work factors.

`CredentialLockState` supports `Active`, `Locked`, and `Disabled`. A locked or disabled credential is rejected before password-hash verification.

## Credential security boundary

`IPasswordHashVerifier` abstracts password verification and deliberately leaves algorithm implementation, secret-memory policy, package choice, and hash upgrades to a future infrastructure phase. Phase 6.0 adds no concrete cryptography and does not reuse or modify the legacy `PasswordHelper`.

`CredentialValidator` applies lock-state gating and delegates only active credential verification to the abstraction. `CredentialValidationResult` distinguishes:

- valid credential;
- wrong password;
- locked credential;
- disabled credential.

Validation results carry identity, credential revision, and the ShiftProfile binding needed by authorization. They do not carry the supplied password, hash, salt, or recovery material.

## Authorization boundary

`Application/Security/Authorization` defines `IAuthorizationService`, immutable request/result contracts, fixed permission definitions, and `AuthorizationService`.

The foundation avoids configurable RBAC. Permissions belong to one of three fixed categories:

### Operational actions

- view operational data;
- enter operational data;
- edit operational data.

These require a valid active user credential whose ShiftProfile identity matches the requested profile.

### Management actions

- finalize a report;
- manage credentials;
- change protected settings.

These require the valid shift credential plus a separately validated `ManagementCredential`.

### Support actions

- run diagnostics;
- request password recovery;
- view authorization audit evidence.

These require a valid credential bound to an active support-enabled ShiftProfile. Support capability is an explicit profile kind, not an arbitrary permission grant.

All categories fail closed for invalid credentials, inactive profiles, or ShiftProfile mismatches.

## Audit boundary

`IAuthorizationAuditWriter` receives one `AuthorizationAuditEntry` for every decision. The entry includes caller-supplied correlation identity and timestamp, subject, ShiftProfile, Station, permission/category, decision, optional validated management identity, and deterministic reasons.

Audit evidence contains no passwords, hashes, salts, tokens, or connection details. The application service awaits audit creation before returning. Storage, retention, transaction policy, redaction infrastructure, and failure recovery are deferred.

## Password recovery boundary

`IPasswordRecoveryService` is a contract only. Its request/result models establish recovery identity, subject, Station, caller-supplied timestamp, status, and reasons. Phase 6.0 does not define recovery secrets, security questions, reset codes, persistence, delivery, UI, or concrete behavior.

## Tests

`AuthorizationFoundationTests` covers:

- correct credential validation;
- wrong-password rejection;
- locked-state rejection without invoking password verification;
- allowed operational permission;
- denied permission for an invalid credential;
- management denial without and authorization with a valid ManagementCredential;
- audit creation and metadata preservation without password material.

Focused Phase 6.0 result: 7 passed, 0 failed, 0 skipped.

## Limitations and future work

- There is no concrete password hashing or verification implementation.
- There is no credential/profile repository, SQLite schema, migration, transaction, or caching.
- There is no login/session integration, attempt counter, timed lockout, logout, or production authorization middleware.
- ManagementCredential issuance, rotation, recovery, and dual-control policy remain undefined.
- Shift scheduling, handover, overlap, and history are outside this foundation.
- Permission activation in reporting, editing, settings, and support workflows requires separate approval.
- Audit persistence and availability policy require a future design.

## Isolation verification

Phase 6.0 changes only new `Core/Security/Identity` and `Application/Security/Authorization` files, isolated tests, and this report. Legacy `FrmLogin`, `PasswordHelper`, `PasswordManagementService`, `AppSettingsService`, `AppSession`, existing management authorization contracts, UI, database schema/migrations, and production startup remain unchanged. No production registration exists.
