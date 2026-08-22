namespace Rah_Negar.Core.Event.Comparison;

public sealed record EventSequenceSnapshot(
    string SourceName,
    string StationId,
    string UnitId,
    EventOperationalState BaselineState,
    IReadOnlyList<NormalizedEvent> Events,
    EventOperationalState? ReportedFinalState = null,
    bool? ReportedIsValid = null);
