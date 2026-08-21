namespace Rah_Negar.Core;

/// <summary>
/// پروفایل رابط کاربری مخصوص Rasht Station.
/// این کلاس فقط مشخصات UI وابسته به ایستگاه را فراهم می‌کند
/// و نباید هیچ وابستگی به فرم، دیتابیس، یا منطق ذخیره‌سازی داشته باشد.
/// </summary>
public sealed class RashtStationRecordProfile : IStationUiProfile
{
    /// <summary>
    /// نام ایستگاهی که این پروفایل برای آن ساخته شده است.
    /// </summary>
    public string StationName => "Rasht Station";

    /// <summary>
    /// پروفایل گرید مخصوص Rasht Station را برمی‌گرداند.
    /// </summary>
    public GridProfile GetGridProfile()
    {
        return RashtGridProfileFactory.Create();
    }

    /// <summary>
    /// پروفایل Paste مخصوص Rasht Station را برمی‌گرداند.
    /// </summary>
    public PasteProfile GetPasteProfile()
    {
        return RashtPasteProfileFactory.Create();
    }
}