using System.Globalization;
using Microsoft.Data.Sqlite;
using Rah_Negar.Data;

namespace Rah_Negar.Services.Reports;

/// <summary>
/// بررسی وضعیت نهایی‌سازی ماه‌ها.
/// این سرویس تشخیص می‌دهد آیا ماهی وجود دارد که
/// اطلاعات آن کامل شده ولی هنوز گزارش نهایی برای آن ایجاد نشده است.
/// </summary>
public static class MonthlyFinalizeStatusService
{
    /// <summary>
    /// اگر ماهی آماده نهایی‌سازی باشد، پیام مناسب برمی‌گرداند.
    /// در غیر این صورت null برمی‌گرداند.
    /// </summary>
    public static string? GetPendingFinalReportMessage()
    {
        using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();

        // گرفتن آخرین تاریخ ثبت‌شده
        long? lastDate = GetLastDate(conn);
        if (!lastDate.HasValue)
            return null;

        int year = (int)(lastDate.Value / 10000);
        int month = (int)((lastDate.Value / 100) % 100);

        // بررسی کامل بودن ماه
        if (!IsMonthComplete(conn, year, month))
            return null;

        // اگر ماه قبلاً نهایی شده باشد
        if (MonthlyLockService.IsMonthLocked(year, month))
            return null;

        return $"گزارش آماده تولید {year}/{month:00} ";
    }

    /// <summary>
    /// آخرین تاریخ ثبت‌شده در tbl_unique را برمی‌گرداند.
    /// </summary>
    private static long? GetLastDate(SqliteConnection conn)
    {
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT MAX(date_rep) FROM tbl_unique;";

        object? result = cmd.ExecuteScalar();

        if (result == null || result == DBNull.Value)
            return null;

        return Convert.ToInt64(result);
    }

    /// <summary>
    /// بررسی می‌کند آیا همه روزهای یک ماه کامل ثبت شده‌اند یا نه.
    /// </summary>
    private static bool IsMonthComplete(SqliteConnection conn, int year, int month)
    {
        int daysInMonth = new PersianCalendar().GetDaysInMonth(year, month);

        long fromDate = year * 10000L + month * 100L + 1;
        long toDate = year * 10000L + month * 100L + daysInMonth;

        // تعداد روزهای ثبت‌شده در tbl_unique
        const string sql = @"
SELECT COUNT(DISTINCT date_rep)
FROM tbl_unique
WHERE date_rep BETWEEN @fromDate AND @toDate;";

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@fromDate", fromDate);
        cmd.Parameters.AddWithValue("@toDate", toDate);

        int count = Convert.ToInt32(cmd.ExecuteScalar());

        return count == daysInMonth;
    }
}
