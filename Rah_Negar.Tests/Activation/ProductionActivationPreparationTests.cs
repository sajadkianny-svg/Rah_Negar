using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using Rah_Negar.Foundation.Application.Activation;
using Rah_Negar.Foundation.Application.Activation.Preparation;
using Rah_Negar.Foundation.Application.Pilot.Deployment;
using Rah_Negar.Foundation.Application.Pilot.Validation;

namespace Rah_Negar.Tests.Activation;

public sealed class ProductionActivationPreparationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 22, 23, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Preparation_context_is_explicit_immutable_and_defensively_copied()
    {
        var approvals = ApprovalReferences().ToList();

        ProductionActivationPreparationContext context = Context(
            approvalReferences: approvals);
        approvals.Add("late-approval");

        Assert.Equal(3, context.ApprovalReferences.Count);
        Assert.Equal(LegacyAuthorityState.LegacyAuthoritative,
            context.LegacyAuthorityState);
        Assert.False(context.AutomaticallyDiscoversEnvironment);
        Assert.False(context.AccessesProduction);
        Assert.False(context.GrantsActivationPermission);
        Assert.False(context.FallsBackToProduction);
        Assert.All(ContractTypes(), type => Assert.DoesNotContain(type.GetProperties(),
            property => property.SetMethod is not null));
    }

    [Fact]
    public void Complete_evidence_is_approved_for_preparation_only()
    {
        ProductionActivationReadinessResult result = Evaluate();

        Assert.Equal(ProductionActivationPreparationDecision.ApprovedForPreparation,
            result.Decision);
        Assert.Equal("activation-approved-for-preparation", result.ReasonCode);
        Assert.Empty(result.Blockers);
        Assert.Empty(result.ReviewItems);
        Assert.NotNull(result.EvidencePackage);
        Assert.Equal(6, result.EvidencePackage!.ActivationGates.Count);
        Assert.Equal(ProductionActivationPreparationDecision.ApprovedForPreparation,
            result.EvidencePackage.PreparationResult);
        AssertNoExecution(result);
    }

    [Theory]
    [InlineData(ProductionActivationGateType.SecurityReview)]
    [InlineData(ProductionActivationGateType.OperationsReadiness)]
    [InlineData(ProductionActivationGateType.DataOwnerApproval)]
    [InlineData(ProductionActivationGateType.RollbackReadiness)]
    [InlineData(ProductionActivationGateType.ValidationCompletion)]
    [InlineData(ProductionActivationGateType.DeploymentReadiness)]
    public void Every_activation_gate_is_required(ProductionActivationGateType removed)
    {
        ProductionActivationGate[] incomplete = Gates().Where(gate => gate.GateType != removed)
            .ToArray();

        ProductionActivationReadinessResult result = Coordinator().Evaluate(Context(),
            incomplete, Backup(), Rollback(), StopConditions());

        Assert.Equal(ProductionActivationPreparationDecision.Blocked, result.Decision);
        Assert.Contains("activation-gates-incomplete", result.Blockers);
        AssertNoExecution(result);
    }

    [Theory]
    [InlineData(ProductionActivationGateType.SecurityReview)]
    [InlineData(ProductionActivationGateType.OperationsReadiness)]
    [InlineData(ProductionActivationGateType.DataOwnerApproval)]
    public void Approval_gates_bind_exactly_to_context_references(
        ProductionActivationGateType changed)
    {
        ProductionActivationGate[] gates = Gates().Select(gate => gate.GateType == changed
            ? Gate(changed, evidenceReference: "different-approval-reference") : gate).ToArray();

        ProductionActivationReadinessResult result = Coordinator().Evaluate(Context(), gates,
            Backup(), Rollback(), StopConditions());

        Assert.Equal(ProductionActivationPreparationDecision.Blocked, result.Decision);
        Assert.Contains("activation-approval-references-mismatch", result.Blockers);
        Assert.All(gates, gate =>
        {
            Assert.False(gate.GrantsPermission);
            Assert.False(gate.CreatesPermission);
            Assert.False(gate.ImplementsRbac);
        });
    }

    [Fact]
    public void Missing_approval_blocks_and_review_approval_requires_review()
    {
        ProductionActivationGate[] missing = Gates().Select(gate =>
            gate.GateType == ProductionActivationGateType.SecurityReview
                ? Gate(gate.GateType, ProductionActivationGateStatus.Missing) : gate).ToArray();
        ProductionActivationGate[] review = Gates().Select(gate =>
            gate.GateType == ProductionActivationGateType.SecurityReview
                ? Gate(gate.GateType, ProductionActivationGateStatus.RequiresReview) : gate)
            .ToArray();

        ProductionActivationReadinessResult missingResult = Coordinator().Evaluate(Context(),
            missing, Backup(), Rollback(), StopConditions());
        ProductionActivationReadinessResult reviewResult = Coordinator().Evaluate(Context(),
            review, Backup(), Rollback(), StopConditions());

        Assert.Equal(ProductionActivationPreparationDecision.Blocked,
            missingResult.Decision);
        Assert.Contains("activation-gate-not-satisfied", missingResult.Blockers);
        Assert.Equal(ProductionActivationPreparationDecision.RequiresReview,
            reviewResult.Decision);
        Assert.Contains("activation-gate-review-required", reviewResult.ReviewItems);
    }

    [Fact]
    public void Backup_verification_is_evidence_only_and_requires_restore_test()
    {
        BackupVerificationResult backup = Backup();

        Assert.Equal(BackupEvidenceStatus.Verified, backup.VerificationStatus);
        Assert.Equal(RestoreTestStatus.Passed, backup.RestoreTestStatus);
        Assert.False(backup.ExecutesBackup);
        Assert.False(backup.AccessesFiles);
        Assert.False(backup.AccessesDatabase);
        Assert.False(backup.ExecutesRestore);

        BackupVerificationResult failed = Backup(BackupEvidenceStatus.Verified,
            RestoreTestStatus.NotPerformed);
        ProductionActivationReadinessResult result = Coordinator().Evaluate(Context(), Gates(),
            failed, Rollback(), StopConditions());
        Assert.Equal(ProductionActivationPreparationDecision.Blocked, result.Decision);
        Assert.Contains("backup-or-restore-verification-failed", result.Blockers);
    }

    [Fact]
    public void Rollback_verification_is_non_destructive_and_bound_to_context()
    {
        RollbackVerificationResult rollback = Rollback();

        Assert.False(rollback.ExecutesRollback);
        Assert.False(rollback.PerformsDestructiveOperation);

        var mismatched = new RollbackVerificationResult("other-rollback-plan",
            RollbackEvidenceStatus.Verified, "rollback-owner-1", "rollback-evidence-1");
        ProductionActivationReadinessResult result = Coordinator().Evaluate(Context(), Gates(),
            Backup(), mismatched, StopConditions());
        Assert.Equal(ProductionActivationPreparationDecision.Blocked, result.Decision);
        Assert.Contains("rollback-verification-invalid", result.Blockers);
    }

    [Theory]
    [InlineData(ProductionActivationStopConditionType.ValidationIncomplete)]
    [InlineData(ProductionActivationStopConditionType.BackupUnavailable)]
    [InlineData(ProductionActivationStopConditionType.RollbackUnavailable)]
    [InlineData(ProductionActivationStopConditionType.ApprovalMissing)]
    [InlineData(ProductionActivationStopConditionType.EvidenceMismatch)]
    [InlineData(ProductionActivationStopConditionType.EnvironmentMismatch)]
    public void Every_stop_condition_records_a_block_without_acting(
        ProductionActivationStopConditionType triggered)
    {
        ProductionActivationStopCondition[] conditions = StopConditions(triggered);

        ProductionActivationReadinessResult result = Coordinator().Evaluate(Context(), Gates(),
            Backup(), Rollback(), conditions);

        Assert.Equal(ProductionActivationPreparationDecision.Blocked, result.Decision);
        Assert.Contains(result.Blockers, blocker => blocker.StartsWith("activation-stop-",
            StringComparison.Ordinal));
        Assert.All(conditions, condition =>
        {
            Assert.False(condition.AutomaticallyActs);
            Assert.False(condition.ShutsDown);
        });
    }

    [Theory]
    [InlineData("validation")]
    [InlineData("deployment")]
    [InlineData("backup")]
    [InlineData("rollback")]
    public void Review_evidence_cannot_be_approved_for_preparation(string source)
    {
        PilotValidationResultStatus validationStatus = source == "validation"
            ? PilotValidationResultStatus.DifferenceDetected
            : PilotValidationResultStatus.Completed;
        PilotDeploymentReadinessStatus deploymentStatus = source == "deployment"
            ? PilotDeploymentReadinessStatus.RequiresReview
            : PilotDeploymentReadinessStatus.Ready;
        ProductionActivationPreparationContext context = Context(validationStatus:
            validationStatus, deploymentStatus: deploymentStatus);
        ProductionActivationGate[] gates = Gates(validationStatus, deploymentStatus,
            source == "rollback" ? RollbackEvidenceStatus.RequiresReview :
                RollbackEvidenceStatus.Verified);
        BackupVerificationResult backup = source == "backup"
            ? Backup(BackupEvidenceStatus.RequiresReview, RestoreTestStatus.Passed)
            : Backup();
        RollbackVerificationResult rollback = source == "rollback"
            ? Rollback(RollbackEvidenceStatus.RequiresReview) : Rollback();

        ProductionActivationReadinessResult result = Coordinator().Evaluate(context, gates,
            backup, rollback, StopConditions());

        Assert.Equal(ProductionActivationPreparationDecision.RequiresReview,
            result.Decision);
        Assert.NotEmpty(result.ReviewItems);
        Assert.Empty(result.Blockers);
        AssertNoExecution(result);
    }

    [Fact]
    public void Gate_status_mismatch_blocks_even_when_source_status_is_ready()
    {
        ProductionActivationGate[] gates = Gates().Select(gate =>
            gate.GateType == ProductionActivationGateType.DeploymentReadiness
                ? Gate(gate.GateType, ProductionActivationGateStatus.RequiresReview) : gate)
            .ToArray();

        ProductionActivationReadinessResult result = Coordinator().Evaluate(Context(), gates,
            Backup(), Rollback(), StopConditions());

        Assert.Equal(ProductionActivationPreparationDecision.Blocked, result.Decision);
        Assert.Contains("activation-gate-evidence-mismatch", result.Blockers);
    }

    [Theory]
    [InlineData(false, LegacyAuthorityState.LegacyAuthoritative,
        "activation-preparation-explicit-request-required")]
    [InlineData(true, LegacyAuthorityState.TargetAuthorityRequested,
        "activation-preparation-context-invalid")]
    public void Implicit_or_nonlegacy_context_fails_closed(bool explicitRequest,
        LegacyAuthorityState authority, string expectedReason)
    {
        ProductionActivationPreparationContext context = Context(
            explicitRequest: explicitRequest, authority: authority);

        ProductionActivationReadinessResult result = Coordinator().Evaluate(context, Gates(),
            Backup(), Rollback(), StopConditions());

        Assert.Equal(ProductionActivationPreparationDecision.Blocked, result.Decision);
        Assert.Equal(expectedReason, result.ReasonCode);
        Assert.Null(result.EvidencePackage);
        AssertNoExecution(result);
    }

    [Fact]
    public void Hostile_identifiers_fail_closed_without_echoing_input()
    {
        ProductionActivationPreparationContext context = Context(
            preparationId: "SELECT-password-FROM-users");

        ProductionActivationReadinessResult result = Coordinator().Evaluate(context, Gates(),
            Backup(), Rollback(), StopConditions());

        Assert.Equal("activation-preparation-identifier-invalid", result.ReasonCode);
        Assert.DoesNotContain("password", result.ReasonCode,
            StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.EvidencePackage);
    }

    [Fact]
    public void Cutover_package_is_immutable_safe_evidence_not_permission()
    {
        ProductionCutoverEvidencePackage package = Evaluate().EvidencePackage!;
        string names = string.Join('|', typeof(ProductionCutoverEvidencePackage).GetProperties(
            BindingFlags.Instance | BindingFlags.Public).Select(property => property.Name));

        Assert.False(package.ContainsSecrets);
        Assert.False(package.ContainsCredentialMaterial);
        Assert.False(package.ContainsDatabaseDump);
        Assert.False(package.ContainsRawLogs);
        Assert.False(package.ContainsPrivateKeys);
        Assert.False(package.GrantsActivationPermission);
        Assert.True(package.ValidationSummary.LegacyRemainsAuthoritative);
        Assert.DoesNotContain("Password", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DatabaseDumpData", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RawLogData", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PrivateKeyData", names, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Throwing_input_enumeration_isolated_with_fixed_reason()
    {
        var throwing = new ThrowingEnumerable<ProductionActivationGate>();

        ProductionActivationReadinessResult result = Coordinator().Evaluate(Context(), throwing,
            Backup(), Rollback(), StopConditions());

        Assert.Equal(ProductionActivationPreparationDecision.Blocked, result.Decision);
        Assert.Equal("activation-preparation-evaluation-failed", result.ReasonCode);
        Assert.DoesNotContain("exception", result.ReasonCode,
            StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.EvidencePackage);
    }

    [Fact]
    public void Legacy_authority_and_all_legacy_workflows_remain_preserved()
    {
        ProductionActivationReadinessResult result = Evaluate();

        Assert.True(result.LegacyAuthorityPreserved);
        Assert.False(result.ReplacedLogin);
        Assert.False(result.ReplacedSettings);
        Assert.False(result.ReplacedReporting);
        Assert.False(result.ReplacedRuntimeEvents);
        Assert.False(result.SwitchedAuthority);
    }

    [Fact]
    public void Coordinator_has_no_execution_route_permission_or_escalation_behavior()
    {
        ProductionActivationReadinessCoordinator coordinator = Coordinator();

        Assert.False(coordinator.ActivatesFeatures);
        Assert.False(coordinator.ExecutesDeployment);
        Assert.False(coordinator.RunsMigration);
        Assert.False(coordinator.ModifiesDatabase);
        Assert.False(coordinator.PerformsEsdCutover);
        Assert.False(coordinator.SwitchesAuthority);
        Assert.False(coordinator.RegistersRoutes);
        Assert.False(coordinator.UsesServiceLocator);
        Assert.False(coordinator.AutomaticallyRuns);
        Assert.False(coordinator.HandlesPasswords);
        Assert.False(coordinator.MutatesCredentials);
        Assert.False(coordinator.CreatesRbac);
        Assert.False(coordinator.UsesSupportIdentity);
        Assert.False(coordinator.StoresSecrets);
        Assert.False(coordinator.EscalatesPermissions);
    }

    [Fact]
    public void Preparation_namespace_has_no_database_migration_UI_executor_or_security_dependency()
    {
        Type[] types = typeof(ProductionActivationReadinessCoordinator).Assembly.GetTypes()
            .Where(type => type.Namespace ==
                typeof(ProductionActivationReadinessCoordinator).Namespace).ToArray();
        string surface = string.Join('|', types.Select(type => type.FullName)
            .Concat(types.SelectMany(type => type.GetInterfaces()).Select(type => type.FullName))
            .Concat(types.SelectMany(type => type.GetFields(BindingFlags.Instance |
                BindingFlags.Static | BindingFlags.NonPublic)).Select(field =>
                field.FieldType.FullName)));
        string methods = string.Join('|', types.SelectMany(type => type.GetMethods(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                BindingFlags.DeclaredOnly)).Where(method => !method.IsSpecialName)
            .Select(method => method.Name));

        Assert.DoesNotContain("Microsoft.Data.Sqlite", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Repository", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MigrationRunner", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.Windows.Forms", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Rah_Negar.UI", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IProductionMigrationExecutor", surface,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CredentialService", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Deploy", methods, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Activate", methods, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Migrate", methods, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Restore", methods, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Escalate", methods, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupportIdentity", surface, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_startup_routes_and_forms_do_not_reference_preparation_layer()
    {
        string root = RepositoryRoot();
        string programPath = Path.Combine(root, "Program.cs");
        string protectedSource = File.ReadAllText(programPath) + Environment.NewLine +
            string.Join(Environment.NewLine,
                Directory.GetFiles(Path.Combine(root, "UI", "Startup"), "*.cs",
                        SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(Path.Combine(root, "UI", "Forms"), "*.cs",
                        SearchOption.AllDirectories)).Select(File.ReadAllText));
        string preparationSource = string.Join(Environment.NewLine,
            Directory.GetFiles(Path.Combine(root, "Application", "Activation", "Preparation"),
                "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));

        Assert.Equal("33985F732E77AFC7249DDA0174E8BCC58601B5E0B3E22B93E31933F01ACCAA76",
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(programPath))));
        Assert.DoesNotContain("ProductionActivationReadinessCoordinator", protectedSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Activation.Preparation", protectedSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.Data.Sqlite", preparationSource,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MigrationRunner", preparationSource,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IProductionMigrationExecutor", preparationSource,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Task.Run", preparationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new Timer", preparationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IServiceProvider", preparationSource,
            StringComparison.OrdinalIgnoreCase);
    }

    private static Type[] ContractTypes() =>
    [
        typeof(ProductionActivationPreparationContext), typeof(ProductionActivationGate),
        typeof(BackupVerificationResult), typeof(RollbackVerificationResult),
        typeof(ProductionActivationStopCondition),
        typeof(ProductionActivationValidationSummary),
        typeof(ProductionCutoverEvidencePackage), typeof(ProductionActivationReadinessResult)
    ];

    private static ProductionActivationReadinessCoordinator Coordinator() => new();

    private static ProductionActivationReadinessResult Evaluate() => Coordinator().Evaluate(
        Context(), Gates(), Backup(), Rollback(), StopConditions());

    private static ProductionActivationPreparationContext Context(
        bool explicitRequest = true,
        LegacyAuthorityState authority = LegacyAuthorityState.LegacyAuthoritative,
        string preparationId = "preparation-1",
        PilotValidationResultStatus validationStatus = PilotValidationResultStatus.Completed,
        PilotDeploymentReadinessStatus deploymentStatus = PilotDeploymentReadinessStatus.Ready,
        IEnumerable<string>? approvalReferences = null) => new(preparationId, "release-9.0",
            ProductionActivationScope.AuthenticationWorkflowActivation, authority,
            validationStatus, deploymentStatus, "rollback-plan-1",
            approvalReferences ?? ApprovalReferences(), Now, explicitRequest);

    private static ProductionActivationGate[] Gates(
        PilotValidationResultStatus validationStatus = PilotValidationResultStatus.Completed,
        PilotDeploymentReadinessStatus deploymentStatus = PilotDeploymentReadinessStatus.Ready,
        RollbackEvidenceStatus rollbackStatus = RollbackEvidenceStatus.Verified) =>
        Enum.GetValues<ProductionActivationGateType>().Select(type => Gate(type,
            type == ProductionActivationGateType.ValidationCompletion &&
                validationStatus == PilotValidationResultStatus.DifferenceDetected ||
            type == ProductionActivationGateType.DeploymentReadiness &&
                deploymentStatus == PilotDeploymentReadinessStatus.RequiresReview ||
            type == ProductionActivationGateType.RollbackReadiness &&
                rollbackStatus == RollbackEvidenceStatus.RequiresReview
                ? ProductionActivationGateStatus.RequiresReview
                : type == ProductionActivationGateType.ValidationCompletion &&
                    validationStatus is PilotValidationResultStatus.Failed or
                        PilotValidationResultStatus.Blocked ||
                  type == ProductionActivationGateType.DeploymentReadiness &&
                    deploymentStatus == PilotDeploymentReadinessStatus.Blocked ||
                  type == ProductionActivationGateType.RollbackReadiness &&
                    rollbackStatus is RollbackEvidenceStatus.Failed or
                        RollbackEvidenceStatus.Unavailable
                    ? ProductionActivationGateStatus.Failed
                    : ProductionActivationGateStatus.Satisfied)).ToArray();

    private static ProductionActivationGate Gate(ProductionActivationGateType type,
        ProductionActivationGateStatus status = ProductionActivationGateStatus.Satisfied,
        string? evidenceReference = null) => new(type, status,
            evidenceReference ?? EvidenceReference(type), "reviewer-1", Now.AddMinutes(-1));

    private static string EvidenceReference(ProductionActivationGateType type) => type switch
    {
        ProductionActivationGateType.SecurityReview => "approval-security-1",
        ProductionActivationGateType.OperationsReadiness => "approval-operations-1",
        ProductionActivationGateType.DataOwnerApproval => "approval-dataowner-1",
        ProductionActivationGateType.RollbackReadiness => "rollback-evidence-1",
        ProductionActivationGateType.ValidationCompletion => "validation-evidence-1",
        ProductionActivationGateType.DeploymentReadiness => "deployment-evidence-1",
        _ => "gate-evidence-1"
    };

    private static string[] ApprovalReferences() =>
    ["approval-dataowner-1", "approval-operations-1", "approval-security-1"];

    private static BackupVerificationResult Backup(
        BackupEvidenceStatus status = BackupEvidenceStatus.Verified,
        RestoreTestStatus restore = RestoreTestStatus.Passed) => new("backup-reference-1",
            status, restore, Now.AddMinutes(-2));

    private static RollbackVerificationResult Rollback(
        RollbackEvidenceStatus status = RollbackEvidenceStatus.Verified) => new(
            "rollback-plan-1", status, "rollback-owner-1", "rollback-evidence-1");

    private static ProductionActivationStopCondition[] StopConditions(
        ProductionActivationStopConditionType? triggered = null) =>
        Enum.GetValues<ProductionActivationStopConditionType>().Select(type =>
            new ProductionActivationStopCondition(type, type == triggered,
                $"stop-evidence-{type.ToString().ToLowerInvariant()}-1")).ToArray();

    private static void AssertNoExecution(ProductionActivationReadinessResult result)
    {
        Assert.False(result.ActivatedFeatures);
        Assert.False(result.ExecutedDeployment);
        Assert.False(result.RanMigration);
        Assert.False(result.ModifiedDatabase);
        Assert.False(result.PerformedEsdCutover);
        Assert.False(result.SwitchedAuthority);
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName,
                   "Rah_Negar.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }

    private sealed class ThrowingEnumerable<T> : IEnumerable<T>
    {
        public IEnumerator<T> GetEnumerator() =>
            throw new InvalidOperationException("sensitive enumeration exception");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
