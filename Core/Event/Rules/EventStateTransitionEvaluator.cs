namespace Rah_Negar.Core.Event.Rules;

public sealed class EventStateTransitionEvaluator : IEventStateTransitionEvaluator
{
    public EventTransitionResult Evaluate(EventOperationalState currentState, EventType eventType)
    {
        EventOperationalState? next = (currentState, eventType) switch
        {
            (EventOperationalState.Stopped, EventType.Start) => EventOperationalState.Running,
            (EventOperationalState.Stopped, EventType.Oh) => EventOperationalState.StoppedAfterOh,
            (EventOperationalState.Running, EventType.Nsd) => EventOperationalState.Stopped,
            (EventOperationalState.Running, EventType.Esd) => EventOperationalState.Stopped,
            (EventOperationalState.StoppedAfterOh, EventType.Start) => EventOperationalState.Running,
            _ => null
        };

        return next.HasValue
            ? new EventTransitionResult(true, currentState, eventType, next, null, null)
            : new EventTransitionResult(
                false,
                currentState,
                eventType,
                null,
                $"event.transition.{currentState}.{eventType}.invalid".ToLowerInvariant(),
                GetCorrectionCode(currentState, eventType));
    }

    private static string GetCorrectionCode(EventOperationalState state, EventType type) =>
        (state, type) switch
        {
            (EventOperationalState.Stopped, EventType.Nsd or EventType.Esd) => "record-real-start-first",
            (EventOperationalState.Running, EventType.Start) => "remove-duplicate-start-or-record-shutdown",
            (EventOperationalState.Running, EventType.Oh) => "record-real-shutdown-before-oh",
            (EventOperationalState.StoppedAfterOh, _) => "start-is-only-valid-next-event",
            _ => "correct-event-chain"
        };
}
