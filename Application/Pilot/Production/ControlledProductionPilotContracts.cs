using System.Collections.ObjectModel;
using Rah_Negar.Foundation.Application.Activation.Preparation;
using Rah_Negar.Foundation.Application.Pilot.Validation;

namespace Rah_Negar.Foundation.Application.Pilot.Production;

public enum ControlledProductionPilotScope
{
    RashtReadOnlyObservation,
    RamsarReadOnlyObservation,
    RashtAndRamsarReadOnlyObservation
}

public enum ControlledPilotSessionState
{
    Created,
    Approved,
    Started,
    Observing,
    Completed,
    Stopped,
    Failed,
    Disposed
}

public enum ControlledPilotOperationStatus
{
    Accepted,
    Blocked,
    Failed,
    Canceled,
    Disposed
}

public enum ControlledPilotObservationStatus
{
    Match,
    Difference,
    Unavailable,
    Failed
}

public enum ControlledPilotHealthStatus
{
    Healthy,
    AttentionRequired,
    Failed,
    Stopped
}

public enum ControlledPilotStopReason
{
    ValidationFailure,
    OperatorStop,
    EvidenceMismatch,
    SecurityConcern,
    RollbackRequested
}

public sealed class ControlledProductionPilotContext
{
    public ControlledProductionPilotContext(
        string pilotId,
        string releaseIdentifier,
        ControlledProductionPilotScope targetScope,
        IEnumerable<string> selectedOperators,
        IEnumerable<PilotValidationWorkflow> approvedFeatures,
        string activationPreparationReference,
        string rollbackReference,
        string monitoringReference,
        DateTimeOffset startWindowUtc,
        DateTimeOffset endWindowUtc)
    {
        ArgumentNullException.ThrowIfNull(selectedOperators);
        ArgumentNullException.ThrowIfNull(approvedFeatures);
        PilotId = pilotId;
        ReleaseIdentifier = releaseIdentifier;
        TargetScope = targetScope;
        SelectedOperators = new ReadOnlyCollection<string>(selectedOperators
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        ApprovedFeatures = new ReadOnlyCollection<PilotValidationWorkflow>(approvedFeatures
            .Distinct().Order().ToArray());
        ActivationPreparationReference = activationPreparationReference;
        RollbackReference = rollbackReference;
        MonitoringReference = monitoringReference;
        StartWindowUtc = startWindowUtc;
        EndWindowUtc = endWindowUtc;
    }

    public string PilotId { get; }
    public string ReleaseIdentifier { get; }
    public ControlledProductionPilotScope TargetScope { get; }
    public IReadOnlyList<string> SelectedOperators { get; }
    public IReadOnlyList<PilotValidationWorkflow> ApprovedFeatures { get; }
    public string ActivationPreparationReference { get; }
    public string RollbackReference { get; }
    public string MonitoringReference { get; }
    public DateTimeOffset StartWindowUtc { get; }
    public DateTimeOffset EndWindowUtc { get; }
    public bool AutomaticallyActivates => false;
    public bool DiscoversEnvironment => false;
    public bool FallsBackToProduction => false;
    public bool ChangesAuthority => false;
}

public sealed class ControlledPilotOperatorApproval
{
    public ControlledPilotOperatorApproval(
        string operatorReference,
        string approvalReference,
        DateTimeOffset approvedAtUtc,
        ControlledProductionPilotScope approvedScope)
    {
        OperatorReference = ControlledProductionPilotText.SafeIdentifier(operatorReference,
            "operator-unavailable");
        ApprovalReference = ControlledProductionPilotText.SafeIdentifier(approvalReference,
            "operator-approval-unavailable");
        ApprovedAtUtc = approvedAtUtc;
        ApprovedScope = approvedScope;
    }

    public string OperatorReference { get; }
    public string ApprovalReference { get; }
    public DateTimeOffset ApprovedAtUtc { get; }
    public ControlledProductionPilotScope ApprovedScope { get; }
    public bool AuthenticatesOperator => false;
    public bool ReplacesLogin => false;
    public bool ImplementsRbac => false;
    public bool CreatesPermission => false;
}

public sealed class ControlledPilotObservationResult
{
    public ControlledPilotObservationResult(
        PilotValidationWorkflow feature,
        ControlledPilotObservationStatus status,
        string resultFingerprint,
        string validationSummary,
        string differenceSummary,
        string evidenceReference,
        DateTimeOffset observedAtUtc)
    {
        Feature = feature;
        Status = status;
        ResultFingerprint = resultFingerprint;
        ValidationSummary = ControlledProductionPilotText.SafeIdentifier(validationSummary,
            "validation-summary-unavailable");
        DifferenceSummary = ControlledProductionPilotText.SafeIdentifier(differenceSummary,
            "difference-summary-unavailable");
        EvidenceReference = ControlledProductionPilotText.SafeIdentifier(evidenceReference,
            "observation-evidence-unavailable");
        ObservedAtUtc = observedAtUtc;
    }

    public PilotValidationWorkflow Feature { get; }
    public ControlledPilotObservationStatus Status { get; }
    public string ResultFingerprint { get; }
    public string ValidationSummary { get; }
    public string DifferenceSummary { get; }
    public string EvidenceReference { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public bool IsReadOnly => true;
    public bool MutatesProduction => false;
    public bool LegacyAuthorityPreserved => true;
}

public interface IControlledProductionPilotObserver
{
    PilotValidationWorkflow Feature { get; }

    ValueTask<ControlledPilotObservationResult?> ObserveAsync(
        ControlledProductionPilotContext context,
        string sessionId,
        CancellationToken cancellationToken = default);
}

public interface IControlledAuthenticationPilotObserver :
    IControlledProductionPilotObserver { }
public interface IControlledReportingPilotObserver :
    IControlledProductionPilotObserver { }
public interface IControlledRuntimeEventPilotObserver :
    IControlledProductionPilotObserver { }
public interface IControlledProtectedSettingsPilotObserver :
    IControlledProductionPilotObserver { }
public interface IControlledExportPilotObserver :
    IControlledProductionPilotObserver { }

public abstract class ImmutableControlledProductionPilotObserverBase :
    IControlledProductionPilotObserver
{
    private readonly ControlledPilotObservationResult _result;

    protected ImmutableControlledProductionPilotObserverBase(
        PilotValidationWorkflow feature,
        ControlledPilotObservationResult result)
    {
        Feature = feature;
        _result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public PilotValidationWorkflow Feature { get; }
    public bool ExecutesWorkflow => false;
    public bool AccessesDatabase => false;

    public ValueTask<ControlledPilotObservationResult?> ObserveAsync(
        ControlledProductionPilotContext context,
        string sessionId,
        CancellationToken cancellationToken = default) => cancellationToken.IsCancellationRequested
            ? ValueTask.FromResult<ControlledPilotObservationResult?>(null)
            : ValueTask.FromResult<ControlledPilotObservationResult?>(_result);
}

public sealed class ControlledAuthenticationPilotObserver(
    ControlledPilotObservationResult result) :
    ImmutableControlledProductionPilotObserverBase(PilotValidationWorkflow.Authentication, result),
    IControlledAuthenticationPilotObserver { }

public sealed class ControlledReportingPilotObserver(
    ControlledPilotObservationResult result) :
    ImmutableControlledProductionPilotObserverBase(PilotValidationWorkflow.Reporting, result),
    IControlledReportingPilotObserver { }

public sealed class ControlledRuntimeEventPilotObserver(
    ControlledPilotObservationResult result) :
    ImmutableControlledProductionPilotObserverBase(PilotValidationWorkflow.RuntimeEvent, result),
    IControlledRuntimeEventPilotObserver { }

public sealed class ControlledProtectedSettingsPilotObserver(
    ControlledPilotObservationResult result) :
    ImmutableControlledProductionPilotObserverBase(PilotValidationWorkflow.ProtectedSettings, result),
    IControlledProtectedSettingsPilotObserver { }

public sealed class ControlledExportPilotObserver(
    ControlledPilotObservationResult result) :
    ImmutableControlledProductionPilotObserverBase(PilotValidationWorkflow.Export, result),
    IControlledExportPilotObserver { }

public sealed class PilotMonitoringEvidence
{
    public PilotMonitoringEvidence(
        string pilotId,
        string sessionId,
        DateTimeOffset timestampUtc,
        ControlledPilotHealthStatus healthStatus,
        string validationSummary,
        string differenceSummary,
        RollbackEvidenceStatus rollbackStatus)
    {
        PilotId = pilotId;
        SessionId = sessionId;
        TimestampUtc = timestampUtc;
        HealthStatus = healthStatus;
        ValidationSummary = validationSummary;
        DifferenceSummary = differenceSummary;
        RollbackStatus = rollbackStatus;
    }

    public string PilotId { get; }
    public string SessionId { get; }
    public DateTimeOffset TimestampUtc { get; }
    public ControlledPilotHealthStatus HealthStatus { get; }
    public string ValidationSummary { get; }
    public string DifferenceSummary { get; }
    public RollbackEvidenceStatus RollbackStatus { get; }
    public bool ContainsSecrets => false;
    public bool ContainsCredentialMaterial => false;
    public bool ContainsRawLogs => false;
    public bool ContainsDatabaseContent => false;
    public bool ImplementsTelemetry => false;
}

public interface IPilotMonitoringEvidenceFactory
{
    PilotMonitoringEvidence Create(
        ControlledProductionPilotContext context,
        string sessionId,
        IReadOnlyList<ControlledPilotObservationResult> observations,
        RollbackVerificationResult rollback,
        DateTimeOffset observedAtUtc);
}

public sealed class DeterministicPilotMonitoringEvidenceFactory :
    IPilotMonitoringEvidenceFactory
{
    public PilotMonitoringEvidence Create(
        ControlledProductionPilotContext context,
        string sessionId,
        IReadOnlyList<ControlledPilotObservationResult> observations,
        RollbackVerificationResult rollback,
        DateTimeOffset observedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(rollback);
        bool difference = observations.Any(observation =>
            observation.Status == ControlledPilotObservationStatus.Difference);
        return new(context.PilotId, sessionId, observedAtUtc,
            difference ? ControlledPilotHealthStatus.AttentionRequired :
                ControlledPilotHealthStatus.Healthy,
            difference ? "validation-summary-difference" : "validation-summary-complete",
            difference ? "difference-summary-recorded" : "difference-summary-none",
            rollback.ValidationStatus);
    }
}

public sealed class PilotStopDecision
{
    public PilotStopDecision(
        string decisionId,
        string pilotId,
        string sessionId,
        ControlledPilotStopReason reason,
        string evidenceReference,
        DateTimeOffset decidedAtUtc)
    {
        DecisionId = ControlledProductionPilotText.SafeIdentifier(decisionId,
            "stop-decision-unavailable");
        PilotId = ControlledProductionPilotText.SafeIdentifier(pilotId,
            "pilot-unavailable");
        SessionId = ControlledProductionPilotText.SafeIdentifier(sessionId,
            "session-unavailable");
        Reason = reason;
        EvidenceReference = ControlledProductionPilotText.SafeIdentifier(evidenceReference,
            "stop-evidence-unavailable");
        DecidedAtUtc = decidedAtUtc;
    }

    public string DecisionId { get; }
    public string PilotId { get; }
    public string SessionId { get; }
    public ControlledPilotStopReason Reason { get; }
    public string EvidenceReference { get; }
    public DateTimeOffset DecidedAtUtc { get; }
    public bool ExecutesRollback => false;
    public bool PerformsDestructiveAction => false;
    public bool AutomaticallyStopsProduction => false;
}

public sealed class ControlledPilotEvidence
{
    internal ControlledPilotEvidence(
        string evidenceId,
        string pilotId,
        string sessionId,
        IEnumerable<ControlledPilotObservationResult> observations,
        PilotMonitoringEvidence monitoringEvidence,
        DateTimeOffset observedAtUtc)
    {
        EvidenceId = evidenceId;
        PilotId = pilotId;
        SessionId = sessionId;
        Observations = new ReadOnlyCollection<ControlledPilotObservationResult>(
            observations.OrderBy(observation => observation.Feature).ToArray());
        MonitoringEvidence = monitoringEvidence;
        ObservedAtUtc = observedAtUtc;
    }

    public string EvidenceId { get; }
    public string PilotId { get; }
    public string SessionId { get; }
    public IReadOnlyList<ControlledPilotObservationResult> Observations { get; }
    public PilotMonitoringEvidence MonitoringEvidence { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public bool LegacyAuthorityPreserved => true;
    public bool MutatedProduction => false;
}

public sealed class ControlledPilotSessionOperationResult
{
    internal ControlledPilotSessionOperationResult(
        ControlledPilotOperationStatus status,
        ControlledPilotSessionState sessionState,
        string reasonCode,
        ControlledPilotEvidence? evidence = null,
        PilotStopDecision? stopDecision = null)
    {
        Status = status;
        SessionState = sessionState;
        ReasonCode = reasonCode;
        Evidence = evidence;
        StopDecision = stopDecision;
    }

    public ControlledPilotOperationStatus Status { get; }
    public ControlledPilotSessionState SessionState { get; }
    public string ReasonCode { get; }
    public ControlledPilotEvidence? Evidence { get; }
    public PilotStopDecision? StopDecision { get; }
    public bool ChangedAuthority => false;
    public bool ExecutedMigration => false;
    public bool MutatedDatabase => false;
    public bool MutatedSettings => false;
    public bool ExecutedEsd => false;
    public bool ActivatedFeature => false;
}

internal static class ControlledProductionPilotText
{
    private const int MaximumLength = 128;
    private static readonly string[] ForbiddenFragments =
    [
        "password", "passwd", "credential", "secret", "private-key", "private key",
        "exception", "stack-trace", "stack trace", "sqlite", "connection-string",
        "select", "insert", "update", "delete", "drop", "alter", "pragma", "attach",
        "access-token", "authorization-token"
    ];

    public static bool IsSafeIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= MaximumLength &&
        value.All(character => char.IsAsciiLetterOrDigit(character) ||
            character is '-' or '_' or '.' or ':') &&
        !ForbiddenFragments.Any(fragment => value.Contains(fragment,
            StringComparison.OrdinalIgnoreCase));

    public static bool IsUsableIdentifier(string? value) => IsSafeIdentifier(value) &&
        !value!.EndsWith("-unavailable", StringComparison.Ordinal);

    public static string SafeIdentifier(string? value, string fallback) =>
        IsSafeIdentifier(value) ? value! : fallback;
}
