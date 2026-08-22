using System.Collections.ObjectModel;
using Rah_Negar.Foundation.Application.Integration;
using Rah_Negar.Foundation.Application.Pilot.Composition;

namespace Rah_Negar.Foundation.Application.Pilot.Validation;

public enum PilotValidationWorkflow
{
    Authentication,
    Reporting,
    RuntimeEvent,
    ProtectedSettings,
    Export
}

public enum PilotObservationBoundary
{
    LegacyAuthoritative,
    TargetReadOnly
}

public enum PilotObservationStatus
{
    Available,
    Unavailable,
    Failed
}

public enum PilotDifferenceClassification
{
    Match,
    Difference,
    Unavailable,
    Failed
}

public enum PilotValidationResultStatus
{
    Completed,
    DifferenceDetected,
    Blocked,
    Failed
}

public enum PilotValidationLifecycleState
{
    Created,
    Validating,
    Completed,
    Failed,
    Disposed
}

public sealed class PilotValidationScope
{
    public PilotValidationScope(
        string scopeId,
        PilotValidationWorkflow workflow,
        string legacyObserverId,
        string targetObserverId,
        IEnumerable<string> subjectIds,
        bool observeLegacy,
        bool observeTarget,
        bool compareResults)
    {
        ArgumentNullException.ThrowIfNull(subjectIds);
        ScopeId = scopeId;
        Workflow = workflow;
        LegacyObserverId = legacyObserverId;
        TargetObserverId = targetObserverId;
        SubjectIds = new ReadOnlyCollection<string>(subjectIds.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray());
        ObserveLegacy = observeLegacy;
        ObserveTarget = observeTarget;
        CompareResults = compareResults;
    }

    public string ScopeId { get; }
    public PilotValidationWorkflow Workflow { get; }
    public string LegacyObserverId { get; }
    public string TargetObserverId { get; }
    public IReadOnlyList<string> SubjectIds { get; }
    public bool ObserveLegacy { get; }
    public bool ObserveTarget { get; }
    public bool CompareResults { get; }
    public bool AllowsAutomaticDiscovery => false;
    public bool AllowsProductionFallback => false;
}

public sealed class PilotWorkflowValidationContext
{
    public PilotWorkflowValidationContext(
        string validationId,
        string pilotId,
        string correlationId,
        string compositionId,
        PilotValidationWorkflow selectedWorkflow,
        DateTimeOffset validationTimestampUtc,
        PilotCapabilityEvidence capabilityEvidence,
        PilotValidationScope scope,
        bool explicitlyApproved)
    {
        ValidationId = validationId;
        PilotId = pilotId;
        CorrelationId = correlationId;
        CompositionId = compositionId;
        SelectedWorkflow = selectedWorkflow;
        ValidationTimestampUtc = validationTimestampUtc;
        CapabilityEvidence = capabilityEvidence;
        Scope = scope;
        ExplicitlyApproved = explicitlyApproved;
    }

    public string ValidationId { get; }
    public string PilotId { get; }
    public string CorrelationId { get; }
    public string CompositionId { get; }
    public PilotValidationWorkflow SelectedWorkflow { get; }
    public DateTimeOffset ValidationTimestampUtc { get; }
    public PilotCapabilityEvidence CapabilityEvidence { get; }
    public PilotValidationScope Scope { get; }
    public bool ExplicitlyApproved { get; }
    public bool AutomaticallyDiscoversWorkflow => false;
    public bool FallsBackToProduction => false;
    public bool SwitchesAuthority => false;
}

public sealed record PilotObservationSafetyProfile(
    bool ReadOnly,
    bool ExecutesProductionWorkflow,
    bool HandlesPasswords,
    bool CreatesSession,
    bool Recalculates,
    bool MutatesEvents,
    bool MutatesSettings,
    bool PerformsProvisioning,
    bool ExecutesCredentials,
    bool MutatesArtifacts,
    bool ChangesAuthority,
    bool AccessesDatabase,
    bool CreatesRbac,
    bool UsesSupportIdentity)
{
    public static PilotObservationSafetyProfile ReadOnlyObservation { get; } = new(
        true, false, false, false, false, false, false, false, false, false,
        false, false, false, false);

    public bool IsSafe => ReadOnly && !ExecutesProductionWorkflow && !HandlesPasswords &&
        !CreatesSession && !Recalculates && !MutatesEvents && !MutatesSettings &&
        !PerformsProvisioning && !ExecutesCredentials && !MutatesArtifacts &&
        !ChangesAuthority && !AccessesDatabase && !CreatesRbac && !UsesSupportIdentity;
}

public sealed class PilotWorkflowObserverDescriptor
{
    public PilotWorkflowObserverDescriptor(
        string observerId,
        string safeName,
        PilotValidationWorkflow workflow,
        PilotObservationBoundary boundary,
        PilotStateAvailability availability,
        PilotObservationSafetyProfile safety)
    {
        ObserverId = PilotValidationText.SafeIdentifier(observerId, "observer-unavailable");
        SafeName = PilotValidationText.SafeLabel(safeName, "Pilot observer");
        Workflow = workflow;
        Boundary = boundary;
        Availability = availability;
        Safety = safety;
    }

    public string ObserverId { get; }
    public string SafeName { get; }
    public PilotValidationWorkflow Workflow { get; }
    public PilotObservationBoundary Boundary { get; }
    public PilotStateAvailability Availability { get; }
    public PilotObservationSafetyProfile Safety { get; }
}

public sealed class PilotWorkflowObservationResult
{
    public PilotWorkflowObservationResult(
        PilotValidationWorkflow workflow,
        PilotObservationBoundary boundary,
        PilotObservationStatus status,
        string fingerprint,
        string evidenceReference,
        DateTimeOffset observedAtUtc,
        IEnumerable<KeyValuePair<string, string>>? comparisonMetadata = null)
    {
        Workflow = workflow;
        Boundary = boundary;
        Status = status;
        Fingerprint = fingerprint;
        EvidenceReference = evidenceReference;
        ObservedAtUtc = observedAtUtc;
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> item in comparisonMetadata ?? [])
        {
            if (PilotValidationText.IsSafeIdentifier(item.Key) &&
                PilotValidationText.IsSafeIdentifier(item.Value))
                metadata[item.Key] = item.Value;
        }
        ComparisonMetadata = new ReadOnlyDictionary<string, string>(metadata);
    }

    public PilotValidationWorkflow Workflow { get; }
    public PilotObservationBoundary Boundary { get; }
    public PilotObservationStatus Status { get; }
    public string Fingerprint { get; }
    public string EvidenceReference { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public IReadOnlyDictionary<string, string> ComparisonMetadata { get; }
    public bool ContainsRawData => false;
    public bool ContainsCredentialMaterial => false;
}

public sealed class PilotWorkflowComparisonResult
{
    public PilotWorkflowComparisonResult(
        PilotValidationWorkflow workflow,
        string legacyFingerprint,
        string targetFingerprint,
        PilotDifferenceClassification classification,
        ShadowDifferenceSeverity severity,
        string evidenceReference)
    {
        Workflow = workflow;
        LegacyFingerprint = legacyFingerprint;
        TargetFingerprint = targetFingerprint;
        Classification = classification;
        Severity = severity;
        EvidenceReference = evidenceReference;
    }

    public PilotValidationWorkflow Workflow { get; }
    public string LegacyFingerprint { get; }
    public string TargetFingerprint { get; }
    public PilotDifferenceClassification Classification { get; }
    public ShadowDifferenceSeverity Severity { get; }
    public string EvidenceReference { get; }
    public bool LegacyRemainsAuthoritative => true;
    public bool AutomaticallyCorrectsDifference => false;
    public bool SwitchesAuthority => false;
}

public sealed class PilotValidationEvidence
{
    public PilotValidationEvidence(
        string validationId,
        string pilotId,
        PilotValidationWorkflow workflow,
        DateTimeOffset timestampUtc,
        PilotValidationResultStatus resultStatus,
        PilotDifferenceClassification comparisonStatus,
        ShadowDifferenceSeverity severity,
        string correlationId,
        string evidenceReference)
    {
        ValidationId = validationId;
        PilotId = pilotId;
        Workflow = workflow;
        TimestampUtc = timestampUtc;
        ResultStatus = resultStatus;
        ComparisonStatus = comparisonStatus;
        Severity = severity;
        CorrelationId = correlationId;
        EvidenceReference = evidenceReference;
    }

    public string ValidationId { get; }
    public string PilotId { get; }
    public PilotValidationWorkflow Workflow { get; }
    public DateTimeOffset TimestampUtc { get; }
    public PilotValidationResultStatus ResultStatus { get; }
    public PilotDifferenceClassification ComparisonStatus { get; }
    public ShadowDifferenceSeverity Severity { get; }
    public string CorrelationId { get; }
    public string EvidenceReference { get; }
    public bool GrantsAuthority => false;
}

public sealed class PilotWorkflowValidationResult
{
    internal PilotWorkflowValidationResult(
        PilotValidationResultStatus status,
        string reasonCode,
        PilotWorkflowObservationResult? legacyObservation,
        PilotWorkflowObservationResult? targetObservation,
        PilotWorkflowComparisonResult? comparison,
        PilotValidationEvidence? evidence)
    {
        Status = status;
        ReasonCode = reasonCode;
        LegacyObservation = legacyObservation;
        TargetObservation = targetObservation;
        Comparison = comparison;
        Evidence = evidence;
    }

    public PilotValidationResultStatus Status { get; }
    public string ReasonCode { get; }
    public PilotWorkflowObservationResult? LegacyObservation { get; }
    public PilotWorkflowObservationResult? TargetObservation { get; }
    public PilotWorkflowComparisonResult? Comparison { get; }
    public PilotValidationEvidence? Evidence { get; }
    public bool MutatedState => false;
    public bool ExecutedProductionWorkflow => false;
    public bool SwitchedAuthority => false;
}

public interface IPilotWorkflowObserver
{
    PilotWorkflowObserverDescriptor Descriptor { get; }

    ValueTask<PilotWorkflowObservationResult?> ObserveAsync(
        PilotWorkflowValidationContext context,
        CancellationToken cancellationToken = default);
}

public interface IAuthenticationPilotValidationObserver : IPilotWorkflowObserver { }
public interface IReportingPilotValidationObserver : IPilotWorkflowObserver { }
public interface IRuntimeEventPilotValidationObserver : IPilotWorkflowObserver { }
public interface IProtectedSettingsPilotValidationObserver : IPilotWorkflowObserver { }
public interface IExportPilotValidationObserver : IPilotWorkflowObserver { }

public interface IPilotWorkflowObservationComparer
{
    PilotWorkflowComparisonResult Compare(
        PilotWorkflowValidationContext context,
        PilotWorkflowObservationResult legacyObservation,
        PilotWorkflowObservationResult targetObservation);
}

public interface IPilotValidationEvidenceFactory
{
    PilotValidationEvidence Create(
        PilotWorkflowValidationContext context,
        PilotWorkflowComparisonResult comparison);
}

internal static class PilotValidationText
{
    private const int MaximumIdentifierLength = 128;
    private static readonly string[] ForbiddenFragments =
    [
        "password", "passwd", "pwd", "credential", "secret", "private-key", "private key",
        "signature", "exception", "stack-trace", "stack trace", "sqlite", "sql",
        "select", "insert", "update", "delete", "drop", "alter", "pragma", "attach",
        "authorization", "token", "hash", "sha"
    ];

    public static bool IsSafeIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= MaximumIdentifierLength &&
        value.All(character => char.IsAsciiLetterOrDigit(character) ||
            character is '-' or '_' or '.' or ':') &&
        !ForbiddenFragments.Any(fragment => value.Contains(fragment,
            StringComparison.OrdinalIgnoreCase));

    public static string SafeIdentifier(string? value, string fallback) =>
        IsSafeIdentifier(value) ? value! : fallback;

    public static string SafeLabel(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        string candidate = value.Trim();
        if (candidate.Length > MaximumIdentifierLength || candidate.Any(char.IsControl) ||
            candidate.Contains('/') || candidate.Contains('\\') ||
            ForbiddenFragments.Any(fragment => candidate.Contains(fragment,
                StringComparison.OrdinalIgnoreCase))) return fallback;
        return candidate;
    }
}
