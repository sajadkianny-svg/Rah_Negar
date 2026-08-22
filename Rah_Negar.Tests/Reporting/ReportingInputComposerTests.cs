using Rah_Negar.Core.Event;
using Rah_Negar.Core.Reporting.Projection;
using Rah_Negar.Core.Runtime;
using Rah_Negar.Foundation.Application.Reporting.Input;

namespace Rah_Negar.Tests.Reporting;

public sealed class ReportingInputComposerTests
{
    [Fact]
    public async Task ValidComposition_CreatesNormalizedInputWithEvidenceAndVersions()
    {
        var sources = new Sources();
        ReportInputCompositionResult result = await Composer(sources).ComposeAsync(Request());

        Assert.True(result.IsSuccess);
        Assert.Equal("revision-1", result.Input!.Evidence.SourceRevision);
        Assert.Equal(["unit-1", "unit-2"], result.Input.Runtimes.Select(x => x.UnitId));
        Assert.Equal("chain-v1", result.Input.Versions.EventChainVersions["unit-1"]);
        Assert.Equal(CompletenessState.Complete, result.Input.Completeness.State);
    }

    [Fact]
    public async Task IdentityMismatch_ReturnsWrongStationFailure()
    {
        var sources = new Sources { HourlyStation = "ramsar" };
        ReportInputCompositionResult result = await Composer(sources).ComposeAsync(Request());
        AssertFailure(result, ReportingInputFailureKind.WrongStation, "reporting.input.station.mismatch");
    }

    [Fact]
    public async Task PeriodMismatch_ReturnsWrongPeriodFailure()
    {
        var sources = new Sources { DailyPeriodEnd = 201 };
        ReportInputCompositionResult result = await Composer(sources).ComposeAsync(Request());
        AssertFailure(result, ReportingInputFailureKind.WrongPeriod, "reporting.input.period.mismatch");
    }

    [Fact]
    public async Task MissingRuntime_ReturnsMissingUnitFailure()
    {
        var sources = new Sources { OmitRuntimeUnit = "unit-2" };
        ReportInputCompositionResult result = await Composer(sources).ComposeAsync(Request());
        ReportingInputFailure failure = AssertFailure(result, ReportingInputFailureKind.MissingUnit,
            "reporting.input.unit.missing");
        Assert.Equal("unit-2", failure.UnitId);
        Assert.Equal("runtime", failure.Source);
    }

    [Fact]
    public async Task MissingEvent_ReturnsMissingUnitFailure()
    {
        var sources = new Sources { OmitEventUnit = "unit-1" };
        ReportInputCompositionResult result = await Composer(sources).ComposeAsync(Request());
        ReportingInputFailure failure = AssertFailure(result, ReportingInputFailureKind.MissingUnit,
            "reporting.input.unit.missing");
        Assert.Equal("unit-1", failure.UnitId);
        Assert.Equal("event", failure.Source);
    }

    [Fact]
    public async Task VersionMismatch_ReturnsIncompatibleVersionFailure()
    {
        var sources = new Sources { RuntimePolicyForUnit2 = "runtime-policy-v2" };
        ReportInputCompositionResult result = await Composer(sources).ComposeAsync(Request());
        AssertFailure(result, ReportingInputFailureKind.IncompatibleVersion,
            "reporting.input.runtime-version.incompatible");
    }

    [Fact]
    public async Task AdapterMissingSource_IsReturnedWithoutPartialInput()
    {
        var sources = new Sources { HourlyUnavailable = true };
        ReportInputCompositionResult result = await Composer(sources).ComposeAsync(Request());
        AssertFailure(result, ReportingInputFailureKind.MissingSource, "reporting.input.hourly.unavailable");
        Assert.Null(result.Input);
    }

    [Fact]
    public async Task Composition_DeterministicallyOrdersEveryCollection()
    {
        var sources = new Sources();
        NormalizedReportInput first = (await Composer(sources).ComposeAsync(Request())).Input!;
        NormalizedReportInput second = (await Composer(sources).ComposeAsync(Request())).Input!;

        Assert.Equal(["alpha", "zeta"], first.Parameters.Select(x => x.ParameterId));
        Assert.Equal(["h-1", "h-2"], first.HourlyValues.Select(x => x.RecordId));
        Assert.Equal(["d-1", "d-2"], first.DailyValues.Select(x => x.RecordId));
        Assert.Equal(["unit-1", "unit-2"], first.Events.Select(x => x.UnitId));
        Assert.Equal(first.Events.SelectMany(x => x.Events).Select(x => x.EventId),
            second.Events.SelectMany(x => x.Events).Select(x => x.EventId));
        Assert.Equal(Request().CalculationTimestamp, first.CalculationTimestamp);
    }

    private static IReportInputComposer Composer(Sources sources) =>
        new ReportInputComposer(sources, sources, sources, sources, sources);

    private static ReportInputCompositionRequest Request() => new(
        new ReportIdentity("report-1", "rasht", "Rasht", 100, 200, "1405/05",
            ReportPeriodKind.Monthly, ["unit-2", "unit-1"], ReportSourceMode.OpenProjection),
        new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.FromHours(3.5)));

    private static ReportingInputFailure AssertFailure(ReportInputCompositionResult result,
        ReportingInputFailureKind kind, string code)
    {
        Assert.False(result.IsSuccess);
        ReportingInputFailure failure = Assert.Single(result.Failures, x => x.Code == code);
        Assert.Equal(kind, failure.Kind);
        return failure;
    }

    private sealed class Sources : IHourlyDataReportingAdapter, IDailyDataReportingAdapter,
        IEventProjectionReportingAdapter, IRuntimeProjectionReportingAdapter, IStationProfileReportingAdapter
    {
        public string HourlyStation { get; init; } = "rasht";
        public long DailyPeriodEnd { get; init; } = 200;
        public string? OmitRuntimeUnit { get; init; }
        public string? OmitEventUnit { get; init; }
        public string RuntimePolicyForUnit2 { get; init; } = "runtime-policy-v1";
        public bool HourlyUnavailable { get; init; }

        Task<ReportingAdapterResult<HourlyDataReportingOutput>> IHourlyDataReportingAdapter.ReadAsync(
            ReportingInputRequest request, CancellationToken cancellationToken)
        {
            if (HourlyUnavailable)
                return Task.FromResult(ReportingAdapterResult<HourlyDataReportingOutput>.Failed(new(
                    ReportingInputFailureKind.MissingSource, "reporting.input.hourly.unavailable",
                    "Hourly source is unavailable.", "hourly")));
            return Task.FromResult(ReportingAdapterResult<HourlyDataReportingOutput>.Success(new(
                HourlyStation, 100, 200, "hourly-source", "revision-1",
                [new("h-2", "zeta", "1405/05/02", 120, 20), new("h-1", "alpha", "1405/05/01", 110, 10)],
                Complete(CompletenessDimension.HourlyData))));
        }

        Task<ReportingAdapterResult<DailyDataReportingOutput>> IDailyDataReportingAdapter.ReadAsync(
            ReportingInputRequest request, CancellationToken cancellationToken) => Task.FromResult(
            ReportingAdapterResult<DailyDataReportingOutput>.Success(new("rasht", 100, DailyPeriodEnd,
                "daily-source", "revision-1",
                [new("d-2", "zeta", "1405/05/02", 2), new("d-1", "alpha", "1405/05/01", 1)],
                Complete(CompletenessDimension.DailyData))));

        Task<ReportingAdapterResult<IReadOnlyList<EventProjectionReportingOutput>>> IEventProjectionReportingAdapter.ReadAsync(
            ReportingInputRequest request, CancellationToken cancellationToken)
        {
            EventProjectionReportingOutput[] values = [Event("unit-2", 2), Event("unit-1", 1)];
            return Task.FromResult(ReportingAdapterResult<IReadOnlyList<EventProjectionReportingOutput>>.Success(
                values.Where(x => x.UnitId != OmitEventUnit).ToArray()));
        }

        Task<ReportingAdapterResult<IReadOnlyList<RuntimeProjectionReportingOutput>>> IRuntimeProjectionReportingAdapter.ReadAsync(
            ReportingInputRequest request, CancellationToken cancellationToken)
        {
            RuntimeProjectionReportingOutput[] values = [Runtime("unit-2", RuntimePolicyForUnit2), Runtime("unit-1", "runtime-policy-v1")];
            return Task.FromResult(ReportingAdapterResult<IReadOnlyList<RuntimeProjectionReportingOutput>>.Success(
                values.Where(x => x.UnitId != OmitRuntimeUnit).ToArray()));
        }

        Task<ReportingAdapterResult<StationProfileReportingOutput>> IStationProfileReportingAdapter.ReadAsync(
            ReportingInputRequest request, CancellationToken cancellationToken) => Task.FromResult(
            ReportingAdapterResult<StationProfileReportingOutput>.Success(new("rasht", "Rasht", "profile-source",
                "revision-1", 0, "persian-calendar-v1", "ordinal-v1", "report-calc-v1", "report-policy-v1",
                "profile-v1", "snapshot-v1", "calendar-policy-v1", ["unit-2", "unit-1"],
                [new("zeta", "Zeta", "u", ReportAggregationType.Sum, 2),
                 new("alpha", "Alpha", "u", ReportAggregationType.Average, 1)])));

        private static EventProjectionReportingOutput Event(string unit, int ordinal) => new("rasht", unit,
            100, 200, $"chain-{unit}", "revision-1", "chain-v1", "event-policy-v1", true,
            [new($"event-{ordinal}-b", unit, EventType.Nsd, 150, 2),
             new($"event-{ordinal}-a", unit, EventType.Start, 110, 1)]);

        private static RuntimeProjectionReportingOutput Runtime(string unit, string policy) => new("rasht", unit,
            100, 200, $"runtime-{unit}", "revision-1", 50, 5, 55, 20, 30, 1,
            UnitOperationalState.Stopped, "runtime-calc-v1", policy, "baseline-v1", "config-v1");

        private static CompletenessDimensionResult Complete(CompletenessDimension dimension) =>
            new(dimension, CompletenessState.Complete);
    }
}
