using Microsoft.Data.Sqlite;
using Rah_Negar.Data;
using Rah_Negar.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Services
{
    public static class CommonRecordQueryService
    {

        /// <summary>
        /// بررسی می‌کند آیا برای تاریخ موردنظر قبلاً اطلاعات ثبت شده یا نه
        /// مبنا: tbl_unique
        /// </summary>
        public static bool ExistsForDate(long DateRep)
        {
            using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();

            const string sql = @"
SELECT COUNT(*)
FROM tbl_unique
WHERE date_rep = @date_rep;";

            using SqliteCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@date_rep", DateRep);

            int count = Convert.ToInt32(cmd.ExecuteScalar());
            return count > 0;
        }

        /// <summary>
        /// داده خلاصه tbl_unique را برای یک تاریخ مشخص برمی‌گرداند
        /// </summary>
        public static DailyUniqueLoadModel? LoadDailyUnique(long dateRep)
        {
            using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();

            const string sql = @"
SELECT
    date_rep,
    ir_f,
    turbine_fuel,
    turbine_flow,
    non_turbine_flow,
    vent
FROM tbl_unique
WHERE date_rep = @date_rep
LIMIT 1;";

            using SqliteCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@date_rep", dateRep);

            using SqliteDataReader reader = cmd.ExecuteReader();

            if (!reader.Read())
                return null;

            return new DailyUniqueLoadModel
            {
                DateRep = Convert.ToInt64(reader["date_rep"]),
                IrFuel = Convert.ToDouble(reader["ir_f"]),
                TurbineFuel = Convert.ToDouble(reader["turbine_fuel"]),
                TurbineFlow = Convert.ToDouble(reader["turbine_flow"]),
                NonTurbineFlow = Convert.ToDouble(reader["non_turbine_flow"]),
                Vent = Convert.ToDouble(reader["vent"])
            };
        }

        /// <summary>
        /// رویدادهای tbl_events را برای یک تاریخ مشخص برمی‌گرداند
        /// </summary>
        public static List<DailyEventRowModel> LoadDailyEvents(long dateRep)
        {
            using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();

            const string sql = @"
SELECT
    date_rep,
    unit,
    event_type,
    event_time,
    remark
FROM tbl_events
WHERE date_rep = @date_rep
ORDER BY event_time;";

            using SqliteCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@date_rep", dateRep);

            using SqliteDataReader reader = cmd.ExecuteReader();

            List<DailyEventRowModel> list = new List<DailyEventRowModel>();

            while (reader.Read())
            {
                list.Add(new DailyEventRowModel
                {
                    DateRep = Convert.ToInt64(reader["date_rep"]),
                    Unit = reader["unit"]?.ToString() ?? "",
                    EventType = reader["event_type"]?.ToString() ?? "",
                    EventTime = reader["event_time"]?.ToString() ?? "",
                    Remark = reader["remark"]?.ToString() ?? ""
                });
            }

            return list;
        }


        /// <summary>
        /// آخرین تاریخ ثبت‌شده در جدول داده‌های روزانه را برمی‌گرداند
        /// </summary>
        public static long? GetLastSavedDate()
        {
            using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();
            using SqliteCommand cmd = conn.CreateCommand();

            cmd.CommandText = @"
SELECT MAX(date_rep)
FROM tbl_data;";

            object? result = cmd.ExecuteScalar();

            if (result == null || result == DBNull.Value)
                return null;

            return Convert.ToInt64(result);
        }

        /// <summary>
        /// بررسی می‌کند آیا تاکنون داده روزانه‌ای در tbl_data ثبت شده است یا نه
        /// </summary>
        public static bool HasAnyDailyRecord()
        {
            using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();
            using SqliteCommand cmd = conn.CreateCommand();

            cmd.CommandText = @"
SELECT COUNT(*)
FROM tbl_data;";

            object? result = cmd.ExecuteScalar();

            return result != null &&
                   result != DBNull.Value &&
                   Convert.ToInt64(result) > 0;
        }

    }
}
