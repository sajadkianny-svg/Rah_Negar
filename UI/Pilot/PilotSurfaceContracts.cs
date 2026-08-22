using System.Collections.ObjectModel;
using Rah_Negar.Foundation.Application.Pilot.Presentation;

namespace Rah_Negar.UI.Pilot;

public interface IPilotDashboardRefreshTarget
{
    void ReplaceState(PilotDashboardState? state);
    void ClearState();
}

/// <summary>
/// Future navigation boundary only. Phase 8.5 supplies no implementation or menu registration.
/// </summary>
public interface IPilotNavigationBoundary
{
    Task OpenPilotDashboardAsync(
        PilotDashboardState state,
        CancellationToken cancellationToken = default);

    Task ClosePilotDashboardAsync(CancellationToken cancellationToken = default);

    Task ReturnToLegacyWorkflowAsync(CancellationToken cancellationToken = default);
}

public enum PilotLocalizedTextKey
{
    SurfaceTitle,
    PilotId,
    SelectedFeature,
    ExecutionState,
    ComparisonStatus,
    Severity,
    Evidence,
    Rollback,
    CorrelationId,
    Timestamp,
    Warnings,
    BlockedReasons,
    Unavailable
}

/// <summary>Future localization adapter contract; Phase 8.6 validates fixed safe fallbacks.</summary>
public interface IPilotLocalizedTextProvider
{
    string GetText(PilotLocalizedTextKey key);
}

public sealed record PilotAccessibilityRequirements(
    bool DpiScalingRequired,
    bool KeyboardNavigationRequired,
    bool AccessibleNamesRequired,
    bool ReadOnlyControlsFocusableRequired,
    bool ActivationControlsProhibited)
{
    public static PilotAccessibilityRequirements Default { get; } =
        new(true, true, true, true, true);
}

public sealed record PilotLayoutRequirements(
    int MinimumSupportedWidth,
    int MinimumSupportedHeight,
    bool DpiScalingRequired,
    bool AutoScrollRequired,
    bool ResponsiveLayoutRequired)
{
    public static PilotLayoutRequirements Default { get; } =
        new(720, 560, true, true, true);
}

/// <summary>
/// Safe future localization adapter. Missing, invalid, unsafe, or failing resources use fixed text.
/// </summary>
public sealed class PilotLocalizationBoundary
{
    private readonly IPilotLocalizedTextProvider? _provider;

    public PilotLocalizationBoundary(IPilotLocalizedTextProvider? provider = null)
    {
        _provider = provider;
    }

    public string GetText(PilotLocalizedTextKey key)
    {
        string fallback = Fallback(key);
        if (_provider is null) return fallback;
        try
        {
            return PilotSurfaceTextSanitizer.SafeText(_provider.GetText(key), fallback);
        }
        catch
        {
            return fallback;
        }
    }

    private static string Fallback(PilotLocalizedTextKey key) => key switch
    {
        PilotLocalizedTextKey.SurfaceTitle => "Pilot observation dashboard",
        PilotLocalizedTextKey.PilotId => "Current pilot ID",
        PilotLocalizedTextKey.SelectedFeature => "Selected feature",
        PilotLocalizedTextKey.ExecutionState => "Execution state",
        PilotLocalizedTextKey.ComparisonStatus => "Comparison status",
        PilotLocalizedTextKey.Severity => "Severity",
        PilotLocalizedTextKey.Evidence => "Evidence",
        PilotLocalizedTextKey.Rollback => "Rollback",
        PilotLocalizedTextKey.CorrelationId => "Correlation ID",
        PilotLocalizedTextKey.Timestamp => "Timestamp",
        PilotLocalizedTextKey.Warnings => "Warnings",
        PilotLocalizedTextKey.BlockedReasons => "Blocked reasons",
        _ => "Unavailable"
    };
}

public sealed class PilotSurfaceSnapshot
{
    public PilotSurfaceSnapshot(
        string pilotId,
        string selectedFeature,
        string executionState,
        string comparisonStatus,
        string severity,
        string evidenceSummary,
        string rollbackSummary,
        string correlationId,
        string timestamp,
        IEnumerable<string> warnings,
        IEnumerable<string> blockedReasons,
        bool usesSafeFallback)
    {
        PilotId = pilotId;
        SelectedFeature = selectedFeature;
        ExecutionState = executionState;
        ComparisonStatus = comparisonStatus;
        Severity = severity;
        EvidenceSummary = evidenceSummary;
        RollbackSummary = rollbackSummary;
        CorrelationId = correlationId;
        Timestamp = timestamp;
        Warnings = new ReadOnlyCollection<string>(warnings.ToArray());
        BlockedReasons = new ReadOnlyCollection<string>(blockedReasons.ToArray());
        UsesSafeFallback = usesSafeFallback;
    }

    public string PilotId { get; }
    public string SelectedFeature { get; }
    public string ExecutionState { get; }
    public string ComparisonStatus { get; }
    public string Severity { get; }
    public string EvidenceSummary { get; }
    public string RollbackSummary { get; }
    public string CorrelationId { get; }
    public string Timestamp { get; }
    public IReadOnlyList<string> Warnings { get; }
    public IReadOnlyList<string> BlockedReasons { get; }
    public bool UsesSafeFallback { get; }
}
