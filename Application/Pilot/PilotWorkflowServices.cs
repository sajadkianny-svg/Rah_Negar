using Rah_Negar.Foundation.Application.Integration;
using Rah_Negar.Foundation.Time;

namespace Rah_Negar.Foundation.Application.Pilot;

public sealed record PilotWorkflowResult<TLegacy, TTarget>(
    IntegrationControlDecision Decision,
    TLegacy? LegacyObservation,
    TTarget? TargetObservation,
    PilotEvidenceRecord? Evidence,
    IReadOnlyList<string> Reasons)
{
    public bool LegacyRemainsAuthoritative => true;
    public bool ProductionMutationAllowed => false;
}

public sealed record AuthenticationPilotRequest(
    PilotExecutionContext Context,
    PilotExecutionPermit? Permit,
    string ShiftProfileId);

public sealed record LegacyAuthenticationPilotObservation(
    bool Succeeded,
    string ResultFingerprint,
    string ResultCategory);

public sealed record ShiftProfileAuthenticationPilotObservation(
    bool Succeeded,
    string ShiftProfileId,
    string StationId,
    int CredentialVersion,
    string ResultFingerprint,
    string ResultCategory);

public interface ILegacyAuthenticationPilotObserver
{
    Task<LegacyAuthenticationPilotObservation> ObserveAuthoritativeAsync(
        AuthenticationPilotRequest request,
        CancellationToken cancellationToken = default);
}

public interface IShiftProfileAuthenticationPilotObserver
{
    Task<ShiftProfileAuthenticationPilotObservation> ObserveReadOnlyAsync(
        AuthenticationPilotRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class AuthenticationPilotService
{
    private readonly IClock _clock;
    private readonly ILegacyAuthenticationPilotObserver _legacy;
    private readonly IShiftProfileAuthenticationPilotObserver _target;

    public AuthenticationPilotService(
        IClock clock,
        ILegacyAuthenticationPilotObserver legacy,
        IShiftProfileAuthenticationPilotObserver target)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _legacy = legacy ?? throw new ArgumentNullException(nameof(legacy));
        _target = target ?? throw new ArgumentNullException(nameof(target));
    }

    public async Task<PilotWorkflowResult<LegacyAuthenticationPilotObservation,
        ShiftProfileAuthenticationPilotObservation>> ObserveAsync(
        AuthenticationPilotRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        IReadOnlyList<string> permitIssues = PilotPermitValidator.Validate(
            request.Permit, request.Context, PilotFeature.AuthenticationPilot,
            _clock.UtcNow.ToUniversalTime());
        if (permitIssues.Count > 0) return Blocked(permitIssues);
        if (string.IsNullOrWhiteSpace(request.ShiftProfileId) ||
            !request.Context.SelectedShiftProfileIds.Contains(request.ShiftProfileId, StringComparer.Ordinal))
            return Blocked(["shift-profile-outside-pilot-scope"]);

        LegacyAuthenticationPilotObservation? legacy = null;
        try
        {
            legacy = await _legacy.ObserveAuthoritativeAsync(request, cancellationToken).ConfigureAwait(false);
            ShiftProfileAuthenticationPilotObservation target =
                await _target.ObserveReadOnlyAsync(request, cancellationToken).ConfigureAwait(false);
            var reasons = new List<string>();
            if (string.IsNullOrWhiteSpace(legacy.ResultFingerprint) ||
                string.IsNullOrWhiteSpace(legacy.ResultCategory))
                reasons.Add("legacy-authentication-observation-invalid");
            if (string.IsNullOrWhiteSpace(target.ResultFingerprint) ||
                string.IsNullOrWhiteSpace(target.ResultCategory) || target.CredentialVersion <= 0 ||
                !StringComparer.Ordinal.Equals(target.ShiftProfileId, request.ShiftProfileId) ||
                !StringComparer.Ordinal.Equals(target.StationId, request.Context.StationId))
                reasons.Add("target-shift-profile-observation-invalid");
            if (reasons.Count > 0)
                return new(IntegrationControlDecision.Blocked, legacy, target, null, reasons.AsReadOnly());

            bool match = legacy.Succeeded == target.Succeeded;
            PilotEvidenceRecord evidence = PilotEvidenceFactory.Create(
                request.Context, PilotFeature.AuthenticationPilot, legacy.ResultFingerprint,
                target.ResultFingerprint, match ? ShadowDifferenceSeverity.None : ShadowDifferenceSeverity.Warning,
                match ? "Authentication observations match." : "Authentication observations differ.",
                _clock.UtcNow.ToUniversalTime());
            return new(IntegrationControlDecision.Allowed, legacy, target, evidence, Array.Empty<string>());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            return new(IntegrationControlDecision.Blocked, legacy, null, null,
                ["authentication-pilot-observation-failed"]);
        }

        PilotWorkflowResult<LegacyAuthenticationPilotObservation, ShiftProfileAuthenticationPilotObservation>
            Blocked(IReadOnlyList<string> reasons) =>
            new(IntegrationControlDecision.Blocked, null, null, null, reasons);
    }

    public bool ReplacesLegacySession => false;
    public bool RequiresSecondLoginScreen => false;
}

public sealed record ReportingPilotRequest(
    PilotExecutionContext Context,
    PilotExecutionPermit? Permit,
    string ReportScope,
    string SnapshotId);

public sealed record LegacyReportPilotObservation(
    bool Readable,
    string ResultFingerprint,
    string ResultCategory);

public sealed record TargetSnapshotPilotObservation(
    bool ReadSucceeded,
    string SnapshotId,
    string ResultFingerprint,
    bool FinalizedSnapshotImmutable,
    bool RecalculationAttempted,
    bool MutationAttempted,
    string ResultCategory);

public sealed record ExportArtifactPilotObservation(
    bool Valid,
    string ArtifactFingerprint,
    bool MutationAttempted,
    string ResultCategory);

public interface ILegacyReportPilotObserver
{
    Task<LegacyReportPilotObservation> ObserveAuthoritativeAsync(
        ReportingPilotRequest request,
        CancellationToken cancellationToken = default);
}

public interface ITargetSnapshotPilotObserver
{
    Task<TargetSnapshotPilotObservation> ObserveReadOnlyAsync(
        ReportingPilotRequest request,
        CancellationToken cancellationToken = default);
}

public interface IExportArtifactPilotValidator
{
    Task<ExportArtifactPilotObservation> ValidateReadOnlyAsync(
        ReportingPilotRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ReportingPilotService
{
    private readonly IClock _clock;
    private readonly ILegacyReportPilotObserver _legacy;
    private readonly ITargetSnapshotPilotObserver _target;
    private readonly IExportArtifactPilotValidator _export;

    public ReportingPilotService(IClock clock, ILegacyReportPilotObserver legacy,
        ITargetSnapshotPilotObserver target, IExportArtifactPilotValidator export)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _legacy = legacy ?? throw new ArgumentNullException(nameof(legacy));
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _export = export ?? throw new ArgumentNullException(nameof(export));
    }

    public async Task<PilotWorkflowResult<LegacyReportPilotObservation, TargetSnapshotPilotObservation>>
        ObserveAsync(ReportingPilotRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        IReadOnlyList<string> permitIssues = PilotPermitValidator.Validate(
            request.Permit, request.Context, PilotFeature.ReportingPilot, _clock.UtcNow.ToUniversalTime());
        if (permitIssues.Count > 0) return Blocked(permitIssues);
        if (string.IsNullOrWhiteSpace(request.ReportScope) || string.IsNullOrWhiteSpace(request.SnapshotId))
            return Blocked(["report-and-snapshot-scope-required"]);

        LegacyReportPilotObservation? legacy = null;
        TargetSnapshotPilotObservation? target = null;
        try
        {
            legacy = await _legacy.ObserveAuthoritativeAsync(request, cancellationToken).ConfigureAwait(false);
            target = await _target.ObserveReadOnlyAsync(request, cancellationToken).ConfigureAwait(false);
            ExportArtifactPilotObservation export =
                await _export.ValidateReadOnlyAsync(request, cancellationToken).ConfigureAwait(false);
            var reasons = new List<string>();
            if (!legacy.Readable || string.IsNullOrWhiteSpace(legacy.ResultFingerprint))
                reasons.Add("legacy-report-must-remain-readable");
            if (!target.ReadSucceeded || !target.FinalizedSnapshotImmutable ||
                target.RecalculationAttempted || target.MutationAttempted ||
                !StringComparer.Ordinal.Equals(target.SnapshotId, request.SnapshotId) ||
                string.IsNullOrWhiteSpace(target.ResultFingerprint))
                reasons.Add("snapshot-read-only-invariant-failed");
            if (!export.Valid || export.MutationAttempted || string.IsNullOrWhiteSpace(export.ArtifactFingerprint))
                reasons.Add("export-artifact-validation-failed");
            if (reasons.Count > 0)
                return new(IntegrationControlDecision.Blocked, legacy, target, null, reasons.AsReadOnly());
            bool match = StringComparer.Ordinal.Equals(legacy.ResultFingerprint, target.ResultFingerprint);
            PilotEvidenceRecord evidence = PilotEvidenceFactory.Create(request.Context,
                PilotFeature.ReportingPilot, legacy.ResultFingerprint, target.ResultFingerprint,
                match ? ShadowDifferenceSeverity.None : ShadowDifferenceSeverity.Warning,
                match ? "Report observations match." : "Report observations differ.",
                _clock.UtcNow.ToUniversalTime());
            return new(IntegrationControlDecision.Allowed, legacy, target, evidence, Array.Empty<string>());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            return new(IntegrationControlDecision.Blocked, legacy, target, null,
                ["reporting-pilot-observation-failed"]);
        }

        PilotWorkflowResult<LegacyReportPilotObservation, TargetSnapshotPilotObservation>
            Blocked(IReadOnlyList<string> reasons) =>
            new(IntegrationControlDecision.Blocked, null, null, null, reasons);
    }

    public bool LegacyDisplayRemainsAvailable => true;
}

public sealed record RuntimeEventPilotRequest(
    PilotExecutionContext Context,
    PilotExecutionPermit? Permit,
    string ProjectionScope);

public sealed record LegacyRuntimeEventPilotObservation(
    string RuntimeFingerprint,
    string EventFingerprint,
    string ResultCategory);

public sealed record TargetRuntimeEventPilotObservation(
    string RuntimeFingerprint,
    string EventFingerprint,
    bool ReadOnly,
    bool InsertAttempted,
    bool UpdateAttempted,
    bool DeleteAttempted,
    bool CacheRebuildAttempted,
    bool RecalculationAttempted,
    string ResultCategory);

public interface ILegacyRuntimeEventPilotObserver
{
    Task<LegacyRuntimeEventPilotObservation> ObserveAuthoritativeAsync(
        RuntimeEventPilotRequest request,
        CancellationToken cancellationToken = default);
}

public interface ITargetRuntimeEventPilotObserver
{
    Task<TargetRuntimeEventPilotObservation> ObserveReadOnlyAsync(
        RuntimeEventPilotRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class RuntimeEventPilotService
{
    private readonly IClock _clock;
    private readonly ILegacyRuntimeEventPilotObserver _legacy;
    private readonly ITargetRuntimeEventPilotObserver _target;

    public RuntimeEventPilotService(IClock clock, ILegacyRuntimeEventPilotObserver legacy,
        ITargetRuntimeEventPilotObserver target)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _legacy = legacy ?? throw new ArgumentNullException(nameof(legacy));
        _target = target ?? throw new ArgumentNullException(nameof(target));
    }

    public async Task<PilotWorkflowResult<LegacyRuntimeEventPilotObservation,
        TargetRuntimeEventPilotObservation>> ObserveAsync(
        RuntimeEventPilotRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        IReadOnlyList<string> permitIssues = PilotPermitValidator.Validate(
            request.Permit, request.Context, PilotFeature.RuntimeEventPilot,
            _clock.UtcNow.ToUniversalTime());
        if (permitIssues.Count > 0) return Blocked(permitIssues);
        if (string.IsNullOrWhiteSpace(request.ProjectionScope))
            return Blocked(["runtime-event-scope-required"]);
        LegacyRuntimeEventPilotObservation? legacy = null;
        TargetRuntimeEventPilotObservation? target = null;
        try
        {
            legacy = await _legacy.ObserveAuthoritativeAsync(request, cancellationToken).ConfigureAwait(false);
            target = await _target.ObserveReadOnlyAsync(request, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(legacy.RuntimeFingerprint) ||
                string.IsNullOrWhiteSpace(legacy.EventFingerprint) ||
                string.IsNullOrWhiteSpace(target.RuntimeFingerprint) ||
                string.IsNullOrWhiteSpace(target.EventFingerprint) || !target.ReadOnly ||
                target.InsertAttempted || target.UpdateAttempted || target.DeleteAttempted ||
                target.CacheRebuildAttempted || target.RecalculationAttempted)
                return new(IntegrationControlDecision.Blocked, legacy, target, null,
                    ["runtime-event-read-only-invariant-failed"]);
            string legacyFingerprint = $"{legacy.RuntimeFingerprint}:{legacy.EventFingerprint}";
            string targetFingerprint = $"{target.RuntimeFingerprint}:{target.EventFingerprint}";
            bool match = StringComparer.Ordinal.Equals(legacyFingerprint, targetFingerprint);
            PilotEvidenceRecord evidence = PilotEvidenceFactory.Create(request.Context,
                PilotFeature.RuntimeEventPilot, legacyFingerprint, targetFingerprint,
                match ? ShadowDifferenceSeverity.None : ShadowDifferenceSeverity.Warning,
                match ? "Runtime/Event observations match." : "Runtime/Event observations differ.",
                _clock.UtcNow.ToUniversalTime());
            return new(IntegrationControlDecision.Allowed, legacy, target, evidence, Array.Empty<string>());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            return new(IntegrationControlDecision.Blocked, legacy, target, null,
                ["runtime-event-pilot-observation-failed"]);
        }

        PilotWorkflowResult<LegacyRuntimeEventPilotObservation, TargetRuntimeEventPilotObservation>
            Blocked(IReadOnlyList<string> reasons) =>
            new(IntegrationControlDecision.Blocked, null, null, null, reasons);
    }
}

public sealed record ProtectedSettingsPilotRequest(
    PilotExecutionContext Context,
    PilotExecutionPermit? Permit,
    string SettingsScope,
    bool SettingsMutationRequested,
    bool TargetProvisioningRequested,
    bool EsdCutoverRequested);

public sealed record LegacySettingsPilotObservation(
    bool Readable,
    string ResultFingerprint,
    string ResultCategory);

public sealed record TargetProtectedSettingsPilotObservation(
    string DecisionFingerprint,
    bool MutationAttempted,
    bool TargetProvisioningAttempted,
    bool EsdCutoverAttempted,
    bool VendorAuthorizationConsumptionAttempted,
    bool ManagementCredentialExecutionAttempted,
    string ResultCategory);

public interface ILegacySettingsPilotObserver
{
    Task<LegacySettingsPilotObservation> ObserveAuthoritativeAsync(
        ProtectedSettingsPilotRequest request,
        CancellationToken cancellationToken = default);
}

public interface ITargetProtectedSettingsPilotObserver
{
    Task<TargetProtectedSettingsPilotObservation> EvaluateDecisionReadOnlyAsync(
        ProtectedSettingsPilotRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ProtectedSettingsPilotService
{
    private readonly IClock _clock;
    private readonly ILegacySettingsPilotObserver _legacy;
    private readonly ITargetProtectedSettingsPilotObserver _target;

    public ProtectedSettingsPilotService(IClock clock, ILegacySettingsPilotObserver legacy,
        ITargetProtectedSettingsPilotObserver target)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _legacy = legacy ?? throw new ArgumentNullException(nameof(legacy));
        _target = target ?? throw new ArgumentNullException(nameof(target));
    }

    public async Task<PilotWorkflowResult<LegacySettingsPilotObservation,
        TargetProtectedSettingsPilotObservation>> ObserveAsync(
        ProtectedSettingsPilotRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        IReadOnlyList<string> permitIssues = PilotPermitValidator.Validate(
            request.Permit, request.Context, PilotFeature.ProtectedSettingsPilot,
            _clock.UtcNow.ToUniversalTime());
        if (permitIssues.Count > 0) return Blocked(permitIssues);
        var prohibited = new List<string>();
        if (request.SettingsMutationRequested) prohibited.Add("settings-mutation-prohibited");
        if (request.TargetProvisioningRequested) prohibited.Add("settings-provisioning-prohibited");
        if (request.EsdCutoverRequested) prohibited.Add("esd-cutover-prohibited");
        if (string.IsNullOrWhiteSpace(request.SettingsScope)) prohibited.Add("settings-scope-required");
        if (prohibited.Count > 0) return Blocked(prohibited.AsReadOnly());
        LegacySettingsPilotObservation? legacy = null;
        TargetProtectedSettingsPilotObservation? target = null;
        try
        {
            legacy = await _legacy.ObserveAuthoritativeAsync(request, cancellationToken).ConfigureAwait(false);
            target = await _target.EvaluateDecisionReadOnlyAsync(request, cancellationToken).ConfigureAwait(false);
            if (!legacy.Readable || string.IsNullOrWhiteSpace(legacy.ResultFingerprint) ||
                string.IsNullOrWhiteSpace(target.DecisionFingerprint) || target.MutationAttempted ||
                target.TargetProvisioningAttempted || target.EsdCutoverAttempted ||
                target.VendorAuthorizationConsumptionAttempted ||
                target.ManagementCredentialExecutionAttempted)
                return new(IntegrationControlDecision.Blocked, legacy, target, null,
                    ["protected-settings-observation-invariant-failed"]);
            bool match = StringComparer.Ordinal.Equals(
                legacy.ResultFingerprint, target.DecisionFingerprint);
            PilotEvidenceRecord evidence = PilotEvidenceFactory.Create(request.Context,
                PilotFeature.ProtectedSettingsPilot, legacy.ResultFingerprint,
                target.DecisionFingerprint,
                match ? ShadowDifferenceSeverity.None : ShadowDifferenceSeverity.Informational,
                match ? "Settings observations match." : "Settings decision observation differs.",
                _clock.UtcNow.ToUniversalTime());
            return new(IntegrationControlDecision.Allowed, legacy, target, evidence, Array.Empty<string>());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            return new(IntegrationControlDecision.Blocked, legacy, target, null,
                ["protected-settings-pilot-observation-failed"]);
        }

        PilotWorkflowResult<LegacySettingsPilotObservation, TargetProtectedSettingsPilotObservation>
            Blocked(IReadOnlyList<string> reasons) =>
            new(IntegrationControlDecision.Blocked, null, null, null, reasons);
    }
}

internal static class PilotEvidenceFactory
{
    public static PilotEvidenceRecord Create(
        PilotExecutionContext context,
        PilotFeature feature,
        string legacyFingerprint,
        string targetFingerprint,
        ShadowDifferenceSeverity severity,
        string safeMessage,
        DateTimeOffset timestampUtc) => new(
            $"pilot-evidence:{context.PilotId}:{feature}:{context.CorrelationId}",
            context.PilotId, feature, timestampUtc, context.CorrelationId,
            legacyFingerprint, targetFingerprint, severity, safeMessage,
            PilotRollbackStatus.Available);
}
