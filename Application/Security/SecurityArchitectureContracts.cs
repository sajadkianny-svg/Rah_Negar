namespace Rah_Negar.Foundation.Application.Security;

/// <summary>The sole normal operational identity. PersonnelNo is the login name.</summary>
public sealed record ShiftProfile(
    string ShiftProfileId,
    string StationId,
    int ShiftNumber,
    string ShiftName,
    string SupervisorFirstName,
    string SupervisorLastName,
    string PersonnelNo,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long Revision);

/// <summary>
/// Internal one-to-one credential material for a ShiftProfile. This is not an independent
/// operational identity and is deliberately not a presentation contract.
/// </summary>
internal sealed record UserCredential(
    string ShiftProfileId,
    string PasswordVerifier,
    string PasswordAlgorithm,
    int CredentialVersion,
    bool IsEnabled,
    DateTimeOffset UpdatedAt);

/// <summary>The singleton, deployment-wide credential used only for protected management actions.</summary>
internal sealed record ManagementCredential(
    string PasswordVerifier,
    string PasswordAlgorithm,
    int CredentialVersion,
    bool IsEnabled,
    DateTimeOffset UpdatedAt);

public enum OperationalAction
{
    EnterData,
    EditUnfinalizedData,
    ViewReports,
    FinalizeReport,
    ManualBackup
}

public static class OperationalAuthorizationPolicy
{
    public static bool IsAuthorized(ShiftProfile profile, OperationalAction action) =>
        profile is not null && profile.IsActive && Enum.IsDefined(action);
}

public enum ProtectedAction
{
    EditShiftProfiles,
    ChangeProtectedSettings,
    BackupPolicy,
    Restore,
    Migration,
    ReopenFinalizedReport,
    SecurityConfiguration,
    IntegrityRepair,
    SensitiveRawImportExport,
    EmergencyRecovery,
    ChangeEsdAdjustment
}

public sealed record ManagementAuthorizationProof(
    string InitiatingShiftProfileId,
    ProtectedAction Action,
    string ActionScope,
    int CredentialVersion,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    string CorrelationId)
{
    public bool AppliesTo(string shiftProfileId, ProtectedAction action, string scope, DateTimeOffset now) =>
        StringComparer.Ordinal.Equals(InitiatingShiftProfileId, shiftProfileId) &&
        Action == action &&
        StringComparer.Ordinal.Equals(ActionScope, scope) &&
        IssuedAt <= now && now < ExpiresAt;
}

public enum SecurityAuthorizationType
{
    OperationalShiftProfile,
    ManagementCredential,
    ExternalVendorSupport
}

public sealed record SecurityAuditEvent(
    string InitiatingShiftProfileId,
    ProtectedAction Action,
    string Scope,
    SecurityAuthorizationType AuthorizationType,
    bool Succeeded,
    DateTimeOffset Timestamp,
    string CorrelationId,
    IReadOnlyDictionary<string, string> NonSecretValueMetadata);

/// <summary>UI-neutral source for future About/Support presentation.</summary>
public interface ISupportContactInformationProvider
{
    string? GetConfiguredSoftwareSupportMobileNumber();
}
