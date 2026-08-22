using Rah_Negar.Core.Event;

namespace Rah_Negar.Core.Runtime.Calculation;

public sealed class RuntimeCalculator
{
    private const long MinutesPerServiceDay = 24 * 60;
    private readonly RuntimeIntervalBuilder _intervalBuilder;

    public RuntimeCalculator() : this(new RuntimeIntervalBuilder())
    {
    }

    public RuntimeCalculator(RuntimeIntervalBuilder intervalBuilder)
    {
        _intervalBuilder = intervalBuilder ?? throw new ArgumentNullException(nameof(intervalBuilder));
    }

    public RuntimeCalculationResult Calculate(RuntimeCalculationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        RuntimeTimelineBuildResult timeline = _intervalBuilder.Build(context);
        if (!timeline.IsSuccess)
            return RuntimeCalculationResult.Failure(timeline.Error!.Code, timeline.Error.Message);

        RuntimeInterval[] clipped = timeline.Intervals
            .Select(x => Clip(x, context.PeriodStartMinute, context.PeriodEndMinute))
            .Where(x => x is not null)
            .Cast<RuntimeInterval>()
            .ToArray();

        long physical = SumDurations(clipped);
        long esdCount = timeline.Events.LongCount(x =>
            x.EventType == EventType.Esd &&
            x.Minute >= context.PeriodStartMinute &&
            x.Minute < context.PeriodEndMinute);
        long esdAdjustment;
        long adjusted;
        long cumulativeTotal;
        try
        {
            esdAdjustment = checked(esdCount * context.CurrentEsdAdjustmentMinutes);
            adjusted = checked(physical + esdAdjustment);
            cumulativeTotal = checked(context.BaselineTotalRuntimeMinutes +
                PhysicalMinutesBetween(timeline.Intervals, context.BaselineMinute, context.PeriodEndMinute) +
                checked(timeline.Events.LongCount(x => x.EventType == EventType.Esd) * context.CurrentEsdAdjustmentMinutes));
        }
        catch (OverflowException)
        {
            return RuntimeCalculationResult.Failure("runtime.value.overflow", "Runtime minute calculation exceeded the supported range.");
        }

        long runtimeAfterOh;
        try
        {
            runtimeAfterOh = CalculateRuntimeAfterOh(context, timeline);
        }
        catch (OverflowException)
        {
            return RuntimeCalculationResult.Failure("runtime.value.overflow", "Runtime After OH calculation exceeded the supported range.");
        }

        long longest = clipped.Length == 0 ? 0 : clipped.Max(x => x.DurationMinutes);
        int serviceDays = CountServiceDays(clipped);

        return RuntimeCalculationResult.Success(new RuntimeProjection(
            context.EventChain.StationId,
            context.EventChain.UnitId,
            context.PeriodStartMinute,
            context.PeriodEndMinute,
            physical,
            esdAdjustment,
            adjusted,
            runtimeAfterOh,
            longest,
            serviceDays,
            cumulativeTotal,
            timeline.FinalState,
            Array.AsReadOnly(clipped),
            context.EventChainVersion,
            context.BaselineVersion,
            context.PolicyVersion,
            context.CalculationVersion,
            context.CalculationTimestamp));
    }

    private static long CalculateRuntimeAfterOh(RuntimeCalculationContext context, RuntimeTimelineBuildResult timeline)
    {
        long value = context.BaselineRuntimeAfterOhMinutes;
        long cursor = context.BaselineMinute;
        UnitOperationalState state = context.BaselineState;

        foreach (RuntimeTimelineEvent item in timeline.Events)
        {
            if (state == UnitOperationalState.Running)
                value = checked(value + item.Minute - cursor);

            switch (item.EventType)
            {
                case EventType.Start:
                    state = UnitOperationalState.Running;
                    break;
                case EventType.Nsd:
                    state = UnitOperationalState.Stopped;
                    break;
                case EventType.Esd:
                    value = checked(value + context.CurrentEsdAdjustmentMinutes);
                    state = UnitOperationalState.Stopped;
                    break;
                case EventType.Oh:
                    value = 0;
                    state = UnitOperationalState.StoppedAfterOh;
                    break;
            }

            cursor = item.Minute;
        }

        if (state == UnitOperationalState.Running)
            value = checked(value + context.PeriodEndMinute - cursor);
        return value;
    }

    private static RuntimeInterval? Clip(RuntimeInterval source, long start, long end)
    {
        long clippedStart = Math.Max(source.StartMinute, start);
        long clippedEnd = Math.Min(source.EndMinute, end);
        return clippedEnd <= clippedStart
            ? null
            : source with { StartMinute = clippedStart, EndMinute = clippedEnd };
    }

    private static long SumDurations(IEnumerable<RuntimeInterval> intervals)
    {
        long total = 0;
        foreach (RuntimeInterval interval in intervals)
            total = checked(total + interval.DurationMinutes);
        return total;
    }

    private static long PhysicalMinutesBetween(IEnumerable<RuntimeInterval> intervals, long start, long end) =>
        SumDurations(intervals.Select(x => Clip(x, start, end)).Where(x => x is not null).Cast<RuntimeInterval>());

    private static int CountServiceDays(IEnumerable<RuntimeInterval> intervals)
    {
        var days = new HashSet<long>();
        foreach (RuntimeInterval interval in intervals)
        {
            long first = FloorDivide(interval.StartMinute, MinutesPerServiceDay);
            long last = FloorDivide(interval.EndMinute - 1, MinutesPerServiceDay);
            for (long day = first; day <= last; day++)
                days.Add(day);
        }
        return days.Count;
    }

    private static long FloorDivide(long value, long divisor)
    {
        long quotient = value / divisor;
        long remainder = value % divisor;
        return remainder < 0 ? quotient - 1 : quotient;
    }
}
