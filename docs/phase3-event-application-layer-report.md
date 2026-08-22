# Phase 3.2 Event Application Layer Report

**Date:** 2026-08-22  
**Scope:** Isolated target Event command path  
**Activation:** Not connected to legacy UI, runtime, reporting, startup, or production database

## Implementation summary

Phase 3.2 adds a complete isolated Add/Edit/Delete application path over the unregistered Phase 3.1 target schema. Every authoritative read, policy check, baseline/chain load, Event write, and EventAudit insert shares one Phase 2 `IMMEDIATE` SQLite transaction. Expected validation failures are raised inside that transaction to guarantee rollback, then translated to `Result<EventCommandOutcome>`. No successful result is returned before commit.

### Commands and application service

Added `AddEventCommand`, `EditEventCommand`, `DeleteEventCommand`, trusted `EventCommandContext`, and `EventCommandOutcome`. `EventApplicationService` validates ShiftProfile/Station/correlation/reason identity, structural Event creation, Station/Unit ownership, operating-day eligibility, old/new finalized-period locks, trusted baseline presence/boundary, complete affected Unit chains, duplicate timestamps, state transitions, and expected RowVersion.

Add generates stable Event/audit ULIDs and canonical chronological minutes. Edit preserves EventId, Station, CreatedAt/CreatedBy, validates old and new Unit chains, increments RowVersion, and writes before/after audit JSON. Delete replays the remaining full chain, tombstones one expected-version row, and writes a DELETE audit. Audit failure, constraint failure, stale version, lock, invalid chain, or cancellation cannot commit an Event-only change.

### State and chain validation

Added `EventChainEvaluator`, which filters active Events, sorts deterministically by `EventDateTime` then ordinal `EventId`, rejects same-Unit timestamp ties before choosing an order, and replays from `EventBaseline.InitialState`. The trusted baseline contract includes effective chronological boundary and version. Repository chain queries begin at that boundary, and candidate Events before it are rejected.

The existing pure state transition evaluator remains authoritative: Stopped→START, Stopped→OH, Running→NSD/ESD, and StoppedAfterOH→START are valid. Repeated START, Running+OH, stopped ESD/NSD, and post-OH shutdown/OH are rejected without state change.

### SQLite repositories

Added `SqliteEventRepository` and `SqliteEventAuditRepository`. They use only caller-provided `ITransactionContext`, parameterized SQL, explicit active filters/order, expected RowVersion predicates, and canonical mappings. They never begin independent transactions or apply state/runtime rules. UTC values are persisted in canonical `Z` form. Audit repository exposes insert and ordered history only; target triggers still prohibit audit update/delete.

### Supporting isolated services/contracts

Added ownership, finalized-period, operating-day, trusted baseline, ID generation, and Persian Event date/time conversion ports. Added an offline ULID generator and deterministic Persian-to-Gregorian chronological-minute converter. No production policy implementation or dependency registration was added.

## Files created

- `Application/Event/Commands/EventCommands.cs`
- `Application/Event/Policies/EventPolicyContracts.cs`
- `Application/Event/EventApplicationService.cs`
- `Core/Event/Rules/EventChainEvaluator.cs`
- `Infrastructure/Event/UlidEventIdGenerator.cs`
- `Infrastructure/Event/PersianEventDateTimeConverter.cs`
- `Infrastructure/Event/SqliteEventRepository.cs`
- `Infrastructure/Event/SqliteEventAuditRepository.cs`
- `Rah_Negar.Tests/Event/EventApplicationTestContext.cs`
- `Rah_Negar.Tests/Event/EventApplicationServiceTests.cs`
- this report

Phase 3.1 Event entity/rule/repository contracts were extended for rehydration, baseline-boundary chain loading, and typed chain failure codes. No legacy Event, UI, Runtime, Reporting, startup, or legacy data-access file was modified.

## Tests

`dotnet build Rah_Negar.sln --configuration Debug --no-restore` succeeded with **0 errors** and six inherited displayed NU1701 warnings (the known three transitive compatibility warnings reported for each project).

`dotnet test Rah_Negar.sln --configuration Debug --no-build --no-restore --collect:"XPlat Code Coverage"` succeeded with **60 passed, 0 failed, 0 skipped**.

New coverage includes valid START→NSD and Stopped→START; repeated START; Running+OH; stopped ESD; duplicate timestamp; baseline boundary; finalized Add/Edit/Delete; Edit invalidating a later Event; Delete invalidating a later chain; successful Add/Edit/Delete with exactly ADD/EDIT/DELETE audits; optimistic versions; canonical persistence; and injected audit failure rolling back the Event insert. All integration databases use GUID temporary paths, pooling disabled, and cleanup; none resolves `Data/db.sys`.

## Limitations

- The entire path is unregistered and has test-only policy implementations. No existing application feature can invoke it.
- Station, Unit, ShiftProfile, finalized lock, operating-day, and trusted runtime baseline production repositories/policies are not implemented.
- The target schema migration remains a draft with placeholder global version allocation and no production registration/data migration.
- Authorization currently establishes trusted Shift identity shape; actual Phase 1 credential/session composition is not connected.
- Expected failures return stable codes and safe English messages. Persian presentation localization/context-rich correction rendering remains UI work.
- Audit JSON is versioned and canonical-property ordered by the current serializer call, but a dedicated versioned audit serializer contract and compatibility tests remain future hardening.
- Infrastructure failures are safely rolled back and translated, but structured technical logging is not wired.
- The date/time converter fixes chronology as Gregorian ticks/minutes derived from the Persian wall-clock date. This convention must be recorded in database metadata before production activation.
- No runtime calculations, ESD adjustment, reporting, import/migration, UI, concurrency/load, or production-path testing was added.

## Rollback

Remove the new command/policy/service, chain evaluator, Infrastructure/Event implementations, Phase 3.2 tests, and this report; revert the Phase 3.1 contract/baseline-boundary extensions; build and run the Phase 1–3.1 suite. No database downgrade or data recovery is needed because the path is unregistered and all writes occurred only in disposable test databases.

## Verification conclusion

The new Event path enforces transaction-plus-audit for every write and complete-chain validation for affected Units while remaining isolated. Legacy `tbl_events`, production application behavior, runtime, reporting, and UI remain unchanged. The production-like baseline database checksum is verified separately before completion.

