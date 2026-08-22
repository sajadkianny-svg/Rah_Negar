# Phase 6.2 Security Persistence Schema and Credential Management Report

> **Superseded identity interpretation:** Phase 7.5 makes ShiftProfile the sole normal operational identity. This historical draft schema must not be activated until its UserCredential relationship and singleton ManagementCredential constraints are reconciled. See `phase7-security-architecture-reconciliation-report.md`.

## Status

Phase 6.2 adds an isolated, opt-in SQLite security schema and persistence implementations. The migration is an explicitly unregistered draft: production startup, legacy login, UI, existing schema behavior, and existing database files remain unchanged.

## Schema

The migration creates `ShiftProfiles`, revisioned `UserCredentials`, revisioned `ManagementCredentials`, and append-only `SecurityAuditEntries`. Tables use primary keys, identity/profile uniqueness constraints, positive revisions, enabled or credential-state constraints, UTC timestamps, indexes for current-revision lookups and audit correlation, and restrictive foreign keys.

Credential rows contain only algorithm, derived hash, salt, work factor, and format-version metadata. There is no plain-password column. Credential updates and deletes are rejected by triggers; rotation and disable operations append a new revision. Audit updates and deletes are also rejected.

## Migration safety

`SecurityPersistenceSchemaMigration` uses the existing checksum-validated migration framework and runs within its SQLite transaction. Any statement failure rolls back the schema and migration ledger together. The migration is not registered or discovered by production startup.

## Persistence

SQLite implementations are provided for ShiftProfiles, user credentials, management credentials, and security audit entries. Credential reads select the latest revision. Parameterized statements are used throughout, and stored hashes are reconstructed as the existing immutable `PasswordHash` domain value.

## Credential management

`CredentialManagementService` supports user credential creation, disabling, hash-metadata rotation, and management credential creation. Creation rejects an existing identity, user creation requires an existing ShiftProfile, and mutation requires the caller's expected revision. Every change creates an immutable next revision rather than updating prior evidence.

## Verification

Tests cover schema creation, absence of plain-password storage, insert/read behavior, uniqueness, disabled state, revision rotation and conflict handling, management credentials, audit metadata persistence, checksum-backed migration execution, and transactional rollback.

No legacy login, UI, production composition, existing migration registration, SQLite data file, or existing database schema was modified.
