# Phase 5.9 Snapshot Persistence Implementation Report

## Status

Phase 5.9 is implemented as an isolated target Reporting persistence slice. It is not registered in startup, is not called by legacy or production Reporting, and does not alter the existing production database path or behavior. All persistence validation uses disposable test databases.

## Implemented architecture

### Isolated migration

`ReportSnapshotSchemaMigration` is an explicit, unregistered migration containing:

- `ReportSnapshots` for relational identity/evidence metadata plus canonical JSON payload and checksum metadata;
- `ReportPeriodLocks` for canonical Station/half-open-period ownership and compare-revision transitions;
- `ReportFinalizationReceipts` for immutable idempotency and committed-result evidence.

Snapshot and receipt tables have triggers rejecting updates and deletes. Snapshot identity is primary-key unique, and Station/period/kind/sequence is unique within a lineage. The migration is never discovered or invoked by production startup.

### Canonical serialization

`IReportSnapshotSerializer` defines serialization and deserialization through `SerializedReportSnapshot`. `CanonicalJsonReportSnapshotSerializer`:

- uses an explicit schema version (`1`);
- maps snapshot content to a controlled payload contract;
- retains deterministic collection ordering and uses ordinal sorted version dictionaries;
- serializes enums as stable names and JSON using fixed options;
- calculates SHA-256 over UTF-8 canonical JSON;
- records canonical byte length and integrity-format version;
- rejects unsupported schema/checksum states;
- verifies checksum and byte length before reconstruction;
- reserializes reconstructed content and requires byte-for-byte canonical JSON equality.

The checksum excludes its own metadata from the canonical payload. Phase 5.7 pending metadata becomes a calculated checksum at the persistence boundary.

### Persistence adapters

- `SQLiteReportSnapshotStore` performs insert-only snapshot writes, canonical payload reads, checksum verification, and duplicate classification as identical or conflicting.
- `SQLiteReportPeriodLockStore` creates the initial finalized lock at revision 1 or applies a compare-revision/effective-snapshot transition. A stale revision returns a conflict.
- `SQLiteFinalizationReceiptStore` performs immutable insert/read operations and distinguishes idempotent receipt replay from conflicting reuse.

The stores use the Phase 2 `ISqliteConnectionFactory`. Standalone methods manage their own isolated transaction; the atomic coordinator uses transaction-aware overloads so all writes share one connection and transaction.

### Atomic coordinator

`SQLiteAtomicReportFinalizationService` performs:

```text
Pure validation
    -> pure snapshot candidate creation
    -> deterministic serialization/fingerprint
    -> BEGIN IMMEDIATE transaction
    -> idempotency receipt check
    -> snapshot insert
    -> compare-revision lock transition
    -> receipt/audit evidence insert
    -> COMMIT
```

Snapshot, lock, and receipt conflicts are structured results. A conflict after snapshot insertion throws within the coordinator and rolls back all transaction-local effects. Identical committed requests reuse the original receipt and return `IdempotentReplay`. Infrastructure failures return a non-success result without activating any production feature.

## Tests

`SnapshotPersistenceTests` uses only `TemporarySqliteDatabase` fixtures and covers:

- creation of all three target tables through the unregistered migration;
- deterministic JSON, SHA-256 metadata, and validated round trip;
- snapshot insert/read and identical/conflicting duplicate detection;
- initial lock transition and stale compare-revision conflict;
- atomic snapshot/lock/receipt commit;
- identical-request idempotency and result reuse;
- rollback of an inserted correction snapshot when lock transition fails;
- simultaneous competing finalization with exactly one effective snapshot.

Focused Phase 5.9 result: 7 passed, 0 failed, 0 skipped.

## Boundaries and limitations

- No production migration list, startup path, dependency registration, UI, exporter, or legacy Reporting file references this implementation.
- No production database file is opened or modified by implementation or tests.
- Receipt rows provide the required finalization/idempotency audit evidence for this isolated phase; a broader audit subsystem remains future work.
- Source freshness is the caller-supplied verified revision already validated by Phase 5.7. No production source-freshness adapter is implemented.
- Correction authorization is not implemented. The storage/lock primitives can represent a valid superseding lineage, but no production correction feature is active.
- There is no finalized UI/read routing or production feature flag activation.
- Physical schema adoption, integration with the production migration sequence, data migration, operational backup policy, and cutover require separate approval.

## Isolation verification

Legacy `Services/Reports`, `Models/Reports`, `Core/Reports`, UI, and existing database helpers remain unchanged. The new migration is unregistered, all integration tests create uniquely named temporary databases, and no target persistence component is constructed by production code.
