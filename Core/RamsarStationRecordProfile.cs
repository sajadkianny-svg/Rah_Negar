namespace Rah_Negar.Core;

/// <summary>
/// پروفایل رابط کاربری مخصوص Ramsar Station.
/// این کلاس فقط مشخصات UI وابسته به ایستگاه را فراهم می‌کند
/// و نباید هیچ وابستگی به فرم، دیتابیس، یا منطق ذخیره‌سازی داشته باشد.
/// </summary>
public sealed class RamsarStationRecordProfile : IStationUiProfile
{
    /// <summary>
    /// نام ایستگاهی که این پروفایل برای آن ساخته شده است.
    /// </summary>
    public string StationName => "Ramsar Station";

    /// <summary>
    /// پروفایل گرید مخصوص Ramsar Station را برمی‌گرداند.
    /// </summary>
    public GridProfile GetGridProfile()
    {
        return RamsarGridProfileFactory.Create();
    }

    /// <summary>
    /// پروفایل Paste مخصوص Ramsar Station را برمی‌گرداند.
    /// </summary>
    public PasteProfile GetPasteProfile()
    {
        return RamsarPasteProfileFactory.Create();
    }
}