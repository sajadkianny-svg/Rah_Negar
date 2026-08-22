namespace Rah_Negar.Core.Runtime.Calculation;

public sealed record RuntimeProjection(
    string StationId,
    string UnitId,
    long PeriodStartMinute,
    long PeriodEndMinute,
    long PhysicalRuntimeMinutes,
    long EsdAdjustmentMinutes,
    long AdjustedRuntimeMinutes,
    long RuntimeAfterOhMinutes,
    long LongestRunMinutes,
    int ServiceDayCount,
    long CumulativeTotalRuntimeMinutes,
    UnitOperationalState FinalState,
    IReadOnlyList<RuntimeInterval> PhysicalIntervals,
    string EventChainVersion,
    string BaselineVersion,
    string PolicyVersion,
    string CalculationVersion,
    DateTimeOffset CalculationTimestamp);
