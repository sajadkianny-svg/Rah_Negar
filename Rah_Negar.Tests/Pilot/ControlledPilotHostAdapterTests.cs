using System.Reflection;
using Rah_Negar.Core;
using Rah_Negar.Core.Event;
using Rah_Negar.Core.Event.Comparison;
using Rah_Negar.Core.Reporting.Snapshot;
using Rah_Negar.Foundation.Application.Activation;
using Rah_Negar.Foundation.Application.Database.Readiness;
using Rah_Negar.Foundation.Application.Integration;
using Rah_Negar.Foundation.Application.Pilot;
using Rah_Negar.Foundation.Application.Pilot.Hosting;
using Rah_Negar.Foundation.Application.Reporting.Export;
using Rah_Negar.Foundation.Application.Reporting.Finalization;
using Rah_Negar.Foundation.Application.Reporting.Finalized;
using Rah_Negar.Foundation.Application.Runtime.Shadow;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Foundation.Application.UI.Settings;
using Rah_Negar.Foundation.Time;
using Rah_Negar.Tests.Reporting.Synthetic;

namespace Rah_Negar.Tests.Pilot;

public sealed class ControlledPilotHostAdapterTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 18, 0, 0, TimeSpan.Zero);
    private const string PilotId = "pilot-host-1";
    private const string StationId = "rasht";
    private const string ShiftId = "shift-profile-1";
    private const string EvidenceId = "pilot-host-evidence-1";
    private const string CorrelationId = "pilot-host-correlation-1";
    private const string DatabaseIdentity = "pilot-host-database-1";

    [Fact]
    public void Host_is_explicit_inactive_and_has_no_hidden_selection_or_activation()
    {
        var host = new PilotExecutionCoordinator(new FixedClock(Now), Array.Empty<IPilotWorkflowExecutor>());

        Assert.False(host.AutomaticallyRuns);
        Assert.False(host.RegisteredInProductionStartup);
        Assert.False(host.SelectsDatabase);
        Assert.False(host.ActivatesFeatures);
    }

    [Fact]
    public async Task Host_rejects_missing_or_wrong_feature_permit_before_workflow_routing()
    {
        var executor = new RecordingExecutor(PilotFeature.AuthenticationPilot,
            typeof(AuthenticationPilotInput));
        var host = new PilotExecutionCoordinator(new FixedClock(Now), [executor]);
        PilotExecutionContext context = Context(PilotFeature.AuthenticationPilot);
        PilotExecutionPermit reportingPermit = Permit(Context(PilotFeature.ReportingPilot),
            PilotFeature.ReportingPilot);

        PilotExecutionResult missing = await host.ExecuteAsync(new(context, null,
            PilotFeature.AuthenticationPilot, new AuthenticationPilotInput(ShiftId)));
        PilotExecutionResult wrong = await host.ExecuteAsync(new(context, reportingPermit,
            PilotFeature.AuthenticationPilot, new AuthenticationPilotInput(ShiftId)));

        Assert.Equal(PilotExecutionStatus.Blocked, missing.Status);
        Assert.Contains("pilot-permit-required", missing.BlockedReasons);
        Assert.Equal(PilotExecutionStatus.Blocked, wrong.Status);
        Assert.Contains("pilot-permit-feature-mismatch", wrong.BlockedReasons);
        Assert.Equal(0, executor.Calls);
    }

    [Fact]
    public async Task Host_routes_only_exact_feature_and_input_type()
    {
        var executor = new RecordingExecutor(PilotFeature.AuthenticationPilot,
            typeof(AuthenticationPilotInput));
        var host = new PilotExecutionCoordinator(new FixedClock(Now), [executor]);
        PilotExecutionContext context = Context(PilotFeature.AuthenticationPilot);
        PilotExecutionPermit permit = Permit(context, PilotFeature.AuthenticationPilot);

        PilotExecutionResult wrongInput = await host.ExecuteAsync(new(context, permit,
            PilotFeature.AuthenticationPilot, new ReportingPilotInput("monthly", "snapshot-1")));
        PilotExecutionResult valid = await host.ExecuteAsync(new(context, permit,
            PilotFeature.AuthenticationPilot, new AuthenticationPilotInput(ShiftId)));

        Assert.Equal(PilotExecutionStatus.Blocked, wrongInput.Status);
        Assert.Contains("pilot-workflow-input-type-mismatch", wrongInput.BlockedReasons);
        Assert.Equal(PilotExecutionStatus.Completed, valid.Status);
        Assert.Equal(1, executor.Calls);
        Assert.True(valid.LegacyAuthorityPreserved);
        Assert.False(valid.ProductionMutationAllowed);
        Assert.False(valid.AuthoritySwitchPerformed);
    }

    [Fact]
    public async Task Authentication_host_uses_legacy_and_ShiftProfile_read_models_without_session_creation()
    {
        PilotExecutionContext context = Context(PilotFeature.AuthenticationPilot);
        var legacySource = new FixedAuthenticationStateReader(true);
        var targetSource = new FixedShiftProfileAuthenticationReadModel(new(true, ShiftId,
            StationId, 4, "TargetAccepted", "target-auth-v4"));
        var legacy = new LegacyAuthenticationObservationAdapter(legacySource);
        var target = new ShiftProfileAuthenticationObservationAdapter(targetSource, "target-auth-v4");
        var executor = new AuthenticationPilotWorkflowExecutor(new FixedClock(Now), legacy, target);
        var host = new PilotExecutionCoordinator(new FixedClock(Now), [executor]);

        PilotExecutionResult result = await host.ExecuteAsync(new(context,
            Permit(context, PilotFeature.AuthenticationPilot), PilotFeature.AuthenticationPilot,
            new AuthenticationPilotInput(ShiftId)));

        Assert.Equal(PilotExecutionStatus.Completed, result.Status);
        Assert.NotNull(result.LegacyResult);
        Assert.NotNull(result.TargetResult);
        Assert.Equal("legacy-authentication-observer", result.LegacyResult!.Metadata.AdapterId);
        Assert.Equal("target-shift-profile-authentication-observer", result.TargetResult!.Metadata.AdapterId);
        Assert.True(result.LegacyResult.Metadata.PreservesLegacyAuthority);
        Assert.True(result.TargetResult.Metadata.ReadOnly);
        Assert.Equal(1, targetSource.Calls);
        Assert.True(result.LegacyAuthorityPreserved);
    }

    [Fact]
    public void AppSession_reader_observes_current_state_without_exposing_a_session_mutator()
    {
        var reader = new AppSessionAuthenticationStateReader();

        Assert.Equal(AppSession.IsLoggedIn, reader.IsAuthenticated);
        Assert.Equal("legacy-app-session-v1", reader.SourceVersion);
        Assert.DoesNotContain(reader.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance |
            BindingFlags.DeclaredOnly), method => method.Name is "Login" or "Logout");
    }

    [Fact]
    public async Task Target_authentication_failure_is_isolated_and_retains_legacy_observation()
    {
        PilotExecutionContext context = Context(PilotFeature.AuthenticationPilot);
        var legacy = new LegacyAuthenticationObservationAdapter(new FixedAuthenticationStateReader(true));
        var target = new ShiftProfileAuthenticationObservationAdapter(
            new ThrowingShiftProfileAuthenticationReadModel(), "target-auth-v4");
        var host = new PilotExecutionCoordinator(new FixedClock(Now),
            [new AuthenticationPilotWorkflowExecutor(new FixedClock(Now), legacy, target)]);

        PilotExecutionResult result = await host.ExecuteAsync(new(context,
            Permit(context, PilotFeature.AuthenticationPilot), PilotFeature.AuthenticationPilot,
            new AuthenticationPilotInput(ShiftId)));

        Assert.Equal(PilotExecutionStatus.TargetFailed, result.Status);
        Assert.NotNull(result.LegacyResult);
        Assert.Null(result.TargetResult);
        Assert.Equal("Target observation failed; legacy authority was preserved.",
            result.Comparison.SafeSummary);
        Assert.DoesNotContain("test-only", string.Join('|', result.BlockedReasons),
            StringComparison.OrdinalIgnoreCase);
        Assert.True(result.LegacyAuthorityPreserved);
    }

    [Fact]
    public async Task Reporting_host_reads_finalized_snapshot_compares_sections_and_validates_export()
    {
        FinalizedReportSnapshot snapshot = await SnapshotAsync();
        var reader = new StubFinalizedReader(FinalizedReportReadResult.Found(snapshot));
        var target = new SnapshotReportObservationAdapter(reader,
            new ReportExportValidator(["snapshot-format-v1"], ["snapshot-integrity-v1"]),
            "snapshot-read-v1");
        var legacy = new LegacyReportObservationAdapter(new FixedLegacyReportReadModel(
            new(true, new Dictionary<string, string> { ["legacy"] = "different" },
                "legacy-report-v1", "LegacyReportReadable")), "legacy-report-v1");
        PilotExecutionContext context = Context(PilotFeature.ReportingPilot);
        var host = new PilotExecutionCoordinator(new FixedClock(Now),
            [new ReportingPilotWorkflowExecutor(new FixedClock(Now), legacy, target, target)]);

        PilotExecutionResult result = await host.ExecuteAsync(new(context,
            Permit(context, PilotFeature.ReportingPilot), PilotFeature.ReportingPilot,
            new ReportingPilotInput("monthly", snapshot.Identity.SnapshotId)));

        Assert.Equal(PilotExecutionStatus.CompletedWithDifference, result.Status);
        Assert.NotNull(result.EvidenceId);
        Assert.Equal("target-finalized-snapshot-observer", result.TargetResult!.Metadata.AdapterId);
        Assert.Equal("FinalizedSnapshotObserved", result.TargetResult.SafeStatus);
        Assert.Equal(2, reader.ByIdReadCount);
        Assert.True(result.TargetResult.Metadata.ReadOnly);
    }

    [Fact]
    public async Task Reporting_adapter_rejects_missing_or_nonfinalized_snapshot_without_mutation()
    {
        var reader = new StubFinalizedReader(FinalizedReportReadResult.Failure(
            FinalizedReportReadStatus.NotFinalized, "not-finalized", "not-finalized"));
        var target = new SnapshotReportObservationAdapter(reader,
            new ReportExportValidator(["snapshot-format-v1"], ["snapshot-integrity-v1"]),
            "snapshot-read-v1");
        var legacy = new LegacyReportObservationAdapter(new FixedLegacyReportReadModel(
            new(true, new Dictionary<string, string> { ["legacy"] = "a" },
                "legacy-report-v1", "LegacyReportReadable")), "legacy-report-v1");
        PilotExecutionContext context = Context(PilotFeature.ReportingPilot);
        var host = new PilotExecutionCoordinator(new FixedClock(Now),
            [new ReportingPilotWorkflowExecutor(new FixedClock(Now), legacy, target, target)]);

        PilotExecutionResult result = await host.ExecuteAsync(new(context,
            Permit(context, PilotFeature.ReportingPilot), PilotFeature.ReportingPilot,
            new ReportingPilotInput("monthly", "missing-snapshot")));

        Assert.Equal(PilotExecutionStatus.Blocked, result.Status);
        Assert.Contains("snapshot-read-only-invariant-failed", result.BlockedReasons);
        Assert.True(result.LegacyAuthorityPreserved);
        Assert.Equal(2, reader.ByIdReadCount);
    }

    [Fact]
    public async Task Runtime_event_host_consumes_Phase4_results_as_read_only_evidence()
    {
        RuntimeEventTargetReadModelResult targetModel = RuntimeTargetResult();
        var legacy = new LegacyRuntimeEventObservationAdapter(new FixedLegacyRuntimeEventReadModel(
            new("legacy-runtime", "legacy-event", "legacy-v1", "LegacyRuntimeEventReadable")),
            "legacy-v1");
        var targetSource = new FixedRuntimeEventTargetReadModel(targetModel);
        var target = new TargetRuntimeEventObservationAdapter(targetSource, "runtime-shadow-v1");
        PilotExecutionContext context = Context(PilotFeature.RuntimeEventPilot);
        var host = new PilotExecutionCoordinator(new FixedClock(Now),
            [new RuntimeEventPilotWorkflowExecutor(new FixedClock(Now), legacy, target)]);

        PilotExecutionResult result = await host.ExecuteAsync(new(context,
            Permit(context, PilotFeature.RuntimeEventPilot), PilotFeature.RuntimeEventPilot,
            new RuntimeEventPilotInput("unit-1:100-200")));

        Assert.True(result.Status == PilotExecutionStatus.CompletedWithDifference,
            string.Join(',', result.BlockedReasons));
        Assert.Equal("target-runtime-event-observer", result.TargetResult!.Metadata.AdapterId);
        Assert.True(result.TargetResult.Metadata.ReadOnly);
        Assert.Equal(1, targetSource.Calls);
        Assert.True(result.LegacyAuthorityPreserved);
    }

    [Fact]
    public async Task Protected_settings_host_wraps_existing_reader_and_pure_target_policy()
    {
        var settingsReader = new FixedProtectedSettingsReader(new(StationId, 1.5m, true,
            new Dictionary<string, string> { ["theme"] = "dark" }));
        var legacy = new LegacyProtectedSettingsObservationAdapter(settingsReader, "legacy-settings-v1");
        var target = new TargetProtectedSettingsDecisionAdapter("settings-policy-v1");
        PilotExecutionContext context = Context(PilotFeature.ProtectedSettingsPilot);
        var host = new PilotExecutionCoordinator(new FixedClock(Now),
            [new ProtectedSettingsPilotWorkflowExecutor(new FixedClock(Now), legacy, target)]);

        PilotExecutionResult result = await host.ExecuteAsync(new(context,
            Permit(context, PilotFeature.ProtectedSettingsPilot), PilotFeature.ProtectedSettingsPilot,
            new ProtectedSettingsPilotInput("esd-view")));

        Assert.True(result.Status is PilotExecutionStatus.Completed or
            PilotExecutionStatus.CompletedWithDifference);
        Assert.Equal(1, settingsReader.Calls);
        Assert.True(result.LegacyAuthorityPreserved);
        Assert.False(result.ProductionMutationAllowed);
    }

    [Fact]
    public async Task Protected_settings_mutation_and_Esd_cutover_are_blocked_before_readers_run()
    {
        var settingsReader = new FixedProtectedSettingsReader(new(StationId, 1.5m, true,
            new Dictionary<string, string>()));
        var legacy = new LegacyProtectedSettingsObservationAdapter(settingsReader, "legacy-settings-v1");
        var target = new TargetProtectedSettingsDecisionAdapter("settings-policy-v1");
        PilotExecutionContext context = Context(PilotFeature.ProtectedSettingsPilot);
        var host = new PilotExecutionCoordinator(new FixedClock(Now),
            [new ProtectedSettingsPilotWorkflowExecutor(new FixedClock(Now), legacy, target)]);

        PilotExecutionResult result = await host.ExecuteAsync(new(context,
            Permit(context, PilotFeature.ProtectedSettingsPilot), PilotFeature.ProtectedSettingsPilot,
            new ProtectedSettingsPilotInput("esd-change", true, true, true)));

        Assert.Equal(PilotExecutionStatus.Blocked, result.Status);
        Assert.Contains("settings-mutation-prohibited", result.BlockedReasons);
        Assert.Contains("settings-provisioning-prohibited", result.BlockedReasons);
        Assert.Contains("esd-cutover-prohibited", result.BlockedReasons);
        Assert.Equal(0, settingsReader.Calls);
        Assert.True(result.LegacyAuthorityPreserved);
    }

    [Fact]
    public async Task Export_host_validates_snapshot_without_rendering_or_writing_artifact()
    {
        FinalizedReportSnapshot snapshot = await SnapshotAsync();
        var reader = new StubFinalizedReader(FinalizedReportReadResult.Found(snapshot));
        var legacy = new LegacyExportObservationAdapter(new FixedLegacyExportReadModel(
            new(true, "legacy-artifact-fingerprint", "legacy-export-v1", "LegacyExportReadable")),
            "legacy-export-v1");
        var target = new SnapshotExportObservationAdapter(reader,
            new ReportExportValidator(["snapshot-format-v1"], ["snapshot-integrity-v1"]),
            "snapshot-export-v1");
        PilotExecutionContext context = Context(PilotFeature.ExportPilot);
        var host = new PilotExecutionCoordinator(new FixedClock(Now),
            [new ExportPilotWorkflowExecutor(new FixedClock(Now), legacy, target)]);

        PilotExecutionResult result = await host.ExecuteAsync(new(context,
            Permit(context, PilotFeature.ExportPilot), PilotFeature.ExportPilot,
            new ExportPilotInput(snapshot.Identity.SnapshotId, "pdf")));

        Assert.Equal(PilotExecutionStatus.CompletedWithDifference, result.Status);
        Assert.Equal("TargetExportValidated", result.TargetResult!.SafeStatus);
        Assert.True(result.TargetResult.Metadata.ReadOnly);
        Assert.Equal(1, reader.ByIdReadCount);
        Assert.False(result.ProductionMutationAllowed);
    }

    [Fact]
    public void All_concrete_adapters_publish_read_only_legacy_authority_metadata()
    {
        IPilotAdapterDescriptorProvider[] adapters =
        [
            new LegacyAuthenticationObservationAdapter(new FixedAuthenticationStateReader(false)),
            new ShiftProfileAuthenticationObservationAdapter(
                new FixedShiftProfileAuthenticationReadModel(new(false, ShiftId, StationId, 1,
                    "Rejected", "target-v1")), "target-v1"),
            new LegacyReportObservationAdapter(new FixedLegacyReportReadModel(
                new(false, new Dictionary<string, string>(), "legacy-v1", "Unavailable")), "legacy-v1"),
            new LegacyRuntimeEventObservationAdapter(new FixedLegacyRuntimeEventReadModel(
                new("r", "e", "legacy-v1", "Observed")), "legacy-v1"),
            new LegacyProtectedSettingsObservationAdapter(new FixedProtectedSettingsReader(
                new(StationId, 0m, false, new Dictionary<string, string>())), "legacy-v1"),
            new TargetProtectedSettingsDecisionAdapter("target-v1"),
            new LegacyExportObservationAdapter(new FixedLegacyExportReadModel(
                new(false, "artifact", "legacy-v1", "Unavailable")), "legacy-v1")
        ];

        Assert.All(adapters, adapter =>
        {
            Assert.True(adapter.Descriptor.ReadOnly);
            Assert.True(adapter.Descriptor.PreservesLegacyAuthority);
            Assert.False(string.IsNullOrWhiteSpace(adapter.Descriptor.AdapterId));
            Assert.False(string.IsNullOrWhiteSpace(adapter.Descriptor.AdapterVersion));
            Assert.False(string.IsNullOrWhiteSpace(adapter.Descriptor.SourceVersion));
        });
    }

    [Fact]
    public void Pilot_result_and_presenter_contracts_exclude_secrets_raw_rows_and_UI_implementation()
    {
        string properties = string.Join('|', typeof(PilotExecutionResult).GetProperties()
            .Concat(typeof(PilotObservationResult).GetProperties()).Select(property => property.Name));
        Type[] hostTypes = typeof(PilotExecutionCoordinator).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(PilotExecutionCoordinator).Namespace).ToArray();

        Assert.DoesNotContain("Password", properties, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Salt", properties, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PrivateKey", properties, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Credential", properties, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RawRow", properties, StringComparison.OrdinalIgnoreCase);
        Assert.True(typeof(IPilotHostPresenter).IsInterface);
        Assert.DoesNotContain(hostTypes, type => typeof(IPilotHostPresenter).IsAssignableFrom(type) &&
            !type.IsInterface);
        Assert.DoesNotContain(hostTypes, type => type.Name.Contains("Form", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Host_namespace_has_no_database_writer_migration_RBAC_or_support_identity()
    {
        Type[] types = typeof(PilotExecutionCoordinator).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(PilotExecutionCoordinator).Namespace).ToArray();
        string names = string.Join('|', types.Select(type => type.Name));
        string methods = string.Join('|', types.SelectMany(type => type.GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Select(method => method.Name));

        Assert.DoesNotContain("SqliteConnection", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DatabaseLocator", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InsertAsync", methods, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UpdateAsync", methods, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DeleteAsync", methods, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Migrate", methods, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Rbac", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupportRole", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupportProfile", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupportLogin", names, StringComparison.OrdinalIgnoreCase);
    }

    private static PilotExecutionContext Context(PilotFeature feature) => new(PilotId, StationId,
        [ShiftId], [feature], EvidenceId, CorrelationId, "rollback-reference-host-1",
        Now.AddMinutes(-10), Now.AddHours(1));

    private static PilotExecutionPermit Permit(PilotExecutionContext context, PilotFeature feature)
    {
        var gateway = new PilotGateway(new FixedClock(Now), new PilotFeatureRegistry(),
            new FeatureIntegrationActivationCoordinator(new FixedClock(Now)));
        PilotGatewayResult result = gateway.Evaluate(new(context, feature, Evidence(feature),
            Approval(feature), new(RollbackReadinessStatus.Ready, Array.Empty<string>())));
        return Assert.IsType<PilotExecutionPermit>(result.Permit);
    }

    private static FeatureIntegrationApproval Approval(PilotFeature feature)
    {
        PilotFeatureDefinition definition = Feature(feature);
        var approval = new ProductionActivationApproval("pilot-host-approval-1", "operator-boundary-1",
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
            new("backup-receipt-host-1", DatabaseIdentity, "backup-fingerprint-host-1",
                true, true, 4096, Now.AddMinutes(-20)),
            new("rehearsal-receipt-host-1", true, true, true, 4,
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

    private static RuntimeEventTargetReadModelResult RuntimeTargetResult()
    {
        var runtime = new RuntimeShadowExecutionResult(StationId, "unit-1", 100, 200,
            RuntimeShadowExecutionStatus.Match, null, null, null,
            new("runtime-execution-1", "isolated-copy-1", "source-fingerprint", Now.AddHours(-1),
                "event-v1", "baseline-v1", "policy-v1", "calculation-v1", Now), null, null);
        var events = new EventComparisonResult(DifferenceCategory.Equivalent, Array.Empty<string>(),
            2, 2, true, true, EventOperationalState.Stopped, EventOperationalState.Stopped);
        return new(runtime, events, "runtime-event-target-v1");
    }

    private static async Task<FinalizedReportSnapshot> SnapshotAsync()
    {
        SyntheticPipelineResult pipeline = await new SyntheticReportingFixture().RunAsync(
            SyntheticReportingScenario.Complete);
        var request = new ReportFinalizationRequest("finalization-pilot-host-1", "snapshot-pilot-host-1",
            pipeline.Projection!, StationId, 10_000, 53_200, ["unit-2", "unit-1"],
            "synthetic-read-revision-v1", "synthetic-read-revision-v1", 1, null,
            "test-actor", Now.AddMinutes(-20), "finalization-policy-v1", "snapshot-integrity-v1");
        FinalizedReportSnapshot pending = new ReportSnapshotFactory().Create(request,
            new ReportFinalizationValidator().Validate(request)).Snapshot!;
        return new(pending.Identity, pending.ReportIdentity, pending.Completeness, pending.Evidence,
            pending.Versions, new SnapshotChecksum("SHA-256", "snapshot-integrity-v1",
                SnapshotChecksumState.Calculated, new string('a', 64), 1024),
            pending.OperationalSummaries, pending.DailySummaries, pending.RuntimeSummaries,
            pending.EventSummaries, pending.EventLog, pending.ServiceSummaries,
            pending.ExtremeDateSummaries, pending.Warnings);
    }

    private sealed record FixedClock(DateTimeOffset UtcNow) : IClock
    {
        public DateTimeOffset LocalNow => UtcNow.ToOffset(TimeSpan.FromHours(3.5));
    }

    private sealed class RecordingExecutor(PilotFeature feature, Type inputType) : IPilotWorkflowExecutor
    {
        public PilotFeature Feature => feature;
        public Type InputType => inputType;
        public int Calls { get; private set; }
        public Task<PilotWorkflowAdapterExecution> ExecuteAsync(PilotHostRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            var descriptor = new PilotAdapterEvidenceMetadata("recording", "v1", "source-v1",
                Now, true, true);
            var observation = new PilotObservationResult(new string('a', 64), "Observed", descriptor);
            var evidence = new PilotEvidenceRecord("recording-evidence", request.Context.PilotId,
                request.Feature, Now, request.Context.CorrelationId, "same", "same",
                ShadowDifferenceSeverity.None, "Observations match.", PilotRollbackStatus.Available);
            return Task.FromResult(new PilotWorkflowAdapterExecution(IntegrationControlDecision.Allowed,
                observation, observation, evidence, Array.Empty<string>(), false));
        }
    }

    private sealed record FixedAuthenticationStateReader(bool IsAuthenticated) : ILegacyAuthenticationStateReader
    {
        public string SourceVersion => "legacy-session-v1";
    }

    private sealed class FixedShiftProfileAuthenticationReadModel(ShiftProfileAuthenticationReadModelResult result)
        : IShiftProfileAuthenticationReadModel
    {
        public int Calls { get; private set; }
        public Task<ShiftProfileAuthenticationReadModelResult> ObserveAsync(string stationId,
            string shiftProfileId, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private sealed class ThrowingShiftProfileAuthenticationReadModel : IShiftProfileAuthenticationReadModel
    {
        public Task<ShiftProfileAuthenticationReadModelResult> ObserveAsync(string stationId,
            string shiftProfileId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("test-only target failure detail");
    }

    private sealed class FixedLegacyReportReadModel(LegacyReportReadModelResult result) : ILegacyReportReadModel
    {
        public Task<LegacyReportReadModelResult> ReadAsync(string stationId, string reportScope,
            CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private sealed class StubFinalizedReader(FinalizedReportReadResult result) : IFinalizedReportReader
    {
        public int ByIdReadCount { get; private set; }
        public Task<FinalizedReportReadResult> GetBySnapshotIdAsync(string snapshotId,
            CancellationToken cancellationToken = default)
        {
            ByIdReadCount++;
            return Task.FromResult(result);
        }
        public Task<FinalizedReportReadResult> GetEffectiveAsync(FinalizedReportQuery query,
            CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private sealed class FixedLegacyRuntimeEventReadModel(LegacyRuntimeEventReadModelResult result)
        : ILegacyRuntimeEventReadModel
    {
        public Task<LegacyRuntimeEventReadModelResult> ReadAsync(string stationId,
            string projectionScope, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private sealed class FixedRuntimeEventTargetReadModel(RuntimeEventTargetReadModelResult result)
        : IRuntimeEventTargetReadModel
    {
        public int Calls { get; private set; }
        public Task<RuntimeEventTargetReadModelResult> ObserveAsync(string stationId,
            string projectionScope, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private sealed class FixedProtectedSettingsReader(ProtectedSettingsSnapshot result)
        : IProtectedSettingsReader
    {
        public int Calls { get; private set; }
        public Task<ProtectedSettingsSnapshot> ReadAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private sealed class FixedLegacyExportReadModel(LegacyExportReadModelResult result)
        : ILegacyExportReadModel
    {
        public Task<LegacyExportReadModelResult> ReadAsync(string stationId, string snapshotId,
            string exportFormat, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }
}
