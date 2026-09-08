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
        if (GenericProfileIdentity.TryGetUnitCount(stationName, out int genericUnitCount))
            return GenericGridProfileFactory.Create(genericUnitCount);

        if (LegacyStationProfileCompatibility.TryGetUnitCount(stationName, out int units, out bool lines))
        {
            GridProfile profile = GenericGridProfileFactory.Create(CanonicalProfileDefinition.Create(stationName, units,
                lines ? ["line_f_p", "line40_p", "line30_p"] : []));
            for (int i = 0; i < profile.Columns.Count; i++)
                profile.Columns[i].Name = $"col{i + 1}";
            return profile;
        }
        throw new NotSupportedException($"Grid profile requires a canonical persisted definition: {stationName}");
    }

    public static GridProfile GetProfile(CanonicalProfileDefinition definition) =>
        GenericGridProfileFactory.Create(definition);
}
