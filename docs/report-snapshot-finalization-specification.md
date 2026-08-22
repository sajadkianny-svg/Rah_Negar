# Report Snapshot and Finalization Architecture Specification

## 1. Document status and scope

This document defines the Phase 5.6 target architecture and contracts for immutable report snapshots and report finalization. It refines the snapshot and finalization principles approved in `reporting-domain-foundation-specification.md` and consumes the isolated projection boundary created in Phases 5.3 through 5.5.

This phase is specification-only. It creates no runtime C# implementation, database table, repository, transaction, production registration, UI lock control, exporter, or production cutover. Names shown as contracts are conceptual target names; a later implementation phase must approve their exact namespaces and representations.

The target applies only to the current Rasht and Ramsar scope. It does not generalize the application into a universal Station platform.

## 2. Architectural principles

1. A `ReportProjection` is a reproducible open calculation, but it is not finalized authority.
2. A `FinalizedReportSnapshot` is the sole target authority for a finalized report.
3. Finalization accepts only a fresh, complete, non-rejected projection with compatible required versions.
4. Snapshot capture and the corresponding period-lock transition form one atomic business operation.
5. A finalized reader uses the snapshot only. It performs no operational hourly, daily, Event, Runtime, Settings, or profile read.
6. Finalized Runtime values remain authoritative integral minutes. Presentation hours never replace them.
7. Later source, Event, Runtime, Baseline, configuration, profile, policy, or calendar changes never rewrite an existing snapshot.
8. A correction creates a new, independently identified snapshot version and preserves the original.
9. Unknown or missing compatibility evidence fails closed.
10. Legacy reports coexist unchanged until a separately approved migration and cutover.

## 3. Report Snapshot architecture

### 3.1 Boundary

The intended one-way flow is:

```text
Authoritative normalized sources
             |
             v
       ReportProjection
       open / reproducible
             |
       finalization gate
             |
             v
 FinalizedReportSnapshot + PeriodLock
             |
             v
 snapshot-only readers and future renderers
```

The snapshot is a self-contained business record, not a cache and not a pointer to live operational rows. References to source identities are evidence; they are not instructions to reload or recalculate the snapshot.

### 3.2 Snapshot identity

The conceptual `ReportSnapshotIdentity` contains:

| Field | Meaning |
|---|---|
| `SnapshotId` | Globally stable identity of this immutable snapshot record. |
| `ReportId` | Identity of the source projection/finalization attempt. |
| `StationId` | Canonical Rasht or Ramsar Station identity. |
| `PeriodStartMinute` | Inclusive canonical local-minute boundary. |
| `PeriodEndMinute` | Exclusive canonical local-minute boundary. |
| `PeriodKind` | Monthly, half-year, yearly, or approved arbitrary range. |
| `UnitIds` | Canonically ordered configured Unit identities captured at finalization. |
| `SnapshotSequence` | Monotonically increasing correction/version sequence within the same Station and period identity. |
| `SupersedesSnapshotId` | Optional identity of the immediately superseded snapshot; absent for the original. |

`SnapshotId` must not be derived only from Station and period because corrected versions must coexist. `SnapshotSequence` is business lineage, not a mutable revision counter. Once assigned, neither identity nor sequence changes.

Only one snapshot in a lineage may be the current effective target snapshot. Earlier snapshots remain addressable by identity and retain their original integrity and finalization evidence. The mechanism used to select the effective snapshot is a future persistence design decision and must not overwrite the old snapshot.

### 3.3 Snapshot content

`FinalizedReportSnapshot` captures the complete `ReportProjection` domain content needed by every finalized view and future export:

- captured report identity and source mode at generation;
- projection status and completeness result at the accepted gate;
- operational summaries with parameter identity, label, unit, aggregation, value, contributing count, and extreme evidence;
- daily summaries with sum, count, and missing-day evidence;
- authoritative Runtime summaries per Unit, including Physical Runtime, ESD Adjustment, Adjusted Runtime, Runtime After OH, Longest Run, Service Day Count, and Final State;
- authoritative Event summaries and stable in-period Event log;
- service-day/service-combination content supported by the projection contract;
- extreme-date content;
- deterministic warnings and the accepted absence of blocking reasons;
- any future mandatory report section introduced by a versioned snapshot-format change.

Collections are stored in their canonical order and exposed as immutable collections. The snapshot must not contain database connections, repositories, ORM entities, UI controls, lazy loaders, callbacks, or mutable collection references.

If a future display stores formatted decimal hours or localized text, those values are derived presentation evidence. Integral minutes and captured domain values remain authoritative. A renderer must not feed formatted values back into calculations.

### 3.4 Snapshot evidence

The snapshot preserves, without reinterpretation:

- consistent source revision/read identity;
- hourly source identity/revision and record count;
- daily source identity/revision and record count;
- Event Chain identity and version per Unit;
- Event validation/policy evidence;
- Runtime Projection identity per Unit;
- Runtime input, Baseline, configuration, policy, and calculation evidence per Unit;
- Station profile/parameter-registry identity;
- `DataStartMinute` used for responsibility and completeness;
- calendar/time-model identity;
- deterministic ordering convention;
- complete dimension-by-dimension `ReportCompletenessResult`, including issues even though ordinary finalization requires all dimensions to be Complete;
- projection calculation timestamp;
- finalization freshness evidence;
- checksum metadata and lock-transition evidence.

Evidence is copied into the snapshot. A source identity may support later audit, but snapshot validity does not depend on the source still existing.

### 3.5 Snapshot versions

The snapshot captures the complete `ReportVersionSet`:

- `ReportCalculationVersion`;
- `ReportPolicyVersion`;
- `ReportProfileVersion`;
- `SnapshotFormatVersion`;
- `EventChainVersion` per Unit;
- `EventPolicyVersion`;
- `RuntimeCalculationVersion`;
- `RuntimePolicyVersion`;
- `RuntimeBaselineVersion` per Unit;
- `RuntimeConfigurationVersion` per Unit;
- `CalendarPolicyVersion`.

It additionally records `SnapshotIntegrityVersion`, which identifies canonicalization and checksum semantics, and `FinalizationPolicyVersion`, which identifies the validation/locking rules applied. These versions are distinct. A timestamp is never a substitute for a version.

Missing required versions reject finalization. Compatibility defaults to exact equality for operations that combine snapshots. A later policy may define explicit compatible version sets, but unknown compatibility is never silently accepted.

### 3.6 Finalization metadata

Conceptual `ReportFinalizationMetadata` contains:

- `FinalizationId` for the attempt that succeeded;
- caller-supplied `FinalizedAt` timestamp;
- authorized actor identity and actor type;
- Station and period identity repeated as transaction evidence;
- accepted projection identity and calculation timestamp;
- source revision verified at the gate;
- `FinalizationPolicyVersion`;
- finalization reason for an original snapshot, or approved correction reason for a superseding snapshot;
- resulting lock identity and lock state;
- optional correlation/audit identity that contains no secret material.

Actor authorization is supplied by an approved security boundary. This specification does not define credentials, roles, cryptographic keys, or a bypass mechanism.

## 4. Finalization contracts

### 4.1 Finalization request

The conceptual `FinalizeReportRequest` contains:

```text
FinalizationId
Projection
ExpectedSourceRevision
ExpectedStationId
ExpectedPeriodStartMinute / ExpectedPeriodEndMinute
RequestedSnapshotSequence
SupersedesSnapshotId (corrections only)
ActorIdentity
FinalizedAt (caller supplied)
FinalizationPolicyVersion
```

The request carries a complete projection, not a UI cache key. It does not contain a database connection or lock-control reference. `ExpectedSourceRevision` enables freshness verification immediately before capture. `FinalizedAt` is supplied once; the workflow does not read the clock in multiple layers.

Ordinary initial finalization requires no `SupersedesSnapshotId` and sequence 1. A correction request requires a separately authorized workflow, an existing lineage, the prior effective snapshot identity, and the next valid sequence.

### 4.2 Freshness verifier boundary

A future application contract may be represented as `IReportSourceFreshnessVerifier`. It accepts only the Report Identity and expected source revision and returns one of:

- `Unchanged` with verified revision evidence;
- `Changed` with current revision identity;
- `Unavailable` with structured evidence.

This boundary performs no repair and returns no operational records. Its implementation and transaction strategy are deferred. Finalization treats `Changed` and `Unavailable` as rejection outcomes.

### 4.3 Snapshot factory boundary

A future pure `IReportSnapshotFactory` accepts an already validated finalization request plus verified freshness evidence and produces an immutable candidate snapshot. It:

- copies all projection content, evidence, and versions;
- adds snapshot identity, lineage, finalization, integrity, and lock-intent metadata;
- establishes canonical ordering;
- creates the integrity payload and checksum;
- performs no IO and does not acquire a lock.

It must not recalculate Reporting, Events, or Runtime and must not read current configuration or the clock.

### 4.4 Finalization coordinator boundary

A future `IReportFinalizationService` coordinates validation, freshness verification, candidate creation, snapshot persistence, and lock transition. Persistence remains behind separately approved ports. This specification does not authorize their implementation.

## 5. Finalization workflow

### Step 1 — Receive and normalize request

Validate required request identifiers, actor evidence, caller-supplied timestamp, Station, Unit set, half-open period, source revision, requested lineage, and policy version. Reject malformed requests without writing a snapshot or lock.

### Step 2 — Apply the validation gate

The gate requires all of the following:

1. projection status is `Complete`;
2. `ReportCompletenessResult.State` is `Complete` and `IsFinalizationEligible` is true;
3. no projection blocking reason exists;
4. projection Station, period, Unit set, and source mode match the request;
5. every mandatory section required by `SnapshotFormatVersion` is present;
6. all required version families are nonblank and internally aligned;
7. Runtime component invariants hold and authoritative minute values are retained;
8. Event and Runtime evidence exists exactly once for every configured Unit;
9. deterministic ordering invariants hold;
10. initial/correction lineage rules are satisfied;
11. the period is not already locked by an incompatible active snapshot.

There is no implicit completeness override. `Incomplete`, `Invalid`, and `Unavailable` all reject ordinary finalization. Any future override requires a separately approved policy and contract amendment.

### Step 3 — Verify source freshness

Within the future consistent transaction boundary, compare the request/projection source revision with current source freshness evidence. A mismatch means operational evidence changed after projection generation. Return `SourceChangedRejected`; do not silently regenerate, finalize stale content, or update the request.

If freshness cannot be verified, fail closed. The caller may generate a new projection and submit a new finalization request.

### Step 4 — Capture candidate snapshot

Copy projection identity, every required section, completeness, evidence, versions, calculation timestamp, finalization metadata, lineage, and lock intent into an immutable candidate. Canonicalize the integrity payload and compute its checksum. Validate the candidate before persistence.

### Step 5 — Persist snapshot and transition lock atomically

A future persistence transaction must perform these inseparable actions:

1. assert freshness/lock preconditions still hold;
2. insert the immutable snapshot as a new record;
3. create or transition the period lock to identify the effective snapshot;
4. append finalization audit evidence;
5. commit all actions together.

No observer may see a lock without its valid snapshot or a finalized snapshot without its intended lock. On any failure, rollback the whole operation. The operation is insert/append oriented; it never updates snapshot domain content.

### Step 6 — Return finalization result

After a successful commit, return snapshot identity, finalization identity, checksum metadata, and lock evidence. Do not return live operational entities. On rejection, return structured deterministic failures and no snapshot identity implying success.

## 6. Lock state model

The conceptual period-lock states are:

```text
Open -> Finalizing -> Finalized
```

`Finalizing` is a transactional/internal state and must not remain observable after rollback or process failure. The committed target state is `Finalized`, tied to one effective `SnapshotId`.

For a future approved correction, the old snapshot remains immutable while the effective lineage pointer transitions atomically to the new snapshot. This does not mean the operational period is casually unlocked. Direct `Finalized -> Open` behavior is forbidden unless a future, separately approved reopen policy defines authorization, audit, reconciliation, and failure semantics.

## 7. Immutable rules

### 7.1 Forbidden changes

After successful finalization, the following are forbidden:

- updating or deleting snapshot identity, report identity, Station, period, Unit set, sequence, or lineage;
- modifying any report section, count, ordering, warning, or completeness result;
- recalculating or replacing Runtime values after Events, ESD configuration, Baseline, or Runtime policy changes;
- modifying Event content or versions after Event corrections;
- replacing evidence, source revisions, calculation/finalization timestamps, actor identity, versions, checksum, or audit linkage;
- mutating a snapshot to adopt a new format version;
- using current profile labels, Settings, policies, or calendar rules to reinterpret stored content;
- deleting the old snapshot when a correction is created;
- changing a lock independently from its snapshot lineage;
- reading operational data to fill missing finalized content at view/export time.

Database administration, backup, and disaster recovery must preserve these business invariants. A technically possible row update is not an allowed domain operation.

### 7.2 Allowed operations

Allowed operations are deliberately narrow:

- read a snapshot by stable identity;
- read the current effective snapshot for a finalized Station/period through snapshot/lock metadata;
- verify checksum and structural integrity;
- reproduce domain content from the stored snapshot alone;
- render a snapshot through a future read-only exporter;
- compare snapshots without modifying either;
- aggregate explicitly compatible finalized snapshots under approved rules;
- append audit/verification observations without changing the snapshot payload;
- create a separately identified superseding snapshot through the correction workflow;
- archive or restore snapshots only under a future retention policy that preserves identity, lineage, checksum, and availability requirements.

## 8. Integrity model

### 8.1 Canonical integrity payload

The checksum covers a canonical representation of:

- snapshot and report identity;
- ordered Unit set;
- all report sections and authoritative values;
- completeness result and issues;
- source/evidence metadata;
- all version families;
- projection and finalization metadata;
- correction lineage;
- lock intent/identity required to bind the finalized business record.

The checksum field itself is excluded from the payload. Mutable storage metadata such as database row location, indexes, access timestamps, or cache state is also excluded.

Canonicalization must define field names, field order, collection order, null representation, integer and decimal encoding, string encoding, date/time offset encoding, enum encoding, and schema version. It must be culture-independent and must never depend on runtime object hash codes or unspecified serializer defaults.

### 8.2 Checksum metadata

Conceptual `SnapshotChecksum` contains:

- `Algorithm`;
- `IntegrityFormatVersion`;
- `CanonicalPayloadLength`;
- `ChecksumValue`;
- `CalculatedAt` equal to the caller-controlled capture evidence or otherwise deterministically defined;
- optional verifier/audit identity for later verification, stored outside the immutable checksum payload when appropriate.

The initial recommended algorithm is SHA-256 because the solution already contains a SHA-256 checksum service for migration evidence. Reuse is subject to a future implementation review; this specification does not wire that service or select storage encoding. A checksum detects accidental or unauthorized content changes but is not by itself an authenticity signature. Digital signing and key custody are separate security decisions.

### 8.3 Verification behavior

Snapshot loading must verify structural requirements and may verify the checksum eagerly or at a trusted boundary. A checksum mismatch makes the snapshot `IntegrityInvalid`; it must not be silently repaired, recalculated from operational data, or treated as current finalized truth. The system must retain diagnostic evidence and follow a separately approved recovery process.

Unknown integrity format or algorithm is `IntegrityUnsupported`, not valid. Missing checksum evidence is unacceptable for target snapshots. Legacy records without target checksums remain explicitly legacy and are never mislabeled as verified target snapshots.

### 8.4 Evidence preservation and reproducibility

Reproducibility means the snapshot alone can supply all finalized domain values, labels/policies captured as required, ordering, and evidence exposed by finalized views and future exporters. It does not require access to original operational rows and does not require recalculating with current software.

Exact binary reproduction of a future PDF or workbook is not required unless an exporter specification later requires it. Domain-value reproduction must be exact. Integral Runtime minutes, decimal aggregates, counts, Event order, Persian labels, and version identities must not drift with culture, machine, current configuration, or later source changes.

## 9. Finalization result contract

The conceptual `ReportFinalizationResult` is a closed outcome contract. Exactly one outcome is returned.

### 9.1 Success

`Succeeded` contains:

- `FinalizationId`;
- `SnapshotId` and sequence;
- Station/period identity;
- committed lock identity/state;
- checksum metadata;
- source revision verified;
- finalized timestamp and actor identity;
- optional superseded snapshot identity.

Success is returned only after the snapshot and lock commit together.

### 9.2 Incomplete rejection

`IncompleteRejected` applies when projection status or completeness is not eligible. It contains deterministic dimension results and issue evidence, including affected dates, Units, fields, and sources. It creates no snapshot and no lock. Invalid and unavailable completeness may use specific subcodes but remain non-successful completeness-gate outcomes.

### 9.3 Version rejection

`VersionRejected` applies when a required version is missing, versions are internally inconsistent, the requested finalization policy is unsupported, or required snapshot compatibility is unknown. It lists version family, Unit when applicable, expected constraint, and supplied value without collapsing distinct version families into one string.

### 9.4 Source change rejection

`SourceChangedRejected` applies when the current source revision differs from the projection/request revision between generation and finalization. It contains expected and observed revision identities when safely available. It creates no snapshot and no lock and instructs the caller contractually to regenerate rather than retry stale content unchanged.

### 9.5 Other non-success outcomes

An implementation will also need structured `IdentityRejected`, `AlreadyFinalizedRejected`, `AuthorizationRejected`, `IntegrityRejected`, and `InfrastructureFailed` outcomes. These do not replace the four required primary outcomes. Infrastructure failure must not masquerade as business incompleteness and must preserve atomic rollback.

Failures are deterministically ordered and safe to log. They contain no passwords, secrets, connection strings, or raw authorization material.

## 10. Correction and supersession policy

### 10.1 No direct modification

There is no operation to edit a finalized snapshot in place. Corrections to operational rows, Events, Runtime Baselines, configuration, policies, labels, or calculations do not alter historical snapshot content. A direct database update, delete-and-replace, checksum refresh after mutation, or reuse of the same `SnapshotId` is forbidden.

### 10.2 New snapshot version approach

A future approved correction workflow must:

1. preserve the original snapshot and checksum;
2. correct authoritative operational/domain sources through their own approved workflows;
3. generate a new open `ReportProjection` from current corrected authoritative inputs;
4. capture a correction reason, authorized actor, and reference to the prior effective snapshot;
5. pass the full completeness, identity, version, freshness, and integrity gates;
6. create a new `SnapshotId` with the next lineage sequence;
7. set `SupersedesSnapshotId` to the prior effective snapshot;
8. atomically transition the effective lock reference to the new snapshot;
9. append audit evidence linking both snapshots.

The old snapshot remains readable as historical evidence and is marked superseded through lineage metadata external to its immutable payload or through append-only status evidence. It is not rewritten to add the new identity.

Correction authorization, required roles, reason taxonomy, reopen behavior, notification, and retention are unresolved security/business decisions. Until approved, no correction implementation is authorized.

## 11. Migration and coexistence strategy

### 11.1 Legacy coexistence

Legacy Reporting remains unchanged and continues using its current tables, finalization behavior, UI, and PDF path during the architecture and validation phases. Target snapshots use distinct contracts and, in a future phase, distinct persistence authority. No target component may silently treat a legacy partial snapshot as a `FinalizedReportSnapshot`.

Legacy locks and target locks must not compete without an approved ownership rule. During coexistence, target generation and shadow validation are read-only. A feature gate must prevent accidental target finalization until persistence, atomicity, migration, and rollback are proven.

Existing legacy finalized reports must remain accessible. Their known mixed snapshot/live characteristics are historical limitations, not permission to weaken target immutability.

### 11.2 Legacy record classification

A future migration assessment must classify each legacy finalized period as:

- complete and eligible for verified conversion from preserved evidence;
- readable legacy-only because required target evidence/versions are absent;
- inconsistent and requiring manual reconciliation;
- unavailable/corrupt and requiring recovery policy.

Migration must not invent Event, Runtime, version, completeness, checksum, or source-revision evidence. Converted records require explicit migration provenance and a migration calculation/policy version. If faithful conversion cannot be proven, retain the record as legacy rather than presenting it as a verified target snapshot.

### 11.3 Future production adoption gates

Production adoption requires all of the following before cutover:

- approved snapshot domain contracts and serialization/schema design;
- read-only source adapters backed by a proven consistent-read mechanism;
- freshness verification and atomic snapshot/lock persistence tests;
- checksum canonicalization golden tests across culture and machines;
- complete Rasht and Ramsar synthetic and representative copied-data validation;
- accepted shadow comparison with every difference classified;
- proof that finalized readers and exporters perform no operational reads;
- legacy coexistence/migration rules and rollback plan;
- correction/recovery authorization policy;
- dependency and security review;
- explicit production activation approval.

Cutover should be Station/feature gated and reversible at the routing level. Rollback must not delete target snapshots already committed. Once a target snapshot is authoritative for a period, fallback must preserve its availability and must not reconstruct it through legacy calculations.

## 12. Required future verification tests

A later implementation must include deterministic tests for:

- complete projection finalizes successfully;
- incomplete, invalid, unavailable, and rejected projections cannot finalize;
- every missing/incompatible version family rejects with the correct Unit evidence;
- source revision changed before capture rejects without writes;
- source revision changed at the transaction boundary rolls back both snapshot and lock;
- snapshot insert failure creates no lock;
- lock failure leaves no snapshot observable;
- duplicate finalization is idempotently rejected or returns the already committed matching result under an approved idempotency policy;
- snapshot content remains unchanged after source, Event, Runtime, Baseline, ESD configuration, profile, policy, and clock changes;
- finalized reads succeed with operational repositories unavailable;
- checksum is stable across culture/machine and changes for every protected payload mutation;
- unsupported checksum/version evidence fails closed;
- correction creates a new identity and sequence while preserving the original;
- concurrent finalization/correction attempts produce one coherent effective snapshot;
- Rasht and Ramsar identities and profiles never leak across snapshots;
- Persian half-open period identity survives capture exactly;
- Runtime minutes remain authoritative and formatted hours never alter them;
- backup/restore preserves snapshot, checksum, lineage, and lock integrity.

## 13. Deferred decisions

The following remain explicitly deferred and must not be inferred during implementation:

- physical database schema, indexes, serialization, and migration scripts;
- source-revision and consistent-transaction mechanism;
- snapshot identity generation algorithm;
- exact lock storage and concurrency mechanism;
- canonical payload encoding and checksum storage encoding;
- whether digital signatures are required in addition to checksums;
- correction authorization, reopen, supersession approval, and recovery process;
- legacy-data conversion eligibility rules for specific existing records;
- retention/archive policy;
- exporter formats and layouts;
- UI lock/finalization controls and messaging;
- compatibility rules allowing aggregation across non-identical versions;
- any exceptional completeness override.

## 14. Phase 5.6 verification

This phase adds only `docs/report-snapshot-finalization-specification.md`.

- No production C# file is created or modified.
- No legacy file under `Services/Reports`, `Models/Reports`, or `Core/Reports` is modified.
- No UI file or lock control is created or modified.
- No database, SQLite schema, migration, repository, persistence implementation, or registration is created or modified.
- No snapshot persistence or exporter is implemented.
- Existing build and test behavior remains unchanged because the specification is documentation-only.

## 15. Approval gate

This specification authorizes no production implementation by itself. A future phase may create isolated snapshot/finalization domain contracts and pure integrity tests only after explicit approval. Persistence/schema work, production wiring, lock enforcement, migration, UI, and export each require their own approved scope and verification gates.
