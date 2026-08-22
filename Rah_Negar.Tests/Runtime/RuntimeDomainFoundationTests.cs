using Rah_Negar.Core.Event;
using Rah_Negar.Core.Event.Comparison;
using Rah_Negar.Core.Runtime;
using Rah_Negar.Foundation.Application.Runtime;

namespace Rah_Negar.Tests.Runtime;

public sealed class RuntimeDomainFoundationTests
{
    [Fact]
    public void RuntimeProjection_SeparatesPhysicalAndAdjustmentMetrics()
    {
        var projection = new RuntimeProjection(
            "station-rasht", "unit-1", 100, 200,
            TimeSpan.FromHours(4), TimeSpan.FromMinutes(30),
            TimeSpan.FromHours(100), TimeSpan.FromHours(2),
            TimeSpan.FromHours(10), 1, TimeSpan.FromHours(4),
            UnitOperationalState.Stopped, "policy-v1");

        Assert.Equal(TimeSpan.FromHours(4.5), projection.PeriodAdjustedRuntime);
        Assert.Equal(TimeSpan.FromHours(102), projection.CumulativeAdjustedRuntime);
    }

    [Fact]
    public void ValidatedChain_PreservesTransitionInputAndResultingState()
    {
        var events = new[] { EventAt(EventType.Start) };
        var chain = ValidatedEventChain.Valid(
            "station-rasht", "unit-1", events,
            UnitOperationalState.Stopped, UnitOperationalState.Running);

        Assert.True(chain.IsValid);
        Assert.Equal(UnitOperationalState.Stopped, chain.InitialState);
        Assert.Equal(UnitOperationalState.Running, chain.ResultingState);
        Assert.Same(events, chain.Events);
    }

    [Fact]
    public void Calculate_InvalidChain_IsRejected()
    {
        var chain = ValidatedEventChain.Invalid(
            "station-rasht", "unit-1", Array.Empty<NormalizedEvent>(),
            UnitOperationalState.Stopped, "event.chain.invalid-transition");

        var result = new RuntimeCalculatorFoundation().Calculate(Request(chain));

        Assert.False(result.IsSuccess);
        Assert.Equal("runtime.event-chain.invalid", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Calculate_BaselineStateDifferentFromChainInput_IsRejected()
    {
        var chain = ValidatedEventChain.Valid(
            "station-rasht", "unit-1", Array.Empty<NormalizedEvent>(),
            UnitOperationalState.Running, UnitOperationalState.Running);

        var result = new RuntimeCalculatorFoundation().Calculate(Request(chain));

        Assert.False(result.IsSuccess);
        Assert.Equal("runtime.baseline.state-mismatch", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Calculate_EmptyValidatedChain_ReturnsUnchangedBaselineAndZeroPeriodMetrics()
    {
        var chain = ValidatedEventChain.Valid(
            "station-rasht", "unit-1", Array.Empty<NormalizedEvent>(),
            UnitOperationalState.Stopped, UnitOperationalState.Stopped);

        var result = new RuntimeCalculatorFoundation().Calculate(Request(chain));

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromHours(25), result.Projection!.CumulativePhysicalRuntime);
        Assert.Equal(TimeSpan.FromHours(1), result.Projection.CumulativeEsdAdjustment);
        Assert.Equal(TimeSpan.Zero, result.Projection.PeriodPhysicalRuntime);
        Assert.Equal(UnitOperationalState.Stopped, result.Projection.FinalState);
    }

    [Fact]
    public void Calculate_NonEmptyChain_FailsExplicitlyUntilProjectionPolicyIsImplemented()
    {
        var chain = ValidatedEventChain.Valid(
            "station-rasht", "unit-1", new[] { EventAt(EventType.Start) },
            UnitOperationalState.Stopped, UnitOperationalState.Running);

        var result = new RuntimeCalculatorFoundation().Calculate(Request(chain));

        Assert.False(result.IsSuccess);
        Assert.Equal("runtime.projection.nonempty-not-implemented", Assert.Single(result.Errors).Code);
    }

    private static RuntimeCalculationRequest Request(ValidatedEventChain chain)
    {
        var state = new RuntimeState(
            "station-rasht", "unit-1", 50, UnitOperationalState.Stopped,
            TimeSpan.FromHours(25), TimeSpan.FromHours(1), TimeSpan.FromHours(8));
        var policy = new RuntimeCalculationPolicy(
            new EsdAdjustmentPolicy(false, TimeSpan.Zero, "esd-unapproved"),
            new OhHandlingPolicy(OhRuntimeHandling.PolicyNotSelected, "oh-unapproved"),
            new ServiceDayBoundaryPolicy(new TimeOnly(0, 0), "Persian", "day-unapproved"),
            "foundation-v1");
        return new RuntimeCalculationRequest(chain, new RuntimeBaseline(state, "baseline-v1", "fixture"), policy, 100, 200);
    }

    private static NormalizedEvent EventAt(EventType type) =>
        new("event-1", "station-rasht", "unit-1", type, 14050101, 60, 638000060, 0, Array.Empty<string>());
}
