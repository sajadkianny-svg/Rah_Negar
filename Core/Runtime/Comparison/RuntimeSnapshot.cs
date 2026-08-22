namespace Rah_Negar.Core.Runtime.Comparison;

/// <summary>
/// Read-only normalized Runtime values. All duration fields are authoritative integral minutes;
/// presentation text and rounded display hours are deliberately excluded.
/// </summary>
public sealed record RuntimeSnapshot(
    string SourceName,
    string StationId,
    string UnitId,
    long PeriodStartMinute,
    long PeriodEndMinute,
    string EventBoundaryVersion,
    long PhysicalRuntimeMinutes,
    long EsdAdjustmentMinutes,
    long AdjustedRuntimeMinutes,
    long RuntimeAfterOhMinutes,
    long LongestRunMinutes,
    int ServiceDayCount,
    UnitOperationalState FinalState,
    string CalculationVersion);
