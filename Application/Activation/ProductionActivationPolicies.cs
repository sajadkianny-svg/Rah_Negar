using Rah_Negar.Foundation.Application.Database.Readiness;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Foundation.Time;

namespace Rah_Negar.Foundation.Application.Activation;

public static class ProductionActivationStateTransitionPolicy
{
    private static readonly IReadOnlyDictionary<ProductionActivationState, ProductionActivationState[]> Allowed =
        new Dictionary<ProductionActivationState, ProductionActivationState[]>
        {
            [ProductionActivationState.NotPrepared] =
                [ProductionActivationState.AssessmentReady, ProductionActivationState.ActivationBlocked],
            [ProductionActivationState.AssessmentReady] =
                [ProductionActivationState.BackupVerified, ProductionActivationState.ActivationBlocked],
            [ProductionActivationState.BackupVerified] =
                [ProductionActivationState.RehearsalVerified, ProductionActivationState.ActivationBlocked],
            [ProductionActivationState.RehearsalVerified] =
                [ProductionActivationState.ApprovalPending, ProductionActivationState.ActivationBlocked],
            [ProductionActivationState.ApprovalPending] =
                [ProductionActivationState.ApprovedForActivation, ProductionActivationState.ActivationBlocked],
            [ProductionActivationState.ApprovedForActivation] =
                [ProductionActivationState.ActivationInProgress, ProductionActivationState.ActivationBlocked],
            [ProductionActivationState.ActivationInProgress] =
                [ProductionActivationState.Activated, ProductionActivationState.ActivationBlocked,
                    ProductionActivationState.ActivationRolledBack],
            [ProductionActivationState.Activated] =
                [ProductionActivationState.ActivationRolledBack],
            [ProductionActivationState.ActivationBlocked] =
                [ProductionActivationState.NotPrepared],
            [ProductionActivationState.ActivationRolledBack] =
                [ProductionActivationState.NotPrepared]
        };

    public static ActivationStateTransitionResult Evaluate(ActivationStateTransitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.TransitionId) ||
            string.IsNullOrWhiteSpace(request.CorrelationId) ||
            string.IsNullOrWhiteSpace(request.ActorReference) ||
            request.RequestedAtUtc.Offset != TimeSpan.Zero)
            return new(false, request.From, "InvalidTransitionEvidence");
        bool allowed = Allowed.TryGetValue(request.From, out ProductionActivationState[]? targets) &&
            targets.Contains(request.To);
        return allowed
            ? new(true, request.To, "ExplicitTransitionAccepted")
            : new(false, request.From, "InvalidStateTransition");
    }

    public static IReadOnlyList<ProductionActivationState> AllowedTargets(ProductionActivationState from) =>
        Allowed.TryGetValue(from, out ProductionActivationState[]? targets)
            ? Array.AsReadOnly(targets.ToArray())
            : Array.Empty<ProductionActivationState>();
}

public static class ActivationEvidencePackageValidator
{
    public static ActivationEvidenceValidationResult Validate(ActivationEvidencePackage? package)
    {
        var issues = new List<string>();
        if (package is null) return new(false, ["evidence-package-required"]);
        AddBlank(package.EvidencePackageId, "evidence-package-id-required");
        AddBlank(package.CorrelationId, "correlation-id-required");
        AddBlank(package.DatabaseIdentityFingerprint, "database-identity-required");
        if (package.AssembledAtUtc.Offset != TimeSpan.Zero) issues.Add("assembled-time-must-be-utc");

        if (package.Preflight is null) issues.Add("preflight-evidence-required");
        else
        {
            if (!StringComparer.Ordinal.Equals(package.DatabaseIdentityFingerprint,
                    package.Preflight.DatabaseIdentityFingerprint))
                issues.Add("preflight-database-identity-mismatch");
            if (!package.Preflight.Succeeded || !package.Preflight.HeaderValid ||
                !package.Preflight.IntegrityPassed || !package.Preflight.ForeignKeyIntegrityPassed ||
                !package.Preflight.ReadOnlyEnforced)
                issues.Add("preflight-not-complete");
            if (package.Preflight.InspectedAtUtc.Offset != TimeSpan.Zero)
                issues.Add("preflight-time-must-be-utc");
            if (package.Preflight.InspectedAtUtc > package.AssembledAtUtc)
                issues.Add("preflight-after-package-assembly");
        }

        if (package.Migration is null) issues.Add("migration-evidence-required");
        else if (!package.Migration.ChainSupported || !package.Migration.ChecksumValidated ||
                 package.Migration.SupportedTargetVersion <= 0 || package.Migration.Classification is not
                     (MigrationHistoryClassification.CleanLegacyBaseline or
                      MigrationHistoryClassification.CleanUnifiedTarget))
            issues.Add("migration-evidence-not-approved");

        if (package.BackupReceipt is null) issues.Add("backup-receipt-required");
        else
        {
            AddBlank(package.BackupReceipt.ReceiptId, "backup-receipt-id-required");
            AddBlank(package.BackupReceipt.BackupIdentityFingerprint, "backup-identity-required");
            if (!StringComparer.Ordinal.Equals(package.DatabaseIdentityFingerprint,
                    package.BackupReceipt.SourceDatabaseIdentityFingerprint))
                issues.Add("backup-source-identity-mismatch");
            if (!package.BackupReceipt.Verified || !package.BackupReceipt.IntegrityPassed ||
                package.BackupReceipt.SizeBytes <= 0)
                issues.Add("backup-not-verified");
            if (package.BackupReceipt.CreatedAtUtc.Offset != TimeSpan.Zero)
                issues.Add("backup-time-must-be-utc");
            if (package.BackupReceipt.CreatedAtUtc > package.AssembledAtUtc)
                issues.Add("backup-after-package-assembly");
        }

        if (package.RehearsalReceipt is null) issues.Add("rehearsal-receipt-required");
        else
        {
            AddBlank(package.RehearsalReceipt.ReceiptId, "rehearsal-receipt-id-required");
            if (!package.RehearsalReceipt.Passed || !package.RehearsalReceipt.IdempotentRerun ||
                !package.RehearsalReceipt.OriginalBackupUnchanged || package.RehearsalReceipt.FinalVersion <= 0)
                issues.Add("rehearsal-not-verified");
            if (package.RehearsalReceipt.EsdAuthorityMode != EsdAuthorityMode.LegacyAuthoritative ||
                IsEsdConflict(package.RehearsalReceipt.EsdReconciliationState))
                issues.Add("esd-reconciliation-not-safe");
            if (package.RehearsalReceipt.CompletedAtUtc.Offset != TimeSpan.Zero)
                issues.Add("rehearsal-time-must-be-utc");
            if (package.RehearsalReceipt.CompletedAtUtc > package.AssembledAtUtc)
                issues.Add("rehearsal-after-package-assembly");
            ActivationSnapshotPreservationEvidence? preservation = package.RehearsalReceipt.Preservation;
            if (preservation is null || !preservation.Passed || !preservation.FinalizedSnapshotsPreserved ||
                !preservation.ReportLocksPreserved || !preservation.LegacyEvidencePreserved ||
                !preservation.EsdValuePreserved || !preservation.NoRbacIntroduced ||
                !preservation.NoSupportIdentityIntroduced)
                issues.Add("preservation-evidence-not-complete");
        }

        if (package.Integrity is null || !package.Integrity.PreflightIntegrityPassed ||
            !package.Integrity.ForeignKeyIntegrityPassed || !package.Integrity.BackupIntegrityPassed ||
            !package.Integrity.RehearsalIntegrityPassed)
            issues.Add("integrity-evidence-not-complete");

        if (package.ApprovalBoundary is null || !package.ApprovalBoundary.ExplicitApprovalRequired ||
            !StringComparer.Ordinal.Equals(package.DatabaseIdentityFingerprint,
                package.ApprovalBoundary.TargetDatabaseIdentityFingerprint) ||
            !StringComparer.Ordinal.Equals(package.EvidencePackageId,
                package.ApprovalBoundary.EvidencePackageId))
            issues.Add("approval-boundary-not-complete");

        return new(issues.Count == 0, issues.AsReadOnly());

        void AddBlank(string? value, string issue)
        {
            if (string.IsNullOrWhiteSpace(value)) issues.Add(issue);
        }
    }

    internal static bool IsEsdConflict(EsdReconciliationState state) => state is
        EsdReconciliationState.Conflict or
        EsdReconciliationState.TargetAlreadyProvisionedDifferentValue or
        EsdReconciliationState.LegacyValueInvalid or
        EsdReconciliationState.Failed;
}

public static class ProductionActivationApprovalValidator
{
    public static ActivationApprovalValidationResult Validate(
        ProductionActivationApproval? approval,
        ProductionActivationScope expectedScope,
        string expectedDatabaseIdentity,
        string expectedEvidencePackageId,
        string expectedCorrelationId,
        DateTimeOffset nowUtc)
    {
        bool expirationMalformed = approval?.ExpiresAtUtc is { } suppliedExpiry &&
            (suppliedExpiry.Offset != TimeSpan.Zero || suppliedExpiry <= approval.ApprovedAtUtc);
        if (approval is null || string.IsNullOrWhiteSpace(approval.ApprovalId) ||
            string.IsNullOrWhiteSpace(approval.ApprovedByActorReference) ||
            string.IsNullOrWhiteSpace(approval.TargetDatabaseIdentityFingerprint) ||
            string.IsNullOrWhiteSpace(approval.EvidencePackageId) ||
            string.IsNullOrWhiteSpace(approval.CorrelationId) ||
            approval.ApprovedAtUtc.Offset != TimeSpan.Zero || nowUtc.Offset != TimeSpan.Zero ||
            expirationMalformed)
            return Fail(ActivationApprovalFailure.Malformed);
        if (approval.ApprovedAtUtc > nowUtc) return Fail(ActivationApprovalFailure.NotYetValid);
        if (approval.ExpiresAtUtc is { } expiry && nowUtc >= expiry)
            return Fail(ActivationApprovalFailure.Expired);
        if (approval.ApprovedScope != expectedScope) return Fail(ActivationApprovalFailure.WrongScope);
        if (!StringComparer.Ordinal.Equals(approval.TargetDatabaseIdentityFingerprint, expectedDatabaseIdentity))
            return Fail(ActivationApprovalFailure.WrongDatabaseIdentity);
        if (!StringComparer.Ordinal.Equals(approval.EvidencePackageId, expectedEvidencePackageId))
            return Fail(ActivationApprovalFailure.WrongEvidencePackage);
        if (!StringComparer.Ordinal.Equals(approval.CorrelationId, expectedCorrelationId))
            return Fail(ActivationApprovalFailure.WrongCorrelation);
        return new(true, ActivationApprovalFailure.None, "ApprovalValid");

        static ActivationApprovalValidationResult Fail(ActivationApprovalFailure failure) =>
            new(false, failure, failure.ToString());
    }
}

public sealed class ProductionActivationGuard
{
    private readonly IClock _clock;

    public ProductionActivationGuard(IClock clock) =>
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public ProductionActivationGuardResult Evaluate(ProductionActivationGuardRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reasons = new List<string>();
        if (request.MigrationClassification.Classification is
            MigrationHistoryClassification.HistoricalDraftRecognized or
            MigrationHistoryClassification.AdoptionRequired)
            return new(ActivationGuardDecision.RequiresManualReview,
                ["migration-adoption-requires-manual-review"]);

        if (request.Readiness.Status != MaintenanceReadinessStatus.ReadyForFutureMigrationApproval ||
            request.Readiness.Blockers.Count > 0) reasons.Add("readiness-report-not-ready");
        if (!request.Preflight.Succeeded || !request.Preflight.HeaderValid ||
            !request.Preflight.IntegrityPassed || request.Preflight.ForeignKeyViolations.Count > 0 ||
            !request.Preflight.ReadOnlyConnectionEnforced)
            reasons.Add("preflight-integrity-not-passed");
        if (!request.MigrationClassification.IsMigrationChainSupported ||
            request.MigrationClassification.Classification is not
                (MigrationHistoryClassification.CleanLegacyBaseline or
                 MigrationHistoryClassification.CleanUnifiedTarget))
            reasons.Add("migration-classification-blocked");
        if (!request.Backup.IsVerified || !request.Backup.IntegrityPassed ||
            request.Backup.Failure != DatabaseBackupFailure.None)
            reasons.Add("backup-not-verified");
        if (!request.Rehearsal.Passed || !request.Rehearsal.IdempotentRerun ||
            !request.Rehearsal.OriginalBackupUnchanged || request.Rehearsal.Preservation is not { Passed: true })
            reasons.Add("rehearsal-not-verified");
        if (request.Rehearsal.EsdAuthorityMode != EsdAuthorityMode.LegacyAuthoritative ||
            ActivationEvidencePackageValidator.IsEsdConflict(request.Rehearsal.EsdReconciliationState))
            reasons.Add("esd-reconciliation-blocked");

        ActivationEvidenceValidationResult evidence =
            ActivationEvidencePackageValidator.Validate(request.EvidencePackage);
        if (!evidence.IsComplete) reasons.Add("evidence-package-incomplete");
        DatabaseTargetDescriptor? selectedTarget = request.Preflight.TargetInspection.Target;
        DatabaseTargetDescriptor? backupSource = request.Backup.SourceIdentity;
        DatabaseTargetDescriptor? backupIdentity = request.Backup.BackupIdentity;
        if (selectedTarget is null || backupSource is null || backupIdentity is null ||
            !StringComparer.Ordinal.Equals(selectedTarget.IdentityFingerprint,
                request.EvidencePackage.DatabaseIdentityFingerprint) ||
            !StringComparer.Ordinal.Equals(backupSource.IdentityFingerprint,
                request.EvidencePackage.DatabaseIdentityFingerprint) ||
            !StringComparer.Ordinal.Equals(backupIdentity.IdentityFingerprint,
                request.EvidencePackage.BackupReceipt.BackupIdentityFingerprint) ||
            request.EvidencePackage.Migration.Classification !=
                request.MigrationClassification.Classification ||
            request.EvidencePackage.RehearsalReceipt.FinalVersion != request.Rehearsal.FinalVersion ||
            request.EvidencePackage.RehearsalReceipt.EsdReconciliationState !=
                request.Rehearsal.EsdReconciliationState ||
            request.EvidencePackage.ApprovalBoundary.RequiredScope != request.RequiredScope)
            reasons.Add("evidence-does-not-bind-readiness-results");
        ActivationApprovalValidationResult approval = ProductionActivationApprovalValidator.Validate(
            request.Approval, request.RequiredScope, request.EvidencePackage.DatabaseIdentityFingerprint,
            request.EvidencePackage.EvidencePackageId, request.EvidencePackage.CorrelationId,
            _clock.UtcNow.ToUniversalTime());
        if (!approval.IsValid) reasons.Add($"approval-{approval.ResultCategory}");
        return new(reasons.Count == 0 ? ActivationGuardDecision.Allowed : ActivationGuardDecision.Blocked,
            reasons.AsReadOnly());
    }
}

public static class ActivationAuditEntryValidator
{
    public static bool IsSafeAndComplete(ActivationAuditEntry? entry) => entry is not null &&
        !string.IsNullOrWhiteSpace(entry.AuditEntryId) &&
        !string.IsNullOrWhiteSpace(entry.CorrelationId) &&
        !string.IsNullOrWhiteSpace(entry.DatabaseIdentityFingerprint) &&
        !string.IsNullOrWhiteSpace(entry.EvidencePackageId) &&
        !string.IsNullOrWhiteSpace(entry.ActorReference) &&
        entry.TimestampUtc.Offset == TimeSpan.Zero;
}

public static class ApprovedProductionMigrationContextValidator
{
    public static bool IsValid(ApprovedProductionMigrationContext? context, DateTimeOffset nowUtc)
    {
        if (context is null || string.IsNullOrWhiteSpace(context.ExplicitDatabasePath) ||
            !Path.IsPathFullyQualified(context.ExplicitDatabasePath) ||
            context.GuardResult.Decision != ActivationGuardDecision.Allowed || nowUtc.Offset != TimeSpan.Zero)
            return false;
        ActivationEvidencePackage evidence = context.EvidencePackage;
        ExplicitProductionMigrationAuthorization authorization = context.Authorization;
        if (!ActivationEvidencePackageValidator.Validate(evidence).IsComplete ||
            string.IsNullOrWhiteSpace(authorization.AuthorizationId) ||
            string.IsNullOrWhiteSpace(authorization.AuthorizedByActorReference) ||
            authorization.IssuedAtUtc.Offset != TimeSpan.Zero || authorization.ExpiresAtUtc.Offset != TimeSpan.Zero ||
            authorization.IssuedAtUtc > nowUtc || authorization.ExpiresAtUtc <= nowUtc ||
            authorization.ExpiresAtUtc <= authorization.IssuedAtUtc)
            return false;
        ActivationApprovalValidationResult approval = ProductionActivationApprovalValidator.Validate(
            context.Approval, ProductionActivationScope.UnifiedMigrationActivation,
            evidence.DatabaseIdentityFingerprint, evidence.EvidencePackageId, evidence.CorrelationId, nowUtc);
        return approval.IsValid &&
            StringComparer.Ordinal.Equals(authorization.ApprovalId, context.Approval.ApprovalId) &&
            StringComparer.Ordinal.Equals(authorization.EvidencePackageId, evidence.EvidencePackageId) &&
            StringComparer.Ordinal.Equals(authorization.TargetDatabaseIdentityFingerprint,
                evidence.DatabaseIdentityFingerprint) &&
            StringComparer.Ordinal.Equals(authorization.CorrelationId, evidence.CorrelationId);
    }
}
