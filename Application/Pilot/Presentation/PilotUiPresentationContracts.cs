using System.Collections.ObjectModel;
using Rah_Negar.Foundation.Application.Integration;
using Rah_Negar.Foundation.Application.Pilot.Hosting;

namespace Rah_Negar.Foundation.Application.Pilot.Presentation;

public enum PilotUiViewStatus
{
    Loading,
    Completed,
    DifferenceDetected,
    Blocked,
    Failed
}

public sealed class PilotFeatureViewState
{
    public PilotFeatureViewState(
        string pilotId,
        PilotFeature? feature,
        string title,
        PilotUiViewStatus status,
        string safeDescription,
        ShadowDifferenceSeverity severity,
        string comparisonSummary,
        PilotEvidenceState evidenceState,
        string? evidenceReference,
        DateTimeOffset timestampUtc,
        IEnumerable<string> warnings,
        IEnumerable<string> blockedReasons,
        string correlationId)
    {
        ArgumentNullException.ThrowIfNull(warnings);
        ArgumentNullException.ThrowIfNull(blockedReasons);
        PilotId = pilotId;
        Feature = feature;
        Title = title;
        Status = status;
        SafeDescription = safeDescription;
        Severity = severity;
        ComparisonSummary = comparisonSummary;
        EvidenceState = evidenceState;
        EvidenceReference = evidenceReference;
        TimestampUtc = timestampUtc;
        Warnings = new ReadOnlyCollection<string>(warnings.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray());
        BlockedReasons = new ReadOnlyCollection<string>(blockedReasons.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray());
        CorrelationId = correlationId;
    }

    public string PilotId { get; }
    public PilotFeature? Feature { get; }
    public string Title { get; }
    public PilotUiViewStatus Status { get; }
    public string SafeDescription { get; }
    public ShadowDifferenceSeverity Severity { get; }
    public string ComparisonSummary { get; }
    public PilotEvidenceState EvidenceState { get; }
    public string? EvidenceReference { get; }
    public DateTimeOffset TimestampUtc { get; }
    public IReadOnlyList<string> Warnings { get; }
    public IReadOnlyList<string> BlockedReasons { get; }
    public string CorrelationId { get; }
    public bool LegacyAuthorityPreserved => true;
    public bool AllowsExecution => false;
    public bool AllowsRouting => false;
    public bool AllowsActivation => false;
}

public sealed class PilotDashboardState
{
    public PilotDashboardState(
        string? activePilotId,
        PilotFeature? selectedFeature,
        PilotUiViewStatus executionStatus,
        string comparisonSummary,
        bool evidenceAvailable,
        bool rollbackAvailable,
        PilotFeatureViewState featureState)
    {
        ActivePilotId = activePilotId;
        SelectedFeature = selectedFeature;
        ExecutionStatus = executionStatus;
        ComparisonSummary = comparisonSummary;
        EvidenceAvailable = evidenceAvailable;
        RollbackAvailable = rollbackAvailable;
        FeatureState = featureState ?? throw new ArgumentNullException(nameof(featureState));
    }

    public string? ActivePilotId { get; }
    public PilotFeature? SelectedFeature { get; }
    public PilotUiViewStatus ExecutionStatus { get; }
    public string ComparisonSummary { get; }
    public bool EvidenceAvailable { get; }
    public bool RollbackAvailable { get; }
    public PilotFeatureViewState FeatureState { get; }
    public bool CanActivateFeature => false;
    public bool CanSwitchAuthority => false;
}

public interface IPilotResultPresenter
{
    PilotFeature Feature { get; }
    PilotFeatureViewState Present(PilotExecutionResult result);
}

public enum PilotUiSurfaceKind
{
    ExistingFormConsumer,
    FuturePilotForm,
    EmbeddedPilotPanel
}

/// <summary>
/// Future WinForms adapter boundary. Phase 8.4 supplies no implementation or navigation registration.
/// </summary>
public interface IPilotWinFormsStateConsumer
{
    PilotUiSurfaceKind SurfaceKind { get; }
    Task RenderAsync(PilotDashboardState state, CancellationToken cancellationToken = default);
}

public static class PilotUiCapabilities
{
    public const string PilotView = "pilot.view";
    public const string EvidenceView = "evidence.view";
    public const string ComparisonView = "comparison.view";

    public static IReadOnlyList<string> All { get; } =
        Array.AsReadOnly([PilotView, EvidenceView, ComparisonView]);
}

public sealed record PilotUiCapabilityRequest(
    string CapabilityId,
    string PilotId,
    string CorrelationId);

public enum PilotUiCapabilityDecision
{
    Available,
    Unavailable,
    RequiresManualReview
}

/// <summary>
/// Capability extension point only. It is not a role model and has no implementation in Phase 8.4.
/// </summary>
public interface IPilotUiCapabilityBoundary
{
    ValueTask<PilotUiCapabilityDecision> EvaluateAsync(
        PilotUiCapabilityRequest request,
        CancellationToken cancellationToken = default);
}
