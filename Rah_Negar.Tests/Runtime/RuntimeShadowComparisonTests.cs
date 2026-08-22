using Rah_Negar.Core.Runtime;
using Rah_Negar.Core.Runtime.Comparison;

namespace Rah_Negar.Tests.Runtime;

public sealed class RuntimeShadowComparisonTests
{
    private readonly RuntimeComparisonService _service = new();

    [Fact]
    public void Normalization_UsesIntegralMinutes()
    {
        Assert.Equal(90, RuntimeSnapshotNormalizer.WholeMinutes(TimeSpan.FromMinutes(90)));
        Assert.Equal(125, RuntimeSnapshotNormalizer.WholeMinutes(TimeSpan.FromMinutes(125)));
        Assert.Throws<ArgumentException>(() => RuntimeSnapshotNormalizer.WholeMinutes(TimeSpan.FromSeconds(61)));
    }

    [Fact]
    public void Normalization_RejectsInconsistentAdjustedRuntime()
    {
        Assert.Throws<ArgumentException>(() => Snapshot(
            physical: 60,
            esd: 20,
            adjusted: 79));
    }

    [Fact]
    public void EqualAuthoritativeMinutes_MatchRegardlessOfSourceOrCalculationLabel()
    {
        RuntimeSnapshot legacy = Snapshot(source: "legacy", calculation: "legacy-hours-display-1.50");
        RuntimeSnapshot target = Snapshot(source: "new", calculation: "runtime-4.2-v1");

        RuntimeComparisonResult result = _service.Compare(legacy, target);

        Assert.True(result.IsMatch);
        Assert.Equal(RuntimeDifferenceCategory.Match, result.Category);
        Assert.Empty(result.Differences);
    }

    [Fact]
    public void DifferentInputPeriod_IsInputMismatchBeforeMetricComparison()
    {
        RuntimeSnapshot legacy = Snapshot(periodEnd: 300);
        RuntimeSnapshot target = Snapshot(periodEnd: 301, physical: 61, adjusted: 61);

        RuntimeComparisonResult result = _service.Compare(legacy, target);

        Assert.Equal(RuntimeDifferenceCategory.InputMismatch, result.Category);
        Assert.Contains(result.Differences, x => x.Metric == "PeriodEndMinute");
        Assert.DoesNotContain(result.Differences, x => x.Metric == "PhysicalRuntime");
    }

    [Fact]
    public void UnexplainedMetricDifference_DefaultsToNewEngineDefect()
    {
        RuntimeSnapshot legacy = Snapshot();
        RuntimeSnapshot target = Snapshot(physical: 61, adjusted: 61);

        RuntimeComparisonResult result = _service.Compare(legacy, target);

        Assert.Equal(RuntimeDifferenceCategory.NewEngineDefect, result.Category);
        Assert.Collection(
            result.Differences,
            difference =>
            {
                Assert.Equal("PhysicalRuntime", difference.Metric);
                Assert.Equal(1, difference.Delta);
            },
            difference =>
            {
                Assert.Equal("AdjustedRuntime", difference.Metric);
                Assert.Equal(1, difference.Delta);
            });
    }

    [Theory]
    [InlineData(RuntimeDifferenceCategory.ExpectedPolicyDifference)]
    [InlineData(RuntimeDifferenceCategory.LegacyDefect)]
    public void EvidenceBackedDifference_UsesExplicitClassification(RuntimeDifferenceCategory category)
    {
        RuntimeSnapshot legacy = Snapshot();
        RuntimeSnapshot target = Snapshot(physical: 61, adjusted: 61);

        RuntimeComparisonResult result = _service.Compare(legacy, target, category, "approved evidence REF-1");

        Assert.Equal(category, result.Category);
        Assert.Equal("approved evidence REF-1", result.ClassificationReason);
    }

    [Fact]
    public void ExpectedDifferenceWithoutReason_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => _service.Compare(
            Snapshot(), Snapshot(physical: 61, adjusted: 61),
            RuntimeDifferenceCategory.ExpectedPolicyDifference));
    }

    [Fact]
    public void Comparison_ReportsEveryRequiredMetricAndFinalStateDifference()
    {
        RuntimeSnapshot legacy = Snapshot();
        RuntimeSnapshot target = Snapshot(
            physical: 61,
            esd: 2,
            adjusted: 63,
            afterOh: 70,
            longest: 50,
            serviceDays: 2,
            state: UnitOperationalState.Running);

        RuntimeComparisonResult result = _service.Compare(legacy, target);

        Assert.Equal(7, result.Differences.Count);
        Assert.Equal(
            new[] { "PhysicalRuntime", "ESDAdjustment", "AdjustedRuntime", "RuntimeAfterOH", "LongestRun", "ServiceDayCount", "FinalState" },
            result.Differences.Select(x => x.Metric));
    }

    [Theory]
    [MemberData(nameof(MatchingFixtureData))]
    public void SyntheticFixture_ProducesDeterministicMatch(
        string scenario,
        RuntimeSnapshot legacy,
        RuntimeSnapshot target)
    {
        RuntimeComparisonResult result = _service.Compare(legacy, target);

        Assert.False(string.IsNullOrWhiteSpace(scenario));
        Assert.Equal(RuntimeDifferenceCategory.Match, result.Category);
    }

    [Fact]
    public void IntentionalFixture_IsExpectedPolicyDifference()
    {
        object[] fixture = RuntimeShadowComparisonFixtures.IntentionalDifference();

        RuntimeComparisonResult result = _service.Compare(
            (RuntimeSnapshot)fixture[1],
            (RuntimeSnapshot)fixture[2],
            RuntimeDifferenceCategory.ExpectedPolicyDifference,
            "Synthetic current-ESD-policy difference.");

        Assert.Equal(RuntimeDifferenceCategory.ExpectedPolicyDifference, result.Category);
        Assert.Contains(result.Differences, x => x.Metric == "ESDAdjustment" && x.Delta == 20);
        Assert.Contains(result.Differences, x => x.Metric == "AdjustedRuntime" && x.Delta == 20);
    }

    public static IEnumerable<object[]> MatchingFixtureData() =>
        RuntimeShadowComparisonFixtures.MatchingScenarios();

    private static RuntimeSnapshot Snapshot(
        string source = "legacy",
        long periodEnd = 300,
        long physical = 60,
        long esd = 0,
        long adjusted = 60,
        long afterOh = 60,
        long longest = 60,
        int serviceDays = 1,
        UnitOperationalState state = UnitOperationalState.Stopped,
        string calculation = "fixture-v1") =>
        RuntimeSnapshotNormalizer.Create(
            source,
            "station-rasht",
            "unit-1",
            0,
            periodEnd,
            "events-v1",
            physical,
            esd,
            adjusted,
            afterOh,
            longest,
            serviceDays,
            state,
            calculation);
}
