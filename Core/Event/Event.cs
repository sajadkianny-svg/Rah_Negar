namespace Rah_Negar.Core.Event;

public sealed record Event
{
    private Event()
    {
    }

    public required string EventId { get; init; }
    public required string StationId { get; init; }
    public required string UnitId { get; init; }
    public required EventType EventType { get; init; }
    public required int EventDate { get; init; }
    public required int EventTime { get; init; }
    public required long EventDateTime { get; init; }
    public string? Remark { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required Guid CreatedByShiftProfileId { get; init; }
    public DateTimeOffset? UpdatedAtUtc { get; init; }
    public EventStatus Status { get; init; } = EventStatus.Active;
    public DateTimeOffset? DeletedAtUtc { get; init; }
    public Guid? DeletedByShiftProfileId { get; init; }
    public long RowVersion { get; init; } = 1;

    public static EventCreationResult Create(
        string eventId,
        string stationId,
        string unitId,
        EventType eventType,
        int eventDate,
        int eventTime,
        long eventDateTime,
        string? remark,
        DateTimeOffset createdAtUtc,
        Guid createdByShiftProfileId)
    {
        var errors = new List<EventValidationError>();
        if (!EventIdentity.IsCanonicalUlid(eventId))
            errors.Add(new("event.id.invalid", "EventId must be a canonical uppercase ULID."));
        if (string.IsNullOrWhiteSpace(stationId))
            errors.Add(new("event.station.required", "StationId is required."));
        if (string.IsNullOrWhiteSpace(unitId))
            errors.Add(new("event.unit.required", "UnitId is required."));
        if (!Enum.IsDefined(eventType))
            errors.Add(new("event.type.invalid", "EventType is not supported."));
        if (!IsValidPersianDate(eventDate))
            errors.Add(new("event.date.invalid", "EventDate is not a valid Persian calendar date."));
        if (eventTime is < 0 or > 1439)
            errors.Add(new("event.time.invalid", "EventTime must be between 0 and 1439."));
        if (((eventDateTime % 1440) + 1440) % 1440 != eventTime)
            errors.Add(new("event.datetime.inconsistent", "EventDateTime does not match EventTime."));
        if (createdAtUtc.Offset != TimeSpan.Zero)
            errors.Add(new("event.created-at.not-utc", "CreatedAt must use UTC."));
        if (createdByShiftProfileId == Guid.Empty)
            errors.Add(new("event.actor.required", "CreatedByShiftProfileId is required."));

        if (errors.Count > 0)
            return new EventCreationResult(null, EventValidationResult.Invalid(errors.ToArray()));

        return new EventCreationResult(
            new Event
            {
                EventId = eventId,
                StationId = stationId.Trim(),
                UnitId = unitId.Trim(),
                EventType = eventType,
                EventDate = eventDate,
                EventTime = eventTime,
                EventDateTime = eventDateTime,
                Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim(),
                CreatedAtUtc = createdAtUtc,
                CreatedByShiftProfileId = createdByShiftProfileId
            },
            EventValidationResult.Valid());
    }

    internal static Event Rehydrate(
        string eventId, string stationId, string unitId, EventType eventType,
        int eventDate, int eventTime, long eventDateTime, string? remark,
        DateTimeOffset createdAtUtc, Guid createdByShiftProfileId,
        DateTimeOffset? updatedAtUtc, EventStatus status,
        DateTimeOffset? deletedAtUtc, Guid? deletedByShiftProfileId, long rowVersion) =>
        new()
        {
            EventId = eventId,
            StationId = stationId,
            UnitId = unitId,
            EventType = eventType,
            EventDate = eventDate,
            EventTime = eventTime,
            EventDateTime = eventDateTime,
            Remark = remark,
            CreatedAtUtc = createdAtUtc,
            CreatedByShiftProfileId = createdByShiftProfileId,
            UpdatedAtUtc = updatedAtUtc,
            Status = status,
            DeletedAtUtc = deletedAtUtc,
            DeletedByShiftProfileId = deletedByShiftProfileId,
            RowVersion = rowVersion
        };

    private static bool IsValidPersianDate(int value)
    {
        int year = value / 10000;
        int month = value / 100 % 100;
        int day = value % 100;
        if (value is < 10000101 or > 99991231 || month is < 1 or > 12 || day is < 1 or > 31)
            return false;

        try
        {
            _ = new System.Globalization.PersianCalendar().ToDateTime(year, month, day, 0, 0, 0, 0);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}

internal static class EventIdentity
{
    private const string Allowed = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static bool IsCanonicalUlid(string? value) =>
        value is { Length: 26 } && value.All(Allowed.Contains);
}
