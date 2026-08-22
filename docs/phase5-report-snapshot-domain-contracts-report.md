# Phase 5.7 Report Snapshot Domain Contracts Report

## Status

Phase 5.7 is implemented as an isolated Core/Application contract slice. It adds immutable snapshot models, a pure validation gate, and a pure snapshot factory. It does not add persistence, repositories, transactions, database/schema changes, UI, exporters, production registration, lock execution, or replacement of legacy Reporting.

## Created contracts

### Core snapshot domain

`Core/Reporting/Snapshot` contains:

- `ReportSnapshotIdentity` — stable Snapshot/Report/Station/period identity, canonical Unit order, positive lineage sequence, and optional superseded snapshot identity.
- `ReportSnapshotEvidence` — copied projection source evidence plus verified revision, finalization identity, actor, caller-supplied timestamps, finalization policy version, and integrity version.
- `SnapshotChecksum` — immutable checksum metadata supporting `Pending` and `Calculated` states. Phase 5.7 creates only a pending SHA-256 placeholder; it does not serialize or hash snapshot content.
- `FinalizedReportSnapshot` — self-contained immutable capture of report identity, completeness, evidence, versions, checksum metadata, and every section currently exposed by `ReportProjection`.

Snapshot construction defensively copies report identity, completeness issues, evidence, version dictionaries, nested date collections, report sections, Event logs, and warnings. Collections are exposed through read-only wrappers and use explicit ordinal/chronological ordering.

### Application finalization contracts

`Application/Reporting/Finalization` contains:

- `ReportFinalizationRequest`;
- `ReportFinalizationResult`;
- `FinalizationValidationResult` and deterministic validation issues;
- `IReportFinalizationValidator` with pure `ReportFinalizationValidator` implementation;
- `IReportSnapshotFactory` with pure `ReportSnapshotFactory` implementation.

Finalization results distinguish successful candidate creation, incomplete rejection, version rejection, source-change rejection, and general identity/evidence validation rejection. Rejected results contain no snapshot.

## Architecture alignment

The implemented flow is deliberately limited to:

```text
Complete ReportProjection
          |
          v
Pure finalization validator
          |
          v
Pure snapshot factory
          |
          v
Immutable candidate FinalizedReportSnapshot
```

The validator checks:

- required finalization and snapshot identities;
- Station, half-open period, and canonical Unit-set alignment;
- open-projection source mode and snapshot lineage shape;
- required source, hourly, daily, profile, calendar, and ordering evidence;
- complete/finalization-eligible status with no blocking reasons;
- every Phase 5.3 required report/Event/Runtime/Baseline/configuration/calendar version;
- expected, projection, and verified source-revision alignment.

The factory accepts the caller-supplied validation result and copies an eligible projection. It does not calculate Reporting, Runtime, or Events. It does not read the clock; calculation and finalization timestamps are supplied by existing contracts/callers. It performs no IO and does not create or transition a lock.

The resulting snapshot is a candidate domain object only. `Succeeded` means pure candidate construction succeeded, not that a durable commit or period lock occurred. A future coordinator/persistence phase must define committed success separately and atomically.

## Tests

`Rah_Negar.Tests/Reporting/ReportSnapshotDomainContractsTests.cs` adds nine focused tests covering:

- valid snapshot construction and identity capture;
- pending checksum metadata;
- immutable/read-only collections and detached report identity;
- evidence and version preservation;
- deterministic Unit, Event, Runtime, and section ordering;
- missing evidence rejection;
- missing version rejection;
- incomplete projection rejection without snapshot creation;
- identity mismatch and changed-source rejection.

Focused result: 9 passed, 0 failed, 0 skipped.

## Limitations

- No checksum payload canonicalization or checksum calculation is implemented; metadata remains pending.
- No database schema, SQLite code, repository, serialization, migration, transaction, or storage validation exists.
- No source-freshness reader exists. The validator compares caller-supplied expected and verified revision evidence only.
- No lock state, lock transition, concurrency, idempotency, or atomic snapshot/lock coordinator is implemented.
- No authorization implementation exists; the contract only captures a supplied actor identity.
- No correction workflow is active. Identity contracts can represent a superseding sequence, but approval, persistence, effective-lineage transition, and auditing remain future work.
- No UI, finalized reader, exporter, production registration, or legacy-report cutover is included.
- Snapshot format compatibility and aggregation across versions remain fail-closed future policy work.

## Isolation verification

No file under legacy `Services/Reports`, `Models/Reports`, `Core/Reports`, `UI`, or `Data` is modified by Phase 5.7. No database or schema artifact is created or changed. The contracts remain disconnected from production Reporting paths.
