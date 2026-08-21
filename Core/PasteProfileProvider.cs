namespace Rah_Negar.Core;

/// <summary>
/// ارائه‌دهنده مرکزی پروفایل Paste بر اساس نام ایستگاه.
/// این کلاس فقط مسئول انتخاب PasteProfile مناسب است.
/// </summary>
public static class PasteProfileProvider
{
    /// <summary>
    /// پروفایل Paste مناسب ایستگاه را برمی‌گرداند.
    /// </summary>
    /// <param name="stationName">نام ایستگاه</param>
    /// <returns>PasteProfile مربوط به ایستگاه</returns>
    public static PasteProfile GetProfile(string stationName)
    {
        return stationName switch
        {
            "Rasht Station" => RashtPasteProfileFactory.Create(),
            "Ramsar Station" => RamsarPasteProfileFactory.Create(),
            _ => throw new NotSupportedException($"Paste profile is not implemented for: {stationName}")
        };
    }
}