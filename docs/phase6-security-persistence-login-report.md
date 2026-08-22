# Phase 6.1 Security Persistence and Login Infrastructure Report

> **Superseded identity interpretation:** Phase 7.5 makes ShiftProfile the sole normal operational identity. Any UserCredential record is internal 1:1 credential material only. See `phase7-security-architecture-reconciliation-report.md`.

## Status

Phase 6.1 adds isolated credential persistence, password hashing, login, session, lock-policy, and security-audit boundaries. It does not register these services in production, replace the legacy login, modify UI, or create/migrate a database schema.

## Persistence boundaries

`IUserCredentialStore` defines user lookup, ShiftProfile lookup, and credential persistence. `IManagementCredentialStore` independently defines management-credential lookup and persistence. No SQLite implementation is included because Phase 6.1 must not migrate or activate the existing user database.

## Password hashing

`IPasswordHasher` extends the Phase 6.0 verification boundary and provides password hashing. `PasswordHasher` uses PBKDF2-HMAC-SHA256, a cryptographically random per-password salt, fixed-time verification, and configurable work factor, salt size, and output size. The default work factor is 210,000 iterations; options reject work factors below 10,000 and salt/hash sizes below 16 bytes. Hash metadata remains represented by the Phase 6.0 `PasswordHash` value object.

## Login and session boundary

`LoginService` supports user login and separate management authentication. Results distinguish success, invalid credentials, locked credentials, disabled credentials, and unavailable/inactive ShiftProfiles. Successful user login returns an immutable `IUserSessionContext` containing the identity, complete ShiftProfile, and authentication correlation/timestamp/credential metadata. The service does not write to `AppSession` or any legacy state.

## Lock and audit boundaries

`ICredentialLockPolicy` receives successful and failed authentication outcomes and may signal that a failed attempt reached a lock threshold. Policy persistence and thresholds remain implementation choices. `ISecurityAuditWriter` receives successful login, failed login, credential-lock, and management-authentication events with correlation, timestamp, subject, credential revision context where applicable, profile/station metadata, and result. Passwords, hashes, and salts are excluded.

## Tests

Tests cover salted hashing and verification, configurable work factor, successful login, invalid password, locked and disabled credentials, unavailable profile, management authentication, session creation, audit metadata, and lock-event auditing.

## Isolation verification

No production composition root, UI file, legacy login class, legacy password helper, SQLite schema, migration, or data file is changed by Phase 6.1. The new infrastructure remains opt-in and inactive.
