using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



using Microsoft.Data.Sqlite;
using Rah_Negar.Models.Reports;

namespace Rah_Negar.Services.Reports;

/// <summary>
/// سرویس خواندن رویدادها از جدول tbl_events برای گزارش‌گیری.
/// این سرویس فقط داده خام رویدادها را از دیتابیس می‌خواند و محاسبه انجام نمی‌دهد.
/// </summary>
public static class EventReportQueryService
{
    /// <summary>
    /// رویدادهای مورد نیاز برای بازسازی Runtime را از تاریخ مبنای داده‌ها
    /// تا پایان بازه گزارش می‌خواند.
    /// این مسیر فعلاً توسط گزارش تولیدی استفاده نمی‌شود.
    /// </summary>
    public static List<EventLogItem> LoadRuntimeHistory(
        SqliteConnection conn,
        long dataStartDate,
        long dateTo)
    {
        if (dataStartDate <= 0)
            throw new ArgumentOutOfRangeException(nameof(dataStartDate));

        if (dateTo < dataStartDate)
            return [];

        return LoadEvents(conn, dataStartDate, dateTo);
    }

    /// <summary>
    /// رویدادهای داخل بازه انتخاب‌شده را از جدول tbl_events می‌خواند.
    /// </summary>
    public static List<EventLogItem> LoadEvents(
        SqliteConnection conn,
        long dateFrom,
        long dateTo)
    {
        List<EventLogItem> result = [];

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT date_rep, unit, event_type, event_time, remark
            FROM tbl_events
            WHERE date_rep BETWEEN $from AND $to
            ORDER BY date_rep, event_time;
            """;

        cmd.Parameters.AddWithValue("$from", dateFrom);
        cmd.Parameters.AddWithValue("$to", dateTo);

        using SqliteDataReader reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            long eventDate = reader["date_rep"] == DBNull.Value
                ? 0
                : Convert.ToInt64(reader["date_rep"]);

            string unit = reader["unit"] == DBNull.Value
                ? string.Empty
                : NormalizeUnit(reader["unit"]?.ToString());

            string eventType = reader["event_type"] == DBNull.Value
                ? string.Empty
                : NormalizeEventType(reader["event_type"]?.ToString());

            string eventTime = reader["event_time"] == DBNull.Value
                ? string.Empty
                : NormalizeTime(reader["event_time"]?.ToString());

            string remark = reader["remark"] == DBNull.Value
                ? string.Empty
                : reader["remark"]?.ToString() ?? string.Empty;

            if (eventDate == 0 || string.IsNullOrWhiteSpace(unit) || string.IsNullOrWhiteSpace(eventType))
                continue;

            DateTime eventDateTime = ConvertPersianDateTimeToGregorian(eventDate, eventTime);

            result.Add(new EventLogItem
            {
                Unit = unit,
                EventType = eventType,
                EventDate = eventDate,
                EventTime = eventTime,
                EventDateTime = eventDateTime,
                Remark = remark
            });
        }

        return result
            .OrderBy(x => x.EventDateTime)
            .ThenBy(x => x.Unit)
            .ToList();
    }

    /// <summary>
    /// نام واحد را نرمال‌سازی می‌کند.
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
    /// نوع رویداد را نرمال‌سازی می‌کند.
    /// </summary>
    private static string NormalizeEventType(string? value)
    {
        return (value ?? string.Empty)
            .Trim()
            .ToUpperInvariant()
            .Replace(".", "");
    }

    /// <summary>
    /// ساعت رویداد را نرمال‌سازی می‌کند.
    /// اگر مقدار نامعتبر باشد، 00:00 برگردانده می‌شود.
    /// </summary>
    private static string NormalizeTime(string? value)
    {
        string text = (value ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(text))
            return "00:00";

        if (TimeSpan.TryParse(text, out TimeSpan ts))
            return ts.ToString(@"hh\:mm");

        return "00:00";
    }

    /// <summary>
    /// تاریخ شمسی عددی و ساعت را به DateTime میلادی تبدیل می‌کند.
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

        DateTime date = calendar.ToDateTime(
            year,
            month,
            day,
            timeSpan.Hours,
            timeSpan.Minutes,
            0,
            0);

        return date;
    }
}
