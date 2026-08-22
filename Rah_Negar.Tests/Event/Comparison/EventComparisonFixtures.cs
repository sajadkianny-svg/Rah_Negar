using Rah_Negar.Core.Event;
using Rah_Negar.Core.Event.Comparison;

namespace Rah_Negar.Tests.Event.Comparison;

internal static class EventComparisonFixtures
{
    internal static EventSequenceSnapshot NormalLifecycle(string source = "target") => Snapshot(source,
        At("01", EventType.Start, 60, 0), At("02", EventType.Nsd, 120, 1));

    internal static EventSequenceSnapshot OhScenario(string source = "target") => Snapshot(source,
        At("01", EventType.Oh, 60, 0), At("02", EventType.Start, 120, 1));

    internal static EventSequenceSnapshot EsdScenario(string source = "target") => Snapshot(source,
        At("01", EventType.Start, 60, 0), At("02", EventType.Esd, 120, 1));

    internal static EventSequenceSnapshot DuplicateScenario(string source = "legacy") => Snapshot(source,
        At("01", EventType.Start, 60, 0), At("02", EventType.Nsd, 60, 1));

    internal static EventSequenceSnapshot InvalidScenario(string source = "legacy") => Snapshot(source,
        At("01", EventType.Start, 60, 0), At("02", EventType.Start, 120, 1));

    internal static EventSequenceSnapshot MissingScenario(string source = "legacy") => Snapshot(source,
        At("01", EventType.Start, 60, 0));

    internal static EventSequenceSnapshot Snapshot(string source, params NormalizedEvent[] events) =>
        new(source, "station-rasht", "unit-1", EventOperationalState.Stopped, events);

    internal static NormalizedEvent At(string id, EventType type, int minute, int ordinal, params string[] notes) =>
        new(id, "station-rasht", "unit-1", type, 14050101, minute, 638000000L + minute, ordinal, notes);
}
