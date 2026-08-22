# Phase 7.7 Security Persistence and Atomic ESD Infrastructure

## Status and isolation

Status: **implemented and verified as inactive SQLite infrastructure**.

This phase adds an explicitly versioned target schema, inactive repositories, durable replay storage, append-only audit persistence, and a transactionally atomic ESD setting adapter. It follows the Phase 7.5 identity model and Phase 7.6 authorization/cryptography contracts. It does not register any service, change `Program.cs`, enable authentication, alter a feature mode, modify a production WinForms file, or connect to a production database.

Every migration/repository/atomic test uses `TemporarySqliteDatabase`, which creates a unique database under the operating-system temporary directory and deletes it afterward. No production database discovery exists in the new infrastructure. The migration remains an unregistered draft and therefore cannot run during application startup.

## Implemented components

- `Infrastructure/Database/Migrations/Drafts/SecurityPersistenceSchemaMigration.cs`: versioned, checksummed, non-destructive target migration from framework version 0 to 1.
- `Application/Security/SecurityPersistenceContracts.cs`: repository contracts and technical credential/device records.
- `Infrastructure/Security/SQLiteSecurityRepositories.cs`: ShiftProfile, ShiftProfile credential, ManagementCredential, DeviceId, vendor public-key, replay-reservation, and audit adapters.
- `Infrastructure/Security/SQLiteAtomicEsdAdjustmentExecutionBoundary.cs`: SQLite implementation of `IAtomicEsdAdjustmentExecutionBoundary` with rollback/failure-injection seams.
- `Application/Security/ProtectedEsdExecution.cs`: extends transaction evidence with DeviceId, action, KeyId, initiating ShiftProfileId, and exact proposed decimal.
- `Rah_Negar.Tests/Security/SecurityPersistenceAtomicEsdTests.cs`: temporary-database migration, constraint, repository, concurrency, immutability, atomicity, and rollback tests.

## Schema overview

The migration ID is `phase7.7-security-persistence-atomic-esd-v1`. Its SQL is a stable checksum payload handled by the existing migration checksum validator/history ledger. It creates only new `Security*` target tables, indexes, and triggers. It contains no `DROP`, table rebuild, copy from a production table, or automatic seed identity.

### SecurityShiftProfiles

Primary key: `ShiftProfileId`.

Columns: StationId, ShiftNumber, ShiftName, supervisor first/last names, display PersonnelNo, normalized PersonnelNo, active flag, created/updated UTC, and positive Revision. `(StationId, ShiftNumber)` is unique, representing one stable ShiftProfile per configured shift. A partial unique index on `(StationId, PersonnelNoNormalized)` where active enforces unique active login names within Station/deployment scope. An index supports active profiles ordered by shift.

There is no RoleId, RoleName, PermissionGroup, ProfileKind, Support flag, Administrator flag, or permission table. All active profiles remain equivalent normal operational identities. Personnel normalization is `Trim().ToUpperInvariant()` in the application adapter; the normalized value is stored explicitly for deterministic lookup/uniqueness.

`SQLiteShiftProfileRepository` reads active profiles, finds an active profile by normalized PersonnelNo, creates Wizard profiles, and updates metadata/active state with optimistic Revision. Updates preserve the primary ShiftProfileId and advance the stored revision only when the expected revision matches.

### SecurityShiftProfileCredentials

Composite primary key: `(ShiftProfileId, CredentialVersion)`. The ShiftProfileId is a restrictive foreign key to SecurityShiftProfiles. The record has only KDF algorithm/parameters, Salt bytes, PasswordVerifier bytes, current state, creation UTC, and retirement UTC. It has no username, role, profile kind, or independent identity.

A partial unique index permits exactly one current revision per ShiftProfile. A check couples current state to null retirement and historical state to non-null retirement. Replacement retires the expected current version and inserts the next revision in one transaction. Missing/stale expected versions fail without inserting. Constraint or locking races return a safe losing result.

No plaintext or reversible password field exists. KDF bytes are infrastructure-only records and never audit/presentation metadata.

### SecurityManagementCredentials

The singleton identity is constrained to integer `1`. Revisions use `(SingletonId, CredentialVersion)` as primary key so retired evidence can be preserved. A partial unique index on SingletonId where current allows only one current deployment ManagementCredential. It contains KDF metadata, Salt, verifier, current/active state, created/updated UTC, and retirement UTC—no username, ShiftProfileId, or normal-session field.

`SQLiteManagementCredentialRepository` loads the singleton current revision and replaces it transactionally using expected CredentialVersion. Initialization expects no current revision. Concurrent initialization tests prove one winner; later expected-version replacement retires the old row and creates one new current row.

### SecurityDeviceIdentity

`SingletonId = 1` is the primary key. The table stores one opaque DeviceId, provisioning UTC, and positive Revision. DeviceId is separately unique and must be nontrivial length. It contains no Station name, hardware serial, fingerprint components, or secret meaning.

`SQLiteDeviceIdentityRepository` loads/provides the stable ID and performs insert-only provisioning. A second provision attempt loses via singleton constraint. No generation or startup provisioning is registered here.

### SecurityTrustedVendorPublicKeys

KeyId is the primary key. The table stores public verification bytes, fixed algorithm identifier `ECDSA-P256-SHA256`, activation UTC, optional retirement UTC, creation UTC, positive Revision, and SHA-256 integrity metadata for the public material. No private-key column exists.

Key creation is insert-only, so the same KeyId cannot silently replace prior material. Retirement uses expected Revision, sets retirement once, and preserves the historical key. The repository implements the Phase 7.6 trusted-key lookup boundary. The check requires retirement after activation.

### SecurityDeploymentSettings

This is the inactive target architecture’s singleton ESD record (`SingletonId = 1`). It contains exactly one `EsdAdjustmentCanonical` string, Revision, update UTC, and optional updating ShiftProfile foreign key. It deliberately has no UnitId or per-Unit row.

The canonical decimal is stored as invariant `G29` text rather than SQLite REAL, preserving the exact approved .NET decimal value without binary floating-point conversion. The legacy production `app_settings.esd_extra_runtime_hours` is not read, updated, or synchronized in Phase 7.7. Production cutover must later decide how to provision the singleton and reconcile legacy state under an approved migration.

### SecurityConsumedVendorAuthorizations

RequestId is the primary key and therefore globally unique for this deployment database. Stored evidence includes CorrelationId, DeviceId, fixed action, exact canonical proposed ESD decimal, KeyId, consumption UTC, initiating ShiftProfileId, unique ExecutionReceiptId, and safe status.

Foreign keys bind the actor to ShiftProfile and KeyId to retained public-key evidence. No raw signed envelope, support code, signature bytes, password, verifier, salt, recovery secret, or private key is stored. Update/delete triggers make every row immutable.

Status `Succeeded` is written only by the atomic ESD adapter. Status `Consumed` is used by the standalone fail-closed replay reservation adapter: a reserved request is permanently unusable and cannot later claim successful execution. `SQLiteConsumedVendorAuthorizationStore` implements `IsConsumedAsync` and uniqueness-backed `TryConsumeAsync`; concurrent claim tests yield exactly one winner.

### SecurityProtectedExecutionReceipts

ExecutionReceiptId is the primary key; RequestId is uniquely constrained and references consumed evidence. Each successful receipt records correlation, fixed action, initiating ShiftProfileId, exact canonical value, execution UTC, `Succeeded`, and resulting configuration Revision. Thus one RequestId cannot produce multiple successful receipts. Update/delete triggers make receipts immutable.

### SecurityAuditEntries and SecurityAuditMetadata

Audit entries store generated AuditEntryId, initiating ShiftProfileId, action, scope, authorization type, result category, UTC timestamp, correlation, and optional RequestId. Correlation/time and non-null request indexes support investigations.

Metadata is normalized into a child table with `(AuditEntryId, MetadataKey)` primary key. The database CHECK allow-list exactly matches Phase 7.6: DeviceId, RequestId, ProposedEsdAdjustment, AuthorizationStage, ResultCategory, KeyId, and CorrelationId. The application builder validates the same list before opening a transaction. Arbitrary keys and all secret categories are rejected. Parent and metadata update/delete triggers enforce append-only behavior.

## Atomic ESD transaction

`SQLiteAtomicEsdAdjustmentExecutionBoundary` opens a SQLite connection and transaction, then:

1. checks whether RequestId already exists;
2. inserts immutable consumed authorization evidence with all non-secret bindings;
3. updates only the singleton SecurityDeploymentSettings row using the exact canonical decimal and increments Revision;
4. invokes the supplied transaction-scoped mutation callback;
5. inserts a unique successful execution receipt with resulting Revision;
6. commits.

Constraint, injected, callback, or SQLite failures roll back the transaction. A duplicate RequestId returns `AlreadyConsumed`; constraint/busy/store failures return `StoreFailed`; callback failure returns `MutationFailed`. Successful completion returns the durable receipt ID.

The database setting update is the authoritative mutation performed inside the transaction. The callback must remain transaction-scoped and must not perform irreversible external side effects, because SQLite cannot roll back external systems. Current application composition is inactive and supplies no production callback.

The adapter never reads or writes ReportSnapshots, ReportPeriodLocks, or finalized payloads. Tests create representative finalized evidence, mutate ESD, and confirm its bytes/text remain unchanged. ESD affects future recalculation of open periods only when a later production calculation adapter intentionally reads the current target setting.

## Concurrency behavior

Uniqueness and transactions determine winners rather than process-local locks:

- partial unique active PersonnelNo index rejects duplicate normalized active login names;
- partial unique current credential indexes permit one ShiftProfile and one management current revision;
- Device singleton primary key permits one provisioning row;
- vendor KeyId primary key prevents overwrite;
- RequestId primary key permits one consume/claim;
- receipt RequestId unique constraint permits one successful receipt.

Repositories treat SQLite constraint and busy/locked race outcomes as safe losses where their Boolean contracts permit it. Concurrent tests use separate connections/tasks and prove one winner for duplicate PersonnelNo, ShiftProfile credential replacement, management singleton initialization, replay reservation, and same-RequestId atomic execution. WAL and the connection factory’s timeout behavior provide SQLite-level serialization/crash recovery; no in-memory production lock is introduced.

## Failure injection and rollback evidence

The atomic adapter has an injected seam with five deterministic points: after replay check, after consume insert, after setting mutation, after receipt insert, and immediately before commit. Tests force each failure and verify all three durable outcomes remain at their original state: ESD stays unchanged, no consumed row remains, and no receipt remains.

Callback failure also rolls back. Constraint replay is distinguished from general storage failure. SQLite busy/locked codes fail closed as StoreFailed. A deterministic long-lived external lock test was not added because it would depend on timing/default timeout; the explicit code path is covered structurally while concurrency races exercise real multiple connections.

SQLite atomicity guarantees apply to the database transaction only. There is no claim that filesystem backups, UI state, or an arbitrary external callback participates in the transaction.

## Migration strategy

The migration participates in the existing version/checksum/history framework and is rerunnable: after successful application, a second run applies nothing and validates recorded checksum. Empty temporary database and representative legacy temporary database tests both pass. The legacy fixture table/data remains untouched.

The migration assumes framework current version 0 and advances to 1. Other draft migrations also originate at version 0 and are intentionally not composed together today. Before production activation, the project requires an approved unified migration sequence/version allocation and schema compatibility audit. This draft must not be registered as-is alongside another 0→1 draft.

Rollback after a committed migration is intentionally not implemented because SQLite table removal would be destructive and the project rules prohibit destructive schema operations without approval. Rollback before commit is automatic. Production deployment must use verified backup, preflight, and forward-repair procedures.

## Test results

The full solution contains **264 passing tests, 0 failures, 0 skipped** after Phase 7.7. New coverage includes:

- empty/legacy migration, preservation, checksum/idempotent rerun;
- target table/schema absence of RBAC and local Support concepts;
- ShiftProfile create/read/update, stable ID, optimistic revision, and normalized PersonnelNo;
- duplicate PersonnelNo constraint and concurrent race;
- internal credential current uniqueness, history, and concurrent replacement;
- management singleton initialization/revision/concurrency;
- DeviceId singleton stability;
- public-key insertion uniqueness, retirement, historical lookup, and absence of private-key storage;
- audit allow-list and append-only triggers;
- durable replay reservation and concurrent one-winner behavior;
- exact decimal ESD mutation, replay rejection, one successful receipt, and concurrent same-request execution;
- rollback at all five injected failure points;
- immutable consumed/receipt evidence;
- no Unit-specific ESD column and unchanged finalized snapshot evidence.

Final build succeeds with zero errors and the same six pre-existing NU1701 compatibility warnings involving transitive OpenTK/SkiaSharp Windows Forms assets. No dependency changed.

## Remaining production activation prerequisites

1. Allocate this migration in a single approved production migration chain with all other draft schemas; resolve the current competing version-0 drafts.
2. Design a non-destructive reconciliation from legacy app_settings ESD storage to SecurityDeploymentSettings, including precision/range, initial Revision, and authoritative cutover timing.
3. Decide deployment-wide versus per-Station PersonnelNo uniqueness if a database can ever host more than one Station; current approved scope is `(StationId, normalized PersonnelNo)`.
4. Select/approve password KDF algorithm, parameter schema, minimum salt/verifier lengths, upgrade policy, secure memory handling, lockout, and recovery processes.
5. Add a production audit boundary around every repository mutation; current repositories expose auditable inputs but are deliberately uncomposed.
6. Approve trusted-key provisioning integrity, key material validation, rotation/revocation, backup, and disaster recovery.
7. Add production preflight, backup/restore, integrity checks, migration rehearsal, disk-full/crash tests, and operational recovery runbooks.
8. Define SQLite busy retry policy and test it under production-representative locking/load.
9. Security-review the transaction callback contract and ensure production uses only transaction-safe behavior.
10. Perform a separate integration phase for login/protected UI adapters behind unchanged-by-default feature gates.

## Verification confirmation

The complete solution builds and all tests pass. `git diff --check` passes. `Program.cs` retains its pre-phase SHA-256 hash. No Phase 7.7 diff exists under production `UI/Forms` or `UI/Startup`; feature configuration is unchanged. Migration executions occurred only on generated temporary test paths.

Schema/source inspection confirms no RBAC table/class, local Support role/profile/login, vendor private-key storage, plaintext password, or Unit-specific ESD column. ShiftProfile remains the only normal identity. The schema contains one management credential table/model with singleton-current enforcement. Ordinary `FinalizeReport` remains in `OperationalAction`; Reopen remains management-protected. The atomic adapter never references finalized snapshot tables, and one RequestId cannot execute a successful ESD mutation twice.
