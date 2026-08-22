using System.Reflection;
using Rah_Negar.Foundation.Application.Activation;
using Rah_Negar.Foundation.Application.Database.Readiness;
using Rah_Negar.Foundation.Application.Integration;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Foundation.Time;

namespace Rah_Negar.Tests.Integration;

public sealed class ControlledProductionIntegrationDesignTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 14, 0, 0, TimeSpan.Zero);
    private const string EvidenceId = "integration-evidence-1";
    private const string CorrelationId = "integration-correlation-1";
    private const string DatabaseIdentity = "database-fingerprint-1";

    [Fact]
    public void Boundary_inventory_covers_every_required_Phase4_to_8_integration_area()
    {
        IntegrationBoundaryInventory inventory = IntegrationBoundaryInventory.CreateCurrent();

        Assert.Equal(Enum.GetValues<IntegrationBoundaryArea>().Length, inventory.Items.Count);
        Assert.Equal(Enum.GetValues<IntegrationBoundaryArea>(), inventory.Items.Select(item => item.Area).ToArray());
        Assert.All(inventory.Items, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.CurrentOwner));
            Assert.False(string.IsNullOrWhiteSpace(item.LegacyOwner));
            Assert.False(string.IsNullOrWhiteSpace(item.FutureOwner));
            Assert.False(string.IsNullOrWhiteSpace(item.IntegrationPoint));
            Assert.False(string.IsNullOrWhiteSpace(item.ActivationDependency));
            Assert.NotEmpty(item.RequiredPreviousPhases);
        });
    }

    [Theory]
    [InlineData(IntegrationAuthorityMode.LegacyOnly, true, false)]
    [InlineData(IntegrationAuthorityMode.ShadowValidation, true, true)]
    public void Routing_modes_keep_legacy_authoritative_until_explicit_target_decision(
        IntegrationAuthorityMode mode, bool legacyAuthority, bool targetReadOnly)
    {
        IntegrationAuthorityRoutingDecision result = Route(
            ControlledIntegrationFeature.RuntimeProjection, mode);

        Assert.Equal(IntegrationControlDecision.Allowed, result.Decision);
        Assert.Equal(mode, result.RequestedMode);
        Assert.Equal(mode, result.EffectiveMode);
        Assert.Equal(legacyAuthority, result.LegacyRemainsAuthoritative);
        Assert.Equal(targetReadOnly, result.TargetReadOnly);
        Assert.False(result.ProductionMutationAllowed);
        Assert.Equal(EvidenceId, result.EvidencePackageId);
        Assert.Equal(CorrelationId, result.CorrelationId);
    }

    [Fact]
    public void Unknown_routing_mode_is_rejected_without_hidden_legacy_fallback()
    {
        var unknown = (IntegrationAuthorityMode)999;
        IntegrationAuthorityRoutingDecision result = Route(
            ControlledIntegrationFeature.RuntimeProjection, unknown);

        Assert.Equal(IntegrationControlDecision.Blocked, result.Decision);
        Assert.Null(result.EffectiveMode);
        Assert.Contains("unknown-integration-mode", result.Reasons);
    }

    [Fact]
    public void Pilot_mode_is_blocked_without_isolation_rollback_and_approved_feature_decision()
    {
        IntegrationAuthorityRoutingDecision result = Route(
            ControlledIntegrationFeature.Authentication, IntegrationAuthorityMode.PilotTarget,
            featureDecision: null, pilot: null);

        Assert.Equal(IntegrationControlDecision.Blocked, result.Decision);
        Assert.Null(result.EffectiveMode);
        Assert.Contains("target-routing-requires-approved-feature-decision", result.Reasons);
        Assert.Contains("pilot-boundary-or-rollback-not-ready", result.Reasons);
    }

    [Fact]
    public async Task Generalized_shadow_comparison_preserves_legacy_authority_and_propagates_evidence()
    {
        var target = new ReadOnlyTargetEvaluator(12);
        var coordinator = new GeneralizedShadowComparisonCoordinator<string, int, int>(
            new LegacyReader(10), target, new IntegerComparer());
        ShadowComparisonEvidenceMetadata evidence = ShadowEvidence();

        GeneralizedShadowComparisonResult<int, int> result = await coordinator.CompareAsync("request", evidence);

        Assert.True(result.Succeeded);
        Assert.True(result.LegacyRemainsAuthoritative);
        Assert.False(result.TargetProductionMutationAllowed);
        Assert.Equal(10, result.LegacyResult);
        Assert.Equal(12, result.TargetResult);
        Assert.Equal(ShadowDifferenceSeverity.Warning, result.Assessment.Severity);
        Assert.Single(result.Assessment.Differences);
        Assert.Equal(EvidenceId, result.Evidence.EvidenceId);
        Assert.Equal(CorrelationId, result.Evidence.CorrelationId);
        Assert.Equal(1, target.EvaluationCount);
        Assert.Equal(0, target.ProductionMutationCount);
    }

    [Fact]
    public async Task Shadow_target_failure_returns_safe_evidence_and_never_changes_authority()
    {
        var coordinator = new GeneralizedShadowComparisonCoordinator<string, int, int>(
            new LegacyReader(10), new ThrowingTargetEvaluator(), new IntegerComparer());

        GeneralizedShadowComparisonResult<int, int> result =
            await coordinator.CompareAsync("request", ShadowEvidence());

        Assert.False(result.Succeeded);
        Assert.True(result.LegacyRemainsAuthoritative);
        Assert.False(result.TargetProductionMutationAllowed);
        Assert.Equal(10, result.LegacyResult);
        Assert.Equal("ShadowComparisonFailed", result.ResultCategory);
        Assert.Equal(ShadowDifferenceSeverity.Failed, result.Assessment.Severity);
    }

    [Fact]
    public void Feature_activation_coordinator_blocks_missing_approval_and_allows_complete_bound_evidence()
    {
        var coordinator = new FeatureIntegrationActivationCoordinator(new FixedClock(Now));
        ActivationEvidencePackage evidence = CompleteActivationEvidence();
        var missing = new FeatureIntegrationActivationRequest(evidence, null,
            ControlledIntegrationFeature.Authentication, "station-1", CorrelationId);
        FeatureIntegrationApproval approval = FeatureApproval(
            ControlledIntegrationFeature.Authentication, "station-1");

        FeatureIntegrationActivationDecision blocked = coordinator.Evaluate(missing);
        FeatureIntegrationActivationDecision allowed = coordinator.Evaluate(missing with { Approval = approval });

        Assert.Equal(IntegrationControlDecision.Blocked, blocked.Decision);
        Assert.Contains("feature-approval-required", blocked.Reasons);
        Assert.Equal(IntegrationControlDecision.Allowed, allowed.Decision);
        Assert.Empty(allowed.Reasons);
    }

    [Fact]
    public void Feature_activation_coordinator_routes_historical_adoption_to_manual_review()
    {
        var coordinator = new FeatureIntegrationActivationCoordinator(new FixedClock(Now));
        ActivationEvidencePackage evidence = CompleteActivationEvidence(
            MigrationHistoryClassification.HistoricalDraftRecognized);
        var request = new FeatureIntegrationActivationRequest(evidence,
            FeatureApproval(ControlledIntegrationFeature.Authentication, "station-1"),
            ControlledIntegrationFeature.Authentication, "station-1", CorrelationId);

        FeatureIntegrationActivationDecision result = coordinator.Evaluate(request);

        Assert.Equal(IntegrationControlDecision.RequiresManualReview, result.Decision);
        Assert.Contains("migration-adoption-requires-manual-review", result.Reasons);
    }

    [Fact]
    public void Authentication_shadow_keeps_legacy_login_authoritative_and_uses_only_ShiftProfile_target()
    {
        IntegrationAuthorityRoutingDecision routing = Route(
            ControlledIntegrationFeature.Authentication, IntegrationAuthorityMode.ShadowValidation);
        var request = new AuthenticationIntegrationRequest(AuthenticationIntegrationMode.ShiftProfileShadow,
            new(true, "legacy-session", "LegacyAccepted"),
            new(true, "shift-profile-1", "station-1", 1, "TargetAccepted"),
            routing, false);

        AuthenticationIntegrationDecision result = AuthenticationIntegrationPolicy.Evaluate(request);

        Assert.Equal(IntegrationControlDecision.Allowed, result.Decision);
        Assert.True(result.LegacyLoginAuthoritative);
        Assert.Equal("shift-profile-1", result.TargetShiftProfileId);
        string contractNames = string.Join('|', typeof(AuthenticationIntegrationRequest).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(AuthenticationIntegrationRequest).Namespace)
            .Select(type => type.Name));
        Assert.DoesNotContain("Role", contractNames, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupportProfile", contractNames, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupportLogin", contractNames, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Authentication_target_is_blocked_without_migration_readiness()
    {
        FeatureIntegrationActivationDecision approval = AllowedFeatureDecision(
            ControlledIntegrationFeature.Authentication, "station-1");
        PilotBoundaryValidationResult pilot = ValidPilotResult();
        IntegrationAuthorityRoutingDecision routing = Route(
            ControlledIntegrationFeature.Authentication, IntegrationAuthorityMode.PilotTarget,
            approval, pilot, migrationReady: false);

        Assert.Equal(IntegrationControlDecision.Blocked, routing.Decision);
        Assert.Contains("authentication-target-requires-migration-readiness", routing.Reasons);
    }

    [Fact]
    public void Reporting_shadow_requires_immutable_validated_snapshots_and_keeps_legacy_readable()
    {
        IntegrationAuthorityRoutingDecision routing = Route(
            ControlledIntegrationFeature.SnapshotReporting, IntegrationAuthorityMode.ShadowValidation);
        var valid = new ReportingIntegrationRequest(ReportingIntegrationMode.SnapshotShadow, routing,
            new(true, true, true, false, false, "snapshot-comparison-1"));
        var invalid = valid with { Evidence = valid.Evidence with { FinalizedSnapshotImmutable = false } };

        ReportingIntegrationDecision validResult = ReportingIntegrationPolicy.Evaluate(valid);
        ReportingIntegrationDecision invalidResult = ReportingIntegrationPolicy.Evaluate(invalid);

        Assert.Equal(IntegrationControlDecision.Allowed, validResult.Decision);
        Assert.True(validResult.LegacyReportingAuthoritative);
        Assert.True(validResult.SnapshotReadAllowed);
        Assert.Equal(IntegrationControlDecision.Blocked, invalidResult.Decision);
    }

    [Fact]
    public void Reporting_authority_is_blocked_without_snapshot_validation()
    {
        IntegrationAuthorityRoutingDecision routing = Route(
            ControlledIntegrationFeature.SnapshotReporting, IntegrationAuthorityMode.FullTarget,
            AllowedFeatureDecision(ControlledIntegrationFeature.SnapshotReporting, "station-1"),
            snapshotValidated: false);

        Assert.Equal(IntegrationControlDecision.Blocked, routing.Decision);
        Assert.Contains("reporting-target-requires-snapshot-validation", routing.Reasons);
    }

    [Fact]
    public void Runtime_Event_shadow_is_read_only_and_blocks_mutation_or_recalculation_side_effects()
    {
        IntegrationAuthorityRoutingDecision routing = Route(
            ControlledIntegrationFeature.RuntimeProjection, IntegrationAuthorityMode.ShadowValidation);
        var valid = new RuntimeEventIntegrationRequest(RuntimeEventIntegrationMode.ShadowComparison, routing,
            new("runtime-comparison-1", "event-comparison-1", true, false, false, true));
        var mutation = valid with { Evidence = valid.Evidence with { MutationAttempted = true } };
        var recalculation = valid with { Evidence = valid.Evidence with { RecalculationSideEffects = true } };

        RuntimeEventIntegrationDecision validResult = RuntimeEventIntegrationPolicy.Evaluate(valid);

        Assert.Equal(IntegrationControlDecision.Allowed, validResult.Decision);
        Assert.True(validResult.LegacyProjectionAuthoritative);
        Assert.True(validResult.TargetProjectionReadOnly);
        Assert.Equal(IntegrationControlDecision.Blocked,
            RuntimeEventIntegrationPolicy.Evaluate(mutation).Decision);
        Assert.Equal(IntegrationControlDecision.Blocked,
            RuntimeEventIntegrationPolicy.Evaluate(recalculation).Decision);
    }

    [Fact]
    public void Protected_settings_shadow_and_pilot_never_provision_or_cut_over_ESD()
    {
        IntegrationAuthorityRoutingDecision shadowRouting = Route(
            ControlledIntegrationFeature.ProtectedSettings, IntegrationAuthorityMode.ShadowValidation);
        var shadow = new ProtectedSettingsIntegrationRequest(
            ProtectedSettingsIntegrationMode.ProtectedSettingsShadow, shadowRouting,
            new(EsdAuthorityMode.LegacyAuthoritative, true, false, false, false, "settings-evidence-1"));
        ProtectedSettingsIntegrationDecision shadowResult = ProtectedSettingsIntegrationPolicy.Evaluate(shadow);
        var cutover = shadow with { Evidence = shadow.Evidence with { EsdCutoverRequested = true } };
        var provisioning = shadow with
        {
            Evidence = shadow.Evidence with { TargetProvisioningRequested = true }
        };

        Assert.Equal(IntegrationControlDecision.Allowed, shadowResult.Decision);
        Assert.True(shadowResult.LegacySettingsAuthoritative);
        Assert.False(shadowResult.TargetProvisioningAllowed);
        Assert.Equal(IntegrationControlDecision.Blocked,
            ProtectedSettingsIntegrationPolicy.Evaluate(cutover).Decision);
        Assert.Equal(IntegrationControlDecision.Blocked,
            ProtectedSettingsIntegrationPolicy.Evaluate(provisioning).Decision);
    }

    [Fact]
    public void Central_safety_rules_reject_ESD_cutover_during_any_feature_activation()
    {
        IntegrationAuthorityRoutingDecision result = Route(
            ControlledIntegrationFeature.ProtectedSettings, IntegrationAuthorityMode.ShadowValidation,
            esdCutover: true);

        Assert.Equal(IntegrationControlDecision.Blocked, result.Decision);
        Assert.Contains("esd-cutover-prohibited-during-feature-integration", result.Reasons);
    }

    [Fact]
    public void Pilot_boundary_requires_isolation_selected_station_shifts_limited_features_and_rollback()
    {
        PilotEnvironmentBoundary valid = ValidPilot();
        var invalid = new PilotEnvironmentBoundary("pilot-2", false, "station-1", ["shift-1"],
            [ControlledIntegrationFeature.Authentication], false, EvidenceId, CorrelationId);

        PilotBoundaryValidationResult validResult = PilotEnvironmentBoundaryValidator.Validate(valid);
        PilotBoundaryValidationResult invalidResult = PilotEnvironmentBoundaryValidator.Validate(invalid);

        Assert.True(validResult.IsValid);
        Assert.True(validResult.RollbackToLegacyAvailable);
        Assert.False(valid.ProductionRegistrationAllowed);
        Assert.False(valid.ActivationPerformed);
        Assert.False(invalidResult.IsValid);
        Assert.Contains("pilot-must-be-isolated", invalidResult.Issues);
        Assert.Contains("pilot-rollback-to-legacy-required", invalidResult.Issues);
    }

    [Fact]
    public void Integration_dependency_graph_is_ordered_and_monitoring_plan_covers_all_signals()
    {
        IntegrationDependencyGraph graph = IntegrationDependencyGraph.CreateDefault();
        var plan = new IntegrationMonitoringPlan(Enum.GetValues<IntegrationMonitoringSignalKind>(),
            "monitor-owner", "rollback-owner");

        Assert.Empty(IntegrationDependencyGraphValidator.Validate(graph));
        Assert.Equal(graph.Nodes.OrderBy(node => node.ActivationOrder), graph.Nodes);
        Assert.All(graph.Nodes, node => Assert.NotEmpty(node.RequiredApprovals));
        Assert.True(plan.IsComplete);
    }

    [Fact]
    public void Integration_namespace_has_no_executor_telemetry_database_UI_startup_RBAC_or_Support_identity()
    {
        Assembly assembly = typeof(IntegrationAuthorityRoutingDecision).Assembly;
        Type[] types = assembly.GetTypes().Where(type =>
            type.Namespace == typeof(IntegrationAuthorityRoutingDecision).Namespace).ToArray();
        string surface = string.Join('|', types.Select(type => type.FullName)
            .Concat(types.SelectMany(type => type.GetInterfaces()).Select(type => type.FullName)));

        Assert.DoesNotContain("Microsoft.Data.Sqlite", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.Windows.Forms", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MigrationRunner", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Startup", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupportProfile", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupportLogin", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RoleId", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(types, type => type.IsClass && !type.IsAbstract &&
            typeof(IIntegrationMonitoringSink).IsAssignableFrom(type));
        Assert.DoesNotContain(types, type => type.Name.Contains("Executor", StringComparison.OrdinalIgnoreCase));
    }

    private static IntegrationAuthorityRoutingDecision Route(
        ControlledIntegrationFeature feature,
        IntegrationAuthorityMode mode,
        FeatureIntegrationActivationDecision? featureDecision = null,
        PilotBoundaryValidationResult? pilot = null,
        bool migrationReady = true,
        bool snapshotValidated = true,
        bool esdCutover = false)
    {
        var safety = new IntegrationSafetyContext(feature, mode, EvidenceId, CorrelationId,
            migrationReady, snapshotValidated, esdCutover, featureDecision, pilot);
        return IntegrationAuthorityRoutingPolicy.Route(new(feature, mode, "station-1",
            EvidenceId, CorrelationId, safety));
    }

    private static FeatureIntegrationActivationDecision AllowedFeatureDecision(
        ControlledIntegrationFeature feature, string targetScope) =>
        new(IntegrationControlDecision.Allowed, feature, targetScope, EvidenceId,
            CorrelationId, "approval-1", Array.Empty<string>());

    private static PilotEnvironmentBoundary ValidPilot() => new("pilot-1", true, "station-1",
        ["shift-1", "shift-2"],
        [ControlledIntegrationFeature.Authentication, ControlledIntegrationFeature.SnapshotReporting],
        true, EvidenceId, CorrelationId);

    private static PilotBoundaryValidationResult ValidPilotResult() =>
        PilotEnvironmentBoundaryValidator.Validate(ValidPilot());

    private static ShadowComparisonEvidenceMetadata ShadowEvidence() => new(EvidenceId,
        CorrelationId, ControlledIntegrationFeature.RuntimeProjection, "station-1", Now,
        "legacy-v1", "target-v1");

    private static FeatureIntegrationApproval FeatureApproval(
        ControlledIntegrationFeature feature, string targetScope)
    {
        ProductionActivationScope scope = FeatureIntegrationActivationCoordinator.MapScope(feature);
        var approval = new ProductionActivationApproval("approval-1", "operator-1", Now.AddMinutes(-2),
            scope, DatabaseIdentity, EvidenceId, CorrelationId, Now.AddMinutes(30));
        return new(approval, feature, targetScope);
    }

    private static ActivationEvidencePackage CompleteActivationEvidence(
        MigrationHistoryClassification classification = MigrationHistoryClassification.CleanLegacyBaseline)
    {
        var preservation = new ActivationSnapshotPreservationEvidence(
            true, true, true, true, true, true, true);
        return new(EvidenceId, CorrelationId, DatabaseIdentity,
            new(DatabaseIdentity, true, true, true, true, true, Now.AddMinutes(-30)),
            new(classification, 4,
                classification is MigrationHistoryClassification.CleanLegacyBaseline or
                    MigrationHistoryClassification.CleanUnifiedTarget,
                classification != MigrationHistoryClassification.ChecksumMismatch),
            new("backup-receipt-1", DatabaseIdentity, "backup-fingerprint-1",
                true, true, 4096, Now.AddMinutes(-20)),
            new("rehearsal-receipt-1", true, true, true, 4,
                EsdReconciliationState.ReadyToProvision, EsdAuthorityMode.LegacyAuthoritative,
                preservation, Now.AddMinutes(-10)),
            new(true, true, true, true),
            new(true, ProductionActivationScope.AuthenticationWorkflowActivation,
                DatabaseIdentity, EvidenceId),
            Now.AddMinutes(-5));
    }

    private sealed record FixedClock(DateTimeOffset UtcNow) : IClock
    {
        public DateTimeOffset LocalNow => UtcNow.ToOffset(TimeSpan.FromHours(3.5));
    }

    private sealed class LegacyReader(int value) : ILegacyShadowResultReader<string, int>
    {
        public Task<int> ReadAuthoritativeAsync(string request,
            CancellationToken cancellationToken = default) => Task.FromResult(value);
    }

    private sealed class ReadOnlyTargetEvaluator(int value) : IReadOnlyTargetShadowEvaluator<string, int>
    {
        public int EvaluationCount { get; private set; }
        public int ProductionMutationCount { get; private set; }

        public Task<int> EvaluateReadOnlyAsync(string request,
            CancellationToken cancellationToken = default)
        {
            EvaluationCount++;
            return Task.FromResult(value);
        }
    }

    private sealed class ThrowingTargetEvaluator : IReadOnlyTargetShadowEvaluator<string, int>
    {
        public Task<int> EvaluateReadOnlyAsync(string request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("test-only target failure");
    }

    private sealed class IntegerComparer : IShadowResultComparer<int, int>
    {
        public ShadowComparisonAssessment Compare(int legacyResult, int targetResult) =>
            legacyResult == targetResult
                ? new("same", ShadowDifferenceSeverity.None, Array.Empty<ShadowComparisonDifference>())
                : new("difference-10-12", ShadowDifferenceSeverity.Warning,
                    [new("value-difference", "Legacy and target values differ.")]);
    }
}
