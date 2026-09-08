using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Data.Sqlite;
using Rah_Negar.Core.Reports;
using Rah_Negar.Core;
using Rah_Negar.Models.Reports;

namespace Rah_Negar.Services.Reports;

/// <summary>
/// سرویس اصلی تولید گزارش.
/// این سرویس اجزای مختلف گزارش‌گیری را به هم متصل می‌کند:
/// خواندن داده، بررسی کامل بودن روزها، محاسبه خلاصه آماری و ساخت داده نمودار.
/// </summary>
public static class ReportEngineService
{
    /// <summary>
    /// گزارش کامل را بر اساس درخواست کاربر و ایستگاه فعال تولید می‌کند.
    /// </summary>
    /// <param name="conn">اتصال باز SQLite.</param>
    /// <param name="stationName">نام ایستگاه فعال.</param>
    /// <param name="request">درخواست گزارش‌گیری.</param>
    /// <returns>خروجی کامل گزارش.</returns>
    public static ReportResult BuildReport(
        SqliteConnection conn,
        string stationName,
        ReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(conn);
        ArgumentNullException.ThrowIfNull(request);

        ReportStationProfile profile =
            ReportStationProfileProvider.GetProfile(stationName);

        IReadOnlyList<ReportParameterDefinition> allParameters =
            profile.Parameters;

        List<ReportParameterDefinition> selectedParameters = allParameters
            .Where(p => request.SelectedParameters.Contains(p.Key))
            .ToList();

        List<string> warnings = [];

        if (selectedParameters.Count == 0)
        {
            warnings.Add("هیچ پارامتر معتبری برای گزارش انتخاب نشده است.");

            return new ReportResult
            {
                Request = request,
                Warnings = warnings
            };
        }

        List<Dictionary<string, object>> dataRows =
            ReportQueryService.LoadDataRows(conn, request, selectedParameters);

        List<Dictionary<string, object>> uniqueRows =
            ReportQueryService.LoadUniqueRows(conn, request, selectedParameters);

        List<ReportSummaryItem> summaryItems =
            ReportAggregationService.BuildSummary(dataRows, uniqueRows, selectedParameters);

        List<ChartPointModel> chartPoints =
            ChartDataBuilder.BuildChartPoints(dataRows, uniqueRows, selectedParameters);

        List<ReportDailyStatus> dailyStatuses = [];

        if (request.IncludeMissingDays)
        {
            dailyStatuses = ReportCompletenessService.CheckRange(
                conn,
                request.DateFrom,
                request.DateTo);

            int incompleteCount = dailyStatuses.Count(x => !x.IsComplete);

            if (incompleteCount > 0)
                warnings.Add($"در بازه انتخاب‌شده {incompleteCount} روز ناقص یا فاقد داده وجود دارد.");
        }

        if (dataRows.Count == 0 && uniqueRows.Count == 0)
        {
            warnings.Add("در بازه انتخاب‌شده هیچ داده‌ای برای گزارش یافت نشد.");
        }

        return new ReportResult
        {
            Request = request,
            SummaryItems = summaryItems,
            ChartPoints = chartPoints,
            DailyStatuses = dailyStatuses,
            Warnings = warnings
        };
    }

    public static ReportResult BuildReport(
        SqliteConnection conn,
        CanonicalProfileDefinition definition,
        ReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(conn);
        ArgumentNullException.ThrowIfNull(request);

        ReportStationProfile profile = ReportStationProfileProvider.GetProfile(definition);
        IReadOnlyList<ReportParameterDefinition> selectedParameters = profile.Parameters
            .Where(p => request.SelectedParameters.Contains(p.Key))
            .ToList();
        if (selectedParameters.Count == 0)
            return new ReportResult { Request = request, Warnings = ["هیچ پارامتر معتبری برای گزارش انتخاب نشده است."] };

        List<Dictionary<string, object>> dataRows = ReportQueryService.LoadDataRows(conn, request, selectedParameters);
        List<Dictionary<string, object>> uniqueRows = ReportQueryService.LoadUniqueRows(conn, request, selectedParameters);
        return new ReportResult
        {
            Request = request,
            SummaryItems = ReportAggregationService.BuildSummary(dataRows, uniqueRows, selectedParameters),
            ChartPoints = ChartDataBuilder.BuildChartPoints(dataRows, uniqueRows, selectedParameters),
            DailyStatuses = request.IncludeMissingDays ? ReportCompletenessService.CheckRange(conn, request.DateFrom, request.DateTo) : [],
            Warnings = dataRows.Count == 0 && uniqueRows.Count == 0 ? ["در بازه انتخاب‌شده هیچ داده‌ای برای گزارش یافت نشد."] : []
        };
    }
}
