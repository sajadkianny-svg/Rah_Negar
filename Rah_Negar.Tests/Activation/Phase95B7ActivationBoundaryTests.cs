using Rah_Negar.Foundation.Application.Activation;
using Rah_Negar.Foundation.Application.Activation.Preparation;
using Rah_Negar.Foundation.Application.Database.Readiness;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Foundation.Time;
using Rah_Negar.Infrastructure.Activation;

namespace Rah_Negar.Tests.Activation;

public sealed class Phase95B7ActivationBoundaryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
    private const string DatabaseIdentity = "database-fingerprint-001";
    private const string BackupIdentity = "backup-fingerprint-001";
    private const string EvidenceId = "evidence-package-001";
    private const string CorrelationId = "activation-correlation-001";
    private const string StationScope = "station-rasht";
    private const string ShiftProfileId = "shift-profile-1";
    private const int ManagementCredentialVersion = 7;

    [Fact]
    public async Task Complete_prerequisites_record_eligible_but_never_execute_activation()
    {
        var store = new CapturingStore();
        var boundary = new ProductionActivationEligibilityBoundary(new FixedClock(Now), store);

        ProductionActivationEligibilityReceipt result = await boundary.EvaluateAsync(ValidRequest());

        Assert.Equal(ProductionActivationEligibilityDecision.EligibleButNotExecuted, result.Decision);
        Assert.Equal(ProductionActivationState.ApprovedForActivation, result.AuthorityState);
        Assert.True(result.LegacyRemainsAuthoritative);
        Assert.False(result.TargetAuthorityAccepted);
        Assert.False(result.ActivationExecuted);
        Assert.True(result.EvidencePersisted);
        Assert.Single(store.Records);
        Assert.Equal(ActivationAuditAction.ActivationRequested, store.Records[0].Audit.Action);
        Assert.Equal(ActivationAuditResult.ManualReviewRequired, store.Records[0].Audit.Result);
    }

    [Fact]
    public async Task Migration_success_alone_is_blocked_without_the_other_prerequisites()
    {
        ProductionActivationEligibilityRequest request = ValidRequest() with
        {
            GuardRequest = null,
            RollbackReadiness = null,
            ManagementProof = null
        };
        var boundary = new ProductionActivationEligibilityBoundary(new FixedClock(Now), new CapturingStore());

        ProductionActivationEligibilityReceipt result = await boundary.EvaluateAsync(request);

        AssertBlocked(result, "activation-guard-request-required");
        Assert.Contains("rollback-readiness-required", result.Reasons);
        Assert.Contains("management-proof-required", result.Reasons);
        Assert.False(result.ActivationExecuted);
    }

    [Fact]
    public async Task Failed_or_missing_migration_receipt_blocks_activation()
    {
        var boundary = new ProductionActivationEligibilityBoundary(new FixedClock(Now), new CapturingStore());

        ProductionActivationEligibilityReceipt missing = await boundary.EvaluateAsync(
            ValidRequest() with { MigrationExecution = null });
        ProductionActivationEligibilityReceipt failed = await boundary.EvaluateAsync(
            ValidRequest() with
            {
                MigrationExecution = ValidMigration(ProductionMigrationExecutionStatus.Failed,
                    postValidationPassed: false)
            });

        AssertBlocked(missing, "migration-success-receipt-required");
        AssertBlocked(failed, "migration-success-receipt-required");
    }

    [Fact]
    public async Task Stale_or_invalid_migration_receipt_blocks_activation()
    {
        var boundary = new ProductionActivationEligibilityBoundary(new FixedClock(Now), new CapturingStore());

        ProductionActivationEligibilityReceipt stale = await boundary.EvaluateAsync(
            ValidRequest() with
            {
                MigrationExecution = ValidMigration(ProductionMigrationExecutionStatus.Succeeded,
                    completedAt: Now.AddDays(-2))
            });
        ProductionMigrationValidationReceipt invalidReceipt = CreateMigrationReceipt(
            completedAt: Now.AddMinutes(-5), postValidationPassed: false);
        ProductionActivationEligibilityReceipt invalid = await boundary.EvaluateAsync(
            ValidRequest() with
            {
                MigrationExecution = new(ProductionMigrationExecutionStatus.Succeeded,
                    CorrelationId, invalidReceipt.ReceiptId, "invalid", invalidReceipt)
            });

        AssertBlocked(stale, "migration-receipt-invalid-or-stale");
        AssertBlocked(invalid, "migration-receipt-invalid-or-stale");
    }

    [Fact]
    public async Task Wrong_database_or_station_management_scope_blocks_activation()
    {
        var boundary = new ProductionActivationEligibilityBoundary(new FixedClock(Now), new CapturingStore());
        ProductionActivationEligibilityReceipt wrongStation = await boundary.EvaluateAsync(
            ValidRequest() with
            {
                StationScope = "station-ramsar",
                ManagementProof = ValidManagementProof("station-rasht")
            });

        // The database identity mismatch is injected at the migration receipt boundary.
        ProductionMigrationValidationReceipt mismatchedReceipt = CreateMigrationReceipt(
            completedAt: Now.AddMinutes(-5), databaseIdentity: "other-database");
        ProductionActivationEligibilityReceipt wrongDatabase = await boundary.EvaluateAsync(ValidRequest() with
        {
            MigrationExecution = new(ProductionMigrationExecutionStatus.Succeeded,
                CorrelationId, mismatchedReceipt.ReceiptId, "invalid", mismatchedReceipt)
        });

        AssertBlocked(wrongDatabase, "migration-receipt-invalid-or-stale");
        AssertBlocked(wrongStation, "management-proof-WrongScope");
    }

    [Fact]
    public async Task Failed_integrity_backup_or_rollback_readiness_blocks_activation()
    {
        var boundary = new ProductionActivationEligibilityBoundary(new FixedClock(Now), new CapturingStore());
        ProductionActivationEligibilityReceipt failedIntegrity = await boundary.EvaluateAsync(
            ValidRequest() with
            {
                GuardRequest = ValidGuardRequest(backupVerified: false),
                MigrationExecution = ValidMigration(ProductionMigrationExecutionStatus.Succeeded,
                    postValidationPassed: false)
            });
        ProductionActivationEligibilityReceipt failedRollback = await boundary.EvaluateAsync(
            ValidRequest() with
            {
                RollbackReadiness = new(false, false, false, null,
                    RollbackDecisionBoundary.NotEstablished)
            });

        AssertBlocked(failedIntegrity, "guard-backup-not-verified");
        Assert.Contains("migration-receipt-invalid-or-stale", failedIntegrity.Reasons);
        Assert.Contains("rollback-rollback-backup-unavailable", failedRollback.Reasons);
    }

    [Fact]
    public async Task Missing_management_proof_and_explicit_operator_intent_block_activation()
    {
        var boundary = new ProductionActivationEligibilityBoundary(new FixedClock(Now), new CapturingStore());

        ProductionActivationEligibilityReceipt result = await boundary.EvaluateAsync(
            ValidRequest() with { ExplicitOperatorIntent = false, ManagementProof = null });

        AssertBlocked(result, "explicit-operator-intent-required");
        Assert.Contains("management-proof-required", result.Reasons);
    }

    [Fact]
    public void Authority_state_projection_is_explicit_and_preserves_legacy_until_acceptance()
    {
        ProductionActivationState[] preAcceptance =
        [
            ProductionActivationState.NotPrepared,
            ProductionActivationState.AssessmentReady,
            ProductionActivationState.BackupVerified,
            ProductionActivationState.RehearsalVerified,
            ProductionActivationState.ApprovalPending,
            ProductionActivationState.ApprovedForActivation,
            ProductionActivationState.ActivationInProgress,
            ProductionActivationState.ActivationBlocked,
            ProductionActivationState.ActivationRolledBack
        ];

        Assert.All(preAcceptance, state =>
        {
            Assert.True(ProductionActivationAuthoritySafety.LegacyRemainsAuthoritative(state));
            Assert.True(ProductionActivationAuthoritySafety.TargetAuthorityNotAccepted(state));
        });
        Assert.True(ProductionActivationAuthoritySafety.TransitionNotStarted(
            ProductionActivationState.ApprovedForActivation));
        Assert.True(ProductionActivationAuthoritySafety.TransitionFailedWithoutAuthorityChange(
            ProductionActivationState.ActivationBlocked));
        Assert.True(ProductionActivationAuthoritySafety.TransitionEligibleButNotExecuted(
            ProductionActivationState.ApprovedForActivation));
        Assert.True(ProductionActivationAuthoritySafety.CompletedOnlyThroughExplicitAcceptance(
            ProductionActivationState.Activated));
        Assert.False(ProductionActivationAuthoritySafety.LegacyRemainsAuthoritative(
            ProductionActivationState.Activated));
    }

    [Fact]
    public async Task Evidence_store_failure_returns_deterministic_blocked_result()
    {
        var boundary = new ProductionActivationEligibilityBoundary(new FixedClock(Now),
            new CapturingStore { Succeeds = false });

        ProductionActivationEligibilityReceipt result = await boundary.EvaluateAsync(ValidRequest());

        AssertBlocked(result, "activation-evidence-persistence-failed");
        Assert.False(result.EvidencePersisted);
        Assert.False(result.TargetAuthorityAccepted);
    }

    [Fact]
    public async Task File_evidence_store_appends_only_non_secret_receipt_and_audit()
    {
        string directory = Path.Combine(Path.GetTempPath(), "RahNegar.Tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "activation-evidence.jsonl");
        try
        {
            using var store = new FileActivationDecisionEvidenceStore(path);
            ProductionActivationEligibilityReceipt receipt = await new ProductionActivationEligibilityBoundary(
                new FixedClock(Now), store).EvaluateAsync(ValidRequest());

            string content = await File.ReadAllTextAsync(path);
            Assert.True(receipt.EvidencePersisted);
            Assert.Contains(receipt.ReceiptId, content, StringComparison.Ordinal);
            Assert.Contains("ActivationRequested", content, StringComparison.Ordinal);
            Assert.DoesNotContain("Password", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Salt", content, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static void AssertBlocked(ProductionActivationEligibilityReceipt result, string reason) {
        Assert.Equal(ProductionActivationEligibilityDecision.Blocked, result.Decision);
        Assert.Equal(ProductionActivationState.ActivationBlocked, result.AuthorityState);
        Assert.Contains(reason, result.Reasons);
        Assert.True(result.LegacyRemainsAuthoritative);
        Assert.False(result.TargetAuthorityAccepted);
        Assert.False(result.ActivationExecuted);
    }

    private static ProductionActivationEligibilityRequest ValidRequest() => new(
        "activation-request-001", StationScope, ShiftProfileId, true,
        ProductionActivationState.ApprovedForActivation, ValidGuardRequest(),
        ValidMigration(ProductionMigrationExecutionStatus.Succeeded),
        new(true, true, true, "rollback-owner-1", RollbackDecisionBoundary.ManualDecisionRequired),
        ValidManagementProof(StationScope), ManagementCredentialVersion, Now.AddMinutes(-1));

    private static ProductionActivationGuardRequest ValidGuardRequest(bool backupVerified = true) =>
        new(CompleteReadiness(), CompletePreflight(), CompleteClassification(),
            CompleteBackup(backupVerified), CompleteRehearsal(), CompleteEvidence(backupVerified),
            ValidApproval(), ProductionActivationScope.UnifiedMigrationActivation);

    private static ProductionMigrationExecutionResult ValidMigration(
        ProductionMigrationExecutionStatus status,
        bool postValidationPassed = true,
        DateTimeOffset? completedAt = null) {
        ProductionMigrationValidationReceipt receipt = CreateMigrationReceipt(
            completedAt ?? Now.AddMinutes(-5), postValidationPassed: postValidationPassed);
        return new(status, CorrelationId, receipt.ReceiptId, "MigrationResult", receipt);
    }

    private static ProductionMigrationValidationReceipt CreateMigrationReceipt(
        DateTimeOffset completedAt,
        bool postValidationPassed = true,
        string databaseIdentity = DatabaseIdentity) => new(
        "migration-receipt-001", CorrelationId, databaseIdentity, BackupIdentity,
        MigrationHistoryClassification.CleanLegacyBaseline, 0, 4,
        ["target-database-foundation-v1"], false, true, true, postValidationPassed,
        CompletePreservation(postValidationPassed), true, true,
        postValidationPassed ? OperationalRollbackState.ValidationPassed :
            OperationalRollbackState.ValidationFailed, completedAt);

    private static ManagementAuthorizationProof ValidManagementProof(string station) => new(
        ShiftProfileId, ProtectedAction.Migration,
        $"{ProductionActivationEligibilityBoundary.ActivationActionScopePrefix}:{station}",
        ManagementCredentialVersion, Now.AddMinutes(-2), Now.AddMinutes(10), CorrelationId);

    private static ProductionActivationApproval ValidApproval() => new(
        "approval-001", ShiftProfileId, Now.AddMinutes(-3),
        ProductionActivationScope.UnifiedMigrationActivation, DatabaseIdentity,
        EvidenceId, CorrelationId, Now.AddMinutes(30));

    private static MaintenanceWindowReadinessResult CompleteReadiness() => new(
        MaintenanceReadinessStatus.ReadyForFutureMigrationApproval, [], []);

    private static MigrationHistoryClassificationResult CompleteClassification() => new(
        MigrationHistoryClassification.CleanLegacyBaseline, 4, []);

    private static DatabasePreflightResult CompletePreflight() {
        var target = new DatabaseTargetDescriptor("explicit.sqlite", 4096, Now.AddHours(-1),
            Now.AddMinutes(-30), DatabaseIdentity);
        return new(true, new(true, target, DatabaseTargetFailure.None, "ValidExplicitSqliteTarget"),
            true, true, ["ok"], [], 1, 0,
            new(false, false, false, null, [], null), [], new Dictionary<string, long>(),
            [], [], new(InspectedEsdValueState.Valid, "2.5", 1),
            new(InspectedEsdValueState.Absent, null, 0),
            new(0, new Dictionary<string, string>(), 0, new Dictionary<string, string>()),
            "wal", true, false, []);
    }

    private static DatabaseBackupVerificationResult CompleteBackup(bool verified = true) {
        var source = new DatabaseTargetDescriptor("explicit.sqlite", 4096, Now.AddHours(-1),
            Now.AddMinutes(-30), DatabaseIdentity);
        var backup = new DatabaseTargetDescriptor("backup.sqlite", 4096, Now.AddMinutes(-20),
            Now.AddMinutes(-20), BackupIdentity);
        return new(verified, "backup.sqlite", source, backup, "safe-file-checksum", 4096,
            Now.AddMinutes(-20), 0, MigrationHistoryClassification.CleanLegacyBaseline,
            verified, verified ? DatabaseBackupFailure.None : DatabaseBackupFailure.IntegrityFailed, []);
    }

    private static MigrationRehearsalResult CompleteRehearsal() => new(
        true, MigrationRehearsalFailure.None, 0, 4, ["target-database-foundation-v1"],
        true, true, CompletePreservation(true), EsdReconciliationState.ReadyToProvision,
        EsdAuthorityMode.LegacyAuthoritative, []);

    private static PreservationVerificationResult CompletePreservation(bool passed) => new(
        passed, passed, passed, passed, passed, passed, passed, passed, passed, passed, passed, []);

    private static ActivationEvidencePackage CompleteEvidence(bool backupVerified = true) => new(
        EvidenceId, CorrelationId, DatabaseIdentity,
        new(DatabaseIdentity, true, true, true, true, true, Now.AddMinutes(-30)),
        new(MigrationHistoryClassification.CleanLegacyBaseline, 4, true, true),
        new("backup-receipt-001", DatabaseIdentity, BackupIdentity, backupVerified,
            backupVerified, 4096, Now.AddMinutes(-20)),
        new("rehearsal-receipt-001", true, true, true, 4,
            EsdReconciliationState.ReadyToProvision, EsdAuthorityMode.LegacyAuthoritative,
            new(true, true, true, true, true, true, true), Now.AddMinutes(-10)),
        new(true, true, backupVerified, true),
        new(true, ProductionActivationScope.UnifiedMigrationActivation,
            DatabaseIdentity, EvidenceId), Now.AddMinutes(-5));

    private sealed record FixedClock(DateTimeOffset UtcNow) : IClock
    {
        public DateTimeOffset LocalNow => UtcNow.ToOffset(TimeSpan.FromHours(3.5));
    }

    private sealed class CapturingStore : IActivationDecisionEvidenceStore
    {
        public bool Succeeds { get; init; } = true;
        public List<(ProductionActivationEligibilityReceipt Receipt, ActivationAuditEntry Audit)> Records { get; } = [];

        public Task<bool> TryAppendAsync(ProductionActivationEligibilityReceipt receipt,
            ActivationAuditEntry auditEntry, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Succeeds) Records.Add((receipt, auditEntry));
            return Task.FromResult(Succeeds);
        }
    }
}
