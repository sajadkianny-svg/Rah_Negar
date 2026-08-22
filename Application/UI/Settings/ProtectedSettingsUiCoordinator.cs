namespace Rah_Negar.Foundation.Application.UI.Settings;

public sealed class ProtectedSettingsUiCoordinator
{
    public const string FeatureKey = "settings.protected.pilot";

    private readonly IProtectedSettingsPresenter _presenter;
    private readonly IProtectedSettingsSessionProvider _sessions;
    private readonly IProtectedSettingsReader _reader;
    private readonly IProtectedSettingsLegacyWorkflow _legacy;
    private readonly ISettingsPilotFeatureModeProvider _features;
    private readonly IProtectedSettingsAuthorizationGateway _authorization;
    private readonly IProtectedOperationAuditWriter _audit;
    private readonly Dictionary<string, PendingOperation> _pending = new(StringComparer.Ordinal);

    public ProtectedSettingsUiCoordinator(
        IProtectedSettingsPresenter presenter,
        IProtectedSettingsSessionProvider sessions,
        IProtectedSettingsReader reader,
        IProtectedSettingsLegacyWorkflow legacy,
        ISettingsPilotFeatureModeProvider features,
        IProtectedSettingsAuthorizationGateway authorization,
        IProtectedOperationAuditWriter audit)
    {
        _presenter = presenter;
        _sessions = sessions;
        _reader = reader;
        _legacy = legacy;
        _features = features;
        _authorization = authorization;
        _audit = audit;
    }

    public async Task ViewAsync(CancellationToken cancellationToken = default)
    {
        if (_features.GetMode(FeatureKey) == SettingsPilotFeatureMode.Legacy)
        {
            _presenter.Present(new() { Status = ProtectedSettingsViewStatus.LegacyFallback, Feedback = "Legacy settings remain authoritative." });
            await _legacy.ShowAsync(cancellationToken);
            return;
        }

        ProtectedSettingsSession? session = GetActiveSession();
        if (session is null)
        {
            PresentSessionExpired();
            return;
        }

        _presenter.Present(ProtectedSettingsViewState.Loading());
        ProtectedSettingsSnapshot snapshot = await _reader.ReadAsync(cancellationToken);
        _presenter.Present(new() { Status = ProtectedSettingsViewStatus.Ready, Settings = snapshot });
    }

    public Task RequestEsdAdjustmentChangeAsync(
        EsdAdjustmentChangeRequest request,
        Func<CancellationToken, Task> execute,
        CancellationToken cancellationToken = default)
    {
        if (request.ProposedHours < 0m)
            return PresentDeniedWithoutExecutionAsync(request, "ESD Adjustment cannot be negative.", cancellationToken);

        return RequestChangeAsync(request, execute, cancellationToken);
    }

    public Task RequestSecuritySettingChangeAsync(SecuritySettingChangeRequest request, Func<CancellationToken, Task> execute, CancellationToken cancellationToken = default) =>
        RequestChangeAsync(request, execute, cancellationToken);

    public Task RequestCredentialManagementAsync(CredentialManagementRequest request, Func<CancellationToken, Task> execute, CancellationToken cancellationToken = default) =>
        RequestChangeAsync(request, execute, cancellationToken);

    public Task SubmitManagementAuthorizationAsync(string correlationId, ReadOnlyMemory<char> credential, CancellationToken cancellationToken = default) =>
        ResumeAsync(correlationId, ResumeKind.Management, (id, ct) => _authorization.SubmitManagementAuthorizationAsync(id, credential, ct), cancellationToken);

    public Task SubmitSupportAuthorizationAsync(string correlationId, ReadOnlyMemory<char> oneTimeCode, CancellationToken cancellationToken = default) =>
        ResumeAsync(correlationId, ResumeKind.ExternalVendorSupport, (id, ct) => _authorization.SubmitSupportAuthorizationAsync(id, oneTimeCode, ct), cancellationToken);

    private async Task RequestChangeAsync(ProtectedSettingsChangeRequest request, Func<CancellationToken, Task> execute, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(execute);

        if (_features.GetMode(FeatureKey) == SettingsPilotFeatureMode.Legacy)
        {
            _presenter.Present(new() { Status = ProtectedSettingsViewStatus.LegacyFallback, Feedback = "Legacy settings remain authoritative." });
            await _legacy.ShowAsync(cancellationToken);
            return;
        }

        ProtectedSettingsSession? session = GetActiveSession();
        if (session is null)
        {
            PresentSessionExpired(request.CorrelationId);
            return;
        }

        ProtectedAuthorizationDecision decision;
        if (request is EsdAdjustmentChangeRequest esd && (esd.IsWizardInitialValue || esd.IsDefaultOrUnchanged))
            decision = new(SecureOperationPresentationResult.Authorized);
        else
            decision = await _authorization.AuthorizeAsync(session, request, cancellationToken);

        await HandleDecisionAsync(session, request, execute, decision, ResumeKind.Initial, cancellationToken);
    }

    private async Task ResumeAsync(string correlationId, ResumeKind resumeKind, Func<string, CancellationToken, Task<ProtectedAuthorizationDecision>> authorize, CancellationToken cancellationToken)
    {
        if (!_pending.TryGetValue(correlationId, out PendingOperation? pending))
        {
            PresentDecision(correlationId, new(SecureOperationPresentationResult.InvalidAuthorization, FailureKind: AuthorizationFailureKind.Expired));
            return;
        }

        ProtectedSettingsSession? session = GetActiveSession();
        if (session is null || session.ShiftProfileId != pending.Session.ShiftProfileId || session.StationId != pending.Session.StationId)
        {
            _pending.Remove(correlationId);
            PresentSessionExpired(correlationId);
            return;
        }

        ProtectedAuthorizationDecision decision = await authorize(correlationId, cancellationToken);
        await HandleDecisionAsync(session, pending.Request, pending.Execute, decision, resumeKind, cancellationToken);
    }

    private async Task HandleDecisionAsync(ProtectedSettingsSession session, ProtectedSettingsChangeRequest request, Func<CancellationToken, Task> execute, ProtectedAuthorizationDecision decision, ResumeKind resumeKind, CancellationToken cancellationToken)
    {
        bool postWizardEsd = request is EsdAdjustmentChangeRequest esd && !esd.IsWizardInitialValue && !esd.IsDefaultOrUnchanged;
        if (postWizardEsd && !IsValidEsdStage(decision.Result, resumeKind))
            decision = new(SecureOperationPresentationResult.Denied);

        string authorizationType = resumeKind switch
        {
            ResumeKind.Management => "ManagementCredential",
            ResumeKind.ExternalVendorSupport => "ExternalVendorSupport",
            _ => "OperationalShiftProfile"
        };
        await _audit.WriteAsync(new(request.CorrelationId, request.RequestedAt, session.ShiftProfileId, session.StationId, request.Operation, decision.Result, authorizationType), cancellationToken);

        if (decision.Result is SecureOperationPresentationResult.ManagementAuthorizationRequired or SecureOperationPresentationResult.SupportAuthorizationRequired)
            _pending[request.CorrelationId] = new(session, request, execute);
        else
            _pending.Remove(request.CorrelationId);

        if (decision.Result != SecureOperationPresentationResult.Authorized)
        {
            PresentDecision(request.CorrelationId, decision);
            return;
        }

        try
        {
            await execute(cancellationToken);
            _presenter.Present(new() { Status = ProtectedSettingsViewStatus.Completed, Result = SecureOperationPresentationResult.Authorized, CorrelationId = request.CorrelationId, Feedback = "Protected operation completed." });
        }
        catch
        {
            _presenter.Present(new() { Status = ProtectedSettingsViewStatus.ExecutionFailed, Result = SecureOperationPresentationResult.ExecutionFailed, CorrelationId = request.CorrelationId, Feedback = "The protected operation could not be completed." });
        }
    }

    private async Task PresentDeniedWithoutExecutionAsync(ProtectedSettingsChangeRequest request, string feedback, CancellationToken cancellationToken)
    {
        ProtectedSettingsSession? session = GetActiveSession();
        if (session is null)
        {
            PresentSessionExpired(request.CorrelationId);
            return;
        }

        await _audit.WriteAsync(new(request.CorrelationId, request.RequestedAt, session.ShiftProfileId, session.StationId, request.Operation, SecureOperationPresentationResult.Denied), cancellationToken);
        _presenter.Present(new() { Status = ProtectedSettingsViewStatus.Denied, Result = SecureOperationPresentationResult.Denied, CorrelationId = request.CorrelationId, Feedback = feedback });
    }

    private ProtectedSettingsSession? GetActiveSession()
    {
        ProtectedSettingsSession? session = _sessions.Current;
        return session is not null && session.ExpiresAt > DateTimeOffset.UtcNow ? session : null;
    }

    private void PresentSessionExpired(string? correlationId = null) => _presenter.Present(new()
    {
        Status = ProtectedSettingsViewStatus.SessionExpired,
        Result = SecureOperationPresentationResult.SessionExpired,
        CorrelationId = correlationId,
        Feedback = "The session is missing or expired. Sign in again."
    });

    private void PresentDecision(string correlationId, ProtectedAuthorizationDecision decision)
    {
        ProtectedSettingsViewStatus status = decision.Result switch
        {
            SecureOperationPresentationResult.ManagementAuthorizationRequired => ProtectedSettingsViewStatus.ManagementPrompt,
            SecureOperationPresentationResult.SupportAuthorizationRequired when string.IsNullOrWhiteSpace(decision.DeviceId) => ProtectedSettingsViewStatus.SupportCodePrompt,
            SecureOperationPresentationResult.SupportAuthorizationRequired => ProtectedSettingsViewStatus.SupportInformation,
            SecureOperationPresentationResult.InvalidAuthorization => ProtectedSettingsViewStatus.InvalidAuthorization,
            SecureOperationPresentationResult.SessionExpired => ProtectedSettingsViewStatus.SessionExpired,
            SecureOperationPresentationResult.ExecutionFailed => ProtectedSettingsViewStatus.ExecutionFailed,
            _ => ProtectedSettingsViewStatus.Denied
        };

        string feedback = decision.Result switch
        {
            SecureOperationPresentationResult.ManagementAuthorizationRequired => "Management authorization is required.",
            SecureOperationPresentationResult.SupportAuthorizationRequired => "Support authorization is required.",
            SecureOperationPresentationResult.InvalidAuthorization when decision.FailureKind == AuthorizationFailureKind.Expired => "Authorization expired.",
            SecureOperationPresentationResult.InvalidAuthorization when decision.FailureKind == AuthorizationFailureKind.Replayed => "Authorization was already used.",
            SecureOperationPresentationResult.InvalidAuthorization => "Authorization is invalid.",
            SecureOperationPresentationResult.SessionExpired => "The session is missing or expired. Sign in again.",
            SecureOperationPresentationResult.ExecutionFailed => "The protected operation could not be completed.",
            _ => "The protected operation was denied."
        };

        _presenter.Present(new()
        {
            Status = status,
            Result = decision.Result,
            CorrelationId = correlationId,
            DeviceId = decision.DeviceId,
            RequestInformation = decision.RequestInformation,
            OneTimeCodePromptVisible = decision.Result == SecureOperationPresentationResult.SupportAuthorizationRequired,
            AuthorizationFailure = decision.FailureKind,
            Feedback = feedback
        });
    }

    private static bool IsValidEsdStage(SecureOperationPresentationResult result, ResumeKind stage) =>
        (stage, result) switch
        {
            (ResumeKind.Initial, SecureOperationPresentationResult.ManagementAuthorizationRequired) => true,
            (ResumeKind.Management, SecureOperationPresentationResult.SupportAuthorizationRequired) => true,
            (ResumeKind.ExternalVendorSupport, SecureOperationPresentationResult.Authorized) => true,
            (_, SecureOperationPresentationResult.Denied or SecureOperationPresentationResult.InvalidAuthorization
                or SecureOperationPresentationResult.SessionExpired or SecureOperationPresentationResult.ExecutionFailed) => true,
            _ => false
        };

    private sealed record PendingOperation(ProtectedSettingsSession Session, ProtectedSettingsChangeRequest Request, Func<CancellationToken, Task> Execute);
    private enum ResumeKind { Initial, Management, ExternalVendorSupport }
}
