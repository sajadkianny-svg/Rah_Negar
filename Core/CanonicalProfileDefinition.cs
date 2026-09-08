using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Rah_Negar.Foundation.Application.Provisioning;

namespace Rah_Negar.Core;

/// <summary>
/// The one operator-defined profile definition used by runtime UI and services.
/// The profile contains only supported product dimensions; it is not an open
/// ended database-column designer.
/// </summary>
public sealed class CanonicalProfileDefinition
{
    public const int CurrentRevision = 1;

    public static readonly IReadOnlySet<string> SupportedOptionalParameters =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "line_f_p",
            "line40_p",
            "line30_p"
        };

    public static readonly IReadOnlySet<string> CoreParameters =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "in_p", "out_p", "rec", "flow", "in_t", "out_t", "amb_t", "ratio"
        };

    private CanonicalProfileDefinition(
        string profileId,
        int revision,
        string stationName,
        int unitCount,
        IEnumerable<string> optionalParameters)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            throw new ArgumentException("Profile identity is required.", nameof(profileId));
        if (string.IsNullOrWhiteSpace(stationName))
            throw new ArgumentException("Station name is required.", nameof(stationName));
        if (revision < 1)
            throw new ArgumentOutOfRangeException(nameof(revision));
        if (!TargetStationProfileRules.IsUnitCountSupported(unitCount))
            throw new ArgumentOutOfRangeException(nameof(unitCount));

        ProfileId = profileId.Trim();
        Revision = revision;
        StationName = stationName.Trim();
        UnitCount = unitCount;
        OptionalParameters = new ReadOnlyCollection<string>(optionalParameters
            .Where(x => SupportedOptionalParameters.Contains(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList());
    }

    public string ProfileId { get; }
    public int Revision { get; }
    public string StationName { get; }
    public int UnitCount { get; }
    public IReadOnlyList<string> OptionalParameters { get; }
    public bool HasLinePressureColumns => OptionalParameters.Count > 0;

    public string Signature => string.Join("|", ProfileId, Revision.ToString(CultureInfo.InvariantCulture),
        StationName, UnitCount.ToString(CultureInfo.InvariantCulture), string.Join(",", OptionalParameters));

    public bool Supports(string parameterKey) =>
        CoreParameters.Contains(parameterKey) || OptionalParameters.Contains(parameterKey);

    public static CanonicalProfileDefinition Create(
        string stationName,
        int unitCount,
        IEnumerable<string>? optionalParameters = null,
        int revision = CurrentRevision,
        string? profileId = null)
    {
        string normalizedName = stationName?.Trim() ?? string.Empty;
        string id = string.IsNullOrWhiteSpace(profileId)
            ? CreateStableProfileId(normalizedName)
            : profileId.Trim();

        return new CanonicalProfileDefinition(id, revision, normalizedName, unitCount,
            optionalParameters ?? Array.Empty<string>());
    }

    public CanonicalProfileDefinition WithRevision(int revision) =>
        Create(StationName, UnitCount, OptionalParameters, revision, ProfileId);

    public static bool TryCreateFromLegacySettings(
        string? profileId,
        int revision,
        string? stationName,
        int unitCount,
        string? optionalParameters,
        out CanonicalProfileDefinition? definition)
    {
        definition = null;
        if (string.IsNullOrWhiteSpace(stationName) ||
            !TargetStationProfileRules.IsUnitCountSupported(unitCount))
            return false;

        try
        {
            IEnumerable<string> parameters = (optionalParameters ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            definition = Create(stationName, unitCount, parameters,
                revision < 1 ? CurrentRevision : revision, profileId);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string CreateStableProfileId(string stationName)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(stationName));
        return "profile-" + Convert.ToHexString(digest)[..16].ToLowerInvariant();
    }
}
