using Rah_Negar.Core.Event;
using Rah_Negar.Core.Event.Comparison;
using Rah_Negar.Core.Event.Rules;
using Rah_Negar.Foundation.Application.Event.Comparison;

namespace Rah_Negar.Tests.Event.Comparison;

public sealed class EventComparisonServiceTests
{
    private readonly EventComparisonService _service = new(new EventStateTransitionEvaluator());

    [Fact]
    public void Compare_EquivalentSequences_AreEquivalent()
    {
        var result = _service.Compare(EventComparisonFixtures.NormalLifecycle("legacy"), EventComparisonFixtures.NormalLifecycle());
        Assert.Equal(DifferenceCategory.Equivalent, result.Category);
        Assert.True(result.IsEquivalent);
    }

    [Fact]
    public void Compare_OnlySourceFormattingDiffers_ClassifiesFormattingDifference()
    {
        var legacy = EventComparisonFixtures.Snapshot("legacy",
            EventComparisonFixtures.At("01", EventType.Start, 60, 0, "event-time-format"),
            EventComparisonFixtures.At("02", EventType.Nsd, 120, 1));
        var result = _service.Compare(legacy, EventComparisonFixtures.NormalLifecycle());
        Assert.Equal(DifferenceCategory.FormattingDifference, result.Category);
    }

    [Fact]
    public void Compare_OutOfOrderLegacyEvents_ClassifiesLegacyDataIssue()
    {
        var legacy = EventComparisonFixtures.Snapshot("legacy",
            EventComparisonFixtures.At("02", EventType.Nsd, 120, 0),
            EventComparisonFixtures.At("01", EventType.Start, 60, 1));
        var result = _service.Compare(legacy, EventComparisonFixtures.NormalLifecycle());
        Assert.Equal(DifferenceCategory.LegacyDataIssue, result.Category);
        Assert.Contains("legacy-ordering", result.Differences);
    }

    [Fact]
    public void Compare_TypeDifferenceWithSameResultingState_ClassifiesRuleDifference()
    {
        var result = _service.Compare(EventComparisonFixtures.EsdScenario("legacy"), EventComparisonFixtures.NormalLifecycle());
        Assert.Equal(DifferenceCategory.RuleDifference, result.Category);
    }

    [Fact]
    public void Compare_InvalidLegacyChain_ClassifiesCriticalStateDifference()
    {
        var result = _service.Compare(EventComparisonFixtures.InvalidScenario(), EventComparisonFixtures.NormalLifecycle());
        Assert.Equal(DifferenceCategory.CriticalStateDifference, result.Category);
        Assert.False(result.LegacyChainIsValid);
        Assert.True(result.TargetChainIsValid);
    }

    [Fact]
    public void Compare_ReportedLegacyStateDisagreesWithReplay_ClassifiesCriticalStateDifference()
    {
        var source = EventComparisonFixtures.NormalLifecycle("legacy");
        var legacy = source with { ReportedFinalState = EventOperationalState.Running };
        Assert.Equal(DifferenceCategory.CriticalStateDifference,
            _service.Compare(legacy, EventComparisonFixtures.NormalLifecycle()).Category);
    }
}
