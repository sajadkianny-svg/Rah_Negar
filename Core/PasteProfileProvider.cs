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
        if (GenericProfileIdentity.TryGetUnitCount(stationName, out int genericUnitCount))
            return GenericPasteProfileFactory.Create(genericUnitCount);

        if (LegacyStationProfileCompatibility.TryGetUnitCount(stationName, out int units, out bool lines))
            return GenericPasteProfileFactory.Create(CanonicalProfileDefinition.Create(stationName, units,
                lines ? ["line_f_p", "line40_p", "line30_p"] : []));
        throw new NotSupportedException($"Paste profile requires a canonical persisted definition: {stationName}");
    }

    public static PasteProfile GetProfile(CanonicalProfileDefinition definition) =>
        GenericPasteProfileFactory.Create(definition);
}
