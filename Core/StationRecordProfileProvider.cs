
namespace Rah_Negar.Core;

/// <summary>
/// ارائه‌دهنده مرکزی پروفایل فرم رکورد بر اساس نام ایستگاه.
/// در صورت اضافه شدن ایستگاه جدید، فقط همین کلاس باید به‌روزرسانی شود.
/// </summary>
public static class StationRecordProfileProvider
{
    /// <summary>
    /// پروفایل مناسب ایستگاه را بر اساس نام آن برمی‌گرداند.
    /// </summary>
    /// <param name="stationName">نام ایستگاه ذخیره‌شده در دیتابیس فعال</param>
    /// <returns>پروفایل اختصاصی همان ایستگاه</returns>
    /// <exception cref="NotSupportedException">
    /// اگر برای ایستگاه موردنظر هنوز پروفایل پیاده‌سازی نشده باشد
    /// </exception>
    public static IStationUiProfile GetProfile(string stationName)
    {
        return stationName switch
        {
            "Rasht Station" => new RashtStationRecordProfile(),
            "Ramsar Station" => new RamsarStationRecordProfile(),
            _ => throw new NotSupportedException($"Station profile is not implemented for: {stationName}")
        };
    }
}