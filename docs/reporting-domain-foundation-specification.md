# Reporting Domain Foundation Specification

## 1. Document status and purpose

This specification defines the Phase 5.2 domain boundary required before implementing the target Reporting subsystem. It is an architecture and policy document only. It does not activate the Phase 4 Runtime Projection Engine, replace the legacy reports, define a database migration, or change current UI, export, finalization, or lock behavior.

The specification is based on:

- `docs/phase5-reporting-audit.md`;
- `docs/reporting-architecture-specification.md`;
- `docs/runtime-calculation-policy-specification.md`;
- `docs/phase4-runtime-projection-engine-report.md`.

The target design preserves the established reporting meanings: applicable hourly operational values use minimum, maximum, and average; daily unique values use sum; Persian period conventions remain authoritative; Rasht and Ramsar definitions remain isolated; and Runtime values originate from the authoritative Runtime domain rather than being recalculated by Reporting.

## 2. Core principles

1. Reporting composes authoritative inputs; it does not become an authority for Events, Runtime, operational entry, or configuration.
2. Open projections are live, reproducible for their captured evidence, and non-authoritative.
3. Finalized snapshots are immutable, self-contained business records.
4. A finalized report screen or export performs no operational-data reads.
5. Report calculations are deterministic for identical normalized inputs, versions, policy, and period.
6. All authoritative Runtime values remain integral minutes. Decimal hours are presentation values only.
7. Every result identifies its Station, period, source mode, calculation timestamp, and relevant versions.
8. Reporting never validates, repairs, reorders, or infers Events and never calculates Runtime.
9. Exporters render contracts supplied to them; they do not query operational persistence or recalculate domain results.
10. The target subsystem coexists with legacy Reporting until explicit shadow-validation and cutover gates are passed.

## 3. Target reporting architecture boundary

```text
Authoritative source adapters
  - hourly operational data
  - daily unique data
  - validated Event Chain/projection
  - Runtime Projection
  - Station/profile and approved policies
                  |
                  v
        Projection input boundary
  typed, normalized, versioned evidence
                  |
                  v
       Report calculation layer
  compose + aggregate + prepare sections
                  |
                  v
           ReportProjection
      open, live, non-authoritative
            |              |
            | finalize     | preview/export
            v              v
     Finalized snapshot   Export layer
     immutable + locked   PDF/Excel/CSV/Print
            |
            v
         Export layer
```

### 3.1 Data sources

Data sources are accessed through read-only application contracts implemented outside the Reporting calculation domain. They normalize persistence-specific representations into typed inputs. Required source categories are:

- hourly operational records for the selected Station and period;
- daily unique records for the selected Station and period;
- an authoritative validated Event Chain or Event reporting projection, including its identity and version;
- an authoritative Runtime Projection per configured Unit, including Runtime evidence metadata;
- Station report profile and parameter definitions for Rasht or Ramsar;
- `DataStartDate`, period/calendar policy, report policy, and any applicable configuration identity;
- a consistent source-revision token or equivalent evidence capable of detecting changes between generation and finalization.

The data-source boundary may eventually be backed by SQLite, copied databases, fixtures, or snapshot storage. Those implementation details must not leak into report calculation models. Reporting source contracts must not write, repair, or lock operational data.

### 3.2 Projection layer

The projection layer coordinates source reads and obtains one normalized input bundle for one Report Identity. It must:

- enforce Station, Unit-set, and period identity consistency across inputs;
- require the requested half-open chronological boundary to match Event and Runtime inputs;
- retain source identities and versions without transforming their meaning;
- invoke the single completeness authority;
- pass typed values to the pure report calculation layer;
- attach a caller-supplied calculation timestamp rather than reading the clock throughout calculation;
- fail explicitly when required inputs conflict, are unavailable, or refer to another period.

This layer may coordinate repositories. It must not query production data through UI code and must not silently fall back from target authoritative Event/Runtime inputs to legacy calculations.

### 3.3 Report calculation layer

The report calculation layer is pure domain/application calculation. It consumes a complete normalized input bundle and produces a `ReportProjection`. It performs composition, approved aggregations, section construction, and presentation preparation only. It has no database, UI, filesystem, printer, or clock dependency.

### 3.4 Snapshot layer

The snapshot layer converts an eligible `ReportProjection` into a self-contained immutable finalized snapshot. It preserves every value and evidence item needed by finalized UI and exporters. Snapshot persistence and period locking must be atomic at the future infrastructure boundary. The storage schema and serialization format are deliberately not selected in this documentation phase.

### 3.5 Export layer

The export layer contains format-specific renderers for PDF, Excel, CSV, and Print. A renderer accepts either a `ReportProjection` for a clearly labeled open preview/export or a finalized snapshot for an official finalized output. It never receives database connections or repositories and never invokes Event or Runtime calculations.

## 4. Report identity

Every projection and snapshot must carry a `ReportIdentity` with at least:

| Field | Meaning |
|---|---|
| `ReportId` | Stable identity for this projection instance or finalized report record |
| `StationId` | Canonical Station identity, not display text |
| `StationName` | Captured display name |
| `PeriodStartMinute` | Inclusive canonical local-minute boundary |
| `PeriodEndMinute` | Exclusive canonical local-minute boundary |
| `PersianPeriodLabel` | Captured presentation label for the requested Persian period |
| `PeriodKind` | Monthly, half-year, yearly, or approved arbitrary range |
| `UnitIds` | Deterministically ordered configured Units included in the report |
| `SourceMode` | `OpenProjection` or `FinalizedSnapshot` |

The identity must reject an empty/reversed period, unknown Station, duplicate Unit identity, or mismatch between the requested period and authoritative Event/Runtime inputs. Report identity is distinct from a snapshot identity: recalculating the same open report produces a new projection instance/evidence set, while finalization assigns a persistent immutable snapshot identity.

## 5. ReportProjection model

`ReportProjection` is the complete calculated representation used by target UI and export preparation. It must be immutable after construction and contain no persistence entities or mutable collections.

### 5.1 Header and state

- `Identity`;
- `ProjectionStatus` (`Complete`, `Incomplete`, or `Rejected`);
- `CalculationTimestamp`;
- `CompletenessResult`;
- `Evidence`;
- `Versions`;
- deterministic warnings and blocking reasons.

A rejected calculation returns structured failure evidence and must not masquerade as a complete projection. Whether the implementation uses a separate result wrapper is an implementation choice; failure must remain explicit.

### 5.2 Required sections

The projection must support the complete existing reporting surface so finalization can be self-contained:

1. **Operational summary** — parameter identity, label/unit, aggregation type, value, contributing count, and applicable extreme evidence.
2. **Daily unique summary** — parameter identity, label/unit, sum, contributing count, and missing-day evidence.
3. **Runtime by Unit** — Physical Runtime, ESD Adjustment, Adjusted Runtime, Runtime After OH, Longest Run, Service Day Count, and Final State exactly as supplied by Runtime Projection.
4. **Event summary by Unit** — counts and reporting fields derived from the authoritative validated Event input; Reporting must not decide Event validity.
5. **Event log** — the authoritative in-period Event representation and stable identity/order supplied to Reporting.
6. **Service-day and service-combination section** — composition of authoritative physical service-day/Runtime results; no Runtime reimplementation.
7. **Extreme-date section** — approved min/max occurrence dates derived from normalized hourly inputs.
8. **Recycle/change section** — preserved legacy business meaning, with its exact target rule required to be isolated and tested before implementation.
9. **Completeness section** — dimension-by-dimension result, missing evidence, and finalization eligibility.

The exact DTO names and UI arrangement are implementation details. Omitting a section from one display does not permit its removal from a finalized snapshot when an official export or another finalized view requires it.

### 5.3 Evidence metadata

Evidence must include:

- source revision or consistent-read identity;
- hourly-data identity/revision and record count;
- daily-data identity/revision and record count;
- Event Chain identity/version and Event boundary;
- Runtime Projection identity per Unit and its input evidence;
- Baseline version per Unit as carried by Runtime;
- ESD/configuration evidence as carried by Runtime;
- Station profile/parameter registry identity;
- DataStartDate used for responsibility/completeness boundaries;
- calendar/time model identity;
- calculation timestamp;
- deterministic ordering/key conventions;
- completeness evidence and any approved override evidence.

The report copies authoritative evidence; it does not reinterpret it. Cryptographic hashes or signatures may supplement evidence later, but algorithms and key management are unresolved Security/Foundation details.

### 5.4 Version metadata

`ReportProjection` must carry the version model defined in Section 10. Missing required versions make a projection ineligible for finalization. Unknown optional presentation versions must not be confused with missing domain evidence.

## 6. Open projection policy

An open-period report is calculated on demand from current authoritative inputs and current applicable approved policies. It is a live view, not authoritative business truth.

- It may change after hourly data, daily data, validated Events, Runtime inputs, or current configuration changes.
- It must state `SourceMode = OpenProjection` and show or retain `CalculationTimestamp`.
- It must retain input identities and versions sufficient to explain what was calculated.
- It is not persisted as a finalized report and must not be used as an implicit lock.
- A future performance cache is disposable, must be invalidated by input/version changes, and never becomes authoritative.
- Exporting an open projection must label it as open/non-finalized. Whether open PDF, Excel, CSV, or Print is exposed to users remains a product decision; the architecture permits it without granting final status.
- Repeating calculation with identical normalized inputs, versions, timestamp input, and policies must produce the same domain values and deterministic ordering.

Open-report ESD behavior is inherited from Runtime: Runtime recalculates unlocked periods using the current deployment ESD Adjustment. Reporting copies the resulting Runtime Projection and must not select an ESD value by Event timestamp.

## 7. Finalized snapshot policy

A finalized snapshot is the sole authority for a finalized report. It is an immutable capture of the eligible projection, evidence, versions, finalization identity, and lock evidence.

### 7.1 Immutability

- No finalized value, section, evidence item, or version may be overwritten by later operational data or configuration changes.
- A later ESD-setting or Event change must not recalculate, reinterpret, or rewrite finalized Runtime fields.
- Any future correction must create a separately identified superseding snapshot through an approved, audited workflow. Reopen/supersession authorization remains unresolved and must not be invented during implementation.
- A performance/materialization cache is not the snapshot authority.

### 7.2 No operational reads

Finalized report viewing, PDF generation, Excel generation, CSV generation, and printing must read only the finalized snapshot and static renderer resources. They must not read hourly, daily, Event, Runtime-base, current Settings, or other operational tables/services. This eliminates the legacy mixed snapshot/live behavior.

### 7.3 Reproducibility

The snapshot must preserve:

- the complete `ReportProjection` content required by every finalized view/export;
- all Report, Snapshot, Event, Runtime, Baseline, policy, calendar, profile, and configuration versions;
- source revision/identity and input counts;
- projection calculation timestamp;
- finalized timestamp and authorized actor identity;
- completeness result and finalization decision evidence;
- deterministic display values or enough authoritative values and captured presentation policy to recreate them exactly.

Runtime authority remains integral minutes. If formatted Runtime hours are stored for convenience, they are derived presentation evidence and never replace the minute values.

## 8. Report calculation responsibilities

### 8.1 Allowed

The Reporting calculator may:

- compose typed hourly, daily, Event, and Runtime inputs into report sections;
- calculate approved Min, Max, and Average over eligible normalized hourly values;
- calculate approved Sum over eligible daily unique values;
- retain counts needed to explain or safely combine aggregations;
- locate extreme dates from the same normalized input set used by aggregation;
- prepare deterministic rows, labels, ordering keys, display groups, and chart-ready values;
- convert Runtime minutes to hours with exactly two decimal places for presentation, without feeding rounded values back into calculation;
- combine authoritative per-Unit service-day results into approved station-level presentation groupings;
- produce deterministic warnings from the completeness result;
- aggregate compatible finalized monthly snapshots only when their semantic versions and identities permit it.

### 8.2 Forbidden

The Reporting calculator must not:

- validate, repair, infer, synthesize, reorder, or discard Events;
- create a generic STOP Event or alter the approved START, NSD, ESD, OH vocabulary;
- calculate Physical Runtime, ESD Adjustment, Adjusted Runtime, Runtime After OH, Longest Run, Service Day Count, or Runtime Final State;
- query SQLite or any database;
- open files, call printers, access UI controls, or read the system clock;
- select current or historical ESD configuration;
- reconstruct Runtime before DataStartDate or invent Baseline facts;
- mutate source records, finalize periods, or acquire locks;
- silently combine inputs from different Stations, Units, periods, revisions, or incompatible policy versions.

Database access belongs only to source/snapshot infrastructure adapters. Event validity belongs to the Event subsystem. Runtime calculation belongs to the Runtime subsystem. Locking belongs to the finalization application/infrastructure boundary.

## 9. Single completeness contract

There must be one `ReportCompletenessResult` authority used by open-report warnings, pending-finalization status, finalization validation, snapshot evidence, and UI messaging. Consumers may present it differently but may not redefine completeness.

### 9.1 Dimensions

| Dimension | Required evidence | Complete condition |
|---|---|---|
| Hourly data | Per responsibility-day record identities and expected-hour coverage | Every required day from the applicable DataStartDate boundary has exactly the approved 12 odd-hour slots: 01 through 23; duplicates, missing slots, or conflicting records are explicit failures |
| Daily data | Required daily-unique identity per responsibility day | Every day has the required unique daily record and required fields according to the Station profile |
| Event Chain | Validation status, identity/version, Station/Unit/period boundary | The supplied chain is authoritative and validated, including a valid empty chain where Events are legitimately optional; any invalid/unavailable required chain blocks finalization |
| Runtime inputs | Per-Unit projection, identity, period, final state, and versions | Every configured Unit has a successful authoritative Runtime Projection matching Station and report boundary, with all required evidence |

Completeness must distinguish `Complete`, `Incomplete`, `Invalid`, and `Unavailable` rather than collapsing every failure into “missing data.” It must list affected dates, Units, fields, and source identities deterministically.

### 9.2 DataStartDate and periods

Software responsibility begins at DataStartDate, the first day of the Wizard-selected Persian month at local `00:00`. For a report overlapping that boundary, completeness evaluates required operational data only from the later of Period Start and DataStartDate. DataStartDate is not a Runtime Baseline; Runtime receives its separate per-Unit Baseline effective at the same instant.

Persian daily/monthly enumeration must use the centralized calendar policy, including leap/non-leap Esfand. Period arithmetic is half-open internally even when UI inputs are entered as inclusive Persian dates.

### 9.3 Finalization eligibility

No projection with `Incomplete`, `Invalid`, or `Unavailable` required dimensions is eligible for ordinary finalization. The current legacy Shift behavior for viewing incomplete open reports is preserved during coexistence but does not establish a target finalization override rule.

Whether an exceptional authorized completeness override will exist in the target is genuinely unresolved. It requires domain/security approval, reason capture, audit requirements, and explicit snapshot evidence before implementation. The system must not infer an override from legacy viewing behavior.

## 10. Versioning model

Every projection and snapshot must carry these version families:

| Version | Scope |
|---|---|
| `ReportCalculationVersion` | Report composition and aggregation algorithm |
| `ReportPolicyVersion` | Approved reporting rules, completeness, precision, and section semantics |
| `ReportProfileVersion` | Rasht/Ramsar parameter definitions, labels, Units, and aggregation mapping |
| `SnapshotFormatVersion` | Serialized/persisted snapshot contract and upgrade reader expectations |
| `EventChainVersion` | Authoritative validated Event input for each applicable Unit/boundary |
| `EventPolicyVersion` | Event state-machine/validation policy carried from Event authority |
| `RuntimeCalculationVersion` | Runtime algorithm version supplied by Runtime Projection |
| `RuntimePolicyVersion` | Runtime policy version supplied by Runtime Projection |
| `RuntimeBaselineVersion` | Trusted Baseline version per Unit supplied by Runtime Projection |
| `RuntimeConfigurationVersion` | Applicable ESD/configuration evidence supplied by Runtime Projection |
| `CalendarPolicyVersion` | Persian calendar and chronological boundary conversion semantics |

Names may be represented as value objects, but their meanings must remain distinct. A single generic version string is insufficient. Projection comparison and finalized-month aggregation require exact compatibility rules. Those rules must default to rejecting unknown incompatibility; they must not silently treat missing versions as equal.

The snapshot also records the projection calculation timestamp and finalization timestamp. Timestamps are evidence, not substitutes for versions.

## 11. Finalization workflow

The target workflow is strictly ordered:

### Step 1 — Generate

Load one consistent, typed input bundle for the Report Identity and calculate a fresh open `ReportProjection`. Do not finalize an arbitrary UI cache without freshness evidence.

### Step 2 — Validate

Confirm identity consistency, successful authoritative Event/Runtime inputs, complete hourly/daily evidence, compatible versions, required sections, deterministic invariants, and absence of blocking errors.

### Step 3 — Capture evidence

Capture or atomically verify the source revision, input identities/counts, all policy/calculation/profile versions, projection timestamp, Station/Unit/period identity, and completeness result. If source freshness has changed since generation, abort and regenerate.

### Step 4 — Snapshot

Create and persist a complete immutable snapshot containing every finalized UI/export field and evidence item. Validate the stored representation before committing. The exact tables or serialized format require a separately approved schema design.

### Step 5 — Lock

Create the period lock in the same atomic commit as the snapshot. A lock without a valid snapshot, or a snapshot without its intended lock, must not be observable. Record finalized actor and timestamp. If any step fails, neither snapshot nor lock is committed.

After commit, all finalized reads resolve by Snapshot Identity and never rebuild from operational inputs.

## 12. Export architecture

All exporters implement a one-way rendering boundary:

```text
ReportProjection OR FinalizedReportSnapshot
                    |
          format-specific renderer
                    |
             output artifact
```

### 12.1 PDF

QuestPDF may remain the offline PDF technology if dependency review permits. The renderer receives a complete contract and static styling resources only. It must not repeat the legacy direct `tbl_events` query or omit required Event types because of renderer-owned filtering.

### 12.2 Excel

An Excel renderer may use the existing ClosedXML dependency only after requirements and package suitability are confirmed. It must preserve authoritative minute values, expose formatted hours separately when needed, and use the same section semantics as other outputs.

### 12.3 CSV

CSV output is suitable for explicitly selected flat sections. Encoding, delimiter, decimal culture, Persian headers, multi-section packaging, and whether CSV is an approved user feature remain unresolved. A CSV renderer still consumes the common contract and may not query sources.

### 12.4 Print

Print renders from the common projection/snapshot contract, preferably through a deterministic print document or finalized artifact. It does not invoke report calculation or operational repositories during page rendering.

### 12.5 Common export rules

- Finalized outputs show Snapshot Identity and finalization evidence.
- Open outputs, if enabled, are visibly marked non-finalized and show Calculation Timestamp.
- Format-specific rounding never changes authoritative values.
- Re-exporting a finalized snapshot with the same renderer/version must preserve domain content; exact binary equality is not required unless separately approved.
- Renderer failures do not alter snapshots, locks, or operational inputs.

## 13. Migration plan

### Phase A — Foundation contracts

Create isolated domain value objects, `ReportProjection`, completeness, evidence, and version contracts. Add no production registration, database adapter, UI path, or schema change.

### Phase B — Read-only target projection

Implement pure reporting calculation and synthetic/read-only adapters outside legacy paths. Consume the validated Event and Runtime contracts; never call legacy Runtime from target Reporting. Verify Rasht/Ramsar profiles and Persian boundaries independently.

### Phase C — Shadow validation

For identical Station, Unit set, period, and source boundary, compare legacy and target fields. Classify matches, approved policy differences, legacy defects, target defects, and input mismatches. Include hourly/daily aggregates, extremes, Event summaries/log, Runtime, service days/combinations, and completeness.

### Phase D — Snapshot design and proof

Design the schema only under separate approval. Prove with fixtures that a snapshot is complete, immutable, versioned, and sufficient for every finalized screen and export with operational repositories unavailable.

### Phase E — Controlled cutover

Cutover requires:

- deterministic calculation and completeness tests passing;
- Event and Runtime authoritative boundaries approved and active for the target path;
- shadow results accepted for Rasht and Ramsar representative periods;
- every difference classified and no unexplained critical difference remaining;
- atomic snapshot/lock and source-freshness tests passing;
- finalized UI/PDF/other approved exporters proven to perform no operational reads;
- migration/reconciliation handling for existing legacy snapshots approved;
- rollback/feature-gate plan approved;
- explicit authorization to change production composition.

Until those conditions are met, legacy reports remain unchanged and authoritative only to the extent of current production behavior. The target subsystem must not silently replace them.

## 14. Known unresolved decisions

The following details are not inferred by this foundation specification:

- target snapshot database schema or serialization format;
- source-revision mechanism and transaction strategy for consistent reads;
- identity/version generation algorithms and any cryptographic evidence;
- exceptional completeness override policy, authorization, and audit;
- correction/reopen/supersession workflow for finalized snapshots;
- exact target rule for recycle/change analysis if legacy behavior is not adopted verbatim;
- which open-report export formats are exposed to users;
- CSV format conventions and whether CSV is in approved product scope;
- Excel workbook layout and Print UX;
- retention and archival policy for snapshots/export artifacts;
- compatibility rules for combining snapshots across explicitly approved policy upgrades.

These are future approval or implementation-design gates. They do not justify changing established behavior in the current system.

## 15. Verification and approval gate

This document creates no implementation. Phase 5.2 verification requires:

- `docs/reporting-domain-foundation-specification.md` exists;
- production C# and project files are unchanged by this phase;
- no database or schema file is changed;
- legacy Reporting, Event, Runtime, UI, export, finalization, and startup paths remain untouched;
- the Event vocabulary remains START, NSD, ESD, and OH; no generic STOP is introduced;
- authoritative Runtime remains integral minutes and two-decimal hours remain presentation-only;
- target finalized views/exports are specified as snapshot-only with no operational reads.

Approval of this foundation authorizes later isolated contract/design work only when separately requested. It does not authorize production activation, schema creation, or report replacement.
