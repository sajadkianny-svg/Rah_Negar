using Microsoft.Data.Sqlite;
using Rah_Negar.Data;

namespace Rah_Negar.Services.Reports;

/// <summary>
/// مدیریت قفل ماهانه گزارش‌ها
/// بعد از نهایی‌سازی ماه، داده‌های همان ماه دیگر قابل ویرایش نیستند
/// </summary>
public static class MonthlyLockService
{
    /// <summary>
    /// بررسی می‌کند آیا ماه مورد نظر قفل شده است یا نه
    /// </summary>
    public static bool IsMonthLocked(int year, int month)
    {
        using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();
        using SqliteCommand cmd = conn.CreateCommand();

        cmd.CommandText = @"
SELECT is_locked
FROM tbl_monthly_lock
WHERE year_rep = @year_rep
  AND month_rep = @month_rep
LIMIT 1;";

        cmd.Parameters.AddWithValue("@year_rep", year);
        cmd.Parameters.AddWithValue("@month_rep", month);

        object? result = cmd.ExecuteScalar();

        if (result == null || result == DBNull.Value)
            return false;

        return Convert.ToInt32(result) == 1;
    }

    /// <summary>
    /// ماه مورد نظر را قفل می‌کند
    /// اگر رکورد قبلاً وجود داشته باشد، همان رکورد به‌روزرسانی می‌شود
    /// </summary>
    public static void LockMonth(
        SqliteConnection conn,
        SqliteTransaction tx,
        int year,
        int month,
        string lockedBy)
    {
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;

        cmd.CommandText = @"
INSERT INTO tbl_monthly_lock
(
    year_rep,
    month_rep,
    is_locked,
    locked_at,
    locked_by
)
VALUES
(
    @year_rep,
    @month_rep,
    1,
    @locked_at,
    @locked_by
)
ON CONFLICT(year_rep, month_rep)
DO UPDATE SET
    is_locked = 1,
    locked_at = excluded.locked_at,
    locked_by = excluded.locked_by;";

        cmd.Parameters.AddWithValue("@year_rep", year);
        cmd.Parameters.AddWithValue("@month_rep", month);
        cmd.Parameters.AddWithValue("@locked_at", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@locked_by", lockedBy);

        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// پیام خطای ماه قفل‌شده را برای نمایش به کاربر می‌سازد
    /// </summary>
    public static string BuildLockedMonthMessage(int year, int month)
    {
        return
            "داده‌های این ماه نهایی شده و قابل ویرایش نیست";

    }


    /// <summary>
    /// بررسی می‌کند آیا همه ماه‌های یک بازه نهایی و قفل شده‌اند یا نه.
    /// برای گزارش‌های نیم‌سال و سالانه استفاده می‌شود.
    /// </summary>
    public static bool AreAllMonthsLocked(int year, List<int> months)
    {
        if (months == null || months.Count == 0)
            return false;

        foreach (int month in months)
        {
            if (!IsMonthLocked(year, month))
                return false;
        }

        return true;
    }




}