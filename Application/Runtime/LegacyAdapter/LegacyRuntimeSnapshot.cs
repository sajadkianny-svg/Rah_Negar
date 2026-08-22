using Rah_Negar.Core.Runtime;

namespace Rah_Negar.Foundation.Application.Runtime.LegacyAdapter;

/// <summary>
/// Read-only raw values captured from legacy Runtime. Nullable fields represent legacy output
/// that is unavailable or cannot yet be derived with evidence; normalization must not infer it.
/// </summary>
public sealed record LegacyRuntimeSnapshot(
    string? SourceName,
    string? StationId,
    string? UnitId,
    long? PeriodStartMinute,
    long? PeriodEndMinute,
    string? EventBoundaryVersion,
    double? PhysicalRuntimeHours,
    double? EsdAdjustmentHours,
    double? AdjustedRuntimeHours,
    double? RuntimeAfterOhHours,
    double? LongestRunHours,
    int? ServiceDayCount,
    UnitOperationalState? FinalState,
    string? CalculationVersion);
