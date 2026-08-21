using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Rah_Negar.Core.Reports;

/// <summary>
/// Provider مرکزی برای ساخت پروفایل گزارش‌گیری هر ایستگاه.
/// این کلاس مشخص می‌کند هر ایستگاه چند واحد دارد
/// و از چه پارامترهایی در گزارش پشتیبانی می‌کند.
/// </summary>
public static class ReportStationProfileProvider
{
    /// <summary>
    /// بر اساس نام ایستگاه، پروفایل گزارش‌گیری همان ایستگاه را برمی‌گرداند.
    /// </summary>
    /// <param name="stationName">نام ایستگاه فعال.</param>
    /// <returns>پروفایل گزارش‌گیری ایستگاه.</returns>
    public static ReportStationProfile GetProfile(string stationName)
    {
        return stationName switch
        {
            "Rasht Station" => CreateRashtProfile(),
            "Ramsar Station" => CreateRamsarProfile(),
            _ => throw new NotSupportedException("پروفایل گزارش‌گیری برای این ایستگاه پشتیبانی نمی‌شود.")
        };
    }

    /// <summary>
    /// پروفایل گزارش‌گیری ایستگاه رشت را ایجاد می‌کند.
    /// </summary>
    private static ReportStationProfile CreateRashtProfile()
    {
        return new ReportStationProfile
        {
            StationName = "Rasht Station",
            Units = ["U1", "U2", "U3"],
            Parameters = ReportParameterRegistry.GetRashtParameters()
        };
    }

    /// <summary>
    /// پروفایل گزارش‌گیری ایستگاه رامسر را ایجاد می‌کند.
    /// </summary>
    private static ReportStationProfile CreateRamsarProfile()
    {
        return new ReportStationProfile
        {
            StationName = "Ramsar Station",
            Units = ["U1", "U2", "U3", "U4"],
            Parameters = ReportParameterRegistry.GetRamsarParameters()
        };
    }
}

