namespace Rah_Negar.Foundation.Application.UI.Settings;

public enum ProtectedSettingsViewStatus
{
    Idle,
    Loading,
    Ready,
    ManagementPrompt,
    SupportInformation,
    SupportCodePrompt,
    Completed,
    Denied,
    SessionExpired,
    InvalidAuthorization,
    ExecutionFailed,
    LegacyFallback
}

public sealed record ProtectedSettingsViewState
{
    public ProtectedSettingsViewStatus Status { get; init; }
    public SecureOperationPresentationResult? Result { get; init; }
    public ProtectedSettingsSnapshot? Settings { get; init; }
    public string Feedback { get; init; } = string.Empty;
    public string? CorrelationId { get; init; }
    public string? DeviceId { get; init; }
    public string? RequestInformation { get; init; }
    public bool OneTimeCodePromptVisible { get; init; }
    public AuthorizationFailureKind AuthorizationFailure { get; init; }

    public static ProtectedSettingsViewState Loading() => new() { Status = ProtectedSettingsViewStatus.Loading };
}
