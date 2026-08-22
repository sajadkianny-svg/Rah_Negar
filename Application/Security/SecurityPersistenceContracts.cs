namespace Rah_Negar.Foundation.Application.Security;

/// <summary>Infrastructure-only verifier material; never a second identity or presentation model.</summary>
public sealed record ShiftProfileCredentialRecord(string ShiftProfileId, int CredentialVersion,
    string KdfAlgorithm, string KdfParameters, byte[] Salt, byte[] PasswordVerifier,
    bool IsCurrent, DateTimeOffset CreatedAtUtc, DateTimeOffset? RetiredAtUtc);

/// <summary>Infrastructure-only singleton management credential revision.</summary>
public sealed record ManagementCredentialRecord(int CredentialVersion, string KdfAlgorithm,
    string KdfParameters, byte[] Salt, byte[] PasswordVerifier, bool IsCurrent, bool IsActive,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, DateTimeOffset? RetiredAtUtc);

public sealed record DeviceIdentityRecord(string DeviceId, DateTimeOffset ProvisionedAtUtc, long Revision);

public static class PersonnelNumberNormalizer
{
    public static string Normalize(string personnelNo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personnelNo);
        return personnelNo.Trim().ToUpperInvariant();
    }
}

public interface IShiftProfileRepository
{
    Task<IReadOnlyList<ShiftProfile>> ReadActiveAsync(string stationId, CancellationToken cancellationToken = default);
    Task<ShiftProfile?> FindByPersonnelNoAsync(string stationId, string personnelNo, CancellationToken cancellationToken = default);
    Task CreateAsync(ShiftProfile profile, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(ShiftProfile profile, long expectedRevision, CancellationToken cancellationToken = default);
}

public interface IShiftProfileCredentialRepository
{
    Task<ShiftProfileCredentialRecord?> LoadCurrentAsync(string shiftProfileId, CancellationToken cancellationToken = default);
    Task<bool> ReplaceAsync(ShiftProfileCredentialRecord replacement, int? expectedCurrentVersion,
        CancellationToken cancellationToken = default);
}

public interface IManagementCredentialRepository
{
    Task<ManagementCredentialRecord?> LoadCurrentAsync(CancellationToken cancellationToken = default);
    Task<bool> ReplaceAsync(ManagementCredentialRecord replacement, int? expectedCurrentVersion,
        CancellationToken cancellationToken = default);
}

public interface IDeviceIdentityRepository : IDeviceIdentityProvider
{
    Task<DeviceIdentityRecord?> LoadAsync(CancellationToken cancellationToken = default);
    Task<bool> TryProvisionAsync(DeviceIdentityRecord identity, CancellationToken cancellationToken = default);
}
