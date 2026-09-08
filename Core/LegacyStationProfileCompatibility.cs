namespace Rah_Negar.Core;

/// <summary>
/// Explicit compatibility boundary for historical Rasht/Ramsar fixtures.
/// Production runtime must use CanonicalProfileDefinition overloads instead.
/// </summary>
public static class LegacyStationProfileCompatibility
{
    public static bool TryGetUnitCount(string? stationName, out int unitCount, out bool linePressure)
    {
        unitCount = 0;
        linePressure = false;
        if (string.Equals(stationName, "Rasht Station", StringComparison.Ordinal))
        {
            unitCount = 3;
            linePressure = true;
            return true;
        }
        if (string.Equals(stationName, "Ramsar Station", StringComparison.Ordinal))
        {
            unitCount = 4;
            return true;
        }
        return false;
    }

    public static IStationProfile GetProfile(StationType stationType) => stationType switch
    {
        StationType.Rasht => new RashtProfile(),
        StationType.Ramsar => new RamsarProfile(),
        _ => throw new NotSupportedException("Legacy station type is not available in the product wizard.")
    };
}
