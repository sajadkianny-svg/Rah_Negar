using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using Rah_Negar.Foundation.Application.Activation.Preparation;
using Rah_Negar.Foundation.Application.Pilot.Operational;
using Rah_Negar.Foundation.Application.Pilot.Validation;

namespace Rah_Negar.Tests.Pilot;

public sealed class ControlledPilotOperationalReadinessTests
{
    [Theory]
    [InlineData("rasht")]
    [InlineData("ramsar")]
    public void Context_is_explicit_bounded_immutable_and_defensively_copied(string fixtureName)
    {
        ControlledPilotOperationalFixture fixture = Fixture(fixtureName);
        var workflows = new List<PilotValidationWorkflow>
        {
            PilotValidationWorkflow.Reporting,
            PilotValidationWorkflow.Authentication,
            PilotValidationWorkflow.Reporting
        };

        ControlledPilotOperationalRehearsalContext context = fixture.Context(workflows);
        workflows.Add(PilotValidationWorkflow.Export);

        Assert.Equal([PilotValidationWorkflow.Authentication,
            PilotValidationWorkflow.Reporting], context.SelectedWorkflows);
        Assert.True(context.ExplicitApproval);
        Assert.False(context.UsesAmbientEnvironment);
        Assert.False(context.AutomaticallyActivates);
        Assert.False(context.ChangesProductionAuthority);
        Assert.Throws<ArgumentOutOfRangeException>(() => new
            ControlledPilotOperationalRehearsalContext("rehearsal", "pilot", "session",
                "correlation", "release", fixture.Scope,
                ControlledPilotOperationalFixture.WindowStart,
                ControlledPilotOperationalFixture.WindowStart.AddHours(9), workflows,
                "operator", "preparation-evidence", "rollback-evidence", true));
    }

    [Theory]
    [InlineData("rasht", 3)]
    [InlineData("ramsar", 4)]
    public void Representative_fixture_uses_expected_station_unit_count_and_runtime_rules(
        string fixtureName, int expectedUnits)
    {
        ControlledPilotOperationalFixture fixture = Fixture(fixtureName);

        Assert.Equal(expectedUnits, fixture.UnitCount);
        Assert.Equal(expectedUnits, fixture.RuntimeObservation.Units.Count);
        RuntimeUnitOperationalObservation crossDay = fixture.RuntimeObservation.Units[0];
        Assert.Equal(120, crossDay.PhysicalRuntimeMinutes);
        Assert.Equal(2, crossDay.ServiceDayCount);
        Assert.Equal(120, crossDay.LongestRunMinutes);

        RuntimeUnitOperationalObservation? esd = fixture.RuntimeObservation.Units.FirstOrDefault(
            unit => unit.AuthoritativeEvents.Any(item => item.EventType ==
                Rah_Negar.Core.Event.EventType.Esd));
        Assert.NotNull(esd);
        Assert.Equal(90, esd!.EsdAdjustmentMinutes);
        Assert.True(esd.AdjustedRuntimeMinutes > esd.PhysicalRuntimeMinutes);
        Assert.True(esd.LongestRunMinutes <= esd.PhysicalRuntimeMinutes);
        Assert.False(fixture.RuntimeObservation.MutatesEvents);
        Assert.False(fixture.RuntimeObservation.AppliesEsdMutation);

        if (fixtureName == "ramsar")
        {
            RuntimeUnitOperationalObservation midnight = fixture.RuntimeObservation.Units[3];
            Assert.Equal(40, midnight.PhysicalRuntimeMinutes);
            Assert.Equal(1, midnight.ServiceDayCount);
            Assert.Contains(midnight.AuthoritativeEvents, item => item.EventMinute == 1_440);
        }
        else
        {
            Assert.Contains(fixture.RuntimeObservation.Units,
                unit => unit.AuthoritativeEvents.Count == 0);
            Assert.Contains(fixture.RuntimeObservation.Units.SelectMany(unit =>
                unit.AuthoritativeEvents), item => item.EventType ==
                    Rah_Negar.Core.Event.EventType.Oh);
        }
    }

    [Theory]
    [InlineData("rasht")]
    [InlineData("ramsar")]
    public void Operational_preflight_is_ready_only_with_complete_concrete_evidence(
        string fixtureName)
    {
        ControlledPilotOperationalFixture fixture = Fixture(fixtureName);
        using ControlledPilotOperationalRehearsalCoordinator coordinator = fixture.Coordinator();

        ControlledPilotOperationalOperationResult result = coordinator.RunPreflight(
            ControlledPilotOperationalFixture.WindowStart);

        Assert.Equal(ControlledPilotOperationalOperationStatus.Accepted, result.Status);
        Assert.Equal(ControlledPilotOperationalPreflightStatus.Ready,
            result.Preflight!.Status);
        Assert.Equal(ControlledPilotOperationalLifecycle.PreflightPassed,
            coordinator.Lifecycle);
        Assert.False(result.Preflight.ExecutedProductionMutation);
        Assert.False(result.Preflight.AccessedProductionDatabase);
    }

    [Fact]
    public void Preflight_blocks_wrong_branch_missing_workflow_and_unavailable_destination()
    {
        ControlledPilotOperationalFixture fixture = ControlledPilotOperationalFixture.Rasht();
        using ControlledPilotOperationalRehearsalCoordinator wrongBranch = fixture.Coordinator(
            release: fixture.Release(branch: "different-branch"));
        using ControlledPilotOperationalRehearsalCoordinator missingWorkflow = fixture.Coordinator(
            observers: fixture.Observers().Skip(1));
        using ControlledPilotOperationalRehearsalCoordinator noDestination = fixture.Coordinator(
            destination: new UnavailableDestination());

        Assert.Equal("operational-preflight-blocked", wrongBranch.RunPreflight(
            ControlledPilotOperationalFixture.WindowStart).ReasonCode);
        Assert.Contains("operational-preflight-workflow-availability-invalid",
            missingWorkflow.RunPreflight(ControlledPilotOperationalFixture.WindowStart)
                .Preflight!.ReasonCodes);
        Assert.Contains("operational-preflight-evidence-destination-unavailable",
            noDestination.RunPreflight(ControlledPilotOperationalFixture.WindowStart)
                .Preflight!.ReasonCodes);
    }

    [Fact]
    public void Preflight_returns_review_for_reviewable_release_or_prerequisite()
    {
        ControlledPilotOperationalFixture fixture = ControlledPilotOperationalFixture.Rasht();
        using ControlledPilotOperationalRehearsalCoordinator coordinator = fixture.Coordinator(
            release: fixture.Release(OperationalEvidenceStatus.RequiresReview));

        ControlledPilotOperationalOperationResult result = coordinator.RunPreflight(
            ControlledPilotOperationalFixture.WindowStart);

        Assert.Equal(ControlledPilotOperationalPreflightStatus.RequiresReview,
            result.Preflight!.Status);
        Assert.Equal(ControlledPilotOperationalLifecycle.ReviewRequired, result.Lifecycle);
    }

    [Theory]
    [InlineData("rasht")]
    [InlineData("ramsar")]
    public async Task End_to_end_rehearsal_completes_all_five_workflows_and_persists_bundle(
        string fixtureName)
    {
        ControlledPilotOperationalFixture fixture = Fixture(fixtureName);
        var destination = new InMemoryControlledPilotOperationalEvidenceDestination();
        using ControlledPilotOperationalRehearsalCoordinator coordinator = fixture.Coordinator(
            destination: destination);

        AdvanceToStarted(coordinator);
        ControlledPilotOperationalOperationResult observed = await coordinator.ObserveAsync(
            ControlledPilotOperationalFixture.WindowStart.AddMinutes(3));
        Assert.Equal(ControlledPilotOperationalLifecycle.ReviewRequired, observed.Lifecycle);
        Assert.Equal(5, observed.WorkflowResults.Count);
        Assert.All(observed.WorkflowResults, result =>
            Assert.Equal(OperationalWorkflowComparisonStatus.Match, result.Status));

        ControlledPilotOperationalOperationResult completed = await coordinator
            .RecordOperatorDecisionAsync(Decision(OperationalOperatorDecisionKind.Complete),
                ControlledPilotOperationalFixture.WindowStart.AddMinutes(5));

        Assert.Equal(ControlledPilotOperationalLifecycle.Completed, completed.Lifecycle);
        Assert.NotNull(completed.EvidenceBundle);
        Assert.True(completed.EvidenceBundle!.HasValidChecksum);
        Assert.Equal(5, completed.EvidenceBundle.FingerprintVersions.Count);
        Assert.Equal(5, completed.EvidenceBundle.Comparisons.Count);
        Assert.Equal(ControlledPilotOperationalHealthStatus.Healthy,
            completed.EvidenceBundle.MonitoringEvidence.Status);
        Assert.Single(destination.Bundles);
        Assert.False(completed.MutatedProduction);
        Assert.False(completed.SwitchedAuthority);
        Assert.False(completed.ExecutedMigration);
        Assert.False(completed.ExecutedEsdCutover);
    }

    [Theory]
    [InlineData(PilotValidationWorkflow.Authentication)]
    [InlineData(PilotValidationWorkflow.Reporting)]
    [InlineData(PilotValidationWorkflow.RuntimeEvent)]
    [InlineData(PilotValidationWorkflow.ProtectedSettings)]
    [InlineData(PilotValidationWorkflow.Export)]
    public async Task Changed_semantic_input_produces_expected_workflow_mismatch_and_stop(
        PilotValidationWorkflow workflow)
    {
        ControlledPilotOperationalFixture fixture = ControlledPilotOperationalFixture.Rasht();
        using ControlledPilotOperationalRehearsalCoordinator coordinator = fixture.Coordinator(
            differenceWorkflow: workflow);
        AdvanceToStarted(coordinator);

        ControlledPilotOperationalOperationResult result = await coordinator.ObserveAsync(
            ControlledPilotOperationalFixture.WindowStart.AddMinutes(3));

        Assert.Equal(ControlledPilotOperationalLifecycle.Stopped, result.Lifecycle);
        Assert.Equal(ControlledPilotOperationalStopReason.FingerprintMismatchAbovePolicy,
            result.StopDecision!.Reason);
        ControlledPilotOperationalWorkflowResult difference = Assert.Single(
            result.WorkflowResults, item => item.Workflow == workflow);
        Assert.Equal(OperationalWorkflowComparisonStatus.Difference, difference.Status);
        Assert.NotEqual(difference.LegacyFingerprint, difference.TargetFingerprint);
        Assert.True(result.EvidenceBundle!.HasValidChecksum);
    }

    [Fact]
    public async Task Allowed_difference_requires_operator_review_and_can_complete_as_evidence()
    {
        ControlledPilotOperationalFixture fixture = ControlledPilotOperationalFixture.Rasht();
        using ControlledPilotOperationalRehearsalCoordinator coordinator = fixture.Coordinator(
            differenceWorkflow: PilotValidationWorkflow.Reporting, allowedDifferences: 1);
        AdvanceToStarted(coordinator);

        ControlledPilotOperationalOperationResult observed = await coordinator.ObserveAsync(
            ControlledPilotOperationalFixture.WindowStart.AddMinutes(3));
        ControlledPilotOperationalOperationResult completed = await coordinator
            .RecordOperatorDecisionAsync(Decision(OperationalOperatorDecisionKind.Complete),
                ControlledPilotOperationalFixture.WindowStart.AddMinutes(5));

        Assert.Equal(ControlledPilotOperationalLifecycle.ReviewRequired, observed.Lifecycle);
        Assert.Equal(ControlledPilotOperationalHealthStatus.AttentionRequired,
            observed.MonitoringEvidence!.Status);
        Assert.Equal(ControlledPilotOperationalLifecycle.Completed, completed.Lifecycle);
    }

    [Theory]
    [InlineData(OperationalOperatorDecisionKind.Stop,
        ControlledPilotOperationalStopReason.ExplicitOperatorStop)]
    [InlineData(OperationalOperatorDecisionKind.RequestRollback,
        ControlledPilotOperationalStopReason.RollbackRequested)]
    public async Task Explicit_stop_and_rollback_request_are_evidence_only(
        OperationalOperatorDecisionKind kind,
        ControlledPilotOperationalStopReason expected)
    {
        ControlledPilotOperationalFixture fixture = ControlledPilotOperationalFixture.Rasht();
        using ControlledPilotOperationalRehearsalCoordinator coordinator = fixture.Coordinator();
        AdvanceToStarted(coordinator);
        await coordinator.ObserveAsync(
            ControlledPilotOperationalFixture.WindowStart.AddMinutes(3));

        ControlledPilotOperationalOperationResult result = await coordinator
            .RecordOperatorDecisionAsync(Decision(kind),
                ControlledPilotOperationalFixture.WindowStart.AddMinutes(5));

        Assert.Equal(ControlledPilotOperationalLifecycle.Stopped, result.Lifecycle);
        Assert.Equal(expected, result.StopDecision!.Reason);
        Assert.True(result.StopDecision.StopsRehearsalOnly);
        Assert.False(result.StopDecision.AutomaticallyStopsProduction);
        Assert.False(result.StopDecision.ExecutesRollback);
        Assert.False(Decision(kind).ExecutesRollback);
    }

    [Theory]
    [InlineData("rollback", ControlledPilotOperationalStopReason.RollbackReadinessLost)]
    [InlineData("security", ControlledPilotOperationalStopReason.SecurityBoundaryViolation)]
    [InlineData("integrity", ControlledPilotOperationalStopReason.EvidenceIntegrityFailure)]
    public async Task Review_stop_conditions_fail_closed(string condition,
        ControlledPilotOperationalStopReason expected)
    {
        ControlledPilotOperationalFixture fixture = ControlledPilotOperationalFixture.Rasht();
        using ControlledPilotOperationalRehearsalCoordinator coordinator = fixture.Coordinator();
        AdvanceToStarted(coordinator);
        await coordinator.ObserveAsync(
            ControlledPilotOperationalFixture.WindowStart.AddMinutes(3));

        ControlledPilotOperationalOperationResult result = await coordinator
            .RecordOperatorDecisionAsync(Decision(OperationalOperatorDecisionKind.Complete),
                ControlledPilotOperationalFixture.WindowStart.AddMinutes(5),
                rollbackReady: condition != "rollback",
                securityBoundaryViolated: condition == "security",
                evidenceIntegrityValid: condition != "integrity");

        Assert.Equal(ControlledPilotOperationalLifecycle.Stopped, result.Lifecycle);
        Assert.Equal(expected, result.StopDecision!.Reason);
    }

    [Fact]
    public async Task Cancellation_stops_only_rehearsal_and_retains_immutable_evidence()
    {
        ControlledPilotOperationalFixture fixture = ControlledPilotOperationalFixture.Rasht();
        using ControlledPilotOperationalRehearsalCoordinator coordinator = fixture.Coordinator();
        AdvanceToStarted(coordinator);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        ControlledPilotOperationalOperationResult result = await coordinator.ObserveAsync(
            ControlledPilotOperationalFixture.WindowStart.AddMinutes(3), cancellation.Token);

        Assert.Equal(ControlledPilotOperationalLifecycle.Stopped, result.Lifecycle);
        Assert.Equal(ControlledPilotOperationalStopReason.Cancellation,
            result.StopDecision!.Reason);
        Assert.NotNull(result.EvidenceBundle);
        Assert.True(result.EvidenceBundle!.HasValidChecksum);
        Assert.Equal(ControlledPilotOperationalOperationStatus.Blocked,
            coordinator.Start(ControlledPilotOperationalFixture.WindowStart.AddMinutes(4)).Status);
    }

    [Fact]
    public async Task Observer_and_destination_failures_are_isolated_without_raw_error_text()
    {
        ControlledPilotOperationalFixture fixture = ControlledPilotOperationalFixture.Rasht();
        IControlledPilotOperationalWorkflowObserver[] observers = fixture.Observers();
        observers[0] = new ThrowingAuthenticationObserver();
        using ControlledPilotOperationalRehearsalCoordinator observerFailure = fixture.Coordinator(
            observers: observers);
        AdvanceToStarted(observerFailure);

        ControlledPilotOperationalOperationResult stopped = await observerFailure.ObserveAsync(
            ControlledPilotOperationalFixture.WindowStart.AddMinutes(3));
        Assert.Equal(ControlledPilotOperationalStopReason.ObserverFailure,
            stopped.StopDecision!.Reason);
        Assert.DoesNotContain("sensitive", stopped.ReasonCode,
            StringComparison.OrdinalIgnoreCase);

        using ControlledPilotOperationalRehearsalCoordinator destinationFailure =
            fixture.Coordinator(destination: new RejectingDestination());
        AdvanceToStarted(destinationFailure);
        await destinationFailure.ObserveAsync(
            ControlledPilotOperationalFixture.WindowStart.AddMinutes(3));
        ControlledPilotOperationalOperationResult failed = await destinationFailure
            .RecordOperatorDecisionAsync(Decision(OperationalOperatorDecisionKind.Complete),
                ControlledPilotOperationalFixture.WindowStart.AddMinutes(5));
        Assert.Equal(ControlledPilotOperationalLifecycle.Failed, failed.Lifecycle);
        Assert.Equal("operational-evidence-destination-failed", failed.ReasonCode);
        Assert.NotNull(failed.EvidenceBundle);
    }

    [Fact]
    public async Task Fingerprints_are_order_and_culture_independent()
    {
        var first = new AuthenticationOperationalObservation("station-rasht", true, true,
            true, true, ["capability-b", "capability-a"]);
        var second = new AuthenticationOperationalObservation("station-rasht", true, true,
            true, true, ["capability-a", "capability-b"]);
        var spec = new AuthenticationFingerprintSpecification();
        string originalCulture = CultureInfo.CurrentCulture.Name;
        string firstFingerprint;
        string secondFingerprint;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fa-IR");
            firstFingerprint = spec.CreateFingerprint(first);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            secondFingerprint = spec.CreateFingerprint(second);
        }
        finally
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(originalCulture);
        }

        Assert.Equal(firstFingerprint, secondFingerprint);
        ControlledPilotOperationalFixture fixture = ControlledPilotOperationalFixture.Rasht();
        IControlledPilotOperationalWorkflowObserver reportObserver = fixture.Observers()
            .Single(observer => observer.Workflow == PilotValidationWorkflow.Reporting);
        ControlledPilotOperationalWorkflowResult? result = await reportObserver.ObserveAsync(
            fixture.Context(), ControlledPilotOperationalFixture.WindowStart);
        Assert.Equal(result!.LegacyFingerprint, result.TargetFingerprint);
    }

    [Theory]
    [InlineData(PilotValidationWorkflow.Authentication,
        "9CE756A65A862E6ACFE8E1DEE9C2504D67450576B326C5CEC2BFCF0AE6F7E29C")]
    [InlineData(PilotValidationWorkflow.Reporting,
        "BE13E8F15CB8283E8A47B20947F9B6EC8DB0070C2CA954D3446B934A61615550")]
    [InlineData(PilotValidationWorkflow.RuntimeEvent,
        "04B803482FD1291F1CE04BF5BF7B3ED347A72607E5F65D91B96A7FE93C581302")]
    [InlineData(PilotValidationWorkflow.ProtectedSettings,
        "7CC5190B12B1E3BE4AB5978FCD662C17E2A85859ED0F5AE3419C00DF329EE2ED")]
    [InlineData(PilotValidationWorkflow.Export,
        "24AAC29BD7F008F806B7ED28AF189AFD329F24D5783153F405A14C313FFA6816")]
    public async Task Rasht_fingerprint_golden_vectors_are_stable(
        PilotValidationWorkflow workflow,
        string expected)
    {
        ControlledPilotOperationalFixture fixture = ControlledPilotOperationalFixture.Rasht();
        IControlledPilotOperationalWorkflowObserver observer = fixture.Observers()
            .Single(item => item.Workflow == workflow);

        ControlledPilotOperationalWorkflowResult? result = await observer.ObserveAsync(
            fixture.Context(), ControlledPilotOperationalFixture.WindowStart);

        Assert.Equal(expected, result!.LegacyFingerprint);
        Assert.Equal(expected, result.TargetFingerprint);
    }

    [Fact]
    public async Task Bundle_checksum_is_reproducible_for_identical_inputs()
    {
        ControlledPilotOperationalFixture fixture = ControlledPilotOperationalFixture.Rasht();
        using ControlledPilotOperationalRehearsalCoordinator first = fixture.Coordinator();
        using ControlledPilotOperationalRehearsalCoordinator second = fixture.Coordinator();

        ControlledPilotOperationalEvidenceBundle firstBundle = await Complete(first);
        ControlledPilotOperationalEvidenceBundle secondBundle = await Complete(second);

        Assert.Equal(firstBundle.BundleChecksum, secondBundle.BundleChecksum);
        Assert.True(firstBundle.HasValidChecksum);
        Assert.True(secondBundle.HasValidChecksum);
    }

    [Fact]
    public void Runbook_has_all_safe_operator_steps_and_no_destructive_automation()
    {
        ControlledPilotOperationalRunbookDefinition runbook =
            ControlledPilotOperationalRunbookDefinition.Standard;

        Assert.Equal(Enum.GetValues<OperationalRunbookStepKind>().Length,
            runbook.Steps.Count);
        Assert.Equal(runbook.Steps.Count, runbook.Steps.Select(step => step.StepId)
            .Distinct(StringComparer.Ordinal).Count());
        Assert.All(runbook.Steps, step => Assert.StartsWith("OPR-", step.StepId));
        Assert.False(runbook.AutomatesDestructiveActions);
    }

    [Fact]
    public void Operational_contracts_are_immutable_and_exclude_sensitive_capabilities()
    {
        Type[] contracts =
        [
            typeof(ControlledPilotOperationalRehearsalContext),
            typeof(AuthenticationOperationalObservation),
            typeof(ReportingOperationalObservation),
            typeof(RuntimeEventOperationalObservation),
            typeof(ProtectedSettingsOperationalObservation),
            typeof(ExportOperationalObservation),
            typeof(ControlledPilotOperationalEvidenceBundle),
            typeof(ControlledPilotOperationalRunbookDefinition)
        ];
        Assert.All(contracts, type => Assert.DoesNotContain(type.GetProperties(),
            property => property.SetMethod is not null));

        var authentication = new AuthenticationOperationalObservation("station", true,
            true, true, true, ["observe"]);
        var settings = new ProtectedSettingsOperationalObservation("station", "active",
            2m, "effective-evidence", true, true);
        Assert.False(authentication.AcceptsPassword);
        Assert.False(authentication.ContainsCredentialHash);
        Assert.False(authentication.CreatesSession);
        Assert.False(authentication.ImplementsRoles);
        Assert.False(settings.VerifiesManagementCredential);
        Assert.False(settings.ExecutesVendorAuthorization);
        Assert.False(settings.MutatesEsdAdjustment);
        Assert.False(settings.RecoversOrProvisions);
    }

    [Fact]
    public void Coordinator_has_no_production_authority_or_automatic_execution_capability()
    {
        using ControlledPilotOperationalRehearsalCoordinator coordinator =
            ControlledPilotOperationalFixture.Rasht().Coordinator();

        Assert.False(coordinator.AutomaticallyRuns);
        Assert.False(coordinator.AutomaticallyRetries);
        Assert.False(coordinator.UsesTimer);
        Assert.False(coordinator.UsesScheduler);
        Assert.False(coordinator.UsesPolling);
        Assert.False(coordinator.UsesBackgroundWorker);
        Assert.False(coordinator.MutatesProductionDatabase);
        Assert.False(coordinator.RunsMigration);
        Assert.False(coordinator.PerformsEsdCutover);
        Assert.False(coordinator.ChangesProductionAuthority);
        Assert.False(coordinator.ReplacesProductionUi);
        Assert.False(coordinator.ImplementsRbac);
        Assert.False(coordinator.CreatesIdentities);
    }

    [Fact]
    public void Disposal_models_application_close_and_is_terminal()
    {
        ControlledPilotOperationalRehearsalCoordinator coordinator =
            ControlledPilotOperationalFixture.Rasht().Coordinator();
        AdvanceToStarted(coordinator);

        coordinator.Dispose();
        coordinator.Dispose();

        Assert.Equal(ControlledPilotOperationalLifecycle.Disposed, coordinator.Lifecycle);
        Assert.Equal(ControlledPilotOperationalOperationStatus.Disposed,
            coordinator.RunPreflight(ControlledPilotOperationalFixture.WindowStart).Status);
    }

    [Fact]
    public void Production_startup_navigation_forms_and_database_layers_are_untouched()
    {
        string root = RepositoryRoot();
        string programPath = Path.Combine(root, "Program.cs");
        string productionSurface = File.ReadAllText(programPath) + Environment.NewLine +
            string.Join(Environment.NewLine,
                Directory.GetFiles(Path.Combine(root, "UI", "Startup"), "*.cs",
                        SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(Path.Combine(root, "UI", "Forms"), "*.cs",
                        SearchOption.AllDirectories)).Select(File.ReadAllText));
        string operationalSource = string.Join(Environment.NewLine,
            Directory.GetFiles(Path.Combine(root, "Application", "Pilot", "Operational"),
                "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));

        Assert.Equal("33985F732E77AFC7249DDA0174E8BCC58601B5E0B3E22B93E31933F01ACCAA76",
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(programPath))));
        Assert.DoesNotContain("Application.Pilot.Operational", productionSurface,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ControlledPilotOperationalRehearsalCoordinator",
            productionSurface, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.Data.Sqlite", operationalSource,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MigrationRunner", operationalSource,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Task.Run", operationalSource, StringComparison.Ordinal);
        Assert.DoesNotContain("PeriodicTimer", operationalSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new Timer", operationalSource, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Windows.Forms", operationalSource,
            StringComparison.OrdinalIgnoreCase);
    }

    private static ControlledPilotOperationalFixture Fixture(string name) => name switch
    {
        "rasht" => ControlledPilotOperationalFixture.Rasht(),
        "ramsar" => ControlledPilotOperationalFixture.Ramsar(),
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    private static void AdvanceToStarted(ControlledPilotOperationalRehearsalCoordinator coordinator)
    {
        Assert.Equal(ControlledPilotOperationalOperationStatus.Accepted,
            coordinator.RunPreflight(ControlledPilotOperationalFixture.WindowStart).Status);
        Assert.Equal(ControlledPilotOperationalOperationStatus.Accepted,
            coordinator.Approve(ControlledPilotOperationalFixture.WindowStart.AddMinutes(1)).Status);
        Assert.Equal(ControlledPilotOperationalOperationStatus.Accepted,
            coordinator.Start(ControlledPilotOperationalFixture.WindowStart.AddMinutes(2)).Status);
    }

    private static ControlledPilotOperationalOperatorDecision Decision(
        OperationalOperatorDecisionKind kind) => new("operator-decision-1", kind,
        "operator-decision-evidence",
        ControlledPilotOperationalFixture.WindowStart.AddMinutes(4));

    private static async Task<ControlledPilotOperationalEvidenceBundle> Complete(
        ControlledPilotOperationalRehearsalCoordinator coordinator)
    {
        AdvanceToStarted(coordinator);
        await coordinator.ObserveAsync(
            ControlledPilotOperationalFixture.WindowStart.AddMinutes(3));
        ControlledPilotOperationalOperationResult result = await coordinator
            .RecordOperatorDecisionAsync(Decision(OperationalOperatorDecisionKind.Complete),
                ControlledPilotOperationalFixture.WindowStart.AddMinutes(5));
        return result.EvidenceBundle!;
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName,
                   "Rah_Negar.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }

    private sealed class UnavailableDestination :
        IControlledPilotOperationalEvidenceDestination
    {
        public bool IsAvailable => false;
        public bool SupportsCancellation => true;
        public ValueTask<bool> WriteAsync(ControlledPilotOperationalEvidenceBundle bundle,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
    }

    private sealed class RejectingDestination :
        IControlledPilotOperationalEvidenceDestination
    {
        public bool IsAvailable => true;
        public bool SupportsCancellation => true;
        public ValueTask<bool> WriteAsync(ControlledPilotOperationalEvidenceBundle bundle,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
    }

    private sealed class ThrowingAuthenticationObserver : IAuthenticationOperationalObserver
    {
        public PilotValidationWorkflow Workflow => PilotValidationWorkflow.Authentication;
        public string FingerprintSpecificationVersion => "auth-fingerprint-v1";
        public bool IsAvailable => true;
        public bool IsReadOnly => true;
        public bool SupportsCancellation => true;
        public bool RequiresReview => false;
        public ValueTask<ControlledPilotOperationalWorkflowResult?> ObserveAsync(
            ControlledPilotOperationalRehearsalContext context,
            DateTimeOffset observedAtUtc,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("sensitive failure detail");
    }
}
