using Rah_Negar.Foundation.Application.Database.Readiness;
using Rah_Negar.Foundation.Application.Security;

namespace Rah_Negar.Foundation.Application.Activation;

public enum ProductionActivationState
{
    NotPrepared,
    AssessmentReady,
    BackupVerified,
    RehearsalVerified,
    ApprovalPending,
    ApprovedForActivation,
    ActivationInProgress,
    Activated,
    ActivationBlocked,
    ActivationRolledBack
}

public sealed record ActivationStateTransitionRequest(
    ProductionActivationState From,
    ProductionActivationState To,
    string TransitionId,
    string CorrelationId,
    string ActorReference,
    DateTimeOffset RequestedAtUtc);

public sealed record ActivationStateTransitionResult(
    bool Accepted,
    ProductionActivationState State,
    string ResultCategory);

public enum ProductionActivationScope
{
    UnifiedMigrationActivation,
    AuthenticationWorkflowActivation,
    SnapshotReportingActivation,
    RuntimeEventProjectionActivation,
    ReportExportActivation,
    ProtectedSettingsActivation,
    MigrationToolingActivation
}

public sealed record ActivationPreflightEvidence(
    string DatabaseIdentityFingerprint,
    bool Succeeded,
    bool HeaderValid,
    bool IntegrityPassed,
    bool ForeignKeyIntegrityPassed,
    bool ReadOnlyEnforced,
    DateTimeOffset InspectedAtUtc);

public sealed record ActivationMigrationEvidence(
    MigrationHistoryClassification Classification,
    int SupportedTargetVersion,
    bool ChainSupported,
    bool ChecksumValidated);

public sealed record ActivationBackupReceipt(
    string ReceiptId,
    string SourceDatabaseIdentityFingerprint,
    string BackupIdentityFingerprint,
    bool Verified,
    bool IntegrityPassed,
    long SizeBytes,
    DateTimeOffset CreatedAtUtc);

public sealed record ActivationIntegrityEvidence(
    bool PreflightIntegrityPassed,
    bool ForeignKeyIntegrityPassed,
    bool BackupIntegrityPassed,
    bool RehearsalIntegrityPassed);

public sealed record ActivationSnapshotPreservationEvidence(
    bool Passed,
    bool FinalizedSnapshotsPreserved,
    bool ReportLocksPreserved,
    bool LegacyEvidencePreserved,
    bool EsdValuePreserved,
    bool NoRbacIntroduced,
    bool NoSupportIdentityIntroduced);

public sealed record ActivationRehearsalReceipt(
    string ReceiptId,
    bool Passed,
    bool IdempotentRerun,
    bool OriginalBackupUnchanged,
    int FinalVersion,
    EsdReconciliationState EsdReconciliationState,
    EsdAuthorityMode EsdAuthorityMode,
    ActivationSnapshotPreservationEvidence Preservation,
    DateTimeOffset CompletedAtUtc);

public sealed record OperatorApprovalMetadataBoundary(
    bool ExplicitApprovalRequired,
    ProductionActivationScope RequiredScope,
    string TargetDatabaseIdentityFingerprint,
    string EvidencePackageId);

public sealed class ActivationEvidencePackage
{
    public ActivationEvidencePackage(
        string evidencePackageId,
        string correlationId,
        string databaseIdentityFingerprint,
        ActivationPreflightEvidence preflight,
        ActivationMigrationEvidence migration,
        ActivationBackupReceipt backupReceipt,
        ActivationRehearsalReceipt rehearsalReceipt,
        ActivationIntegrityEvidence integrity,
        OperatorApprovalMetadataBoundary approvalBoundary,
        DateTimeOffset assembledAtUtc)
    {
        EvidencePackageId = evidencePackageId;
        CorrelationId = correlationId;
        DatabaseIdentityFingerprint = databaseIdentityFingerprint;
        Preflight = preflight;
        Migration = migration;
        BackupReceipt = backupReceipt;
        RehearsalReceipt = rehearsalReceipt;
        Integrity = integrity;
        ApprovalBoundary = approvalBoundary;
        AssembledAtUtc = assembledAtUtc;
    }

    public string EvidencePackageId { get; }
    public string CorrelationId { get; }
    public string DatabaseIdentityFingerprint { get; }
    public ActivationPreflightEvidence Preflight { get; }
    public ActivationMigrationEvidence Migration { get; }
    public ActivationBackupReceipt BackupReceipt { get; }
    public ActivationRehearsalReceipt RehearsalReceipt { get; }
    public ActivationIntegrityEvidence Integrity { get; }
    public EsdReconciliationState EsdReconciliationStatus => RehearsalReceipt.EsdReconciliationState;
    public ActivationSnapshotPreservationEvidence SnapshotPreservation => RehearsalReceipt.Preservation;
    public OperatorApprovalMetadataBoundary ApprovalBoundary { get; }
    public DateTimeOffset AssembledAtUtc { get; }
}

public sealed record ActivationEvidenceValidationResult(
    bool IsComplete,
    IReadOnlyList<string> Issues);

public sealed record ProductionActivationApproval(
    string ApprovalId,
    string ApprovedByActorReference,
    DateTimeOffset ApprovedAtUtc,
    ProductionActivationScope ApprovedScope,
    string TargetDatabaseIdentityFingerprint,
    string EvidencePackageId,
    string CorrelationId,
    DateTimeOffset? ExpiresAtUtc);

public enum ActivationApprovalFailure
{
    None,
    Malformed,
    NotYetValid,
    Expired,
    WrongScope,
    WrongDatabaseIdentity,
    WrongEvidencePackage,
    WrongCorrelation
}

public sealed record ActivationApprovalValidationResult(
    bool IsValid,
    ActivationApprovalFailure Failure,
    string ResultCategory);

public enum ActivationGuardDecision
{
    Allowed,
    Blocked,
    RequiresManualReview
}

public sealed record ProductionActivationGuardRequest(
    MaintenanceWindowReadinessResult Readiness,
    DatabasePreflightResult Preflight,
    MigrationHistoryClassificationResult MigrationClassification,
    DatabaseBackupVerificationResult Backup,
    MigrationRehearsalResult Rehearsal,
    ActivationEvidencePackage EvidencePackage,
    ProductionActivationApproval? Approval,
    ProductionActivationScope RequiredScope);

public sealed record ProductionActivationGuardResult(
    ActivationGuardDecision Decision,
    IReadOnlyList<string> Reasons);

public sealed record ExplicitProductionMigrationAuthorization(
    string AuthorizationId,
    string AuthorizedByActorReference,
    string ApprovalId,
    string EvidencePackageId,
    string TargetDatabaseIdentityFingerprint,
    string CorrelationId,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public sealed record ApprovedProductionMigrationContext(
    string ExplicitDatabasePath,
    ActivationEvidencePackage EvidencePackage,
    ProductionActivationApproval Approval,
    ExplicitProductionMigrationAuthorization Authorization,
    ProductionActivationGuardResult GuardResult,
    string? ExplicitVerifiedBackupPath = null,
    string? VerifiedBackupSha256 = null);

public enum ProductionMigrationExecutionStatus
{
    Succeeded,
    Rejected,
    Failed
}

public sealed record ProductionMigrationExecutionResult(
    ProductionMigrationExecutionStatus Status,
    string CorrelationId,
    string? SafeReceiptId,
    string ResultCategory,
    ProductionMigrationValidationReceipt? Receipt = null);

/// <summary>
/// Immutable, non-secret result of one explicit migration execution boundary.
/// It records validation and preservation evidence only; it does not grant or
/// imply target authority.
/// </summary>
public sealed class ProductionMigrationValidationReceipt
{
    public ProductionMigrationValidationReceipt(
        string receiptId,
        string correlationId,
        string databaseIdentityFingerprint,
        string backupIdentityFingerprint,
        MigrationHistoryClassification initialClassification,
        int initialVersion,
        int finalVersion,
        IEnumerable<string> appliedMigrationIds,
        bool idempotentRerun,
        bool originalBackupUnchanged,
        bool preflightIntegrityPassed,
        bool postValidationPassed,
        PreservationVerificationResult preservation,
        bool legacyRemainsAuthoritative,
        bool targetRoutingDisabled,
        OperationalRollbackState rollbackState,
        DateTimeOffset completedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(receiptId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseIdentityFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupIdentityFingerprint);
        ArgumentNullException.ThrowIfNull(appliedMigrationIds);
        ArgumentNullException.ThrowIfNull(preservation);
        if (completedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Receipt time must be UTC.", nameof(completedAtUtc));

        ReceiptId = receiptId;
        CorrelationId = correlationId;
        DatabaseIdentityFingerprint = databaseIdentityFingerprint;
        BackupIdentityFingerprint = backupIdentityFingerprint;
        InitialClassification = initialClassification;
        InitialVersion = initialVersion;
        FinalVersion = finalVersion;
        AppliedMigrationIds = Array.AsReadOnly(appliedMigrationIds.ToArray());
        IdempotentRerun = idempotentRerun;
        OriginalBackupUnchanged = originalBackupUnchanged;
        PreflightIntegrityPassed = preflightIntegrityPassed;
        PostValidationPassed = postValidationPassed;
        Preservation = preservation;
        LegacyRemainsAuthoritative = legacyRemainsAuthoritative;
        TargetRoutingDisabled = targetRoutingDisabled;
        RollbackState = rollbackState;
        CompletedAtUtc = completedAtUtc;
    }

    public string ReceiptId { get; }
    public string CorrelationId { get; }
    public string DatabaseIdentityFingerprint { get; }
    public string BackupIdentityFingerprint { get; }
    public MigrationHistoryClassification InitialClassification { get; }
    public int InitialVersion { get; }
    public int FinalVersion { get; }
    public IReadOnlyList<string> AppliedMigrationIds { get; }
    public bool IdempotentRerun { get; }
    public bool OriginalBackupUnchanged { get; }
    public bool PreflightIntegrityPassed { get; }
    public bool PostValidationPassed { get; }
    public PreservationVerificationResult Preservation { get; }
    public bool LegacyRemainsAuthoritative { get; }
    public bool TargetRoutingDisabled { get; }
    public OperationalRollbackState RollbackState { get; }
    public DateTimeOffset CompletedAtUtc { get; }
}

/// <summary>
/// Explicit production execution contract. Implementations must remain behind
/// the approved-context boundary and must never activate target authority.
/// </summary>
public interface IProductionMigrationExecutor
{
    Task<ProductionMigrationExecutionResult> ExecuteAsync(
        ApprovedProductionMigrationContext approvedContext,
        CancellationToken cancellationToken = default);
}

public enum ActivationAuditAction
{
    StateTransitionRequested,
    StateTransitionRejected,
    EvidenceAssembled,
    ApprovalRecorded,
    GuardEvaluated,
    ActivationRequested,
    RollbackDecisionRecorded
}

public enum ActivationAuditResult
{
    Succeeded,
    Rejected,
    Blocked,
    ManualReviewRequired
}

public sealed record ActivationAuditEntry(
    string AuditEntryId,
    ActivationAuditAction Action,
    ProductionActivationState FromState,
    ProductionActivationState ToState,
    string CorrelationId,
    string DatabaseIdentityFingerprint,
    string EvidencePackageId,
    string ActorReference,
    DateTimeOffset TimestampUtc,
    ActivationAuditResult Result);

public interface IActivationAuditSink
{
    Task WriteAsync(ActivationAuditEntry entry, CancellationToken cancellationToken = default);
}
