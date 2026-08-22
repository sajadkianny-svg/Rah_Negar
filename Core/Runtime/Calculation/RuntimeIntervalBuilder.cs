using Rah_Negar.Core.Event;
using Rah_Negar.Core.Event.Comparison;

namespace Rah_Negar.Core.Runtime.Calculation;

public sealed class RuntimeIntervalBuilder
{
    public RuntimeTimelineBuildResult Build(RuntimeCalculationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        RuntimeCalculationError? validationError = ValidateContext(context);
        if (validationError is not null)
            return RuntimeTimelineBuildResult.Failure(validationError);

        UnitOperationalState state = context.BaselineState;
        long? openRunStart = state == UnitOperationalState.Running ? context.BaselineMinute : null;
        string? openRunEventId = null;
        var intervals = new List<RuntimeInterval>();
        var timelineEvents = new List<RuntimeTimelineEvent>();

        foreach (NormalizedEvent item in context.EventChain.Events)
        {
            switch (state, item.EventType)
            {
                case (UnitOperationalState.Stopped, EventType.Start):
                case (UnitOperationalState.StoppedAfterOh, EventType.Start):
                    state = UnitOperationalState.Running;
                    openRunStart = item.EventDateTime;
                    openRunEventId = item.SourceEventId;
                    break;

                case (UnitOperationalState.Running, EventType.Nsd):
                case (UnitOperationalState.Running, EventType.Esd):
                    intervals.Add(new RuntimeInterval(
                        openRunStart!.Value,
                        item.EventDateTime,
                        openRunEventId,
                        item.SourceEventId,
                        false));
                    state = UnitOperationalState.Stopped;
                    openRunStart = null;
                    openRunEventId = null;
                    break;

                case (UnitOperationalState.Stopped, EventType.Oh):
                    state = UnitOperationalState.StoppedAfterOh;
                    break;

                default:
                    return RuntimeTimelineBuildResult.Failure(new RuntimeCalculationError(
                        "runtime.event-chain.transition-conflict",
                        $"Validated Event chain contains an impossible {state} + {item.EventType} transition."));
            }

            timelineEvents.Add(new RuntimeTimelineEvent(item.EventDateTime, item.EventType));
        }

        if (state != context.EventChain.ResultingState)
            return RuntimeTimelineBuildResult.Failure(new RuntimeCalculationError(
                "runtime.event-chain.resulting-state-mismatch",
                "Replayed Runtime state does not match the validated Event chain resulting state."));

        if (state == UnitOperationalState.Running)
        {
            intervals.Add(new RuntimeInterval(
                openRunStart!.Value,
                context.PeriodEndMinute,
                openRunEventId,
                null,
                true));
        }

        return RuntimeTimelineBuildResult.Success(intervals, timelineEvents, state);
    }

    private static RuntimeCalculationError? ValidateContext(RuntimeCalculationContext context)
    {
        if (!context.EventChain.IsValid)
            return new("runtime.event-chain.invalid", "Runtime requires a validated Event chain.");
        if (context.PeriodEndMinute <= context.PeriodStartMinute)
            return new("runtime.period.invalid", "Runtime period end must be after its start.");
        if (context.BaselineMinute > context.PeriodStartMinute)
            return new("runtime.baseline.after-period-start", "Runtime Baseline cannot be after the requested period start.");
        if (context.BaselineTotalRuntimeMinutes < 0 || context.BaselineRuntimeAfterOhMinutes < 0)
            return new("runtime.baseline.value-invalid", "Runtime Baseline values cannot be negative.");
        if (context.CurrentEsdAdjustmentMinutes < 0)
            return new("runtime.esd-adjustment.invalid", "Current ESD Adjustment cannot be negative.");
        if (context.BaselineState != context.EventChain.InitialState)
            return new("runtime.baseline.state-mismatch", "Baseline state must match the validated Event chain initial state.");
        if (string.IsNullOrWhiteSpace(context.EventChainVersion) ||
            string.IsNullOrWhiteSpace(context.BaselineVersion) ||
            string.IsNullOrWhiteSpace(context.PolicyVersion) ||
            string.IsNullOrWhiteSpace(context.CalculationVersion))
            return new("runtime.metadata.version-missing", "All Runtime calculation versions are required.");

        long? previous = null;
        foreach (NormalizedEvent item in context.EventChain.Events)
        {
            if (!StringComparer.Ordinal.Equals(item.StationId, context.EventChain.StationId) ||
                !StringComparer.Ordinal.Equals(item.UnitId, context.EventChain.UnitId))
                return new("runtime.event-chain.identity-mismatch", "Every Event must belong to the validated chain Station and Unit.");
            if (item.EventDateTime < context.BaselineMinute)
                return new("runtime.event.before-baseline", "Events before DataStartDate/Runtime Baseline are outside Runtime responsibility.");
            if (item.EventDateTime >= context.PeriodEndMinute)
                return new("runtime.event.outside-input-boundary", "The supplied Event chain must end before the exclusive calculation boundary.");
            if (previous.HasValue && item.EventDateTime <= previous.Value)
                return new("runtime.event-chain.order-invalid", "Events must be strictly ordered with unique same-Unit timestamps.");
            previous = item.EventDateTime;
        }

        return null;
    }
}

internal sealed record RuntimeTimelineEvent(long Minute, EventType EventType);

public sealed class RuntimeTimelineBuildResult
{
    private RuntimeTimelineBuildResult(
        IReadOnlyList<RuntimeInterval> intervals,
        IReadOnlyList<RuntimeTimelineEvent> events,
        UnitOperationalState finalState,
        RuntimeCalculationError? error)
    {
        Intervals = intervals;
        Events = events;
        FinalState = finalState;
        Error = error;
    }

    public bool IsSuccess => Error is null;
    public IReadOnlyList<RuntimeInterval> Intervals { get; }
    internal IReadOnlyList<RuntimeTimelineEvent> Events { get; }
    public UnitOperationalState FinalState { get; }
    public RuntimeCalculationError? Error { get; }

    internal static RuntimeTimelineBuildResult Success(
        IReadOnlyList<RuntimeInterval> intervals,
        IReadOnlyList<RuntimeTimelineEvent> events,
        UnitOperationalState finalState) => new(intervals, events, finalState, null);

    internal static RuntimeTimelineBuildResult Failure(RuntimeCalculationError error) =>
        new(Array.Empty<RuntimeInterval>(), Array.Empty<RuntimeTimelineEvent>(), default, error);
}
