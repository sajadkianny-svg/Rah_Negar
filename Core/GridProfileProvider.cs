using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace Rah_Negar.Core;

/// <summary>
/// ارائه‌دهنده مرکزی پروفایل گرید بر اساس نام ایستگاه.
/// این کلاس فقط مسئول انتخاب GridProfile مناسب است.
/// </summary>
public static class GridProfileProvider
{
    /// <summary>
    /// پروفایل گرید مناسب ایستگاه را برمی‌گرداند.
    /// </summary>
    /// <param name="stationName">نام ایستگاه</param>
    /// <returns>GridProfile مربوط به ایستگاه</returns>
    public static GridProfile GetProfile(string stationName)
    {
        return stationName switch
        {
            "Rasht Station" => RashtGridProfileFactory.Create(),
            "Ramsar Station" => RamsarGridProfileFactory.Create(),
            _ => throw new NotSupportedException($"Grid profile is not implemented for: {stationName}")
        };
    }
}