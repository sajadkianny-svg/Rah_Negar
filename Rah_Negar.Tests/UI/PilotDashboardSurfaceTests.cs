using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using Rah_Negar.Foundation.Application.Integration;
using Rah_Negar.Foundation.Application.Pilot;
using Rah_Negar.Foundation.Application.Pilot.Live;
using Rah_Negar.Foundation.Application.Pilot.Operational;
using Rah_Negar.Foundation.Application.Pilot.Presentation;
using Rah_Negar.Foundation.Application.Pilot.Validation;
using Rah_Negar.UI.Pilot;

namespace Rah_Negar.Tests.UI;

public sealed class PilotDashboardSurfaceTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 8, 22, 19, 30, 45, TimeSpan.Zero);

    [Fact]
    public void Dashboard_is_an_isolated_explicitly_constructed_user_control()
    {
        RunSta(() =>
        {
            using var surface = new PilotDashboardControl();

            Assert.IsAssignableFrom<UserControl>(surface);
            Assert.Equal(PilotUiSurfaceKind.EmbeddedPilotPanel, surface.SurfaceKind);
            Assert.False(surface.AutomaticallyOpens);
            Assert.False(surface.ExecutesCommands);
            Assert.False(surface.RequestsRefresh);
            Assert.False(surface.HasState);
            Assert.NotEmpty(surface.Controls.Cast<Control>());
        });
    }

    [Fact]
    public void Dashboard_renders_complete_immutable_state()
    {
        RunSta(() =>
        {
            using var surface = new PilotDashboardControl();
            PilotDashboardState state = State(PilotUiViewStatus.Completed,
                ShadowDifferenceSeverity.None, evidenceAvailable: true, rollbackAvailable: true);

            surface.ReplaceState(state);
            PilotSurfaceSnapshot rendered = surface.Snapshot;

            Assert.True(surface.HasState);
            Assert.Equal("pilot-surface-1", rendered.PilotId);
            Assert.Equal("Reporting pilot", rendered.SelectedFeature);
            Assert.Equal("Completed", rendered.ExecutionState);
            Assert.Equal("Legacy and target observations matched.", rendered.ComparisonStatus);
            Assert.Equal("None", rendered.Severity);
            Assert.Equal("Available: evidence-surface-1", rendered.EvidenceSummary);
            Assert.Equal("Available", rendered.RollbackSummary);
            Assert.Equal("correlation-surface-1", rendered.CorrelationId);
            Assert.Equal("2026-08-22 19:30:45 UTC", rendered.Timestamp);
            Assert.False(rendered.UsesSafeFallback);
        });
    }

    [Fact]
    public void Selecting_live_workflow_row_refreshes_safe_top_detail_panel()
    {
        RunSta(() =>
        {
            using var surface = new PilotDashboardControl();
            var view = new LivePilotDashboardView("pilot-live-1", "Rasht", "Stopped",
                "Ready",
                [
                    new LivePilotWorkflowView(PilotValidationWorkflow.Authentication,
                        "Completed", "Difference", "auth-fingerprint-v1",
                        OperationalWorkflowComparisonStatus.Difference,
                        "live-authentication-evidence", Timestamp),
                    new LivePilotWorkflowView(PilotValidationWorkflow.Reporting,
                        "Failed", "Failed", "reporting-fingerprint-v1",
                        OperationalWorkflowComparisonStatus.Failed,
                        "observer-invalid-evidence", Timestamp)
                ],
                "Failed", "Ready", "Read-only observation failed", "Not completed");
            surface.RenderLive(view);
            DataGridView grid = Descendants(surface).OfType<DataGridView>().Single(item =>
                item.AccessibleName == "Five Pilot workflow results");

            grid.CurrentCell = grid.Rows.Cast<DataGridViewRow>().Single(row =>
                row.Tag is LivePilotWorkflowView workflow &&
                workflow.Workflow == PilotValidationWorkflow.Reporting).Cells[0];

            Assert.Equal(grid.CurrentRow!.Cells[0].Value, surface.Snapshot.SelectedFeature);
            Assert.Equal("Failed", surface.Snapshot.ComparisonStatus);
            Assert.Equal("Available: observer-invalid-evidence",
                surface.Snapshot.EvidenceSummary);
            Assert.Equal("2026-08-22 19:30:45 UTC", surface.Snapshot.Timestamp);
            Assert.Contains("diagnostic details are intentionally not displayed",
                surface.Snapshot.Warnings.Single(), StringComparison.Ordinal);
            string rendered = string.Join('|', surface.Snapshot.SelectedFeature,
                surface.Snapshot.ComparisonStatus, surface.Snapshot.EvidenceSummary,
                surface.Snapshot.Timestamp, string.Join('|', surface.Snapshot.Warnings));
            Assert.DoesNotContain("exception", rendered, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("stack trace", rendered, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SELECT ", rendered, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Surface_consumes_a_defensive_immutable_state_snapshot()
    {
        var warnings = new List<string> { "A comparison difference requires review." };
        var blocked = new List<string> { "The pilot result was blocked by a safety rule." };
        PilotDashboardState state = State(PilotUiViewStatus.Blocked,
            ShadowDifferenceSeverity.Warning, warnings: warnings, blockedReasons: blocked);
        warnings.Add("late secret");
        blocked.Clear();

        RunSta(() =>
        {
            using var surface = new PilotDashboardControl();
            surface.ReplaceState(state);

            Assert.Equal(["A comparison difference requires review."], surface.Snapshot.Warnings);
            Assert.Equal(["The pilot result was blocked by a safety rule."],
                surface.Snapshot.BlockedReasons);
        });
    }

    [Fact]
    public void Hostile_text_is_replaced_and_never_rendered()
    {
        const string hostile = "password=secret SQL error C:\\production\\database.db stack trace";
        PilotDashboardState state = State(PilotUiViewStatus.Blocked,
            ShadowDifferenceSeverity.Failed, evidenceAvailable: true,
            comparison: hostile, evidenceReference: hostile, correlationId: hostile,
            warnings: [hostile], blockedReasons: [hostile]);

        RunSta(() =>
        {
            using var surface = new PilotDashboardControl();
            surface.ReplaceState(state);
            PilotSurfaceSnapshot rendered = surface.Snapshot;
            string allText = string.Join('|', rendered.PilotId, rendered.SelectedFeature,
                rendered.ExecutionState, rendered.ComparisonStatus, rendered.Severity,
                rendered.EvidenceSummary, rendered.RollbackSummary, rendered.CorrelationId,
                rendered.Timestamp, string.Join('|', rendered.Warnings),
                string.Join('|', rendered.BlockedReasons));

            Assert.DoesNotContain("password", allText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", allText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SQL", allText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("production", allText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("stack trace", allText, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Comparison details are unavailable.", rendered.ComparisonStatus);
            Assert.Equal("Available; reference unavailable", rendered.EvidenceSummary);
            Assert.True(rendered.UsesSafeFallback);
        });
    }

    [Fact]
    public void Blocked_state_displays_only_safe_blocked_reasons()
    {
        PilotDashboardState state = State(PilotUiViewStatus.Blocked,
            ShadowDifferenceSeverity.Failed,
            blockedReasons: ["Settings changes are prohibited in pilot observation."]);

        RunSta(() =>
        {
            using var surface = new PilotDashboardControl();
            surface.ReplaceState(state);

            Assert.Equal("Blocked", surface.Snapshot.ExecutionState);
            Assert.Equal(["Settings changes are prohibited in pilot observation."],
                surface.Snapshot.BlockedReasons);
        });
    }

    [Fact]
    public void Difference_state_is_visualized_without_switching_authority()
    {
        PilotDashboardState state = State(PilotUiViewStatus.DifferenceDetected,
            ShadowDifferenceSeverity.Warning,
            comparison: "A legacy and target difference was recorded for human review.",
            warnings: ["A comparison difference requires review."]);

        RunSta(() =>
        {
            using var surface = new PilotDashboardControl();
            surface.ReplaceState(state);

            Assert.Equal("Difference detected", surface.Snapshot.ExecutionState);
            Assert.Equal("Warning", surface.Snapshot.Severity);
            Assert.False(state.CanSwitchAuthority);
            Assert.False(state.CanActivateFeature);
            Assert.False(surface.ExecutesCommands);
        });
    }

    [Theory]
    [InlineData(ShadowDifferenceSeverity.None, "None")]
    [InlineData(ShadowDifferenceSeverity.Informational, "Informational")]
    [InlineData(ShadowDifferenceSeverity.Warning, "Warning")]
    [InlineData(ShadowDifferenceSeverity.Critical, "Critical")]
    [InlineData(ShadowDifferenceSeverity.Failed, "Unavailable")]
    [InlineData((ShadowDifferenceSeverity)999, "Unavailable")]
    public void Severity_is_mapped_to_fixed_display_text(
        ShadowDifferenceSeverity severity,
        string expected)
    {
        RunSta(() =>
        {
            using var display = new PilotSeverityDisplay();
            display.Render(severity);
            Assert.Equal(expected, display.DisplayedText);
        });
    }

    [Fact]
    public void Missing_evidence_uses_safe_fallback()
    {
        PilotDashboardState state = State(PilotUiViewStatus.Completed,
            ShadowDifferenceSeverity.None, evidenceAvailable: true, evidenceReference: null);

        RunSta(() =>
        {
            using var surface = new PilotDashboardControl();
            surface.ReplaceState(state);

            Assert.Equal("Available; reference unavailable", surface.Snapshot.EvidenceSummary);
            Assert.True(surface.Snapshot.UsesSafeFallback);
        });
    }

    [Fact]
    public void Unknown_status_uses_safe_fallback_without_throwing()
    {
        PilotDashboardState state = State((PilotUiViewStatus)999,
            ShadowDifferenceSeverity.None);

        RunSta(() =>
        {
            using var surface = new PilotDashboardControl();
            Exception? error = Record.Exception(() => surface.ReplaceState(state));

            Assert.Null(error);
            Assert.Equal("Unavailable", surface.Snapshot.ExecutionState);
            Assert.True(surface.Snapshot.UsesSafeFallback);
            Assert.False(surface.ExecutesCommands);
        });
    }

    [Fact]
    public void Refresh_replaces_displayed_state_and_clear_removes_it()
    {
        RunSta(() =>
        {
            using var surface = new PilotDashboardControl();
            surface.ReplaceState(State(PilotUiViewStatus.Completed,
                ShadowDifferenceSeverity.None));
            Assert.Equal("Completed", surface.Snapshot.ExecutionState);

            surface.ReplaceState(State(PilotUiViewStatus.Blocked,
                ShadowDifferenceSeverity.Failed));
            Assert.Equal("Blocked", surface.Snapshot.ExecutionState);
            Assert.True(surface.HasState);

            surface.ClearState();
            Assert.False(surface.HasState);
            Assert.Equal("No active pilot", surface.Snapshot.PilotId);
            Assert.Equal("No pilot result is displayed.", surface.Snapshot.ComparisonStatus);
        });
    }

    [Fact]
    public void Render_boundary_honors_cancellation_without_refresh_or_execution()
    {
        RunSta(() =>
        {
            using var surface = new PilotDashboardControl();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Exception? error = Record.Exception(() => surface.RenderAsync(
                State(PilotUiViewStatus.Completed, ShadowDifferenceSeverity.None),
                cancellation.Token).GetAwaiter().GetResult());
            Assert.Null(error);
            Assert.False(surface.HasState);
            Assert.False(surface.RequestsRefresh);
            Assert.False(surface.ExecutesCommands);
        });
    }

    [Fact]
    public void Disposed_surface_ignores_refresh_without_throwing_or_execution()
    {
        RunSta(() =>
        {
            var surface = new PilotDashboardControl();
            surface.Dispose();

            Exception? replaceError = Record.Exception(() => surface.ReplaceState(
                State(PilotUiViewStatus.Completed, ShadowDifferenceSeverity.None)));
            Exception? clearError = Record.Exception(surface.ClearState);

            Assert.Null(replaceError);
            Assert.Null(clearError);
            Assert.False(surface.ExecutesCommands);
            Assert.False(surface.RequestsRefresh);
        });
    }

    [Fact]
    public void Surface_contains_no_buttons_or_mutation_commands()
    {
        RunSta(() =>
        {
            using var surface = new PilotDashboardControl();
            Control[] controls = Descendants(surface).ToArray();
            string publicMethods = string.Join('|', typeof(PilotDashboardControl).GetMethods(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName).Select(method => method.Name));

            Assert.DoesNotContain(controls, control => control is ButtonBase);
            Assert.DoesNotContain("Activate", publicMethods, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Execute", publicMethods, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Save", publicMethods, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Update", publicMethods, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Login", publicMethods, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Surface_constructor_and_fields_have_no_application_service_dependency()
    {
        Type surface = typeof(PilotDashboardControl);
        Type[] constructorParameters = surface.GetConstructors().SelectMany(constructor =>
            constructor.GetParameters()).Select(parameter => parameter.ParameterType).ToArray();
        Type[] fieldTypes = surface.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Select(field => field.FieldType).ToArray();
        string dependencyNames = string.Join('|', constructorParameters.Concat(fieldTypes)
            .Select(type => type.FullName));

        Assert.Empty(constructorParameters);
        Assert.DoesNotContain("PilotHost", dependencyNames, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Coordinator", dependencyNames, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Repository", dependencyNames, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Connection", dependencyNames, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Migration", dependencyNames, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", dependencyNames, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Credential", dependencyNames, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IServiceProvider", dependencyNames, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Navigation_is_interface_only_and_not_a_production_form_reference()
    {
        Type[] types = SurfaceTypes();

        Assert.True(typeof(IPilotNavigationBoundary).IsInterface);
        Assert.DoesNotContain(types, type => typeof(IPilotNavigationBoundary).IsAssignableFrom(type) &&
            !type.IsInterface);
        Assert.DoesNotContain(types, type => type.IsSubclassOf(typeof(Form)));
        Assert.DoesNotContain(types, type => type.Namespace?.StartsWith("Rah_Negar.UI.Forms",
            StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Localization_DPI_and_keyboard_accessibility_foundations_are_present()
    {
        RunSta(() =>
        {
            using var surface = new PilotDashboardControl();

            Assert.True(typeof(IPilotLocalizedTextProvider).IsInterface);
            Assert.Equal(13, Enum.GetValues<PilotLocalizedTextKey>().Length);
            Assert.True(surface.Accessibility.DpiScalingRequired);
            Assert.True(surface.Accessibility.KeyboardNavigationRequired);
            Assert.True(surface.Accessibility.AccessibleNamesRequired);
            Assert.Equal(AutoScaleMode.Dpi, surface.AutoScaleMode);
            Assert.True(surface.TabStop);
            Assert.False(string.IsNullOrWhiteSpace(surface.AccessibleName));
            Assert.All(Descendants(surface).OfType<TextBox>(), textBox =>
            {
                Assert.True(textBox.ReadOnly);
                Assert.True(textBox.TabStop);
                Assert.False(string.IsNullOrWhiteSpace(textBox.AccessibleName));
            });
        });
    }

    [Fact]
    public void Surface_namespace_has_no_storage_migration_activation_RBAC_or_support_identity()
    {
        Type[] types = SurfaceTypes();
        string names = string.Join('|', types.Select(type => type.Name));
        string methods = string.Join('|', types.SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.DeclaredOnly))
            .Where(method => !method.IsSpecialName).Select(method => method.Name));

        Assert.DoesNotContain("Repository", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Connection", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Migration", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Activate", methods, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Migrate", methods, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Provision", methods, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Rbac", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupportRole", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupportProfile", names, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupportLogin", names, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_startup_forms_and_navigation_do_not_reference_the_pilot_surface()
    {
        string root = RepositoryRoot();
        string[] productionSources =
        [
            Path.Combine(root, "Program.cs"),
            .. Directory.GetFiles(Path.Combine(root, "UI", "Startup"), "*.cs",
                SearchOption.AllDirectories),
            .. Directory.GetFiles(Path.Combine(root, "UI", "Forms"), "*.cs",
                SearchOption.AllDirectories)
        ];
        string source = string.Join(Environment.NewLine,
            productionSources.Select(File.ReadAllText));

        Assert.DoesNotContain("Rah_Negar.UI.Pilot", source, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(PilotDashboardControl), source, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(IPilotNavigationBoundary), source, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(IPilotDashboardRefreshTarget), source, StringComparison.Ordinal);
    }

    private static PilotDashboardState State(
        PilotUiViewStatus status,
        ShadowDifferenceSeverity severity,
        bool evidenceAvailable = false,
        bool rollbackAvailable = false,
        string comparison = "Legacy and target observations matched.",
        string? evidenceReference = "evidence-surface-1",
        string correlationId = "correlation-surface-1",
        IEnumerable<string>? warnings = null,
        IEnumerable<string>? blockedReasons = null)
    {
        var featureState = new PilotFeatureViewState("pilot-surface-1",
            PilotFeature.ReportingPilot, "Reporting pilot", status,
            "Reporting pilot observation. Legacy remains authoritative.", severity,
            comparison, evidenceAvailable ? PilotEvidenceState.Complete : PilotEvidenceState.Incomplete,
            evidenceReference, Timestamp, warnings ?? Array.Empty<string>(),
            blockedReasons ?? Array.Empty<string>(), correlationId);
        return new("pilot-surface-1", PilotFeature.ReportingPilot, status, comparison,
            evidenceAvailable, rollbackAvailable, featureState);
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control descendant in Descendants(child)) yield return descendant;
        }
    }

    private static Type[] SurfaceTypes() => typeof(PilotDashboardControl).Assembly.GetTypes()
        .Where(type => type.Namespace == typeof(PilotDashboardControl).Namespace).ToArray();

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "Rah_Negar.csproj")))
            directory = directory.Parent;
        return directory?.FullName ??
            throw new DirectoryNotFoundException("The repository root could not be located.");
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

}
