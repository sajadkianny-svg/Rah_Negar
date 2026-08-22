using System.Collections.ObjectModel;
using Rah_Negar.Foundation.Application.Integration;

namespace Rah_Negar.Foundation.Application.Pilot;

public enum PilotPresentationStatus
{
    Ready,
    Match,
    Difference,
    Warning,
    Blocked,
    Expired,
    Closed
}

public sealed class PilotPresentationModel
{
    public PilotPresentationModel(
        string pilotId,
        PilotFeature feature,
        PilotPresentationStatus status,
        string safeSummary,
        IEnumerable<string> warnings,
        IEnumerable<string> blockedReasons,
        PilotEvidenceState evidenceState,
        string? evidenceId,
        string correlationId)
    {
        ArgumentNullException.ThrowIfNull(warnings);
        ArgumentNullException.ThrowIfNull(blockedReasons);
        PilotId = pilotId;
        Feature = feature;
        Status = status;
        SafeSummary = safeSummary;
        Warnings = new ReadOnlyCollection<string>(warnings.ToArray());
        BlockedReasons = new ReadOnlyCollection<string>(blockedReasons.ToArray());
        EvidenceState = evidenceState;
        EvidenceId = evidenceId;
        CorrelationId = correlationId;
    }

    public string PilotId { get; }
    public PilotFeature Feature { get; }
    public PilotPresentationStatus Status { get; }
    public string SafeSummary { get; }
    public IReadOnlyList<string> Warnings { get; }
    public IReadOnlyList<string> BlockedReasons { get; }
    public PilotEvidenceState EvidenceState { get; }
    public string? EvidenceId { get; }
    public string CorrelationId { get; }
    public bool LegacyRemainsAuthoritative => true;
    public bool OffersProductionActivation => false;
}

/// <summary>Future UI boundary only. Phase 8.2 supplies no WinForms implementation.</summary>
public interface IPilotPresentationSink
{
    Task PresentAsync(PilotPresentationModel model, CancellationToken cancellationToken = default);
}

public enum PilotMonitoringSignalKind
{
    AuthenticationDifference,
    ReportDifference,
    RuntimeEventDifference,
    SecurityFailure,
    PilotHealth
}

public sealed record PilotMonitoringSignal(
    PilotMonitoringSignalKind Kind,
    string PilotId,
    PilotFeature Feature,
    string CorrelationId,
    string EvidenceId,
    ShadowDifferenceSeverity Severity,
    string SafeCategory,
    DateTimeOffset ObservedAtUtc);

/// <summary>Monitoring extension point only. There is intentionally no telemetry provider.</summary>
public interface IPilotMonitoringHook
{
    Task ObserveAsync(PilotMonitoringSignal signal, CancellationToken cancellationToken = default);
}

public sealed record PilotRollbackRequest(
    PilotExecutionContext Context,
    bool DisablePilotRequested,
    bool ReturnToLegacyRequested,
    bool PreserveEvidenceRequested,
    bool ClosePilotSessionRequested,
    DateTimeOffset RequestedAtUtc);

public sealed record PilotRollbackPlan(
    IntegrationControlDecision Decision,
    string PilotId,
    string CorrelationId,
    PilotRollbackStatus Status,
    bool LegacyAuthorityRestored,
    bool EvidencePreserved,
    bool DestructiveActionAllowed,
    IReadOnlyList<string> Reasons);

/// <summary>Creates a non-destructive rollback plan; it does not change routing or persisted state.</summary>
public static class PilotRollbackCoordinator
{
    public static PilotRollbackPlan Evaluate(PilotRollbackRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reasons = new List<string>();
        if (request.RequestedAtUtc.Offset != TimeSpan.Zero) reasons.Add("rollback-time-must-be-utc");
        if (!request.DisablePilotRequested) reasons.Add("pilot-disable-required");
        if (!request.ReturnToLegacyRequested) reasons.Add("return-to-legacy-required");
        if (!request.PreserveEvidenceRequested) reasons.Add("pilot-evidence-preservation-required");
        if (!request.ClosePilotSessionRequested) reasons.Add("pilot-session-close-required");
        if (string.IsNullOrWhiteSpace(request.Context.RollbackReference))
            reasons.Add("rollback-reference-required");
        bool allowed = reasons.Count == 0;
        return new(allowed ? IntegrationControlDecision.Allowed : IntegrationControlDecision.Blocked,
            request.Context.PilotId, request.Context.CorrelationId,
            allowed ? PilotRollbackStatus.Closed : PilotRollbackStatus.Unavailable,
            allowed, allowed, false, reasons.AsReadOnly());
    }
}
