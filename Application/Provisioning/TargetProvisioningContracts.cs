using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Rah_Negar.Foundation.Application.Security;

namespace Rah_Negar.Foundation.Application.Provisioning;

public enum TargetStationCode
{
    Rasht,
    Ramsar
}

public static class TargetStationScopeRules
{
    public static int ExpectedUnitCount(TargetStationCode station) => station switch
    {
        TargetStationCode.Rasht => 3,
        TargetStationCode.Ramsar => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(station))
    };

    public static bool IsSupported(TargetStationCode station) =>
        station is TargetStationCode.Rasht or TargetStationCode.Ramsar;
}

public sealed record TargetUnitProvisioningRecord(
    string StationId,
    string UnitId,
    int UnitNumber,
    string UnitName,
    bool IsActive,
    long Revision);

public sealed record TargetShiftProfileProvisioningRecord(
    ShiftProfile Profile,
    ShiftProfileCredentialRecord Credential);

public sealed record TargetTrustedVendorKeyProvisioningRecord(
    string KeyId,
    byte[] SubjectPublicKeyInfo,
    string Algorithm,
    DateTimeOffset ActivatedAtUtc,
    DateTimeOffset? RetiredAtUtc,
    DateTimeOffset CreatedAtUtc,
    long Revision);

public sealed record TargetRuntimeBaselineProvisioningRecord(
    string UnitId,
    string InitialState,
    long EffectiveFromEventDateTime,
    string BaselineVersion,
    string Provenance);

public sealed record TargetEventProvisioningRecord(
    string EventId,
    string StationId,
    string UnitId,
    string EventType,
    int EventDate,
    int EventTime,
    long EventDateTime,
    string? Remark,
    DateTimeOffset CreatedAtUtc,
    string CreatedByShiftProfileId,
    long RowVersion = 1);

public sealed record TargetFinalizedSnapshotProvisioningRecord(
    string SnapshotId,
    string ReportId,
    string StationId,
    long PeriodStartMinute,
    long PeriodEndMinute,
    string PeriodKind,
    int SnapshotSequence,
    string? SupersedesSnapshotId,
    int PayloadSchemaVersion,
    string CanonicalJson,
    string ChecksumAlgorithm,
    string IntegrityFormatVersion,
    string ChecksumValue,
    long CanonicalPayloadLength,
    string SourceRevision,
    DateTimeOffset FinalizedAt);

public sealed record TargetFinalizedLockProvisioningRecord(
    string StationId,
    long PeriodStartMinute,
    long PeriodEndMinute,
    string PeriodKind,
    string EffectiveSnapshotId,
    long Revision,
    string FinalizationId,
    DateTimeOffset FinalizedAt,
    string ActorIdentity);

/// <summary>
/// Sensitive provisioning material is accepted only as an execution input. It is never copied
/// into the safe manifest returned by the provisioning boundary.
/// </summary>
public sealed record TargetStationProvisioningPackage(
    string ManifestId,
    string CorrelationId,
    TargetStationCode Station,
    string StationId,
    string StationName,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<TargetUnitProvisioningRecord> Units,
    IReadOnlyList<TargetShiftProfileProvisioningRecord> ShiftProfiles,
    ManagementCredentialRecord ManagementCredential,
    DeviceIdentityRecord DeviceIdentity,
    TargetTrustedVendorKeyProvisioningRecord TrustedVendorKey,
    IReadOnlyList<TargetRuntimeBaselineProvisioningRecord> RuntimeBaselines,
    IReadOnlyList<TargetEventProvisioningRecord> Events,
    string EsdAdjustmentCanonical,
    IReadOnlyList<TargetFinalizedSnapshotProvisioningRecord> FinalizedSnapshots,
    IReadOnlyList<TargetFinalizedLockProvisioningRecord> FinalizedLocks,
    string ManagementApproverReference,
    string DataOwnerReference,
    string SecurityReviewerReference);

public sealed record TargetProvisioningEntitySummary(
    string EntityType,
    string EntityReference,
    long Revision,
    string Fingerprint);

/// <summary>Safe, reviewable mapping evidence. No password, verifier, private key, or raw personnel number is present.</summary>
public sealed record TargetStationProvisioningManifest(
    string ManifestId,
    string CorrelationId,
    TargetStationCode Station,
    string StationId,
    string StationName,
    int ExpectedUnitCount,
    int TargetSchemaVersion,
    IReadOnlyDictionary<string, int> EntityCounts,
    IReadOnlyList<TargetProvisioningEntitySummary> Entities,
    string EsdAdjustmentFingerprint,
    string ManifestFingerprint,
    string ManagementApproverReference,
    string DataOwnerReference,
    string SecurityReviewerReference);

public enum TargetProvisioningFailure
{
    None,
    InvalidManifest,
    UnsupportedStation,
    SchemaUnavailable,
    Conflict,
    InfrastructureFailure
}

public enum TargetProvisioningOutcome
{
    Provisioned,
    AlreadyProvisioned,
    Rejected
}

public sealed record TargetProvisioningResult(
    TargetProvisioningOutcome Outcome,
    TargetProvisioningFailure Failure,
    TargetStationProvisioningManifest? Manifest,
    IReadOnlyList<string> Issues)
{
    public bool Succeeded => Outcome is TargetProvisioningOutcome.Provisioned or
        TargetProvisioningOutcome.AlreadyProvisioned;
}

public sealed record TargetProvisioningValidationResult(
    bool IsValid,
    IReadOnlyList<string> Issues,
    TargetStationProvisioningManifest? Manifest);

public interface ITargetStationProvisioningBoundary
{
    Task<TargetProvisioningResult> ProvisionAsync(
        TargetStationProvisioningPackage package,
        CancellationToken cancellationToken = default);
}

public static class TargetStationProvisioningManifestBuilder
{
    public static TargetProvisioningValidationResult Validate(TargetStationProvisioningPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var issues = new List<string>();
        ValidateIdentity(package, issues);
        ValidateUnits(package, issues);
        ValidateProfiles(package, issues);
        ValidateSecurity(package, issues);
        ValidateBaselines(package, issues);
        ValidateEvents(package, issues);
        ValidateEsd(package, issues);
        ValidateSnapshotsAndLocks(package, issues);
        ValidateApprovals(package, issues);
        TargetStationProvisioningManifest? manifest = issues.Count == 0 ? Create(package) : null;
        return new(issues.Count == 0, issues.AsReadOnly(), manifest);
    }

    public static TargetStationProvisioningManifest Create(TargetStationProvisioningPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        TargetProvisioningValidationResult validation = ValidateWithoutManifest(package);
        if (!validation.IsValid)
            throw new ArgumentException(string.Join(";", validation.Issues), nameof(package));

        var entities = new List<TargetProvisioningEntitySummary>();
        entities.AddRange(package.Units.OrderBy(x => x.UnitId, StringComparer.Ordinal)
            .Select(x => new TargetProvisioningEntitySummary("Unit", x.UnitId, x.Revision, Fingerprint(
                $"{x.StationId}|{x.UnitId}|{x.UnitNumber}|{x.UnitName}|{x.IsActive}|{x.Revision}"))));
        entities.AddRange(package.ShiftProfiles.OrderBy(x => x.Profile.ShiftProfileId, StringComparer.Ordinal)
            .Select(x => new TargetProvisioningEntitySummary("ShiftProfile", x.Profile.ShiftProfileId, x.Profile.Revision,
                Fingerprint($"{x.Profile.ShiftProfileId}|{x.Profile.StationId}|{x.Profile.ShiftNumber}|" +
                    $"{PersonnelFingerprint(x.Profile.PersonnelNo)}|{x.Profile.IsActive}|{x.Profile.Revision}|" +
                    $"credential:{CredentialFingerprint(x.Credential)}"))));
        entities.Add(new("ManagementCredential", "singleton-1", package.ManagementCredential.CredentialVersion,
            CredentialFingerprint(package.ManagementCredential)));
        entities.Add(new("DeviceIdentity", package.DeviceIdentity.DeviceId, package.DeviceIdentity.Revision,
            Fingerprint($"{package.DeviceIdentity.DeviceId}|{package.DeviceIdentity.Revision}")));
        entities.Add(new("TrustedVendorPublicKey", package.TrustedVendorKey.KeyId, package.TrustedVendorKey.Revision,
            Fingerprint($"{package.TrustedVendorKey.KeyId}|{package.TrustedVendorKey.Algorithm}|" +
                $"{Convert.ToHexString(SHA256.HashData(package.TrustedVendorKey.SubjectPublicKeyInfo))}|{package.TrustedVendorKey.Revision}")));
        entities.AddRange(package.RuntimeBaselines.OrderBy(x => x.UnitId, StringComparer.Ordinal)
            .Select(x => new TargetProvisioningEntitySummary("RuntimeBaseline", x.UnitId, 1,
                Fingerprint($"{x.UnitId}|{x.InitialState}|{x.EffectiveFromEventDateTime}|{x.BaselineVersion}|{x.Provenance}"))));
        entities.AddRange(package.Events.OrderBy(x => x.EventId, StringComparer.Ordinal)
            .Select(x => new TargetProvisioningEntitySummary("Event", x.EventId, x.RowVersion,
                Fingerprint($"{x.EventId}|{x.StationId}|{x.UnitId}|{x.EventType}|{x.EventDate}|{x.EventTime}|" +
                    $"{x.EventDateTime}|{Fingerprint(x.Remark ?? "<null>")}|{x.CreatedByShiftProfileId}|{x.RowVersion}"))));
        entities.Add(new("EsdAdjustment", "singleton-1", 1, Fingerprint(package.EsdAdjustmentCanonical)));
        entities.AddRange(package.FinalizedSnapshots.OrderBy(x => x.SnapshotId, StringComparer.Ordinal)
            .Select(x => new TargetProvisioningEntitySummary("FinalizedSnapshot", x.SnapshotId, x.SnapshotSequence,
                Fingerprint($"{x.SnapshotId}|{x.StationId}|{Fingerprint(x.CanonicalJson)}|{x.ChecksumValue}"))));
        entities.AddRange(package.FinalizedLocks.OrderBy(x => x.EffectiveSnapshotId, StringComparer.Ordinal)
            .Select(x => new TargetProvisioningEntitySummary("FinalizedLock", x.EffectiveSnapshotId, x.Revision,
                Fingerprint($"{x.StationId}|{x.PeriodStartMinute}|{x.PeriodEndMinute}|{x.PeriodKind}|" +
                    $"{x.EffectiveSnapshotId}|{x.Revision}|{x.FinalizationId}"))));

        IReadOnlyDictionary<string, int> counts = new ReadOnlyDictionary<string, int>(
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["Units"] = package.Units.Count,
                ["ShiftProfiles"] = package.ShiftProfiles.Count,
                ["ManagementCredential"] = 1,
                ["DeviceIdentity"] = 1,
                ["TrustedVendorPublicKeys"] = 1,
                ["RuntimeBaselines"] = package.RuntimeBaselines.Count,
                ["Events"] = package.Events.Count,
                ["EsdAdjustment"] = 1,
                ["FinalizedSnapshots"] = package.FinalizedSnapshots.Count,
                ["FinalizedLocks"] = package.FinalizedLocks.Count
            });
        string fingerprint = Fingerprint(string.Join('\n', entities.Select(x =>
            $"{x.EntityType}|{x.EntityReference}|{x.Revision}|{x.Fingerprint}")));
        return new(package.ManifestId.Trim(), package.CorrelationId.Trim(), package.Station,
            package.StationId.Trim(), package.StationName.Trim(), TargetStationScopeRules.ExpectedUnitCount(package.Station),
            4, counts, new ReadOnlyCollection<TargetProvisioningEntitySummary>(entities),
            Fingerprint(package.EsdAdjustmentCanonical), fingerprint, package.ManagementApproverReference.Trim(),
            package.DataOwnerReference.Trim(), package.SecurityReviewerReference.Trim());
    }

    private static TargetProvisioningValidationResult ValidateWithoutManifest(TargetStationProvisioningPackage package)
    {
        var issues = new List<string>();
        ValidateIdentity(package, issues); ValidateUnits(package, issues); ValidateProfiles(package, issues);
        ValidateSecurity(package, issues); ValidateBaselines(package, issues); ValidateEvents(package, issues);
        ValidateEsd(package, issues); ValidateSnapshotsAndLocks(package, issues); ValidateApprovals(package, issues);
        return new(issues.Count == 0, issues.AsReadOnly(), null);
    }

    private static void ValidateIdentity(TargetStationProvisioningPackage p, ICollection<string> issues)
    {
        if (!TargetStationScopeRules.IsSupported(p.Station)) issues.Add("unsupported-station");
        if (!IsSafeId(p.ManifestId) || !IsSafeId(p.CorrelationId)) issues.Add("manifest-correlation-required");
        if (!IsSafeId(p.StationId) || string.IsNullOrWhiteSpace(p.StationName)) issues.Add("station-identity-required");
        if (p.CreatedAtUtc.Offset != TimeSpan.Zero) issues.Add("created-at-must-be-utc");
    }

    private static void ValidateUnits(TargetStationProvisioningPackage p, ICollection<string> issues)
    {
        int expected = TargetStationScopeRules.ExpectedUnitCount(p.Station);
        if (p.Units.Count != expected) issues.Add("unit-count-mismatch");
        if (p.Units.Any(x => !StringComparer.Ordinal.Equals(x.StationId, p.StationId))) issues.Add("unit-station-mismatch");
        if (p.Units.Select(x => x.UnitId).Distinct(StringComparer.Ordinal).Count() != p.Units.Count) issues.Add("duplicate-unit");
        if (p.Units.Select(x => x.UnitNumber).Distinct().Count() != p.Units.Count ||
            p.Units.Any(x => x.UnitNumber < 1 || x.UnitNumber > expected)) issues.Add("unit-number-scope-invalid");
        if (p.Units.Any(x => !IsSafeId(x.UnitId) || string.IsNullOrWhiteSpace(x.UnitName) || x.Revision < 1))
            issues.Add("unit-invalid");
    }

    private static void ValidateProfiles(TargetStationProvisioningPackage p, ICollection<string> issues)
    {
        if (p.ShiftProfiles.Count == 0) issues.Add("shift-profile-required");
        if (p.ShiftProfiles.Select(x => x.Profile.ShiftProfileId).Distinct(StringComparer.Ordinal).Count() != p.ShiftProfiles.Count)
            issues.Add("duplicate-shift-profile");
        if (p.ShiftProfiles.Select(x => x.Profile.ShiftNumber).Distinct().Count() != p.ShiftProfiles.Count)
            issues.Add("duplicate-shift-number");
        foreach (TargetShiftProfileProvisioningRecord item in p.ShiftProfiles)
        {
            if (!StringComparer.Ordinal.Equals(item.Profile.StationId, p.StationId) ||
                !StringComparer.Ordinal.Equals(item.Credential.ShiftProfileId, item.Profile.ShiftProfileId))
                issues.Add("shift-profile-station-or-credential-mismatch");
            if (!IsSafeId(item.Profile.ShiftProfileId) || !IsSafeId(item.Profile.PersonnelNo) ||
                !item.Profile.IsActive || item.Profile.Revision < 1 || item.Credential.CredentialVersion < 1 ||
                !item.Credential.IsCurrent || item.Credential.Salt.Length == 0 || item.Credential.PasswordVerifier.Length == 0)
                issues.Add("shift-profile-invalid");
        }
    }

    private static void ValidateSecurity(TargetStationProvisioningPackage p, ICollection<string> issues)
    {
        ManagementCredentialRecord management = p.ManagementCredential;
        if (management.CredentialVersion < 1 || !management.IsCurrent || !management.IsActive ||
            management.Salt.Length == 0 || management.PasswordVerifier.Length == 0)
            issues.Add("management-credential-invalid");
        if (!IsSafeId(p.DeviceIdentity.DeviceId) || p.DeviceIdentity.DeviceId.Length < 16 || p.DeviceIdentity.Revision < 1)
            issues.Add("device-identity-invalid");
        TargetTrustedVendorKeyProvisioningRecord key = p.TrustedVendorKey;
        if (!IsSafeId(key.KeyId) || key.SubjectPublicKeyInfo.Length == 0 ||
            !StringComparer.Ordinal.Equals(key.Algorithm, "ECDSA-P256-SHA256") || key.Revision < 1 ||
            key.ActivatedAtUtc.Offset != TimeSpan.Zero || key.CreatedAtUtc.Offset != TimeSpan.Zero ||
            (key.RetiredAtUtc is not null && (key.RetiredAtUtc.Value.Offset != TimeSpan.Zero || key.RetiredAtUtc <= key.ActivatedAtUtc)))
            issues.Add("vendor-key-invalid");
    }

    private static void ValidateBaselines(TargetStationProvisioningPackage p, ICollection<string> issues)
    {
        if (p.RuntimeBaselines.Count != p.Units.Count ||
            p.RuntimeBaselines.Select(x => x.UnitId).Distinct(StringComparer.Ordinal).Count() != p.RuntimeBaselines.Count ||
            p.RuntimeBaselines.Any(x => !p.Units.Any(u => StringComparer.Ordinal.Equals(u.UnitId, x.UnitId)) ||
                !IsSafeId(x.BaselineVersion) || !IsSafeId(x.Provenance) || string.IsNullOrWhiteSpace(x.InitialState)))
            issues.Add("runtime-baseline-scope-invalid");
    }

    private static void ValidateEvents(TargetStationProvisioningPackage p, ICollection<string> issues)
    {
        string[] allowed = ["START", "NSD", "ESD", "OH"];
        if (p.Events.Any(x => !allowed.Contains(x.EventType, StringComparer.Ordinal) || !IsSafeId(x.EventId) ||
            !StringComparer.Ordinal.Equals(x.StationId, p.StationId) || !p.Units.Any(u => u.UnitId == x.UnitId) ||
            !p.ShiftProfiles.Any(s => s.Profile.ShiftProfileId == x.CreatedByShiftProfileId) ||
            x.EventDate is < 10000101 or > 99991231 || x.EventTime is < 0 or > 1439 ||
            ((x.EventDateTime % 1440) + 1440) % 1440 != x.EventTime || x.RowVersion < 1))
            issues.Add("event-scope-or-type-invalid");
        if (p.Events.Select(x => $"{x.UnitId}|{x.EventDateTime}").Distinct(StringComparer.Ordinal).Count() != p.Events.Count)
            issues.Add("duplicate-active-event-timestamp");
    }

    private static void ValidateEsd(TargetStationProvisioningPackage p, ICollection<string> issues)
    {
        if (!decimal.TryParse(p.EsdAdjustmentCanonical, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out decimal value) || value < 0 ||
            !StringComparer.Ordinal.Equals(value.ToString("G29", CultureInfo.InvariantCulture), p.EsdAdjustmentCanonical))
            issues.Add("esd-value-invalid");
    }

    private static void ValidateSnapshotsAndLocks(TargetStationProvisioningPackage p, ICollection<string> issues)
    {
        foreach (TargetFinalizedSnapshotProvisioningRecord s in p.FinalizedSnapshots)
        {
            if (!IsSafeId(s.SnapshotId) || !StringComparer.Ordinal.Equals(s.StationId, p.StationId) ||
                s.PeriodStartMinute >= s.PeriodEndMinute || s.SnapshotSequence < 1 ||
                (s.SnapshotSequence == 1 && s.SupersedesSnapshotId is not null) ||
                (s.SnapshotSequence > 1 && !IsSafeId(s.SupersedesSnapshotId)) || string.IsNullOrWhiteSpace(s.CanonicalJson) ||
                s.CanonicalPayloadLength < 0 || s.FinalizedAt.Offset != TimeSpan.Zero)
                issues.Add("snapshot-invalid");
        }
        var snapshotIds = p.FinalizedSnapshots.Select(x => x.SnapshotId).ToHashSet(StringComparer.Ordinal);
        if (snapshotIds.Count != p.FinalizedSnapshots.Count) issues.Add("duplicate-snapshot");
        foreach (TargetFinalizedLockProvisioningRecord l in p.FinalizedLocks)
            if (!StringComparer.Ordinal.Equals(l.StationId, p.StationId) || l.PeriodStartMinute >= l.PeriodEndMinute ||
                !snapshotIds.Contains(l.EffectiveSnapshotId) || l.Revision < 1 || !IsSafeId(l.FinalizationId) ||
                l.FinalizedAt.Offset != TimeSpan.Zero || !IsSafeId(l.ActorIdentity)) issues.Add("finalized-lock-invalid");
    }

    private static void ValidateApprovals(TargetStationProvisioningPackage p, ICollection<string> issues)
    {
        foreach (string value in new[] { p.ManagementApproverReference, p.DataOwnerReference, p.SecurityReviewerReference })
            if (!IsSafeId(value)) issues.Add("approval-reference-invalid");
    }

    private static bool IsSafeId(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 160 &&
        value.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' or ':' or '@');

    private static string PersonnelFingerprint(string value) => Fingerprint(value.Trim().ToUpperInvariant());

    internal static string CredentialFingerprint(ShiftProfileCredentialRecord value) => Fingerprint(
        $"{value.ShiftProfileId}|{value.CredentialVersion}|{value.KdfAlgorithm}|{value.KdfParameters}|" +
        $"{Convert.ToHexString(SHA256.HashData(value.Salt))}|{Convert.ToHexString(SHA256.HashData(value.PasswordVerifier))}|{value.IsCurrent}");

    internal static string CredentialFingerprint(ManagementCredentialRecord value) => Fingerprint(
        $"singleton-1|{value.CredentialVersion}|{value.KdfAlgorithm}|{value.KdfParameters}|" +
        $"{Convert.ToHexString(SHA256.HashData(value.Salt))}|{Convert.ToHexString(SHA256.HashData(value.PasswordVerifier))}|{value.IsCurrent}|{value.IsActive}");

    internal static string Fingerprint(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

}
