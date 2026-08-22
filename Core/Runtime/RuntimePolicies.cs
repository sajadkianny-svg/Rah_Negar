namespace Rah_Negar.Core.Runtime;

public sealed record EsdAdjustmentPolicy(bool IsEnabled, TimeSpan Adjustment, string PolicyVersion);

public enum OhRuntimeHandling
{
    ResetRuntimeAfterOh,
    PolicyNotSelected
}

public sealed record OhHandlingPolicy(OhRuntimeHandling Handling, string PolicyVersion);

public sealed record ServiceDayBoundaryPolicy(
    TimeOnly BoundaryLocalTime,
    string CalendarSystem,
    string PolicyVersion);

public sealed record RuntimeCalculationPolicy(
    EsdAdjustmentPolicy EsdAdjustment,
    OhHandlingPolicy OhHandling,
    ServiceDayBoundaryPolicy ServiceDayBoundary,
    string CalculationPolicyVersion);
