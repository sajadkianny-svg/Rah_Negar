# Phase 7.8 — Unified Migration Chain and Legacy ESD Reconciliation

## Status and safety boundary

Phase 7.8 is implemented as inactive, adapter-oriented infrastructure. The unified chain is exposed only through an explicit factory and is not registered in `Program.cs`, startup composition, feature configuration, or any production database path. Authentication and protected operations remain inactive. Production WinForms and legacy operational tables were not changed. All migration and reconciliation tests use uniquely generated temporary SQLite files.

This phase reconciles the competing draft migration transitions identified in Phase 7.7, strengthens migration-ledger validation, and adds an explicit legacy-to-target ESD reconciliation workflow. It does not perform an ESD authority cutover.

## Migration inventory before reconciliation

Before Phase 7.8, three independent draft migrations each declared framework version `0 -> 1`. They were individually usable in isolated tests but could not form one registered chain. None was registered in production or recorded as a deployed production migration by this repository.

| Migration ID | Original transition | Registration | Objects owned | Dependencies and assumptions | Rerunnable through runner | Destructive | Test use and conflict |
|---|---:|---|---|---|---|---|---|
| `phase7.7-security-persistence-atomic-esd-v1` | 0 -> 1 | Draft/test only | `SecurityShiftProfiles`, credential, management credential, device identity, trusted keys, consumed requests, receipts, audit, deployment settings, related indexes/triggers | Assumed an empty framework ledger | Yes, ledger/checksum controlled | No | Phase 7.7 persistence tests; collided with both other 0 -> 1 drafts |
| `event-target-schema-v1-draft` | 0 -> 1 | Draft/test only | `Events`, `EventAudit`, event indexes and immutability triggers | Expected station, unit, and shift-profile parents to exist; previously referred to a non-authoritative `ShiftProfiles` name | Yes, ledger/checksum controlled | No | Event target tests; collided with security and reporting drafts |
| `report-snapshot-target-schema-v1-isolated` | 0 -> 1 | Draft/test only | `ReportSnapshots`, `ReportPeriodLocks`, `ReportFinalizationReceipts`, indexes and append-only/immutability triggers | No dependency encoded in the version transition | Yes, ledger/checksum controlled | No | Reporting persistence tests; collided with security and event drafts |

The collision was structural, not merely numeric: the event schema needs foundation parents and the authoritative security identity table, while all three migrations independently assumed ownership of the first framework transition. Mechanical renumbering without dependency repair would have retained an invalid event foreign-key assumption.

## Authoritative target chain

Phase 7.8 defines one explicit chain with final target version 4:

```text
v0
 └─ target-database-foundation-v1
      v1
       └─ phase7.7-security-persistence-atomic-esd-v1
            v2
             └─ event-target-schema-v1-draft
                  v3
                   └─ report-snapshot-target-schema-v1-isolated
                        v4
```

The authoritative inventory is:

| Order | Migration ID | From | To | SHA-256 schema checksum | Explicit dependency | Active ownership |
|---:|---|---:|---:|---|---|---|
| 1 | `target-database-foundation-v1` | 0 | 1 | `E4EFE49224C15D343EF530C7C26FA0BD683633171B0F77569B45D8DC54A0B3FE` | None | Deployment foundation (`Stations`, `Units`) |
| 2 | `phase7.7-security-persistence-atomic-esd-v1` | 1 | 2 | `811E519E428F193A5529ADEAC1D4F59BDD64097E1FD6A65CABC6535052D19F7E` | Foundation | Phase 7.7 `Security*` persistence |
| 3 | `event-target-schema-v1-draft` | 2 | 3 | `52472743843783DDFF95AAF77A127ABD165FED99EFE848EECAB11FF7E2273913` | Foundation and `SecurityShiftProfiles` | Event target and event audit schema |
| 4 | `report-snapshot-target-schema-v1-isolated` | 3 | 4 | `34003D9571E8886F17DEAF30DBE2AFA5B17FCBEC85A344FA94EDCF9D7B4535F7` | Prior target chain | Reporting snapshots, locks, and finalization receipts |

Existing draft IDs were retained because they are stable identifiers. Their version metadata was rebased into the dependency order. The event schema was also corrected to reference `SecurityShiftProfiles`, the sole normal operational identity persistence table. Checksums are deterministic hashes of canonical migration SQL; the ledger additionally records and validates transition versions. The chain factory fixes order explicitly and performs no reflection-based discovery.

## Schema ownership

Each target object has one migration owner:

- Database foundation owns `Stations` and `Units`. It supplies generalized deployment parents and does not encode Rasht, Ramsar, or a fixed unit count.
- Security owns all Phase 7.7 tables prefixed `Security`, including the station-wide `SecurityDeploymentSettings`. It introduces no role, permission, RBAC, or local Support schema.
- Event owns `Events`, `EventAudit`, and their event-specific indexes/triggers.
- Reporting owns `ReportSnapshots`, `ReportPeriodLocks`, `ReportFinalizationReceipts`, and their reporting-specific indexes/triggers.

No active migration independently creates another migration's table, index, or trigger. All SQL is additive. The chain contains no `DROP TABLE`, legacy rebuild, deletion, or fabricated history operation.

## Migration runner and ledger integrity

The runner now validates the supplied chain before opening its execution transaction. It rejects duplicate migration IDs, duplicate source transitions, duplicate target versions, gaps, overlaps, and non-increasing transitions. For an existing ledger it validates:

- each applied ID exists in the supplied chain;
- recorded source and target versions match current migration metadata;
- the stored checksum matches the deterministic migration checksum;
- recorded history is contiguous and ordered;
- the framework current version equals the last valid history target;
- a database version newer than the supplied target fails closed.

Pending migrations are applied in a single SQLite transaction. A migration is entered in history only after its SQL has succeeded within that transaction, and the framework version is advanced in the same transaction. The failure-injection test throws during an intermediate migration and proves that no later migration is marked, no false success remains in history, and a subsequent valid run can recover. SQLite rollback therefore protects the chain at the framework boundary; production activation must still establish backup, busy-timeout, exclusive maintenance, and operational recovery policy.

Empty-database and representative-legacy tests both reach deterministic version 4. A rerun applies zero migrations. Legacy fixtures retain `app_settings`, representative runtime rows, legacy event rows, and an arbitrary unrelated table unchanged while target objects are added.

## Legacy ESD source audit

The currently active application setting is `app_settings.esd_extra_runtime_hours`.

- `StartupSetupService` defines it as SQLite `REAL NOT NULL DEFAULT 0`.
- The table uses an autoincrement `id`; there is no database singleton constraint. Current setup code deletes rows before inserting one, while read/write code chooses the first row by `id`.
- `AppSettingsService` converts the stored value to `double` and writes a `double` parameter.
- The setup UI parses with the current culture, validates a nonnegative value, and presents the setting as hours.
- Current reporting reads this legacy setting. Therefore legacy `app_settings` remains the production authority before an approved cutover.
- SQLite `REAL` and the current `double` application path are binary floating-point. Exact original decimal text cannot be reconstructed if precision was already lost before this phase.

The inactive reader checks that the table and column exist, detects zero or multiple rows explicitly, and obtains SQLite's current stored numeric representation with `CAST(... AS TEXT)`. It parses directly as invariant `decimal`, avoiding an additional binary floating-point conversion in the reconciliation path. It accepts zero and surrounding whitespace, preserves exact target decimal values and trailing-zero equivalence, and rejects negative, malformed, comma-locale, and policy-exceeding values. A bounded policy is injected; production must approve its concrete maximum and scale before activation.

## Reconciliation model and states

The application contracts expose the requested explicit states: `LegacyValueFound`, `LegacyValueMissing`, `LegacyValueInvalid`, `TargetNotProvisioned`, `TargetAlreadyProvisionedSameValue`, `TargetAlreadyProvisionedDifferentValue`, `ReadyToProvision`, `Provisioned`, `Conflict`, and `Failed`.

Inspection proceeds without mutation:

```text
Read legacy value
  ├─ absent                 -> LegacyValueMissing
  ├─ malformed/out of rule -> LegacyValueInvalid
  └─ valid decimal         -> LegacyValueFound
       ├─ target absent          -> TargetNotProvisioned / ReadyToProvision
       ├─ target exact same      -> TargetAlreadyProvisionedSameValue
       └─ target different       -> TargetAlreadyProvisionedDifferentValue / Conflict
```

Provisioning is a separate explicit call. It inserts the singleton target only when the inspection result is ready and the target remains absent. It is insert-only: no update or last-write-wins path exists. A concurrent insert is reread; an exact match is reported safely, while a different value becomes a conflict. Results contain only non-secret reconciliation evidence and categories.

The target setting is station/deployment-wide and has no `UnitId`. The value written to `SecurityDeploymentSettings` is the exact validated `decimal` rendered invariantly. Cases cover zero, integers, fractional values, trailing zeros, whitespace, the policy maximum, malformed strings, locale-specific comma text, negatives, and excessive values.

## Authority and cutover

`EsdAuthorityMode` makes ownership explicit. The Phase 7.8 provider always returns `LegacyAuthoritative` and identifies `app_settings.esd_extra_runtime_hours` as the source. Provisioning the target does not change authority. A future privileged cutover must replace this inactive provider/composition only after migration validation, conflict resolution, audit approval, rollback planning, and production registration are approved.

This prevents an ambiguous dual-authority state: before cutover, target data can be reconciled but is not read as authoritative; after a future explicit cutover, `SecurityDeploymentSettings` must become the only application authority. Phase 7.8 supplies no automatic cutover, discovery of a production path, or startup migration behavior.

## Conflict and finalized-snapshot isolation

If legacy and target values differ, neither value is overwritten. The service returns an explicit conflict requiring a future privileged reconciliation decision. Missing, invalid, and multiple legacy values similarly fail without fallback.

The reconciliation adapters access only `app_settings` and `SecurityDeploymentSettings`. They contain no update path for `ReportSnapshots`, `ReportPeriodLocks`, finalized canonical JSON, or report recalculation. Tests seed a finalized snapshot and lock, capture the canonical JSON bytes, provision the target ESD value, and verify both snapshot bytes and lock state remain unchanged.

## Test and verification results

The complete suite passes: **284 tests passed, 0 failed, 0 skipped**. Phase 7.8 coverage includes inventory consistency, collision freedom, duplicate IDs/versions, gaps, ordering, empty and legacy migrations, idempotency, deterministic final version, checksum/history/schema tampering, intermediate rollback and recovery, legacy row preservation, exact decimal cases, missing/invalid/multiple values, same-value idempotence, different-value conflict, no overwrite, station-wide scope, inactive authority, snapshot byte isolation, and absence of production discovery, RBAC, Support identity, and destructive migration SQL.

The complete solution builds with zero errors. Six existing `NU1701` compatibility warnings remain for `OpenTK 3.1.0`, `OpenTK.GLControl 3.1.0`, and `SkiaSharp.Views.WindowsForms 3.119.0` across the application/test projects. They predate this phase and are not silently upgraded here. `git diff --check` passes.

Verification also confirms that `Program.cs`, production WinForms, startup composition, and feature configuration are unchanged; no deployment database was opened or modified; migration execution was limited to generated temporary test databases; no destructive legacy-table operation, RBAC structure, local Support identity, vendor private key, or unit-specific ESD field was introduced.

## Remaining activation prerequisites

Before production registration, the product needs an approved database backup/restore and maintenance-window procedure, a reviewed baseline/adoption plan for installations with any historical experimental migration ledger, a production busy/lock policy, deployment-specific migration rehearsal, and signed operational rollback guidance. Old draft histories must never be fabricated or silently rewritten; any real database containing an earlier draft ID/version/checksum needs an explicit assessed adoption migration.

The approved ESD decimal range/scale must be finalized. Because legacy `REAL` may already contain binary rounding, values requiring business confirmation must be surfaced rather than guessed. A privileged reconciliation UI/workflow, security audit integration, management and vendor authorization enforcement, and an explicit authority-cutover implementation remain future work. Only after those prerequisites are reviewed should startup registration or production data access be considered.
