namespace Rah_Negar.Core.Event.Comparison;

public sealed record NormalizedEvent(
    string SourceEventId,
    string StationId,
    string UnitId,
    EventType EventType,
    int EventDate,
    int EventTimeMinutes,
    long EventDateTime,
    int SourceOrdinal,
    IReadOnlyList<string> FormattingNotes)
{
    public bool HasFormattingDifferences => FormattingNotes.Count > 0;
}

public sealed record EventSourceRecord(
    string SourceEventId,
    string StationId,
    string UnitId,
    string EventType,
    int EventDate,
    string EventTime,
    int SourceOrdinal);
