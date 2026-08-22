# RahNegar Pre-Implementation Decision Gap Review

## 1. Review scope

This review reconciles the Pre-Implementation Decision Package, Domain Glossary, Runtime Truth Table, Final Snapshot Domain Schema, implementation roadmap, and revised ADR-004. It identifies issues that remain unsafe to resolve implicitly.

No previously approved architectural direction is redesigned here unless a concrete conflict exists.

## 2. Blocking unresolved decisions

### 2.1 Runtime transitions

All items below remain **Pending Product Owner Decision**:

- Running + START.
- Stopped + NSD.
- StoppedAfterOH + NSD.
- Stopped + ESD.
- StoppedAfterOH + ESD.
- Running + OH.
- Stopped + OH.
- StoppedAfterOH + OH.

Risk: choosing defaults would change cumulative runtime, after-OH runtime, longest runs, service days, and event statistics. These block production Runtime and Event Validation milestones.

### 2.2 ESD Adjustment

Each target remains independently unresolved:

- PeriodRuntimeHours.
- CumulativeRuntimeAtPeriodEnd.
- RuntimeAfterOHAtPeriodEnd.
- ServiceDay.
- LongestRunInPeriod.

Also unresolved: whether a stopped-state ESD qualifies and whether adjustment duration varies by station, unit, event, or configuration effective date.

### 2.3 Runtime boundaries

- ServiceDay minimum qualifying duration.
- Period-clipped versus whole-run LongestRunInPeriod.
- Initially Running baseline with unknown historical START.
- Whether an open run can precede `data_start_date`.
- Runtime behavior when event history contains a material unresolved anomaly.
- Checkpoint trust and invalidation rules beyond the minimum provenance contract.

### 2.4 Event ordering/statistics

- Whether same-time events for one unit are prohibited or explicitly sequenced.
- Whether legacy row ID is accepted only as provenance or as reconstructed business order.
- Whether OH receives day/night statistics.
- Whether invalid preserved legacy events appear in a separate raw count.
- Whether shift boundaries are configurable and whether legacy 07:00–19:00 is confirmed for both stations.

### 2.5 Operational definitions

- Whether `data_start_date` may be any Persian date or only month day one.
- Whether new entries must remain strictly sequential by day.
- Meaning of S, M, A, and hourly OH statuses.
- Whether hourly statuses affect runtime or are report-only observations.
- Required units, ranges, precision, and zero/null meaning of every legacy field.
- Whether events can ever participate in Completeness.
- Whether a configuration-version change may become effective mid-month.

### 2.6 Reporting/finalization

- Recycle transition definition beyond legacy zero/nonzero behavior.
- Rounding policy for stored decimal summaries and display.
- Cross-month LongestRun aggregation.
- Handling incompatible configuration/runtime versions in finalized multi-period reports.
- Whether multi-period finalized results are persisted snapshots or deterministic on-demand projections over monthly snapshots.
- Supported lifetime for old snapshot schema readers.
- Whether Version 1 PDF must visually match legacy output.

### 2.7 Security/operations

- Named users and role matrix approval.
- Whether Operators may finalize or create backups.
- Recovery authority and offline administrative process.
- Encryption key custody, rotation, and disaster recovery.
- Factory reset prerequisites and whether a verified backup is mandatory.
- Maximum supported data volume and performance targets.

## 3. Concrete contradictions

### 3.1 ESD semantics became accidentally normative

Earlier glossary text recommended that ESD Adjustment increase the three accounting-runtime values but not ServiceDay or LongestRun. The package also listed exact ESD behavior as unresolved. The authoritative documents now mark every target **Pending Product Owner Decision** and preserve physical/adjustment separation.

Required resolution: approve a target matrix before M8. Recommendations are not defaults.

### 3.2 OH behavior became accidentally normative

Earlier text stated that OH closes a running unit, leaves it stopped, and resets after-OH runtime. The decision package separately identified OH-while-running and OH-while-stopped as unresolved.

Resolution in current documents: reset semantics apply only to an accepted OH; source-state validity and direct-close behavior remain pending.

### 3.3 ServiceDay appeared both fixed and unresolved

Earlier text defined any positive overlap as a ServiceDay while later blocking decisions requested product-owner confirmation.

Resolution: positive overlap is the recommended rule, but duration threshold and adjustment interaction remain pending.

### 3.4 LongestRun appeared period-clipped and unresolved

Earlier text selected period clipping while the decision list still asked for boundary approval.

Resolution: `LongestRunInPeriod` is the authoritative name; clipped versus whole touching run remains pending, with clipping documented only as recommendation.

### 3.5 ADR-004 project count conflict

The original architecture proposal showed one physical project per logical concern, which could be read as mandatory. The requested revision permits fewer projects while retaining mandatory logical boundaries and dependency direction.

Resolution: `05-adr-004-solution-architecture-revised.md` supersedes one-to-one project wording.

### 3.6 One station versus generalized schema

ADR-002 proposes one active station per Version 1 database while the ERD includes `station_id` everywhere. This is not inherently contradictory: the schema remains generalized and future-compatible while the host/UI enforces one active station.

Required clarification: enforce single-station as an application rule only, or add database metadata preventing a second station. Recommendation: application rule plus explicit database installation identity, without distorting relational design.

### 3.7 Snapshot completeness versus legacy import

ADR-014 requires complete immutable snapshots, but legacy tables never froze every analytical section.

Resolution required: an imported legacy finalized artifact cannot falsely claim native completeness. It must be marked `LegacyImportedFinalized` with unavailable or explicitly reconstructed sections and provenance. Whether reconstruction is permitted is still pending.

## 4. Assumptions that must not be normative

- Twelve odd-hour observations are Rasht/Ramsar configuration, not a platform invariant.
- U1–U4 and maximum four units are not platform limits.
- START/NSD/ESD/OH codes are initial configuration semantics, not hard-coded column design.
- Station name is display metadata, not identity or dispatch.
- Daily unique data is not the identity of an Operating Day.
- `data_start_date` being month day one is legacy UI behavior, not yet an approved invariant.
- 07:00–19:00 shift boundaries are not universal.
- Zero is not automatically missing or inactive.
- Pressure, temperature, fuel, flow, and recycle units cannot be inferred authoritatively from labels.
- A Windows username is not an authenticated finalizer.
- A cached runtime value is not automatically a Trusted Runtime Baseline.
- Legacy finalized analytical sections are not complete snapshots.
- JSON snapshot storage does not mean arbitrary untyped JSON is the domain model.
- Logical modules do not require one assembly each.
- A synthetic station is an acceptance proof, not a third hard-coded profile.

## 5. Requirements too vague for implementation

### 5.1 Configuration formulas

The proposal calls for constrained formulas but does not freeze:

- Supported operators/functions.
- Decimal precision and rounding stages.
- Null propagation.
- Divide-by-zero result.
- Dependency cycles.
- Whether formulas may reference other calculated fields.

This blocks calculated-field implementation in M3/M6.

### 5.2 Typed observation storage

The ERD proposes numeric/integer/text columns and exactly one populated value. It does not freeze:

- Decimal SQLite representation and precision guarantees.
- Whether integer is semantically distinct or normalized to decimal.
- Status representation and localization.
- Comparison rules for imported floating-point legacy values.

This blocks final schema approval.

### 5.3 Configuration effective dates

Immutable effective-dated versions are approved conceptually, but behavior is vague when:

- A new unit starts mid-period.
- A field becomes required mid-month.
- A schedule changes mid-month.
- Finalization spans multiple versions.

Completeness and reporting need explicit subrange rules or a Version 1 restriction to month-boundary changes.

### 5.4 Concurrency

Optimistic revision checks are proposed, but Version 1 deployment is local/single-station. Required behavior for multiple application processes, stale forms, and backup/finalization races needs explicit acceptance scenarios.

### 5.5 Reporting precision

“Min/max/average/sum” is insufficient without:

- Decimal arithmetic policy.
- Intermediate precision.
- Final rounding.
- Null/invalid exclusion.
- Zero behavior.
- Units and unit conversion.

### 5.6 Snapshot canonicalization

Hashing requires a frozen canonicalization specification:

- Property ordering.
- Collection ordering.
- Decimal representation.
- Date/time representation.
- Unicode normalization.
- Inclusion/exclusion of metadata.

This blocks M11 even though snapshot content is otherwise defined.

### 5.7 Backup key management

Authenticated encryption is approved, but offline key creation, storage, recovery, rotation, and operator workflow remain undefined. Cryptographic primitives alone do not solve recoverability.

### 5.8 Migration acceptance tolerances

Side-by-side comparison requires tolerances and classifications for:

- Legacy floating-point rounding.
- Known runtime defects.
- Missing snapshot sections.
- Duplicate main records.
- Invalid events.
- Legacy data before baseline/start date.

## 6. Architecture risks before coding

### 6.1 Generic observation model performance

The generalized row model can multiply row counts substantially. Before final schema freeze, benchmark representative maximum station definitions and historical years using realistic report queries and indexes. Do not revert to station-shaped tables without evidence.

### 6.2 Configuration over-flexibility

Making every behavior configurable can create an unsafe rules engine. Version 1 should constrain semantic event effects, data types, aggregation types, and formula operations to approved enumerations. Configuration must not execute arbitrary code.

### 6.3 Logical boundaries in shared assemblies

Revised ADR-004 permits fewer projects, increasing leakage risk. Architecture tests, namespace rules, and code review are required from M1. Otherwise infrastructure and legacy concerns can contaminate Domain before extraction.

### 6.4 Runtime checkpoint corruption/staleness

Checkpoints improve performance but can silently produce wrong cumulative values after historical edits. Version 1 should initially prove replay correctness without checkpoints or define strict event-watermark invalidation before enabling them.

### 6.5 Final snapshot size

Storing complete event logs and daily combinations for long periods may create large payloads. Monthly snapshots are bounded, but size/performance must be measured. Semantic completeness must not be sacrificed; normalized immutable child records are an alternative if payload size is unacceptable.

### 6.6 SQLite restore and process coordination

Atomic file replacement can fail if another process holds the database. Version 1 needs single-instance/process coordination and explicit connection shutdown before restore.

### 6.7 Offline credential recovery

Removing hard-coded recovery secrets creates an operational key-custody requirement. Without a defined recovery authority, stronger security could cause unrecoverable station access.

### 6.8 Legacy runtime ambiguity

Some legacy histories cannot be made authoritative without product decisions. Migration must allow “runtime unresolved” status instead of manufacturing precise values. Cutover criteria must say whether such stations can operate pending reconciliation.

### 6.9 Configuration identity stability

Stable IDs in imported configuration must remain consistent across versions. Regenerating IDs from labels or file order would break history. ID issuance and template-update policy must be frozen.

### 6.10 Finalization across rule changes

If calculation or configuration versions change mid-month, one snapshot may need multiple provenance segments. Either support that complexity or restrict effective changes to clean boundaries in Version 1.

## 7. Required pre-coding resolution order

### Gate A — Before architecture foundation

- Approve revised ADR-004.
- Approve physical initial packaging choice.
- Approve one-station-per-database enforcement level.
- Approve named-user direction.

### Gate B — Before domain/configuration/schema implementation

- Freeze `data_start_date` policy.
- Freeze units, field types, precision, zero semantics, statuses.
- Freeze formula language.
- Freeze same-time event storage policy.
- Freeze configuration effective-date restrictions.
- Freeze SQLite decimal representation.

### Gate C — Before event/runtime implementation

- Close every runtime truth-table pending transition.
- Freeze ESD targets.
- Freeze ServiceDay, LongestRun, baseline uncertainty, and shift rules.

### Gate D — Before reporting/finalization

- Freeze aggregation/rounding/extreme/recycle semantics.
- Freeze snapshot canonicalization and schema support policy.
- Freeze multi-period and cross-month runtime aggregation.
- Resolve native versus legacy-imported snapshot status.

### Gate E — Before operational release

- Freeze backup key/recovery operations.
- Freeze role capability matrix.
- Freeze migration tolerances and anomaly approval.
- Approve performance limits and benchmarks.
- Accept Rasht, Ramsar, and synthetic station scenarios.

## 8. Review conclusion

The architecture direction is coherent: an offline modular monolith, configuration-driven stations, generalized storage, one runtime engine, transactional daily writes, complete immutable snapshots, and isolated legacy migration. The remaining blockers are predominantly product semantics and operational security policies rather than a need to redesign those approved foundations.

Coding may begin only with M1 foundation work that does not encode unresolved semantics. Domain/runtime/schema work must respect the milestone gates above.
