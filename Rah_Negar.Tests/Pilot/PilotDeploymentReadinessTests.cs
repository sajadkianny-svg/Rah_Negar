using System.Reflection;
using System.Security.Cryptography;
using Rah_Negar.Foundation.Application.Pilot.Deployment;
using Rah_Negar.Foundation.Application.Pilot.Validation;

namespace Rah_Negar.Tests.Pilot;

public sealed class PilotDeploymentReadinessTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 22, 23, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Readiness_context_is_explicit_immutable_and_defensively_copied()
    {
        var features = new List<PilotDeploymentFeature>
        {
            PilotDeploymentFeature.ReportingObservation,
            PilotDeploymentFeature.AuthenticationObservation,
            PilotDeploymentFeature.ReportingObservation
        };
        var references = ApprovalReferences().ToList();

        PilotDeploymentReadinessContext context = Context(features: features,
            approvalReferences: references);
        features.Add(PilotDeploymentFeature.ExportObservation);
        references.Add("late-approval");

        Assert.Equal(2, context.RequiredFeatures.Count);
        Assert.Equal(4, context.ApprovalReferences.Count);
        Assert.False(context.AutomaticallyDiscoversEnvironment);
        Assert.False(context.FallsBackToProduction);
        Assert.False(context.ActivatesPilot);
        Assert.All(ContractTypes(), type => Assert.DoesNotContain(type.GetProperties(),
            property => property.SetMethod is not null));
    }

    [Fact]
    public void Complete_explicit_evidence_is_ready_but_does_not_deploy()
    {
        PilotDeploymentReadinessCoordinator coordinator = Coordinator();

        PilotDeploymentReadinessResult result = coordinator.Evaluate(Context(), Manifest(),
            Rollback(), Approvals(), StopConditions(), Checklist(), Monitoring());

        Assert.Equal(PilotDeploymentReadinessStatus.Ready, result.Status);
        Assert.Equal("readiness-ready", result.ReasonCode);
        Assert.Empty(result.Blockers);
        Assert.NotNull(result.EvidencePackage);
        Assert.Equal(5, result.EvidencePackage!.ValidationRecords.Count);
        Assert.Equal(4, result.EvidencePackage.Approvals.Count);
        Assert.Equal(PilotRollbackValidationStatus.Ready,
            result.EvidencePackage.RollbackStatus.ValidationStatus);
        Assert.False(result.Deployed);
        Assert.False(result.Activated);
        Assert.False(result.Migrated);
        Assert.False(result.ModifiedDatabase);
        Assert.False(result.SwitchedAuthority);
    }

    [Theory]
    [InlineData(PilotEnvironmentValidationKind.OsCompatibility)]
    [InlineData(PilotEnvironmentValidationKind.ApplicationBuild)]
    [InlineData(PilotEnvironmentValidationKind.Dependency)]
    [InlineData(PilotEnvironmentValidationKind.Configuration)]
    [InlineData(PilotEnvironmentValidationKind.SecurityBaseline)]
    public void Any_failed_environment_gate_blocks_readiness(
        PilotEnvironmentValidationKind failedKind)
    {
        PilotDeploymentReadinessCoordinator coordinator = Coordinator(failedKind:
            failedKind);

        PilotDeploymentReadinessResult result = coordinator.Evaluate(Context(), Manifest(),
            Rollback(), Approvals(), StopConditions(), Checklist(), Monitoring());

        Assert.Equal(PilotDeploymentReadinessStatus.Blocked, result.Status);
        Assert.Contains("environment-validation-failed", result.Blockers);
        Assert.False(result.EvidencePackage!.ValidationRecords.Single(record =>
            record.Kind == failedKind).ModifiesEnvironment);
    }

    [Theory]
    [InlineData("validation")]
    [InlineData("manifest")]
    [InlineData("environment")]
    [InlineData("rollback")]
    [InlineData("approval")]
    public void Review_evidence_never_becomes_ready(string source)
    {
        PilotDeploymentReadinessContext context = Context(validationStatus:
            source == "validation" ? PilotValidationResultStatus.DifferenceDetected :
                PilotValidationResultStatus.Completed);
        PilotDeploymentManifest manifest = Manifest(source == "manifest"
            ? PilotReadinessGateStatus.RequiresReview : PilotReadinessGateStatus.Passed);
        PilotRollbackReadiness rollback = Rollback(source == "rollback"
            ? PilotRollbackValidationStatus.RequiresReview : PilotRollbackValidationStatus.Ready);
        PilotApprovalGate[] approvals = Approvals(source == "approval"
            ? PilotApprovalGateKind.Security : null,
            source == "approval" ? PilotApprovalGateStatus.RequiresReview :
                PilotApprovalGateStatus.Approved);
        PilotDeploymentReadinessCoordinator coordinator = Coordinator(reviewKind:
            source == "environment" ? PilotEnvironmentValidationKind.Dependency : null);

        PilotDeploymentReadinessResult result = coordinator.Evaluate(context, manifest,
            rollback, approvals, StopConditions(), Checklist(), Monitoring());

        Assert.Equal(PilotDeploymentReadinessStatus.RequiresReview, result.Status);
        Assert.Equal("readiness-review-required", result.ReasonCode);
        Assert.NotEmpty(result.Blockers);
        Assert.Equal(PilotDeploymentReadinessStatus.RequiresReview,
            result.EvidencePackage!.ReadinessStatus);
    }

    [Theory]
    [InlineData(PilotStopConditionKind.ValidationFailure)]
    [InlineData(PilotStopConditionKind.EvidenceMismatch)]
    [InlineData(PilotStopConditionKind.EnvironmentFailure)]
    [InlineData(PilotStopConditionKind.RollbackUnavailable)]
    [InlineData(PilotStopConditionKind.ApprovalMissing)]
    public void Every_triggered_stop_condition_blocks_without_automatic_shutdown(
        PilotStopConditionKind triggeredKind)
    {
        PilotStopCondition[] conditions = StopConditions(triggeredKind);

        PilotDeploymentReadinessResult result = Coordinator().Evaluate(Context(), Manifest(),
            Rollback(), Approvals(), conditions, Checklist(), Monitoring());

        Assert.Equal(PilotDeploymentReadinessStatus.Blocked, result.Status);
        Assert.Contains(result.Blockers, blocker => blocker.StartsWith("stop-condition-",
            StringComparison.Ordinal));
        Assert.All(conditions, condition =>
        {
            Assert.False(condition.AutomaticallyShutsDown);
            Assert.False(condition.ExecutesAction);
        });
    }

    [Fact]
    public void Manifest_filters_sensitive_dynamic_content_and_cannot_deploy()
    {
        var manifest = new PilotDeploymentManifest("manifest-1", "../secret/config.json",
            "build-1", ["artifact-1", "SELECT-password", @"C:\pilot\app.exe"],
            ["net8-runtime", "credential-secret", "dependency-1"],
            PilotReadinessGateStatus.Passed);

        Assert.Equal("version-unavailable", manifest.Version);
        Assert.Equal(["artifact-1"], manifest.ArtifactIdentifiers);
        Assert.Equal(["dependency-1", "net8-runtime"], manifest.DependencySummary);
        Assert.False(manifest.ContainsSensitiveConfiguration);
        Assert.False(manifest.ContainsCredentialMaterial);
        Assert.False(manifest.DeploysArtifacts);
    }

    [Fact]
    public void Rollback_boundary_is_preparation_only_and_never_restores()
    {
        PilotRollbackReadiness rollback = Rollback();

        Assert.Equal("rollback-plan-1", rollback.RollbackPlanId);
        Assert.Equal(PilotRollbackValidationStatus.Ready, rollback.ValidationStatus);
        Assert.False(rollback.ExecutesRollback);
        Assert.False(rollback.PerformsDestructiveAction);
        Assert.False(rollback.RestoresDatabase);
    }

    [Theory]
    [InlineData(PilotApprovalGateKind.Security)]
    [InlineData(PilotApprovalGateKind.Operations)]
    [InlineData(PilotApprovalGateKind.DataOwner)]
    [InlineData(PilotApprovalGateKind.Product)]
    public void Every_approval_gate_is_required_and_is_evidence_only(
        PilotApprovalGateKind removedKind)
    {
        PilotApprovalGate[] incomplete = Approvals().Where(gate => gate.Kind != removedKind)
            .ToArray();

        PilotDeploymentReadinessResult result = Coordinator().Evaluate(Context(), Manifest(),
            Rollback(), incomplete, StopConditions(), Checklist(), Monitoring());

        Assert.Equal(PilotDeploymentReadinessStatus.Blocked, result.Status);
        Assert.Contains("approval-gates-incomplete", result.Blockers);
        Assert.All(incomplete, gate =>
        {
            Assert.False(gate.GrantsPermission);
            Assert.False(gate.ImplementsRbac);
        });
    }

    [Fact]
    public void Monitoring_plan_prepares_all_signals_without_telemetry()
    {
        PilotMonitoringReadinessPlan monitoring = Monitoring();

        Assert.Equal(Enum.GetValues<PilotMonitoringSignalKind>(),
            monitoring.RequiredSignals);
        Assert.False(monitoring.ImplementsTelemetry);
        Assert.False(monitoring.StartsMonitoring);
    }

    [Fact]
    public void Deployment_checklist_is_complete_immutable_and_non_executing()
    {
        PilotDeploymentChecklist checklist = Checklist();

        Assert.Equal(Enum.GetValues<PilotDeploymentChecklistItem>(),
            checklist.Entries.Select(entry => entry.Item));
        Assert.All(checklist.Entries, entry =>
        {
            Assert.Equal(PilotReadinessGateStatus.Passed, entry.Status);
            Assert.False(entry.PerformsAction);
        });
        Assert.False(checklist.Deploys);
        Assert.False(checklist.Activates);
    }

    [Fact]
    public void Incomplete_deployment_checklist_blocks_readiness()
    {
        var incomplete = new PilotDeploymentChecklist(Checklist().Entries.Skip(1));

        PilotDeploymentReadinessResult result = Coordinator().Evaluate(Context(), Manifest(),
            Rollback(), Approvals(), StopConditions(), incomplete, Monitoring());

        Assert.Equal(PilotDeploymentReadinessStatus.Blocked, result.Status);
        Assert.Contains("deployment-checklist-incomplete", result.Blockers);
    }

    [Fact]
    public void Incomplete_monitoring_and_unavailable_rollback_are_blockers()
    {
        var incompleteMonitoring = new PilotMonitoringReadinessPlan("monitoring-plan-1",
            [PilotMonitoringSignalKind.PilotHealth], "monitoring-owner-1", "escalation-1");

        PilotDeploymentReadinessResult result = Coordinator().Evaluate(Context(), Manifest(),
            Rollback(PilotRollbackValidationStatus.Unavailable), Approvals(), StopConditions(),
            Checklist(), incompleteMonitoring);

        Assert.Equal(PilotDeploymentReadinessStatus.Blocked, result.Status);
        Assert.Contains("rollback-unavailable", result.Blockers);
        Assert.Contains("monitoring-readiness-incomplete", result.Blockers);
    }

    [Theory]
    [InlineData(false, "readiness-explicit-request-required")]
    [InlineData(true, "readiness-context-identifier-invalid")]
    public void Invalid_context_fails_closed_without_evidence(bool explicitRequest,
        string expectedReason)
    {
        PilotDeploymentReadinessContext context = Context(explicitRequest: explicitRequest,
            readinessId: explicitRequest ? "SELECT-password" : "readiness-1");

        PilotDeploymentReadinessResult result = Coordinator().Evaluate(context, Manifest(),
            Rollback(), Approvals(), StopConditions(), Checklist(), Monitoring());

        Assert.Equal(PilotDeploymentReadinessStatus.Blocked, result.Status);
        Assert.Equal(expectedReason, result.ReasonCode);
        Assert.Null(result.EvidencePackage);
    }

    [Fact]
    public void Failed_workflow_validation_and_missing_approval_block_readiness()
    {
        PilotApprovalGate[] approvals = Approvals(PilotApprovalGateKind.Product,
            PilotApprovalGateStatus.Missing);

        PilotDeploymentReadinessResult result = Coordinator().Evaluate(
            Context(validationStatus: PilotValidationResultStatus.Failed), Manifest(),
            Rollback(), approvals, StopConditions(), Checklist(), Monitoring());

        Assert.Equal(PilotDeploymentReadinessStatus.Blocked, result.Status);
        Assert.Contains("pilot-validation-failed", result.Blockers);
        Assert.Contains("approval-missing", result.Blockers);
    }

    [Fact]
    public void Evidence_package_excludes_sensitive_payload_shapes()
    {
        PilotDeploymentEvidencePackage package = Coordinator().Evaluate(Context(), Manifest(),
            Rollback(), Approvals(), StopConditions(), Checklist(), Monitoring()).EvidencePackage!;
        string names = string.Join('|', typeof(PilotDeploymentEvidencePackage).GetProperties(
            BindingFlags.Instance | BindingFlags.Public).Select(property => property.Name));

        Assert.False(package.ContainsSecrets);
        Assert.False(package.ContainsRawLogs);
        Assert.False(package.ContainsDatabaseDump);
        Assert.DoesNotContain("Password", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Credential", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PrivateKey", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RawLogData", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DatabaseDumpData", names, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validator_exception_is_isolated_and_never_exposed()
    {
        IPilotEnvironmentReadinessValidator[] validators = Validators().Select(validator =>
            validator.Kind == PilotEnvironmentValidationKind.SecurityBaseline
                ? (IPilotEnvironmentReadinessValidator)new ThrowingValidator(validator.Kind)
                : validator).ToArray();

        PilotDeploymentReadinessResult result = new PilotDeploymentReadinessCoordinator(
            validators).Evaluate(Context(), Manifest(), Rollback(), Approvals(),
                StopConditions(), Checklist(), Monitoring());

        Assert.Equal(PilotDeploymentReadinessStatus.Blocked, result.Status);
        Assert.Equal("readiness-environment-validator-failed", result.ReasonCode);
        Assert.DoesNotContain("exception", result.ReasonCode,
            StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.EvidencePackage);
    }

    [Fact]
    public void Missing_or_duplicate_validator_sets_fail_closed()
    {
        IPilotEnvironmentReadinessValidator[] complete = Validators();
        var missing = new PilotDeploymentReadinessCoordinator(complete.Skip(1));
        var duplicate = new PilotDeploymentReadinessCoordinator(complete.Append(complete[0]));

        PilotDeploymentReadinessResult missingResult = missing.Evaluate(Context(), Manifest(),
            Rollback(), Approvals(), StopConditions(), Checklist(), Monitoring());
        PilotDeploymentReadinessResult duplicateResult = duplicate.Evaluate(Context(), Manifest(),
            Rollback(), Approvals(), StopConditions(), Checklist(), Monitoring());

        Assert.Equal("readiness-validator-set-incomplete", missingResult.ReasonCode);
        Assert.Equal("readiness-validator-duplicate", duplicateResult.ReasonCode);
    }

    [Fact]
    public void Coordinator_exposes_no_execution_or_fallback_behavior()
    {
        PilotDeploymentReadinessCoordinator coordinator = Coordinator();

        Assert.False(coordinator.Deploys);
        Assert.False(coordinator.Activates);
        Assert.False(coordinator.Migrates);
        Assert.False(coordinator.ModifiesDatabase);
        Assert.False(coordinator.PerformsEsdCutover);
        Assert.False(coordinator.SwitchesAuthority);
        Assert.False(coordinator.UsesServiceLocator);
        Assert.False(coordinator.AutomaticallyRuns);
        Assert.False(coordinator.FallsBackToProduction);
    }

    [Fact]
    public void Deployment_namespace_has_no_database_migration_UI_executor_or_identity_dependency()
    {
        Type[] types = typeof(PilotDeploymentReadinessCoordinator).Assembly.GetTypes().Where(type =>
            type.Namespace == typeof(PilotDeploymentReadinessCoordinator).Namespace).ToArray();
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
        Assert.DoesNotContain("Deploy", methods, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Activate", methods, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Migrate", methods, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Restore", methods, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupportIdentity", surface, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_startup_navigation_and_forms_do_not_reference_readiness_layer()
    {
        string root = RepositoryRoot();
        string programPath = Path.Combine(root, "Program.cs");
        string protectedSource = File.ReadAllText(programPath) + Environment.NewLine +
            string.Join(Environment.NewLine,
                Directory.GetFiles(Path.Combine(root, "UI", "Startup"), "*.cs",
                        SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(Path.Combine(root, "UI", "Forms"), "*.cs",
                        SearchOption.AllDirectories)).Select(File.ReadAllText));
        string readinessSource = string.Join(Environment.NewLine,
            Directory.GetFiles(Path.Combine(root, "Application", "Pilot", "Deployment"),
                "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));

        Assert.Equal("33985F732E77AFC7249DDA0174E8BCC58601B5E0B3E22B93E31933F01ACCAA76",
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(programPath))));
        Assert.DoesNotContain("PilotDeploymentReadinessCoordinator", protectedSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Application.Pilot.Deployment", protectedSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.Data.Sqlite", readinessSource,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MigrationRunner", readinessSource,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IProductionMigrationExecutor", readinessSource,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Task.Run", readinessSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new Timer", readinessSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IServiceProvider", readinessSource,
            StringComparison.OrdinalIgnoreCase);
    }

    private static Type[] ContractTypes() =>
    [
        typeof(PilotDeploymentReadinessContext), typeof(PilotDeploymentManifest),
        typeof(PilotEnvironmentValidationEvidence), typeof(PilotRollbackReadiness),
        typeof(PilotApprovalGate), typeof(PilotStopCondition),
        typeof(PilotMonitoringReadinessPlan), typeof(PilotDeploymentChecklistEntry),
        typeof(PilotDeploymentChecklist), typeof(PilotDeploymentEvidencePackage),
        typeof(PilotDeploymentReadinessResult)
    ];

    private static PilotDeploymentReadinessCoordinator Coordinator(
        PilotEnvironmentValidationKind? failedKind = null,
        PilotEnvironmentValidationKind? reviewKind = null) =>
        new(Validators(failedKind, reviewKind));

    private static IPilotEnvironmentReadinessValidator[] Validators(
        PilotEnvironmentValidationKind? failedKind = null,
        PilotEnvironmentValidationKind? reviewKind = null) =>
        Enum.GetValues<PilotEnvironmentValidationKind>().Select(kind =>
            (IPilotEnvironmentReadinessValidator)new ImmutablePilotEnvironmentReadinessValidator(
                new PilotEnvironmentValidationEvidence(kind,
                    kind == failedKind ? PilotReadinessGateStatus.Failed :
                    kind == reviewKind ? PilotReadinessGateStatus.RequiresReview :
                    PilotReadinessGateStatus.Passed,
                    $"environment-{kind.ToString().ToLowerInvariant()}-1", Now.AddMinutes(-2))))
            .ToArray();

    private static PilotDeploymentReadinessContext Context(
        bool explicitRequest = true,
        string readinessId = "readiness-1",
        PilotValidationResultStatus validationStatus = PilotValidationResultStatus.Completed,
        IEnumerable<PilotDeploymentFeature>? features = null,
        IEnumerable<string>? approvalReferences = null) => new(readinessId,
            "pilot-scope-rasht-ramsar", "environment-controlled-1",
            features ?? Enum.GetValues<PilotDeploymentFeature>(), validationStatus,
            approvalReferences ?? ApprovalReferences(), "rollback-plan-1", Now,
            explicitRequest);

    private static PilotDeploymentManifest Manifest(
        PilotReadinessGateStatus status = PilotReadinessGateStatus.Passed) => new(
            "manifest-1", "version-8.9", "build-fingerprint-1",
            ["application-artifact-1", "test-artifact-1"],
            ["net8-runtime", "sqlite-provider"], status);

    private static PilotRollbackReadiness Rollback(
        PilotRollbackValidationStatus status = PilotRollbackValidationStatus.Ready) => new(
            "rollback-plan-1", "restore-point-1", status, "rollback-owner-1",
            "rollback-evidence-1");

    private static PilotApprovalGate[] Approvals(
        PilotApprovalGateKind? changedKind = null,
        PilotApprovalGateStatus changedStatus = PilotApprovalGateStatus.Approved) =>
        Enum.GetValues<PilotApprovalGateKind>().Select(kind => new PilotApprovalGate(kind,
            kind == changedKind ? changedStatus : PilotApprovalGateStatus.Approved,
            ApprovalReference(kind), $"approval-evidence-{kind.ToString().ToLowerInvariant()}-1",
            Now.AddMinutes(-1))).ToArray();

    private static string[] ApprovalReferences() => Enum.GetValues<PilotApprovalGateKind>()
        .Select(ApprovalReference).Order(StringComparer.Ordinal).ToArray();

    private static string ApprovalReference(PilotApprovalGateKind kind) =>
        $"approval-{kind.ToString().ToLowerInvariant()}-1";

    private static PilotStopCondition[] StopConditions(
        PilotStopConditionKind? triggeredKind = null) =>
        Enum.GetValues<PilotStopConditionKind>().Select(kind => new PilotStopCondition(kind,
            kind == triggeredKind, $"stop-evidence-{kind.ToString().ToLowerInvariant()}-1"))
            .ToArray();

    private static PilotMonitoringReadinessPlan Monitoring() => new("monitoring-plan-1",
        Enum.GetValues<PilotMonitoringSignalKind>(), "monitoring-owner-1", "escalation-1");

    private static PilotDeploymentChecklist Checklist() => new(
        Enum.GetValues<PilotDeploymentChecklistItem>().Select(item =>
            new PilotDeploymentChecklistEntry(item, PilotReadinessGateStatus.Passed,
                $"checklist-evidence-{item.ToString().ToLowerInvariant()}-1")));

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName,
                   "Rah_Negar.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }

    private sealed class ThrowingValidator(PilotEnvironmentValidationKind kind) :
        IPilotEnvironmentReadinessValidator
    {
        public PilotEnvironmentValidationKind Kind { get; } = kind;
        public PilotEnvironmentValidationEvidence? Validate(
            PilotDeploymentReadinessContext context, PilotDeploymentManifest manifest) =>
            throw new InvalidOperationException("sensitive validator exception");
    }
}
