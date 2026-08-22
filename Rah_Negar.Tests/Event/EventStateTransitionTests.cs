using Rah_Negar.Core.Event;
using Rah_Negar.Core.Event.Rules;

namespace Rah_Negar.Tests.Event;

public sealed class EventStateTransitionTests
{
    private readonly EventStateTransitionEvaluator _evaluator = new();

    [Theory]
    [InlineData(EventOperationalState.Stopped, EventType.Nsd)]
    [InlineData(EventOperationalState.Stopped, EventType.Esd)]
    [InlineData(EventOperationalState.Running, EventType.Start)]
    [InlineData(EventOperationalState.Running, EventType.Oh)]
    [InlineData(EventOperationalState.StoppedAfterOh, EventType.Nsd)]
    [InlineData(EventOperationalState.StoppedAfterOh, EventType.Esd)]
    [InlineData(EventOperationalState.StoppedAfterOh, EventType.Oh)]
    public void Forbidden_transitions_return_structured_failure(
        EventOperationalState state,
        EventType type)
    {
        EventTransitionResult result = _evaluator.Evaluate(state, type);

        Assert.False(result.IsValid);
        Assert.Null(result.NextState);
        Assert.NotEmpty(result.ErrorCode!);
        Assert.NotEmpty(result.CorrectionCode!);
    }

    [Theory]
    [InlineData(EventOperationalState.Stopped, EventType.Start, EventOperationalState.Running)]
    [InlineData(EventOperationalState.Stopped, EventType.Oh, EventOperationalState.StoppedAfterOh)]
    [InlineData(EventOperationalState.Running, EventType.Nsd, EventOperationalState.Stopped)]
    [InlineData(EventOperationalState.Running, EventType.Esd, EventOperationalState.Stopped)]
    [InlineData(EventOperationalState.StoppedAfterOh, EventType.Start, EventOperationalState.Running)]
    public void Approved_transitions_return_expected_state(
        EventOperationalState state,
        EventType type,
        EventOperationalState expected)
    {
        EventTransitionResult result = _evaluator.Evaluate(state, type);

        Assert.True(result.IsValid);
        Assert.Equal(expected, result.NextState);
    }
}
