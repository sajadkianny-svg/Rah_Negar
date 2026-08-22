using Rah_Negar.Core.Event;
using Rah_Negar.Core.Event.Comparison;
using Rah_Negar.Core.Runtime;
using Rah_Negar.Core.Runtime.Calculation;
using RuntimeCalculationResult = Rah_Negar.Core.Runtime.Calculation.RuntimeCalculationResult;
using RuntimeProjection = Rah_Negar.Core.Runtime.Calculation.RuntimeProjection;

namespace Rah_Negar.Tests.Runtime;

public sealed class RuntimeProjectionEngineTests
{
    private static readonly DateTimeOffset CalculationTime = new(2026, 8, 22, 8, 0, 0, TimeSpan.Zero);
    private readonly RuntimeCalculator _calculator = new();

    [Fact]
    public void StartThenNsd_CreatesHalfOpenPhysicalInterval()
    {
        RuntimeCalculationResult result = Calculate(
            UnitOperationalState.Stopped,
            UnitOperationalState.Stopped,
            0, 300,
            events: new[]
            {
                EventAt("start", EventType.Start, 60),
                EventAt("nsd", EventType.Nsd, 180)
            });

        Assert.True(result.IsSuccess);
        Assert.Equal(120, result.Projection!.PhysicalRuntimeMinutes);
        RuntimeInterval interval = Assert.Single(result.Projection.PhysicalIntervals);
        Assert.Equal(60, interval.StartMinute);
        Assert.Equal(180, interval.EndMinute);
        Assert.False(interval.IsOpenAtCalculationEnd);
    }

    [Fact]
    public void StartThenEsd_AddsCurrentAdjustmentExactlyOnce()
    {
        RuntimeCalculationResult result = Calculate(
            UnitOperationalState.Stopped,
            UnitOperationalState.Stopped,
            0, 300,
            esdAdjustment: 45,
            events: new[]
            {
                EventAt("start", EventType.Start, 60),
                EventAt("esd", EventType.Esd, 180)
            });

        Assert.True(result.IsSuccess);
        Assert.Equal(120, result.Projection!.PhysicalRuntimeMinutes);
        Assert.Equal(45, result.Projection.EsdAdjustmentMinutes);
        Assert.Equal(165, result.Projection.AdjustedRuntimeMinutes);
    }

    [Fact]
    public void MultipleRuns_AreSummedAndLongestRunRemainsPhysical()
    {
        RuntimeCalculationResult result = Calculate(
            UnitOperationalState.Stopped,
            UnitOperationalState.Stopped,
            0, 500,
            esdAdjustment: 100,
            events: new[]
            {
                EventAt("start-1", EventType.Start, 30),
                EventAt("nsd", EventType.Nsd, 90),
                EventAt("start-2", EventType.Start, 200),
                EventAt("esd", EventType.Esd, 320)
            });

        Assert.True(result.IsSuccess);
        Assert.Equal(180, result.Projection!.PhysicalRuntimeMinutes);
        Assert.Equal(120, result.Projection.LongestRunMinutes);
        Assert.Equal(100, result.Projection.EsdAdjustmentMinutes);
    }

    [Fact]
    public void RunningBaseline_StartsAtSoftwareResponsibilityBoundary()
    {
        RuntimeCalculationResult result = Calculate(
            UnitOperationalState.Running,
            UnitOperationalState.Stopped,
            30, 200,
            baselineTotal: 1_000,
            events: new[] { EventAt("nsd", EventType.Nsd, 120) });

        Assert.True(result.IsSuccess);
        Assert.Equal(90, result.Projection!.PhysicalRuntimeMinutes);
        Assert.Equal(90, result.Projection.LongestRunMinutes);
        Assert.Equal(1_120, result.Projection.CumulativeTotalRuntimeMinutes);
        Assert.Equal(30, Assert.Single(result.Projection.PhysicalIntervals).StartMinute);
    }

    [Fact]
    public void OpenRun_IsClippedAtCalculationEndWithoutSyntheticEvent()
    {
        RuntimeCalculationResult result = Calculate(
            UnitOperationalState.Stopped,
            UnitOperationalState.Running,
            0, 300,
            events: new[] { EventAt("start", EventType.Start, 60) });

        Assert.True(result.IsSuccess);
        Assert.Equal(240, result.Projection!.PhysicalRuntimeMinutes);
        Assert.Equal(UnitOperationalState.Running, result.Projection.FinalState);
        RuntimeInterval interval = Assert.Single(result.Projection.PhysicalIntervals);
        Assert.True(interval.IsOpenAtCalculationEnd);
        Assert.Null(interval.EndEventId);
    }

    [Fact]
    public void Oh_ResetsOnlyRuntimeAfterOh()
    {
        RuntimeCalculationResult result = Calculate(
            UnitOperationalState.Stopped,
            UnitOperationalState.Stopped,
            0, 600,
            baselineTotal: 2_000,
            baselineAfterOh: 500,
            events: new[]
            {
                EventAt("start-1", EventType.Start, 100),
                EventAt("nsd-1", EventType.Nsd, 200),
                EventAt("oh", EventType.Oh, 300),
                EventAt("start-2", EventType.Start, 400),
                EventAt("nsd-2", EventType.Nsd, 460)
            });

        Assert.True(result.IsSuccess);
        Assert.Equal(160, result.Projection!.PhysicalRuntimeMinutes);
        Assert.Equal(2_160, result.Projection.CumulativeTotalRuntimeMinutes);
        Assert.Equal(60, result.Projection.RuntimeAfterOhMinutes);
    }

    [Fact]
    public void CrossMidnightRun_RemainsContinuousAndCountsTwoServiceDays()
    {
        RuntimeCalculationResult result = Calculate(
            UnitOperationalState.Stopped,
            UnitOperationalState.Stopped,
            0, 2_000,
            events: new[]
            {
                EventAt("start", EventType.Start, 1_380),
                EventAt("nsd", EventType.Nsd, 1_500)
            });

        Assert.True(result.IsSuccess);
        Assert.Equal(120, result.Projection!.PhysicalRuntimeMinutes);
        Assert.Equal(120, result.Projection.LongestRunMinutes);
        Assert.Equal(2, result.Projection.ServiceDayCount);
    }

    [Fact]
    public void RunEndingAtMidnight_DoesNotCountNewServiceDay()
    {
        RuntimeCalculationResult result = Calculate(
            UnitOperationalState.Stopped,
            UnitOperationalState.Stopped,
            0, 2_000,
            events: new[]
            {
                EventAt("start", EventType.Start, 1_400),
                EventAt("nsd", EventType.Nsd, 1_440)
            });

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Projection!.ServiceDayCount);
    }

    [Fact]
    public void CurrentEsdAdjustment_RecalculatesEarlierOpenPeriodEvent()
    {
        NormalizedEvent[] events =
        {
            EventAt("start", EventType.Start, 60),
            EventAt("esd", EventType.Esd, 120)
        };

        RuntimeCalculationResult oldResult = Calculate(
            UnitOperationalState.Stopped, UnitOperationalState.Stopped, 0, 200, 100, events: events);
        RuntimeCalculationResult newResult = Calculate(
            UnitOperationalState.Stopped, UnitOperationalState.Stopped, 0, 200, 120, events: events);

        Assert.Equal(100, oldResult.Projection!.EsdAdjustmentMinutes);
        Assert.Equal(120, newResult.Projection!.EsdAdjustmentMinutes);
        Assert.Equal(160, oldResult.Projection.AdjustedRuntimeMinutes);
        Assert.Equal(180, newResult.Projection.AdjustedRuntimeMinutes);
    }

    [Fact]
    public void InvalidChain_IsRejectedWithoutProjection()
    {
        var chain = ValidatedEventChain.Invalid(
            "station-rasht", "unit-1", Array.Empty<NormalizedEvent>(),
            UnitOperationalState.Stopped, "event.transition.invalid");
        RuntimeCalculationContext context = Context(chain, 0, 100);

        RuntimeCalculationResult result = _calculator.Calculate(context);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Projection);
        Assert.Equal("runtime.event-chain.invalid", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void FinalizedSnapshotSimulation_IsImmutableAfterCurrentSettingChanges()
    {
        NormalizedEvent[] events =
        {
            EventAt("start", EventType.Start, 10),
            EventAt("esd", EventType.Esd, 70)
        };
        RuntimeCalculationResult finalizedCalculation = Calculate(
            UnitOperationalState.Stopped, UnitOperationalState.Stopped, 0, 100, 100, events: events);
        RuntimeProjection finalizedSnapshot = finalizedCalculation.Projection!;

        RuntimeCalculationResult currentCalculation = Calculate(
            UnitOperationalState.Stopped, UnitOperationalState.Stopped, 0, 100, 120, events: events);

        Assert.Equal(100, finalizedSnapshot.EsdAdjustmentMinutes);
        Assert.Equal(160, finalizedSnapshot.AdjustedRuntimeMinutes);
        Assert.Equal(120, currentCalculation.Projection!.EsdAdjustmentMinutes);
        Assert.Equal("policy-v1", finalizedSnapshot.PolicyVersion);
    }

    [Fact]
    public void Projection_PreservesDeterministicMetadata()
    {
        RuntimeCalculationResult result = Calculate(
            UnitOperationalState.Stopped,
            UnitOperationalState.Stopped,
            0, 100);

        Assert.True(result.IsSuccess);
        Assert.Equal("events-v1", result.Projection!.EventChainVersion);
        Assert.Equal("baseline-v1", result.Projection.BaselineVersion);
        Assert.Equal("policy-v1", result.Projection.PolicyVersion);
        Assert.Equal("runtime-4.2-v1", result.Projection.CalculationVersion);
        Assert.Equal(CalculationTime, result.Projection.CalculationTimestamp);
    }

    private RuntimeCalculationResult Calculate(
        UnitOperationalState initialState,
        UnitOperationalState resultingState,
        long periodStart,
        long periodEnd,
        long esdAdjustment = 0,
        long baselineTotal = 0,
        long baselineAfterOh = 0,
        params NormalizedEvent[] events)
    {
        var chain = ValidatedEventChain.Valid(
            "station-rasht", "unit-1", events, initialState, resultingState);
        return _calculator.Calculate(Context(
            chain, periodStart, periodEnd, esdAdjustment, baselineTotal, baselineAfterOh));
    }

    private static RuntimeCalculationContext Context(
        ValidatedEventChain chain,
        long periodStart,
        long periodEnd,
        long esdAdjustment = 0,
        long baselineTotal = 0,
        long baselineAfterOh = 0) =>
        new(
            chain,
            BaselineMinute: 0,
            BaselineState: chain.InitialState,
            BaselineTotalRuntimeMinutes: baselineTotal,
            BaselineRuntimeAfterOhMinutes: baselineAfterOh,
            PeriodStartMinute: periodStart,
            PeriodEndMinute: periodEnd,
            CurrentEsdAdjustmentMinutes: esdAdjustment,
            EventChainVersion: "events-v1",
            BaselineVersion: "baseline-v1",
            PolicyVersion: "policy-v1",
            CalculationVersion: "runtime-4.2-v1",
            CalculationTimestamp: CalculationTime);

    private static NormalizedEvent EventAt(string id, EventType type, long minute) =>
        new(id, "station-rasht", "unit-1", type, 14050101, checked((int)(minute % 1_440)), minute, checked((int)minute), Array.Empty<string>());
}
