using System.Reflection;
using Rah_Negar.Foundation.Application.Activation;
using Rah_Negar.Foundation.Application.Database.Readiness;
using Rah_Negar.Foundation.Application.Integration;
using Rah_Negar.Foundation.Application.Pilot;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Foundation.Time;

namespace Rah_Negar.Tests.Pilot;

public sealed class ControlledPilotImplementationFoundationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 16, 0, 0, TimeSpan.Zero);
    private const string PilotId = "pilot-82-1";
    private const string StationId = "station-rasht";
    private const string ShiftId = "shift-profile-1";
    private const string EvidenceId = "activation-evidence-82-1";
    private const string CorrelationId = "pilot-correlation-82-1";
    private const string DatabaseIdentity = "database-fingerprint-82-1";

    [Fact]
    public void Pilot_context_requires_explicit_non_wildcard_scope_and_is_immutable()
    {
        PilotExecutionContext valid = Context(PilotFeature.AuthenticationPilot);
        var shifts = new List<string> { ShiftId };
        var features = new List<PilotFeature> { PilotFeature.AuthenticationPilot };
        var copied = new PilotExecutionContext(PilotId, StationId, shifts, features,
            EvidenceId, CorrelationId, "rollback-1", Now.AddMinutes(-5), Now.AddMinutes(30));
        shifts.Add("shift-profile-2");
        features.Add(PilotFeature.ReportingPilot);
        var wildcard = new PilotExecutionContext(PilotId, "*", ["all"],
            [PilotFeature.AuthenticationPilot], EvidenceId, CorrelationId, "rollback-1",
            Now.AddMinutes(-5), Now.AddMinutes(30));

        Assert.True(PilotExecutionContextValidator.Validate(valid, Now).IsValid);
        Assert.Single(copied.SelectedShiftProfileIds);
        Assert.Single(copied.EnabledPilotFeatures);
        PilotContextValidationResult invalid = PilotExecutionContextValidator.Validate(wildcard, Now);
        Assert.False(invalid.IsValid);
        Assert.Contains("pilot-station-wildcard-prohibited", invalid.Issues);
        Assert.Contains("shift-profile-wildcard-prohibited", invalid.Issues);
        Assert.False(valid.EnabledByDefault);
        Assert.False(valid.ProductionRegistrationAllowed);
        Assert.False(valid.ProductionMutationAllowed);
    }

    [Fact]
    public void Pilot_context_expiration_fails_closed()
    {
        PilotExecutionContext expired = Context(PilotFeature.AuthenticationPilot,
            expiresAtUtc: Now);

        PilotContextValidationResult result = PilotExecutionContextValidator.Validate(expired, Now);

        Assert.False(result.IsValid);
        Assert.Contains("pilot-expired", result.Issues);
    }

    [Fact]
    public void Feature_registry_contains_exact_supported_features_and_none_is_enabled_by_default()
    {
        var registry = new PilotFeatureRegistry();

        Assert.Equal(Enum.GetValues<PilotFeature>(), registry.Features.Select(x => x.Feature).ToArray());
        Assert.All(registry.Features, feature =>
        {
            Assert.False(feature.EnabledByDefault);
            Assert.True(feature.RollbackRequired);
            Assert.NotEmpty(feature.RequiredDependencies);
            Assert.NotEmpty(feature.RequiredApprovals);
            Assert.False(string.IsNullOrWhiteSpace(feature.FeatureId));
        });
    }

    [Fact]
    public void Gateway_blocks_missing_approval_and_missing_rollback()
    {
        PilotFeature feature = PilotFeature.AuthenticationPilot;
        PilotGateway gateway = Gateway();
        PilotExecutionContext context = Context(feature);
        ActivationEvidencePackage evidence = Evidence(feature);

        PilotGatewayResult missingApproval = gateway.Evaluate(new(context, feature, evidence, null, ReadyRollback()));
        PilotGatewayResult missingRollback = gateway.Evaluate(new(context, feature, evidence,
            Approval(feature), new(RollbackReadinessStatus.Blocked, ["backup-unavailable"])));

        Assert.Equal(IntegrationControlDecision.Blocked, missingApproval.Decision);
        Assert.Contains("feature-approval-required", missingApproval.Reasons);
        Assert.Null(missingApproval.Permit);
        Assert.Equal(IntegrationControlDecision.Blocked, missingRollback.Decision);
        Assert.Contains("pilot-rollback-not-ready", missingRollback.Reasons);
        Assert.Null(missingRollback.Permit);
    }

    [Fact]
    public void Gateway_rejects_unknown_or_out_of_scope_feature()
    {
        PilotGateway gateway = Gateway();
        PilotExecutionContext context = Context(PilotFeature.AuthenticationPilot);
        PilotFeature unknown = (PilotFeature)999;

        PilotGatewayResult unknownResult = gateway.Evaluate(new(context, unknown,
            Evidence(PilotFeature.AuthenticationPilot), Approval(PilotFeature.AuthenticationPilot),
            ReadyRollback()));
        PilotGatewayResult outsideScope = gateway.Evaluate(new(context, PilotFeature.ReportingPilot,
            Evidence(PilotFeature.ReportingPilot), Approval(PilotFeature.ReportingPilot), ReadyRollback()));

        Assert.Equal(IntegrationControlDecision.Blocked, unknownResult.Decision);
        Assert.Contains("unknown-pilot-feature", unknownResult.Reasons);
        Assert.Equal(IntegrationControlDecision.Blocked, outsideScope.Decision);
        Assert.Contains("pilot-feature-outside-context-scope", outsideScope.Reasons);
    }

    [Theory]
    [InlineData(PilotFeature.AuthenticationPilot)]
    [InlineData(PilotFeature.ReportingPilot)]
    [InlineData(PilotFeature.RuntimeEventPilot)]
    [InlineData(PilotFeature.ProtectedSettingsPilot)]
    [InlineData(PilotFeature.ExportPilot)]
    public void Gateway_issues_read_only_permit_only_for_complete_bound_evidence(PilotFeature feature)
    {
        PilotExecutionContext context = Context(feature);

        PilotGatewayResult result = Gateway().Evaluate(new(context, feature, Evidence(feature),
            Approval(feature), ReadyRollback()));

        Assert.Equal(IntegrationControlDecision.Allowed, result.Decision);
        PilotExecutionPermit permit = Assert.IsType<PilotExecutionPermit>(result.Permit);
        Assert.Equal(context.PilotId, permit.PilotId);
        Assert.Equal(context.StationId, permit.StationId);
        Assert.Equal(feature, permit.Feature);
        Assert.True(permit.LegacyRemainsAuthoritative);
        Assert.True(permit.TargetReadOnly);
        Assert.False(permit.ProductionMutationAllowed);
        Assert.False(permit.EsdCutoverAllowed);
    }

    [Fact]
    public async Task Authentication_pilot_compares_safe_observations_without_replacing_legacy_session()
    {
        PilotExecutionContext context = Context(PilotFeature.AuthenticationPilot);
        PilotExecutionPermit permit = Permit(context, PilotFeature.AuthenticationPilot);
        var legacy = new AuthenticationLegacyObserver(new(true, "auth-accepted", "LegacyAccepted"));
        var target = new AuthenticationTargetObserver(new(true, ShiftId, StationId, 3,
            "auth-accepted", "TargetAccepted"));
        var service = new AuthenticationPilotService(new FixedClock(Now), legacy, target);

        PilotWorkflowResult<LegacyAuthenticationPilotObservation, ShiftProfileAuthenticationPilotObservation>
            result = await service.ObserveAsync(new(context, permit, ShiftId));

        Assert.Equal(IntegrationControlDecision.Allowed, result.Decision);
        Assert.True(result.LegacyRemainsAuthoritative);
        Assert.False(result.ProductionMutationAllowed);
        Assert.False(service.ReplacesLegacySession);
        Assert.False(service.RequiresSecondLoginScreen);
        Assert.Equal(3, result.TargetObservation!.CredentialVersion);
        Assert.Equal(PilotEvidenceState.Complete,
            PilotEvidenceValidator.Validate(result.Evidence, context));
        Assert.Equal(1, legacy.Calls);
        Assert.Equal(1, target.Calls);
    }

    [Fact]
    public void Authentication_pilot_contracts_expose_no_credential_input_or_secret_evidence()
    {
        string inputProperties = string.Join('|', typeof(AuthenticationPilotRequest).GetProperties()
            .Select(property => property.Name));
        string evidenceProperties = string.Join('|', typeof(PilotEvidenceRecord).GetProperties()
            .Select(property => property.Name));

        Assert.DoesNotContain("Password", inputProperties, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Secret", inputProperties, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Verifier", inputProperties, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", evidenceProperties, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Salt", evidenceProperties, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PrivateKey", evidenceProperties, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Secret", evidenceProperties, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reporting_pilot_requires_immutable_snapshot_and_blocks_recalculation_or_mutation()
    {
        PilotExecutionContext context = Context(PilotFeature.ReportingPilot);
        PilotExecutionPermit permit = Permit(context, PilotFeature.ReportingPilot);
        var legacy = new ReportLegacyObserver(new(true, "report-fp", "Readable"));
        var target = new ReportTargetObserver(new(true, "snapshot-1", "report-fp", true,
            false, false, "ReadOnly"));
        var export = new ReportExportValidator(new(true, "export-fp", false, "Valid"));
        var service = new ReportingPilotService(new FixedClock(Now), legacy, target, export);

        var allowed = await service.ObserveAsync(new(context, permit, "monthly", "snapshot-1"));
        target.Observation = target.Observation with { RecalculationAttempted = true };
        var blocked = await service.ObserveAsync(new(context, permit, "monthly", "snapshot-1"));

        Assert.Equal(IntegrationControlDecision.Allowed, allowed.Decision);
        Assert.True(allowed.LegacyRemainsAuthoritative);
        Assert.True(service.LegacyDisplayRemainsAvailable);
        Assert.Equal(IntegrationControlDecision.Blocked, blocked.Decision);
        Assert.Contains("snapshot-read-only-invariant-failed", blocked.Reasons);
    }

    [Fact]
    public async Task Runtime_event_pilot_blocks_every_write_cache_rebuild_and_recalculation_signal()
    {
        PilotExecutionContext context = Context(PilotFeature.RuntimeEventPilot);
        PilotExecutionPermit permit = Permit(context, PilotFeature.RuntimeEventPilot);
        var legacy = new RuntimeLegacyObserver(new("runtime-a", "event-a", "LegacyRead"));
        var target = new RuntimeTargetObserver(new("runtime-a", "event-a", true,
            false, false, false, false, false, "TargetRead"));
        var service = new RuntimeEventPilotService(new FixedClock(Now), legacy, target);

        var allowed = await service.ObserveAsync(new(context, permit, "month-1405-05"));
        target.Observation = target.Observation with { UpdateAttempted = true, CacheRebuildAttempted = true };
        var blocked = await service.ObserveAsync(new(context, permit, "month-1405-05"));

        Assert.Equal(IntegrationControlDecision.Allowed, allowed.Decision);
        Assert.True(allowed.LegacyRemainsAuthoritative);
        Assert.Equal(IntegrationControlDecision.Blocked, blocked.Decision);
        Assert.Contains("runtime-event-read-only-invariant-failed", blocked.Reasons);
    }

    [Fact]
    public async Task Protected_settings_pilot_blocks_mutation_provisioning_and_Esd_cutover_before_adapters_run()
    {
        PilotExecutionContext context = Context(PilotFeature.ProtectedSettingsPilot);
        PilotExecutionPermit permit = Permit(context, PilotFeature.ProtectedSettingsPilot);
        var legacy = new SettingsLegacyObserver(new(true, "legacy-esd", "Readable"));
        var target = new SettingsTargetObserver(new("target-decision", false, false, false,
            false, false, "Observed"));
        var service = new ProtectedSettingsPilotService(new FixedClock(Now), legacy, target);

        var result = await service.ObserveAsync(new(context, permit, "esd-adjustment",
            true, true, true));

        Assert.Equal(IntegrationControlDecision.Blocked, result.Decision);
        Assert.Contains("settings-mutation-prohibited", result.Reasons);
        Assert.Contains("settings-provisioning-prohibited", result.Reasons);
        Assert.Contains("esd-cutover-prohibited", result.Reasons);
        Assert.Equal(0, legacy.Calls);
        Assert.Equal(0, target.Calls);
    }

    [Fact]
    public async Task Protected_settings_observation_never_consumes_authorization_or_executes_management_credential()
    {
        PilotExecutionContext context = Context(PilotFeature.ProtectedSettingsPilot);
        var legacy = new SettingsLegacyObserver(new(true, "settings-a", "Readable"));
        var target = new SettingsTargetObserver(new("settings-a", false, false, false,
            false, false, "Observed"));
        var service = new ProtectedSettingsPilotService(new FixedClock(Now), legacy, target);

        var result = await service.ObserveAsync(new(context,
            Permit(context, PilotFeature.ProtectedSettingsPilot), "esd-adjustment", false, false, false));

        Assert.Equal(IntegrationControlDecision.Allowed, result.Decision);
        Assert.False(result.TargetObservation!.VendorAuthorizationConsumptionAttempted);
        Assert.False(result.TargetObservation.ManagementCredentialExecutionAttempted);
        Assert.False(result.ProductionMutationAllowed);
    }

    [Fact]
    public async Task Export_pilot_requires_immutable_read_only_target_artifact()
    {
        PilotExecutionContext context = Context(PilotFeature.ExportPilot);
        var legacy = new ExportLegacyObserver(new(true, "artifact-a", "Readable"));
        var target = new ExportTargetObserver(new(true, "artifact-a", true, true, false, "Valid"));
        var service = new ExportPilotService(new FixedClock(Now), legacy, target);

        var allowed = await service.ObserveAsync(new(context, Permit(context, PilotFeature.ExportPilot),
            "snapshot-1", "pdf"));
        target.Observation = target.Observation with { MutationAttempted = true };
        var blocked = await service.ObserveAsync(new(context, Permit(context, PilotFeature.ExportPilot),
            "snapshot-1", "pdf"));

        Assert.Equal(IntegrationControlDecision.Allowed, allowed.Decision);
        Assert.Equal(IntegrationControlDecision.Blocked, blocked.Decision);
        Assert.Contains("export-read-only-invariant-failed", blocked.Reasons);
    }

    [Fact]
    public async Task Expired_pilot_permit_blocks_workflow_before_any_observer_runs()
    {
        PilotExecutionContext context = Context(PilotFeature.AuthenticationPilot,
            expiresAtUtc: Now.AddMinutes(1));
        PilotExecutionPermit permit = Permit(context, PilotFeature.AuthenticationPilot);
        var legacy = new AuthenticationLegacyObserver(new(true, "a", "Accepted"));
        var target = new AuthenticationTargetObserver(new(true, ShiftId, StationId, 1, "a", "Accepted"));
        var service = new AuthenticationPilotService(new FixedClock(Now.AddMinutes(2)), legacy, target);

        var result = await service.ObserveAsync(new(context, permit, ShiftId));

        Assert.Equal(IntegrationControlDecision.Blocked, result.Decision);
        Assert.Contains("pilot-permit-expired", result.Reasons);
        Assert.Equal(0, legacy.Calls);
        Assert.Equal(0, target.Calls);
    }

    [Fact]
    public async Task Permit_cannot_be_reused_with_a_widened_ShiftProfile_scope()
    {
        PilotExecutionContext original = Context(PilotFeature.AuthenticationPilot);
        PilotExecutionPermit permit = Permit(original, PilotFeature.AuthenticationPilot);
        var widened = new PilotExecutionContext(PilotId, StationId, [ShiftId, "shift-profile-2"],
            [PilotFeature.AuthenticationPilot], EvidenceId, CorrelationId, "rollback-reference-1",
            original.CreatedAtUtc, original.ExpiresAtUtc);
        var legacy = new AuthenticationLegacyObserver(new(true, "a", "Accepted"));
        var target = new AuthenticationTargetObserver(new(true, "shift-profile-2", StationId, 1,
            "a", "Accepted"));
        var service = new AuthenticationPilotService(new FixedClock(Now), legacy, target);

        var result = await service.ObserveAsync(new(widened, permit, "shift-profile-2"));

        Assert.Equal(IntegrationControlDecision.Blocked, result.Decision);
        Assert.Contains("pilot-permit-scope-binding-mismatch", result.Reasons);
        Assert.Equal(0, legacy.Calls);
        Assert.Equal(0, target.Calls);
    }

    [Fact]
    public void Pilot_evidence_is_bound_to_context_and_preserves_safe_rollback_state()
    {
        PilotExecutionContext context = Context(PilotFeature.ReportingPilot);
        var evidence = new PilotEvidenceRecord("pilot-evidence-1", PilotId,
            PilotFeature.ReportingPilot, Now, CorrelationId, "legacy-fp", "target-fp",
            ShadowDifferenceSeverity.Warning, "Reports differ.", PilotRollbackStatus.Available);
        var wrongPilot = new PilotEvidenceRecord("pilot-evidence-1", "another-pilot",
            PilotFeature.ReportingPilot, Now, CorrelationId, "legacy-fp", "target-fp",
            ShadowDifferenceSeverity.Warning, "Reports differ.", PilotRollbackStatus.Available);

        Assert.Equal(PilotEvidenceState.Complete, PilotEvidenceValidator.Validate(evidence, context));
        Assert.Equal(PilotEvidenceState.Blocked, PilotEvidenceValidator.Validate(wrongPilot, context));
        Assert.False(evidence.ContainsCredentialMaterial);
    }

    [Fact]
    public void Rollback_coordinator_requires_disable_legacy_return_evidence_preservation_and_close()
    {
        PilotExecutionContext context = Context(PilotFeature.ReportingPilot);
        PilotRollbackPlan blocked = PilotRollbackCoordinator.Evaluate(new(context,
            true, true, false, true, Now));
        PilotRollbackPlan ready = PilotRollbackCoordinator.Evaluate(new(context,
            true, true, true, true, Now));

        Assert.Equal(IntegrationControlDecision.Blocked, blocked.Decision);
        Assert.Contains("pilot-evidence-preservation-required", blocked.Reasons);
        Assert.Equal(IntegrationControlDecision.Allowed, ready.Decision);
        Assert.True(ready.LegacyAuthorityRestored);
        Assert.True(ready.EvidencePreserved);
        Assert.False(ready.DestructiveActionAllowed);
        Assert.Equal(PilotRollbackStatus.Closed, ready.Status);
    }

    [Fact]
    public void Presentation_and_monitoring_are_UI_neutral_interfaces_without_providers()
    {
        Assembly assembly = typeof(PilotPresentationModel).Assembly;
        Type[] pilotTypes = assembly.GetTypes()
            .Where(type => type.Namespace == typeof(PilotPresentationModel).Namespace).ToArray();

        Assert.True(typeof(IPilotPresentationSink).IsInterface);
        Assert.True(typeof(IPilotMonitoringHook).IsInterface);
        Assert.DoesNotContain(pilotTypes, type => typeof(IPilotPresentationSink).IsAssignableFrom(type) &&
            !type.IsInterface);
        Assert.DoesNotContain(pilotTypes, type => typeof(IPilotMonitoringHook).IsAssignableFrom(type) &&
            !type.IsInterface);
        Assert.DoesNotContain(pilotTypes, type => type.Name.Contains("Form", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Pilot_foundation_has_no_database_writer_RBAC_or_support_identity_contract()
    {
        Type[] types = typeof(PilotGateway).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(PilotGateway).Namespace).ToArray();
        string names = string.Join('|', types.Select(type => type.Name));
        string methodNames = string.Join('|', types.SelectMany(type => type.GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Select(method => method.Name));

        Assert.DoesNotContain("SupportRole", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupportProfile", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupportLogin", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Rbac", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SqliteConnection", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InsertAsync", methodNames, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UpdateAsync", methodNames, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DeleteAsync", methodNames, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Migrate", methodNames, StringComparison.OrdinalIgnoreCase);
    }

    private static PilotGateway Gateway() => new(new FixedClock(Now), new PilotFeatureRegistry(),
        new FeatureIntegrationActivationCoordinator(new FixedClock(Now)));

    private static PilotExecutionContext Context(PilotFeature feature, DateTimeOffset? expiresAtUtc = null) =>
        new(PilotId, StationId, [ShiftId], [feature], EvidenceId, CorrelationId,
            "rollback-reference-1", Now.AddMinutes(-10), expiresAtUtc ?? Now.AddHours(1));

    private static PilotExecutionPermit Permit(PilotExecutionContext context, PilotFeature feature) =>
        Assert.IsType<PilotExecutionPermit>(Gateway().Evaluate(new(context, feature, Evidence(feature),
            Approval(feature), ReadyRollback())).Permit);

    private static RollbackReadinessResult ReadyRollback() =>
        new(RollbackReadinessStatus.Ready, Array.Empty<string>());

    private static FeatureIntegrationApproval Approval(PilotFeature feature)
    {
        PilotFeatureDefinition definition = Feature(feature);
        var approval = new ProductionActivationApproval("approval-82-1", "operator-boundary-1",
            Now.AddMinutes(-2), definition.RequiredApprovalScope, DatabaseIdentity,
            EvidenceId, CorrelationId, Now.AddMinutes(30));
        return new(approval, definition.IntegrationFeature, StationId);
    }

    private static ActivationEvidencePackage Evidence(PilotFeature feature)
    {
        PilotFeatureDefinition definition = Feature(feature);
        var preservation = new ActivationSnapshotPreservationEvidence(
            true, true, true, true, true, true, true);
        return new(EvidenceId, CorrelationId, DatabaseIdentity,
            new(DatabaseIdentity, true, true, true, true, true, Now.AddMinutes(-30)),
            new(MigrationHistoryClassification.CleanLegacyBaseline, 4, true, true),
            new("backup-receipt-82-1", DatabaseIdentity, "backup-fingerprint-82-1",
                true, true, 4096, Now.AddMinutes(-20)),
            new("rehearsal-receipt-82-1", true, true, true, 4,
                EsdReconciliationState.ReadyToProvision, EsdAuthorityMode.LegacyAuthoritative,
                preservation, Now.AddMinutes(-10)),
            new(true, true, true, true),
            new(true, definition.RequiredApprovalScope, DatabaseIdentity, EvidenceId),
            Now.AddMinutes(-5));
    }

    private static PilotFeatureDefinition Feature(PilotFeature feature)
    {
        Assert.True(new PilotFeatureRegistry().TryGet(feature, out PilotFeatureDefinition? definition));
        return Assert.IsType<PilotFeatureDefinition>(definition);
    }

    private sealed record FixedClock(DateTimeOffset UtcNow) : IClock
    {
        public DateTimeOffset LocalNow => UtcNow.ToOffset(TimeSpan.FromHours(3.5));
    }

    private sealed class AuthenticationLegacyObserver(LegacyAuthenticationPilotObservation observation)
        : ILegacyAuthenticationPilotObserver
    {
        public int Calls { get; private set; }
        public Task<LegacyAuthenticationPilotObservation> ObserveAuthoritativeAsync(
            AuthenticationPilotRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(observation);
        }
    }

    private sealed class AuthenticationTargetObserver(ShiftProfileAuthenticationPilotObservation observation)
        : IShiftProfileAuthenticationPilotObserver
    {
        public int Calls { get; private set; }
        public Task<ShiftProfileAuthenticationPilotObservation> ObserveReadOnlyAsync(
            AuthenticationPilotRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(observation);
        }
    }

    private sealed class ReportLegacyObserver(LegacyReportPilotObservation observation)
        : ILegacyReportPilotObserver
    {
        public Task<LegacyReportPilotObservation> ObserveAuthoritativeAsync(
            ReportingPilotRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(observation);
    }

    private sealed class ReportTargetObserver(TargetSnapshotPilotObservation observation)
        : ITargetSnapshotPilotObserver
    {
        public TargetSnapshotPilotObservation Observation { get; set; } = observation;
        public Task<TargetSnapshotPilotObservation> ObserveReadOnlyAsync(
            ReportingPilotRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Observation);
    }

    private sealed class ReportExportValidator(ExportArtifactPilotObservation observation)
        : IExportArtifactPilotValidator
    {
        public Task<ExportArtifactPilotObservation> ValidateReadOnlyAsync(
            ReportingPilotRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(observation);
    }

    private sealed class RuntimeLegacyObserver(LegacyRuntimeEventPilotObservation observation)
        : ILegacyRuntimeEventPilotObserver
    {
        public Task<LegacyRuntimeEventPilotObservation> ObserveAuthoritativeAsync(
            RuntimeEventPilotRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(observation);
    }

    private sealed class RuntimeTargetObserver(TargetRuntimeEventPilotObservation observation)
        : ITargetRuntimeEventPilotObserver
    {
        public TargetRuntimeEventPilotObservation Observation { get; set; } = observation;
        public Task<TargetRuntimeEventPilotObservation> ObserveReadOnlyAsync(
            RuntimeEventPilotRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Observation);
    }

    private sealed class SettingsLegacyObserver(LegacySettingsPilotObservation observation)
        : ILegacySettingsPilotObserver
    {
        public int Calls { get; private set; }
        public Task<LegacySettingsPilotObservation> ObserveAuthoritativeAsync(
            ProtectedSettingsPilotRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(observation);
        }
    }

    private sealed class SettingsTargetObserver(TargetProtectedSettingsPilotObservation observation)
        : ITargetProtectedSettingsPilotObserver
    {
        public int Calls { get; private set; }
        public Task<TargetProtectedSettingsPilotObservation> EvaluateDecisionReadOnlyAsync(
            ProtectedSettingsPilotRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(observation);
        }
    }

    private sealed class ExportLegacyObserver(LegacyExportPilotObservation observation)
        : ILegacyExportPilotObserver
    {
        public Task<LegacyExportPilotObservation> ObserveAuthoritativeAsync(
            ExportPilotRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(observation);
    }

    private sealed class ExportTargetObserver(TargetExportPilotObservation observation)
        : ITargetExportPilotObserver
    {
        public TargetExportPilotObservation Observation { get; set; } = observation;
        public Task<TargetExportPilotObservation> ValidateReadOnlyAsync(
            ExportPilotRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Observation);
    }
}
