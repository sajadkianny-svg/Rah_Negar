# Phase 5.11 Finalized Report Reader Report

## Status

Phase 5.11 is implemented as an isolated, read-only finalized-report path. It adds no UI, exporter, production registration, operational adapter, snapshot mutation, database migration, or legacy Reporting change. Production report routing remains inactive.

## Contracts

`Application/Reporting/Finalized` contains:

- `IFinalizedReportReader` with reads by stable Snapshot identity and by effective Station/period lock;
- `FinalizedReportQuery` using canonical Station and half-open period identity;
- `FinalizedReportReadResult` with an immutable snapshot only on success;
- `FinalizedReportReadStatus` and deterministic errors.

Supported outcomes are:

- `FoundValid`;
- `NotFound`;
- `NotFinalized`;
- `IntegrityInvalid`;
- `IntegrityUnsupported`;
- `LockSnapshotMismatch`;
- `InfrastructureFailed`.

No failed outcome exposes a partial snapshot.

## Snapshot-based reader

`SnapshotFinalizedReportReader` depends only on:

- `IReportSnapshotStore`;
- `IReportPeriodLockStore`;
- explicitly supplied supported snapshot-format versions;
- explicitly supplied supported integrity-format versions.

An effective read performs:

```text
Read canonical period lock
          |
          v
Require Finalized + effective SnapshotId
          |
          v
Load immutable snapshot by identity
          |
          v
Serializer checksum/schema round-trip verification
          |
          v
Supported snapshot/integrity version verification
          |
          v
Lock/snapshot Station, period, kind, and identity alignment
          |
          v
FoundValid
```

The Phase 5.9 snapshot store and canonical serializer verify the stored SHA-256 checksum, canonical byte length, payload schema version, JSON reconstruction, and byte-for-byte deterministic round trip. The reader then verifies configured snapshot/integrity version support and complete version evidence.

A missing direct Snapshot identity returns `NotFound`. A period without a target lock returns `NotFinalized`. A finalized lock referencing absent or identity-incompatible snapshot content returns `LockSnapshotMismatch`. Unsupported payload/schema/version evidence is distinct from corrupted checksum or domain structure.

## Snapshot-only enforcement

The reader has no dependency on Event repositories, Runtime services, Settings, Station-profile sources, hourly/daily adapters, legacy report services, or UI controls. Missing or invalid finalized content fails closed. It is never repaired or supplemented from live operational data.

Reads are side-effect free. The reader does not insert, update, delete, relock, upgrade, recalculate, or rewrite checksums. Corruption tests alter only disposable test fixtures after removing their immutability trigger; production/domain reader code performs no mutation.

## Tests

`FinalizedReportReaderTests` uses only disposable SQLite databases and covers:

- successful effective read with operational/legacy tables absent;
- invalid stored checksum mapped to `IntegrityInvalid`;
- unsupported payload schema mapped to `IntegrityUnsupported`;
- supported-schema payload with unsupported snapshot-format version;
- lock/snapshot Station identity mismatch;
- missing Snapshot identity mapped to `NotFound`;
- period without target lock mapped to `NotFinalized`.

Focused Phase 5.11 result: 7 passed, 0 failed, 0 skipped.

## Limitations

- The reader is not registered or called by production startup, UI, exporters, or legacy Reporting.
- No lineage query, historical-snapshot browser, correction workflow, or legacy/target routing resolver is implemented.
- Supported version sets must be supplied by a future approved composition root.
- No automatic schema upgrade, recovery, checksum repair, or operational fallback exists.
- The Phase 5.9 migration remains isolated and unregistered; production database behavior is unchanged.

## Isolation verification

Phase 5.11 changes only the new application finalized-reader boundary, its tests, and this report. Legacy `Services/Reports`, `Models/Reports`, `Core/Reports`, UI, operational data services, migrations, and production startup remain unchanged. The tests use uniquely named temporary databases and never open the production database path.
