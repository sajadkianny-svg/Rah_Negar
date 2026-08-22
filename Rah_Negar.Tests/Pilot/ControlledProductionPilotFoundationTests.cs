using System.Reflection;
using System.Security.Cryptography;
using Rah_Negar.Foundation.Application.Activation;
using Rah_Negar.Foundation.Application.Activation.Preparation;
using Rah_Negar.Foundation.Application.Pilot.Deployment;
using Rah_Negar.Foundation.Application.Pilot.Production;
using Rah_Negar.Foundation.Application.Pilot.Validation;

namespace Rah_Negar.Tests.Pilot;

public sealed class ControlledProductionPilotFoundationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 23, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Pilot_context_is_immutable_explicit_and_defensively_copied()
    {
        var operators = new List<string> { "operator-b", "operator-a", "operator-a" };
        var features = new List<PilotValidationWorkflow>
        {
            PilotValidationWorkflow.Reporting,
            PilotValidationWorkflow.Authentication,
            PilotValidationWorkflow.Reporting
        };

        ControlledProductionPilotContext context = Context(operators, features);
        operators.Add("operator-late");
        features.Add(PilotValidationWorkflow.Export);

        Assert.Equal(["operator-a", "operator-b"], context.SelectedOperators);
        Assert.Equal([PilotValidationWorkflow.Authentication,
            PilotValidationWorkflow.Reporting], context.ApprovedFeatures);
        Assert.False(context.AutomaticallyActivates);
        Assert.False(context.DiscoversEnvironment);
        Assert.False(context.FallsBackToProduction);
        Assert.False(context.ChangesAuthority);
        Assert.All(ContractTypes(), type => Assert.DoesNotContain(type.GetProperties(),
            property => property.SetMethod is not null));
    }

    [Fact]
    public void Operator_approval_is_evidence_only_without_authentication_or_permissions()
    {
        ControlledPilotOperatorApproval approval = OperatorApproval("operator-a");

        Assert.Equal(ControlledProductionPilotScope.RashtAndRamsarReadOnlyObservation,
            approval.ApprovedScope);
        Assert.False(approval.AuthenticatesOperator);
        Assert.False(approval.ReplacesLogin);
        Assert.False(approval.ImplementsRbac);
        Assert.False(approval.CreatesPermission);
    }

    [Fact]
    public async Task Full_lifecycle_requires_each_explicit_transition()
    {
        using ControlledProductionPilotCoordinator coordinator = Coordinator();

        Assert.Equal(ControlledPilotSessionState.Created, coordinator.State);
        Assert.Equal(ControlledPilotOperationStatus.Blocked,
            coordinator.Start("session-1", Now).Status);
        Assert.Equal(ControlledPilotSessionState.Created, coordinator.State);
        AssertAccepted(coordinator.Approve(Now.AddMinutes(-1)),
            ControlledPilotSessionState.Approved);
        AssertAccepted(coordinator.Start("session-1", Now),
            ControlledPilotSessionState.Started);
        AssertAccepted(coordinator.BeginObservation(Now.AddMinutes(5)),
            ControlledPilotSessionState.Observing);
        ControlledPilotSessionOperationResult observed = await coordinator.ObserveAsync(
            Now.AddMinutes(10));
        AssertAccepted(observed, ControlledPilotSessionState.Observing);
        Assert.NotNull(observed.Evidence);
        AssertAccepted(coordinator.Complete(Now.AddMinutes(15)),
            ControlledPilotSessionState.Completed);
        Assert.Equal(ControlledPilotOperationStatus.Blocked,
            coordinator.Start("session-2", Now.AddMinutes(20)).Status);
        coordinator.Dispose();
        Assert.Equal(ControlledPilotSessionState.Disposed, coordinator.State);
    }

    [Theory]
    [InlineData(PilotValidationWorkflow.Authentication)]
    [InlineData(PilotValidationWorkflow.Reporting)]
    [InlineData(PilotValidationWorkflow.RuntimeEvent)]
    [InlineData(PilotValidationWorkflow.ProtectedSettings)]
    [InlineData(PilotValidationWorkflow.Export)]
    public async Task Every_feature_uses_a_typed_read_only_observation_boundary(
        PilotValidationWorkflow feature)
    {
        using ControlledProductionPilotCoordinator coordinator = Coordinator(
            features: [feature]);
        AdvanceToObserving(coordinator);

        ControlledPilotSessionOperationResult result = await coordinator.ObserveAsync(
            Now.AddMinutes(10));

        ControlledPilotObservationResult observation = Assert.Single(
            result.Evidence!.Observations);
        Assert.Equal(feature, observation.Feature);
        Assert.Equal(ControlledPilotObservationStatus.Match, observation.Status);
        Assert.True(observation.IsReadOnly);
        Assert.False(observation.MutatesProduction);
        Assert.True(observation.LegacyAuthorityPreserved);
        Assert.False(result.ChangedAuthority);
        Assert.False(result.ExecutedMigration);
        Assert.False(result.MutatedDatabase);
        Assert.False(result.MutatedSettings);
        Assert.False(result.ExecutedEsd);
        Assert.False(result.ActivatedFeature);
    }

    [Fact]
    public async Task Difference_creates_attention_monitoring_evidence_only()
    {
        IControlledProductionPilotObserver[] observers = Observers(
            differenceFeature: PilotValidationWorkflow.Reporting);
        using ControlledProductionPilotCoordinator coordinator = Coordinator(observers:
            observers);
        AdvanceToObserving(coordinator);

        ControlledPilotSessionOperationResult result = await coordinator.ObserveAsync(
            Now.AddMinutes(10));

        PilotMonitoringEvidence monitoring = result.Evidence!.MonitoringEvidence;
        Assert.Equal(ControlledPilotHealthStatus.AttentionRequired, monitoring.HealthStatus);
        Assert.Equal("difference-summary-recorded", monitoring.DifferenceSummary);
        Assert.False(monitoring.ContainsSecrets);
        Assert.False(monitoring.ContainsCredentialMaterial);
        Assert.False(monitoring.ContainsRawLogs);
        Assert.False(monitoring.ContainsDatabaseContent);
        Assert.False(monitoring.ImplementsTelemetry);
        Assert.True(result.Evidence.LegacyAuthorityPreserved);
        Assert.False(result.Evidence.MutatedProduction);
    }

    [Theory]
    [InlineData(ControlledPilotStopReason.ValidationFailure)]
    [InlineData(ControlledPilotStopReason.OperatorStop)]
    [InlineData(ControlledPilotStopReason.EvidenceMismatch)]
    [InlineData(ControlledPilotStopReason.SecurityConcern)]
    [InlineData(ControlledPilotStopReason.RollbackRequested)]
    public void Every_stop_reason_records_only_and_never_rolls_back(
        ControlledPilotStopReason reason)
    {
        using ControlledProductionPilotCoordinator coordinator = Coordinator();
        AssertAccepted(coordinator.Approve(Now.AddMinutes(-1)),
            ControlledPilotSessionState.Approved);
        AssertAccepted(coordinator.Start("session-1", Now),
            ControlledPilotSessionState.Started);
        PilotStopDecision decision = StopDecision(reason);

        ControlledPilotSessionOperationResult result = coordinator.Stop(decision);

        AssertAccepted(result, ControlledPilotSessionState.Stopped);
        Assert.Same(decision, result.StopDecision);
        Assert.False(decision.ExecutesRollback);
        Assert.False(decision.PerformsDestructiveAction);
        Assert.False(decision.AutomaticallyStopsProduction);
        Assert.Equal(RollbackEvidenceStatus.Verified, Rollback().ValidationStatus);
    }

    [Fact]
    public void Missing_operator_approval_fails_closed()
    {
        using ControlledProductionPilotCoordinator coordinator = Coordinator(
            approvals: [OperatorApproval("operator-a")]);

        ControlledPilotSessionOperationResult result = coordinator.Approve(
            Now.AddMinutes(-1));

        Assert.Equal(ControlledPilotOperationStatus.Failed, result.Status);
        Assert.Equal(ControlledPilotSessionState.Failed, coordinator.State);
        Assert.Equal("production-pilot-operator-approval-invalid", result.ReasonCode);
    }

    [Fact]
    public void Invalid_context_and_unapproved_preparation_fail_closed()
    {
        var invalidContext = Context(pilotId: "SELECT-password");
        using ControlledProductionPilotCoordinator invalid = Coordinator(context: invalidContext);
        ProductionActivationReadinessResult blockedPreparation = Preparation(approved: false);
        using ControlledProductionPilotCoordinator unprepared = Coordinator(
            preparation: blockedPreparation);

        ControlledPilotSessionOperationResult invalidResult = invalid.Approve(
            Now.AddMinutes(-1));
        ControlledPilotSessionOperationResult preparationResult = unprepared.Approve(
            Now.AddMinutes(-1));

        Assert.Equal("production-pilot-context-invalid", invalidResult.ReasonCode);
        Assert.Equal("production-pilot-preparation-evidence-invalid",
            preparationResult.ReasonCode);
        Assert.Equal(ControlledPilotSessionState.Failed, invalid.State);
        Assert.Equal(ControlledPilotSessionState.Failed, unprepared.State);
    }

    [Fact]
    public async Task Observer_failure_isolated_with_fixed_reason()
    {
        IControlledProductionPilotObserver[] observers = Observers().Select(observer =>
            observer.Feature == PilotValidationWorkflow.Authentication
                ? (IControlledProductionPilotObserver)new ThrowingAuthenticationObserver()
                : observer).ToArray();
        using ControlledProductionPilotCoordinator coordinator = Coordinator(observers:
            observers);
        AdvanceToObserving(coordinator);

        ControlledPilotSessionOperationResult result = await coordinator.ObserveAsync(
            Now.AddMinutes(10));

        Assert.Equal(ControlledPilotOperationStatus.Failed, result.Status);
        Assert.Equal("production-pilot-observer-failed", result.ReasonCode);
        Assert.Equal(ControlledPilotSessionState.Failed, coordinator.State);
        Assert.DoesNotContain("exception", result.ReasonCode,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Monitoring_failure_isolated_with_fixed_reason()
    {
        using ControlledProductionPilotCoordinator coordinator = Coordinator(
            monitoringFactory: new ThrowingMonitoringFactory());
        AdvanceToObserving(coordinator);

        ControlledPilotSessionOperationResult result = await coordinator.ObserveAsync(
            Now.AddMinutes(10));

        Assert.Equal(ControlledPilotOperationStatus.Failed, result.Status);
        Assert.Equal("production-pilot-monitoring-failed", result.ReasonCode);
        Assert.Equal(ControlledPilotSessionState.Failed, coordinator.State);
        Assert.Null(coordinator.LastEvidence);
    }

    [Fact]
    public void Invalid_stop_decision_fails_without_action()
    {
        using ControlledProductionPilotCoordinator coordinator = Coordinator();
        AssertAccepted(coordinator.Approve(Now.AddMinutes(-1)),
            ControlledPilotSessionState.Approved);
        AssertAccepted(coordinator.Start("session-1", Now),
            ControlledPilotSessionState.Started);
        var invalid = new PilotStopDecision("decision-1", "different-pilot", "session-1",
            ControlledPilotStopReason.OperatorStop, "stop-evidence-1", Now.AddMinutes(5));

        ControlledPilotSessionOperationResult result = coordinator.Stop(invalid);

        Assert.Equal(ControlledPilotOperationStatus.Failed, result.Status);
        Assert.Equal("production-pilot-stop-decision-invalid", result.ReasonCode);
        Assert.Equal(ControlledPilotSessionState.Failed, coordinator.State);
        Assert.Null(coordinator.StopDecision);
    }

    [Fact]
    public async Task Cancellation_fails_closed_without_restart_or_evidence()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using ControlledProductionPilotCoordinator coordinator = Coordinator();
        AdvanceToObserving(coordinator);

        ControlledPilotSessionOperationResult result = await coordinator.ObserveAsync(
            Now.AddMinutes(10), cancellation.Token);

        Assert.Equal(ControlledPilotOperationStatus.Canceled, result.Status);
        Assert.Equal(ControlledPilotSessionState.Failed, coordinator.State);
        Assert.Null(coordinator.LastEvidence);
        Assert.Equal(ControlledPilotOperationStatus.Blocked,
            coordinator.Start("session-2", Now.AddMinutes(15)).Status);
    }

    [Fact]
    public void Disposed_coordinator_is_terminal_and_non_throwing()
    {
        var coordinator = Coordinator();

        coordinator.Dispose();
        coordinator.Dispose();
        ControlledPilotSessionOperationResult result = coordinator.Approve(
            Now.AddMinutes(-1));

        Assert.Equal(ControlledPilotSessionState.Disposed, coordinator.State);
        Assert.Equal(ControlledPilotOperationStatus.Disposed, result.Status);
        Assert.Equal("production-pilot-disposed", result.ReasonCode);
    }

    [Fact]
    public void Coordinator_has_no_production_execution_or_replacement_capability()
    {
        using ControlledProductionPilotCoordinator coordinator = Coordinator();

        Assert.False(coordinator.AutomaticallyActivates);
        Assert.False(coordinator.AutomaticallyRestarts);
        Assert.False(coordinator.UsesScheduler);
        Assert.False(coordinator.UsesBackgroundExecution);
        Assert.False(coordinator.UsesPolling);
        Assert.False(coordinator.ChangesAuthority);
        Assert.False(coordinator.ExecutesMigration);
        Assert.False(coordinator.MutatesProductionData);
        Assert.False(coordinator.ModifiesSettings);
        Assert.False(coordinator.CreatesUsers);
        Assert.False(coordinator.ExecutesEsd);
        Assert.False(coordinator.ActivatesFeatures);
        Assert.False(coordinator.ReplacesLogin);
        Assert.False(coordinator.ReplacesSettings);
        Assert.False(coordinator.ReplacesReporting);
        Assert.False(coordinator.ReplacesRuntimeEvents);
        Assert.False(coordinator.ReplacesExport);
        Assert.False(coordinator.CreatesRbac);
        Assert.False(coordinator.UsesSupportIdentity);
    }

    [Fact]
    public void Production_pilot_namespace_has_no_database_migration_UI_host_or_executor_dependency()
    {
        Type[] types = typeof(ControlledProductionPilotCoordinator).Assembly.GetTypes()
            .Where(type => type.Namespace ==
                typeof(ControlledProductionPilotCoordinator).Namespace).ToArray();
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
        Assert.DoesNotContain("PilotHost", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PilotExecutionCoordinator", surface,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IProductionMigrationExecutor", surface,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Activate", methods, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Migrate", methods, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RollbackAsync", methods, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Startup_routes_and_production_forms_do_not_reference_production_pilot()
    {
        string root = RepositoryRoot();
        string programPath = Path.Combine(root, "Program.cs");
        string protectedSource = File.ReadAllText(programPath) + Environment.NewLine +
            string.Join(Environment.NewLine,
                Directory.GetFiles(Path.Combine(root, "UI", "Startup"), "*.cs",
                        SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(Path.Combine(root, "UI", "Forms"), "*.cs",
                        SearchOption.AllDirectories)).Select(File.ReadAllText));
        string pilotSource = string.Join(Environment.NewLine,
            Directory.GetFiles(Path.Combine(root, "Application", "Pilot", "Production"),
                "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));

        Assert.Equal("33985F732E77AFC7249DDA0174E8BCC58601B5E0B3E22B93E31933F01ACCAA76",
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(programPath))));
        Assert.DoesNotContain("ControlledProductionPilotCoordinator", protectedSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Application.Pilot.Production", protectedSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.Data.Sqlite", pilotSource,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MigrationRunner", pilotSource,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Task.Run", pilotSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new Timer", pilotSource, StringComparison.Ordinal);
        Assert.DoesNotContain("PeriodicTimer", pilotSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IServiceProvider", pilotSource,
            StringComparison.OrdinalIgnoreCase);
    }

    private static Type[] ContractTypes() =>
    [
        typeof(ControlledProductionPilotContext), typeof(ControlledPilotOperatorApproval),
        typeof(ControlledPilotObservationResult), typeof(PilotMonitoringEvidence),
        typeof(PilotStopDecision), typeof(ControlledPilotEvidence),
        typeof(ControlledPilotSessionOperationResult)
    ];

    private static ControlledProductionPilotCoordinator Coordinator(
        ControlledProductionPilotContext? context = null,
        ProductionActivationReadinessResult? preparation = null,
        RollbackVerificationResult? rollback = null,
        IEnumerable<ControlledPilotOperatorApproval>? approvals = null,
        IEnumerable<IControlledProductionPilotObserver>? observers = null,
        IPilotMonitoringEvidenceFactory? monitoringFactory = null,
        IEnumerable<PilotValidationWorkflow>? features = null)
    {
        PilotValidationWorkflow[] selected = (features ??
            Enum.GetValues<PilotValidationWorkflow>()).ToArray();
        ControlledProductionPilotContext resolvedContext = context ?? Context(features: selected);
        return new(resolvedContext, preparation ?? Preparation(), rollback ?? Rollback(),
            approvals ?? resolvedContext.SelectedOperators.Select(OperatorApproval),
            observers ?? Observers(features: selected),
            monitoringFactory ?? new DeterministicPilotMonitoringEvidenceFactory());
    }

    private static ControlledProductionPilotContext Context(
        IEnumerable<string>? operators = null,
        IEnumerable<PilotValidationWorkflow>? features = null,
        string pilotId = "production-pilot-1") => new(pilotId, "release-9.1",
            ControlledProductionPilotScope.RashtAndRamsarReadOnlyObservation,
            operators ?? ["operator-a", "operator-b"],
            features ?? Enum.GetValues<PilotValidationWorkflow>(),
            "preparation-1:cutover-evidence", "rollback-plan-1", "monitoring-plan-1",
            Now, Now.AddHours(1));

    private static ControlledPilotOperatorApproval OperatorApproval(string operatorReference) =>
        new(operatorReference, $"approval-{operatorReference}", Now.AddMinutes(-10),
            ControlledProductionPilotScope.RashtAndRamsarReadOnlyObservation);

    private static IControlledProductionPilotObserver[] Observers(
        IEnumerable<PilotValidationWorkflow>? features = null,
        PilotValidationWorkflow? differenceFeature = null) =>
        (features ?? Enum.GetValues<PilotValidationWorkflow>()).Select(feature =>
            Observer(feature, feature == differenceFeature)).ToArray();

    private static IControlledProductionPilotObserver Observer(
        PilotValidationWorkflow feature,
        bool difference)
    {
        var result = new ControlledPilotObservationResult(feature,
            difference ? ControlledPilotObservationStatus.Difference :
                ControlledPilotObservationStatus.Match,
            $"result-{feature.ToString().ToLowerInvariant()}-1",
            difference ? "validation-difference" : "validation-complete",
            difference ? "difference-recorded" : "difference-none",
            $"evidence-{feature.ToString().ToLowerInvariant()}-1", Now.AddMinutes(10));
        return feature switch
        {
            PilotValidationWorkflow.Authentication =>
                new ControlledAuthenticationPilotObserver(result),
            PilotValidationWorkflow.Reporting => new ControlledReportingPilotObserver(result),
            PilotValidationWorkflow.RuntimeEvent =>
                new ControlledRuntimeEventPilotObserver(result),
            PilotValidationWorkflow.ProtectedSettings =>
                new ControlledProtectedSettingsPilotObserver(result),
            PilotValidationWorkflow.Export => new ControlledExportPilotObserver(result),
            _ => throw new ArgumentOutOfRangeException(nameof(feature))
        };
    }

    private static void AdvanceToObserving(ControlledProductionPilotCoordinator coordinator)
    {
        AssertAccepted(coordinator.Approve(Now.AddMinutes(-1)),
            ControlledPilotSessionState.Approved);
        AssertAccepted(coordinator.Start("session-1", Now),
            ControlledPilotSessionState.Started);
        AssertAccepted(coordinator.BeginObservation(Now.AddMinutes(5)),
            ControlledPilotSessionState.Observing);
    }

    private static PilotStopDecision StopDecision(ControlledPilotStopReason reason) => new(
        "stop-decision-1", "production-pilot-1", "session-1", reason,
        "stop-evidence-1", Now.AddMinutes(5));

    private static RollbackVerificationResult Rollback() => new("rollback-plan-1",
        RollbackEvidenceStatus.Verified, "rollback-owner-1", "rollback-evidence-1");

    private static ProductionActivationReadinessResult Preparation(bool approved = true)
    {
        DateTimeOffset timestamp = Now.AddMinutes(-2);
        var context = new ProductionActivationPreparationContext("preparation-1",
            "release-9.1", ProductionActivationScope.AuthenticationWorkflowActivation,
            LegacyAuthorityState.LegacyAuthoritative, PilotValidationResultStatus.Completed,
            PilotDeploymentReadinessStatus.Ready, "rollback-plan-1",
            ["approval-dataowner-1", "approval-operations-1", "approval-security-1"],
            timestamp, explicitlyRequested: approved);
        ProductionActivationGate[] gates = Enum.GetValues<ProductionActivationGateType>()
            .Select(type => new ProductionActivationGate(type,
                ProductionActivationGateStatus.Satisfied, PreparationGateEvidence(type),
                "reviewer-1", timestamp.AddMinutes(-1))).ToArray();
        var backup = new BackupVerificationResult("backup-reference-1",
            BackupEvidenceStatus.Verified, RestoreTestStatus.Passed,
            timestamp.AddMinutes(-1));
        ProductionActivationStopCondition[] stops =
            Enum.GetValues<ProductionActivationStopConditionType>().Select(type =>
                new ProductionActivationStopCondition(type, false,
                    $"stop-preparation-{type.ToString().ToLowerInvariant()}-1")).ToArray();
        return new ProductionActivationReadinessCoordinator().Evaluate(context, gates,
            backup, Rollback(), stops);
    }

    private static string PreparationGateEvidence(ProductionActivationGateType type) => type switch
    {
        ProductionActivationGateType.SecurityReview => "approval-security-1",
        ProductionActivationGateType.OperationsReadiness => "approval-operations-1",
        ProductionActivationGateType.DataOwnerApproval => "approval-dataowner-1",
        ProductionActivationGateType.RollbackReadiness => "rollback-evidence-1",
        ProductionActivationGateType.ValidationCompletion => "validation-evidence-1",
        ProductionActivationGateType.DeploymentReadiness => "deployment-evidence-1",
        _ => "gate-evidence-1"
    };

    private static void AssertAccepted(
        ControlledPilotSessionOperationResult result,
        ControlledPilotSessionState expectedState)
    {
        Assert.Equal(ControlledPilotOperationStatus.Accepted, result.Status);
        Assert.Equal(expectedState, result.SessionState);
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName,
                   "Rah_Negar.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }

    private sealed class ThrowingAuthenticationObserver :
        IControlledAuthenticationPilotObserver
    {
        public PilotValidationWorkflow Feature => PilotValidationWorkflow.Authentication;
        public ValueTask<ControlledPilotObservationResult?> ObserveAsync(
            ControlledProductionPilotContext context, string sessionId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("sensitive observer exception");
    }

    private sealed class ThrowingMonitoringFactory : IPilotMonitoringEvidenceFactory
    {
        public PilotMonitoringEvidence Create(ControlledProductionPilotContext context,
            string sessionId, IReadOnlyList<ControlledPilotObservationResult> observations,
            RollbackVerificationResult rollback, DateTimeOffset observedAtUtc) =>
            throw new InvalidOperationException("sensitive monitoring exception");
    }
}
