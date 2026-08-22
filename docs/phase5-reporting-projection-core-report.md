# Phase 5.3 Reporting Projection Core Report

## Status

Phase 5.3 is implemented as an isolated domain slice under `Core/Reporting/Projection`. It is not registered in production startup and does not replace or call legacy Reporting.

## Architecture

`NormalizedReportInput` is the single pure-calculation input boundary. It accepts only typed, normalized operational values and authoritative Event/Runtime results, together with report identity, completeness, evidence, versions, and a caller-supplied calculation timestamp.

`IReportCalculator` exposes a deterministic calculation operation. `ReportCalculator` performs no IO, database access, clock reads, persistence, locking, export, or UI work. It validates identity alignment and version evidence, gates projection state through the supplied completeness authority, and constructs deterministically ordered immutable output.

The output is `ReportProjection`. Ordinary input conflicts produce a `Rejected` projection with stable blocking codes. Valid but incomplete evidence produces `Incomplete`; only complete evidence produces `Complete` and is eligible for future finalization.

## Created contracts

- Identity and state: `ReportIdentity`, report period/source/status enums.
- Evidence and versions: `ReportEvidence`, `ReportVersionSet` with distinct report, snapshot, Event, Runtime, baseline, configuration, and calendar version families.
- Completeness: `ReportCompletenessResult`, dimension results, four completeness states, and deterministic issues.
- Normalized inputs: parameter definitions, hourly values, daily values, authoritative Event inputs, and authoritative Runtime inputs.
- Sections: `OperationalSummary`, `DailySummary`, `RuntimeSummary`, `EventSummary`, `ReportEvent`, `ServiceSummary`, and `ExtremeDateSummary`.
- Calculation: `IReportCalculator`, `ReportCalculator`, and immutable `ReportProjection`.

The calculator currently supports approved hourly minimum/maximum/average, daily sum, authoritative Runtime copying, Event counts/log composition, service composition, and extreme-date composition. Runtime values remain integral minutes.

## Validation

- Report identity requires nonblank identifiers, a non-empty half-open period, at least one Unit, and no duplicate Units. Unit order is canonical ordinal order.
- Every configured Unit requires exactly one authoritative Event input and Runtime input matching Station and period.
- Event chains must already be validated; Reporting does not repair or reinterpret them.
- Runtime component and version evidence must match the supplied authoritative values.
- All required version families must be present. Missing or mismatched evidence rejects calculation.
- Projection rows, logs, warnings, blocking reasons, dates, and Units use explicit deterministic ordering.

## Tests

`Rah_Negar.Tests/Reporting/ReportingProjectionCoreTests.cs` covers:

- identity validation and canonical Unit ordering;
- Complete, Incomplete, Invalid, and Unavailable completeness states;
- finalization gating;
- missing required version rejection;
- projection immutability against source collection mutation;
- deterministic ordering, values, and caller-supplied timestamps;
- valid incomplete projection behavior.

## Limitations

- There is no repository, SQLite adapter, database/schema change, or source-revision implementation.
- There is no snapshot model or persistence, finalization/locking workflow, exporter, UI integration, dependency injection registration, or production cutover.
- Completeness evidence is supplied by an upstream authority; Persian calendar enumeration and validation of the 12 required odd-hour observations remain outside this isolated core.
- Event validity and Runtime calculation remain authoritative in their own domains.
- Recycle/change semantics and station-level service-combination rules remain unresolved and are not invented here.
- Snapshot compatibility/aggregation rules and completeness override policy remain future approval gates.

## Isolation verification

No file under legacy `Services/Reports`, `Models/Reports`, `Core/Reports`, `UI`, or `Data` was modified for Phase 5.3. No database or schema file was created or changed.
