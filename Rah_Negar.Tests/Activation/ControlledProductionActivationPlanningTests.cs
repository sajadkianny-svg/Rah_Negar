using System.Reflection;
using Rah_Negar.Foundation.Application.Activation;
using Rah_Negar.Foundation.Application.Database.Readiness;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Foundation.Time;

namespace Rah_Negar.Tests.Activation;

public sealed class ControlledProductionActivationPlanningTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
    private const string DatabaseIdentity = "database-fingerprint-001";
    private const string BackupIdentity = "backup-fingerprint-001";
    private const string EvidenceId = "evidence-package-001";
    private const string CorrelationId = "activation-correlation-001";

    [Theory]
    [InlineData(ProductionActivationState.NotPrepared, ProductionActivationState.AssessmentReady)]
    [InlineData(ProductionActivationState.AssessmentReady, ProductionActivationState.BackupVerified)]
    [InlineData(ProductionActivationState.BackupVerified, ProductionActivationState.RehearsalVerified)]
    [InlineData(ProductionActivationState.RehearsalVerified, ProductionActivationState.ApprovalPending)]
    [InlineData(ProductionActivationState.ApprovalPending, ProductionActivationState.ApprovedForActivation)]
    [InlineData(ProductionActivationState.ApprovedForActivation, ProductionActivationState.ActivationInProgress)]
    [InlineData(ProductionActivationState.ActivationInProgress, ProductionActivationState.Activated)]
    [InlineData(ProductionActivationState.Activated, ProductionActivationState.ActivationRolledBack)]
    public void Activation_state_machine_accepts_only_explicit_next_transitions(
        ProductionActivationState from, ProductionActivationState to)
    {
        var request = new ActivationStateTransitionRequest(from, to, "transition-1",
            CorrelationId, "operator-1", Now);

        ActivationStateTransitionResult result = ProductionActivationStateTransitionPolicy.Evaluate(request);

        Assert.True(result.Accepted);
        Assert.Equal(to, result.State);
    }

    [Theory]
    [InlineData(ProductionActivationState.NotPrepared, ProductionActivationState.Activated)]
    [InlineData(ProductionActivationState.AssessmentReady, ProductionActivationState.RehearsalVerified)]
    [InlineData(ProductionActivationState.ApprovalPending, ProductionActivationState.ActivationInProgress)]
    [InlineData(ProductionActivationState.Activated, ProductionActivationState.ApprovedForActivation)]
    [InlineData(ProductionActivationState.NotPrepared, ProductionActivationState.NotPrepared)]
    public void Activation_state_machine_rejects_skips_reversal_and_automatic_promotion(
        ProductionActivationState from, ProductionActivationState to)
    {
        ActivationStateTransitionResult result = ProductionActivationStateTransitionPolicy.Evaluate(
            new(from, to, "transition-1", CorrelationId, "operator-1", Now));

        Assert.False(result.Accepted);
        Assert.Equal(from, result.State);
        Assert.Equal("InvalidStateTransition", result.ResultCategory);
    }

    [Fact]
    public void Activation_state_transition_requires_safe_explicit_evidence()
    {
        ActivationStateTransitionResult result = ProductionActivationStateTransitionPolicy.Evaluate(
            new(ProductionActivationState.NotPrepared, ProductionActivationState.AssessmentReady,
                "", CorrelationId, "operator-1", Now));

        Assert.False(result.Accepted);
        Assert.Equal("InvalidTransitionEvidence", result.ResultCategory);
    }

    [Fact]
    public void Complete_evidence_package_passes_and_incomplete_package_fails_closed()
    {
        ActivationEvidencePackage complete = CompleteEvidence();
        ActivationEvidencePackage incomplete = CompleteEvidence(backupVerified: false);

        ActivationEvidenceValidationResult valid = ActivationEvidencePackageValidator.Validate(complete);
        ActivationEvidenceValidationResult invalid = ActivationEvidencePackageValidator.Validate(incomplete);

        Assert.True(valid.IsComplete);
        Assert.Empty(valid.Issues);
        Assert.False(invalid.IsComplete);
        Assert.Contains("backup-not-verified", invalid.Issues);
    }

    [Fact]
    public void Evidence_and_audit_contracts_have_no_secret_material_fields()
    {
        string[] forbidden =
            ["Password", "PasswordHash", "Salt", "CredentialVerifier", "PrivateKey", "RecoveryCode",
                "SupportAuthorizationSecret", "RawAuthorization"];
        Type[] contracts = typeof(ActivationEvidencePackage).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(ActivationEvidencePackage).Namespace)
            .ToArray();
        string propertyNames = string.Join('|', contracts.SelectMany(type =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)).Select(property => property.Name));

        foreach (string name in forbidden)
            Assert.DoesNotContain(name, propertyNames, StringComparison.OrdinalIgnoreCase);
        Assert.True(ActivationAuditEntryValidator.IsSafeAndComplete(new(
            "audit-1", ActivationAuditAction.GuardEvaluated,
            ProductionActivationState.ApprovalPending, ProductionActivationState.ApprovedForActivation,
            CorrelationId, DatabaseIdentity, EvidenceId, "operator-1", Now,
            ActivationAuditResult.Succeeded)));
    }

    [Fact]
    public void Approval_validation_accepts_only_matching_current_explicit_approval()
    {
        ProductionActivationApproval approval = ValidApproval();

        ActivationApprovalValidationResult result = ProductionActivationApprovalValidator.Validate(
            approval, ProductionActivationScope.UnifiedMigrationActivation, DatabaseIdentity,
            EvidenceId, CorrelationId, Now);

        Assert.True(result.IsValid);
        Assert.Equal(ActivationApprovalFailure.None, result.Failure);
    }

    [Fact]
    public void Approval_validation_rejects_expired_and_wrong_database_identity()
    {
        ProductionActivationApproval expired = ValidApproval() with { ExpiresAtUtc = Now };
        ProductionActivationApproval wrongDatabase = ValidApproval() with
        {
            TargetDatabaseIdentityFingerprint = "another-database"
        };

        ActivationApprovalValidationResult expiredResult = ProductionActivationApprovalValidator.Validate(
            expired, ProductionActivationScope.UnifiedMigrationActivation, DatabaseIdentity,
            EvidenceId, CorrelationId, Now);
        ActivationApprovalValidationResult wrongResult = ProductionActivationApprovalValidator.Validate(
            wrongDatabase, ProductionActivationScope.UnifiedMigrationActivation, DatabaseIdentity,
            EvidenceId, CorrelationId, Now);

        Assert.Equal(ActivationApprovalFailure.Expired, expiredResult.Failure);
        Assert.Equal(ActivationApprovalFailure.WrongDatabaseIdentity, wrongResult.Failure);
    }

    [Fact]
    public void Activation_guard_allows_only_complete_bound_evidence_and_approval()
    {
        var guard = new ProductionActivationGuard(new FixedClock(Now));

        ProductionActivationGuardResult result = guard.Evaluate(CompleteGuardRequest());

        Assert.Equal(ActivationGuardDecision.Allowed, result.Decision);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void Activation_guard_blocks_missing_approval_and_failed_backup()
    {
        var guard = new ProductionActivationGuard(new FixedClock(Now));
        ProductionActivationGuardRequest request = CompleteGuardRequest();
        DatabaseBackupVerificationResult failedBackup = request.Backup with
        {
            IsVerified = false,
            IntegrityPassed = false,
            Failure = DatabaseBackupFailure.IntegrityFailed
        };

        ProductionActivationGuardResult result = guard.Evaluate(request with
        {
            Backup = failedBackup,
            Approval = null
        });

        Assert.Equal(ActivationGuardDecision.Blocked, result.Decision);
        Assert.Contains("backup-not-verified", result.Reasons);
        Assert.Contains(result.Reasons, reason => reason.StartsWith("approval-", StringComparison.Ordinal));
    }

    [Fact]
    public void Activation_guard_routes_adoption_states_to_manual_review()
    {
        var guard = new ProductionActivationGuard(new FixedClock(Now));
        ProductionActivationGuardRequest request = CompleteGuardRequest();
        var adoption = new MigrationHistoryClassificationResult(
            MigrationHistoryClassification.HistoricalDraftRecognized, 4, ["recognized-draft"]);

        ProductionActivationGuardResult result = guard.Evaluate(request with
        {
            MigrationClassification = adoption
        });

        Assert.Equal(ActivationGuardDecision.RequiresManualReview, result.Decision);
    }

    [Fact]
    public void Rollback_readiness_requires_verified_restore_owner_and_manual_decision_boundary()
    {
        RollbackReadinessResult ready = RollbackReadinessEvaluator.Evaluate(
            new(true, true, true, "rollback-owner-1", RollbackDecisionBoundary.ManualDecisionRequired));
        RollbackReadinessResult blocked = RollbackReadinessEvaluator.Evaluate(
            new(true, false, false, null, RollbackDecisionBoundary.NotEstablished));

        Assert.Equal(RollbackReadinessStatus.Ready, ready.Status);
        Assert.Equal(RollbackReadinessStatus.Blocked, blocked.Status);
        Assert.Contains("rollback-owner-not-assigned", blocked.Blockers);
        Assert.Contains("rollback-decision-boundary-not-established", blocked.Blockers);
    }

    [Fact]
    public void Cutover_checklist_requires_every_technical_operational_and_security_item()
    {
        ProductionCutoverChecklist pending =
            ProductionCutoverChecklistEvaluator.CreateDisabledPlanningChecklist();
        ProductionCutoverChecklistResult pendingResult =
            ProductionCutoverChecklistEvaluator.Evaluate(pending);
        var confirmed = new ProductionCutoverChecklist(pending.Entries.Select(entry =>
            entry with { Status = ChecklistItemStatus.Confirmed, EvidenceReference = $"evidence:{entry.Item}" }));
        ProductionCutoverChecklistResult confirmedResult =
            ProductionCutoverChecklistEvaluator.Evaluate(confirmed);
        var missing = new ProductionCutoverChecklist(confirmed.Entries.Skip(1));

        Assert.True(pendingResult.IsComplete);
        Assert.False(pendingResult.AllConfirmed);
        Assert.True(confirmedResult.IsComplete);
        Assert.True(confirmedResult.AllConfirmed);
        Assert.False(ProductionCutoverChecklistEvaluator.Evaluate(missing).IsComplete);
        Assert.Equal(Enum.GetValues<ProductionCutoverChecklistItem>().Length, confirmed.Entries.Count);
    }

    [Fact]
    public void Every_feature_activation_boundary_remains_disabled()
    {
        FeatureActivationBoundarySnapshot snapshot = FeatureActivationBoundarySnapshot.Inactive;

        Assert.Equal(Enum.GetValues<ControlledProductionFeature>().Length, snapshot.Entries.Count);
        Assert.All(snapshot.Entries, entry => Assert.Equal(FeatureActivationState.Disabled, entry.State));
        Assert.DoesNotContain(snapshot.Entries, entry => entry.State == FeatureActivationState.Enabled);
    }

    [Fact]
    public async Task Production_executor_exists_only_as_contract_and_test_double_requires_approved_context()
    {
        Type[] productionTypes = typeof(IProductionMigrationExecutor).Assembly.GetTypes();
        Assert.DoesNotContain(productionTypes, type => type.IsClass && !type.IsAbstract &&
            typeof(IProductionMigrationExecutor).IsAssignableFrom(type));
        Assert.DoesNotContain(productionTypes, type => type.IsClass && !type.IsAbstract &&
            typeof(IFutureFeatureActivationExecutor).IsAssignableFrom(type));

        var testDouble = new ValidatingMigrationExecutorTestDouble();
        ApprovedProductionMigrationContext context = ApprovedContext();
        ProductionMigrationExecutionResult result = await testDouble.ExecuteAsync(context);

        Assert.Equal(ProductionMigrationExecutionStatus.Succeeded, result.Status);
        Assert.Equal(1, testDouble.CallCount);
    }

    [Fact]
    public void Current_authority_comparison_exposes_all_six_activation_gaps()
    {
        ProductionReadinessComparison comparison = ProductionReadinessComparison.CreateCurrent();

        Assert.Equal(Enum.GetValues<ProductionReadinessDimension>().Length, comparison.Items.Count);
        Assert.Equal(Enum.GetValues<ProductionReadinessDimension>(),
            comparison.Items.Select(item => item.Dimension).ToArray());
        Assert.All(comparison.Items, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.CurrentLegacyAuthority));
            Assert.False(string.IsNullOrWhiteSpace(item.FutureTargetAuthority));
            Assert.False(string.IsNullOrWhiteSpace(item.RemainingGap));
            Assert.NotEqual(AuthorityReadinessState.CurrentLegacyAuthority, item.TargetState);
        });
    }

    [Fact]
    public void Activation_foundation_has_no_startup_UI_database_or_migration_dependency()
    {
        Assembly assembly = typeof(ActivationEvidencePackage).Assembly;
        Type[] activationTypes = assembly.GetTypes()
            .Where(type => type.Namespace == typeof(ActivationEvidencePackage).Namespace)
            .ToArray();
        string typeSurface = string.Join('|', activationTypes.Select(type => type.FullName)
            .Concat(activationTypes.SelectMany(type => type.GetInterfaces()).Select(type => type.FullName)));

        Assert.DoesNotContain("Microsoft.Data.Sqlite", typeSurface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.Windows.Forms", typeSurface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MigrationRunner", typeSurface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Startup", typeSurface, StringComparison.OrdinalIgnoreCase);
    }

    private static ProductionActivationGuardRequest CompleteGuardRequest()
    {
        DatabasePreflightResult preflight = CompletePreflight();
        var classification = new MigrationHistoryClassificationResult(
            MigrationHistoryClassification.CleanLegacyBaseline, 4, Array.Empty<string>());
        DatabaseBackupVerificationResult backup = CompleteBackup();
        MigrationRehearsalResult rehearsal = CompleteRehearsal();
        return new(
            new(MaintenanceReadinessStatus.ReadyForFutureMigrationApproval,
                Array.Empty<string>(), Array.Empty<string>()),
            preflight, classification, backup, rehearsal, CompleteEvidence(), ValidApproval(),
            ProductionActivationScope.UnifiedMigrationActivation);
    }

    private static ActivationEvidencePackage CompleteEvidence(bool backupVerified = true)
    {
        var preservation = new ActivationSnapshotPreservationEvidence(
            true, true, true, true, true, true, true);
        return new(EvidenceId, CorrelationId, DatabaseIdentity,
            new(DatabaseIdentity, true, true, true, true, true, Now.AddMinutes(-30)),
            new(MigrationHistoryClassification.CleanLegacyBaseline, 4, true, true),
            new("backup-receipt-1", DatabaseIdentity, BackupIdentity, backupVerified,
                backupVerified, 4096, Now.AddMinutes(-20)),
            new("rehearsal-receipt-1", true, true, true, 4,
                EsdReconciliationState.ReadyToProvision, EsdAuthorityMode.LegacyAuthoritative,
                preservation, Now.AddMinutes(-10)),
            new(true, true, backupVerified, true),
            new(true, ProductionActivationScope.UnifiedMigrationActivation,
                DatabaseIdentity, EvidenceId),
            Now.AddMinutes(-5));
    }

    private static ProductionActivationApproval ValidApproval() => new(
        "approval-1", "operator-1", Now.AddMinutes(-2),
        ProductionActivationScope.UnifiedMigrationActivation, DatabaseIdentity,
        EvidenceId, CorrelationId, Now.AddMinutes(30));

    private static DatabasePreflightResult CompletePreflight()
    {
        var target = new DatabaseTargetDescriptor("explicit.sqlite", 4096, Now.AddHours(-1),
            Now.AddMinutes(-30), DatabaseIdentity);
        return new(true, new(true, target, DatabaseTargetFailure.None, "ValidExplicitSqliteTarget"),
            true, true, ["ok"], Array.Empty<string>(), 1, 0,
            new(false, false, false, null, Array.Empty<InspectedMigrationEntry>(), null),
            Array.Empty<SqliteSchemaObject>(), new Dictionary<string, long>(),
            Array.Empty<string>(), Array.Empty<string>(),
            new(InspectedEsdValueState.Valid, "2.5", 1),
            new(InspectedEsdValueState.Absent, null, 0),
            new(0, new Dictionary<string, string>(), 0, new Dictionary<string, string>()),
            "wal", true, false, Array.Empty<string>());
    }

    private static DatabaseBackupVerificationResult CompleteBackup()
    {
        var source = new DatabaseTargetDescriptor("explicit.sqlite", 4096, Now.AddHours(-1),
            Now.AddMinutes(-30), DatabaseIdentity);
        var backup = new DatabaseTargetDescriptor("backup.sqlite", 4096, Now.AddMinutes(-20),
            Now.AddMinutes(-20), BackupIdentity);
        return new(true, "backup.sqlite", source, backup, "safe-file-checksum", 4096,
            Now.AddMinutes(-20), 0, MigrationHistoryClassification.CleanLegacyBaseline,
            true, DatabaseBackupFailure.None, Array.Empty<string>());
    }

    private static MigrationRehearsalResult CompleteRehearsal()
    {
        var preservation = new PreservationVerificationResult(true, true, true, true, true, true,
            true, true, true, true, true, Array.Empty<string>());
        return new(true, MigrationRehearsalFailure.None, 0, 4,
            ["target-database-foundation-v1"], true, true, preservation,
            EsdReconciliationState.ReadyToProvision, EsdAuthorityMode.LegacyAuthoritative,
            Array.Empty<string>());
    }

    private static ApprovedProductionMigrationContext ApprovedContext()
    {
        ActivationEvidencePackage evidence = CompleteEvidence();
        ProductionActivationApproval approval = ValidApproval();
        var authorization = new ExplicitProductionMigrationAuthorization(
            "authorization-1", "migration-authorizer-1", approval.ApprovalId,
            evidence.EvidencePackageId, evidence.DatabaseIdentityFingerprint,
            evidence.CorrelationId, Now.AddMinutes(-1), Now.AddMinutes(10));
        return new(Path.GetFullPath(Path.Combine("future", "explicit-production.sqlite")), evidence,
            approval, authorization, new(ActivationGuardDecision.Allowed, Array.Empty<string>()));
    }

    private sealed record FixedClock(DateTimeOffset UtcNow) : IClock
    {
        public DateTimeOffset LocalNow => UtcNow.ToOffset(TimeSpan.FromHours(3.5));
    }

    private sealed class ValidatingMigrationExecutorTestDouble : IProductionMigrationExecutor
    {
        public int CallCount { get; private set; }

        public Task<ProductionMigrationExecutionResult> ExecuteAsync(
            ApprovedProductionMigrationContext approvedContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(ApprovedProductionMigrationContextValidator.IsValid(approvedContext, Now)
                ? new ProductionMigrationExecutionResult(ProductionMigrationExecutionStatus.Succeeded,
                    approvedContext.EvidencePackage.CorrelationId, "test-receipt", "TestDoubleAccepted")
                : new ProductionMigrationExecutionResult(ProductionMigrationExecutionStatus.Rejected,
                    approvedContext?.EvidencePackage.CorrelationId ?? "none", null, "TestDoubleRejected"));
        }
    }
}
