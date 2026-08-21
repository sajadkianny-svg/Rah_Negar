using Microsoft.Data.Sqlite;
using Rah_Negar.Data;
using Rah_Negar.Models.Reports;

namespace Rah_Negar.Services.Reports;

/// <summary>
/// سرویس خواندن و تجمیع گزارش‌های نهایی‌شده چندماهه.
/// برای گزارش نیم‌سال و سالانه زمانی استفاده می‌شود که همه ماه‌های بازه نهایی شده باشند.
/// </summary>
public static class PeriodFinalReportReadService
{
    /// <summary>
    /// Summary چند ماه نهایی‌شده را از دیتابیس می‌خواند و به شکل ReportResult برمی‌گرداند.
    /// </summary>
    public static ReportResult LoadPeriodSummarySnapshot(
        SqliteConnection conn,
        int year,
        List<int> months)
    {
        ArgumentNullException.ThrowIfNull(conn);

        List<ReportSummaryItem> items = LoadSummaryItems(conn, year, months);

        return new ReportResult
        {
            SummaryItems = items
        };
    }

    /// <summary>
    /// آیتم‌های Summary را برای چند ماه می‌خواند و تجمیع می‌کند.
    /// Min کمترین، Max بیشترین، Avg میانگین ساده ماه‌ها، Sum مجموع ماه‌ها محاسبه می‌شود.
    /// </summary>
    private static List<ReportSummaryItem> LoadSummaryItems(
        SqliteConnection conn,
        int year,
        List<int> months)
    {
        List<ReportSummaryItem> rawItems = [];

        foreach (int month in months)
        {
            ReportResult monthly =
                MonthlyFinalReportReadService.LoadMonthlySummarySnapshot(conn, year, month);

            rawItems.AddRange(monthly.SummaryItems);
        }

        return rawItems
            .GroupBy(x => new { x.ParameterKey, x.DisplayName, x.AggregationType })
            .Select(g =>
            {
                List<double> values = g
                    .Where(x => x.Value.HasValue)
                    .Select(x => x.Value!.Value)
                    .ToList();

                double? finalValue = null;

                if (values.Count > 0)
                {
                    finalValue = g.Key.AggregationType switch
                    {
                        ReportAggregationType.Min => values.Min(),
                        ReportAggregationType.Max => values.Max(),
                        ReportAggregationType.Sum => values.Sum(),

                        // میانگین وزنی بر اساس تعداد رکوردهای مؤثر هر ماه
                        ReportAggregationType.Avg => CalculateWeightedAverage(g),

                        _ => null
                    };
                }


                return new ReportSummaryItem
                {
                    ParameterKey = g.Key.ParameterKey,
                    DisplayName = g.Key.DisplayName,
                    AggregationType = g.Key.AggregationType,
                    Value = finalValue,
                    ValueCount = g.Sum(x => x.ValueCount)
                };
            })
            .ToList();
    }



    /// <summary>
    /// میانگین وزنی را برای گزارش‌های چندماهه محاسبه می‌کند.
    /// هر ماه به اندازه تعداد رکوردهای مؤثر خود در میانگین نهایی وزن دارد.
    /// </summary>
    private static double? CalculateWeightedAverage(
        IEnumerable<ReportSummaryItem> items)
    {
        double weightedSum = 0;
        int totalCount = 0;

        foreach (ReportSummaryItem item in items)
        {
            if (!item.Value.HasValue || item.ValueCount <= 0)
                continue;

            weightedSum += item.Value.Value * item.ValueCount;
            totalCount += item.ValueCount;
        }

        if (totalCount == 0)
            return null;

        return weightedSum / totalCount;
    }

}


