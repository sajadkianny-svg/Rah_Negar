namespace Rah_Negar.Core.Event.Rules;

public interface IEventValidator
{
    EventValidationResult Validate(Event candidate);
}

public interface IEventStateTransitionEvaluator
{
    EventTransitionResult Evaluate(EventOperationalState currentState, EventType eventType);
}

public interface IEventChainEvaluator
{
    EventChainEvaluationResult Evaluate(
        EventOperationalState baselineState,
        IReadOnlyList<Event> chronologicallyOrderedEvents);
}

public sealed record EventTransitionResult(
    bool IsValid,
    EventOperationalState CurrentState,
    EventType EventType,
    EventOperationalState? NextState,
    string? ErrorCode,
    string? CorrectionCode);

public sealed record EventChainEvaluationResult(
    bool IsValid,
    EventOperationalState FinalState,
    Event? InvalidEvent,
    EventTransitionResult? FailedTransition,
    string? FailureCode = null);
