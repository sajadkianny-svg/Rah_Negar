using Microsoft.Data.Sqlite;
using Rah_Negar.Data;
using Rah_Negar.Models.Reports;

namespace Rah_Negar.Services.Reports;

/// <summary>
/// سرویس خواندن Snapshot گزارش ماهانه نهایی‌شده.
/// این سرویس داده را از جدول‌های نهایی‌شده می‌خواند، نه از داده خام عملیاتی.
/// </summary>
public static class MonthlyFinalReportReadService
{
    /// <summary>
    /// خلاصه آماری گزارش ماهانه نهایی‌شده را از Snapshot می‌خواند
    /// و به شکل ReportResult برمی‌گرداند تا با UI فعلی سازگار باشد.
    /// </summary>
    public static ReportResult LoadMonthlySummarySnapshot(
        SqliteConnection conn,
        int year,
        int month)
    {
        ArgumentNullException.ThrowIfNull(conn);

        List<ReportSummaryItem> summaryItems = LoadSummaryItems(conn, year, month);

        return new ReportResult
        {
            SummaryItems = summaryItems
        };
    }

    /// <summary>
    /// ردیف‌های Summary ذخیره‌شده در Snapshot ماهانه را می‌خواند.
    /// </summary>
    private static List<ReportSummaryItem> LoadSummaryItems(
        SqliteConnection conn,
        int year,
        int month)
    {
        const string sql = @"
SELECT
    parameter_key,
    parameter_title,
    aggregation_type,
    value,
    value_count
FROM tbl_monthly_report_summary
WHERE year_rep = @year_rep
  AND month_rep = @month_rep
ORDER BY parameter_key, aggregation_type;";

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@year_rep", year);
        cmd.Parameters.AddWithValue("@month_rep", month);

        using SqliteDataReader reader = cmd.ExecuteReader();

        List<ReportSummaryItem> items = [];

        while (reader.Read())
        {
            string aggregationText = reader["aggregation_type"].ToString() ?? string.Empty;

            if (!Enum.TryParse(aggregationText, out ReportAggregationType aggregationType))
                continue;

            double? value = reader["value"] == DBNull.Value
                ? null
                : Convert.ToDouble(reader["value"]);

            items.Add(new ReportSummaryItem
            {
                ParameterKey = reader["parameter_key"].ToString() ?? string.Empty,
                DisplayName = reader["parameter_title"].ToString() ?? string.Empty,
                AggregationType = aggregationType,
                Value = value,
                ValueCount = reader["value_count"] == DBNull.Value
                    ? 0
                    : Convert.ToInt32(reader["value_count"])
            });
        }

        return items;
    }

    /// <summary>
    /// مقدار شاخص Recycle Change را از Snapshot ماهانه می‌خواند.
    /// </summary>
    public static int LoadRecycleChangeCount(
        SqliteConnection conn,
        int year,
        int month)
    {
        const string sql = @"
SELECT value
FROM tbl_monthly_report_service_summary
WHERE year_rep = @year_rep
  AND month_rep = @month_rep
  AND item_key = 'recycle_change_count'
LIMIT 1;";

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@year_rep", year);
        cmd.Parameters.AddWithValue("@month_rep", month);

        object? result = cmd.ExecuteScalar();

        if (result == null || result == DBNull.Value)
            return 0;

        return Convert.ToInt32(result);
    }


    /// <summary>
    /// خلاصه رویدادها و Runtime واحدها را از گزارش ماهانه نهایی‌شده می‌خواند.
    /// خروجی این متد برای پر کردن dgvEventSummary استفاده می‌شود.
    /// </summary>
    public static EventReportResult LoadMonthlyEventSummarySnapshot(
        SqliteConnection conn,
        int year,
        int month)
    {
        ArgumentNullException.ThrowIfNull(conn);

        List<UnitEventSummary> unitSummaries = [];

        const string sql = @"
SELECT
    unit,
    runtime_hours,
    runtime_after_oh,
    total_events,
    start_count,
    nsd_count,
    esd_count,
    esd_extra_hours_total,
    longest_run_hours,
    day_start_count,
    night_start_count,
    day_nsd_count,
    night_nsd_count,
    day_esd_count,
    night_esd_count
FROM tbl_monthly_report_unit_event_summary
WHERE year_rep = @year_rep
  AND month_rep = @month_rep
ORDER BY unit;";

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@year_rep", year);
        cmd.Parameters.AddWithValue("@month_rep", month);

        using SqliteDataReader reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            unitSummaries.Add(new UnitEventSummary
            {
                Unit = reader["unit"].ToString() ?? string.Empty,
                RuntimeHours = Convert.ToDouble(reader["runtime_hours"]),
                RuntimeAfterOH = Convert.ToDouble(reader["runtime_after_oh"]),
                TotalEvents = Convert.ToInt32(reader["total_events"]),
                StartCount = Convert.ToInt32(reader["start_count"]),
                NSDCount = Convert.ToInt32(reader["nsd_count"]),
                ESDCount = Convert.ToInt32(reader["esd_count"]),
                EsdExtraHoursTotal = Convert.ToDouble(reader["esd_extra_hours_total"]),
                LongestRunHours = Convert.ToDouble(reader["longest_run_hours"]),
                DayStartCount = Convert.ToInt32(reader["day_start_count"]),
                NightStartCount = Convert.ToInt32(reader["night_start_count"]),
                DayNSDCount = Convert.ToInt32(reader["day_nsd_count"]),
                NightNSDCount = Convert.ToInt32(reader["night_nsd_count"]),
                DayESDCount = Convert.ToInt32(reader["day_esd_count"]),
                NightESDCount = Convert.ToInt32(reader["night_esd_count"])
            });
        }

        return new EventReportResult
        {
            UnitSummaries = unitSummaries
        };
    }




    /// <summary>
    /// خلاصه Service Days را از Snapshot می‌خواند.
    /// </summary>
    public static Dictionary<string, double> LoadServiceDaysSummary(
        SqliteConnection conn,
        int year,
        int month)
    {
        const string sql = @"
SELECT item_key, value
FROM tbl_monthly_report_service_summary
WHERE year_rep = @year_rep
  AND month_rep = @month_rep;";

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@year_rep", year);
        cmd.Parameters.AddWithValue("@month_rep", month);

        using SqliteDataReader reader = cmd.ExecuteReader();

        Dictionary<string, double> map = [];

        while (reader.Read())
        {
            string key = reader["item_key"].ToString() ?? "";
            double value = Convert.ToDouble(reader["value"]);

            map[key] = value;
        }

        return map;
    }



}