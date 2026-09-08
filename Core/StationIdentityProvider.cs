namespace Rah_Negar.Core;

/// <summary>
/// Resolves the operator-facing station identity from the canonical persisted
/// station name. Profile names and unit counts are not station identities.
/// </summary>
public static class StationIdentityProvider
{
    public const string FallbackStationName = "ایستگاه عملیاتی";

    public static string ResolveForLogin(string? configuredStationName)
    {
        string stationName = configuredStationName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(stationName) ||
            GenericProfileIdentity.TryGetUnitCount(stationName, out _))
        {
            return FallbackStationName;
        }

        return stationName;
    }
}
