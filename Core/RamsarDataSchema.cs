

namespace Rah_Negar.Core;

/// <summary>
/// ساختار اختصاصی tbl_data برای Ramsar Station
/// </summary>
public sealed class RamsarDataSchema : IStationDataSchema
{
    /// <summary>
    /// نام ایستگاه مربوط به این ساختار
    /// </summary>
    public string StationName => "Ramsar Station";

    /// <summary>
    /// SQL ساخت جدول tbl_data برای Ramsar Station
    /// </summary>
    public string GetCreateTableSql()
    {
        return @"
CREATE TABLE IF NOT EXISTS tbl_data (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    date_rep INTEGER NOT NULL,
    time_rep TEXT NOT NULL,
    in_p REAL NOT NULL,
    out_p REAL NOT NULL,
    u1_st TEXT NOT NULL,
    u1_rpm INTEGER NOT NULL,
    u2_st TEXT NOT NULL,
    u2_rpm INTEGER NOT NULL,
    u3_st TEXT NOT NULL,
    u3_rpm INTEGER NOT NULL,
    u4_st TEXT NOT NULL,
    u4_rpm INTEGER NOT NULL,
    rec REAL NOT NULL,
    flow REAL NOT NULL,
    in_t REAL NOT NULL,
    out_t REAL NOT NULL,
    amb_t REAL NOT NULL,
    ratio REAL NOT NULL
);";
    }

    /// <summary>
    /// SQLهای ایندکس tbl_data برای Ramsar Station
    /// </summary>
    public List<string> GetIndexSqlList()
    {
        return new List<string>
        {
            "CREATE INDEX IF NOT EXISTS idx_tbl_data_date_time ON tbl_data(date_rep, time_rep);"
        };
    }
}
