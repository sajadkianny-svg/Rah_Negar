using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;





namespace Rah_Negar.Core;

/// <summary>
/// ساختار جداول مشترک بین همه ایستگاه‌ها.
/// شامل tbl_unique و tbl_events و ایندکس‌های مشترک آن‌ها.
/// </summary>
public static class CommonDataSchema
{
    public static string GetCreateTblUniqueSql()
    {
        return @"
CREATE TABLE IF NOT EXISTS tbl_unique (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    date_rep INTEGER NOT NULL,
    ir_f REAL,
    turbine_fuel REAL,
    turbine_flow REAL,
    non_turbine_flow REAL,
    vent REAL
);";
    }

    public static string GetCreateTblEventsSql()
    {
        return @"
CREATE TABLE IF NOT EXISTS tbl_events (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    date_rep INTEGER NOT NULL,
    unit TEXT NOT NULL,
    event_type TEXT NOT NULL,
    event_time TEXT NOT NULL,
    remark TEXT
);";
    }

    public static List<string> GetCommonIndexSqlList()
    {
        return new List<string>
    {
        "CREATE UNIQUE INDEX IF NOT EXISTS idx_tbl_unique_date ON tbl_unique(date_rep);",
        "CREATE INDEX IF NOT EXISTS idx_tbl_events_date ON tbl_events(date_rep);",
        "CREATE INDEX IF NOT EXISTS idx_tbl_events_date_time_unit ON tbl_events(date_rep, event_time, unit);",
        "CREATE INDEX IF NOT EXISTS idx_tbl_events_unit ON tbl_events(unit);",
        "CREATE INDEX IF NOT EXISTS idx_tbl_events_type ON tbl_events(event_type);",
        "CREATE INDEX IF NOT EXISTS idx_tbl_events_unit_type_date ON tbl_events(unit, event_type, date_rep);",
        "CREATE INDEX IF NOT EXISTS idx_tbl_events_type_date ON tbl_events(event_type, date_rep);",

    };
    }
}