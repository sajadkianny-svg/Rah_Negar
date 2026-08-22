# Phase 3.1 Event Target Schema Foundation Report

**Date:** 2026-08-22  
**Scope:** Unregistered target schema draft, isolated Event domain foundation, and contracts  
**Activation:** None; legacy Event UI, persistence, runtime, and reporting remain authoritative

## Implemented items

- Added an `EventTargetSchemaMigration` artifact using the Phase 2 migration contract and checksum validation.
- Added target `Events` and append-only `EventAudit` table definitions, closed Event type/action representation, ownership/actor foreign keys, indexes, structural checks, active timestamp uniqueness, tombstone consistency, optimistic RowVersion, immutable-field protection, and audit append-only triggers.
- Added Event domain types: `Event`, `EventType`, `EventStatus`, `EventOperationalState`, `EventAudit`, `EventAuditAction`, `EventValidationResult`, validation errors, and creation result.
- Added strict canonical Event type code parsing and full Persian calendar date validation at Event creation.
- Added rule contracts `IEventValidator`, `IEventStateTransitionEvaluator`, and `IEventChainEvaluator`, plus typed transition/chain results.
- Added an isolated pure `EventStateTransitionEvaluator` implementing the approved valid/forbidden transition matrix. It has no persistence or production caller.
- Added repository contracts `IEventRepository` and `IEventAuditRepository`; no SQLite repository implementation exists.

## Files created

- `Infrastructure/Database/Migrations/Drafts/EventTargetSchemaMigration.cs`
- `Core/Event/Event.cs`
- `Core/Event/EventType.cs`
- `Core/Event/EventStatus.cs`
- `Core/Event/EventAudit.cs`
- `Core/Event/EventValidationResult.cs`
- `Core/Event/Rules/EventRuleContracts.cs`
- `Core/Event/Rules/EventStateTransitionEvaluator.cs`
- `Application/Event/IEventRepository.cs`
- `Application/Event/IEventAuditRepository.cs`
- `Rah_Negar.Tests/Event/EventDomainTests.cs`
- `Rah_Negar.Tests/Event/EventStateTransitionTests.cs`
- `Rah_Negar.Tests/Event/EventAuditTests.cs`
- `Rah_Negar.Tests/Event/EventTargetSchemaTests.cs`
- this report

No existing legacy Event, Runtime, Reporting, UI, database helper, or startup file was modified.

## Schema decisions

The draft creates new `Events` and `EventAudit` objects and never references or alters legacy `tbl_events`. `EventTargetSchemaMigration` is under a `Drafts` namespace/folder, has no registry/discovery/composition, and is never called by application startup. Its draft version numbers are test-only placeholders and require an approved global migration allocation before any future registration.

`EventId` and `AuditId` are canonical uppercase 26-character ULID text. Events explicitly own `StationId` and composite `(StationId, UnitId)`. Event types are exact closed codes `START`, `NSD`, `ESD`, and `OH`; they are not configurable lookup data. Persian `EventDate` is an integer `yyyyMMdd` structural candidate, `EventTime` is minute-of-day `0..1439`, and `EventDateTime` is a chronological integer minute key whose minute component must match EventTime. Full Persian validity remains a domain responsibility and is implemented in the Event creation foundation.

The active unique index covers `(StationId, UnitId, EventDateTime)` where `IsDeleted=0`; different Units may share a timestamp and tombstones retain identity. Query indexes support Station chronology, Unit-chain replay, and Persian-day queries. Deletion metadata must be all-null for active or complete for deleted rows. RowVersion is positive. Creation/Station identity is protected against update, and a tombstoned row cannot be mutated again.

EventAudit enforces `ADD`, `EDIT`, and `DELETE` snapshot shapes, restrictive Event/actor foreign keys, required reason/correlation, useful actor snapshots, and append-only update/delete triggers. In accordance with the newer approved foundation identity, actor fields use stable ShiftProfile IDs plus optional PersonnelNo/supervisor snapshots rather than the older generic `User` wording in the Event schema document.

The draft assumes approved parent tables named `Stations`, `Units`, and `ShiftProfiles` with matching binary text keys and a unique `(StationId, UnitId)` ownership key. It does not create those foundation tables.

## Tests executed

`dotnet build Rah_Negar.sln --configuration Debug --no-restore` succeeded with **0 errors** and the six previously documented displayed NU1701 warnings inherited across both solution projects.

`dotnet test Rah_Negar.sln --configuration Debug --no-build --no-restore --collect:"XPlat Code Coverage"` succeeded with **47 passed, 0 failed, 0 skipped**. Coverage output was generated under ignored test results.

New tests verify:

- valid Event creation, normalization, stable actor/status/version fields;
- aggregation of invalid identity, Station/Unit, enum, Persian date, time/date-time, UTC, and actor inputs;
- actual Persian invalid month-day rejection;
- exact Event type code acceptance and alias/case/whitespace rejection;
- all seven approved forbidden transition examples and five valid transitions;
- EventAudit construction and action/snapshot shape rejection;
- target table/index/trigger creation on a GUID temporary database;
- preservation of a temporary legacy `tbl_events` object during target creation;
- database rejection of unknown Event type, duplicate active Unit timestamp, and invalid Unit ownership;
- EventAudit action-shape enforcement and append-only update rejection.

All database tests use the Phase 2 temporary database guard beneath the OS temporary directory, disable pooling, never resolve `Data/db.sys`, and delete test directories after execution.

## Limitations and deferred work

- This is a schema migration draft, not authorization to apply a migration. It lacks approved production version allocation, package signing, backup/approval binding, and production parent-table migrations.
- No production Event/ShiftProfile/Station/Unit schema exists through this work; tests create minimal prerequisite parents only in temporary databases.
- No legacy mapping, staging, copy, reconciliation, or anomaly handling is implemented.
- `EventDateTime` epoch/local-time convention must be formally fixed before command/persistence implementation. The current entity validates minute consistency but accepts the derived key from its isolated factory; future commands must compute it through one approved Persian date/time service rather than UI/import input.
- Remark maximum length remains a product-policy decision; the draft does not silently invent a limit.
- UTC text checks are structural and do not replace canonical serializer/integrity validation.
- Repository interfaces have no implementation. There are no Add/Edit/Delete handlers, complete-chain evaluator implementation, finalized/operating-day policy integration, concurrency workflow, runtime projection, or reporting/UI integration.
- Trigger defenses do not implement the Event state machine; complete-chain validation must remain in the application transaction.
- Existing dependency warnings and provider ambiguity remain outside this phase.

## Rollback procedure

Rollback is source-only: remove the draft migration, `Core/Event`, `Application/Event`, Event tests, and this report; then build and run the Phase 1/2 suite. Because the draft is unregistered, no application code references it, and all executions occurred on disposable temporary databases, rollback requires no database downgrade, data copy, or feature switch.

## Verification conclusion

The target Event schema/domain foundation is isolated and reversible. The legacy `tbl_events` schema and all existing Event/UI/runtime/reporting code remain unchanged. The Phase 0 database artifact checksum is verified separately at completion and no production migration or application execution occurred.

