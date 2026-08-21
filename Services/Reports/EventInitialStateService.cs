using Microsoft.Data.Sqlite;
using Rah_Negar.Core.Reports;
using Rah_Negar.Models.Reports;

namespace Rah_Negar.Services.Reports;

/// <summary>
/// سرویس تشخیص وضعیت اولیه واحدها در ابتدای بازه گزارش
/// اگر قبل از بازه رویداد وجود داشته باشد، وضعیت از آخرین رویدادها استخراج می‌شود
/// اگر هیچ رویدادی قبل از بازه وجود نداشته باشد، وضعیت از مقدار initial_is_running خوانده می‌شود
/// </summary>
public static class EventInitialStateService
{
    public static Dictionary<string, UnitInitialEventState> LoadInitialStates(
        SqliteConnection conn,
        ReportStationProfile profile,
        long dateFrom)
    {
        Dictionary<string, UnitInitialEventState> result = [];

        Dictionary<string, bool> initialRunningMap =
            LoadInitialRunningStates(conn);

        foreach (string unit in profile.Units)
        {
            EventLogItem? lastStart = LoadLastEventBeforeDate(conn, unit, "START", dateFrom);
            EventLogItem? lastNSD = LoadLastEventBeforeDate(conn, unit, "NSD", dateFrom);
            EventLogItem? lastESD = LoadLastEventBeforeDate(conn, unit, "ESD", dateFrom);
            EventLogItem? lastOH = LoadLastEventBeforeDate(conn, unit, "OH", dateFrom);

            bool hasAnyEvent =
                lastStart != null ||
                lastNSD != null ||
                lastESD != null ||
                lastOH != null;

            DateTime? lastStopTime =
                MaxDateTime(lastNSD?.EventDateTime, lastESD?.EventDateTime);

            bool isRunning;

            if (!hasAnyEvent)
            {
                isRunning =
                    initialRunningMap.TryGetValue(unit, out bool initialRunning) &&
                    initialRunning;
            }
            else
            {
                isRunning =
                    lastStart != null &&
                    (!lastStopTime.HasValue || lastStart.EventDateTime > lastStopTime.Value);
            }

            bool hasSeenOH = lastOH != null;

            // طبق منطق جدید:
            // اگر واحد در ابتدای بازه روشن باشد، RuntimeAfterOH هم باید باز باشد
            bool isRunningAfterOH = isRunning;

            result[unit] = new UnitInitialEventState
            {
                Unit = unit,
                IsRunningAtPeriodStart = isRunning,
                HasSeenOHBeforePeriod = hasSeenOH,
                IsRunningAfterOHAtPeriodStart = isRunningAfterOH
            };
        }

        return result;
    }

    /// <summary>
    /// خواندن وضعیت اولیه روشن/خاموش بودن واحدها از جدول unit_runtime_base
    /// </summary>
    private static Dictionary<string, bool> LoadInitialRunningStates(SqliteConnection conn)
    {
        Dictionary<string, bool> map = [];

        using SqliteCommand cmd = conn.CreateCommand();

        cmd.CommandText = """
            SELECT unit_no, initial_is_running
            FROM unit_runtime_base;
            """;

        using SqliteDataReader reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            int unitNo = Convert.ToInt32(reader["unit_no"]);
            bool isRunning = Convert.ToInt32(reader["initial_is_running"]) == 1;

            string unit = $"U{unitNo}";
            map[unit] = isRunning;
        }

        return map;
    }

    /// <summary>
    /// آخرین رویداد مشخص‌شده را قبل از تاریخ شروع گزارش می‌خواند
    /// </summary>
    private static EventLogItem? LoadLastEventBeforeDate(
        SqliteConnection conn,
        string unit,
        string eventType,
        long dateFrom)
    {
        using SqliteCommand cmd = conn.CreateCommand();

        cmd.CommandText = """
            SELECT date_rep, unit, event_type, event_time, remark
            FROM tbl_events
            WHERE unit = $unit
              AND event_type = $eventType
              AND date_rep < $dateFrom
            ORDER BY date_rep DESC, event_time DESC
            LIMIT 1;
            """;

        cmd.Parameters.AddWithValue("$unit", unit);
        cmd.Parameters.AddWithValue("$eventType", eventType);
        cmd.Parameters.AddWithValue("$dateFrom", dateFrom);

        using SqliteDataReader reader = cmd.ExecuteReader();

        if (!reader.Read())
            return null;

        long eventDate = Convert.ToInt64(reader["date_rep"]);
        string eventTime = NormalizeTime(reader["event_time"]?.ToString());

        return new EventLogItem
        {
            Unit = NormalizeUnit(reader["unit"]?.ToString()),
            EventType = NormalizeEventType(reader["event_type"]?.ToString()),
            EventDate = eventDate,
            EventTime = eventTime,
            EventDateTime = ConvertPersianDateTimeToGregorian(eventDate, eventTime),
            Remark = reader["remark"]?.ToString() ?? string.Empty
        };
    }

    /// <summary>
    /// بزرگ‌ترین مقدار DateTime بین دو مقدار nullable را برمی‌گرداند
    /// </summary>
    private static DateTime? MaxDateTime(DateTime? first, DateTime? second)
    {
        if (!first.HasValue)
            return second;

        if (!second.HasValue)
            return first;

        return first.Value >= second.Value ? first : second;
    }

    /// <summary>
    /// نرمال‌سازی نام واحد
    /// </summary>
    private static string NormalizeUnit(string? value)
    {
        return (value ?? string.Empty)
            .Trim()
            .ToUpperInvariant()
            .Replace("UNIT", "U")
            .Replace(" ", "");
    }

    /// <summary>
    /// نرمال‌سازی نوع رویداد
    /// </summary>
    private static string NormalizeEventType(string? value)
    {
        return (value ?? string.Empty)
            .Trim()
            .ToUpperInvariant()
            .Replace(".", "");
    }

    /// <summary>
    /// نرمال‌سازی ساعت رویداد
    /// </summary>
    private static string NormalizeTime(string? value)
    {
        string text = (value ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(text))
            return "00:00";

        return TimeSpan.TryParse(text, out TimeSpan ts)
            ? ts.ToString(@"hh\:mm")
            : "00:00";
    }

    /// <summary>
    /// تبدیل تاریخ شمسی عددی و ساعت به DateTime میلادی
    /// </summary>
    private static DateTime ConvertPersianDateTimeToGregorian(long persianDate, string time)
    {
        int year = (int)(persianDate / 10000);
        int month = (int)((persianDate / 100) % 100);
        int day = (int)(persianDate % 100);

        TimeSpan timeSpan = TimeSpan.TryParse(time, out TimeSpan parsed)
            ? parsed
            : TimeSpan.Zero;

        System.Globalization.PersianCalendar calendar = new();

        return calendar.ToDateTime(
            year,
            month,
            day,
            timeSpan.Hours,
            timeSpan.Minutes,
            0,
            0);
    }
}