# Phase 7.9 — Production Migration Adoption, Backup, and Operational Readiness Foundations

Status: **Implemented as inactive pre-production foundations; production migration remains blocked**

Date: 2026-08-22

Scope: application contracts, offline SQLite adapters, isolated rehearsal, safety policies, tests, and this readiness report

## 1. Executive conclusion

Phase 7.9 provides the inactive workflow boundary needed to assess a caller-selected SQLite database, classify its migration history, create and verify a backup, rehearse the unified chain on an isolated copy, compare preservation evidence, and evaluate readiness for a later approval phase. Nothing in this phase selects a deployment database automatically, runs at startup, modifies a production database, provisions target ESD authority, or authorizes a production migration.

The implemented flow is:

```text
Explicit database selection
        -> read-only preflight
        -> migration/adoption classification
        -> explicit verified backup
        -> isolated-copy rehearsal
        -> integrity and preservation validation
        -> maintenance readiness evaluation
        -> explicit future approval gate
        -> stop
```

The current production-readiness assessment is **Blocked**, intentionally. No production database was selected or inspected, no production backup was requested, no production rehearsal was performed, and no future migration authorization was supplied. Phase 7.9 creates the mechanisms for a later operator-driven assessment; it is not evidence that a particular installation is ready.

## 2. Architectural placement and inactivity

The public contracts are in `Foundation.Application.Database.Readiness`. Policy-only services, including migration classification, adoption planning, preservation comparison, disk estimation, rollback expectations, and the final maintenance gate, are UI-neutral. SQLite-specific adapters are in `Infrastructure.Database.Readiness`.

No Phase 7.9 service is registered in `Program.cs`. There is no startup hook, form integration, feature-flag activation, environment-variable path selection, directory scan, default production-path fallback, or background task. Existing production WinForms and ordinary ShiftProfile workflows remain unchanged. The unified migration chain remains an explicitly constructed inactive chain.

The architecture continues the Phase 7.5 through Phase 7.8 decisions:

- ShiftProfile remains the sole normal operational identity.
- Ordinary Finalize remains a normal ShiftProfile workflow.
- Management proof and external vendor authorization remain required for any future post-Wizard ESD adjustment.
- ESD target provisioning and authority cutover are not performed by rehearsal.
- No RBAC model and no Support role, profile, or login are introduced.

## 3. Explicit database selection boundary

`IExplicitDatabaseTargetInspector` requires a path argument on every inspection. Blank paths, missing files, directories, invalid paths, and files without the SQLite format-3 header produce stable failure categories. Selection is caller-owned: the component does not discover databases, scan directories, inspect environment variables, infer the historical `Data/db.sys` location, or open a database during construction.

A successful inspection returns `DatabaseTargetDescriptor` with:

- the normalized explicit path;
- file size;
- last-write timestamp in UTC;
- inspection timestamp from the injected clock;
- a deterministic safe identity fingerprint derived from SQLite header bytes, size, and timestamp.

The identity fingerprint is an identification aid, not a content-integrity claim and not a secret. Backup SHA-256 and structural fingerprints provide separate stronger evidence. Paths are retained because the future operator must know the exact selected target; callers must treat readiness reports as operational records with appropriate local access controls.

## 4. Read-only preflight

`IReadOnlyDatabasePreflightAnalyzer` opens only the explicit file using SQLite `ReadOnly` mode, private cache, disabled pooling, and `PRAGMA query_only=ON`. It does not use the production connection factory because that factory intentionally establishes writable operational pragmas, including WAL. It also does not use `MigrationRunner.ReadHistoryAsync`, because that runner creates the migration ledger when absent.

Preflight collects without repair or mutation:

- SQLite header validity and openability;
- `quick_check` or full `integrity_check`, selected explicitly by the caller;
- `foreign_key_check` evidence;
- SQLite `schema_version` and `user_version`;
- migration ledger table existence, expected column shape, current version, IDs, transitions, checksums, and applied timestamps;
- tables, indexes, triggers, and views with SHA-256 hashes of their definitions;
- row counts for all non-internal tables;
- recognized legacy and unified-target tables;
- legacy `app_settings.esd_extra_runtime_hours` state;
- target `SecurityDeploymentSettings` ESD state;
- immutable target snapshot and finalized report-lock hashes;
- current journal mode;
- enforced connection read-only status and source file read-only attribute.

Any invalid structure or query failure returns a safe category. No exception text is included in the result. Preflight performs no `CREATE`, `INSERT`, `UPDATE`, `DELETE`, migration, checkpoint, journal-mode change, or repair.

## 5. Migration history classification

`MigrationHistoryClassifier` compares read-only ledger evidence with the authoritative unified chain and returns exactly one classification:

| Classification | Meaning and disposition |
|---|---|
| `CleanLegacyBaseline` | No target ledger/objects, or a pristine version-zero ledger; eligible for isolated-chain rehearsal. |
| `CleanUnifiedTarget` | Complete contiguous unified chain at the supported target version with matching metadata and checksums. |
| `HistoricalDraftRecognized` | A known pre-unification draft ID/transition is present; requires adoption review. |
| `AdoptionRequired` | Target objects or a supported partial state exist but cannot be treated as a clean baseline. |
| `LedgerSchemaMismatch` | Only one ledger table exists or expected ledger columns do not match. |
| `UnknownMigrationHistory` | At least one migration ID is outside the approved chain. |
| `ChecksumMismatch` | A known migration’s stored checksum differs from the current approved definition. |
| `CorruptMigrationHistory` | Duplicate, missing, overlapping, inconsistent, or unreadable history/version evidence. |
| `UnsupportedNewerVersion` | Recorded version is newer than this application supports. |
| `UnsafeToMigrate` | SQLite validity, integrity, or foreign-key preflight did not pass. |

History is never rewritten. The classifier does not fabricate ledger rows, mark drafts applied, repair checksums, normalize transitions, or execute SQL. Only clean legacy and clean unified-target states are directly supported by the inactive rehearsal service; all other states fail closed or require a later approved adoption process.

## 6. Historical draft adoption planning

`HistoricalDraftAdoptionPlanner` converts preflight evidence and classification into a structured, non-executing plan. A clean target produces `NoAdoptionNeeded`; a clean legacy baseline produces `BaselineUnifiedChain`. Recognized draft objects may require `ValidateExistingSecuritySchema`, `ValidateExistingEventSchema`, and/or `ValidateExistingReportingSchema`. Ambiguous partial states produce `RequireManualAssessment`; unsafe, corrupt, mismatched, unknown, checksum-failed, or newer histories produce `RejectAutomaticAdoption`.

The planner reports whether manual review is required and whether automatic adoption is rejected. It never creates or edits ledger records. A future phase must define reviewed schema equivalence rules and explicit authorization before any adoption may execute.

## 7. SQLite backup and immutable verification result

`IExplicitSqliteBackupService` requires both source and destination paths. Source and destination may not resolve to the same file. The destination directory must already exist. Existing destinations are rejected unless the caller explicitly supplies `BackupOverwritePolicy.Allow`; overwrite remains an explicit operation rather than a default.

The service performs a full source preflight and captures a structural source fingerprint, then uses Microsoft.Data.Sqlite’s SQLite backup API from a read-only source connection to an explicitly created destination. SQLite’s backup API provides a consistent copy of committed database state and safely includes committed WAL-backed content without copying a live database file byte-for-byte. The source is never migrated or altered.

After copying, the service performs full integrity and foreign-key checks on the destination, computes its SHA-256 checksum, captures source evidence again, and fails if the source structural fingerprint changed during the operation. The immutable `DatabaseBackupVerificationResult` includes:

- explicit backup path;
- source and backup identity descriptors;
- backup SHA-256 and size;
- creation time from the injected clock;
- migration/schema-chain version and classification;
- SQLite integrity status;
- stable success or failure category.

It carries no credentials, password hashes, salts, verification material, private keys, or other secrets. A verified result is necessary but not sufficient for production readiness.

## 8. Restore validation foundation

`IRestoreValidationService` validates only an explicitly selected backup. It first requires and compares the expected SHA-256 checksum, then performs full read-only SQLite integrity, foreign-key, schema, and migration-state inspection. Missing paths, checksum mismatches, invalid SQLite files, failed integrity, and unsupported migration histories are distinct failures.

Restore validation never replaces, renames, deletes, attaches over, or copies into a production database. It validates a candidate restore artifact only. Actual restore procedures, operator authorization, downtime coordination, and post-restore validation remain production integration prerequisites.

## 9. Isolated migration rehearsal

`IMigrationRehearsalService` accepts only an already verified backup result and revalidates the file and checksum before use. It rejects unsupported adoption states. The workspace factory creates a unique directory under the operating system’s temporary area and copies the verified backup into it. Only that copy is opened read/write.

The rehearsal then:

1. Captures the pre-migration structural fingerprint.
2. Applies the explicit unified chain using the existing transactional migration runner.
3. Runs the chain a second time and requires zero additional migrations, proving idempotent ledger behavior.
4. Performs full read-only integrity and foreign-key validation.
5. Calls ESD reconciliation `InspectAsync` under the inactive `LegacyAuthoritative` authority provider.
6. Captures the post-migration fingerprint.
7. Compares preservation evidence and verifies the original backup checksum remains unchanged.
8. Returns a safe immutable rehearsal result.

The workspace is deleted after use through a guarded temporary-root check. Rehearsal never calls ESD `ProvisionAsync`, never changes authority to `TargetAuthoritative`, and never touches the original selected source. An ESD value conflict blocks the result and requires manual resolution in a future phase.

## 10. Preservation verification

The structural fingerprint and comparator cover the requested evidence while excluding credential and secret material. Inputs include:

- schema object type/name/table and definition hashes;
- table row counts;
- hashes of approved columns in Runtime/Event-related tables;
- target finalized snapshot hashes;
- legacy monthly-report table hashes;
- target report-lock and legacy monthly-lock hashes;
- migration ledger metadata hash;
- canonical legacy and target ESD values.

Tables or columns whose names indicate password, salt, credential, verifier, private key, secret, or recovery material are excluded from representative data hashing. Credential values are never loaded into the fingerprint. Schema-definition hashes may identify schema structure but cannot reveal stored credential values.

The comparator requires every pre-existing non-ledger schema object and row count to remain unchanged; every pre-existing representative Runtime/Event hash must still exist and match; snapshot and lock hash maps must match exactly; legacy and target ESD values must remain identical; and migration ledger progress must reach the supported target and be idempotent. It also rejects schema object names containing RBAC tokens or Support-role/profile/login tokens.

New approved target Runtime/Event evidence can appear after migration, but it cannot replace or alter pre-existing evidence. This asymmetry allows additive migration while preserving all before-state evidence.

## 11. Lock and busy policy

`SqliteLockBusyPolicy` requires a positive busy timeout, a bounded retry count from zero through 100, and an injected delay policy. `SqliteBusyRetryExecutor` retries only SQLite busy/locked error codes, checks cancellation before every attempt, uses the configured delay, and rethrows after the finite retry budget. It has no infinite loop or generic exception retry.

`SqliteLockReadinessEvaluator` exposes the configured timeout and retry count as readiness evidence. This phase does not register the executor around startup or production migration. Future infrastructure must apply the busy timeout at connection/command boundaries consistently and decide an approved maintenance-window retry profile.

## 12. Disk-space safety

`IDiskCapacityProvider` separates capacity lookup from policy. The default drive adapter examines only the root of the explicitly supplied destination; it does not search for databases or select a location. `DiskSpaceReadinessService` estimates:

- one full backup;
- one full rehearsal copy;
- configurable migration growth;
- configurable journal/WAL overhead;
- configurable minimum free-space reserve.

Checked arithmetic prevents overflow from becoming a false ready result. Capacity returns `Ready`, `InsufficientSpace`, or `Unknown`; both insufficient and unknown states block the maintenance readiness gate. Production policy values and destination volumes must be chosen during future operational planning.

## 13. Operational rollback state model

The non-destructive state model is:

| State | Recovery expectation |
|---|---|
| `BeforeMigration` | Preserve source; create and verify backup before any future mutation. |
| `BackupVerified` | A restore candidate exists; production remains unchanged. |
| `MigrationStarted` | Rely on transaction rollback before commit; do not delete or rewrite history. |
| `MigrationCommitted` | Do not attempt destructive reversal; validate and select approved restore or forward repair. |
| `ValidationPassed` | Retain backup and evidence through the approved observation period. |
| `ValidationFailed` | Stop activation, preserve evidence, and follow an approved recovery decision. |

These are evidence and procedure states, not an executable rollback engine. Phase 7.9 intentionally does not delete production files, reverse migrations, rewrite history, or replace a deployment database.

## 14. Maintenance-window readiness contract

`MaintenanceWindowReadinessEvaluator` requires all of the following:

- database and foreign-key integrity passed;
- migration chain is a supported clean baseline or clean unified target;
- no checksum mismatch or corrupt/unknown/newer history;
- verified backup exists and passed integrity;
- migration rehearsal passed;
- no ESD conflict;
- disk capacity is `Ready`;
- bounded SQLite lock policy is ready;
- explicit future migration authorization is available.

Any missing condition produces named blockers and `Blocked`. A passing result is named `ReadyForFutureMigrationApproval`, deliberately not “migration approved” or “migration executed.” The future authorization gate is external to this phase and cannot be inferred from technical readiness.

## 15. Current readiness report

This repository-level report records foundation readiness, not installation readiness:

| Evidence | Current result |
|---|---|
| Database identity | Not selected. No deployment path was supplied or discovered. |
| Production preflight | Not run; prohibited in Phase 7.9. |
| Migration classification | Not available for a production installation. |
| Adoption plan | Not generated for production; planner implementation is available. |
| Production backup | Not created; explicit paths and future operator action required. |
| Production rehearsal | Not run; test rehearsals used isolated temporary fixtures only. |
| ESD reconciliation | No production inspection or cutover; rehearsal adapter is inspection-only and legacy-authoritative. |
| Finalized snapshot preservation | Proven by automated temporary-database tests; not yet assessed for a production installation. |
| Disk readiness | Not assessed for a production destination. |
| Lock readiness | Policy foundation exists; production policy values are not activated. |
| Blockers | Explicit database selection, installation preflight, verified backup, installation rehearsal, capacity assessment, maintenance plan, and future authorization. |
| Warnings | Six pre-existing NU1701 package compatibility warnings remain; no package versions changed in this phase. |
| Final status | **Blocked — pre-production foundation only.** |

## 16. Automated tests

`ProductionMigrationReadinessFoundationTests` uses GUID-scoped databases under the operating system temporary directory. Test guards prevent selection of the production suffix. Tests cover:

- explicit path requirement, invalid SQLite rejection, and absence of discovery/scan APIs;
- read-only byte-preserving preflight and clean legacy classification;
- complete clean-target classification and chain idempotency;
- ledger shape mismatch, checksum mismatch, unknown ID, unsupported newer version, and known historical draft detection;
- adoption planning with mandatory manual security-schema validation;
- verified backup creation, checksum, integrity, source preservation, same-path rejection, overwrite policy, and WAL content;
- restore validation, checksum mismatch, and corrupt SQLite rejection;
- successful isolated rehearsal, second-run idempotency, legacy schema/rows/Runtime/Event evidence, snapshot bytes, report locks, and ESD preservation;
- ESD conflict blocking and retained `LegacyAuthoritative` mode;
- bounded busy retry, fail-closed exhaustion, and cancellation;
- ready, insufficient, and unknown disk capacity;
- explicit future approval gating and rollback-state expectations;
- no RBAC and no Support identity introduction in rehearsal preservation evidence.

The complete suite contains 308 passing tests, including 18 Phase 7.9 test cases after xUnit theory expansion.

## 17. Known limitations

- No real installation has been assessed; this report must not be used as installation approval.
- Known historical draft IDs are explicit. New field evidence requires a reviewed classifier update, not heuristic adoption.
- Structural fingerprints prove selected invariants, not semantic equivalence of every legacy business table.
- Representative data hashing intentionally excludes secret-bearing columns and therefore cannot validate credential values.
- The SQLite backup API is synchronous at its core; cancellation is checked before and around the operation but cannot interrupt the native copy mid-call.
- Disk estimation is policy-based and cannot predict every filesystem, antivirus, shadow-copy, or WAL peak.
- The default temporary rehearsal workspace uses the OS temporary volume; a future operator tool should allow an explicitly approved isolated volume and apply its capacity policy.
- No remote storage, cloud service, remote key download, or network dependency is introduced.
- No production restore or destructive rollback implementation exists.
- No maintenance UI, CLI, service registration, telemetry, or operator approval store exists.
- Migration plus validation across an external operational procedure is not globally atomic; the existing migration runner provides transaction scope only inside the rehearsal copy.

## 18. Production activation prerequisites

A future production migration phase must, at minimum:

1. Define an operator-owned, explicit database and backup destination selection experience without hidden defaults.
2. Review the actual installation’s identity descriptor and retain an approved evidence record.
3. Run read-only full preflight during an approved diagnostic window and resolve every unsafe classification without rewriting history.
4. Obtain manual adoption approval for recognized drafts or ambiguous target schemas.
5. Select operational disk-growth, reserve, busy-timeout, retry, and maintenance-window policies.
6. Create and independently retain a verified backup and validate the restore candidate.
7. Rehearse that exact backup and review integrity, preservation, and ESD reconciliation evidence.
8. Define tested production restore and forward-repair runbooks, owners, stop conditions, and post-migration observation period.
9. Obtain explicit future migration authorization separate from technical readiness.
10. Register production migration only in the future approved phase, with audit-before-execution and no automatic ESD authority cutover.
11. Keep post-Wizard ESD adjustment protected by valid ShiftProfile management proof, external vendor signed authorization, replay protection, audit policy, and atomic persistence/execution.
12. Address or explicitly accept the existing package compatibility warnings before production activation.

## 19. Verification record

The implementation verification requires and records:

- complete Debug and Release solution builds;
- complete automated test suite;
- `git diff --check`;
- unchanged `Program.cs` hash and no diff in production WinForms;
- no startup or feature-flag change;
- no production database discovery, open, migration, backup, or modification;
- all Phase 7.9 integration tests using explicit temporary database paths;
- no ESD provisioning/cutover call in rehearsal;
- no migration history rewriting or automatic draft adoption;
- no RBAC or Support identity;
- unchanged normal ShiftProfile and ordinary Finalize behavior.

Verification completed with these results:

- Debug solution build: succeeded, zero errors, six pre-existing NU1701 warnings.
- Release solution build: succeeded, zero errors, six pre-existing NU1701 warnings.
- Complete Release test suite: 308 passed, zero failed, zero skipped.
- Phase 7.9 focused tests: all passed.
- `git diff --check`: passed; only pre-existing line-ending notices were emitted for unrelated working-tree files.
- `Program.cs`: unchanged from the pre-implementation SHA-256 baseline and has no Git diff.
- Production WinForms: no Git diff.
- Startup and feature flags: unchanged; readiness services are not registered.
- Production database: no path was selected, discovered, opened, backed up, or migrated.
- ESD: rehearsal invokes inspection only, retains `LegacyAuthoritative`, and performs no provisioning or cutover.

Until a future production phase meets the prerequisites above, the only valid final status is **Blocked before production migration**.
