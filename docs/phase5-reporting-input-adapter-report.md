# Phase 5.4 Reporting Input Adapter Report

## Status

Phase 5.4 is implemented as an isolated application-layer contract and composition boundary under `Application/Reporting/Input`. It adds no production registration and does not connect the target Reporting domain to legacy Reporting, UI, SQLite, or any other persistence mechanism.

## Architecture

```text
Future read-only source implementations
  |-- hourly operational adapter
  |-- daily unique adapter
  |-- authoritative Event projection adapter
  |-- authoritative Runtime projection adapter
  `-- Station profile adapter
                    |
                    v
          IReportInputComposer
       identity/version validation
       deterministic normalization
                    |
                    v
          NormalizedReportInput
                    |
                    v
       Phase 5.3 report calculator
```

Adapters receive one typed Station/half-open-period/Unit-set request. Their outputs contain normalized domain values together with explicit Station, period, Unit where applicable, source identity/revision, version metadata, and completeness evidence. Persistence-specific objects cannot cross this boundary.

`ReportInputComposer` starts all five independent adapter reads, collects their results, validates alignment, and either returns a complete `NormalizedReportInput` or a deterministic list of structured failures. The calculation timestamp is supplied by the caller; the composer does not read a clock.

## Contracts

- `IHourlyDataReportingAdapter` returns normalized hourly values, Station/period identity, source evidence, and hourly completeness.
- `IDailyDataReportingAdapter` returns normalized daily values, Station/period identity, source evidence, and daily completeness.
- `IEventProjectionReportingAdapter` returns one authoritative Event projection per Unit with chain/policy versions and validation state.
- `IRuntimeProjectionReportingAdapter` returns one authoritative Runtime projection per Unit with calculation, policy, Baseline, and configuration versions and integral-minute metrics.
- `IStationProfileReportingAdapter` returns Station identity, configured Units, typed parameters, calendar/profile evidence, and report version families.
- `IReportInputComposer` composes these outputs through `ReportInputCompositionRequest` and `ReportInputCompositionResult`.

Adapter failures use `ReportingAdapterResult<T>` and `ReportingInputFailure`. Failure kinds explicitly distinguish `MissingSource`, `IncompatibleVersion`, `WrongStation`, `WrongPeriod`, and `MissingUnit`.

## Validation and deterministic behavior

- Every adapter Station must equal the requested canonical Station.
- Hourly, daily, Event, and Runtime periods must equal the requested half-open period.
- Exactly one Event and Runtime projection must exist for every requested Unit; every Unit must exist in the Station profile.
- Unexpected Unit outputs are rejected.
- All outputs must share one consistent source revision.
- Event policy versions must agree across Units.
- Runtime calculation and policy versions must agree across Units.
- Hourly and daily completeness results must identify their correct dimensions.
- Parameters, hourly values, daily values, Unit projections, and Events are copied into deterministic ordinal/chronological order.
- Missing or incompatible inputs never produce a partial `NormalizedReportInput`.

## Boundaries

The application contracts reference only Phase 5.3 projection-domain types and .NET abstractions. They contain no SQL, SQLite connection, table, row, DataSet, DataTable, UI control, filesystem, exporter, snapshot, lock, or service-registration dependency. Event validity and Runtime calculations remain owned by their authoritative domains; the composer copies their outputs and evidence.

The namespace follows the existing application-layer convention, `Rah_Negar.Foundation.Application.Reporting.Input`, while the physical location remains the requested `Application/Reporting/Input`.

## Tests

`Rah_Negar.Tests/Reporting/ReportingInputComposerTests.cs` covers:

- valid composition with evidence and version propagation;
- wrong Station;
- wrong period;
- missing Runtime Unit;
- missing Event Unit;
- incompatible Runtime versions;
- explicit missing-source behavior;
- deterministic ordering and caller-supplied timestamp preservation.

## Limitations

- No adapter has a production implementation.
- No SQLite repository, query, transaction, schema, migration, or database registration is included.
- No dependency-injection or startup registration is included.
- No legacy report or UI path consumes the composer.
- A future source implementation must define how it obtains one consistent source revision without leaking transaction/database objects into these contracts.
- Persian range enumeration and the 12-slot hourly completeness algorithm remain future upstream adapter/completeness work.
- Snapshot persistence, finalization, locking, shadow comparison, production cutover, and export remain outside Phase 5.4.

## Future implementation notes

Future adapter implementations should be read-only, station-specific where required, and normalize persistence representations before returning. They should return `MissingSource` rather than throwing for expected absence/unavailability, preserve authoritative Event and Runtime versions without reinterpretation, honor cancellation, and obtain outputs from one consistent-read boundary. Production wiring requires a separately approved phase after source implementation and shadow validation.

## Isolation verification

No file under legacy `Services/Reports`, `Models/Reports`, `Core/Reports`, `UI`, or `Data` was modified for Phase 5.4. No database or schema file was created or changed.
