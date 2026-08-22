using Rah_Negar.Core.Event;
using Rah_Negar.Core.Reporting.Projection;
using Rah_Negar.Core.Runtime;

namespace Rah_Negar.Tests.Reporting;

public sealed class ReportingProjectionCoreTests
{
    [Fact]
    public void Identity_ValidatesPeriodAndDuplicateUnits()
    {
        Assert.Throws<ArgumentException>(() => Identity(start: 10, end: 10));
        Assert.Throws<ArgumentException>(() => Identity(units: ["unit-1", "unit-1"]));
        Assert.Throws<ArgumentException>(() => Identity(stationId: " "));

        ReportIdentity valid = Identity(units: ["unit-2", "unit-1"]);
        Assert.Equal(["unit-1", "unit-2"], valid.UnitIds);
    }

    [Theory]
    [InlineData(CompletenessState.Complete, true)]
    [InlineData(CompletenessState.Incomplete, false)]
    [InlineData(CompletenessState.Invalid, false)]
    [InlineData(CompletenessState.Unavailable, false)]
    public void Completeness_PreservesFourStatesAndGatesFinalization(CompletenessState state, bool eligible)
    {
        ReportCompletenessResult result = Completeness(state);
        Assert.Equal(state, result.State);
        Assert.Equal(eligible, result.IsFinalizationEligible);
    }

    [Fact]
    public void MissingRequiredVersion_RejectsProjection()
    {
        NormalizedReportInput input = Input(versions: Versions(runtimePolicy: ""));
        ReportProjection result = new ReportCalculator().Calculate(input);
        Assert.Equal(ReportProjectionStatus.Rejected, result.Status);
        Assert.Contains("version.runtime-policy.missing", result.BlockingReasons);
    }

    [Fact]
    public void Projection_IsImmutableFromSourceCollectionChanges()
    {
        var hourly = new List<NormalizedHourlyValue> { new("h-1", "pressure", "1405/05/01", 110, 10m) };
        ReportProjection projection = new ReportCalculator().Calculate(Input(hourly: hourly));
        hourly.Add(new("h-2", "pressure", "1405/05/02", 120, 99m));

        Assert.Equal(10m, Assert.Single(projection.OperationalSummaries).Value);
        Assert.Throws<NotSupportedException>(() => ((IList<OperationalSummary>)projection.OperationalSummaries)
            .Add(new("x", "x", "x", ReportAggregationType.Maximum, 1, 1)));
    }

    [Fact]
    public void IdenticalInput_ProducesDeterministicOrderingAndValues()
    {
        NormalizedReportInput input = Input(
            hourly:
            [
                new("h-2", "pressure", "1405/05/02", 200, 30m),
                new("h-1", "pressure", "1405/05/01", 100, 10m)
            ],
            reportEvents:
            [
                new("event-2", "unit-1", EventType.Nsd, 150, 2),
                new("event-1", "unit-1", EventType.Start, 100, 1)
            ]);

        ReportProjection first = new ReportCalculator().Calculate(input);
        ReportProjection second = new ReportCalculator().Calculate(input);

        Assert.Equal(ReportProjectionStatus.Complete, first.Status);
        Assert.Equal(first.OperationalSummaries, second.OperationalSummaries);
        Assert.Equal(["event-1", "event-2"], first.EventLog.Select(x => x.EventId));
        Assert.Equal(20m, Assert.Single(first.OperationalSummaries).Value);
        Assert.Equal(first.CalculationTimestamp, second.CalculationTimestamp);
    }

    [Fact]
    public void IncompleteEvidence_ProducesNonFinalizableProjectionWithoutRejectingValidInputs()
    {
        ReportProjection result = new ReportCalculator().Calculate(Input(completeness: Completeness(CompletenessState.Incomplete)));
        Assert.Equal(ReportProjectionStatus.Incomplete, result.Status);
        Assert.False(result.Completeness.IsFinalizationEligible);
        Assert.Contains("hourly.missing", result.Warnings);
    }

    private static NormalizedReportInput Input(IEnumerable<NormalizedHourlyValue>? hourly = null,
        IEnumerable<ReportEvent>? reportEvents = null, ReportCompletenessResult? completeness = null,
        ReportVersionSet? versions = null)
    {
        ReportIdentity identity = Identity();
        var events = new AuthoritativeEventInput("chain-1", "chain-v1", "rasht", "unit-1", 100, 200,
            true, (reportEvents ?? []).ToArray());
        var runtime = new AuthoritativeRuntimeInput("runtime-1", "rasht", "unit-1", 100, 200,
            50, 5, 55, 20, 30, 1, UnitOperationalState.Stopped,
            "runtime-calc-v1", "runtime-policy-v1", "baseline-v1", "config-v1");
        return new(identity, new DateTimeOffset(2026, 8, 22, 8, 0, 0, TimeSpan.FromHours(3.5)),
            [new("pressure", "Pressure", "bar", ReportAggregationType.Average, 1)],
            hourly ?? [new("h-1", "pressure", "1405/05/01", 110, 10m)], [], [events], [runtime],
            completeness ?? Completeness(CompletenessState.Complete),
            new ReportEvidence("source-v1", "hourly-v1", 1, "daily-v1", 0, "rasht-profile-v1", 0,
                "persian-calendar-v1", "ordinal-keys-v1"), versions ?? Versions());
    }

    private static ReportIdentity Identity(long start = 100, long end = 200, string stationId = "rasht",
        IEnumerable<string>? units = null) => new("report-1", stationId, "Rasht", start, end,
            "1405/05", ReportPeriodKind.Monthly, units ?? ["unit-1"], ReportSourceMode.OpenProjection);

    private static ReportCompletenessResult Completeness(CompletenessState state)
    {
        CompletenessIssue[] issues = state == CompletenessState.Complete ? [] : [new("hourly.missing", "Missing hourly row")];
        return new(Enum.GetValues<CompletenessDimension>().Select((dimension, index) =>
            new CompletenessDimensionResult(dimension, index == 0 ? state : CompletenessState.Complete,
                index == 0 ? issues : [])));
    }

    private static ReportVersionSet Versions(string runtimePolicy = "runtime-policy-v1") => new(
        "report-calc-v1", "report-policy-v1", "rasht-profile-v1", "snapshot-v1", "event-policy-v1",
        "runtime-calc-v1", runtimePolicy, "calendar-v1",
        new Dictionary<string, string> { ["unit-1"] = "chain-v1" },
        new Dictionary<string, string> { ["unit-1"] = "baseline-v1" },
        new Dictionary<string, string> { ["unit-1"] = "config-v1" });
}
