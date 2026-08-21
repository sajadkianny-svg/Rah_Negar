

using Microsoft.Data.Sqlite;
using Rah_Negar.Data;
using Rah_Negar.Models;
using Rah_Negar.Services.Reports;

namespace Rah_Negar.Services;

/// <summary>
/// سرویس ذخیره و لود tbl_data برای Ramsar Station
/// </summary>
public static class RamsarRecordPersistenceService
{
    /// <summary>
    /// لود داده‌های tbl_data برای یک تاریخ مشخص
    /// </summary>
    public static List<RamsarDailyDataRowModel> LoadDailyData(long dateRep)
    {
        using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();

        const string sql = @"
SELECT
    time_rep,
    in_p,
    out_p,
    u1_st,
    u1_rpm,
    u2_st,
    u2_rpm,
    u3_st,
    u3_rpm,
    u4_st,
    u4_rpm,
    rec,
    flow,
    in_t,
    out_t,
    amb_t,
    ratio
FROM tbl_data
WHERE date_rep = @date_rep
ORDER BY time_rep;";

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@date_rep", dateRep);

        using SqliteDataReader reader = cmd.ExecuteReader();

        List<RamsarDailyDataRowModel> list = new();

        while (reader.Read())
        {
            list.Add(new RamsarDailyDataRowModel
            {
                TimeRep = reader["time_rep"]?.ToString() ?? string.Empty,

                InP = Convert.ToDouble(reader["in_p"]),
                OutP = Convert.ToDouble(reader["out_p"]),

                U1St = reader["u1_st"]?.ToString() ?? string.Empty,
                U1Rpm = Convert.ToInt32(reader["u1_rpm"]),

                U2St = reader["u2_st"]?.ToString() ?? string.Empty,
                U2Rpm = Convert.ToInt32(reader["u2_rpm"]),

                U3St = reader["u3_st"]?.ToString() ?? string.Empty,
                U3Rpm = Convert.ToInt32(reader["u3_rpm"]),

                U4St = reader["u4_st"]?.ToString() ?? string.Empty,
                U4Rpm = Convert.ToInt32(reader["u4_rpm"]),

                Rec = Convert.ToDouble(reader["rec"]),
                Flow = Convert.ToDouble(reader["flow"]),
                InT = Convert.ToDouble(reader["in_t"]),
                OutT = Convert.ToDouble(reader["out_t"]),
                AmbT = Convert.ToDouble(reader["amb_t"]),
                Ratio = Convert.ToDouble(reader["ratio"])
            });
        }

        return list;
    }

    /// <summary>
    /// فقط داده‌های tbl_data را برای Ramsar Station
    /// داخل Transaction جاری درج می‌کند.
    /// </summary>
    public static void InsertDailyDataOnly(SqliteConnection conn, SqliteTransaction tx, RamsarDailyDataSaveModel model)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        MonthlyLockService.EnsureDateIsEditable(conn, tx, model.DateRep);

        DeleteExisting(conn, tx, model.DateRep);
        InsertRows(conn, tx, model);
    }

    /// <summary>
    /// حذف داده‌های قبلی همان تاریخ
    /// </summary>
    private static void DeleteExisting(SqliteConnection conn, SqliteTransaction tx, long dateRep)
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
    /// درج ردیف‌های جدید Ramsar در tbl_data
    /// </summary>
    private static void InsertRows(SqliteConnection conn, SqliteTransaction tx, RamsarDailyDataSaveModel model)
    {
        const string sql = @"
INSERT INTO tbl_data
(
    date_rep,
    time_rep,
    in_p,
    out_p,
    u1_st,
    u1_rpm,
    u2_st,
    u2_rpm,
    u3_st,
    u3_rpm,
    u4_st,
    u4_rpm,
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
    @u1_st,
    @u1_rpm,
    @u2_st,
    @u2_rpm,
    @u3_st,
    @u3_rpm,
    @u4_st,
    @u4_rpm,
    @rec,
    @flow,
    @in_t,
    @out_t,
    @amb_t,
    @ratio
);";

        foreach (RamsarDailyDataRowModel row in model.Rows)
        {
            var parameters = new List<SqliteParameter>
            {
                SqliteCommandHelper.Param("@date_rep", model.DateRep),
                SqliteCommandHelper.Param("@time_rep", row.TimeRep),

                SqliteCommandHelper.Param("@in_p", row.InP),
                SqliteCommandHelper.Param("@out_p", row.OutP),

                SqliteCommandHelper.Param("@u1_st", row.U1St),
                SqliteCommandHelper.Param("@u1_rpm", row.U1Rpm),

                SqliteCommandHelper.Param("@u2_st", row.U2St),
                SqliteCommandHelper.Param("@u2_rpm", row.U2Rpm),

                SqliteCommandHelper.Param("@u3_st", row.U3St),
                SqliteCommandHelper.Param("@u3_rpm", row.U3Rpm),

                SqliteCommandHelper.Param("@u4_st", row.U4St),
                SqliteCommandHelper.Param("@u4_rpm", row.U4Rpm),

                SqliteCommandHelper.Param("@rec", row.Rec),
                SqliteCommandHelper.Param("@flow", row.Flow),
                SqliteCommandHelper.Param("@in_t", row.InT),
                SqliteCommandHelper.Param("@out_t", row.OutT),
                SqliteCommandHelper.Param("@amb_t", row.AmbT),
                SqliteCommandHelper.Param("@ratio", row.Ratio)
            };

            SqliteCommandHelper.ExecuteNonQuery(conn, sql, parameters, tx);
        }
    }
}
