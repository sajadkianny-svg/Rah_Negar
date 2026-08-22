using System.Collections.ObjectModel;
using Rah_Negar.Foundation.Application.Pilot.Validation;

namespace Rah_Negar.Foundation.Application.Pilot.Deployment;

public enum PilotDeploymentReadinessStatus
{
    Ready,
    Blocked,
    RequiresReview
}

public enum PilotDeploymentFeature
{
    AuthenticationObservation,
    ReportingObservation,
    RuntimeEventObservation,
    ProtectedSettingsObservation,
    ExportObservation
}

public enum PilotEnvironmentValidationKind
{
    OsCompatibility,
    ApplicationBuild,
    Dependency,
    Configuration,
    SecurityBaseline
}

public enum PilotReadinessGateStatus
{
    Passed,
    Failed,
    RequiresReview
}

public enum PilotApprovalGateKind
{
    Security,
    Operations,
    DataOwner,
    Product
}

public enum PilotApprovalGateStatus
{
    Approved,
    Missing,
    RequiresReview
}

public enum PilotRollbackValidationStatus
{
    Ready,
    Unavailable,
    RequiresReview
}

public enum PilotStopConditionKind
{
    ValidationFailure,
    EvidenceMismatch,
    EnvironmentFailure,
    RollbackUnavailable,
    ApprovalMissing
}

public enum PilotMonitoringSignalKind
{
    PilotHealth,
    ValidationDifferences,
    SecurityEvents,
    RollbackStatus
}

public enum PilotDeploymentChecklistItem
{
    WorkflowValidationEvidence,
    EnvironmentEvidence,
    DeploymentManifest,
    RollbackPreparation,
    ApprovalEvidence,
    StopConditions,
    MonitoringPreparation
}

public sealed class PilotDeploymentReadinessContext
{
    public PilotDeploymentReadinessContext(
        string readinessId,
        string pilotScope,
        string targetEnvironmentId,
        IEnumerable<PilotDeploymentFeature> requiredFeatures,
        PilotValidationResultStatus validationStatus,
        IEnumerable<string> approvalReferences,
        string rollbackReference,
        DateTimeOffset timestampUtc,
        bool explicitlyRequested)
    {
        ArgumentNullException.ThrowIfNull(requiredFeatures);
        ArgumentNullException.ThrowIfNull(approvalReferences);
        ReadinessId = readinessId;
        PilotScope = pilotScope;
        TargetEnvironmentId = targetEnvironmentId;
        RequiredFeatures = new ReadOnlyCollection<PilotDeploymentFeature>(requiredFeatures
            .Distinct().Order().ToArray());
        ApprovalReferences = new ReadOnlyCollection<string>(approvalReferences
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        ValidationStatus = validationStatus;
        RollbackReference = rollbackReference;
        TimestampUtc = timestampUtc;
        ExplicitlyRequested = explicitlyRequested;
    }

    public string ReadinessId { get; }
    public string PilotScope { get; }
    public string TargetEnvironmentId { get; }
    public IReadOnlyList<PilotDeploymentFeature> RequiredFeatures { get; }
    public PilotValidationResultStatus ValidationStatus { get; }
    public IReadOnlyList<string> ApprovalReferences { get; }
    public string RollbackReference { get; }
    public DateTimeOffset TimestampUtc { get; }
    public bool ExplicitlyRequested { get; }
    public bool AutomaticallyDiscoversEnvironment => false;
    public bool FallsBackToProduction => false;
    public bool ActivatesPilot => false;
}

public sealed class PilotDeploymentManifest
{
    public PilotDeploymentManifest(
        string manifestId,
        string version,
        string buildFingerprint,
        IEnumerable<string> artifactIdentifiers,
        IEnumerable<string> dependencySummary,
        PilotReadinessGateStatus validationStatus)
    {
        ArgumentNullException.ThrowIfNull(artifactIdentifiers);
        ArgumentNullException.ThrowIfNull(dependencySummary);
        ManifestId = PilotDeploymentText.SafeIdentifier(manifestId, "manifest-unavailable");
        Version = PilotDeploymentText.SafeIdentifier(version, "version-unavailable");
        BuildFingerprint = PilotDeploymentText.SafeIdentifier(buildFingerprint,
            "build-unavailable");
        ArtifactIdentifiers = PilotDeploymentText.SafeIdentifiers(artifactIdentifiers);
        DependencySummary = PilotDeploymentText.SafeIdentifiers(dependencySummary);
        ValidationStatus = validationStatus;
    }

    public string ManifestId { get; }
    public string Version { get; }
    public string BuildFingerprint { get; }
    public IReadOnlyList<string> ArtifactIdentifiers { get; }
    public IReadOnlyList<string> DependencySummary { get; }
    public PilotReadinessGateStatus ValidationStatus { get; }
    public bool ContainsSensitiveConfiguration => false;
    public bool ContainsCredentialMaterial => false;
    public bool DeploysArtifacts => false;
}

public sealed class PilotEnvironmentValidationEvidence
{
    public PilotEnvironmentValidationEvidence(
        PilotEnvironmentValidationKind kind,
        PilotReadinessGateStatus status,
        string evidenceReference,
        DateTimeOffset observedAtUtc)
    {
        Kind = kind;
        Status = status;
        EvidenceReference = PilotDeploymentText.SafeIdentifier(evidenceReference,
            "environment-evidence-unavailable");
        ObservedAtUtc = observedAtUtc;
    }

    public PilotEnvironmentValidationKind Kind { get; }
    public PilotReadinessGateStatus Status { get; }
    public string EvidenceReference { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public bool IsReadOnly => true;
    public bool IsDeterministic => true;
    public bool ModifiesEnvironment => false;
}

public interface IPilotEnvironmentReadinessValidator
{
    PilotEnvironmentValidationKind Kind { get; }

    PilotEnvironmentValidationEvidence? Validate(
        PilotDeploymentReadinessContext context,
        PilotDeploymentManifest manifest);
}

public sealed class ImmutablePilotEnvironmentReadinessValidator :
    IPilotEnvironmentReadinessValidator
{
    private readonly PilotEnvironmentValidationEvidence _evidence;

    public ImmutablePilotEnvironmentReadinessValidator(
        PilotEnvironmentValidationEvidence evidence)
    {
        _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
    }

    public PilotEnvironmentValidationKind Kind => _evidence.Kind;
    public bool ReadsOperatingSystem => false;
    public bool ReadsConfiguration => false;
    public bool ResolvesServices => false;

    public PilotEnvironmentValidationEvidence Validate(
        PilotDeploymentReadinessContext context,
        PilotDeploymentManifest manifest) => _evidence;
}

public sealed class PilotRollbackReadiness
{
    public PilotRollbackReadiness(
        string rollbackPlanId,
        string restorePointReference,
        PilotRollbackValidationStatus validationStatus,
        string ownerReference,
        string evidenceReference)
    {
        RollbackPlanId = PilotDeploymentText.SafeIdentifier(rollbackPlanId,
            "rollback-plan-unavailable");
        RestorePointReference = PilotDeploymentText.SafeIdentifier(restorePointReference,
            "restore-point-unavailable");
        ValidationStatus = validationStatus;
        OwnerReference = PilotDeploymentText.SafeIdentifier(ownerReference,
            "rollback-owner-unavailable");
        EvidenceReference = PilotDeploymentText.SafeIdentifier(evidenceReference,
            "rollback-evidence-unavailable");
    }

    public string RollbackPlanId { get; }
    public string RestorePointReference { get; }
    public PilotRollbackValidationStatus ValidationStatus { get; }
    public string OwnerReference { get; }
    public string EvidenceReference { get; }
    public bool ExecutesRollback => false;
    public bool PerformsDestructiveAction => false;
    public bool RestoresDatabase => false;
}

public sealed class PilotApprovalGate
{
    public PilotApprovalGate(
        PilotApprovalGateKind kind,
        PilotApprovalGateStatus status,
        string approvalReference,
        string evidenceReference,
        DateTimeOffset reviewedAtUtc)
    {
        Kind = kind;
        Status = status;
        ApprovalReference = PilotDeploymentText.SafeIdentifier(approvalReference,
            "approval-unavailable");
        EvidenceReference = PilotDeploymentText.SafeIdentifier(evidenceReference,
            "approval-evidence-unavailable");
        ReviewedAtUtc = reviewedAtUtc;
    }

    public PilotApprovalGateKind Kind { get; }
    public PilotApprovalGateStatus Status { get; }
    public string ApprovalReference { get; }
    public string EvidenceReference { get; }
    public DateTimeOffset ReviewedAtUtc { get; }
    public bool GrantsPermission => false;
    public bool ImplementsRbac => false;
}

public sealed class PilotStopCondition
{
    public PilotStopCondition(
        PilotStopConditionKind kind,
        bool triggered,
        string evidenceReference)
    {
        Kind = kind;
        Triggered = triggered;
        EvidenceReference = PilotDeploymentText.SafeIdentifier(evidenceReference,
            "stop-evidence-unavailable");
    }

    public PilotStopConditionKind Kind { get; }
    public bool Triggered { get; }
    public string EvidenceReference { get; }
    public bool AutomaticallyShutsDown => false;
    public bool ExecutesAction => false;
}

public sealed class PilotMonitoringReadinessPlan
{
    public PilotMonitoringReadinessPlan(
        string planId,
        IEnumerable<PilotMonitoringSignalKind> requiredSignals,
        string ownerReference,
        string escalationReference)
    {
        ArgumentNullException.ThrowIfNull(requiredSignals);
        PlanId = PilotDeploymentText.SafeIdentifier(planId, "monitoring-plan-unavailable");
        RequiredSignals = new ReadOnlyCollection<PilotMonitoringSignalKind>(requiredSignals
            .Distinct().Order().ToArray());
        OwnerReference = PilotDeploymentText.SafeIdentifier(ownerReference,
            "monitoring-owner-unavailable");
        EscalationReference = PilotDeploymentText.SafeIdentifier(escalationReference,
            "monitoring-escalation-unavailable");
    }

    public string PlanId { get; }
    public IReadOnlyList<PilotMonitoringSignalKind> RequiredSignals { get; }
    public string OwnerReference { get; }
    public string EscalationReference { get; }
    public bool ImplementsTelemetry => false;
    public bool StartsMonitoring => false;
}

public sealed class PilotDeploymentChecklistEntry
{
    public PilotDeploymentChecklistEntry(
        PilotDeploymentChecklistItem item,
        PilotReadinessGateStatus status,
        string evidenceReference)
    {
        Item = item;
        Status = status;
        EvidenceReference = PilotDeploymentText.SafeIdentifier(evidenceReference,
            "checklist-evidence-unavailable");
    }

    public PilotDeploymentChecklistItem Item { get; }
    public PilotReadinessGateStatus Status { get; }
    public string EvidenceReference { get; }
    public bool PerformsAction => false;
}

public sealed class PilotDeploymentChecklist
{
    public PilotDeploymentChecklist(IEnumerable<PilotDeploymentChecklistEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Entries = new ReadOnlyCollection<PilotDeploymentChecklistEntry>(entries.ToArray());
    }

    public IReadOnlyList<PilotDeploymentChecklistEntry> Entries { get; }
    public bool Deploys => false;
    public bool Activates => false;
}

public sealed class PilotDeploymentEvidencePackage
{
    internal PilotDeploymentEvidencePackage(
        string packageId,
        PilotDeploymentReadinessStatus readinessStatus,
        IEnumerable<PilotEnvironmentValidationEvidence> validationRecords,
        IEnumerable<PilotApprovalGate> approvals,
        IEnumerable<string> blockers,
        PilotRollbackReadiness rollbackStatus,
        string manifestId,
        DateTimeOffset assembledAtUtc)
    {
        PackageId = packageId;
        ReadinessStatus = readinessStatus;
        ValidationRecords = new ReadOnlyCollection<PilotEnvironmentValidationEvidence>(
            validationRecords.ToArray());
        Approvals = new ReadOnlyCollection<PilotApprovalGate>(approvals.ToArray());
        Blockers = new ReadOnlyCollection<string>(blockers.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray());
        RollbackStatus = rollbackStatus;
        ManifestId = manifestId;
        AssembledAtUtc = assembledAtUtc;
    }

    public string PackageId { get; }
    public PilotDeploymentReadinessStatus ReadinessStatus { get; }
    public IReadOnlyList<PilotEnvironmentValidationEvidence> ValidationRecords { get; }
    public IReadOnlyList<PilotApprovalGate> Approvals { get; }
    public IReadOnlyList<string> Blockers { get; }
    public PilotRollbackReadiness RollbackStatus { get; }
    public string ManifestId { get; }
    public DateTimeOffset AssembledAtUtc { get; }
    public bool ContainsSecrets => false;
    public bool ContainsRawLogs => false;
    public bool ContainsDatabaseDump => false;
}

public sealed class PilotDeploymentReadinessResult
{
    internal PilotDeploymentReadinessResult(
        PilotDeploymentReadinessStatus status,
        string reasonCode,
        IEnumerable<string> blockers,
        PilotDeploymentEvidencePackage? evidencePackage)
    {
        Status = status;
        ReasonCode = reasonCode;
        Blockers = new ReadOnlyCollection<string>(blockers.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray());
        EvidencePackage = evidencePackage;
    }

    public PilotDeploymentReadinessStatus Status { get; }
    public string ReasonCode { get; }
    public IReadOnlyList<string> Blockers { get; }
    public PilotDeploymentEvidencePackage? EvidencePackage { get; }
    public bool Deployed => false;
    public bool Activated => false;
    public bool Migrated => false;
    public bool ModifiedDatabase => false;
    public bool SwitchedAuthority => false;
}

internal static class PilotDeploymentText
{
    private const int MaximumLength = 128;
    private static readonly string[] ForbiddenFragments =
    [
        "password", "passwd", "credential", "secret", "private-key", "private key",
        "exception", "stack-trace", "stack trace", "sqlite", "connection-string",
        "select", "insert", "update", "delete", "drop", "alter", "pragma", "attach",
        "authorization-token", "access-token"
    ];

    public static bool IsSafeIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= MaximumLength &&
        value.All(character => char.IsAsciiLetterOrDigit(character) ||
            character is '-' or '_' or '.' or ':') &&
        !ForbiddenFragments.Any(fragment => value.Contains(fragment,
            StringComparison.OrdinalIgnoreCase));

    public static string SafeIdentifier(string? value, string fallback) =>
        IsSafeIdentifier(value) ? value! : fallback;

    public static bool IsUsableIdentifier(string? value) => IsSafeIdentifier(value) &&
        !value!.EndsWith("-unavailable", StringComparison.Ordinal);

    public static IReadOnlyList<string> SafeIdentifiers(IEnumerable<string> values) =>
        new ReadOnlyCollection<string>(values.Where(IsSafeIdentifier)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
}
