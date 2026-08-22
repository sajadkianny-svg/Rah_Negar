using Rah_Negar.Core.Event;
using Rah_Negar.Core.Event.Comparison;
using Rah_Negar.Core.Event.Rules;

namespace Rah_Negar.Foundation.Application.Event.Comparison;

public sealed class EventComparisonService
{
    private readonly IEventStateTransitionEvaluator _transitions;

    public EventComparisonService(IEventStateTransitionEvaluator transitions) =>
        _transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));

    public async Task<EventComparisonResult> CompareLegacyAsync(
        ILegacyEventReader reader,
        LegacyEventReadRequest request,
        EventSequenceSnapshot target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var legacy = await reader.ReadSnapshotAsync(request, cancellationToken).ConfigureAwait(false);
        return Compare(legacy, target);
    }

    public EventComparisonResult Compare(EventSequenceSnapshot legacy, EventSequenceSnapshot target)
    {
        ArgumentNullException.ThrowIfNull(legacy);
        ArgumentNullException.ThrowIfNull(target);
        if (legacy.StationId != target.StationId || legacy.UnitId != target.UnitId)
            throw new ArgumentException("Snapshots must describe the same Station and Unit.");

        var differences = new List<string>();
        var legacyEvaluation = Evaluate(legacy);
        var targetEvaluation = Evaluate(target);
        var category = DifferenceCategory.Equivalent;

        if (legacy.Events.Count != target.Events.Count)
        {
            category = Max(category, DifferenceCategory.LegacyDataIssue);
            differences.Add("event-count");
        }

        if (!IsSourceChronological(legacy.Events))
        {
            category = Max(category, DifferenceCategory.LegacyDataIssue);
            differences.Add("legacy-ordering");
        }

        var left = CanonicalOrder(legacy.Events);
        var right = CanonicalOrder(target.Events);
        foreach (var pair in left.Zip(right))
        {
            if (pair.First.EventDateTime != pair.Second.EventDateTime)
            {
                category = Max(category, DifferenceCategory.LegacyDataIssue);
                AddOnce(differences, "chronology");
            }
            else if (pair.First.EventType != pair.Second.EventType)
            {
                category = Max(category, DifferenceCategory.RuleDifference);
                AddOnce(differences, "event-type");
            }
        }

        if (legacy.Events.Any(x => x.HasFormattingDifferences) || target.Events.Any(x => x.HasFormattingDifferences))
        {
            category = Max(category, DifferenceCategory.FormattingDifference);
            differences.Add("source-formatting");
        }

        var legacyReportedMismatch = legacy.ReportedFinalState.HasValue && legacy.ReportedFinalState != legacyEvaluation.FinalState;
        var legacyValidityMismatch = legacy.ReportedIsValid.HasValue && legacy.ReportedIsValid != legacyEvaluation.IsValid;
        if (legacyEvaluation.IsValid != targetEvaluation.IsValid || legacyEvaluation.FinalState != targetEvaluation.FinalState ||
            legacyReportedMismatch || legacyValidityMismatch)
        {
            category = DifferenceCategory.CriticalStateDifference;
            differences.Add("resulting-chain-state");
        }

        return new EventComparisonResult(category, differences, legacy.Events.Count, target.Events.Count,
            legacyEvaluation.IsValid, targetEvaluation.IsValid, legacyEvaluation.FinalState, targetEvaluation.FinalState);
    }

    private ChainResult Evaluate(EventSequenceSnapshot snapshot)
    {
        var state = snapshot.BaselineState;
        var ordered = CanonicalOrder(snapshot.Events);
        for (var index = 0; index < ordered.Count; index++)
        {
            if (index > 0 && ordered[index - 1].EventDateTime == ordered[index].EventDateTime)
                return new ChainResult(false, state);
            var result = _transitions.Evaluate(state, ordered[index].EventType);
            if (!result.IsValid)
                return new ChainResult(false, state);
            state = result.NextState!.Value;
        }
        return new ChainResult(true, state);
    }

    private static List<NormalizedEvent> CanonicalOrder(IReadOnlyList<NormalizedEvent> events) =>
        events.OrderBy(x => x.EventDateTime).ThenBy(x => x.SourceEventId, StringComparer.Ordinal).ToList();

    private static bool IsSourceChronological(IReadOnlyList<NormalizedEvent> events) =>
        events.SequenceEqual(events.OrderBy(x => x.EventDateTime).ThenBy(x => x.SourceEventId, StringComparer.Ordinal));

    private static DifferenceCategory Max(DifferenceCategory left, DifferenceCategory right) =>
        (DifferenceCategory)Math.Max((int)left, (int)right);

    private static void AddOnce(List<string> values, string value)
    {
        if (!values.Contains(value, StringComparer.Ordinal)) values.Add(value);
    }

    private sealed record ChainResult(bool IsValid, EventOperationalState FinalState);
}
