using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



using Microsoft.Data.Sqlite;
using Rah_Negar.Core.Reports;
using Rah_Negar.Models.Reports;

namespace Rah_Negar.Services.Reports;

/// <summary>
/// سرویس محاسبه تاریخ‌های وقوع حداقل و حداکثر پارامترهای tbl_data.
/// این سرویس مستقل از UI است و بر اساس پارامترهای پروفایل فعال کار می‌کند.
/// </summary>
public static class ExtremeDatesService
{
    /// <summary>
    /// تاریخ‌های ثبت حداقل و حداکثر پارامترهای منتخب را در بازه گزارش محاسبه می‌کند.
    /// </summary>
    public static List<ExtremeDateItem> Calculate(
        SqliteConnection conn,
        ReportStationProfile profile,
        long dateFrom,
        long dateTo)
    {
        ArgumentNullException.ThrowIfNull(conn);
        ArgumentNullException.ThrowIfNull(profile);

        List<ExtremeDateItem> result = [];

        List<ReportParameterDefinition> parameters = GetExtremeTargetParameters(profile);

        foreach (ReportParameterDefinition parameter in parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.DataColumnName))
                continue;

            ExtremeDateItem? item = CalculateForParameter(
                conn,
                parameter,
                dateFrom,
                dateTo);

            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// پارامترهای هدف برای تحلیل تاریخ حداقل و حداکثر را از پروفایل فعال استخراج می‌کند.
    /// اگر پروفایل ستونی را نداشته باشد، خودکار حذف می‌شود.
    /// </summary>
    private static List<ReportParameterDefinition> GetExtremeTargetParameters(
        ReportStationProfile profile)
    {
        string[] targetKeys =
        [
            "in_p",
            "out_p",
            "flow",
            "out_t",
            "amb_t"
        ];

        return profile.Parameters
            .Where(p => targetKeys.Contains(p.Key))
            .Where(p => !string.IsNullOrWhiteSpace(p.DataColumnName))
            .Where(p => p.SupportedAggregations.Contains(ReportAggregationType.Min))
            .Where(p => p.SupportedAggregations.Contains(ReportAggregationType.Max))
            .ToList();
    }

    /// <summary>
    /// حداقل، حداکثر و تاریخ‌های وقوع آن‌ها را برای یک پارامتر محاسبه می‌کند.
    /// </summary>
    private static ExtremeDateItem? CalculateForParameter(
        SqliteConnection conn,
        ReportParameterDefinition parameter,
        long dateFrom,
        long dateTo)
    {
        string columnName = parameter.DataColumnName!;

        double? minValue;
        double? maxValue;

        using (SqliteCommand cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                $"""
                SELECT
                    MIN({EscapeIdentifier(columnName)}) AS min_value,
                    MAX({EscapeIdentifier(columnName)}) AS max_value
                FROM tbl_data
                WHERE date_rep BETWEEN $from AND $to;
                """;

            cmd.Parameters.AddWithValue("$from", dateFrom);
            cmd.Parameters.AddWithValue("$to", dateTo);

            using SqliteDataReader reader = cmd.ExecuteReader();

            if (!reader.Read())
                return null;

            minValue = reader["min_value"] == DBNull.Value
                ? null
                : Convert.ToDouble(reader["min_value"]);

            maxValue = reader["max_value"] == DBNull.Value
                ? null
                : Convert.ToDouble(reader["max_value"]);
        }

        if (!minValue.HasValue || !maxValue.HasValue)
            return null;

        List<long> minDates = LoadDatesForValue(
            conn,
            columnName,
            minValue.Value,
            dateFrom,
            dateTo);

        List<long> maxDates = LoadDatesForValue(
            conn,
            columnName,
            maxValue.Value,
            dateFrom,
            dateTo);

        return new ExtremeDateItem
        {
            ParameterKey = parameter.Key,
            DisplayName = parameter.DisplayName,
            MinValue = minValue,
            MinDates = minDates,
            MaxValue = maxValue,
            MaxDates = maxDates
        };
    }

    /// <summary>
    /// تاریخ‌هایی را که مقدار پارامتر در آن‌ها برابر مقدار هدف است برمی‌گرداند.
    /// تاریخ‌ها Distinct هستند چون ممکن است در یک روز چند بار همان مقدار ثبت شده باشد.
    /// </summary>
    private static List<long> LoadDatesForValue(
        SqliteConnection conn,
        string columnName,
        double targetValue,
        long dateFrom,
        long dateTo)
    {
        List<long> dates = [];

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText =
            $"""
            SELECT DISTINCT date_rep
            FROM tbl_data
            WHERE date_rep BETWEEN $from AND $to
              AND ABS({EscapeIdentifier(columnName)} - $value) < 0.000001
            ORDER BY date_rep;
            """;

        cmd.Parameters.AddWithValue("$from", dateFrom);
        cmd.Parameters.AddWithValue("$to", dateTo);
        cmd.Parameters.AddWithValue("$value", targetValue);

        using SqliteDataReader reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            if (reader["date_rep"] != DBNull.Value)
                dates.Add(Convert.ToInt64(reader["date_rep"]));
        }

        return dates;
    }

    /// <summary>
    /// نام ستون دیتابیس را برای استفاده امن‌تر در SQL داخل براکت قرار می‌دهد.
    /// توجه: نام ستون فقط از Registry داخلی برنامه می‌آید، نه ورودی کاربر.
    /// </summary>
    private static string EscapeIdentifier(string identifier)
    {
        return "\"" + identifier.Replace("\"", "\"\"") + "\"";
    }
}