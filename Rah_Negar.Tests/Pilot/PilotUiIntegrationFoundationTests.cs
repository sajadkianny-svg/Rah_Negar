using System.Reflection;
using Rah_Negar.Foundation.Application.Integration;
using Rah_Negar.Foundation.Application.Pilot;
using Rah_Negar.Foundation.Application.Pilot.Hosting;
using Rah_Negar.Foundation.Application.Pilot.Presentation;

namespace Rah_Negar.Tests.Pilot;

public sealed class PilotUiIntegrationFoundationTests
{
    private static readonly DateTimeOffset Started =
        new(2026, 8, 22, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Completed = Started.AddSeconds(2);

    [Fact]
    public void Feature_presenters_map_all_host_features_to_safe_view_states()
    {
        IPilotResultPresenter[] presenters = Presenters();

        PilotFeatureViewState[] states = presenters.Select(presenter =>
            presenter.Present(Result(presenter.Feature, PilotExecutionStatus.Completed,
                ShadowDifferenceSeverity.None))).ToArray();

        Assert.Equal(5, states.Length);
        Assert.All(states, state =>
        {
            Assert.Equal(PilotUiViewStatus.Completed, state.Status);
            Assert.Equal("Legacy and target observations matched.", state.ComparisonSummary);
            Assert.Contains("Legacy remains authoritative", state.SafeDescription);
            Assert.True(state.LegacyAuthorityPreserved);
            Assert.False(state.AllowsExecution);
            Assert.False(state.AllowsRouting);
            Assert.False(state.AllowsActivation);
        });
        Assert.Equal(new[]
        {
            "Authentication pilot", "Reporting pilot", "Runtime and Event pilot",
            "Protected settings pilot", "Export pilot"
        }, states.Select(state => state.Title));
    }

    [Fact]
    public void View_state_is_immutable_and_defensively_copies_collections()
    {
        var warnings = new List<string> { "warning-b" };
        var reasons = new List<string> { "reason-b" };
        var state = new PilotFeatureViewState("pilot-1", PilotFeature.ReportingPilot,
            "Reporting pilot", PilotUiViewStatus.Blocked, "Safe.",
            ShadowDifferenceSeverity.Warning, "Comparison.", PilotEvidenceState.Blocked,
            null, Completed, warnings, reasons, "correlation-1");

        warnings.Add("later-warning");
        reasons.Add("later-reason");

        Assert.Equal(["warning-b"], state.Warnings);
        Assert.Equal(["reason-b"], state.BlockedReasons);
        Assert.All(typeof(PilotFeatureViewState).GetProperties(),
            property => Assert.Null(property.SetMethod));
        Assert.All(typeof(PilotDashboardState).GetProperties(),
            property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void Safe_feedback_never_carries_internal_or_host_supplied_text()
    {
        const string hostile = "password=secret; SQL error at C:\\data\\production.db\nstack trace";
        PilotExecutionResult result = Result(PilotFeature.AuthenticationPilot,
            PilotExecutionStatus.Blocked, ShadowDifferenceSeverity.Failed,
            evidenceId: hostile, correlationId: hostile,
            safeSummary: hostile, blockedReasons: [hostile]);

        PilotFeatureViewState state = new AuthenticationPilotPresenter().Present(result);
        string rendered = string.Join('|', state.Title, state.SafeDescription,
            state.ComparisonSummary, state.EvidenceReference, state.CorrelationId,
            string.Join('|', state.Warnings), string.Join('|', state.BlockedReasons));

        Assert.DoesNotContain("password", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SQL", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("production.db", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack trace", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.Null(state.EvidenceReference);
        Assert.Equal("correlation-unavailable", state.CorrelationId);
        Assert.Contains("safety rule", state.BlockedReasons.Single());
    }

    [Fact]
    public void Presentation_contracts_exclude_secrets_credentials_raw_rows_and_signatures()
    {
        Type[] types = PresentationTypes();
        string properties = string.Join('|', types.SelectMany(type => type.GetProperties())
            .Select(property => property.Name));

        Assert.DoesNotContain("Password", properties, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Credential", properties, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RawRow", properties, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Signature", properties, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PrivateKey", properties, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", properties, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", properties, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unknown_feature_is_failed_closed_without_presenter_or_activation()
    {
        var coordinator = Coordinator();
        PilotExecutionResult unknown = Result((PilotFeature)999,
            PilotExecutionStatus.Completed, ShadowDifferenceSeverity.None);

        PilotFeatureViewState state = coordinator.Present(unknown);

        Assert.Null(state.Feature);
        Assert.Equal(PilotUiViewStatus.Failed, state.Status);
        Assert.Equal("Pilot result", state.Title);
        Assert.False(state.AllowsActivation);
        Assert.True(state.LegacyAuthorityPreserved);
    }

    [Fact]
    public void Blocked_result_maps_known_codes_and_suppresses_unknown_codes()
    {
        PilotExecutionResult blocked = Result(PilotFeature.ProtectedSettingsPilot,
            PilotExecutionStatus.Blocked, ShadowDifferenceSeverity.Failed,
            blockedReasons: ["settings-mutation-prohibited", "internal-secret-reason"]);

        PilotFeatureViewState state = new ProtectedSettingsPilotPresenter().Present(blocked);

        Assert.Equal(PilotUiViewStatus.Blocked, state.Status);
        Assert.Contains("Settings changes are prohibited in pilot observation.", state.BlockedReasons);
        Assert.Contains("The pilot result was blocked by a safety rule.", state.BlockedReasons);
        Assert.DoesNotContain(state.BlockedReasons,
            reason => reason.Contains("internal-secret", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(ShadowDifferenceSeverity.Informational,
        "An informational comparison difference is available.")]
    [InlineData(ShadowDifferenceSeverity.Warning, "A comparison difference requires review.")]
    [InlineData(ShadowDifferenceSeverity.Critical,
        "A critical comparison difference requires manual review.")]
    [InlineData(ShadowDifferenceSeverity.Failed, "Comparison evidence is unavailable.")]
    public void Comparison_severity_maps_to_fixed_operator_warning(
        ShadowDifferenceSeverity severity,
        string expectedWarning)
    {
        PilotFeatureViewState state = new ReportingPilotPresenter().Present(
            Result(PilotFeature.ReportingPilot, PilotExecutionStatus.CompletedWithDifference, severity));

        Assert.Equal(severity, state.Severity);
        Assert.Equal(PilotUiViewStatus.DifferenceDetected, state.Status);
        Assert.Contains(expectedWarning, state.Warnings);
    }

    [Fact]
    public void Evidence_reference_and_correlation_are_propagated_when_safe()
    {
        PilotFeatureViewState state = new ExportPilotPresenter().Present(
            Result(PilotFeature.ExportPilot, PilotExecutionStatus.Completed,
                ShadowDifferenceSeverity.None, "evidence-8.4", "correlation:8.4"));

        Assert.Equal("evidence-8.4", state.EvidenceReference);
        Assert.Equal(PilotEvidenceState.Complete, state.EvidenceState);
        Assert.Equal("correlation:8.4", state.CorrelationId);
    }

    [Fact]
    public void Timestamp_uses_completed_utc_and_invalid_time_fails_to_epoch()
    {
        PilotFeatureViewState valid = new RuntimeEventPilotPresenter().Present(
            Result(PilotFeature.RuntimeEventPilot, PilotExecutionStatus.Completed,
                ShadowDifferenceSeverity.None));
        PilotExecutionResult invalidResult = Result(PilotFeature.RuntimeEventPilot,
            PilotExecutionStatus.Completed, ShadowDifferenceSeverity.None,
            startedAt: Started.ToOffset(TimeSpan.FromHours(3.5)), completedAt: Completed);
        PilotFeatureViewState invalid = new RuntimeEventPilotPresenter().Present(invalidResult);

        Assert.Equal(Completed, valid.TimestampUtc);
        Assert.Equal(DateTimeOffset.UnixEpoch, invalid.TimestampUtc);
        Assert.Equal(TimeSpan.Zero, valid.TimestampUtc.Offset);
        Assert.Equal(TimeSpan.Zero, invalid.TimestampUtc.Offset);
    }

    [Fact]
    public void Presenter_failure_is_isolated_and_preserves_safe_evidence_reference()
    {
        var coordinator = new PilotPresentationCoordinator(
            [new ThrowingPresenter(PilotFeature.AuthenticationPilot)]);
        PilotExecutionResult result = Result(PilotFeature.AuthenticationPilot,
            PilotExecutionStatus.Completed, ShadowDifferenceSeverity.None,
            "evidence-safe-1", "correlation-safe-1");

        PilotFeatureViewState state = coordinator.Present(result);

        Assert.Equal(PilotUiViewStatus.Failed, state.Status);
        Assert.Equal("evidence-safe-1", state.EvidenceReference);
        Assert.Equal(PilotEvidenceState.Complete, state.EvidenceState);
        Assert.True(result.LegacyAuthorityPreserved);
        Assert.Equal(PilotExecutionStatus.Completed, result.Status);
        Assert.False(state.AllowsExecution);
    }

    [Fact]
    public void Loading_and_dashboard_models_are_display_only()
    {
        var coordinator = Coordinator();
        PilotFeatureViewState loading = coordinator.CreateLoading(PilotFeature.ReportingPilot,
            "pilot-ui-1", "correlation-ui-1", Started);
        PilotDashboardState dashboard = coordinator.CreateDashboard(loading, true, true);

        Assert.Equal(PilotUiViewStatus.Loading, loading.Status);
        Assert.Equal("pilot-ui-1", dashboard.ActivePilotId);
        Assert.Equal(PilotFeature.ReportingPilot, dashboard.SelectedFeature);
        Assert.False(dashboard.EvidenceAvailable);
        Assert.True(dashboard.RollbackAvailable);
        Assert.False(dashboard.CanActivateFeature);
        Assert.False(dashboard.CanSwitchAuthority);
    }

    [Fact]
    public void Capability_boundary_is_UI_neutral_and_has_no_role_or_table_implementation()
    {
        Type[] types = PresentationTypes();

        Assert.Equal(["pilot.view", "evidence.view", "comparison.view"], PilotUiCapabilities.All);
        Assert.True(typeof(IPilotUiCapabilityBoundary).IsInterface);
        Assert.DoesNotContain(types, type => typeof(IPilotUiCapabilityBoundary).IsAssignableFrom(type) &&
            !type.IsInterface);
        Assert.DoesNotContain(types, type => type.Name.Contains("Role", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(types, type => type.Name.Contains("Table", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WinForms_boundary_supports_future_surfaces_but_has_no_implementation()
    {
        Type[] types = PresentationTypes();

        Assert.Equal(3, Enum.GetValues<PilotUiSurfaceKind>().Length);
        Assert.True(typeof(IPilotWinFormsStateConsumer).IsInterface);
        Assert.DoesNotContain(types, type => typeof(IPilotWinFormsStateConsumer).IsAssignableFrom(type) &&
            !type.IsInterface);
        Assert.DoesNotContain(types, type => type.BaseType?.Name.Contains("Form",
            StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void Coordinator_has_no_host_execution_service_locator_or_external_state_dependency()
    {
        var coordinator = Coordinator();
        Type coordinatorType = coordinator.GetType();
        Type[] fieldTypes = coordinatorType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Select(field => field.FieldType).ToArray();
        string methods = string.Join('|', coordinatorType.GetMethods(BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName).Select(method => method.Name));

        Assert.False(coordinator.ExecutesPilotWorkflows);
        Assert.False(coordinator.RoutesPilotFeatures);
        Assert.False(coordinator.ActivatesPilotFeatures);
        Assert.False(coordinator.ReadsExternalState);
        Assert.DoesNotContain(fieldTypes, type => typeof(IPilotHost).IsAssignableFrom(type));
        Assert.DoesNotContain(fieldTypes, type => type == typeof(IServiceProvider));
        Assert.DoesNotContain("Execute", methods, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Activate", methods, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Migrate", methods, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Presentation_namespace_has_no_mutation_migration_RBAC_or_support_identity()
    {
        Type[] types = PresentationTypes();
        string names = string.Join('|', types.Select(type => type.Name));
        string methods = string.Join('|', types.SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.DeclaredOnly))
            .Select(method => method.Name));

        Assert.DoesNotContain("Connection", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Repository", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Migration", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Insert", methods, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Update", methods, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Delete", methods, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Save", methods, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Rbac", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupportRole", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupportProfile", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupportLogin", names, StringComparison.OrdinalIgnoreCase);
    }

    private static PilotPresentationCoordinator Coordinator() => new(Presenters());

    private static IPilotResultPresenter[] Presenters() =>
    [
        new AuthenticationPilotPresenter(),
        new ReportingPilotPresenter(),
        new RuntimeEventPilotPresenter(),
        new ProtectedSettingsPilotPresenter(),
        new ExportPilotPresenter()
    ];

    private static PilotExecutionResult Result(
        PilotFeature feature,
        PilotExecutionStatus status,
        ShadowDifferenceSeverity severity,
        string? evidenceId = "evidence-ui-1",
        string correlationId = "correlation-ui-1",
        string safeSummary = "host-provided-summary",
        IEnumerable<string>? blockedReasons = null,
        DateTimeOffset? startedAt = null,
        DateTimeOffset? completedAt = null) =>
        new("pilot-ui-1", feature, status, null, null,
            new(status == PilotExecutionStatus.Completed, severity, safeSummary,
                severity == ShadowDifferenceSeverity.None ? Array.Empty<string>() : ["difference"]),
            evidenceId, correlationId, startedAt ?? Started, completedAt ?? Completed,
            blockedReasons ?? Array.Empty<string>());

    private static Type[] PresentationTypes() => typeof(PilotPresentationCoordinator).Assembly.GetTypes()
        .Where(type => type.Namespace == typeof(PilotPresentationCoordinator).Namespace).ToArray();

    private sealed class ThrowingPresenter(PilotFeature feature) : IPilotResultPresenter
    {
        public PilotFeature Feature { get; } = feature;
        public PilotFeatureViewState Present(PilotExecutionResult result) =>
            throw new InvalidOperationException("test-only secret exception");
    }
}
