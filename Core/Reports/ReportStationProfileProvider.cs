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
        if (GenericProfileIdentity.TryGetUnitCount(stationName, out int genericUnitCount))
        {
            return new ReportStationProfile
            {
                StationName = stationName,
                Units = Enumerable.Range(1, genericUnitCount).Select(i => $"U{i}").ToArray(),
                Parameters = ReportParameterRegistry.GetGenericParameters(genericUnitCount)
            };
        }

        if (LegacyStationProfileCompatibility.TryGetUnitCount(stationName, out int units, out bool lines))
        {
            CanonicalProfileDefinition definition = CanonicalProfileDefinition.Create(stationName, units,
                lines ? ["line_f_p", "line40_p", "line30_p"] : []);
            return GetProfile(definition);
        }
        throw new NotSupportedException("پروفایل گزارش‌گیری به تعریف canonical نیاز دارد.");
    }

    public static ReportStationProfile GetProfile(CanonicalProfileDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new ReportStationProfile
        {
            StationName = definition.StationName,
            ProfileId = definition.ProfileId,
            ProfileRevision = definition.Revision,
            Units = Enumerable.Range(1, definition.UnitCount).Select(i => $"U{i}").ToArray(),
            Parameters = ReportParameterRegistry.GetGenericParameters(definition)
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

