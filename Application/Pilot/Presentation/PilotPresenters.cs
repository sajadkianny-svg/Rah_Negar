using System.Collections.ObjectModel;
using Rah_Negar.Foundation.Application.Integration;
using Rah_Negar.Foundation.Application.Pilot.Hosting;

namespace Rah_Negar.Foundation.Application.Pilot.Presentation;

public sealed class AuthenticationPilotPresenter : PilotResultPresenterBase
{
    public override PilotFeature Feature => PilotFeature.AuthenticationPilot;
    protected override string Title => "Authentication pilot";
}

public sealed class ReportingPilotPresenter : PilotResultPresenterBase
{
    public override PilotFeature Feature => PilotFeature.ReportingPilot;
    protected override string Title => "Reporting pilot";
}

public sealed class RuntimeEventPilotPresenter : PilotResultPresenterBase
{
    public override PilotFeature Feature => PilotFeature.RuntimeEventPilot;
    protected override string Title => "Runtime and Event pilot";
}

public sealed class ProtectedSettingsPilotPresenter : PilotResultPresenterBase
{
    public override PilotFeature Feature => PilotFeature.ProtectedSettingsPilot;
    protected override string Title => "Protected settings pilot";
}

public sealed class ExportPilotPresenter : PilotResultPresenterBase
{
    public override PilotFeature Feature => PilotFeature.ExportPilot;
    protected override string Title => "Export pilot";
}

public abstract class PilotResultPresenterBase : IPilotResultPresenter
{
    public abstract PilotFeature Feature { get; }
    protected abstract string Title { get; }

    public PilotFeatureViewState Present(PilotExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Feature != Feature)
            throw new ArgumentException("The pilot result does not match this presenter.", nameof(result));

        PilotUiViewStatus status = PilotUiSafeFeedback.MapStatus(result.Status);
        ShadowDifferenceSeverity severity = Enum.IsDefined(result.Severity)
            ? result.Severity
            : ShadowDifferenceSeverity.Failed;
        string? evidence = PilotUiSafeFeedback.SafeEvidenceReference(result.EvidenceId);
        var warnings = new List<string>(PilotUiSafeFeedback.Warnings(status, severity));
        if (!string.IsNullOrWhiteSpace(result.EvidenceId) && evidence is null)
            warnings.Add("The evidence reference was withheld because it is not safe for display.");

        return new(
            PilotUiSafeFeedback.SafeIdentifier(result.PilotId, "pilot-unavailable"),
            Feature,
            Title,
            status,
            PilotUiSafeFeedback.Description(Title, status),
            severity,
            PilotUiSafeFeedback.ComparisonSummary(status),
            PilotUiSafeFeedback.EvidenceState(status, evidence),
            evidence,
            PilotUiSafeFeedback.SafeTimestamp(result.StartedAtUtc, result.CompletedAtUtc),
            warnings,
            PilotUiSafeFeedback.BlockedReasons(result.BlockedReasons),
            PilotUiSafeFeedback.SafeIdentifier(result.CorrelationId, "correlation-unavailable"));
    }
}

public sealed class PilotPresentationCoordinator
{
    private readonly IReadOnlyDictionary<PilotFeature, IPilotResultPresenter> _presenters;

    public PilotPresentationCoordinator(IEnumerable<IPilotResultPresenter> presenters)
    {
        ArgumentNullException.ThrowIfNull(presenters);
        IPilotResultPresenter[] supplied = presenters.ToArray();
        if (supplied.Any(presenter => presenter is null))
            throw new ArgumentException("Pilot presenters cannot contain null.", nameof(presenters));
        if (supplied.Any(presenter => !Enum.IsDefined(presenter.Feature)))
            throw new ArgumentException("Pilot presenters must declare a known feature.", nameof(presenters));
        if (supplied.GroupBy(presenter => presenter.Feature).Any(group => group.Count() != 1))
            throw new ArgumentException("Each pilot feature may have only one presenter.", nameof(presenters));
        _presenters = new ReadOnlyDictionary<PilotFeature, IPilotResultPresenter>(
            supplied.ToDictionary(presenter => presenter.Feature));
    }

    public bool ExecutesPilotWorkflows => false;
    public bool RoutesPilotFeatures => false;
    public bool ActivatesPilotFeatures => false;
    public bool ReadsExternalState => false;

    public PilotFeatureViewState Present(PilotExecutionResult? result)
    {
        if (result is null)
            return FailureState(null, null, null, DateTimeOffset.UnixEpoch);
        if (!Enum.IsDefined(result.Feature) || !_presenters.TryGetValue(result.Feature, out var presenter))
            return FailureState(result.PilotId, result.CorrelationId, result.EvidenceId,
                PilotUiSafeFeedback.SafeTimestamp(result.StartedAtUtc, result.CompletedAtUtc));
        try
        {
            return presenter.Present(result);
        }
        catch
        {
            return FailureState(result.PilotId, result.CorrelationId, result.EvidenceId,
                PilotUiSafeFeedback.SafeTimestamp(result.StartedAtUtc, result.CompletedAtUtc));
        }
    }

    public PilotFeatureViewState CreateLoading(
        PilotFeature feature,
        string pilotId,
        string correlationId,
        DateTimeOffset timestampUtc)
    {
        if (!Enum.IsDefined(feature) || !_presenters.TryGetValue(feature, out var presenter))
            return FailureState(pilotId, correlationId, null,
                timestampUtc.Offset == TimeSpan.Zero ? timestampUtc : DateTimeOffset.UnixEpoch);
        return new(PilotUiSafeFeedback.SafeIdentifier(pilotId, "pilot-unavailable"), feature,
            PilotUiSafeFeedback.Title(presenter), PilotUiViewStatus.Loading,
            "Pilot evidence is loading. Legacy remains authoritative.",
            ShadowDifferenceSeverity.None, "No comparison result is available yet.",
            PilotEvidenceState.Incomplete, null,
            timestampUtc.Offset == TimeSpan.Zero ? timestampUtc : DateTimeOffset.UnixEpoch,
            Array.Empty<string>(), Array.Empty<string>(),
            PilotUiSafeFeedback.SafeIdentifier(correlationId, "correlation-unavailable"));
    }

    public PilotDashboardState CreateDashboard(
        PilotFeatureViewState featureState,
        bool pilotSessionActive,
        bool rollbackAvailable)
    {
        ArgumentNullException.ThrowIfNull(featureState);
        return new(pilotSessionActive ? featureState.PilotId : null, featureState.Feature,
            featureState.Status, featureState.ComparisonSummary,
            featureState.EvidenceState == PilotEvidenceState.Complete,
            rollbackAvailable, featureState);
    }

    private static PilotFeatureViewState FailureState(
        string? pilotId,
        string? correlationId,
        string? evidenceId,
        DateTimeOffset timestampUtc)
    {
        string? safeEvidence = PilotUiSafeFeedback.SafeEvidenceReference(evidenceId);
        return new(PilotUiSafeFeedback.SafeIdentifier(pilotId, "pilot-unavailable"), null,
            "Pilot result", PilotUiViewStatus.Failed,
            "The pilot result cannot be displayed. Legacy remains authoritative.",
            ShadowDifferenceSeverity.Failed,
            "Pilot comparison is unavailable; legacy remains authoritative.",
            safeEvidence is null ? PilotEvidenceState.Blocked : PilotEvidenceState.Complete,
            safeEvidence, timestampUtc.Offset == TimeSpan.Zero ? timestampUtc : DateTimeOffset.UnixEpoch,
            ["The pilot presentation is unavailable."], Array.Empty<string>(),
            PilotUiSafeFeedback.SafeIdentifier(correlationId, "correlation-unavailable"));
    }
}

public static class PilotUiSafeFeedback
{
    private const int MaximumIdentifierLength = 128;

    private static readonly IReadOnlyDictionary<string, string> SafeBlockedReasonMap =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pilot-context-required"] = "Pilot context is required.",
            ["pilot-permit-required"] = "A valid pilot permit is required.",
            ["pilot-permit-feature-mismatch"] = "The pilot permit does not cover this feature.",
            ["pilot-workflow-not-configured"] = "The selected pilot workflow is unavailable.",
            ["pilot-workflow-input-required"] = "Pilot input is incomplete.",
            ["pilot-workflow-input-type-mismatch"] = "Pilot input does not match the selected feature.",
            ["pilot-expired"] = "The pilot context has expired.",
            ["settings-mutation-prohibited"] = "Settings changes are prohibited in pilot observation.",
            ["settings-provisioning-prohibited"] = "Settings provisioning is prohibited in pilot observation.",
            ["esd-cutover-prohibited"] = "ESD authority cutover is prohibited in pilot observation.",
            ["snapshot-read-only-invariant-failed"] = "The snapshot could not be observed safely.",
            ["pilot-workflow-failed"] = "The pilot workflow result is unavailable."
        });

    public static PilotUiViewStatus MapStatus(PilotExecutionStatus status) => status switch
    {
        PilotExecutionStatus.Completed => PilotUiViewStatus.Completed,
        PilotExecutionStatus.CompletedWithDifference => PilotUiViewStatus.DifferenceDetected,
        PilotExecutionStatus.Blocked => PilotUiViewStatus.Blocked,
        PilotExecutionStatus.TargetFailed or PilotExecutionStatus.Failed => PilotUiViewStatus.Failed,
        _ => PilotUiViewStatus.Failed
    };

    public static string Description(string title, PilotUiViewStatus status) => status switch
    {
        PilotUiViewStatus.Loading => $"{title} evidence is loading. Legacy remains authoritative.",
        PilotUiViewStatus.Completed => $"{title} observation completed. Legacy remains authoritative.",
        PilotUiViewStatus.DifferenceDetected =>
            $"{title} recorded a difference for human review. Legacy remains authoritative.",
        PilotUiViewStatus.Blocked => $"{title} result was blocked. Legacy remains authoritative.",
        _ => $"{title} result is unavailable. Legacy remains authoritative."
    };

    public static string ComparisonSummary(PilotUiViewStatus status) => status switch
    {
        PilotUiViewStatus.Loading => "No comparison result is available yet.",
        PilotUiViewStatus.Completed => "Legacy and target observations matched.",
        PilotUiViewStatus.DifferenceDetected =>
            "A legacy and target difference was recorded for human review.",
        PilotUiViewStatus.Blocked => "No comparison result is available because the pilot result was blocked.",
        _ => "Pilot comparison is unavailable; legacy remains authoritative."
    };

    public static PilotEvidenceState EvidenceState(PilotUiViewStatus status, string? evidence) =>
        evidence is not null ? PilotEvidenceState.Complete :
        status is PilotUiViewStatus.Blocked or PilotUiViewStatus.Failed
            ? PilotEvidenceState.Blocked
            : PilotEvidenceState.Incomplete;

    public static IReadOnlyList<string> Warnings(
        PilotUiViewStatus status,
        ShadowDifferenceSeverity severity)
    {
        var warnings = new List<string>();
        if (status == PilotUiViewStatus.Failed)
            warnings.Add("The target observation is unavailable; legacy remains authoritative.");
        string? severityWarning = severity switch
        {
            ShadowDifferenceSeverity.Informational => "An informational comparison difference is available.",
            ShadowDifferenceSeverity.Warning => "A comparison difference requires review.",
            ShadowDifferenceSeverity.Critical => "A critical comparison difference requires manual review.",
            ShadowDifferenceSeverity.Failed => "Comparison evidence is unavailable.",
            _ => null
        };
        if (severityWarning is not null) warnings.Add(severityWarning);
        return warnings.AsReadOnly();
    }

    public static IReadOnlyList<string> BlockedReasons(IEnumerable<string>? reasons)
    {
        if (reasons is null) return Array.Empty<string>();
        var messages = new List<string>();
        bool unknown = false;
        foreach (string? reason in reasons)
        {
            if (reason is not null && SafeBlockedReasonMap.TryGetValue(reason, out string? message))
                messages.Add(message);
            else
                unknown = true;
        }
        if (unknown) messages.Add("The pilot result was blocked by a safety rule.");
        return new ReadOnlyCollection<string>(messages.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray());
    }

    public static string SafeIdentifier(string? value, string fallback) => IsSafeIdentifier(value)
        ? value!
        : fallback;

    public static string? SafeEvidenceReference(string? value) => IsSafeIdentifier(value) ? value : null;

    public static DateTimeOffset SafeTimestamp(DateTimeOffset startedAtUtc, DateTimeOffset completedAtUtc) =>
        startedAtUtc.Offset == TimeSpan.Zero && completedAtUtc.Offset == TimeSpan.Zero &&
        completedAtUtc >= startedAtUtc ? completedAtUtc : DateTimeOffset.UnixEpoch;

    internal static string Title(IPilotResultPresenter presenter) => presenter.Feature switch
    {
        PilotFeature.AuthenticationPilot => "Authentication pilot",
        PilotFeature.ReportingPilot => "Reporting pilot",
        PilotFeature.RuntimeEventPilot => "Runtime and Event pilot",
        PilotFeature.ProtectedSettingsPilot => "Protected settings pilot",
        PilotFeature.ExportPilot => "Export pilot",
        _ => "Pilot result"
    };

    private static bool IsSafeIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= MaximumIdentifierLength &&
        value.All(character => char.IsAsciiLetterOrDigit(character) ||
            character is '-' or '_' or '.' or ':');
}
