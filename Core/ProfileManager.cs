using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Core;

/// <summary>
/// مسئول انتخاب و برگرداندن پروفایل ایستگاه
/// </summary>
public static class ProfileManager
{
    public static IStationProfile GetProfile(CanonicalProfileDefinition definition) =>
        new GenericStationProfile(definition);

    public static IStationProfile GetProfile(StationType stationType)
        => LegacyStationProfileCompatibility.GetProfile(stationType);
}
