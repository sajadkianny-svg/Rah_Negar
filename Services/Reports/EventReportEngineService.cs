using Microsoft.Data.Sqlite;
using Rah_Negar.Core.Reports;
using Rah_Negar.Models;
using Rah_Negar.Models.Reports;

namespace Rah_Negar.Services.Reports;

/// <summary>
/// سرویس اصلی تولید گزارش رویدادها.
/// این سرویس خواندن رویدادها، وضعیت اولیه واحدها، مقادیر پایه Runtime،
//— تنظیمات برنامه و محاسبات نهایی Runtime / Service Days را به هم متصل می‌کند.
/// </summary>
public static class EventReportEngineService
{
    /// <summary>
    /// گزارش کامل رویدادها را برای بازه انتخاب‌شده تولید می‌کند.
    /// </summary>
    public static EventReportResult BuildEventReport(
        SqliteConnection conn,
        ReportStationProfile profile,
        long dateFrom,
        long dateTo)
    {
        ArgumentNullException.ThrowIfNull(conn);
        ArgumentNullException.ThrowIfNull(profile);

        List<EventLogItem> events =
            EventReportQueryService.LoadEvents(conn, dateFrom, dateTo);

        Dictionary<string, UnitInitialEventState> initialStates =
            EventInitialStateService.LoadInitialStates(conn, profile, dateFrom);

        Dictionary<string, double> baseRuntimeHours =
            UnitRuntimeBaseQueryService.LoadBaseRuntimeHours(conn);

        Dictionary<string, double> baseRuntimeAfterOHHours =
            UnitRuntimeBaseQueryService.LoadBaseRuntimeAfterOHHours(conn);

        AppSettingsModel? settings = AppSettingsService.GetSettings();

        bool esdExtraEnabled = settings?.EsdExtraRuntimeEnabled ?? false;

        double esdExtraHours = settings?.EsdExtraRuntimeHours ?? 0;

        EventReportResult result =
            EventRuntimeCalculationService.Calculate(
                profile,
                events,
                dateFrom,
                dateTo,
                baseRuntimeHours,
                baseRuntimeAfterOHHours,
                initialStates,
                esdExtraEnabled,
                esdExtraHours);

        return result;
    }
}