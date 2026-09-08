
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
        if (GenericProfileIdentity.TryGetUnitCount(stationName, out int genericUnitCount))
            return new GenericStationRecordProfile(stationName, genericUnitCount);

        if (LegacyStationProfileCompatibility.TryGetUnitCount(stationName, out int units, out _))
            return units == 3 ? new RashtStationRecordProfile() : new RamsarStationRecordProfile();
        throw new NotSupportedException($"Station profile requires a canonical persisted definition: {stationName}");
    }

    public static IStationUiProfile GetProfile(CanonicalProfileDefinition definition) =>
        new GenericStationRecordProfile(definition);
}
