# Reporting Architecture Specification

**Repository:** `D:\Projects\RahNegar_SQLite\Rah_Negar`  
**Document type:** Target architecture specification  
**Scope:** Generalized RahNegar reporting subsystem; documentation only

## 1. Executive overview

The target reporting subsystem is a versioned projection and evidence system. It turns authoritative, Station-scoped source data into typed report projections, optionally freezes complete projections as immutable finalized snapshots, and renders those projections or snapshots through presentation and export adapters. A report shown after finalization must be the same report that was finalized. It must not combine stored totals with newly queried event logs, extreme dates, settings, or runtime calculations.

The legacy reporting implementation cannot be reused directly because responsibilities are mixed across a large WinForms form, static calculation services, direct SQLite queries, partial snapshot tables, and export code. Its finalized views and PDF output read some values from snapshots and other values from live tables. The production runtime path also uses legacy Event calculation behavior rather than the approved complete-chain state machine. Snapshot identity lacks stable Station identity, source revision, calculation/projection/configuration versions, and checksum. These limitations prevent reliable multi-station isolation, historical reproduction, and safe evolution of calculation rules.

The following concepts are preserved:

- A compact Report Center with Persian period selection, grouped read-only results, Generate, Finalize, and Export workflows.
- Metadata-driven operational parameters and isolated Station profiles.
- Min/Max/Avg for applicable hourly observations and sums for applicable daily values.
- Weighted aggregation of finalized monthly averages using effective value counts.
- An explicit completeness result and a clearly marked provisional live-report option.
- Atomic snapshot-and-lock as the core finalization principle.
- PDF as the first formal export, with room for Excel, CSV, and printing.

The following components are replaced:

- UI-owned SQL, calculations, cache authority, and finalization orchestration.
- Dictionary-shaped report rows and display-name-based Station identity.
- Legacy Event initial-state/runtime calculations.
- Partial monthly snapshots and hybrid snapshot/live finalized views.
- Unversioned report calculations, settings, and export inputs.
- Lock keys that omit StationId and finalization flows that persist a stale preview.

The architecture remains suitable for the current offline SQLite WinForms deployment while defining stable boundaries for the generalized RahNegar platform. It does not require cloud services. Repository, transaction, and snapshot implementations may remain local, but domain and projection contracts must not depend on WinForms or SQLite.

## 2. Target reporting pipeline

```text
Source repositories
        ↓
Domain calculation layer
        ↓
Report projection layer
        ↓
Snapshot/finalization layer
        ↓
Presentation/export layer
```

### 2.1 Source repositories

Source repositories provide typed, read-only data required by a report. They are responsible for persistence access, Station scoping, canonical ordering, and mapping stored values to source records. They do not calculate report measures or decide whether a report may be finalized.

Required repository roles include:

- Operational observations by Station and operating-date range.
- Daily values by Station and operating-date range.
- Trusted Runtime Baselines by Station and Unit.
- Complete validated Event chains by Station and affected Units.
- Station and Unit configuration effective for the requested period.
- Reporting configuration and parameter definitions.
- finalized-period locks, SourceRevision records, and existing snapshot metadata.

All source reads for one generation attempt use one consistent transaction snapshot or one frozen SourceRevision. Repositories return stable identifiers and typed values. They never expose DataGridView objects, raw UI state, or arbitrary column dictionaries.

### 2.2 Domain calculation layer

The domain calculation layer owns deterministic business calculations independently of storage and presentation. It contains operational aggregators, daily-value aggregators, `RuntimeProjectionService`, service-day/longest-run logic, extreme-value calculation, recycle-transition calculation, and period roll-up rules.

Calculators accept explicit typed inputs and calculation policy/version. They return typed results plus evidence such as effective sample count, excluded/invalid data status, contributing dates, physical runtime intervals, and adjustment totals. They do not query the database, format Persian messages, create snapshots, or render exports.

### 2.3 Report projection layer

The projection layer coordinates calculators to create a complete `ReportProjection` for a `ReportRequest`. It selects required sources, validates request and source scope, invokes domain calculations, assigns deterministic section ordering, and attaches completeness, warnings, versions, and lineage.

Projection creation is the only supported route from source data to report content. UI and exporters do not reconstruct calculations independently. A live projection may be provisional; a projection eligible for finalization must be complete, internally consistent, and created from a frozen revision.

### 2.4 Snapshot/finalization layer

This layer converts an eligible projection into an immutable `ReportSnapshot`. It owns authorization, completeness enforcement, source revision freezing, transaction coordination, version/checksum persistence, period locking, and supersession metadata. It never accepts an old UI preview as authoritative without proving that its SourceRevision and versions still match.

The complete projection—including detailed evidence sections—is stored. After commit, finalized report reads use the snapshot only. Source repositories are not consulted to fill missing sections.

### 2.5 Presentation/export layer

Presentation maps projections and snapshots to screen view models. Export adapters render the same content to PDF and future formats. This layer owns layout, formatting, localization, Persian calendar display, paging, accessibility, and user interaction. It does not own SQL, business formulas, finalization eligibility, locks, or source selection.

## 3. Report domain model

### 3.1 ReportRequest

**Purpose:** Immutable description of the report the caller wants.

**Main fields:**

- `RequestId`: correlation identity for diagnostics; not a finalized identity.
- `StationId`: required stable Station identity from trusted application context.
- `ReportType`: target category or composed report definition.
- `Period`: canonical inclusive operating-date range and requested granularity.
- `UnitIds`: optional Station-owned Unit filter.
- `ParameterIds`: selected operational/daily parameter identities.
- `SectionSelection`: requested sections for a live report; final monthly definitions may require a fixed complete set.
- `Culture` and `Calendar`: display/output preferences, not source identities.
- `RequestedBy` and `RequestedAt`: actor and UTC request time.
- `RequestedCalculationVersion`/`RequestedProjectionVersion`: optional explicit version for reproducibility; otherwise current approved versions are resolved and recorded.
- `AllowProvisional`: permits a live incomplete report only; never permits finalization.

**Lifecycle:** Constructed by the application layer from validated UI input; validated once; passed unchanged to projection generation; embedded in projection lineage. It is never mutated by the UI after generation.

### 3.2 ReportProjection

**Purpose:** Complete typed result of one deterministic generation attempt against one SourceRevision and version set.

**Main fields:**

- `ProjectionId`, `Request`, `StationId`, and normalized `Period`.
- `GeneratedAt`, `GeneratedBy`, and generation correlation ID.
- `SourceRevision`, `CalculationVersion`, `ProjectionVersion`, `ConfigurationVersion`, and `SchemaVersion`.
- `CompletenessResult` and projection status (`Complete`, `Provisional`, `Failed`).
- Ordered collection of typed `ReportSection` objects.
- Warnings, source lineage summary, rounding/units metadata, and internal consistency results.
- Canonical content checksum candidate.

**Lifecycle:** Created in memory from one consistent source view. It may be displayed/exported as a clearly labeled live report. A complete monthly projection may become the content of a snapshot only within the finalization transaction after revision/version verification.

### 3.3 ReportSnapshot

**Purpose:** Immutable, fully reproducible record of a finalized report.

**Main fields:** Snapshot identity, ReportVersion, full ReportProjection payload/normalized section records, StationId, period, actor/time, source and version lineage, checksum, lock identity, status, and optional supersession links.

**Lifecycle:** Created once by `FinalizeReportCommand`; committed atomically with the period lock and audit entry. Normal operations cannot update or delete it. Reopening does not mutate it. A corrected finalization creates a new snapshot that supersedes the old version while retaining both.

### 3.4 ReportVersion

**Purpose:** Version of a finalized report identity, distinct from software calculation versions.

**Main fields:** `ReportId`, integer or monotonic `VersionNumber`, `SnapshotId`, status (`Current`, `Superseded`, optionally `Withdrawn`), `SupersedesSnapshotId`, reason, actor, and timestamp.

**Lifecycle:** Version 1 is created on first finalization. An authorized reopen releases the period for controlled correction without altering version 1. Re-finalization creates the next version and atomically marks the previous current version superseded. Historical retrieval can select any version; default retrieval returns current.

### 3.5 CalculationVersion

**Purpose:** Stable identifier for the set of domain formulas and policies used to calculate measures.

**Main fields:** identifier, semantic version, effective/approval metadata, component versions where useful (operational, runtime, recycle), policy parameters, and release notes/reference.

**Lifecycle:** Registered and approved before use. Existing snapshots retain their version forever. A new version does not recalculate or rewrite old snapshots.

### 3.6 SourceRevision

**Purpose:** Identifies the exact consistent source state from which a projection was generated.

**Main fields:** revision identity, StationId, created/frozen timestamp, constituent source watermarks or content hashes, Event/baseline/config revisions, and transaction/database identity.

**Lifecycle:** Live preview may capture a read revision. Finalization freezes or verifies a revision inside its transaction, generates against it, and stores it with the snapshot. SQLite implementation may use an application-maintained monotonic revision plus per-source hashes/watermarks; a connection-local transaction alone is not sufficient historical evidence after commit.

### 3.7 CompletenessResult

**Purpose:** One authoritative, structured statement of report readiness.

**Main fields:** StationId, period, status, required/observed hourly slots, required/observed daily values, missing/duplicate/invalid items, Event-chain validity, baseline availability, finalized-dependency status, error/warning codes, and `IsEligibleForFinalization`.

**Lifecycle:** Calculated from the same SourceRevision as the projection. It travels with live projections and is stored in snapshots. UI displays it but cannot override finalization eligibility.

### 3.8 ReportSection

**Purpose:** Typed, ordered, self-describing part of a projection or snapshot.

**Main fields:** stable section type/ID, title resource key, schema version, row/item type, values with units and precision, evidence/lineage references, sort definition, confidentiality/visibility metadata, and section-specific warnings.

**Lifecycle:** Created by a dedicated section projector from domain outputs. It is immutable once added to a projection. Snapshot serialization preserves section type and version so old sections remain readable after new fields are introduced.

## 4. Report types

| Report category | Source data | Calculation owner | Projection owner | Snapshot requirement | UI requirement |
|---|---|---|---|---|---|
| Operational Summary | Station-scoped hourly operational observations | `OperationalAggregationService` | `OperationalSummaryProjector` | Required as a section of finalized monthly reports; optional for live reports | Typed Min/Max/Avg grid with units, sample counts, warnings |
| Daily Values Summary | Station-scoped daily unique values | `DailyValueAggregationService` | `DailyValuesSummaryProjector` | Required when configured for the Station/month | Sum grid with units, contributing-day count, missing-day evidence |
| Runtime/Event Report | Trusted Runtime Baselines plus complete validated Event chains | `RuntimeProjectionService` and Event state machine | `RuntimeEventReportProjector` | Required in finalized reports when Station has Units; store totals and event evidence | Unit summary, chronological Event log, separate physical/ESD/adjusted values |
| Service Day Report | Physical run intervals from Runtime projection | `RuntimeProjectionService` | `ServiceDayReportProjector` | Required with Unit runtime section | Per-Unit days and daily Unit combinations; zero-service days explicit |
| Extreme Values Report | Valid hourly operational observations | `ExtremeValueCalculationService` | `ExtremeValuesProjector` | Required if included by report definition; persist all tied dates/value evidence | Parameter-grouped min/max values and all occurrence dates |
| Recycle Report | Ordered recycle observations and approved zero/tolerance policy | `RecycleCalculationService` | `RecycleReportProjector` | Required if Station configuration enables it | Transition count plus policy/version and optional transition evidence |
| Final Monthly Report | All required monthly section projections and completeness | Individual domain owners composed by `MonthlyReportProjectionService` | `MonthlyReportProjector` | Always immutable and complete | Finalized badge, version, lineage, no provisional state |
| Period Aggregated Report | Current finalized monthly snapshots for every included month | `PeriodAggregationService` | `PeriodReportProjector` | Store only if business requires period finalization; otherwise reproducible derived projection records input snapshot IDs | Half/year/custom-period selectors; show contributing snapshot versions |
| PDF Export | A ReportProjection for live/provisional output or ReportSnapshot for finalized output | No business calculation; formatting only | `PdfReportRenderer` | Final PDF metadata must identify snapshot/version/checksum | Save/preview/print-friendly, localized, deterministic pagination |

For period aggregation, Min is the minimum of monthly minima, Max the maximum of monthly maxima, Sum the sum of monthly sums, and Avg is weighted by effective value counts. The projector must record the exact contributing snapshot IDs. It may not mix a live month with finalized months under a “finalized period” label.

## 5. Report Projection architecture

### 5.1 Typed and Station-scoped projections

Every projector exposes a typed contract. Examples include `OperationalSummarySection`, `DailyValuesSection`, `UnitRuntimeSection`, `EventTimelineSection`, `ServiceDaysSection`, `ExtremeValuesSection`, and `RecycleSection`. Each numeric result includes unit, precision/rounding policy, value count, and availability status. Unknown or invalid source values produce structured issues; they are not silently dropped or coerced.

Every request, source query, calculation context, projection, section, snapshot, lock, and export carries `StationId`. Unit filters are validated against that Station. Display names remain localizable labels only. A report cannot infer Station ownership from Unit text, database filename, or active-form caption.

Projection schema and calculation output versions are explicit. Adding a field or changing section structure increments `ProjectionVersion`; changing a formula or policy increments `CalculationVersion`. A renderer declares which section versions it supports.

### 5.2 Generation flow

```text
ReportRequest + trusted user/Station context
                    ↓
Request, ownership, period, and version validation
                    ↓
Consistent typed source load + CompletenessResult
                    ↓
Domain calculations
                    ↓
Typed section projection and cross-section invariants
                    ↓
ReportProjection
          ↙                         ↘
live presentation/export       finalization/snapshot
```

Exact application workflow:

1. Validate request shape, permissions, Station/Unit ownership, date range, parameter availability, and supported versions.
2. Open a consistent read transaction or finalization transaction and identify SourceRevision.
3. Load only the typed sources required by the report definition, always Station-scoped.
4. Calculate `CompletenessResult` once from that revision.
5. If the request is not allowed provisionally and completeness fails, return structured failure without a projection.
6. Run domain calculators using explicit policy/version contexts.
7. Build typed sections in deterministic order.
8. Validate cross-section invariants: adjusted runtime equals physical plus ESD adjustment; event totals match timeline; service days derive from physical overlap; aggregate counts reconcile.
9. Canonically serialize the projection and compute its checksum candidate.
10. Return the projection for display/export, or pass it to finalization under the same verified revision.

UI has no repository reference. Repository implementations have no dependency on projections or WinForms. Calculators have no repository or renderer reference. Exporters accept projection/snapshot contracts only.

## 6. Finalization architecture

### 6.1 FinalizeReportCommand

`FinalizeReportCommand` contains StationId from trusted context, target monthly period, expected source/configuration revision where a preview is being confirmed, requested report definition, actor, authorization context, reason, and optional expected current ReportVersion. It does not contain precomputed totals as authority.

The command handler performs the following in one transaction:

1. **Validate permissions.** Confirm actor may finalize this Station and period; validate Station is active and report definition approved.
2. **Validate completeness.** Run the authoritative `CompletenessService` against the transaction revision. Provisional overrides are rejected.
3. **Freeze source revision.** Allocate/verify a SourceRevision covering observations, daily values, Events, baselines, configuration, and relevant locks.
4. **Generate complete projection.** Load and calculate all required sections within the same consistent transaction view. If a UI preview revision still matches, it may be used only as a performance hint; authoritative content is regenerated or checksum-verified.
5. **Store immutable snapshot.** Persist header, complete canonical projection/sections, evidence, completeness, and lineage.
6. **Store versions and checksum.** Persist schema, calculation, projection, configuration, source revision, canonical checksum, report version, actor, and timestamps.
7. **Lock period.** Create the Station+period lock referencing the new SnapshotId/ReportVersion, append finalization audit, and commit.

Any failure rolls back snapshot, version, audit, and lock. Success is not returned before commit.

### 6.2 Finalized reads

After finalization, the Report Center and every exporter load the selected `ReportSnapshot`. They do not read Events, hourly observations, daily values, runtime baselines, or current configuration to fill a section. Current display resource strings may be applied only where doing so does not alter recorded business content; the snapshot retains original label/resource keys and rendering metadata needed for faithful reproduction.

### 6.3 Reopen and supersession

Reopen is a separate authorized command requiring reason and audit. It never edits or deletes the old snapshot. It changes period governance state so approved source corrections can occur, records which finalized versions may be affected, and enforces cross-period Event dependencies. The safe default rejects changes that would affect a later locked runtime snapshot until every affected period is included in the authorized reopen set.

Re-finalization generates a new complete snapshot and next ReportVersion. On commit it marks the prior current version `Superseded`, links both versions, and restores the lock to the new current snapshot. Historical users can retrieve the original, reason, correction audit, and new version.

## 7. Snapshot design

Every snapshot must contain:

- Stable `SnapshotId`, `ReportId`, and `ReportVersion`.
- `StationId`; optional denormalized Station display data as recorded presentation metadata.
- Exact operating period and granularity.
- Report type/definition identity.
- `SchemaVersion`, `ProjectionVersion`, `CalculationVersion`, and `ConfigurationVersion`.
- `SourceRevision` and constituent source lineage/watermarks.
- UTC generated/finalized timestamps and explicit display time-zone/calendar context.
- Generating/finalizing user identity and finalization reason.
- Canonical serialization format and cryptographic checksum.
- Complete `CompletenessResult`.
- Every required `ReportSection`, including detailed event timeline, occurrence dates, service-day combinations, counts, units, rounding metadata, warnings, and evidence needed by export.
- Supersession/status metadata and lock reference.

Snapshot storage may use normalized header/section/evidence tables, a canonical versioned document payload, or both. A normalized model supports indexing and queries; a canonical payload supports exact reproduction. If both are stored, the canonical payload/checksum is authoritative and normalized rows must be validated against it inside finalization.

The checksum is computed over canonical business content and lineage, not over a PDF binary whose rendering metadata may vary. An exported file records SnapshotId, ReportVersion, and snapshot checksum; an optional separate export checksum may cover the output bytes.

Reproducibility means that, given a snapshot and a compatible renderer, the system can display and export the same business values, evidence, units, ordering, and recorded labels without reading mutable operational tables or applying current formulas. This is essential for audit, migration, dispute resolution, and comparison of calculation versions.

## 8. Completeness architecture

`CompletenessService` is a domain/application service with typed source inputs and one approved definition per report/configuration version. It replaces independent UI, notification, and finalization completeness checks.

Responsibilities:

- Verify the required hourly slots for every applicable operating day from `data_start_date` onward, including uniqueness and canonical time validity.
- Verify required daily values and distinguish optional fields from required Station-specific values.
- Report missing, duplicate, malformed, out-of-range, and unexpected observations.
- Verify Event chain validity status and Trusted Runtime Baseline availability for runtime sections. Events remain optional for daily data completeness; a no-Event day can be complete.
- Evaluate finalized-period and cross-period dependency conditions relevant to eligibility.
- Produce exact per-day/per-parameter structured issues and an unambiguous `IsEligibleForFinalization`.

Live reports may be generated provisionally only when the caller is authorized by product policy and the result is visibly marked in UI and export. Event-chain invalidity or ambiguous identity is not silently overridden; affected calculations fail explicitly. Finalized reports require the authoritative result to be eligible. Main-window readiness notification, Report Center warning, command handler, and tests consume the same service result.

## 9. Runtime report integration

`RuntimeProjectionService` is the sole runtime calculator used by reports. Its inputs are:

```text
Trusted Runtime Baseline
          +
complete chronologically ordered, validated Event chain
```

Its outputs are separately auditable:

- **Physical Runtime:** positive elapsed Running overlap, period-clipped.
- **ESD Adjustment:** configured adjustment only for valid Running → ESD → Stopped transitions.
- **Adjusted Runtime:** Physical Runtime plus ESD Adjustment.
- **Runtime After OH:** cumulative applicable physical runtime and ESD adjustment since the latest valid OH reset.
- **Service Days:** operating days with any positive physical Running overlap in `[00:00, next 00:00)`.
- **Longest Run:** longest physical Running interval overlap within the report period; ESD adjustment excluded.

OH does not reset cumulative runtime, does reset Runtime After OH, and cannot terminate a Running Unit because Running+OH is invalid. ESD adjustment creates no ServiceDay and does not extend Longest Run. Initial Running Units come from the baseline; the projector does not invent historical START Events.

Hourly ST/RPM observations are operational report inputs only. They neither determine runtime nor cross-validate Event runtime. A report may display ST/RPM and runtime in separate sections, but no calculation dependency may flow from observations into runtime.

The runtime section records baseline identity/version, Event-chain revision, accepted Event IDs, physical interval evidence, ESD policy/version, and any rejected-chain error. This supports reconciliation without storing runtime back into Event rows.

## 10. Multi-station architecture

`StationId` is a required immutable identity in:

- ReportRequest and every source repository call.
- ReportProjection and each section/evidence record.
- ReportSnapshot, ReportVersion identity, and SourceRevision.
- Period locks and reopen/finalization audit.
- Unit ownership validation and runtime baseline selection.
- Report Center filters, authorization context, cache keys, export metadata, and filenames.

Snapshot/lock uniqueness is at least `(StationId, ReportType, Period, ReportVersion)`; the current version constraint is scoped by Station and report identity. UnitId is checked through Station ownership. Cross-station aggregation is a distinct report type that lists all contributing StationIds and cannot masquerade as a Station report.

Display names cannot be identities because they are editable, localizable, not necessarily unique, and historically variable. “Rasht Station” and “Ramsar Station” remain labels/profile mappings, not database keys. Station-specific fields and required-data rules are selected by stable Station profile/configuration version, preserving isolation while allowing future stations.

## 11. UI architecture

### 11.1 Report Center responsibilities

The Report Center:

- collects Station, period, granularity, Unit, parameter, section, language, and output selections;
- submits generation, finalization, reopen, snapshot-load, and export commands/queries;
- shows progress/cancellation state without holding a database transaction open for user interaction;
- displays structured completeness and validation errors in Persian or selected locale;
- binds typed section view models to read-only grids/charts;
- clearly distinguishes Live, Provisional, Finalized, and Superseded versions;
- displays snapshot/calculation version and source lineage appropriate to the user's role.

It must not own SQL, source-table names, business calculations, Event-state interpretation, completeness authority, cache validity rules, snapshot persistence, transaction control, or lock decisions.

### 11.2 Interaction and display principles

- Preserve compact Monthly/first-half/second-half/Yearly shortcuts and add explicit Daily/custom-range selection where required.
- Station and Unit selectors use stable IDs and localized labels; filters must never widen authorization scope.
- Grids are read-only, chronologically deterministic, virtualized/paged where volume requires, keyboard navigable, and preserve meaningful grouping.
- Generate, Finalize, Reopen, and Export are distinct commands with clear confirmation and status feedback.
- Incomplete/provisional output has persistent visual marking, not a hidden Shift-only convention.
- Persian dates use a shared calendar abstraction for parsing, validation, range construction, and formatting. Canonical values remain separate from display strings.
- All text uses resource keys and supports Persian right-to-left layout plus required English official report output. Mixed hard-coded language is prohibited.
- WinForms implementation uses DPI-aware layout containers, AutoScaleMode appropriate to the application, scalable fonts, minimum sizes, and tests at 100–200% scaling. Fixed pixel layout is limited to elements proven safe by visual tests.

UI caching may cache immutable projections by the full request, SourceRevision, versions, and culture. A change to any source/config/version invalidates a live cache entry. Snapshots are cached by SnapshotId/checksum because their content is immutable.

## 12. Export architecture

All export adapters implement a conceptual `IReportExporter<TOptions>` and accept only `ReportProjection` or `ReportSnapshot`. They never query raw tables or call domain calculators.

### 12.1 PDF

PDF is the first required formal renderer. For finalized output it consumes only a ReportSnapshot and includes Station, period, ReportVersion, generated/finalized timestamps, calculation/projection versions, and snapshot checksum or verification code. It renders every selected snapshot section consistently, including Event remarks where policy permits, OH evidence, units, rounding, and repeated headers. PDF generation is deterministic in business content and supports Persian fonts/glyphs, right-to-left text, pagination, preview, Save As, and print handoff.

A live projection may be exported only if policy permits and must be labeled `LIVE` or `PROVISIONAL`; it cannot carry a finalized designation.

### 12.2 Future Excel, CSV, and Print

- **Excel:** typed sheets per section, machine-readable values, units, lineage/version sheet, and localized display labels.
- **CSV:** one well-defined section/schema per file or a documented package; invariant machine values with encoding/locale metadata.
- **Print:** renders an existing PDF or print-specific presentation of the same projection/snapshot; it does not recalculate.

Every exporter declares supported ProjectionVersion/section versions. Unsupported content fails clearly rather than omitting sections. Export audit records SnapshotId/ProjectionId, format, renderer version, actor, time, options, and output checksum/path metadata without storing sensitive filesystem details unnecessarily.

## 13. Versioning strategy

| Version | Meaning | Change trigger | Historical rule |
|---|---|---|---|
| `SchemaVersion` | Persistence/canonical payload structure | Tables, serialized field contracts, identity or constraint representation changes | Reader/migrator retains support or explicitly converts a copy; snapshot content is never silently rewritten |
| `CalculationVersion` | Formula and business-policy semantics | Runtime transition/effects, aggregation, completeness, recycle tolerance, rounding-before-calculation changes | Old snapshot keeps old result/version; comparisons label semantic differences |
| `ProjectionVersion` | Report section composition and typed output schema | Added/removed/renamed fields, section ordering/meaning, lineage representation | Renderer selects a compatible adapter; missing support is explicit |
| `ConfigurationVersion` | Station/Unit/parameter/report-definition settings | Effective required parameters, units, baseline/config policy, report layout definition affecting content | Snapshot records exact version/effective configuration used |

Application build and renderer versions may also be recorded, but they do not replace the four business/persistence versions. Versions are immutable identifiers backed by approved metadata. “Latest” is resolved at generation time and stored explicitly.

Historical reproducibility requires retaining compatible readers/renderers or a validated non-destructive conversion path. New versions never cause background recalculation of finalized reports. A comparison tool may generate a new live projection from preserved sources and explain differences, but it does not alter old evidence.

## 14. Testing strategy

### 14.1 Unit tests

- Operational Min/Max/Avg/Sum, null/invalid policy, effective counts, units, precision, and weighted period average.
- Daily-value aggregation and Station-specific requirements.
- Runtime state transition matrix and full-chain projection: physical runtime, ESD adjustment, adjusted runtime, after-OH, service days, longest run, midnight clipping, day/night event shifts.
- Extreme ties and occurrence dates; recycle threshold transitions.
- Completeness for 12 required odd-hour observations, duplicates, malformed values, missing daily data, optional Events, invalid Event chains, and `data_start_date`.
- Each section projector maps typed results, evidence, warnings, units, and deterministic ordering correctly.
- Canonical serialization/checksum stability and version compatibility behavior.

### 14.2 Integration tests

- Every repository enforces Station scope and returns deterministic typed ordering.
- SQLite foreign keys/indexes/unique constraints and transaction isolation support required queries.
- SourceRevision changes for every report-affecting source/configuration mutation.
- Finalize transaction atomically stores complete snapshot/version/audit and lock; injected failure at each step rolls back all.
- A stale preview/source revision cannot be finalized.
- Finalized reads and exporters make zero raw-source queries.
- Reopen and superseding finalization preserve old versions and enforce cross-period runtime dependencies.
- Concurrent finalize attempts produce one current version without partial snapshots.

### 14.3 Scenario and acceptance tests

- Persian month lengths, leap/non-leap Esfand, midnight, month/year transitions, partial first month, half-year and yearly periods.
- Rasht/Ramsar Station isolation with identical dates and Unit labels; no data leakage.
- Same-time Events across different Units versus prohibited duplicate timestamp for one Unit.
- Earlier Event changes affecting later runtime and locked-period governance.
- Finalized immutability after source, baseline, configuration, localization, and current calculation version changes.
- Period aggregate consumes the recorded monthly snapshot versions and computes weighted averages correctly.
- UI status and authoritative finalization eligibility always agree.
- PDF values/evidence/checksum reconcile exactly to snapshot; Excel/CSV/Print use the same contract when introduced.
- DPI 100%, 125%, 150%, 175%, 200%; Persian RTL; keyboard-only operation; large Event timeline; cancellation and error recovery.

Golden fixtures must include valid and intentionally invalid legacy chains, known monthly observations, finalized legacy evidence, and approved new-engine expectations. Golden exports assert business content and stable layout regions without relying solely on fragile byte equality.

## 15. Migration strategy

Legacy reports and PDFs remain historical evidence. Existing finalized records are tagged with a legacy report/calculation provenance and retained read-only. They are not silently recalculated, overwritten, or relabeled as outputs of the approved architecture.

Migration proceeds through inventory, extraction, validation, reconciliation, and reviewed import:

1. Back up each Rasht/Ramsar database and identify its stable target StationId.
2. Inventory legacy lock/header/summary/unit-event/service rows, existing PDF artifacts, live source tables, baselines, and application settings.
3. Detect missing snapshot sections, duplicate hourly samples, duplicate Event timestamps, invalid dates/times/types, incomplete months, and lock/snapshot mismatches.
4. Preserve available legacy snapshot payload and source evidence with `LegacyCalculationVersion`/`LegacyProjectionVersion`; record which sections were live-derived and therefore not immutable.
5. Recalculate copies with both the actual legacy production engine and approved new engine where sources permit.
6. Classify every difference as confirmed legacy defect, approved-rule change, corrupt/ambiguous source, baseline/configuration difference, rounding/format difference, or unexplained discrepancy requiring human review.
7. Import only reviewed identities and evidence. Do not manufacture missing historical START Events, baselines, event ordering, or finalized details.
8. Validate counts, totals, lineage, checksums, Station isolation, and sample rendered reports before any cutover.

A migrated legacy snapshot may be displayable but not claim full target reproducibility if its original content was partial. The UI labels evidence quality and provenance. New finalization begins only after source revision tracking, complete snapshots, Station-scoped locks, and approved calculators are active. Migration is non-destructive and reversible; the original databases remain retained according to backup/retention policy.

## 16. Final architecture decision table

| Component | Decision | Reason | Implementation impact |
|---|---|---|---|
| Reporting pipeline | Enforce repository → domain calculation → projection → snapshot → presentation/export | Separates authority, calculation, persistence, and rendering | Introduce explicit interfaces/DTOs and dependency injection/composition |
| Source access | Typed, Station-scoped repositories under one consistent revision | Prevents table/UI coupling and mixed-time reports | Replace dictionary queries; add revision-aware transaction context |
| ReportRequest | Immutable request with StationId, period, filters, versions, locale | Complete cache/audit identity | UI maps controls to command DTO; validation centralized |
| Calculations | Pure versioned domain services | Enables tests and historical meaning | Move recycle/runtime/aggregation logic below UI; register versions |
| Runtime authority | Trusted Runtime Baseline plus validated Events only | Implements approved Event rules; isolates ST/RPM | Replace legacy runtime report path and reconcile history |
| Projection | Typed immutable sections with evidence and invariants | Makes UI/export consumers safe and explicit | Create per-section projectors and compatibility contracts |
| Completeness | One authoritative CompletenessService | Eliminates conflicting readiness/finalization rules | Main notification, UI, and finalize handler share one result |
| Live report | Projection tied to SourceRevision; may be visibly provisional | Supports operations without weakening finalization | Add status/warnings and full cache key |
| Monthly finalization | Regenerate/verify complete projection inside one transaction | Eliminates stale preview and partial snapshot risks | Implement `FinalizeReportCommandHandler` and unit-of-work boundary |
| Snapshot | Store all sections, evidence, versions, revision, actor, checksum | Guarantees finalized reproduction without live reads | New immutable snapshot schema/payload and validation |
| Finalized read | Snapshot only; zero raw-source fallback | Preserves immutability and internal consistency | Separate snapshot query service and prohibit hybrid paths |
| Reopen | Audited governance command; old snapshot retained | Protects evidence while allowing correction | Add period state, affected-period analysis, reason/authorization |
| Supersession | New ReportVersion linked to prior snapshot | Avoids mutation and supports audit | Version/current-status constraints and retrieval UI |
| Multi-station identity | Require StationId everywhere; names are labels | Prevents collisions and Rasht/Ramsar leakage | Update keys, filters, repositories, locks, snapshot IDs |
| Period aggregation | Consume explicit current monthly SnapshotIds; weighted Avg | Reproducible and mathematically correct | Store contributing versions and effective counts |
| Report Center | Thin presentation/application client | Removes SQL/calculation/finalization authority from form | Preserve workflow while replacing internals/view models |
| Persian calendar | Shared validated calendar/date abstraction | Prevents divergent boundary logic | Canonical period model plus localized formatter/parser |
| Localization/DPI | Resource-based RTL/LTR UI and DPI-aware layout | Generalized usable presentation | Replace hard-coded strings/fixed layout; visual test matrix |
| Export | Render projection/snapshot only | Prevents PDF/live-table inconsistency | PDF adapter first; common exporter contract for future formats |
| Versioning | Persist Schema, Calculation, Projection, Configuration versions | Explains historical results across evolution | Version registry, compatibility readers, snapshot metadata |
| Migration | Preserve legacy evidence; reconcile rather than overwrite | Avoids false history and destructive conversion | Read-only extraction, anomaly inventory, reviewed mapping |
| Direct database bypass | Application commands plus constraints, audit, file protections | SQLite local files lack a complete role boundary | Encapsulate writes; integrity checks and controlled maintenance tools |

This specification defines the architecture boundary and required behavior only. It does not authorize production changes, schema migrations, or implementation code.
