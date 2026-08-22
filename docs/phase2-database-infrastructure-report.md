# Phase 2 Database Infrastructure Report

**Date:** 2026-08-22  
**Scope:** Isolated database infrastructure and tests only  
**Activation status:** Not connected to legacy startup, repositories, or production database

## Implemented items

### Connection and lifecycle

- Added `SqliteDatabaseOptions` for explicit data source, open/cache mode, pooling, and timeout.
- Added `ISqliteConnectionFactory` and `SqliteConnectionFactory`.
- Every opened connection applies and tests `foreign_keys=ON`, `journal_mode=WAL`, `synchronous=NORMAL`, and `temp_store=MEMORY`.
- Factory creates a parent directory only for explicitly configured `ReadWriteCreate`, disposes failed opens, and transfers successful connection lifetime to the caller for `await using` disposal.
- No default or production database path is embedded in the new factory.

### Transactions

- Extended the unused Phase 1 `ITransactionManager` with `ITransactionContext` carrying provider-neutral `DbConnection` and `DbTransaction`.
- Added `SqliteTransactionManager` using a short non-deferred (`IMMEDIATE`) SQLite transaction.
- Successful callbacks commit; exceptions trigger best-effort rollback and preserve the original exception; connection/transaction disposal is guaranteed.

### Migration and schema version framework

- Added `IDatabaseMigration`, `MigrationMetadata`, `SchemaVersion`, `AppliedMigration`, `MigrationHistory`, and `MigrationRunResult`.
- Added SHA-256 checksum abstraction/implementation and `MigrationChecksumValidator`.
- Added explicit-only `MigrationRunner` with ordered execution, monotonic version validation, unique migration IDs, payload checksum validation, applied-checksum conflict detection, idempotent pending execution, and atomic ledger/schema changes.
- Framework tables are `__rahnegar_schema_version` and `__rahnegar_migration_history`; they are created only when a caller explicitly runs/reads the framework against its configured database.
- No business, Event, report, legacy, or production migration was created or registered. Nothing invokes the runner from application startup.

### Integrity and backup foundation

- Added `DatabaseIntegrityService` for `PRAGMA integrity_check`, `PRAGMA foreign_key_check`, and composable `IDatabaseSchemaValidationHook` checks.
- Added typed integrity result and foreign-key violation models.
- Added backup foundation contracts only: `BackupMetadata`, `BackupStatus`, `BackupVerificationResult`, and `IBackupVerificationService`.
- The checksum abstraction can support future manifest/backup verification, but no production backup UI, Restore, catalogue, encryption replacement, or file swap was implemented.

## Files created

Production-unused infrastructure was added under:

- `Infrastructure/Database` — options, connection factory, transaction manager
- `Infrastructure/Database/Checksums` — checksum contract and SHA-256 implementation
- `Infrastructure/Database/Migrations` — migration contracts/models/validator/runner
- `Infrastructure/Database/Integrity` — integrity models/hooks/service
- `Infrastructure/Database/Backup` — backup contracts/models
- `Rah_Negar.Tests/Database` — temporary database guard/helper and database tests

This report is the only new documentation file for Phase 2.

## Files modified

- `Application/Foundation/Transactions/ITransactionManager.cs` now supplies an `ITransactionContext` to the callback.
- Added `Application/Foundation/Transactions/ITransactionContext.cs`.

No existing legacy data-access, schema, Event, Runtime, Reporting, UI, startup, backup, or recovery source file was modified.

## Tests executed

`dotnet build Rah_Negar.sln --configuration Debug --no-restore` succeeded with 0 errors. It reports the known six displayed NU1701 warnings: the same OpenTK, OpenTK.GLControl, and SkiaSharp.Views.WindowsForms compatibility warnings inherited by both solution projects.

`dotnet test Rah_Negar.sln --configuration Debug --no-build --no-restore --collect:"XPlat Code Coverage"` succeeded: **19 passed, 0 failed, 0 skipped**. This includes all ten Phase 1 tests plus nine Phase 2 tests.

Phase 2 verification covers:

- option validation and exact connection PRAGMA values;
- transaction commit and exception rollback;
- reversed-input migration ordering and version/history rows;
- repeat execution idempotency;
- invalid checksum rejection with ledger/schema rollback;
- injected migration SQL failure with complete rollback;
- temporary-database integrity check, foreign-key check, and schema hook execution.

All integration tests create a GUID-named directory below the OS temporary path, use `fixture.sqlite`, disable pooling, reject the production `Data/db.sys` suffix, clear pools, and delete the temporary directory. They do not resolve the application output/database path.

## Verification of unchanged production behavior and data

The new classes have no composition root registration and are not referenced by `Program`, `SqliteDatabaseHelper`, startup services, or legacy repositories. Existing application behavior therefore remains on the legacy path.

The Phase 0 database artifact `bin/x64/Debug/net8.0-windows/Data/db.sys` retained SHA-256 `EB3ECA2C96092888912D23AAFD2B4DBBBC1F25CA13894EB2E39B67B5ED4D2F43`. No application launch, migration, schema creation, or backup/Restore command targeted it.

## Limitations and deferred work

- The migration ledger is framework infrastructure, not yet the fully audited/management-authorized production Migration Manager.
- Migration timestamps currently use UTC system time directly; production composition should inject the approved clock/actor/correlation context before activation.
- No migration package signing, application compatibility range, preflight, backup binding, Started/Failed durable state, multi-process maintenance lock, or post-restart validation exists yet.
- No Event/EventAudit/report/baseline target tables or repositories were created; those require separately approved implementation work and tests.
- Integrity hooks are caller-supplied; no legacy or target schema shape hook is registered.
- Backup verification is contract-only; there is no implementation or UI.
- No concurrency/load, power-loss, WAL backup, Restore, path-reparse, or production-like database test was added in this isolated phase.
- Existing NU1701 and multiple SQLite provider/package ambiguity remain unresolved.

## Rollback procedure

Rollback is source-only and requires no database operation:

1. remove `Infrastructure/Database` and `Rah_Negar.Tests/Database`;
2. restore the Phase 1 `ITransactionManager` signature and remove `ITransactionContext`;
3. remove this report;
4. build the solution and run the remaining Phase 1 tests.

Because the infrastructure is not activated and tests clean their temporary databases, rollback requires no schema downgrade, data copy, or feature switch.

