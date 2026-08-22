using Rah_Negar.Core.Event;
using Rah_Negar.Core.Reporting.Projection;
using Rah_Negar.Core.Runtime;
using Rah_Negar.Foundation.Application.Reporting.Input;

namespace Rah_Negar.Tests.Reporting.Synthetic;

internal enum SyntheticReportingScenario
{
    Complete,
    MissingHourlyData,
    VersionMismatch,
    InvalidEventUnitAlignment
}

internal sealed record SyntheticPipelineResult(
    ReportInputCompositionResult Composition,
    ReportProjection? Projection);

internal sealed class SyntheticReportingFixture
{
    private static readonly DateTimeOffset CalculationTimestamp =
        new(2026, 8, 22, 12, 0, 0, TimeSpan.FromHours(3.5));

    public async Task<SyntheticPipelineResult> RunAsync(SyntheticReportingScenario scenario)
    {
        var sources = new SyntheticReportingSources(scenario);
        IReportInputComposer composer = new ReportInputComposer(sources, sources, sources, sources, sources);
        ReportInputCompositionResult composition = await composer.ComposeAsync(new(
            new ReportIdentity("synthetic-report-1405-05", "rasht", "Rasht Synthetic Station",
                10_000, 53_200, "1405/05", ReportPeriodKind.Monthly,
                ["unit-2", "unit-1"], ReportSourceMode.OpenProjection),
            CalculationTimestamp));
        if (!composition.IsSuccess) return new(composition, null);

        ReportProjection projection = new ReportCalculator().Calculate(composition.Input!);
        return new(composition, projection);
    }

    public static string Fingerprint(ReportProjection projection)
    {
        IEnumerable<string> parts =
        [
            projection.Identity.ReportId,
            projection.Identity.StationId,
            projection.Identity.PeriodStartMinute.ToString(System.Globalization.CultureInfo.InvariantCulture),
            projection.Identity.PeriodEndMinute.ToString(System.Globalization.CultureInfo.InvariantCulture),
            projection.Status.ToString(),
            projection.CalculationTimestamp.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            projection.Completeness.State.ToString(),
            projection.Evidence.SourceRevision,
            projection.Versions.ReportCalculationVersion
        ];
        parts = parts.Concat(projection.OperationalSummaries.Select(x =>
                $"O|{x.ParameterId}|{x.Aggregation}|{x.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{x.ContributingCount}"))
            .Concat(projection.DailySummaries.Select(x =>
                $"D|{x.ParameterId}|{x.Sum.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{x.ContributingCount}"))
            .Concat(projection.RuntimeSummaries.Select(x =>
                $"R|{x.UnitId}|{x.PhysicalRuntimeMinutes}|{x.EsdAdjustmentMinutes}|{x.AdjustedRuntimeMinutes}|{x.FinalState}"))
            .Concat(projection.EventLog.Select(x => $"E|{x.UnitId}|{x.EventMinute}|{x.SourceOrdinal}|{x.EventId}"))
            .Concat(projection.ExtremeDateSummaries.Select(x =>
                $"X|{x.ParameterId}|{x.Minimum}|{x.Maximum}|{string.Join(',', x.MinimumDates)}|{string.Join(',', x.MaximumDates)}"))
            .Concat(projection.Warnings.Select(x => $"W|{x}"))
            .Concat(projection.BlockingReasons.Select(x => $"B|{x}"));
        return string.Join('\n', parts);
    }

    private sealed class SyntheticReportingSources : IHourlyDataReportingAdapter,
        IDailyDataReportingAdapter, IEventProjectionReportingAdapter,
        IRuntimeProjectionReportingAdapter, IStationProfileReportingAdapter
    {
        private const string Station = "rasht";
        private const long Start = 10_000;
        private const long End = 53_200;
        private const string Revision = "synthetic-read-revision-v1";
        private readonly SyntheticReportingScenario _scenario;

        public SyntheticReportingSources(SyntheticReportingScenario scenario) => _scenario = scenario;

        Task<ReportingAdapterResult<HourlyDataReportingOutput>> IHourlyDataReportingAdapter.ReadAsync(
            ReportingInputRequest request, CancellationToken cancellationToken)
        {
            bool incomplete = _scenario == SyntheticReportingScenario.MissingHourlyData;
            NormalizedHourlyValue[] values = Enumerable.Range(0, incomplete ? 11 : 12)
                .Select(index => new NormalizedHourlyValue($"hourly-{index + 1:D2}", "pressure",
                    "1405/05/01", Start + 60 + (index * 120), 100m + index)).Reverse().ToArray();
            CompletenessDimensionResult completeness = incomplete
                ? new(CompletenessDimension.HourlyData, CompletenessState.Incomplete,
                    [new("hourly.slot.missing", "The synthetic 23:00 hourly slot is missing.",
                        Date: "1405/05/01", Field: "23:00", SourceIdentity: "synthetic-hourly")])
                : new(CompletenessDimension.HourlyData, CompletenessState.Complete);
            return Success(new HourlyDataReportingOutput(Station, Start, End, "synthetic-hourly",
                Revision, values, completeness));
        }

        Task<ReportingAdapterResult<DailyDataReportingOutput>> IDailyDataReportingAdapter.ReadAsync(
            ReportingInputRequest request, CancellationToken cancellationToken) => Success(
            new DailyDataReportingOutput(Station, Start, End, "synthetic-daily", Revision,
                [new("daily-2", "fuel", "1405/05/02", 4m),
                 new("daily-1", "fuel", "1405/05/01", 6m)],
                new(CompletenessDimension.DailyData, CompletenessState.Complete)));

        Task<ReportingAdapterResult<IReadOnlyList<EventProjectionReportingOutput>>> IEventProjectionReportingAdapter.ReadAsync(
            ReportingInputRequest request, CancellationToken cancellationToken)
        {
            string firstUnit = _scenario == SyntheticReportingScenario.InvalidEventUnitAlignment ? "unit-x" : "unit-1";
            IReadOnlyList<EventProjectionReportingOutput> values =
            [
                EventProjection("unit-2", 2),
                EventProjection(firstUnit, 1)
            ];
            return Success(values);
        }

        Task<ReportingAdapterResult<IReadOnlyList<RuntimeProjectionReportingOutput>>> IRuntimeProjectionReportingAdapter.ReadAsync(
            ReportingInputRequest request, CancellationToken cancellationToken)
        {
            string unit1Baseline = _scenario == SyntheticReportingScenario.VersionMismatch ? "" : "baseline-v1";
            IReadOnlyList<RuntimeProjectionReportingOutput> values =
            [
                RuntimeProjection("unit-2", "baseline-v1", 600, 30),
                RuntimeProjection("unit-1", unit1Baseline, 480, 15)
            ];
            return Success(values);
        }

        Task<ReportingAdapterResult<StationProfileReportingOutput>> IStationProfileReportingAdapter.ReadAsync(
            ReportingInputRequest request, CancellationToken cancellationToken) => Success(
            new StationProfileReportingOutput(Station, "Rasht Synthetic Station", "synthetic-profile",
                Revision, Start, "synthetic-persian-calendar-v1", "minute-unit-ordinal-v1",
                "report-calculation-v1", "report-policy-v1", "rasht-synthetic-profile-v1",
                "snapshot-format-v1", "calendar-policy-v1", ["unit-2", "unit-1"],
                [new("fuel", "Daily Fuel", "m3", ReportAggregationType.Sum, 2),
                 new("pressure", "Pressure", "bar", ReportAggregationType.Average, 1)]));

        private static EventProjectionReportingOutput EventProjection(string unit, int ordinal) => new(
            Station, unit, Start, End, $"synthetic-chain-{unit}", Revision, "event-chain-v1",
            "event-policy-v1", true,
            [new($"event-{ordinal}-stop", unit, EventType.Nsd, Start + 300, 2),
             new($"event-{ordinal}-start", unit, EventType.Start, Start + 120, 1)]);

        private static RuntimeProjectionReportingOutput RuntimeProjection(
            string unit, string baselineVersion, long physicalMinutes, long adjustmentMinutes) => new(
            Station, unit, Start, End, $"synthetic-runtime-{unit}", Revision,
            physicalMinutes, adjustmentMinutes, physicalMinutes + adjustmentMinutes,
            physicalMinutes / 2, 180, 2, UnitOperationalState.Stopped,
            "runtime-calculation-v1", "runtime-policy-v1", baselineVersion, "runtime-config-v1");

        private static Task<ReportingAdapterResult<T>> Success<T>(T value) =>
            Task.FromResult(ReportingAdapterResult<T>.Success(value));
    }
}
