using CalculationProjection = Rah_Negar.Core.Runtime.Calculation.RuntimeProjection;

namespace Rah_Negar.Core.Runtime.Comparison;

public static class RuntimeSnapshotNormalizer
{
    public static RuntimeSnapshot FromProjection(
        CalculationProjection projection,
        string sourceName,
        string eventBoundaryVersion)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return Create(
            sourceName,
            projection.StationId,
            projection.UnitId,
            projection.PeriodStartMinute,
            projection.PeriodEndMinute,
            eventBoundaryVersion,
            projection.PhysicalRuntimeMinutes,
            projection.EsdAdjustmentMinutes,
            projection.AdjustedRuntimeMinutes,
            projection.RuntimeAfterOhMinutes,
            projection.LongestRunMinutes,
            projection.ServiceDayCount,
            projection.FinalState,
            projection.CalculationVersion);
    }

    public static RuntimeSnapshot Create(
        string sourceName,
        string stationId,
        string unitId,
        long periodStartMinute,
        long periodEndMinute,
        string eventBoundaryVersion,
        long physicalRuntimeMinutes,
        long esdAdjustmentMinutes,
        long adjustedRuntimeMinutes,
        long runtimeAfterOhMinutes,
        long longestRunMinutes,
        int serviceDayCount,
        UnitOperationalState finalState,
        string calculationVersion)
    {
        Require(sourceName, nameof(sourceName));
        Require(stationId, nameof(stationId));
        Require(unitId, nameof(unitId));
        Require(eventBoundaryVersion, nameof(eventBoundaryVersion));
        Require(calculationVersion, nameof(calculationVersion));
        if (periodEndMinute <= periodStartMinute)
            throw new ArgumentOutOfRangeException(nameof(periodEndMinute), "Period end must be after period start.");
        if (physicalRuntimeMinutes < 0 || esdAdjustmentMinutes < 0 || adjustedRuntimeMinutes < 0 ||
            runtimeAfterOhMinutes < 0 || longestRunMinutes < 0 || serviceDayCount < 0)
            throw new ArgumentOutOfRangeException(nameof(physicalRuntimeMinutes), "Normalized Runtime values cannot be negative.");
        if (adjustedRuntimeMinutes != checked(physicalRuntimeMinutes + esdAdjustmentMinutes))
            throw new ArgumentException("Adjusted Runtime must equal Physical Runtime plus ESD Adjustment.", nameof(adjustedRuntimeMinutes));

        return new RuntimeSnapshot(
            sourceName,
            stationId,
            unitId,
            periodStartMinute,
            periodEndMinute,
            eventBoundaryVersion,
            physicalRuntimeMinutes,
            esdAdjustmentMinutes,
            adjustedRuntimeMinutes,
            runtimeAfterOhMinutes,
            longestRunMinutes,
            serviceDayCount,
            finalState,
            calculationVersion);
    }

    public static long WholeMinutes(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(value), "Runtime cannot be negative.");
        if (value.Ticks % TimeSpan.TicksPerMinute != 0)
            throw new ArgumentException("Runtime comparison requires integral-minute authority.", nameof(value));
        return checked((long)value.TotalMinutes);
    }

    private static void Require(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A non-empty value is required.", parameterName);
    }
}
