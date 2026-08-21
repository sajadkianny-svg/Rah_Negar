
using Microsoft.Data.Sqlite;
using Rah_Negar.Data;
using Rah_Negar.Models.Reports;

namespace Rah_Negar.Services.Reports;

/// <summary>
/// ذخیره Snapshot نهایی گزارش ماهانه.
/// این سرویس گزارش نمی‌سازد؛ فقط خروجی آماده ReportEngineService را ذخیره می‌کند.
/// </summary>
public static class MonthlyFinalReportService
{
    /// <summary>
    /// گزارش ماهانه را نهایی کرده، خلاصه‌های آماری و رویدادی را ذخیره می‌کند
    /// و سپس ماه را قفل می‌کند.
    /// </summary>
    public static void FinalizeMonthlyReport(
        SqliteConnection conn,
        SqliteTransaction tx,
        string stationName,
        int year,
        int month,
        long dataStartDate,
        string finalizedBy,
        ReportResult report,
        EventReportResult eventReport,
        int recycleChangeCount)
    {
        ArgumentNullException.ThrowIfNull(conn);
        ArgumentNullException.ThrowIfNull(tx);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(eventReport);

        if (string.IsNullOrWhiteSpace(stationName))
            throw new ArgumentException("نام ایستگاه را وارد نمایید", nameof(stationName));

        if (year <= 0)
            throw new ArgumentOutOfRangeException(nameof(year));

        if (month < 1 || month > 12)
            throw new ArgumentOutOfRangeException(nameof(month));

        if (dataStartDate <= 0)
            throw new ArgumentOutOfRangeException(nameof(dataStartDate));

        if (MonthlyLockService.IsMonthLocked(year, month))
            throw new InvalidOperationException("این ماه قبلاً نهایی و قفل شده است");

        DeleteExistingSnapshot(conn, tx, year, month);

        InsertHeader(
            conn,
            tx,
            stationName,
            year,
            month,
            dataStartDate,
            finalizedBy);

        InsertSummaryItems(
            conn,
            tx,
            year,
            month,
            report.SummaryItems);

        // ذخیره خلاصه رویدادها و Runtime هر واحد برای گزارش نهایی ماهانه
        InsertUnitEventSummaries(
            conn,
            tx,
            year,
            month,
            eventReport.UnitSummaries);

        // ذخیره خلاصه Service Days بر اساس گزارش رویدادها
        InsertServiceDaysSummary(
            conn,
            tx,
            year,
            month,
            eventReport.ServiceDaysByUnit,
            eventReport.UnitSummaries.Select(x => x.Unit).ToList());


        InsertServiceSummaryItem(
            conn,
            tx,
            year,
            month,
            "recycle_change_count",
            "Recycle Change Count",
            recycleChangeCount);

        ValidateSnapshotCreated(
            conn,
            tx,
            year,
            month);

        MonthlyLockService.LockMonth(
            conn,
            tx,
            year,
            month,
            finalizedBy);
    }



    private static void DeleteExistingSnapshot(
        SqliteConnection conn,
        SqliteTransaction tx,
        int year,
        int month)
    {
        var parameters = new List<SqliteParameter>
        {
            SqliteCommandHelper.Param("@year_rep", year),
            SqliteCommandHelper.Param("@month_rep", month)
        };


        SqliteCommandHelper.ExecuteNonQuery(
            conn,
            "DELETE FROM tbl_monthly_report_unit_event_summary WHERE year_rep = @year_rep AND month_rep = @month_rep;",
            parameters,
            tx);

        SqliteCommandHelper.ExecuteNonQuery(
                conn,
            "DELETE FROM tbl_monthly_report_service_summary WHERE year_rep = @year_rep AND month_rep = @month_rep;",
            parameters,
            tx);

        SqliteCommandHelper.ExecuteNonQuery(
            conn,
            "DELETE FROM tbl_monthly_report_summary WHERE year_rep = @year_rep AND month_rep = @month_rep;",
            parameters,
            tx);

        SqliteCommandHelper.ExecuteNonQuery(
            conn,
            "DELETE FROM tbl_monthly_report_header WHERE year_rep = @year_rep AND month_rep = @month_rep;",
            parameters,
            tx);
    }

    private static void InsertHeader(
        SqliteConnection conn,
        SqliteTransaction tx,
        string stationName,
        int year,
        int month,
        long dataStartDate,
        string finalizedBy)
    {
        const string sql = @"
INSERT INTO tbl_monthly_report_header
(
    year_rep,
    month_rep,
    station_name,
    finalized_at,
    finalized_by,
    data_start_date,
    report_title
)
VALUES
(
    @year_rep,
    @month_rep,
    @station_name,
    @finalized_at,
    @finalized_by,
    @data_start_date,
    @report_title
);";

        var parameters = new List<SqliteParameter>
        {
            SqliteCommandHelper.Param("@year_rep", year),
            SqliteCommandHelper.Param("@month_rep", month),
            SqliteCommandHelper.Param("@station_name", stationName.Trim()),
            SqliteCommandHelper.Param("@finalized_at", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")),
            SqliteCommandHelper.Param("@finalized_by", string.IsNullOrWhiteSpace(finalizedBy) ? DBNull.Value : finalizedBy.Trim()),
            SqliteCommandHelper.Param("@data_start_date", dataStartDate),
            SqliteCommandHelper.Param("@report_title", $"Monthly Final Report - {stationName.Trim()} - {year}/{month:00}")
        };

        SqliteCommandHelper.ExecuteNonQuery(conn, sql, parameters, tx);
    }

    private static void InsertSummaryItems(
        SqliteConnection conn,
        SqliteTransaction tx,
        int year,
        int month,
        IReadOnlyList<ReportSummaryItem> summaryItems)
    {

        if (summaryItems == null || summaryItems.Count == 0)
            return;

        const string sql = @"
INSERT INTO tbl_monthly_report_summary
(
    year_rep,
    month_rep,
    parameter_key,
    parameter_title,
    aggregation_type,
    value,
    value_count
)
VALUES
(
    @year_rep,
    @month_rep,
    @parameter_key,
    @parameter_title,
    @aggregation_type,
    @value,
    @value_count
);";

        foreach (ReportSummaryItem item in summaryItems)
        {
            if (string.IsNullOrWhiteSpace(item.ParameterKey))
                continue;

            var parameters = new List<SqliteParameter>
            {
                SqliteCommandHelper.Param("@year_rep", year),
                SqliteCommandHelper.Param("@month_rep", month),
                SqliteCommandHelper.Param("@parameter_key", item.ParameterKey.Trim()),
                SqliteCommandHelper.Param("@parameter_title", item.DisplayName.Trim()),
                SqliteCommandHelper.Param("@aggregation_type", item.AggregationType.ToString()),
                SqliteCommandHelper.Param("@value", item.Value),
                 SqliteCommandHelper.Param("@value_count", item.ValueCount),
            };

            SqliteCommandHelper.ExecuteNonQuery(conn, sql, parameters, tx);
        }
    }


    /// <summary>
    /// یک شاخص عملکردی ماهانه را در جدول Service Summary ذخیره می‌کند.
    /// برای مواردی مثل تعداد تغییرات Recycle، روزهای سرویس یا شاخص‌های محاسباتی ماهانه استفاده می‌شود.
    /// </summary>
    private static void InsertServiceSummaryItem(
        SqliteConnection conn,
        SqliteTransaction tx,
        int year,
        int month,
        string itemKey,
        string itemTitle,
        double value)
    {
        const string sql = @"
INSERT INTO tbl_monthly_report_service_summary
(
    year_rep,
    month_rep,
    item_key,
    item_title,
    value
)
VALUES
(
    @year_rep,
    @month_rep,
    @item_key,
    @item_title,
    @value
);";

        var parameters = new List<SqliteParameter>
    {
        SqliteCommandHelper.Param("@year_rep", year),
        SqliteCommandHelper.Param("@month_rep", month),
        SqliteCommandHelper.Param("@item_key", itemKey),
        SqliteCommandHelper.Param("@item_title", itemTitle),
        SqliteCommandHelper.Param("@value", value)
    };

        SqliteCommandHelper.ExecuteNonQuery(conn, sql, parameters, tx);
    }

    /// <summary>
    /// خلاصه محاسبات رویدادی و Runtime هر واحد را در Snapshot ماهانه ذخیره می‌کند.
    /// این داده‌ها بعداً برای نمایش گزارش نهایی و تولید PDF استفاده می‌شوند.
    /// </summary>
    private static void InsertUnitEventSummaries(
        SqliteConnection conn,
        SqliteTransaction tx,
        int year,
        int month,
        IReadOnlyList<UnitEventSummary> summaries)
    {
        if (summaries == null || summaries.Count == 0)
            return;

        const string sql = @"
INSERT INTO tbl_monthly_report_unit_event_summary
(
    year_rep,
    month_rep,
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
)
VALUES
(
    @year_rep,
    @month_rep,
    @unit,
    @runtime_hours,
    @runtime_after_oh,
    @total_events,
    @start_count,
    @nsd_count,
    @esd_count,
    @esd_extra_hours_total,
    @longest_run_hours,
    @day_start_count,
    @night_start_count,
    @day_nsd_count,
    @night_nsd_count,
    @day_esd_count,
    @night_esd_count
);";

        foreach (UnitEventSummary item in summaries)
        {
            if (string.IsNullOrWhiteSpace(item.Unit))
                continue;

            var parameters = new List<SqliteParameter>
        {
            SqliteCommandHelper.Param("@year_rep", year),
            SqliteCommandHelper.Param("@month_rep", month),
            SqliteCommandHelper.Param("@unit", item.Unit.Trim()),

            SqliteCommandHelper.Param("@runtime_hours", item.RuntimeHours),
            SqliteCommandHelper.Param("@runtime_after_oh", item.RuntimeAfterOH),
            SqliteCommandHelper.Param("@total_events", item.TotalEvents),
            SqliteCommandHelper.Param("@start_count", item.StartCount),
            SqliteCommandHelper.Param("@nsd_count", item.NSDCount),
            SqliteCommandHelper.Param("@esd_count", item.ESDCount),
            SqliteCommandHelper.Param("@esd_extra_hours_total", item.EsdExtraHoursTotal),
            SqliteCommandHelper.Param("@longest_run_hours", item.LongestRunHours),

            SqliteCommandHelper.Param("@day_start_count", item.DayStartCount),
            SqliteCommandHelper.Param("@night_start_count", item.NightStartCount),
            SqliteCommandHelper.Param("@day_nsd_count", item.DayNSDCount),
            SqliteCommandHelper.Param("@night_nsd_count", item.NightNSDCount),
            SqliteCommandHelper.Param("@day_esd_count", item.DayESDCount),
            SqliteCommandHelper.Param("@night_esd_count", item.NightESDCount)
        };

            SqliteCommandHelper.ExecuteNonQuery(conn, sql, parameters, tx);
        }
    }



    /// <summary>
    /// خلاصه تعداد روزهای سرویس هر واحد و تعداد روزهای همزمانی واحدها را ذخیره می‌کند.
    /// </summary>
    private static void InsertServiceDaysSummary(
        SqliteConnection conn,
        SqliteTransaction tx,
        int year,
        int month,
        Dictionary<string, HashSet<long>> serviceDaysByUnit,
        List<string> units)
    {
        if (serviceDaysByUnit == null || serviceDaysByUnit.Count == 0)
            return;

        // 1) تعداد روزهای سرویس هر واحد
        foreach (string unit in units)
        {
            int days = serviceDaysByUnit.TryGetValue(unit, out var set)
                ? set.Count
                : 0;

            InsertServiceSummaryItem(
                conn,
                tx,
                year,
                month,
                $"unit_service_days_{unit}",
                $"Service Days {unit}",
                days);
        }

        // 2) تعداد روزهای همزمانی (چند واحد باهم فعال بودند)
        Dictionary<long, int> activeUnitsPerDay = [];

        foreach (var kvp in serviceDaysByUnit)
        {
            foreach (long day in kvp.Value)
            {
                activeUnitsPerDay[day] = activeUnitsPerDay.TryGetValue(day, out int c)
                    ? c + 1
                    : 1;
            }
        }

        int maxUnits = units.Count;

        for (int count = 1; count <= maxUnits; count++)
        {
            int days = activeUnitsPerDay.Values.Count(x => x == count);

            InsertServiceSummaryItem(
                conn,
                tx,
                year,
                month,
                $"combination_{count}_units_days",
                $"{count} Units Active Days",
                days);
        }
    }

    /// <summary>
    /// بررسی می‌کند Snapshot ماهانه قبل از قفل شدن ماه واقعاً ذخیره شده باشد.
    /// اگر هر بخش اصلی ناقص باشد، عملیات Finalize باید متوقف شود.
    /// </summary>
    private static void ValidateSnapshotCreated(
        SqliteConnection conn,
        SqliteTransaction tx,
        int year,
        int month)
    {
        int headerCount = CountSnapshotRows(
            conn,
            tx,
            "tbl_monthly_report_header",
            year,
            month);

        int summaryCount = CountSnapshotRows(
            conn,
            tx,
            "tbl_monthly_report_summary",
            year,
            month);

        int eventSummaryCount = CountSnapshotRows(
            conn,
            tx,
            "tbl_monthly_report_unit_event_summary",
            year,
            month);

        int serviceSummaryCount = CountSnapshotRows(
            conn,
            tx,
            "tbl_monthly_report_service_summary",
            year,
            month);

        if (headerCount != 1)
            throw new InvalidOperationException("ثبت اطلاعات اصلی گزارش نهایی انجام نشد.");

        if (summaryCount == 0)
            throw new InvalidOperationException("خلاصه آماری گزارش نهایی ذخیره نشد.");

        if (eventSummaryCount == 0)
            throw new InvalidOperationException("خلاصه رویدادهای گزارش نهایی ذخیره نشد.");

        if (serviceSummaryCount == 0)
            throw new InvalidOperationException("خلاصه روزهای سرویس گزارش نهایی ذخیره نشد.");
    }


    private static int CountSnapshotRows(
    SqliteConnection conn,
    SqliteTransaction tx,
    string tableName,
    int year,
    int month)
    {
        using SqliteCommand cmd = conn.CreateCommand();

        cmd.Transaction = tx;

        cmd.CommandText = $@"
SELECT COUNT(*)
FROM {tableName}
WHERE year_rep = @year_rep
  AND month_rep = @month_rep;";

        cmd.Parameters.AddWithValue("@year_rep", year);
        cmd.Parameters.AddWithValue("@month_rep", month);

        return Convert.ToInt32(cmd.ExecuteScalar());
    }

}
