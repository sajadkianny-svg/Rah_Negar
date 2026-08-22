using Rah_Negar.Foundation.Application.Integration;
using Rah_Negar.Foundation.Time;

namespace Rah_Negar.Foundation.Application.Pilot;

public sealed record ExportPilotRequest(
    PilotExecutionContext Context,
    PilotExecutionPermit? Permit,
    string SnapshotId,
    string ExportFormat);

public sealed record LegacyExportPilotObservation(
    bool Readable,
    string ArtifactFingerprint,
    string ResultCategory);

public sealed record TargetExportPilotObservation(
    bool Valid,
    string ArtifactFingerprint,
    bool SnapshotImmutable,
    bool ReadOnly,
    bool MutationAttempted,
    string ResultCategory);

public interface ILegacyExportPilotObserver
{
    Task<LegacyExportPilotObservation> ObserveAuthoritativeAsync(
        ExportPilotRequest request,
        CancellationToken cancellationToken = default);
}

public interface ITargetExportPilotObserver
{
    Task<TargetExportPilotObservation> ValidateReadOnlyAsync(
        ExportPilotRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ExportPilotService
{
    private readonly IClock _clock;
    private readonly ILegacyExportPilotObserver _legacy;
    private readonly ITargetExportPilotObserver _target;

    public ExportPilotService(IClock clock, ILegacyExportPilotObserver legacy,
        ITargetExportPilotObserver target)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _legacy = legacy ?? throw new ArgumentNullException(nameof(legacy));
        _target = target ?? throw new ArgumentNullException(nameof(target));
    }

    public async Task<PilotWorkflowResult<LegacyExportPilotObservation, TargetExportPilotObservation>>
        ObserveAsync(ExportPilotRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        IReadOnlyList<string> permitIssues = PilotPermitValidator.Validate(
            request.Permit, request.Context, PilotFeature.ExportPilot, _clock.UtcNow.ToUniversalTime());
        if (permitIssues.Count > 0) return Blocked(permitIssues);
        if (string.IsNullOrWhiteSpace(request.SnapshotId) || string.IsNullOrWhiteSpace(request.ExportFormat))
            return Blocked(["export-scope-required"]);
        LegacyExportPilotObservation? legacy = null;
        TargetExportPilotObservation? target = null;
        try
        {
            legacy = await _legacy.ObserveAuthoritativeAsync(request, cancellationToken).ConfigureAwait(false);
            target = await _target.ValidateReadOnlyAsync(request, cancellationToken).ConfigureAwait(false);
            if (!legacy.Readable || string.IsNullOrWhiteSpace(legacy.ArtifactFingerprint) ||
                !target.Valid || !target.SnapshotImmutable || !target.ReadOnly || target.MutationAttempted ||
                string.IsNullOrWhiteSpace(target.ArtifactFingerprint))
                return new(IntegrationControlDecision.Blocked, legacy, target, null,
                    ["export-read-only-invariant-failed"]);
            bool match = StringComparer.Ordinal.Equals(
                legacy.ArtifactFingerprint, target.ArtifactFingerprint);
            PilotEvidenceRecord evidence = PilotEvidenceFactory.Create(request.Context,
                PilotFeature.ExportPilot, legacy.ArtifactFingerprint, target.ArtifactFingerprint,
                match ? ShadowDifferenceSeverity.None : ShadowDifferenceSeverity.Warning,
                match ? "Export observations match." : "Export observations differ.",
                _clock.UtcNow.ToUniversalTime());
            return new(IntegrationControlDecision.Allowed, legacy, target, evidence, Array.Empty<string>());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            return new(IntegrationControlDecision.Blocked, legacy, target, null,
                ["export-pilot-observation-failed"]);
        }

        PilotWorkflowResult<LegacyExportPilotObservation, TargetExportPilotObservation>
            Blocked(IReadOnlyList<string> reasons) =>
            new(IntegrationControlDecision.Blocked, null, null, null, reasons);
    }
}
