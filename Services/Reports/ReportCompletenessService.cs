using Microsoft.Data.Sqlite;
using Rah_Negar.Models.Reports;
using Rah_Negar.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Services.Reports;

/// <summary>
/// سرویس بررسی کامل بودن داده‌های روزانه.
/// این سرویس مشخص می‌کند هر روز از نظر رکوردهای tbl_data و tbl_unique کامل است یا ناقص.
/// </summary>
public static class ReportCompletenessService
{
    /// <summary>
    /// ساعات مورد انتظار برای کامل بودن یک روز.
    /// </summary>
    private static readonly string[] RequiredTimes =
    [
        "01", "03", "05", "07", "09", "11",
        "13", "15", "17", "19", "21", "23"
    ];
    
    /// <summary>
    /// وضعیت کامل بودن داده‌ها را برای یک بازه تاریخی بررسی می‌کند.
    /// حرکت بین روزها با PersianDateHelper انجام می‌شود تا تاریخ‌های نامعتبر ساخته نشوند.
    /// </summary>
    public static List<ReportDailyStatus> CheckRange(
        SqliteConnection conn,
        long dateFrom,
        long dateTo)
    {
        List<ReportDailyStatus> result = [];

        long dataStartDate = AppSettingsService.GetDataStartDate();
        long currentDate = dataStartDate > 0
            ? Math.Max(dateFrom, dataStartDate)
            : dateFrom;

        while (currentDate <= dateTo)
        {
            ReportDailyStatus status = CheckDay(conn, currentDate);
            result.Add(status);

            currentDate = PersianDateHelper.AddDays(currentDate, 1);
        }

        return result;
    }

    /// <summary>
    /// وضعیت کامل بودن داده‌ها را برای یک روز مشخص بررسی می‌کند.
    /// </summary>
    /// <param name="conn">اتصال باز SQLite.</param>
    /// <param name="dateRep">تاریخ مورد بررسی.</param>
    /// <returns>وضعیت کامل یا ناقص بودن همان روز.</returns>
    public static ReportDailyStatus CheckDay(SqliteConnection conn, long dateRep)
    {
        List<string> existingTimes = LoadExistingTimes(conn, dateRep);
        bool hasUniqueRow = HasUniqueRow(conn, dateRep);

        List<string> missingTimes = RequiredTimes
            .Where(t => !existingTimes.Contains(t))
            .ToList();

        bool isComplete =
            existingTimes.Count == RequiredTimes.Length &&
            missingTimes.Count == 0 &&
            hasUniqueRow;

        return new ReportDailyStatus
        {
            DateRep = dateRep,
            IsComplete = isComplete,
            HasNoData = existingTimes.Count == 0 && !hasUniqueRow,
            DataRowCount = existingTimes.Count,
            HasUniqueRow = hasUniqueRow,
            ExistingTimes = existingTimes,
            MissingTimes = missingTimes
        };
    }

    /// <summary>
    /// ساعات ثبت‌شده در tbl_data برای یک تاریخ مشخص را می‌خواند.
    /// </summary>
    private static List<string> LoadExistingTimes(SqliteConnection conn, long dateRep)
    {
        List<string> times = [];

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT time_rep
            FROM tbl_data
            WHERE date_rep = $dateRep
            ORDER BY time_rep;
            """;

        cmd.Parameters.AddWithValue("$dateRep", dateRep);

        using SqliteDataReader reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            string rawTime = reader.GetString(0);
            string normalizedTime = NormalizeTimeRep(rawTime);

            if (!string.IsNullOrWhiteSpace(normalizedTime))
                times.Add(normalizedTime);
        }

        return times;
    }

    /// <summary>
    /// بررسی می‌کند آیا برای تاریخ مشخص، رکورد tbl_unique وجود دارد یا خیر.
    /// </summary>
    private static bool HasUniqueRow(SqliteConnection conn, long dateRep)
    {
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT COUNT(*)
            FROM tbl_unique
            WHERE date_rep = $dateRep;
            """;

        cmd.Parameters.AddWithValue("$dateRep", dateRep);

        long count = (long)cmd.ExecuteScalar()!;

        return count > 0;
    }

    /// <summary>
    /// مقدار time_rep ذخیره‌شده در دیتابیس را به فرمت دو رقمی ساعت تبدیل می‌کند.
    /// مثال:
    /// 1      => 01
    /// 01     => 01
    /// 1:00   => 01
    /// 01:00  => 01
    /// </summary>
    private static string NormalizeTimeRep(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string text = value.Trim();

        if (text.Contains(':'))
            text = text.Split(':')[0];

        if (!int.TryParse(text, out int hour))
            return string.Empty;

        if (hour < 0 || hour > 23)
            return string.Empty;

        return hour.ToString("00");
    }

}
