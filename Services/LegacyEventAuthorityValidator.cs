using System.Globalization;
using Microsoft.Data.Sqlite;
using Rah_Negar.Models;

namespace Rah_Negar.Services;

/// <summary>
/// Below-UI authority for the legacy Events table. It rejects malformed data,
/// duplicate chronological minutes, and invalid state transitions before commit.
/// </summary>
internal static class LegacyEventAuthorityValidator
{
    private static readonly PersianCalendar PersianCalendar = new();

    public static void ValidateReplacement(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long dateRep,
        IReadOnlyCollection<DailyEventRowModel> replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        HashSet<string> units = replacement.Select(x => NormalizeUnit(x.Unit))
            .Where(x => x.Length > 0).ToHashSet(StringComparer.Ordinal);
        using (SqliteCommand existing = connection.CreateCommand())
        {
            existing.Transaction = transaction;
            existing.CommandText = "SELECT DISTINCT unit FROM tbl_events WHERE date_rep = @date;";
            existing.Parameters.AddWithValue("@date", dateRep);
            using SqliteDataReader reader = existing.ExecuteReader();
            while (reader.Read()) units.Add(NormalizeUnit(reader.GetString(0)));
        }

        foreach (string unit in units)
        {
            List<LegacyEvent> chain = LoadExisting(connection, transaction, unit);
            chain.AddRange(replacement.Where(x => NormalizeUnit(x.Unit) == unit).Select(ToEvent));
            ValidateChain(connection, transaction, unit, chain);
        }
    }

    public static HashSet<string> ReadUnitsOnDate(
        SqliteConnection connection, SqliteTransaction transaction, long dateRep)
    {
        var units = new HashSet<string>(StringComparer.Ordinal);
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT DISTINCT unit FROM tbl_events WHERE date_rep = @date;";
        command.Parameters.AddWithValue("@date", dateRep);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read()) units.Add(NormalizeUnit(reader.GetString(0)));
        return units;
    }

    public static void ValidateCurrentUnit(SqliteConnection connection,
        SqliteTransaction transaction, string unit)
    {
        ValidateChain(connection, transaction, unit, LoadExisting(connection, transaction, NormalizeUnit(unit)));
    }

    private static List<LegacyEvent> LoadExisting(SqliteConnection connection,
        SqliteTransaction transaction, string unit)
    {
        var result = new List<LegacyEvent>();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT date_rep, unit, event_type, event_time
            FROM tbl_events WHERE unit = @unit;
            """;
        command.Parameters.AddWithValue("@unit", unit);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            long date = reader.GetInt64(0);
            result.Add(CreateEvent(date, reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        }
        return result;
    }

    private static LegacyEvent ToEvent(DailyEventRowModel value) =>
        CreateEvent(value.DateRep, value.Unit, value.EventType, value.EventTime);

    private static LegacyEvent CreateEvent(long dateRep, string unit, string eventType, string time)
    {
        string normalizedUnit = NormalizeUnit(unit);
        if (!IsSupportedUnit(normalizedUnit))
            throw new InvalidOperationException("واحد رویداد معتبر نیست.");

        string normalizedType = (eventType ?? string.Empty).Trim().ToUpperInvariant();
        if (normalizedType is not ("START" or "NSD" or "ESD" or "OH"))
            throw new InvalidOperationException("نوع رویداد معتبر نیست.");

        if (dateRep is < 10000101 or > 99991231)
            throw new InvalidOperationException("تاریخ رویداد معتبر نیست.");
        int minutes = ParseMinutes(time);
        int year = checked((int)(dateRep / 10000));
        int month = checked((int)(dateRep / 100 % 100));
        int day = checked((int)(dateRep % 100));
        DateTime dateTime;
        try { dateTime = PersianCalendar.ToDateTime(year, month, day, minutes / 60, minutes % 60, 0, 0); }
        catch (ArgumentOutOfRangeException) { throw new InvalidOperationException("تاریخ رویداد معتبر نیست."); }
        return new(normalizedUnit, normalizedType, dateRep, minutes, dateTime);
    }

    private static void ValidateChain(SqliteConnection connection, SqliteTransaction transaction,
        string unit, List<LegacyEvent> events)
    {
        events.Sort((left, right) => left.DateTime.CompareTo(right.DateTime));
        EventState state = ReadInitialState(connection, transaction, unit);
        DateTime? previous = null;
        foreach (LegacyEvent item in events)
        {
            if (previous == item.DateTime)
                throw new InvalidOperationException("برای هر واحد، هر دقیقه فقط یک رویداد مجاز است.");
            EventState next = item.Type switch
            {
                "START" when state is EventState.Stopped or EventState.StoppedAfterOh => EventState.Running,
                "NSD" or "ESD" when state == EventState.Running => EventState.Stopped,
                "OH" when state is EventState.Stopped or EventState.Running => EventState.StoppedAfterOh,
                _ => throw new InvalidOperationException("توالی رویدادهای واحد معتبر نیست.")
            };
            state = next;
            previous = item.DateTime;
        }
    }

    private static EventState ReadInitialState(SqliteConnection connection,
        SqliteTransaction transaction, string unit)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT initial_is_running FROM unit_runtime_base WHERE unit_no = @unit LIMIT 1;";
        command.Parameters.AddWithValue("@unit", int.Parse(unit[1..], CultureInfo.InvariantCulture));
        object? result = command.ExecuteScalar();
        if (result is null or DBNull)
            throw new InvalidOperationException("وضعیت اولیه واحد در تنظیمات ثبت نشده است.");
        return Convert.ToInt32(result, CultureInfo.InvariantCulture) == 1
            ? EventState.Running : EventState.Stopped;
    }

    private static int ParseMinutes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !TimeSpan.TryParse(value.Trim(), CultureInfo.InvariantCulture, out TimeSpan parsed) ||
            parsed < TimeSpan.Zero || parsed >= TimeSpan.FromDays(1) ||
            parsed.Ticks % TimeSpan.TicksPerMinute != 0)
            throw new InvalidOperationException("ساعت رویداد معتبر نیست.");
        return checked(parsed.Hours * 60 + parsed.Minutes);
    }

    private static string NormalizeUnit(string? value) =>
        (value ?? string.Empty).Trim().ToUpperInvariant().Replace("UNIT", "U").Replace(" ", "");

    private static bool IsSupportedUnit(string value) =>
        value.Length == 2 && value[0] == 'U' && value[1] is >= '1' and <= '5';

    private sealed record LegacyEvent(string Unit, string Type, long DateRep, int Minutes, DateTime DateTime);
    private enum EventState { Stopped, Running, StoppedAfterOh }
}
