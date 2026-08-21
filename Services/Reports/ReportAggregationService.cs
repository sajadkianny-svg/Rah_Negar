using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rah_Negar.Models.Reports;

namespace Rah_Negar.Services.Reports;

/// <summary>
/// سرویس محاسبه خلاصه‌های آماری گزارش.
/// این سرویس داده خام را دریافت می‌کند و min / max / avg / sum را تولید می‌کند.
/// </summary>
public static class ReportAggregationService
{
    /// <summary>
    /// خلاصه آماری پارامترهای انتخاب‌شده را تولید می‌کند.
    /// </summary>
    public static List<ReportSummaryItem> BuildSummary(
        IReadOnlyList<Dictionary<string, object>> dataRows,
        IReadOnlyList<Dictionary<string, object>> uniqueRows,
        IReadOnlyList<ReportParameterDefinition> parameters)
    {
        List<ReportSummaryItem> result = [];

        foreach (ReportParameterDefinition parameter in parameters)
        {
            if (parameter.DataColumnName != null)
            {
                AddDataColumnSummary(result, dataRows, parameter);
            }

            if (parameter.UniqueColumnName != null)
            {
                AddUniqueColumnSummary(result, uniqueRows, parameter);
            }
        }

        return result;
    }

    /// <summary>
    /// خلاصه آماری پارامترهای ساعتی tbl_data را محاسبه می‌کند.
    /// </summary>
    private static void AddDataColumnSummary(
        List<ReportSummaryItem> result,
        IReadOnlyList<Dictionary<string, object>> rows,
        ReportParameterDefinition parameter)
    {
        List<double> values = ExtractNumericValues(rows, parameter.DataColumnName!);

        AddSummaryItems(result, parameter, values);
    }

    /// <summary>
    /// خلاصه آماری پارامترهای روزانه tbl_unique را محاسبه می‌کند.
    /// </summary>
    private static void AddUniqueColumnSummary(
        List<ReportSummaryItem> result,
        IReadOnlyList<Dictionary<string, object>> rows,
        ReportParameterDefinition parameter)
    {
        List<double> values = ExtractNumericValues(rows, parameter.UniqueColumnName!);

        AddSummaryItems(result, parameter, values);
    }

    /// <summary>
    /// بر اساس نوع محاسبات پشتیبانی‌شده، آیتم‌های خلاصه را ایجاد می‌کند.
    /// </summary>
    private static void AddSummaryItems(
        List<ReportSummaryItem> result,
        ReportParameterDefinition parameter,
        List<double> values)
    {
        foreach (ReportAggregationType aggregation in parameter.SupportedAggregations)
        {
            double? value = values.Count == 0
                ? null
                : aggregation switch
                {
                    ReportAggregationType.Min => values.Min(),
                    ReportAggregationType.Max => values.Max(),
                    ReportAggregationType.Avg => values.Average(),
                    ReportAggregationType.Sum => values.Sum(),
                    _ => null
                };

            result.Add(new ReportSummaryItem
            {
                ParameterKey = parameter.Key,
                DisplayName = parameter.DisplayName,
                AggregationType = aggregation,
                Value = value,
                ValueCount = values.Count
            });
        }
    }

    /// <summary>
    /// مقادیر عددی یک ستون را از ردیف‌های خام استخراج می‌کند.
    /// مقادیر null یا غیرقابل تبدیل نادیده گرفته می‌شوند.
    /// </summary>
    private static List<double> ExtractNumericValues(
        IReadOnlyList<Dictionary<string, object>> rows,
        string columnName)
    {
        List<double> values = [];

        foreach (Dictionary<string, object> row in rows)
        {
            if (!row.TryGetValue(columnName, out object? rawValue))
                continue;

            if (rawValue == null)
                continue;

            if (double.TryParse(rawValue.ToString(), out double value))
                values.Add(value);
        }

        return values;
    }
}