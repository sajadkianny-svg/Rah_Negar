namespace Rah_Negar.Core.Runtime.Comparison;

public sealed record RuntimeMetricDifference(
    string Metric,
    string LegacyValue,
    string NewEngineValue,
    long? Delta = null);

public sealed record RuntimeComparisonResult(
    RuntimeDifferenceCategory Category,
    RuntimeSnapshot Legacy,
    RuntimeSnapshot NewEngine,
    IReadOnlyList<RuntimeMetricDifference> Differences,
    string? ClassificationReason)
{
    public bool IsMatch => Category == RuntimeDifferenceCategory.Match;
}
