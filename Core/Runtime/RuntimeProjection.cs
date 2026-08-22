namespace Rah_Negar.Core.Runtime;

public sealed record RuntimeProjection(
    string StationId,
    string UnitId,
    long PeriodStartMinute,
    long PeriodEndMinute,
    TimeSpan PeriodPhysicalRuntime,
    TimeSpan PeriodEsdAdjustment,
    TimeSpan CumulativePhysicalRuntime,
    TimeSpan CumulativeEsdAdjustment,
    TimeSpan RuntimeAfterOh,
    int ServiceDays,
    TimeSpan LongestRun,
    UnitOperationalState FinalState,
    string CalculationPolicyVersion)
{
    public TimeSpan PeriodAdjustedRuntime => PeriodPhysicalRuntime + PeriodEsdAdjustment;
    public TimeSpan CumulativeAdjustedRuntime => CumulativePhysicalRuntime + CumulativeEsdAdjustment;
}
