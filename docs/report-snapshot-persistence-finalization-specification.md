# Snapshot Persistence and Atomic Finalization Design Specification

## 1. Document status and scope

This document defines the Phase 5.8 target persistence boundaries and atomic finalization behavior for immutable report snapshots. It builds on the Phase 5.6 architecture and the Phase 5.7 `FinalizedReportSnapshot`, finalization validation, and pure snapshot-factory contracts.

This phase is specification-only. It creates no C# implementation, database schema, table, index, migration, SQLite command, repository, transaction implementation, UI, exporter, dependency registration, or production route. Interface names in this document are conceptual contracts for a later approved phase.

The design remains limited to the current Rasht and Ramsar production scope. It preserves legacy Reporting unchanged during coexistence.

## 2. Design invariants

1. A snapshot becomes durably finalized only when its immutable content, period lock, and required audit evidence commit atomically.
2. A persisted snapshot is append-only domain truth. It is never updated to reflect later operational data or configuration.
3. Exactly one effective target snapshot may own a finalized Station/period lock at a time.
4. No committed lock may point to a missing, invalid, or uncommitted snapshot.
5. No committed finalized snapshot may exist without the lock/audit outcome intended by its finalization request.
6. Expected source revision, lock revision, lineage, and idempotency preconditions are checked again inside the atomic boundary.
7. Duplicate delivery of the same request is safe and returns the original committed result.
8. Reuse of an idempotency identity for different content is a conflict, never a second finalization.
9. Finalized reads use snapshot persistence only and never fill content from operational sources.
10. Unknown versions, checksum formats, lock ownership, or migration provenance fail closed.

## 3. Persistence architecture

The target direction is:

```text
Validated finalization request
       + candidate snapshot
       + expected revisions
                  |
                  v
   Atomic finalization coordinator
      |           |           |
      v           v           v
 Snapshot      Period       Finalization
  store       lock store       audit
      \           |           /
       \----------+----------/
          one atomic commit
                  |
                  v
        committed result receipt
```

The three logical stores participate in one persistence unit. They may eventually share one physical database and transaction, but that implementation choice is not made here. If they cannot provide one atomic commit, the design is not eligible for production finalization.

The pure Phase 5.7 factory creates a candidate domain snapshot. Its current `Succeeded` result means candidate creation only. A future persistence coordinator must introduce a distinct committed result so callers cannot confuse in-memory construction with durable finalization.

## 4. Snapshot persistence boundary

### 4.1 `IReportSnapshotStore` responsibility

`IReportSnapshotStore` is the persistence port for immutable target snapshots. Its responsibility is to:

- insert one fully constructed and integrity-ready `FinalizedReportSnapshot` candidate;
- retrieve a snapshot by stable `SnapshotId`;
- locate immutable lineage members by canonical Station/period identity when required for validation or audit;
- detect identity, sequence, and payload conflicts;
- preserve complete content, evidence, versions, checksum metadata, ordering, and lineage without reinterpretation;
- participate in the caller-controlled atomic finalization boundary;
- return storage-neutral structured outcomes rather than provider exceptions as business results.

The store persists exactly the supplied snapshot contract. It does not decide completeness, calculate sections, select Runtime configuration, normalize Events, or manufacture missing evidence.

### 4.2 Conceptual operations

The minimum conceptual operations are:

```text
TryInsert(candidateSnapshot, persistenceContext)
GetById(snapshotId, readContext)
GetLineage(stationId, periodStartMinute, periodEndMinute, readContext)
Exists(snapshotId, readContext)
```

`TryInsert` is insert-only. Its outcomes distinguish:

- `Inserted`;
- `AlreadyExistsSameContent` for verified idempotent replay;
- `SnapshotIdConflict` when the same identity represents different content;
- `LineageSequenceConflict`;
- `IntegrityRejected`;
- `InfrastructureFailed`.

The future exact method signatures may be synchronous or asynchronous according to project conventions. Cancellation must never leave a partially committed business operation.

### 4.3 Allowed operations

The snapshot store may:

- serialize and deserialize an approved snapshot format;
- enforce uniqueness and integrity constraints;
- compare an approved request/content fingerprint for idempotency;
- verify checksum metadata and supported format versions;
- expose immutable snapshot reads;
- support append-only superseding snapshots;
- provide storage revision evidence required by the coordinator;
- perform administrative archive/restore only under a separately approved retention policy that preserves identity and integrity.

### 4.4 Forbidden behavior

The snapshot store must not:

- update or delete finalized snapshot domain content;
- perform an implicit upsert that overwrites an existing identity;
- recalculate Reporting, Runtime, Event summaries, completeness, or checksums from live data;
- query operational hourly, daily, Event, Runtime, Settings, or profile sources;
- silently upgrade a snapshot to a new format/version during ordinary reads;
- replace missing snapshot fields with current application defaults;
- assign or transition period-lock ownership on its own;
- commit independently from the lock and required finalization audit;
- expose persistence entities, connections, commands, or provider-specific exceptions through application/domain contracts;
- accept an unsupported integrity format as valid;
- treat a legacy report row as a target snapshot without an approved conversion record.

## 5. Period lock boundary

### 5.1 `IReportPeriodLockStore` responsibility

`IReportPeriodLockStore` is the target authority for finalization ownership of a canonical report period. It records whether a Station/period is open to target finalization and, when finalized, which immutable snapshot is effective.

The lock identity includes at least:

- canonical `StationId`;
- `PeriodStartMinute` and `PeriodEndMinute` using the approved half-open model;
- `PeriodKind` where required to disambiguate approved report scope;
- canonical Unit-set/profile identity if lock ownership depends on it;
- lock generation/revision token;
- effective `SnapshotId` and snapshot sequence when finalized;
- finalization identity and policy version;
- committed actor/timestamp evidence.

Display labels and Persian date text are not lock keys.

### 5.2 Lock states

The conceptual states are:

| State | Meaning |
|---|---|
| `Open` | No committed target snapshot owns the period. |
| `Finalizing` | Transaction-local intent while atomic finalization is in progress. It must not survive rollback or be externally observable as a durable business state. |
| `Finalized` | One committed effective target snapshot owns the period. |
| `IntegrityBlocked` | Optional future safety state when lock/snapshot integrity cannot be proven. It permits no editing or silent fallback and requires approved recovery. |

Ordinary Phase 5 finalization supports only `Open -> Finalized` as an externally visible transition. `Finalizing` exists to explain transaction behavior, not to authorize a durable intermediate row.

There is no ordinary `Finalized -> Open` transition. A future approved correction performs `Finalized(old snapshot) -> Finalized(new superseding snapshot)` atomically while preserving both snapshots. Reopen/unlock remains outside this specification.

### 5.3 Conceptual operations

```text
Read(periodIdentity, context)
AssertOpen(periodIdentity, expectedLockRevision, context)
TryFinalize(periodIdentity, snapshotIdentity, expectedLockRevision, context)
TrySupersede(periodIdentity, oldSnapshotId, newSnapshotId,
             expectedLockRevision, context)
```

`TrySupersede` is unavailable until correction authorization and policy are separately approved. Defining its concurrency semantics here does not activate it.

### 5.4 Ownership rules

- A finalized lock owns exactly one effective `SnapshotId`.
- The snapshot identity must carry the same Station, period, Unit set, and lineage expected by the lock.
- Sequence 1 may acquire only an open period.
- Sequence greater than 1 must name the currently effective snapshot as `SupersedesSnapshotId` and use its immediate next sequence.
- A lock transition uses compare-and-swap semantics against the expected lock revision/effective snapshot.
- A stale caller cannot replace the winner of another finalization or correction.
- Lock ownership is target-specific during coexistence; it must not silently reinterpret or modify a legacy lock.
- Rasht and Ramsar locks are isolated by canonical Station identity.
- Overlapping report kinds require a separately approved ownership policy. A monthly lock must not automatically imply half-year/year ownership, or vice versa, without an explicit rule.

### 5.5 Forbidden lock behavior

The lock store must not create a finalized lock before the corresponding snapshot is accepted in the same atomic context, unlock because a UI requests editing, infer ownership from display text, overwrite another effective snapshot, or commit independently from required audit evidence.

## 6. Atomic finalization coordinator

### 6.1 Responsibility

A future application service, conceptually `IAtomicReportFinalizationService`, coordinates pure validation/factory work with the snapshot store, lock store, source-freshness boundary, and audit sink. It owns orchestration only. It does not calculate report sections or contain provider-specific SQL.

A future unit-of-work abstraction may provide one atomic context to all participating ports. This document intentionally does not name a database transaction class or select a provider.

### 6.2 Inputs

The coordinator receives:

- the original `ReportFinalizationRequest`;
- a complete projection and pure validation result;
- the candidate `FinalizedReportSnapshot` after checksum completion;
- stable `FinalizationId` used as idempotency key;
- expected source revision;
- expected period-lock revision/state;
- caller-supplied actor and finalization timestamp;
- request/content fingerprint produced under a versioned canonicalization policy.

The candidate must carry a calculated checksum before persistence. The Phase 5.7 pending checksum placeholder is not persistable as finalized truth.

### 6.3 Workflow

#### Step 1 — Preflight validation outside the atomic boundary

Run the pure finalization validator and snapshot factory. Verify supported snapshot/integrity/finalization versions and calculate the candidate checksum/fingerprint. Reject incomplete, version-invalid, identity-invalid, or malformed requests before opening a write boundary.

Preflight improves efficiency but is not sufficient for concurrency safety.

#### Step 2 — Begin atomic context

Open one atomic persistence context shared by source-freshness verification, idempotency receipt lookup, snapshot insertion, lock transition, and required audit append. No participating store may commit independently.

#### Step 3 — Resolve idempotency

Look up `FinalizationId` and its immutable request fingerprint inside the atomic context:

- if an identical request already committed, return its stored committed result after verifying referenced snapshot/lock integrity;
- if the identity exists with different content or target, reject `IdempotencyConflict`;
- if an earlier attempt is uncommitted/rolled back, proceed as a retry;
- if no receipt exists, continue.

#### Step 4 — Recheck preconditions

Within the same atomic view:

1. verify current source revision equals the projection/request revision;
2. verify lock state and revision equal expected values;
3. verify no conflicting snapshot identity or lineage sequence exists;
4. verify the requested `SnapshotId` is unused, or contains exactly the same canonical payload for an idempotent replay;
5. verify the actor/authorization decision remains acceptable under the supplied policy evidence;
6. verify candidate checksum and structural invariants;
7. for correction, verify the effective snapshot and immediate lineage sequence.

Any failed precondition ends the atomic context without writes.

#### Step 5 — Persist snapshot candidate

Insert the complete immutable snapshot. An existing identical candidate is acceptable only as part of proven idempotent recovery; an existing different candidate is a hard conflict. Do not expose the candidate as finalized before commit.

#### Step 6 — Transition period lock

Use compare-and-swap semantics to transition the expected lock to the candidate snapshot. Failure due to a changed lock revision or effective snapshot is `FinalizationConflict`, not a retry that overwrites the winner.

#### Step 7 — Append audit evidence and committed-result receipt

Append finalization audit evidence containing request/finalization identity, actor, Station/period, snapshot identity/sequence, prior effective snapshot when applicable, source revision, versions, checksum/fingerprint, lock transition, and outcome. Store the idempotency receipt needed to reproduce the committed result.

Audit content must be deterministic, safe to log, and contain no passwords, secrets, authorization tokens, or connection information.

#### Step 8 — Commit

Commit snapshot, lock, audit, and idempotency receipt together. Only after commit may the coordinator return `CommittedSucceeded`. If commit outcome is uncertain because of an infrastructure interruption, the caller must resolve by `FinalizationId`; it must not submit a new identity blindly.

### 6.4 Commit guarantees

After a successful commit:

- the snapshot is readable by `SnapshotId`;
- the period lock names that snapshot as effective;
- required audit evidence exists;
- the idempotency receipt reproduces the result;
- all identities, revisions, and checksums agree.

After a rejected or rolled-back attempt, none of those new business effects is observable.

## 7. Idempotency model

### 7.1 Idempotency identity

`FinalizationId` is the primary idempotency key. It is generated before the first attempt and reused for retries of the same logical request. It is not regenerated merely because a timeout or process restart obscures the first result.

The stored request fingerprint binds at least:

- `FinalizationId` and `SnapshotId`;
- Station, period, period kind, Unit set, and sequence;
- projection/report identity;
- expected source revision;
- snapshot checksum and integrity format;
- complete version set;
- actor identity and finalization policy version;
- superseded snapshot identity where applicable.

Whether `FinalizedAt` participates exactly in the fingerprint must be decided with retry semantics; the recommended rule is that the original caller-supplied timestamp is reused and therefore included.

### 7.2 Duplicate finalize request

If the same `FinalizationId` and fingerprint already committed, the service returns the original committed result receipt. It does not insert another snapshot, increment sequence, append a second success audit, or touch the lock.

If the same `FinalizationId` is supplied with a different fingerprint, return `IdempotencyConflict`. Never guess which request was intended and never reuse the old result for different content.

If a different `FinalizationId` targets an already finalized period with identical content, ordinary finalization returns `AlreadyFinalized` referencing the effective snapshot according to an approved disclosure policy. It does not create a duplicate snapshot. This is conflict handling, not idempotent replay.

### 7.3 Retry behavior

- Business rejections such as incomplete, version mismatch, source change, or identity mismatch are not retried unchanged.
- Concurrency conflicts require re-reading the effective lock/snapshot and deciding whether the goal is already satisfied; automatic overwrite is forbidden.
- Transient infrastructure failures may retry with the same `FinalizationId`, fingerprint, snapshot identity, and caller-supplied timestamps.
- An uncertain commit is resolved by querying the idempotency receipt and validating snapshot/lock integrity.
- Retries must be bounded by application policy and cancellation, but cancellation cannot interrupt an already committing atomic operation into a partial state.

### 7.4 Result reuse

The persisted committed receipt contains enough data to reconstruct the success outcome without operational reads: snapshot/finalization identity, Station/period, sequence, checksum, source revision, lock identity/revision, actor/timestamp evidence, and superseded identity where applicable.

Result reuse verifies that the receipt, snapshot, and lock still agree. A broken reference or checksum mismatch returns integrity failure, not a cached success.

## 8. Concurrency model

### 8.1 Simultaneous initial finalization

Two requests may observe an open period before either writes. Both may pass pure preflight. Inside the atomic boundary, only one may successfully compare-and-swap the open lock revision and commit sequence 1.

The winner commits its snapshot, lock, audit, and receipt. The loser rolls back its candidate effects and returns `FinalizationConflict` or `AlreadyFinalized` after reading the winner. It must not overwrite the lock, renumber its snapshot automatically, or treat itself as a correction.

### 8.2 Simultaneous retry of the same request

Concurrent deliveries with the same `FinalizationId` and fingerprint converge on one committed result. One transaction may perform the insert; the other returns that receipt after conflict resolution. At most one success audit and one snapshot/lock transition are committed.

### 8.3 Simultaneous corrections

Future corrections must supply the currently effective `SnapshotId`, its lock revision, and the next sequence. If two corrections target the same old snapshot, only one can transition ownership. The loser is stale and must regenerate/revalidate against the new effective snapshot. It cannot become sequence `n+2` automatically because its business evidence may no longer be appropriate.

### 8.4 Conflict handling

Concurrency conflicts are structured outcomes containing safe current-state evidence where permitted:

- expected and observed lock revision;
- expected and effective snapshot identity;
- expected and observed lineage sequence;
- whether the same finalization identity already committed.

Provider-specific constraint exceptions are translated at the infrastructure boundary. Blind retries are prohibited for identity, lineage, or lock conflicts.

### 8.5 Effective snapshot rules

- An open period has no effective target snapshot.
- A finalized period has exactly one effective target snapshot named by its committed lock.
- A superseded snapshot remains immutable and readable by identity but is not effective.
- The effective snapshot must be the highest committed valid sequence in the lock-selected lineage, but readers trust the lock reference rather than independently guessing by maximum sequence.
- A snapshot without a matching committed lock is not effective, even if storage remnants are discovered during integrity diagnostics.
- A lock referencing an invalid/missing snapshot is an integrity incident, never permission to read operational data or fall back silently.

The required isolation level is the behavioral equivalent of serializing conflicting finalizations for one canonical period. The exact database isolation mode is deferred.

## 9. Rollback and failure behavior

### 9.1 General rule

Every failure before commit leaves the externally visible business state as it was before the attempt. Rollback covers snapshot insert, lock transition, required audit evidence, and idempotency receipt.

### 9.2 Snapshot failure

If serialization, checksum verification, identity constraint, or snapshot insertion fails:

- do not transition the lock;
- do not append a success audit or committed receipt;
- rollback any transaction-local snapshot artifact;
- return `IntegrityRejected`, `PersistenceConflict`, or `InfrastructureFailed` as appropriate;
- preserve the original candidate and request evidence for safe diagnostics outside the committed business payload.

### 9.3 Lock failure

If snapshot insertion succeeds transaction-locally but the lock compare-and-swap or ownership validation fails:

- rollback the inserted snapshot;
- do not append success audit/receipt evidence;
- retain the prior lock/effective snapshot unchanged;
- return a structured conflict or infrastructure failure;
- never leave an orphan candidate visible as finalized.

If an implementation physically cannot remove an uncommitted insert on rollback, it does not meet this design.

### 9.4 Audit failure

Required finalization audit evidence is part of the atomic business operation. If audit append or idempotency receipt creation fails:

- rollback snapshot insertion and lock transition;
- return `InfrastructureFailed`;
- do not report success based only on snapshot/lock writes.

Optional diagnostic telemetry outside the authoritative audit record may fail independently, but it must never be confused with required finalization audit evidence.

### 9.5 Commit uncertainty

When connection/process failure makes commit outcome unknown, return an indeterminate infrastructure outcome rather than claiming rollback or success. Recovery resolves `FinalizationId` through the committed receipt, then cross-checks snapshot, lock, audit, and checksum. A new request identity must not be created until resolution completes.

### 9.6 Recovery invariants

Startup or maintenance integrity checks may detect inconsistent remnants caused by corruption or unsupported external modification. They must quarantine/report the condition and follow an approved recovery procedure. They must not synthesize snapshots, delete evidence, rewrite checksums, or select an effective snapshot heuristically.

## 10. Finalized read boundary

### 10.1 Conceptual reader

A future `IFinalizedReportReader` provides:

```text
GetBySnapshotId(snapshotId)
GetEffective(stationId, periodStartMinute, periodEndMinute)
GetLineage(stationId, periodStartMinute, periodEndMinute)
```

`GetEffective` resolves the lock, loads the exact referenced snapshot, verifies identity/checksum/version support, and returns a self-contained finalized read result. `GetLineage` is an audit/correction capability and does not change which snapshot is effective.

### 10.2 Snapshot-only rule

Finalized readers must use only:

- snapshot persistence;
- period-lock metadata;
- snapshot integrity/version readers;
- static, versioned presentation resources explicitly allowed by the snapshot format.

They must not read operational hourly data, daily data, Events, Runtime bases/projections, Settings, current Station profiles, current ESD configuration, or legacy report calculation services. Missing finalized content is an integrity/format failure, not a signal to query live sources.

### 10.3 Read outcomes

The reader distinguishes:

- `FoundValid`;
- `NotFound`;
- `NotFinalized`;
- `IntegrityInvalid`;
- `IntegrityUnsupported`;
- `LockSnapshotMismatch`;
- `LegacyOnly`;
- `InfrastructureFailed`.

A checksum mismatch, unsupported snapshot format, or broken lock reference never returns a partially populated snapshot. Read operations are side-effect free; they do not upgrade, repair, relock, or recalculate.

## 11. Audit evidence

The authoritative success audit includes:

- finalization/idempotency identity and request fingerprint version;
- snapshot/report identity and sequence;
- Station, period, Unit set, and period kind;
- prior/effective snapshot identity for correction;
- expected and verified source revision;
- projection calculation and caller-supplied finalization timestamps;
- actor identity and finalization policy version;
- complete version-family summary;
- checksum algorithm, integrity format, payload length, and value;
- expected/committed lock revision and transition;
- committed outcome.

Rejected attempts should be auditable according to future policy, but a rejection audit must not imply a committed snapshot or lock. Audit storage design, retention, and security classification are deferred.

## 12. Migration and coexistence

### 12.1 Legacy coexistence

Legacy Reporting, its current finalized data, locks, UI paths, and exporters remain unchanged until explicit cutover. Target stores and locks are separate logical authorities. During shadow and validation phases, target persistence is disabled or isolated from production decisions.

A resolver must know whether a period is `LegacyOnly`, `TargetFinalized`, or in a separately approved migration state. It must not prefer a target candidate merely because one exists; only a valid committed target lock establishes target authority.

Target finalization must not activate while legacy UI can independently finalize/unlock the same target-owned period without an approved coordination rule. Ownership must be unambiguous at cutover.

### 12.2 Future persistence adoption stages

1. Approve domain serialization, canonical checksum, schema, migration, and transaction designs separately.
2. Implement stores against disposable test persistence only.
3. Prove atomicity, rollback, idempotency, and concurrency with fault injection.
4. Prove snapshot-only reads while operational repositories are unavailable.
5. Run synthetic and copied-data shadow validation for Rasht and Ramsar.
6. Classify legacy periods without inventing missing evidence.
7. Introduce feature-gated, Station-scoped target persistence with rollback at routing level.
8. Activate target authority only after explicit acceptance.

### 12.3 Legacy conversion

Legacy records lacking complete evidence, versions, checksum, or self-contained sections remain `LegacyOnly`. A future conversion must be append-only, provenance-bearing, reproducible, and explicitly versioned. It may not fabricate source revision or authoritative Event/Runtime evidence. Conversion and its schema are outside Phase 5.8.

### 12.4 Rollback of production adoption

Routing rollback must preserve every committed target snapshot, lock, receipt, checksum, and audit record. It must not delete target truth or recalculate it through legacy services. The policy for displaying a target-finalized period while target write routing is disabled must be approved before activation.

## 13. Verification requirements

### 13.1 Atomicity tests

- snapshot, lock, audit, and idempotency receipt become visible together after commit;
- no reader observes a transaction-local `Finalizing` state;
- source revision is rechecked inside the atomic boundary;
- lock revision and effective snapshot are rechecked inside the boundary;
- successful result is returned only after durable commit;
- finalized snapshot carries a calculated, supported checksum rather than a pending placeholder;
- snapshot/lock identities, periods, Units, sequences, and checksums agree after commit.

### 13.2 Rollback/fault-injection tests

- snapshot serialization/checksum/insert failure leaves lock, audit, and receipt unchanged;
- lock compare-and-swap failure rolls back snapshot and success audit;
- required audit append failure rolls back snapshot and lock;
- idempotency receipt failure rolls back every business effect;
- cancellation before commit leaves no effects;
- exception at every persistence step produces the same pre-attempt observable state;
- commit uncertainty resolves correctly by `FinalizationId`;
- recovery never fabricates or silently repairs content.

### 13.3 Idempotency tests

- identical request replay returns the original committed result;
- repeated replay creates no additional snapshot, lock transition, sequence, or success audit;
- same `FinalizationId` with different fingerprint is rejected;
- retry after confirmed rollback can commit once using the same identity;
- concurrent identical retries converge on one result;
- result reuse detects missing snapshot, lock mismatch, or checksum corruption;
- a different request identity against an already finalized period does not duplicate content.

### 13.4 Concurrency tests

- two initial finalizations for one open period yield exactly one winner;
- loser effects are fully rolled back and the winner remains effective;
- simultaneous Rasht and Ramsar finalizations remain isolated;
- simultaneous finalizations for independent periods do not conflict unnecessarily;
- stale lock revision is rejected;
- two corrections from the same effective snapshot yield one winner;
- stale correction is not automatically renumbered;
- no execution yields two effective snapshots for one period;
- constraint/provider conflicts map to deterministic application outcomes.

### 13.5 Finalized read tests

- read by snapshot identity returns exact domain content/evidence/versions;
- effective read follows the committed lock, not maximum sequence guessing;
- finalized read succeeds with all operational source adapters unavailable;
- no hourly, daily, Event, Runtime, Settings, profile, or legacy calculation call occurs;
- missing/invalid checksum, unsupported format, and broken lock reference fail closed;
- superseded snapshots remain readable by identity;
- culture/timezone changes do not alter authoritative values or ordering.

### 13.6 Coexistence and migration tests

- legacy-only periods remain routed to legacy reads without target mutation;
- target-finalized periods never fall back silently to live legacy calculation;
- feature-gate rollback preserves target records;
- unverified legacy records cannot be labeled target snapshots;
- Station ownership/routing prevents legacy and target writers from finalizing the same target-owned period.

## 14. Deferred implementation decisions

This specification intentionally does not decide:

- physical schema, table count, keys, constraints, indexes, or serialization layout;
- SQLite transaction mode, isolation syntax, busy timeout, or retry configuration;
- repository classes or infrastructure namespaces;
- checksum canonical encoding implementation;
- finalization/request fingerprint algorithm and encoding;
- source revision implementation;
- audit storage and retention;
- exact lock granularity for overlapping monthly/half-year/year periods;
- correction authorization and reopen policy;
- archival/deletion policy;
- UI commands and lock presentation;
- exporter behavior;
- dependency injection and production activation.

Each requires separate approval and implementation verification.

## 15. Phase 5.8 verification and approval gate

Phase 5.8 adds only `docs/report-snapshot-persistence-finalization-specification.md`.

- No production C# file is created or modified.
- No database table, schema definition, migration, SQLite command, repository, or transaction implementation is added.
- No legacy Reporting, UI, exporter, startup, or production-registration file is modified.
- No production finalization or lock behavior changes.

This specification authorizes no persistence implementation. A future phase may implement isolated ports and disposable integration fixtures only when explicitly approved. Physical schema/migration, production wiring, UI, exporter adoption, and legacy cutover remain separate gates.
