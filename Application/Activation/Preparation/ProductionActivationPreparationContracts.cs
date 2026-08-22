using System.Collections.ObjectModel;
using Rah_Negar.Foundation.Application.Pilot.Deployment;
using Rah_Negar.Foundation.Application.Pilot.Validation;

namespace Rah_Negar.Foundation.Application.Activation.Preparation;

public enum LegacyAuthorityState
{
    LegacyAuthoritative,
    TargetAuthorityRequested,
    Unknown
}

public enum ProductionActivationPreparationDecision
{
    ApprovedForPreparation,
    Blocked,
    RequiresReview
}

public enum ProductionActivationGateType
{
    SecurityReview,
    OperationsReadiness,
    DataOwnerApproval,
    RollbackReadiness,
    ValidationCompletion,
    DeploymentReadiness
}

public enum ProductionActivationGateStatus
{
    Satisfied,
    Missing,
    Failed,
    RequiresReview
}

public enum BackupEvidenceStatus
{
    Verified,
    Unavailable,
    Failed,
    RequiresReview
}

public enum RestoreTestStatus
{
    Passed,
    NotPerformed,
    Failed,
    RequiresReview
}

public enum RollbackEvidenceStatus
{
    Verified,
    Unavailable,
    Failed,
    RequiresReview
}

public enum ProductionActivationStopConditionType
{
    ValidationIncomplete,
    BackupUnavailable,
    RollbackUnavailable,
    ApprovalMissing,
    EvidenceMismatch,
    EnvironmentMismatch
}

public sealed class ProductionActivationPreparationContext
{
    public ProductionActivationPreparationContext(
        string preparationId,
        string releaseIdentifier,
        ProductionActivationScope targetScope,
        LegacyAuthorityState legacyAuthorityState,
        PilotValidationResultStatus pilotValidationStatus,
        PilotDeploymentReadinessStatus deploymentReadinessStatus,
        string rollbackReference,
        IEnumerable<string> approvalReferences,
        DateTimeOffset timestampUtc,
        bool explicitlyRequested)
    {
        ArgumentNullException.ThrowIfNull(approvalReferences);
        PreparationId = preparationId;
        ReleaseIdentifier = releaseIdentifier;
        TargetScope = targetScope;
        LegacyAuthorityState = legacyAuthorityState;
        PilotValidationStatus = pilotValidationStatus;
        DeploymentReadinessStatus = deploymentReadinessStatus;
        RollbackReference = rollbackReference;
        ApprovalReferences = new ReadOnlyCollection<string>(approvalReferences
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        TimestampUtc = timestampUtc;
        ExplicitlyRequested = explicitlyRequested;
    }

    public string PreparationId { get; }
    public string ReleaseIdentifier { get; }
    public ProductionActivationScope TargetScope { get; }
    public LegacyAuthorityState LegacyAuthorityState { get; }
    public PilotValidationResultStatus PilotValidationStatus { get; }
    public PilotDeploymentReadinessStatus DeploymentReadinessStatus { get; }
    public string RollbackReference { get; }
    public IReadOnlyList<string> ApprovalReferences { get; }
    public DateTimeOffset TimestampUtc { get; }
    public bool ExplicitlyRequested { get; }
    public bool AutomaticallyDiscoversEnvironment => false;
    public bool AccessesProduction => false;
    public bool GrantsActivationPermission => false;
    public bool FallsBackToProduction => false;
}

public sealed class ProductionActivationGate
{
    public ProductionActivationGate(
        ProductionActivationGateType gateType,
        ProductionActivationGateStatus status,
        string evidenceReference,
        string reviewerReference,
        DateTimeOffset reviewedAtUtc)
    {
        GateType = gateType;
        Status = status;
        EvidenceReference = ProductionPreparationText.SafeIdentifier(evidenceReference,
            "gate-evidence-unavailable");
        ReviewerReference = ProductionPreparationText.SafeIdentifier(reviewerReference,
            "gate-reviewer-unavailable");
        ReviewedAtUtc = reviewedAtUtc;
    }

    public ProductionActivationGateType GateType { get; }
    public ProductionActivationGateStatus Status { get; }
    public string EvidenceReference { get; }
    public string ReviewerReference { get; }
    public DateTimeOffset ReviewedAtUtc { get; }
    public bool GrantsPermission => false;
    public bool CreatesPermission => false;
    public bool ImplementsRbac => false;
}

public sealed class BackupVerificationResult
{
    public BackupVerificationResult(
        string backupReference,
        BackupEvidenceStatus verificationStatus,
        RestoreTestStatus restoreTestStatus,
        DateTimeOffset verifiedAtUtc)
    {
        BackupReference = ProductionPreparationText.SafeIdentifier(backupReference,
            "backup-unavailable");
        VerificationStatus = verificationStatus;
        RestoreTestStatus = restoreTestStatus;
        VerifiedAtUtc = verifiedAtUtc;
    }

    public string BackupReference { get; }
    public BackupEvidenceStatus VerificationStatus { get; }
    public RestoreTestStatus RestoreTestStatus { get; }
    public DateTimeOffset VerifiedAtUtc { get; }
    public bool ExecutesBackup => false;
    public bool AccessesFiles => false;
    public bool AccessesDatabase => false;
    public bool ExecutesRestore => false;
}

public sealed class RollbackVerificationResult
{
    public RollbackVerificationResult(
        string rollbackPlanReference,
        RollbackEvidenceStatus validationStatus,
        string ownerReference,
        string evidenceReference)
    {
        RollbackPlanReference = ProductionPreparationText.SafeIdentifier(
            rollbackPlanReference, "rollback-plan-unavailable");
        ValidationStatus = validationStatus;
        OwnerReference = ProductionPreparationText.SafeIdentifier(ownerReference,
            "rollback-owner-unavailable");
        EvidenceReference = ProductionPreparationText.SafeIdentifier(evidenceReference,
            "rollback-evidence-unavailable");
    }

    public string RollbackPlanReference { get; }
    public RollbackEvidenceStatus ValidationStatus { get; }
    public string OwnerReference { get; }
    public string EvidenceReference { get; }
    public bool ExecutesRollback => false;
    public bool PerformsDestructiveOperation => false;
}

public sealed class ProductionActivationStopCondition
{
    public ProductionActivationStopCondition(
        ProductionActivationStopConditionType conditionType,
        bool triggered,
        string evidenceReference)
    {
        ConditionType = conditionType;
        Triggered = triggered;
        EvidenceReference = ProductionPreparationText.SafeIdentifier(evidenceReference,
            "stop-evidence-unavailable");
    }

    public ProductionActivationStopConditionType ConditionType { get; }
    public bool Triggered { get; }
    public string EvidenceReference { get; }
    public bool AutomaticallyActs => false;
    public bool ShutsDown => false;
}

public sealed class ProductionActivationValidationSummary
{
    internal ProductionActivationValidationSummary(
        PilotValidationResultStatus pilotValidationStatus,
        PilotDeploymentReadinessStatus deploymentReadinessStatus,
        LegacyAuthorityState legacyAuthorityState,
        string validationEvidenceReference,
        string deploymentEvidenceReference)
    {
        PilotValidationStatus = pilotValidationStatus;
        DeploymentReadinessStatus = deploymentReadinessStatus;
        LegacyAuthorityState = legacyAuthorityState;
        ValidationEvidenceReference = validationEvidenceReference;
        DeploymentEvidenceReference = deploymentEvidenceReference;
    }

    public PilotValidationResultStatus PilotValidationStatus { get; }
    public PilotDeploymentReadinessStatus DeploymentReadinessStatus { get; }
    public LegacyAuthorityState LegacyAuthorityState { get; }
    public string ValidationEvidenceReference { get; }
    public string DeploymentEvidenceReference { get; }
    public bool LegacyRemainsAuthoritative => true;
}

public sealed class ProductionCutoverEvidencePackage
{
    internal ProductionCutoverEvidencePackage(
        string packageId,
        ProductionActivationPreparationDecision preparationResult,
        IEnumerable<ProductionActivationGate> activationGates,
        ProductionActivationValidationSummary validationSummary,
        RollbackVerificationResult rollbackStatus,
        BackupVerificationResult backupStatus,
        IEnumerable<string> blockers,
        IEnumerable<string> reviewItems,
        DateTimeOffset assembledAtUtc)
    {
        PackageId = packageId;
        PreparationResult = preparationResult;
        ActivationGates = new ReadOnlyCollection<ProductionActivationGate>(
            activationGates.ToArray());
        ValidationSummary = validationSummary;
        RollbackStatus = rollbackStatus;
        BackupStatus = backupStatus;
        Blockers = new ReadOnlyCollection<string>(blockers.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray());
        ReviewItems = new ReadOnlyCollection<string>(reviewItems.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray());
        AssembledAtUtc = assembledAtUtc;
    }

    public string PackageId { get; }
    public ProductionActivationPreparationDecision PreparationResult { get; }
    public IReadOnlyList<ProductionActivationGate> ActivationGates { get; }
    public ProductionActivationValidationSummary ValidationSummary { get; }
    public RollbackVerificationResult RollbackStatus { get; }
    public BackupVerificationResult BackupStatus { get; }
    public IReadOnlyList<string> Blockers { get; }
    public IReadOnlyList<string> ReviewItems { get; }
    public DateTimeOffset AssembledAtUtc { get; }
    public bool ContainsSecrets => false;
    public bool ContainsCredentialMaterial => false;
    public bool ContainsDatabaseDump => false;
    public bool ContainsRawLogs => false;
    public bool ContainsPrivateKeys => false;
    public bool GrantsActivationPermission => false;
}

public sealed class ProductionActivationReadinessResult
{
    internal ProductionActivationReadinessResult(
        ProductionActivationPreparationDecision decision,
        string reasonCode,
        IEnumerable<string> blockers,
        IEnumerable<string> reviewItems,
        ProductionCutoverEvidencePackage? evidencePackage)
    {
        Decision = decision;
        ReasonCode = reasonCode;
        Blockers = new ReadOnlyCollection<string>(blockers.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray());
        ReviewItems = new ReadOnlyCollection<string>(reviewItems.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray());
        EvidencePackage = evidencePackage;
    }

    public ProductionActivationPreparationDecision Decision { get; }
    public string ReasonCode { get; }
    public IReadOnlyList<string> Blockers { get; }
    public IReadOnlyList<string> ReviewItems { get; }
    public ProductionCutoverEvidencePackage? EvidencePackage { get; }
    public bool ActivatedFeatures => false;
    public bool ExecutedDeployment => false;
    public bool RanMigration => false;
    public bool ModifiedDatabase => false;
    public bool PerformedEsdCutover => false;
    public bool SwitchedAuthority => false;
    public bool LegacyAuthorityPreserved => true;
    public bool ReplacedLogin => false;
    public bool ReplacedSettings => false;
    public bool ReplacedReporting => false;
    public bool ReplacedRuntimeEvents => false;
}

internal static class ProductionPreparationText
{
    private const int MaximumLength = 128;
    private static readonly string[] ForbiddenFragments =
    [
        "password", "passwd", "credential", "secret", "private-key", "private key",
        "exception", "stack-trace", "stack trace", "sqlite", "connection-string",
        "select", "insert", "update", "delete", "drop", "alter", "pragma", "attach",
        "access-token", "authorization-token", "permission-escalation"
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
