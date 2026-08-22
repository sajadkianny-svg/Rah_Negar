namespace Rah_Negar.Core.Event.Rules;

public sealed class EventChainEvaluator : IEventChainEvaluator
{
    private readonly IEventStateTransitionEvaluator _transitionEvaluator;

    public EventChainEvaluator(IEventStateTransitionEvaluator transitionEvaluator)
    {
        _transitionEvaluator = transitionEvaluator ?? throw new ArgumentNullException(nameof(transitionEvaluator));
    }

    public EventChainEvaluationResult Evaluate(
        EventOperationalState baselineState,
        IReadOnlyList<Event> chronologicallyOrderedEvents)
    {
        ArgumentNullException.ThrowIfNull(chronologicallyOrderedEvents);
        Event[] ordered = chronologicallyOrderedEvents
            .Where(x => x.Status == EventStatus.Active)
            .OrderBy(x => x.EventDateTime)
            .ThenBy(x => x.EventId, StringComparer.Ordinal)
            .ToArray();

        EventOperationalState state = baselineState;
        long? previousTime = null;
        foreach (Event item in ordered)
        {
            if (previousTime == item.EventDateTime)
                return new(false, state, item, null, "event.chain.duplicate-timestamp");

            EventTransitionResult transition = _transitionEvaluator.Evaluate(state, item.EventType);
            if (!transition.IsValid)
                return new(false, state, item, transition, transition.ErrorCode);

            state = transition.NextState!.Value;
            previousTime = item.EventDateTime;
        }

        return new(true, state, null, null);
    }
}
