using Microsoft.Data.Sqlite;
using Rah_Negar.Data;
using Rah_Negar.Utils;

namespace Rah_Negar.Services;

/// <summary>
/// سرویس ساخت داده تستی.
/// این سرویس فقط برای محیط تست استفاده می‌شود و نباید در نسخه عملیاتی فعال باشد.
/// </summary>
public static class TestDataSeederService
{
    /// <summary>
    /// داده‌های یک روز نمونه را برای تمام روزهای یک سال شمسی کپی می‌کند.
    /// tbl_data و tbl_unique و tbl_events از تاریخ نمونه خوانده شده
    /// و با date_rep جدید برای کل سال درج می‌شوند.
    /// </summary>

    public static string CopyTemplateDayToFullYear(long templateDateRep, int targetYear)
    {
        using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();

        int templateDataCount = CountRows(conn, "tbl_data", templateDateRep);
        int templateUniqueCount = CountRows(conn, "tbl_unique", templateDateRep);
        int templateEventsCount = CountRows(conn, "tbl_events", templateDateRep);

        if (templateDataCount == 0)
            return $"تاریخ نمونه {templateDateRep} در tbl_data داده ندارد.";

        if (templateUniqueCount == 0)
            return $"تاریخ نمونه {templateDateRep} در tbl_unique داده ندارد.";

        using SqliteTransaction tx = conn.BeginTransaction();

        int insertedData = 0;
        int insertedUnique = 0;
        int insertedEvents = 0;

        try
        {
            List<long> targetDates = BuildYearDates(targetYear);

            foreach (long targetDate in targetDates)
            {
                if (targetDate == templateDateRep)
                    continue;

                DeleteExistingDay(conn, tx, targetDate);

                insertedData += CopyTblData(conn, tx, templateDateRep, targetDate);
                insertedUnique += CopyTblUnique(conn, tx, templateDateRep, targetDate);
                insertedEvents += CopyTblEvents(conn, tx, templateDateRep, targetDate);
            }

            tx.Commit();

            return
                $"عملیات انجام شد." + Environment.NewLine +
                $"Template tbl_data: {templateDataCount}" + Environment.NewLine +
                $"Template tbl_unique: {templateUniqueCount}" + Environment.NewLine +
                $"Template tbl_events: {templateEventsCount}" + Environment.NewLine +
                $"Inserted tbl_data: {insertedData}" + Environment.NewLine +
                $"Inserted tbl_unique: {insertedUnique}" + Environment.NewLine +
                $"Inserted tbl_events: {insertedEvents}";
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>
    /// تعداد رکوردهای یک جدول را برای یک تاریخ مشخص برمی‌گرداند.
    /// </summary>
    private static int CountRows(SqliteConnection conn, string tableName, long dateRep)
    {
        using SqliteCommand cmd = conn.CreateCommand();

        cmd.CommandText = $@"
SELECT COUNT(*)
FROM {tableName}
WHERE date_rep = @date_rep;";

        cmd.Parameters.AddWithValue("@date_rep", dateRep);

        return Convert.ToInt32(cmd.ExecuteScalar());
    }


    /// <summary>
    /// لیست تمام تاریخ‌های یک سال شمسی را تولید می‌کند.
    /// </summary>
    private static List<long> BuildYearDates(int year)
    {
        List<long> dates = [];

        long current = year * 10000L + 101;

        while ((int)(current / 10000) == year)
        {
            dates.Add(current);
            current = PersianDateHelper.AddDays(current, 1);
        }

        return dates;
    }

    /// <summary>
    /// داده‌های قبلی یک روز را پاک می‌کند تا درج تکراری ایجاد نشود.
    /// </summary>
    private static void DeleteExistingDay(
        SqliteConnection conn,
        SqliteTransaction tx,
        long dateRep)
    {
        var parameters = new List<SqliteParameter>
    {
        SqliteCommandHelper.Param("@date_rep", dateRep)
    };

        SqliteCommandHelper.ExecuteNonQuery(
            conn,
            "DELETE FROM tbl_data WHERE date_rep = @date_rep;",
            parameters,
            tx);

        SqliteCommandHelper.ExecuteNonQuery(
            conn,
            "DELETE FROM tbl_unique WHERE date_rep = @date_rep;",
            parameters,
            tx);

        SqliteCommandHelper.ExecuteNonQuery(
            conn,
            "DELETE FROM tbl_events WHERE date_rep = @date_rep;",
            parameters,
            tx);
    }


    /// <summary>
    /// رکورد روزانه tbl_unique را از روز نمونه به روز مقصد کپی می‌کند.
    /// </summary>
    private static int CopyTblUnique(
        SqliteConnection conn,
        SqliteTransaction tx,
        long templateDateRep,
        long targetDateRep)
    {
        const string sql = @"
INSERT INTO tbl_unique
(
    date_rep,
    ir_f,
    turbine_fuel,
    turbine_flow,
    non_turbine_flow,
    vent
)
SELECT
    @target_date,
    ir_f,
    turbine_fuel,
    turbine_flow,
    non_turbine_flow,
    vent
FROM tbl_unique
WHERE date_rep = @template_date;";

        var parameters = new List<SqliteParameter>
    {
        SqliteCommandHelper.Param("@template_date", templateDateRep),
        SqliteCommandHelper.Param("@target_date", targetDateRep)
    };
        return
        SqliteCommandHelper.ExecuteNonQuery(conn, sql, parameters, tx);
    }

    /// <summary>
    /// رویدادهای tbl_events را از روز نمونه به روز مقصد کپی می‌کند.
    /// </summary>
    private static int CopyTblEvents(
        SqliteConnection conn,
        SqliteTransaction tx,
        long templateDateRep,
        long targetDateRep)
    {
        const string sql = @"
INSERT INTO tbl_events
(
    date_rep,
    unit,
    event_type,
    event_time,
    remark
)
SELECT
    @target_date,
    unit,
    event_type,
    event_time,
    remark
FROM tbl_events
WHERE date_rep = @template_date;";

        var parameters = new List<SqliteParameter>
    {
        SqliteCommandHelper.Param("@template_date", templateDateRep),
        SqliteCommandHelper.Param("@target_date", targetDateRep)
    };
        return
        SqliteCommandHelper.ExecuteNonQuery(conn, sql, parameters, tx);
    }

    /// <summary>
    /// رکوردهای tbl_data را به صورت داینامیک از روز نمونه به روز مقصد کپی می‌کند.
    /// این متد مستقل از نوع ایستگاه (Ramsar/Rasht) است.
    /// </summary>
private static int CopyTblData(
    SqliteConnection conn,
    SqliteTransaction tx,
    long templateDateRep,
    long targetDateRep)
    {
        // گرفتن لیست ستون‌ها
        List<string> columns = [];

        using (SqliteCommand cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(tbl_data);";

            using SqliteDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                string colName = reader["name"].ToString()!;

                if (colName.Equals("id", StringComparison.OrdinalIgnoreCase))
                    continue;

                columns.Add(colName);
            }
        }

        // ساخت SELECT داینامیک
        List<string> selectParts = [];

        foreach (string col in columns)
        {
            if (col == "date_rep")
                selectParts.Add("@target_date");
            else
                selectParts.Add(col);
        }

        string columnList = string.Join(", ", columns);
        string selectList = string.Join(", ", selectParts);

        string sql = $@"
INSERT INTO tbl_data
({columnList})
SELECT
{selectList}
FROM tbl_data
WHERE date_rep = @template_date;";

        var parameters = new List<SqliteParameter>
    {
        SqliteCommandHelper.Param("@template_date", templateDateRep),
        SqliteCommandHelper.Param("@target_date", targetDateRep)
    };

        return
        SqliteCommandHelper.ExecuteNonQuery(conn, sql, parameters, tx);
    }


    /// <summary>
    /// داده‌های یک روز نمونه را برای تمام روزهای یک ماه شمسی کپی می‌کند.
    /// tbl_data و tbl_unique و tbl_events از تاریخ نمونه خوانده شده
    /// و با date_rep جدید برای کل ماه درج می‌شوند.
    /// </summary>
    public static string CopyTemplateDayToFullMonth(
        long templateDateRep,
        int targetYear,
        int targetMonth)
    {
        if (targetMonth < 1 || targetMonth > 12)
            return "شماره ماه نامعتبر است.";

        using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();

        int templateDataCount = CountRows(conn, "tbl_data", templateDateRep);
        int templateUniqueCount = CountRows(conn, "tbl_unique", templateDateRep);
        int templateEventsCount = CountRows(conn, "tbl_events", templateDateRep);

        if (templateDataCount == 0)
            return $"تاریخ نمونه {templateDateRep} در tbl_data داده ندارد.";

        if (templateUniqueCount == 0)
            return $"تاریخ نمونه {templateDateRep} در tbl_unique داده ندارد.";

        using SqliteTransaction tx = conn.BeginTransaction();

        int insertedData = 0;
        int insertedUnique = 0;
        int insertedEvents = 0;

        try
        {
            List<long> targetDates = BuildMonthDates(targetYear, targetMonth);

            foreach (long targetDate in targetDates)
            {
                if (targetDate == templateDateRep)
                    continue;

                DeleteExistingDay(conn, tx, targetDate);

                insertedData += CopyTblData(conn, tx, templateDateRep, targetDate);
                insertedUnique += CopyTblUnique(conn, tx, templateDateRep, targetDate);
                insertedEvents += CopyTblEvents(conn, tx, templateDateRep, targetDate);
            }

            tx.Commit();

            return
                $"عملیات ساخت داده تستی ماهانه انجام شد." + Environment.NewLine +
                $"Target: {targetYear}/{targetMonth:00}" + Environment.NewLine +
                $"Template tbl_data: {templateDataCount}" + Environment.NewLine +
                $"Template tbl_unique: {templateUniqueCount}" + Environment.NewLine +
                $"Template tbl_events: {templateEventsCount}" + Environment.NewLine +
                $"Inserted tbl_data: {insertedData}" + Environment.NewLine +
                $"Inserted tbl_unique: {insertedUnique}" + Environment.NewLine +
                $"Inserted tbl_events: {insertedEvents}";
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }




    /// <summary>
    /// لیست تمام تاریخ‌های یک ماه شمسی را تولید می‌کند.
    /// </summary>
    private static List<long> BuildMonthDates(int year, int month)
    {
        List<long> dates = [];

        long current = year * 10000L + month * 100L + 1;

        while (true)
        {
            int currentYear = (int)(current / 10000);
            int currentMonth = (int)((current / 100) % 100);

            if (currentYear != year || currentMonth != month)
                break;

            dates.Add(current);

            current = PersianDateHelper.AddDays(current, 1);
        }

        return dates;
    }


}
