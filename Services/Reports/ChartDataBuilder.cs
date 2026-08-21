using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rah_Negar.Models.Reports;

namespace Rah_Negar.Services.Reports;

/// <summary>
/// سرویس ساخت داده‌های آماده برای نمودار گزارش.
/// این سرویس داده خام را به نقاط قابل نمایش در Chart تبدیل می‌کند.
/// </summary>
public static class ChartDataBuilder
{
    /// <summary>
    /// نقاط نمودار را برای پارامترهای انتخاب‌شده می‌سازد.
    /// </summary>
    public static List<ChartPointModel> BuildChartPoints(
        IReadOnlyList<Dictionary<string, object>> dataRows,
        IReadOnlyList<Dictionary<string, object>> uniqueRows,
        IReadOnlyList<ReportParameterDefinition> parameters)
    {
        List<ChartPointModel> result = [];

        foreach (ReportParameterDefinition parameter in parameters)
        {
            if (parameter.DataColumnName != null)
            {
                AddDataColumnPoints(result, dataRows, parameter);
            }

            if (parameter.UniqueColumnName != null)
            {
                AddUniqueColumnPoints(result, uniqueRows, parameter);
            }
        }

        return result;
    }

    /// <summary>
    /// نقاط نمودار مربوط به داده‌های ساعتی tbl_data را اضافه می‌کند.
    /// </summary>
    private static void AddDataColumnPoints(
        List<ChartPointModel> result,
        IReadOnlyList<Dictionary<string, object>> rows,
        ReportParameterDefinition parameter)
    {
        foreach (Dictionary<string, object> row in rows)
        {
            long dateRep = Convert.ToInt64(row["date_rep"]);
            string? timeRep = row["time_rep"]?.ToString();

            double? value = TryGetDouble(row, parameter.DataColumnName!);

            result.Add(new ChartPointModel
            {
                DateRep = dateRep,
                TimeRep = timeRep,
                ParameterKey = parameter.Key,
                Value = value
            });
        }
    }

    /// <summary>
    /// نقاط نمودار مربوط به داده‌های روزانه tbl_unique را اضافه می‌کند.
    /// </summary>
    private static void AddUniqueColumnPoints(
        List<ChartPointModel> result,
        IReadOnlyList<Dictionary<string, object>> rows,
        ReportParameterDefinition parameter)
    {
        foreach (Dictionary<string, object> row in rows)
        {
            long dateRep = Convert.ToInt64(row["date_rep"]);

            double? value = TryGetDouble(row, parameter.UniqueColumnName!);

            result.Add(new ChartPointModel
            {
                DateRep = dateRep,
                TimeRep = null,
                ParameterKey = parameter.Key,
                Value = value
            });
        }
    }

    /// <summary>
    /// مقدار یک ستون را به عدد اعشاری تبدیل می‌کند.
    /// اگر مقدار وجود نداشته باشد یا قابل تبدیل نباشد، null برمی‌گرداند.
    /// </summary>
    private static double? TryGetDouble(
        Dictionary<string, object> row,
        string columnName)
    {
        if (!row.TryGetValue(columnName, out object? rawValue))
            return null;

        if (rawValue == null)
            return null;

        return double.TryParse(rawValue.ToString(), out double value)
            ? value
            : null;
    }
}