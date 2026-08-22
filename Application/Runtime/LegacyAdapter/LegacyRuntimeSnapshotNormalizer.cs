using Rah_Negar.Core.Runtime;
using Rah_Negar.Core.Runtime.Comparison;

namespace Rah_Negar.Foundation.Application.Runtime.LegacyAdapter;

public static class LegacyRuntimeSnapshotNormalizer
{
    private const double WholeMinuteTolerance = 0.000001d;

    public static RuntimeSnapshot Normalize(
        LegacyRuntimeSnapshot legacy,
        string expectedStationId,
        string expectedUnitId,
        long expectedPeriodStartMinute,
        long expectedPeriodEndMinute,
        string expectedEventBoundaryVersion)
    {
        ArgumentNullException.ThrowIfNull(legacy);
        RequireIdentity(legacy.StationId, expectedStationId, "StationId");
        RequireIdentity(legacy.UnitId, expectedUnitId, "UnitId");
        RequireIdentity(legacy.EventBoundaryVersion, expectedEventBoundaryVersion, "EventBoundaryVersion");

        long periodStart = Require(legacy.PeriodStartMinute, "PeriodStartMinute");
        long periodEnd = Require(legacy.PeriodEndMinute, "PeriodEndMinute");
        if (periodStart != expectedPeriodStartMinute || periodEnd != expectedPeriodEndMinute)
            throw new InvalidOperationException("Legacy Runtime period does not match the requested comparison period.");

        long physical = HoursToMinutes(legacy.PhysicalRuntimeHours, "PhysicalRuntimeHours");
        long esd = HoursToMinutes(legacy.EsdAdjustmentHours, "EsdAdjustmentHours");
        long adjusted = HoursToMinutes(legacy.AdjustedRuntimeHours, "AdjustedRuntimeHours");
        long afterOh = HoursToMinutes(legacy.RuntimeAfterOhHours, "RuntimeAfterOhHours");
        long longest = HoursToMinutes(legacy.LongestRunHours, "LongestRunHours");
        int serviceDays = Require(legacy.ServiceDayCount, "ServiceDayCount");
        UnitOperationalState finalState = Require(legacy.FinalState, "FinalState");

        return RuntimeSnapshotNormalizer.Create(
            RequireText(legacy.SourceName, "SourceName"),
            expectedStationId,
            expectedUnitId,
            periodStart,
            periodEnd,
            expectedEventBoundaryVersion,
            physical,
            esd,
            adjusted,
            afterOh,
            longest,
            serviceDays,
            finalState,
            RequireText(legacy.CalculationVersion, "CalculationVersion"));
    }

    public static long HoursToMinutes(double? hours, string fieldName)
    {
        if (!hours.HasValue)
            throw new InvalidOperationException($"Legacy Runtime field {fieldName} is missing.");
        if (double.IsNaN(hours.Value) || double.IsInfinity(hours.Value) || hours.Value < 0)
            throw new InvalidOperationException($"Legacy Runtime field {fieldName} is invalid.");

        double rawMinutes = hours.Value * 60d;
        double rounded = Math.Round(rawMinutes, 0, MidpointRounding.AwayFromZero);
        if (Math.Abs(rawMinutes - rounded) > WholeMinuteTolerance)
            throw new InvalidOperationException($"Legacy Runtime field {fieldName} is not representable as authoritative integral minutes.");
        if (rounded > long.MaxValue)
            throw new OverflowException($"Legacy Runtime field {fieldName} exceeds the supported minute range.");
        return checked((long)rounded);
    }

    private static void RequireIdentity(string? actual, string expected, string fieldName)
    {
        string value = RequireText(actual, fieldName);
        if (!StringComparer.Ordinal.Equals(value, expected))
            throw new InvalidOperationException($"Legacy Runtime {fieldName} does not match the requested comparison input.");
    }

    private static string RequireText(string? value, string fieldName) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Legacy Runtime field {fieldName} is missing.");

    private static T Require<T>(T? value, string fieldName) where T : struct =>
        value ?? throw new InvalidOperationException($"Legacy Runtime field {fieldName} is missing.");
}
