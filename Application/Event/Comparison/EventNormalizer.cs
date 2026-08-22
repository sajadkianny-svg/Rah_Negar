using System.Globalization;
using Rah_Negar.Core.Event;
using Rah_Negar.Core.Event.Comparison;
using Rah_Negar.Foundation.Errors;

namespace Rah_Negar.Foundation.Application.Event.Comparison;

public sealed class EventNormalizer
{
    private static readonly PersianCalendar PersianCalendar = new();

    public Result<NormalizedEvent> Normalize(EventSourceRecord source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var notes = new List<string>();
        var typeText = source.EventType.Trim().ToUpperInvariant();
        if (!EventTypeCode.TryParse(typeText, out var type))
            return Result<NormalizedEvent>.Failure(ApplicationError.Create("event.comparison.type.invalid", "The source Event type is not recognized."));
        if (!string.Equals(source.EventType, typeText, StringComparison.Ordinal))
            notes.Add("event-type-format");

        var pieces = source.EventTime.Trim().Split(':');
        if (pieces.Length != 2 || !int.TryParse(pieces[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hour) ||
            !int.TryParse(pieces[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minute) ||
            hour is < 0 or > 23 || minute is < 0 or > 59)
            return Result<NormalizedEvent>.Failure(ApplicationError.Create("event.comparison.time.invalid", "The source Event time is not a valid minute value."));
        var canonicalTime = $"{hour:00}:{minute:00}";
        if (!string.Equals(source.EventTime, canonicalTime, StringComparison.Ordinal))
            notes.Add("event-time-format");

        try
        {
            var year = source.EventDate / 10000;
            var month = source.EventDate / 100 % 100;
            var day = source.EventDate % 100;
            var local = PersianCalendar.ToDateTime(year, month, day, hour, minute, 0, 0);
            return Result<NormalizedEvent>.Success(new NormalizedEvent(
                source.SourceEventId, source.StationId, source.UnitId, type, source.EventDate,
                hour * 60 + minute, local.Ticks / TimeSpan.TicksPerMinute, source.SourceOrdinal, notes));
        }
        catch (ArgumentOutOfRangeException)
        {
            return Result<NormalizedEvent>.Failure(ApplicationError.Create("event.comparison.date.invalid", "The source Persian Event date is invalid."));
        }
    }
}
