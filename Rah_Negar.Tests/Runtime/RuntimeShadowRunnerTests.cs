using Rah_Negar.Core.Event;
using Rah_Negar.Core.Event.Comparison;
using Rah_Negar.Core.Runtime;
using Rah_Negar.Core.Runtime.Calculation;
using Rah_Negar.Core.Runtime.Comparison;
using Rah_Negar.Foundation.Application.Runtime.LegacyAdapter;
using Rah_Negar.Foundation.Application.Runtime.Shadow;

namespace Rah_Negar.Tests.Runtime;

public sealed class RuntimeShadowRunnerTests
{
    private static readonly DateTimeOffset ExecutionTime = new(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SuccessfulShadowRun_ReturnsSnapshotsComparisonAndEvidence()
    {
        var source = new FakeInputSource(CopyIdentity(), Context);
        var adapter = new FakeLegacyAdapter(MatchingLegacy);
        RuntimeShadowRunner runner = Runner(adapter);

        RuntimeShadowExecutionResult result = Assert.Single(runner.Execute(Request(source, "unit-1")));

        Assert.Equal(RuntimeShadowExecutionStatus.Match, result.Status);
        Assert.NotNull(result.LegacySnapshot);
        Assert.NotNull(result.NewProjection);
        Assert.Equal(RuntimeDifferenceCategory.Match, result.ComparisonResult!.Category);
        Assert.Equal("copy-001", result.Evidence!.DatabaseCopyId);
        Assert.Equal("events-unit-1-v1", result.Evidence.EventChainVersion);
        Assert.Equal(ExecutionTime, result.Evidence.ExecutionTimestamp);
    }

    [Fact]
    public void InvalidInput_IsRejectedBeforeSourceExecution()
    {
        var source = new FakeInputSource(CopyIdentity(), Context);
        RuntimeShadowRunner runner = Runner(new FakeLegacyAdapter(MatchingLegacy));
        RuntimeShadowExecutionRequest invalid = Request(source) with { UnitIds = Array.Empty<string>() };

        Assert.Throws<ArgumentException>(() => runner.Execute(invalid));
        Assert.Equal(0, source.LoadCount);
    }

    [Fact]
    public void ProductionPath_IsRejectedBeforeAnyRead()
    {
        RuntimeDatabaseCopyIdentity production = CopyIdentity() with { IsProductionSource = true };
        var source = new FakeInputSource(production, Context);
        var adapter = new FakeLegacyAdapter(MatchingLegacy);
        RuntimeShadowRunner runner = Runner(adapter);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            runner.Execute(Request(source, "unit-1")));

        Assert.Contains("production", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, source.LoadCount);
        Assert.Equal(0, adapter.ReadCount);
    }

    [Fact]
    public void LegacyUnavailable_ReturnsIsolatedUnitFailure()
    {
        var source = new FakeInputSource(CopyIdentity(), Context);
        var adapter = new FakeLegacyAdapter((_, _, _, _, _) =>
            throw new InvalidOperationException("legacy capture unavailable"));

        RuntimeShadowExecutionResult result = Assert.Single(Runner(adapter).Execute(Request(source, "unit-1")));

        Assert.Equal(RuntimeShadowExecutionStatus.LegacyUnavailable, result.Status);
        Assert.Equal("runtime.shadow.legacy-unavailable", result.ErrorCode);
        Assert.Null(result.NewProjection);
        Assert.NotNull(result.Evidence);
    }

    [Fact]
    public void RuntimeComparisonDifference_IsReportedWithoutChangingEitherResult()
    {
        var source = new FakeInputSource(CopyIdentity(), Context);
        var adapter = new FakeLegacyAdapter((station, unit, start, end, boundary) =>
            Legacy(station, unit, start, end, boundary, physicalMinutes: 30));

        RuntimeShadowExecutionResult result = Assert.Single(Runner(adapter).Execute(Request(source, "unit-1")));

        Assert.Equal(RuntimeShadowExecutionStatus.DifferenceDetected, result.Status);
        Assert.Equal(RuntimeDifferenceCategory.NewEngineDefect, result.ComparisonResult!.Category);
        Assert.Equal(30, result.LegacySnapshot!.PhysicalRuntimeMinutes);
        Assert.Equal(60, result.NewProjection!.PhysicalRuntimeMinutes);
        Assert.Contains(result.ComparisonResult.Differences, x => x.Metric == "PhysicalRuntime");
    }

    [Fact]
    public void BatchExecution_OrdersDistinctUnitsDeterministically()
    {
        var source = new FakeInputSource(CopyIdentity(), Context);
        var adapter = new FakeLegacyAdapter(MatchingLegacy);

        IReadOnlyList<RuntimeShadowExecutionResult> results =
            Runner(adapter).Execute(Request(source, "unit-2", "unit-1", "unit-2"));

        Assert.Equal(new[] { "unit-1", "unit-2" }, results.Select(x => x.UnitId));
        Assert.All(results, x => Assert.Equal(RuntimeShadowExecutionStatus.Match, x.Status));
        Assert.Equal(2, source.LoadCount);
    }

    [Fact]
    public void WritableCopy_IsRejectedBeforeAnyRead()
    {
        var source = new FakeInputSource(CopyIdentity() with { IsReadOnly = false }, Context);
        var adapter = new FakeLegacyAdapter(MatchingLegacy);

        Assert.Throws<InvalidOperationException>(() => Runner(adapter).Execute(Request(source, "unit-1")));
        Assert.Equal(0, source.LoadCount);
        Assert.Equal(0, adapter.ReadCount);
    }

    private static RuntimeShadowRunner Runner(ILegacyRuntimeAdapter adapter) =>
        new(adapter, new RuntimeCalculator(), new RuntimeComparisonService(), new FixedTimeProvider(ExecutionTime));

    private static RuntimeShadowExecutionRequest Request(
        IRuntimeShadowInputSource source,
        params string[] units) =>
        new(source, "station-rasht", units.Length == 0 ? new[] { "unit-1" } : units, 0, 300, "shadow-run-001");

    private static RuntimeDatabaseCopyIdentity CopyIdentity() =>
        new(
            "copy-001",
            "sha256:synthetic-copy",
            new DateTimeOffset(2026, 8, 21, 10, 0, 0, TimeSpan.Zero),
            "anonymized-test-copy",
            IsReadOnly: true,
            IsProductionSource: false);

    private static RuntimeCalculationContext Context(string station, string unit, long start, long end)
    {
        NormalizedEvent[] events =
        {
            EventAt("start", station, unit, EventType.Start, 60),
            EventAt("nsd", station, unit, EventType.Nsd, 120)
        };
        var chain = ValidatedEventChain.Valid(
            station, unit, events, UnitOperationalState.Stopped, UnitOperationalState.Stopped);
        return new RuntimeCalculationContext(
            chain,
            BaselineMinute: 0,
            BaselineState: UnitOperationalState.Stopped,
            BaselineTotalRuntimeMinutes: 1_000,
            BaselineRuntimeAfterOhMinutes: 0,
            PeriodStartMinute: start,
            PeriodEndMinute: end,
            CurrentEsdAdjustmentMinutes: 0,
            EventChainVersion: $"events-{unit}-v1",
            BaselineVersion: "baseline-v1",
            PolicyVersion: "policy-v1",
            CalculationVersion: "runtime-4.2-v1",
            CalculationTimestamp: ExecutionTime);
    }

    private static LegacyRuntimeSnapshot MatchingLegacy(
        string station,
        string unit,
        long start,
        long end,
        string boundary) => Legacy(station, unit, start, end, boundary, 60);

    private static LegacyRuntimeSnapshot Legacy(
        string station,
        string unit,
        long start,
        long end,
        string boundary,
        long physicalMinutes) =>
        new(
            "legacy-synthetic",
            station,
            unit,
            start,
            end,
            boundary,
            physicalMinutes / 60d,
            0,
            physicalMinutes / 60d,
            physicalMinutes / 60d,
            physicalMinutes / 60d,
            1,
            UnitOperationalState.Stopped,
            "legacy-fixture-v1");

    private static NormalizedEvent EventAt(
        string id,
        string station,
        string unit,
        EventType type,
        long minute) =>
        new(id, station, unit, type, 14050101, checked((int)minute), minute, checked((int)minute), Array.Empty<string>());

    private sealed class FakeInputSource : IRuntimeShadowInputSource
    {
        private readonly Func<string, string, long, long, RuntimeCalculationContext> _loader;

        public FakeInputSource(
            RuntimeDatabaseCopyIdentity identity,
            Func<string, string, long, long, RuntimeCalculationContext> loader)
        {
            Identity = identity;
            _loader = loader;
        }

        public RuntimeDatabaseCopyIdentity Identity { get; }
        public int LoadCount { get; private set; }

        public RuntimeCalculationContext LoadContext(string stationId, string unitId, long periodStartMinute, long periodEndMinute)
        {
            LoadCount++;
            return _loader(stationId, unitId, periodStartMinute, periodEndMinute);
        }
    }

    private sealed class FakeLegacyAdapter : ILegacyRuntimeAdapter
    {
        private readonly Func<string, string, long, long, string, LegacyRuntimeSnapshot> _reader;

        public FakeLegacyAdapter(Func<string, string, long, long, string, LegacyRuntimeSnapshot> reader)
        {
            _reader = reader;
        }

        public int ReadCount { get; private set; }

        public LegacyRuntimeSnapshot Read(
            string stationId,
            string unitId,
            long periodStartMinute,
            long periodEndMinute,
            string eventBoundaryVersion)
        {
            ReadCount++;
            return _reader(stationId, unitId, periodStartMinute, periodEndMinute, eventBoundaryVersion);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _value;

        public FixedTimeProvider(DateTimeOffset value) => _value = value;

        public override DateTimeOffset GetUtcNow() => _value;
    }
}
