namespace Rah_Negar.Foundation.Application.UI.Settings;

public enum SettingsPilotFeatureMode
{
    Legacy,
    Pilot
}

public enum ProtectedSettingsOperation
{
    View,
    ChangeEsdAdjustment,
    ChangeSecuritySetting,
    ManageCredential
}

public enum SecureOperationPresentationResult
{
    Authorized,
    Denied,
    SessionExpired,
    ManagementAuthorizationRequired,
    SupportAuthorizationRequired,
    InvalidAuthorization,
    ExecutionFailed
}

public enum AuthorizationFailureKind
{
    None,
    Invalid,
    Expired,
    Replayed
}

public sealed record ProtectedSettingsSession(string ShiftProfileId, string StationId, DateTimeOffset ExpiresAt);

public sealed record ProtectedSettingsSnapshot(
    string StationId,
    decimal EsdAdjustmentHours,
    bool EsdAdjustmentEnabled,
    IReadOnlyDictionary<string, string> DisplaySettings);

public abstract record ProtectedSettingsChangeRequest(
    string CorrelationId,
    DateTimeOffset RequestedAt,
    ProtectedSettingsOperation Operation);

public sealed record EsdAdjustmentChangeRequest(
    string CorrelationId,
    DateTimeOffset RequestedAt,
    decimal CurrentHours,
    decimal ProposedHours,
    bool IsWizardInitialValue = false)
    : ProtectedSettingsChangeRequest(CorrelationId, RequestedAt, ProtectedSettingsOperation.ChangeEsdAdjustment)
{
    public bool IsDefaultOrUnchanged => ProposedHours == 0m && CurrentHours == 0m;
}

public sealed record SecuritySettingChangeRequest(
    string CorrelationId,
    DateTimeOffset RequestedAt,
    string SettingKey)
    : ProtectedSettingsChangeRequest(CorrelationId, RequestedAt, ProtectedSettingsOperation.ChangeSecuritySetting);

public sealed record CredentialManagementRequest(
    string CorrelationId,
    DateTimeOffset RequestedAt,
    string Action,
    string TargetIdentity)
    : ProtectedSettingsChangeRequest(CorrelationId, RequestedAt, ProtectedSettingsOperation.ManageCredential);

public sealed record ProtectedAuthorizationDecision(
    SecureOperationPresentationResult Result,
    string? DeviceId = null,
    string? RequestInformation = null,
    AuthorizationFailureKind FailureKind = AuthorizationFailureKind.None);

public sealed record ProtectedOperationAuditDecision(
    string CorrelationId,
    DateTimeOffset Timestamp,
    string InitiatingShiftProfileId,
    string StationId,
    ProtectedSettingsOperation Operation,
    SecureOperationPresentationResult Decision,
    string AuthorizationType = "OperationalShiftProfile");

public interface IProtectedSettingsPresenter
{
    void Present(ProtectedSettingsViewState state);
}

public interface IProtectedSettingsSessionProvider
{
    ProtectedSettingsSession? Current { get; }
}

public interface IProtectedSettingsReader
{
    Task<ProtectedSettingsSnapshot> ReadAsync(CancellationToken cancellationToken);
}

public interface IProtectedSettingsLegacyWorkflow
{
    Task ShowAsync(CancellationToken cancellationToken);
}

public interface ISettingsPilotFeatureModeProvider
{
    SettingsPilotFeatureMode GetMode(string featureKey);
}

public interface IProtectedSettingsAuthorizationGateway
{
    Task<ProtectedAuthorizationDecision> AuthorizeAsync(
        ProtectedSettingsSession session,
        ProtectedSettingsChangeRequest request,
        CancellationToken cancellationToken);

    Task<ProtectedAuthorizationDecision> SubmitManagementAuthorizationAsync(
        string correlationId,
        ReadOnlyMemory<char> credential,
        CancellationToken cancellationToken);

    Task<ProtectedAuthorizationDecision> SubmitSupportAuthorizationAsync(
        string correlationId,
        ReadOnlyMemory<char> oneTimeCode,
        CancellationToken cancellationToken);
}

public interface IProtectedOperationAuditWriter
{
    Task WriteAsync(ProtectedOperationAuditDecision decision, CancellationToken cancellationToken);
}
