namespace Rah_Negar.Core.Runtime.Calculation;

public sealed record RuntimeCalculationContext(
    ValidatedEventChain EventChain,
    long BaselineMinute,
    UnitOperationalState BaselineState,
    long BaselineTotalRuntimeMinutes,
    long BaselineRuntimeAfterOhMinutes,
    long PeriodStartMinute,
    long PeriodEndMinute,
    long CurrentEsdAdjustmentMinutes,
    string EventChainVersion,
    string BaselineVersion,
    string PolicyVersion,
    string CalculationVersion,
    DateTimeOffset CalculationTimestamp);
