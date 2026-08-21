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
/// سرویس ذخیره یکپارچه رکوردهای Rasht Station
/// تمام جداول مرتبط را در یک Transaction مشترک ذخیره می‌کند
/// </summary>
public static class RashtRecordSaveService
{
    /// <summary>
    /// ذخیره همزمان tbl_data و tbl_unique و tbl_events
    /// در صورت وجود رکورد قبلی برای همان تاریخ، ابتدا حذف و سپس درج انجام می‌شود
    /// </summary>
    public static void SaveAll(
        DailyDataSaveModel dailyDataModel,
        DailyUniqueSaveModel uniqueModel,
        List<DailyEventRowModel> eventsModel)
    {
        if (dailyDataModel == null)
            throw new ArgumentNullException(nameof(dailyDataModel));

        if (uniqueModel == null)
            throw new ArgumentNullException(nameof(uniqueModel));

        if (eventsModel == null)
            throw new ArgumentNullException(nameof(eventsModel));

        using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();
        using SqliteTransaction tx = conn.BeginTransaction();

        try
        {
            long repDate = dailyDataModel.DateRep;

            DeleteExistingDailyData(conn, tx, repDate);
            DeleteExistingUnique(conn, tx, repDate);
            DeleteExistingEvents(conn, tx, repDate);

            InsertDailyData(conn, tx, dailyDataModel);
            InsertUnique(conn, tx, uniqueModel);
            InsertEvents(conn, tx, eventsModel);

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static void DeleteExistingDailyData(SqliteConnection conn, SqliteTransaction tx, long repDate)
    {
        const string sql = @"
DELETE FROM tbl_data
WHERE date_rep = @date_rep;";

        var parameters = new List<SqliteParameter>
        {
            SqliteCommandHelper.Param("@date_rep", repDate)
        };

        SqliteCommandHelper.ExecuteNonQuery(conn, sql, parameters, tx);
    }

    private static void DeleteExistingUnique(SqliteConnection conn, SqliteTransaction tx, long repDate)
    {
        const string sql = @"
DELETE FROM tbl_unique
WHERE date_rep = @date_rep;";

        var parameters = new List<SqliteParameter>
        {
            SqliteCommandHelper.Param("@date_rep", repDate)
        };

        SqliteCommandHelper.ExecuteNonQuery(conn, sql, parameters, tx);
    }

    private static void DeleteExistingEvents(SqliteConnection conn, SqliteTransaction tx, long repDate)
    {
        const string sql = @"
DELETE FROM tbl_events
WHERE date_rep = @date_rep;";

        var parameters = new List<SqliteParameter>
        {
            SqliteCommandHelper.Param("@date_rep", repDate)
        };

        SqliteCommandHelper.ExecuteNonQuery(conn, sql, parameters, tx);
    }

    private static void InsertDailyData(SqliteConnection conn, SqliteTransaction tx, DailyDataSaveModel model)
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

        foreach (DailyDataRowModel row in model.Rows)
        {
            var parameters = new List<SqliteParameter>
            {
                SqliteCommandHelper.Param("@date_rep", model.DateRep),
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
    }

    private static void InsertUnique(SqliteConnection conn, SqliteTransaction tx, DailyUniqueSaveModel model)
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
VALUES
(
    @date_rep,
    @ir_f,
    @turbine_fuel,
    @turbine_flow,
    @non_turbine_flow,
    @vent
);";

        var parameters = new List<SqliteParameter>
        {
            SqliteCommandHelper.Param("@date_rep", model.DateRep),
            SqliteCommandHelper.Param("@ir_f", model.IrFuel),
            SqliteCommandHelper.Param("@turbine_fuel", model.TurbineFuel),
            SqliteCommandHelper.Param("@turbine_flow", model.TurbineFlow),
            SqliteCommandHelper.Param("@non_turbine_flow", model.NonTurbineFlow),
            SqliteCommandHelper.Param("@vent", model.Vent)
        };

        SqliteCommandHelper.ExecuteNonQuery(conn, sql, parameters, tx);
    }

    private static void InsertEvents(SqliteConnection conn, SqliteTransaction tx, List<DailyEventRowModel> eventsList)
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
VALUES
(
    @date_rep,
    @unit,
    @event_type,
    @event_time,
    @remark
);";

        foreach (DailyEventRowModel item in eventsList)
        {
            var parameters = new List<SqliteParameter>
            {
                SqliteCommandHelper.Param("@date_rep", item.DateRep),
                SqliteCommandHelper.Param("@unit", item.Unit),
                SqliteCommandHelper.Param("@event_type", item.EventType),
                SqliteCommandHelper.Param("@event_time", item.EventTime),
                SqliteCommandHelper.Param("@remark", item.Remark)
            };

            SqliteCommandHelper.ExecuteNonQuery(conn, sql, parameters, tx);
        }
    }

    /// <summary>
    /// داده‌های جدول tbl_data را برای یک تاریخ مشخص برمی‌گرداند
    /// </summary>
    public static List<DailyDataRowModel> LoadDailyData(long dateRep)
    {
        using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();

        const string sql = @"
SELECT
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
FROM tbl_data
WHERE date_rep = @date_rep
ORDER BY time_rep;";

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@date_rep", dateRep);

        using SqliteDataReader reader = cmd.ExecuteReader();

        List<DailyDataRowModel> list = new List<DailyDataRowModel>();

        while (reader.Read())
        {
            list.Add(new DailyDataRowModel
            {
                TimeRep = reader["time_rep"]?.ToString() ?? "",

                InP = Convert.ToDouble(reader["in_p"]),
                OutP = Convert.ToDouble(reader["out_p"]),
                LineFP = Convert.ToDouble(reader["line_f_p"]),
                Line40P = Convert.ToDouble(reader["line40_p"]),
                Line30P = Convert.ToDouble(reader["line30_p"]),

                U1St = reader["u1_st"]?.ToString() ?? "",
                U1Rpm = Convert.ToInt32(reader["u1_rpm"]),

                U2St = reader["u2_st"]?.ToString() ?? "",
                U2Rpm = Convert.ToInt32(reader["u2_rpm"]),

                U3St = reader["u3_st"]?.ToString() ?? "",
                U3Rpm = Convert.ToInt32(reader["u3_rpm"]),

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
    /// فقط داده‌های tbl_data را برای Rasht Station
    /// داخل Transaction جاری درج می‌کند.
    /// </summary>
    public static void InsertDailyDataOnly(SqliteConnection conn, SqliteTransaction tx, DailyDataSaveModel model)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        DeleteExistingDailyData(conn, tx, model.DateRep);
        InsertDailyData(conn, tx, model);
    }

}