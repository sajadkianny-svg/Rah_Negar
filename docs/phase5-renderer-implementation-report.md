# Phase 5.13 PDF and Excel Renderer Implementation Report

## Status

Phase 5.13 implements isolated in-memory PDF and Excel renderers for `FinalizedReportExportModel`. The renderers do not access databases, operational sources, legacy Reporting, UI, files, or download workflows, and they are not registered in production.

## Implemented components

`PdfReportRenderer` uses QuestPDF to render an A4 finalized-report document containing:

- a Station/period header;
- authoritative operational summaries;
- per-Unit Runtime summaries;
- per-Unit Event summaries;
- Snapshot identity, Report/schema/integrity versions, checksum, and generation evidence;
- Snapshot identity and deterministic pagination in the footer.

`ExcelReportRenderer` uses ClosedXML to create these sheets in fixed order:

1. `Summary`
2. `Runtime`
3. `Events`
4. `Daily`
5. `Evidence`

The workbook contains only values already present in the export model. It has no formulas or recalculation logic. Workbook properties use caller-supplied generation metadata. Sheet rows follow the export model's canonical ordering.

`DeterministicReportFileNamePolicy` derives safe filenames from Station identity, Persian period label, Report period kind, and snapshot schema version. The policy uses no clock, sequence, random value, user setting, or file-system state. Example:

```text
rasht_1405-05_Monthly_snapshot-format-v1.pdf
rasht_1405-05_Monthly_snapshot-format-v1.xlsx
```

`RenderedReport` now carries the suggested deterministic filename and defensively copies its byte content.

## Determinism

Both renderers consume the already sorted immutable export model and enumerate every section in its canonical order. Numeric and timestamp text uses invariant formats. Repeated PDF rendering preserves layout, content order, pagination, filename, and output size for the same model. PDF container-internal identifiers remain an implementation detail of QuestPDF and are not authoritative report content.

Excel additionally normalizes the Open Packaging Convention archive: entries are sorted, ZIP timestamps are fixed, volatile core-properties paths and relationship identifiers are canonicalized, and workbook timestamps come from generation metadata. Repeated Excel rendering is byte-for-byte identical for the same model.

## Metadata preservation

PDF evidence content and the Excel `Evidence` sheet include:

- `SnapshotId` and `ReportId` where applicable;
- Report calculation version;
- snapshot schema version;
- integrity version;
- checksum;
- generator version;
- caller-supplied generation timestamp;
- requesting actor identity.

Renderers do not regenerate, repair, or reinterpret this evidence.

## Tests

`ReportRendererTests` verifies:

- valid PDF generation and PDF signature;
- valid Excel generation and exact required sheet order;
- Snapshot, version, checksum, and generation metadata preservation;
- deterministic filenames, model ordering, repeated PDF layout characteristics, and byte-stable Excel output;
- successful rendering when no operational source dependency exists.

All renderer tests construct models from synthetic finalized snapshots and render entirely in memory.

## Limitations

- The PDF is a neutral technical report without production branding, Persian font embedding policy, or approved layout artwork.
- Excel styling is intentionally minimal and contains no charts, formulas, macros, pivots, or external links.
- There is no filesystem writer, UI command, download, print, email, or production composition-root registration.
- Very large report pagination and workbook-size performance require future acceptance tests with approved snapshot fixtures.
- Accessibility, translated labels, page orientation rules, and Station-specific presentation policy remain deferred.

## Isolation verification

Phase 5.13 changes only the Phase 5.12 export contracts, the new renderer implementations, isolated renderer tests, and this report. Legacy `Services/Reports`, `Models/Reports`, `Core/Reports`, operational adapters, database schema/migrations, UI, and production startup remain unchanged. No production export path is active.
