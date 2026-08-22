namespace Rah_Negar.Core.Runtime.Comparison;

public sealed class RuntimeComparisonService
{
    public RuntimeComparisonResult Compare(
        RuntimeSnapshot legacy,
        RuntimeSnapshot newEngine,
        RuntimeDifferenceCategory differenceDisposition = RuntimeDifferenceCategory.NewEngineDefect,
        string? classificationReason = null)
    {
        ArgumentNullException.ThrowIfNull(legacy);
        ArgumentNullException.ThrowIfNull(newEngine);

        IReadOnlyList<RuntimeMetricDifference> inputDifferences = CompareInputs(legacy, newEngine);
        if (inputDifferences.Count != 0)
            return new(RuntimeDifferenceCategory.InputMismatch, legacy, newEngine, inputDifferences,
                "Station, Unit, period, and Event boundary must match before Runtime values can be compared.");

        IReadOnlyList<RuntimeMetricDifference> metricDifferences = CompareMetrics(legacy, newEngine);
        if (metricDifferences.Count == 0)
            return new(RuntimeDifferenceCategory.Match, legacy, newEngine, metricDifferences, null);

        if (differenceDisposition is RuntimeDifferenceCategory.Match or RuntimeDifferenceCategory.InputMismatch)
            throw new ArgumentException("A metric difference can only be classified as ExpectedPolicyDifference, LegacyDefect, or NewEngineDefect.", nameof(differenceDisposition));
        if (differenceDisposition is RuntimeDifferenceCategory.ExpectedPolicyDifference or RuntimeDifferenceCategory.LegacyDefect &&
            string.IsNullOrWhiteSpace(classificationReason))
            throw new ArgumentException("Expected policy differences and legacy defects require an evidence-backed reason.", nameof(classificationReason));

        return new(differenceDisposition, legacy, newEngine, metricDifferences, classificationReason);
    }

    private static IReadOnlyList<RuntimeMetricDifference> CompareInputs(RuntimeSnapshot legacy, RuntimeSnapshot target)
    {
        var differences = new List<RuntimeMetricDifference>();
        AddTextDifference(differences, "StationId", legacy.StationId, target.StationId);
        AddTextDifference(differences, "UnitId", legacy.UnitId, target.UnitId);
        AddNumberDifference(differences, "PeriodStartMinute", legacy.PeriodStartMinute, target.PeriodStartMinute);
        AddNumberDifference(differences, "PeriodEndMinute", legacy.PeriodEndMinute, target.PeriodEndMinute);
        AddTextDifference(differences, "EventBoundaryVersion", legacy.EventBoundaryVersion, target.EventBoundaryVersion);
        return differences;
    }

    private static IReadOnlyList<RuntimeMetricDifference> CompareMetrics(RuntimeSnapshot legacy, RuntimeSnapshot target)
    {
        var differences = new List<RuntimeMetricDifference>();
        AddNumberDifference(differences, "PhysicalRuntime", legacy.PhysicalRuntimeMinutes, target.PhysicalRuntimeMinutes);
        AddNumberDifference(differences, "ESDAdjustment", legacy.EsdAdjustmentMinutes, target.EsdAdjustmentMinutes);
        AddNumberDifference(differences, "AdjustedRuntime", legacy.AdjustedRuntimeMinutes, target.AdjustedRuntimeMinutes);
        AddNumberDifference(differences, "RuntimeAfterOH", legacy.RuntimeAfterOhMinutes, target.RuntimeAfterOhMinutes);
        AddNumberDifference(differences, "LongestRun", legacy.LongestRunMinutes, target.LongestRunMinutes);
        AddNumberDifference(differences, "ServiceDayCount", legacy.ServiceDayCount, target.ServiceDayCount);
        AddTextDifference(differences, "FinalState", legacy.FinalState.ToString(), target.FinalState.ToString());
        return differences;
    }

    private static void AddNumberDifference(
        ICollection<RuntimeMetricDifference> differences,
        string metric,
        long legacy,
        long target)
    {
        if (legacy != target)
            differences.Add(new(metric, legacy.ToString(System.Globalization.CultureInfo.InvariantCulture),
                target.ToString(System.Globalization.CultureInfo.InvariantCulture), checked(target - legacy)));
    }

    private static void AddTextDifference(
        ICollection<RuntimeMetricDifference> differences,
        string metric,
        string legacy,
        string target)
    {
        if (!StringComparer.Ordinal.Equals(legacy, target))
            differences.Add(new(metric, legacy, target));
    }
}
