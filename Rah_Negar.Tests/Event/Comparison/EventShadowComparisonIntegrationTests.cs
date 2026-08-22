using Rah_Negar.Core.Event.Comparison;
using Rah_Negar.Core.Event.Rules;
using Rah_Negar.Foundation.Application.Event.Comparison;

namespace Rah_Negar.Tests.Event.Comparison;

public sealed class EventShadowComparisonIntegrationTests
{
    private readonly EventComparisonService _service = new(new EventStateTransitionEvaluator());

    public static IEnumerable<object[]> FixtureCases()
    {
        yield return new object[] { EventComparisonFixtures.NormalLifecycle("legacy"), EventComparisonFixtures.NormalLifecycle(), DifferenceCategory.Equivalent };
        yield return new object[] { EventComparisonFixtures.OhScenario("legacy"), EventComparisonFixtures.OhScenario(), DifferenceCategory.Equivalent };
        yield return new object[] { EventComparisonFixtures.EsdScenario("legacy"), EventComparisonFixtures.EsdScenario(), DifferenceCategory.Equivalent };
        yield return new object[] { EventComparisonFixtures.DuplicateScenario(), EventComparisonFixtures.NormalLifecycle(), DifferenceCategory.CriticalStateDifference };
        yield return new object[] { EventComparisonFixtures.InvalidScenario(), EventComparisonFixtures.NormalLifecycle(), DifferenceCategory.CriticalStateDifference };
        yield return new object[] { EventComparisonFixtures.MissingScenario(), EventComparisonFixtures.NormalLifecycle(), DifferenceCategory.CriticalStateDifference };
    }

    [Theory]
    [MemberData(nameof(FixtureCases))]
    public void Compare_FixtureDataset_ReturnsExpectedClassification(
        EventSequenceSnapshot legacy, EventSequenceSnapshot target, DifferenceCategory expected) =>
        Assert.Equal(expected, _service.Compare(legacy, target).Category);

    [Fact]
    public async Task CompareLegacyAsync_UsesReadOnlySnapshotContract()
    {
        var reader = new FixtureLegacyReader(EventComparisonFixtures.NormalLifecycle("legacy"));
        var request = new LegacyEventReadRequest("station-rasht", "unit-1");

        var result = await _service.CompareLegacyAsync(reader, request, EventComparisonFixtures.NormalLifecycle());

        Assert.Equal(DifferenceCategory.Equivalent, result.Category);
        Assert.Equal(request, reader.LastRequest);
    }

    private sealed class FixtureLegacyReader(EventSequenceSnapshot snapshot) : ILegacyEventReader
    {
        public LegacyEventReadRequest? LastRequest { get; private set; }

        public Task<EventSequenceSnapshot> ReadSnapshotAsync(LegacyEventReadRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(snapshot);
        }
    }
}
