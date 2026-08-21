using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;




using Microsoft.Data.Sqlite;
using Rah_Negar.Data;
using Rah_Negar.Models;

namespace Rah_Negar.Services;

/// <summary>
/// سرویس مربوط به ذخیره و مدیریت داده‌های جدول tbl_data
/// </summary>
public static class DailyDataService
{
    /// <summary>
    /// داده‌های روزانه را در tbl_data ذخیره می‌کند
    /// اگر برای این تاریخ رکوردی وجود داشته باشد، ابتدا حذف و سپس دوباره ثبت می‌شود
    /// </summary>
    public static void SaveDailyData(DailyDataSaveModel model)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        if (model.DateRep <= 0)
            throw new ArgumentException("DateRep is invalid.");

        if (model.Rows == null || model.Rows.Count == 0)
            throw new ArgumentException("No daily rows were provided.");

        using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();
        using SqliteTransaction tx = conn.BeginTransaction();

        try
        {
            DeleteExistingRows(conn, tx, model.DateRep);

            foreach (DailyDataRowModel row in model.Rows)
            {
                InsertDailyRow(conn, tx, model.DateRep, row);
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>
    /// حذف رکوردهای قبلی همان تاریخ
    /// </summary>
    private static void DeleteExistingRows(SqliteConnection conn, SqliteTransaction tx, long dateRep)
    {
        const string sql = @"
DELETE FROM tbl_data
WHERE date_rep = @date_rep;";

        var parameters = new List<SqliteParameter>
        {
            SqliteCommandHelper.Param("@date_rep", dateRep)
        };

        SqliteCommandHelper.ExecuteNonQuery(conn, sql, parameters, tx);
    }

    /// <summary>
    /// درج یک ردیف در tbl_data
    /// </summary>
    private static void InsertDailyRow(SqliteConnection conn, SqliteTransaction tx, long dateRep, DailyDataRowModel row)
    {
        const string sql = @"
INSERT INTO tbl_data
(
    date_rep,
    time_rep,
    in_p,
    out_p,
    line_f_p,
    line40_p,
    line30_p,
    u1_st,
    u1_rpm,
    u2_st,
    u2_rpm,
    u3_st,
    u3_rpm,
    rec,
    flow,
    in_t,
    out_t,
    amb_t,
    ratio
)
VALUES
(
    @date_rep,
    @time_rep,
    @in_p,
    @out_p,
    @line_f_p,
    @line40_p,
    @line30_p,
    @u1_st,
    @u1_rpm,
    @u2_st,
    @u2_rpm,
    @u3_st,
    @u3_rpm,
    @rec,
    @flow,
    @in_t,
    @out_t,
    @amb_t,
    @ratio
);";

        var parameters = new List<SqliteParameter>
        {
            SqliteCommandHelper.Param("@date_rep", dateRep),
            SqliteCommandHelper.Param("@time_rep", row.TimeRep),

            SqliteCommandHelper.Param("@in_p", row.InP),
            SqliteCommandHelper.Param("@out_p", row.OutP),
            SqliteCommandHelper.Param("@line_f_p", row.LineFP),
            SqliteCommandHelper.Param("@line40_p", row.Line40P),
            SqliteCommandHelper.Param("@line30_p", row.Line30P),

            SqliteCommandHelper.Param("@u1_st", row.U1St),
            SqliteCommandHelper.Param("@u1_rpm", row.U1Rpm),

            SqliteCommandHelper.Param("@u2_st", row.U2St),
            SqliteCommandHelper.Param("@u2_rpm", row.U2Rpm),

            SqliteCommandHelper.Param("@u3_st", row.U3St),
            SqliteCommandHelper.Param("@u3_rpm", row.U3Rpm),

            SqliteCommandHelper.Param("@rec", row.Rec),
            SqliteCommandHelper.Param("@flow", row.Flow),
            SqliteCommandHelper.Param("@in_t", row.InT),
            SqliteCommandHelper.Param("@out_t", row.OutT),
            SqliteCommandHelper.Param("@amb_t", row.AmbT),
            SqliteCommandHelper.Param("@ratio", row.Ratio)
        };

        SqliteCommandHelper.ExecuteNonQuery(conn, sql, parameters, tx);
    }

    public static bool ExistsForDate(long dateRep)
    {
        using var conn = SqliteDatabaseHelper.CreateConnection();

        const string sql = @"
SELECT COUNT(*)
FROM tbl_data
WHERE date_rep = @date_rep;";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@date_rep", dateRep);

        int count = Convert.ToInt32(cmd.ExecuteScalar());

        return count > 0;
    }
}
