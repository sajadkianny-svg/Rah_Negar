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
/// سرویس ذخیره و حذف داده‌های مشترک بین همه ایستگاه‌ها
/// برای جداول tbl_unique و tbl_events
/// </summary>
public static class CommonRecordPersistenceService
{
    /// <summary>
    /// رکورد قبلی tbl_unique مربوط به تاریخ را حذف می‌کند.
    /// </summary>
    public static void DeleteExistingUnique(SqliteConnection conn, SqliteTransaction tx, long dateRep)
    {
        const string sql = @"
DELETE FROM tbl_unique
WHERE date_rep = @date_rep;";

        var parameters = new List<SqliteParameter>
        {
            SqliteCommandHelper.Param("@date_rep", dateRep)
        };

        SqliteCommandHelper.ExecuteNonQuery(conn, sql, parameters, tx);
    }

    /// <summary>
    /// داده جدید tbl_unique را درج می‌کند.
    /// </summary>
    public static void InsertUnique(SqliteConnection conn, SqliteTransaction tx, DailyUniqueSaveModel model)
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

    /// <summary>
    /// رویدادهای قبلی tbl_events مربوط به تاریخ را حذف می‌کند.
    /// </summary>
    public static void DeleteExistingEvents(SqliteConnection conn, SqliteTransaction tx, long dateRep)
    {
        const string sql = @"
DELETE FROM tbl_events
WHERE date_rep = @date_rep;";

        var parameters = new List<SqliteParameter>
        {
            SqliteCommandHelper.Param("@date_rep", dateRep)
        };

        SqliteCommandHelper.ExecuteNonQuery(conn, sql, parameters, tx);
    }

    /// <summary>
    /// رویدادهای جدید tbl_events را درج می‌کند.
    /// </summary>
    public static void InsertEvents(SqliteConnection conn, SqliteTransaction tx, List<DailyEventRowModel> eventsList)
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
}

