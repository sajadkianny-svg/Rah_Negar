# Phase 5.12 Snapshot-based Export Architecture Report

## Status

Phase 5.12 adds an isolated application-layer export architecture for finalized snapshots. It does not implement PDF or Excel rendering, register a production exporter, add UI, query operational data, mutate snapshots, or change legacy Reporting.

## Architecture

The export flow is deliberately one-way:

```text
FinalizedReportExportRequest
             |
             v
IFinalizedReportReader.GetEffectiveAsync
             |
             v
FoundValid finalized snapshot only
             |
             v
IReportExportValidator
             |
             v
Immutable, deterministically ordered FinalizedReportExportModel
             |
             +----> IPdfReportRenderer
             |
             +----> IExcelReportRenderer
```

`SnapshotReportExporter` never receives an hourly, daily, Event, Runtime, Settings, Station-profile, database, or legacy report dependency. It cannot fall back to operational calculation when finalized content is absent or invalid. All unsuccessful finalized-reader outcomes stop before model construction and rendering.

## Contracts

`Application/Reporting/Export` contains:

- `IReportExporter`, the application orchestration boundary;
- `FinalizedReportExportRequest`, including effective-period query, target format, and caller-supplied generation metadata;
- `FinalizedReportExportModel`, the immutable renderer input;
- `IPdfReportRenderer` and `IExcelReportRenderer`, format-specific render-only ports;
- `IReportExportValidator` and `ReportExportValidator`, the pure export eligibility gate;
- result, status, error, format, generation metadata, and rendered-artifact contracts.

The model includes Snapshot identity, Report identity, Station and period identity, ordered Unit identities, Report calculation version, snapshot schema version, integrity version and checksum, generation metadata, all finalized report sections, Event log, and warnings. Collections are copied into read-only arrays and sorted using ordinal or domain-defined ordering.

## Validation boundary

Export requires a reader result with `FoundValid`, which means Phase 5.11 has already resolved the effective period lock and validated stored checksum, canonical payload round trip, schema support, version support, and lock/snapshot identity consistency.

The pure export validator then requires:

- a calculated checksum with value and canonical payload length;
- explicitly supported snapshot and integrity versions;
- finalization-eligible completeness;
- complete Report, Event, Runtime, calendar, profile, and per-Unit version evidence.

Reader failures remain distinct as not found, not finalized, invalid integrity, unsupported integrity/version, lock mismatch, or infrastructure failure. Renderer exceptions produce a renderer failure without exposing a partial artifact.

## Renderer boundaries

Renderers receive only `FinalizedReportExportModel`. They have no calculation, database, reader, repository, lock, snapshot mutation, or operational source responsibility. A future renderer may translate the supplied values into bytes, but it must preserve authoritative values and deterministic collection order. Locale-specific presentation may format labels without recalculating report facts.

No concrete PDF or Excel renderer is included in this phase. Existing PDF/Excel packages and legacy exporters are not used or modified.

## Tests

`SnapshotReportExporterTests` covers:

- successful finalized snapshot export and metadata propagation;
- invalid checksum/integrity reader rejection before rendering;
- unsupported snapshot version rejection;
- successful export with no operational source dependency available;
- identical deterministic renderer input and output ordering across repeated exports.

Tests use synthetic finalized domain snapshots and in-memory test doubles. They do not open a database or invoke production Reporting.

## Limitations and future work

- No PDF or Excel document generation is implemented.
- No file system, download, print, email, or UI integration exists.
- No dependency-injection or production startup registration is added.
- Generation timestamps and actor identity are caller-supplied; the exporter has no clock or authentication dependency.
- The export model supports the current finalized snapshot schema only through explicitly configured version sets.
- Branding, pagination, fonts, localization, workbook layout, accessibility, and filename policy are deferred.
- A future renderer must be tested independently for byte-level determinism where its file format permits it.

## Isolation verification

Phase 5.12 changes only the new application export boundary, its isolated tests, and this report. Legacy `Services/Reports`, `Models/Reports`, `Core/Reports`, UI, operational adapters, SQLite schema/migrations, production startup, and existing export paths remain unchanged. No production export path is activated.
