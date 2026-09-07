using System.Globalization;
using System.Text.RegularExpressions;
using Rah_Negar.Foundation.Application.Provisioning;

namespace Rah_Negar.Core;

public static class GenericProfileIdentity
{
    private static readonly Regex Pattern = new(
        @"^Generic Profile \((?<count>[3-5]) Units\)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Create(int unitCount)
    {
        if (!TargetStationProfileRules.IsUnitCountSupported(unitCount))
            throw new ArgumentOutOfRangeException(nameof(unitCount));

        return $"Generic Profile ({unitCount.ToString(CultureInfo.InvariantCulture)} Units)";
    }

    public static bool TryGetUnitCount(string? profileName, out int unitCount)
    {
        unitCount = 0;
        Match match = Pattern.Match(profileName?.Trim() ?? string.Empty);
        return match.Success && int.TryParse(match.Groups["count"].Value,
            NumberStyles.None, CultureInfo.InvariantCulture, out unitCount);
    }
}
