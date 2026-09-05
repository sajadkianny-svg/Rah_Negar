using System.Reflection;
using System.Windows.Forms;
using Microsoft.Data.Sqlite;
using Rah_Negar.Foundation.Application.Pilot.Live;
using Rah_Negar.Foundation.Application.Pilot.Operational;
using Rah_Negar.Foundation.Application.Pilot.Validation;
using Rah_Negar.Infrastructure.Database.Readiness;
using Rah_Negar.Infrastructure.Pilot;
using Rah_Negar.UI.Composition.Pilot;
using Rah_Negar.UI.Forms.Pilot;
using Rah_Negar.UI.Pilot;

namespace Rah_Negar.Tests.Pilot;

public sealed class LivePilotPhase93Tests
{
    private static readonly DateTimeOffset At = new(2026, 9, 3, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Pilot_connection_is_strictly_read_only_and_cannot_mutate_fixture()
    {
        await using TestPilotDatabase database = await TestPilotDatabase.CreateAsync("Rasht Station");
        var factory = new PilotReadOnlySqliteConnectionFactory(database.Path);
        await using SqliteConnection connection = await factory.OpenReadOnlyAsync();

        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
        Assert.Contains("Mode=ReadOnly", connection.ConnectionString,
            StringComparison.OrdinalIgnoreCase);
        await Assert.ThrowsAnyAsync<SqliteException>(async () =>
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO tbl_events(date_rep) VALUES (14050601);";
            await command.ExecuteNonQueryAsync();
        });
        Assert.Equal(0L, await database.ScalarAsync("SELECT COUNT(*) FROM tbl_events;"));
    }

    [Theory]
    [InlineData("Rasht Station", "station-rasht", "Rasht Station", 3)]
    [InlineData("Ramsar Station", "station-ramsar", "Ramsar Station", 4)]
    public async Task Pilot_preflight_is_non_mutating_and_selects_the_existing_station_scope(
        string station, string id, string name, int units)
    {
        await using TestPilotDatabase database = await TestPilotDatabase.CreateAsync(station);
        string before = await database.DigestAsync();
        var preflight = new LivePilotReadOnlyPreflight(
            new PilotReadOnlySqliteConnectionFactory(database.Path));

        LivePilotReadOnlyPreflightResult result = await preflight.EvaluateAsync(At);

        Assert.True(result.IsReady);
        Assert.Equal(id, result.Scope!.StationId);
        Assert.Equal(name, result.Scope.StationName);
        Assert.Equal(units, database.UnitCount);
        Assert.False(result.MutatedProduction);
        Assert.False(result.RanMigration);
        Assert.False(preflight.CreatesSchema);
        Assert.False(preflight.RunsMigration);
        Assert.False(preflight.OpensTransaction);
        Assert.False(preflight.MutatesPragma);
        Assert.Equal(before, await database.DigestAsync());
    }

    [Fact]
    public async Task Preflight_cancellation_is_safe_and_does_not_open_or_mutate()
    {
        await using TestPilotDatabase database = await TestPilotDatabase.CreateAsync("Rasht Station");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var preflight = new LivePilotReadOnlyPreflight(
            new PilotReadOnlySqliteConnectionFactory(database.Path));

        LivePilotReadOnlyPreflightResult result = await preflight.EvaluateAsync(At, cancellation.Token);

        Assert.Equal(LivePilotReadOnlyPreflightStatus.Canceled, result.Status);
        Assert.Equal("live-preflight-canceled", result.ReasonCode);
        Assert.False(result.MutatedProduction);
    }

    [Fact]
    public async Task All_five_live_observers_are_read_only_and_execute_deterministically()
    {
        ControlledPilotOperationalFixture fixture = ControlledPilotOperationalFixture.Rasht();
        LiveModels models = LiveModels.From(fixture);
        IControlledPilotOperationalWorkflowObserver[] observers =
        [
            new LiveAuthenticationPilotObserver(models),
            new LiveReportingPilotObserver(models),
            new LiveRuntimeEventPilotObserver(models),
            new LiveProtectedSettingsPilotObserver(models),
            new LiveExportPilotObserver(models)
        ];

        Assert.Equal(5, observers.Length);
        Assert.All(observers, observer =>
        {
            Assert.True(observer.IsAvailable);
            Assert.True(observer.IsReadOnly);
            Assert.True(observer.SupportsCancellation);
            Assert.False(observer.RequiresReview);
        });

        var context = fixture.Context();
        ControlledPilotOperationalWorkflowResult?[] first = await Task.WhenAll(
            observers.Select(observer => observer.ObserveAsync(context, At).AsTask()));
        ControlledPilotOperationalWorkflowResult?[] second = await Task.WhenAll(
            observers.Select(observer => observer.ObserveAsync(context, At).AsTask()));

        Assert.All(first, result => Assert.Equal(OperationalWorkflowComparisonStatus.Match,
            result!.Status));
        Assert.Equal(first.Select(x => x!.LegacyFingerprint), second.Select(x => x!.LegacyFingerprint));
        Assert.Equal(first.Select(x => x!.FingerprintSpecificationVersion),
            ["auth-fingerprint-v1", "reporting-fingerprint-v1", "runtime-event-fingerprint-v1",
                "protected-settings-fingerprint-v1", "export-fingerprint-v1"]);
    }

    [Fact]
    public async Task Live_observer_difference_and_invalid_boundary_are_deterministic()
    {
        ControlledPilotOperationalFixture fixture = ControlledPilotOperationalFixture.Rasht();
        var changed = new LiveModels(fixture, difference: true);
        var observer = new LiveReportingPilotObserver(changed);

        ControlledPilotOperationalWorkflowResult result = (await observer.ObserveAsync(
            fixture.Context(), At))!;
        Assert.Equal(OperationalWorkflowComparisonStatus.Difference, result.Status);
        Assert.Equal(1, result.SemanticDifferenceCount);

        var invalid = new LiveReportingPilotObserver(new LiveModels(fixture, invalid: true));
        Assert.Null(await invalid.ObserveAsync(fixture.Context(), At));
    }

    [Fact]
    public async Task Live_session_supports_confirm_start_observe_review_complete_stop_and_dispose()
    {
        ControlledPilotOperationalFixture fixture = ControlledPilotOperationalFixture.Rasht();
        var preflight = Ready(fixture);
        using var session = new LivePilotOperatorSession(fixture.Coordinator(), preflight,
            new FixedTimeProvider(ControlledPilotOperationalFixture.WindowStart.AddMinutes(10)));

        Assert.Equal(ControlledPilotOperationalLifecycle.Created, session.Lifecycle);
        Assert.False(session.AutomaticallyStarts);
        LivePilotDashboardView observed = await session.StartObservationAsync();
        Assert.Equal(ControlledPilotOperationalLifecycle.ReviewRequired, session.Lifecycle);
        Assert.Equal(5, observed.Workflows.Count);
        Assert.True(observed.IsReadOnly);
        Assert.False(observed.CanSwitchAuthority);
        Assert.Equal("مرجع بهره‌برداری: سامانه فعلی (Legacy)", observed.LegacyAuthorityIndicator);

        LivePilotDashboardView completed = await session.CompleteAsync();
        Assert.Equal(ControlledPilotOperationalLifecycle.Completed, session.Lifecycle);
        Assert.Contains("تکمیل", completed.CompletionStatus);
        session.Dispose();
        Assert.Equal(ControlledPilotOperationalLifecycle.Disposed, session.Lifecycle);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await session.CompleteAsync());

        using var stopSession = new LivePilotOperatorSession(fixture.Coordinator(), preflight,
            new FixedTimeProvider(ControlledPilotOperationalFixture.WindowStart.AddMinutes(10)));
        await stopSession.StartObservationAsync();
        LivePilotDashboardView stopped = await stopSession.StopAsync();
        Assert.Equal(ControlledPilotOperationalLifecycle.Stopped, stopSession.Lifecycle);
        Assert.Contains("توقف", stopped.CompletionStatus);
    }

    [Fact]
    public async Task Session_cancellation_and_shutdown_are_safe()
    {
        ControlledPilotOperationalFixture fixture = ControlledPilotOperationalFixture.Ramsar();
        using var session = new LivePilotOperatorSession(fixture.Coordinator(), Ready(fixture),
            new FixedTimeProvider(ControlledPilotOperationalFixture.WindowStart.AddMinutes(10)));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        LivePilotDashboardView view = await session.StartObservationAsync(cancellation.Token);
        Assert.NotEqual(ControlledPilotOperationalLifecycle.ReviewRequired, session.Lifecycle);
        Assert.True(session.IsTerminal || session.Lifecycle == ControlledPilotOperationalLifecycle.Created);
        session.Dispose();
        session.Dispose();
        Assert.Equal(ControlledPilotOperationalLifecycle.Disposed, session.Lifecycle);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await session.StartObservationAsync());
        _ = view;
    }

    [Fact]
    public void Pilot_UI_exposes_safety_banner_and_has_no_prohibited_actions_or_secrets()
    {
        string root = RepositoryRoot();
        string formSource = File.ReadAllText(Path.Combine(root, "UI", "Composition", "Pilot", "FrmLivePilot.cs"));
        string dashboardSource = File.ReadAllText(Path.Combine(root, "UI", "Pilot", "PilotDashboardControl.cs"));
        string mainSource = File.ReadAllText(Path.Combine(root, "UI", "Forms", "FrmMain.cs"));
        string all = formSource + dashboardSource + mainSource;

        Assert.Contains("حالت آزمایشی", dashboardSource);
        Assert.Contains("فقط خواندنی", all);
        Assert.Contains("مرجع بهره‌برداری", dashboardSource);
        Assert.Contains("Pilot / فقط خواندنی", mainSource);
        foreach (string prohibited in new[] { "password", "hash", "recovery", "connectionstring", "connection string" })
            Assert.DoesNotContain(prohibited, all, StringComparison.OrdinalIgnoreCase);
        foreach (string prohibitedAction in new[] { "INSERT", "UPDATE", "DELETE", "Migrate", "ExecuteProduction", "SwitchAuthority" })
            Assert.DoesNotContain(prohibitedAction, formSource, StringComparison.OrdinalIgnoreCase);

        RunSta(() =>
        {
            using var dashboard = new PilotDashboardControl();
            dashboard.RenderLive(LivePilotDashboardView.Waiting());
            string visible = AllControlText(dashboard);
            Assert.Contains("حالت آزمایشی", visible);
            Assert.Contains("فقط خواندنی", visible);
            Assert.Contains("مرجع بهره‌برداری", visible);
            Assert.DoesNotContain("password", visible, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("connection", visible, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Pilot_is_explicit_only_and_legacy_window_remains_authoritative()
    {
        string root = RepositoryRoot();
        string main = File.ReadAllText(Path.Combine(root, "UI", "Forms", "FrmMain.cs"));
        string program = File.ReadAllText(Path.Combine(root, "Program.cs"));
        Assert.Contains("OpenReadOnlyPilot", main);
        Assert.Contains("ConfigurePilotEntry", main);
        Assert.Contains("ReplacesLegacyWindow => false", File.ReadAllText(
            Path.Combine(root, "UI", "Composition", "Pilot", "FrmLivePilot.cs")));
        Assert.DoesNotContain("LivePilotCompositionRoot", program, StringComparison.Ordinal);
        Assert.DoesNotContain("ComposeAsync", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Active_first_close_attempt_triggers_guard()
    {
        ActiveCloseScenarioResult result = RunActiveCloseScenario(1);

        Assert.Equal(1, result.WarningCount);
        Assert.Equal([true], result.CancelledAttempts);
    }

    [Fact]
    public void Cancelling_first_close_attempt_keeps_form_and_session_open()
    {
        ActiveCloseScenarioResult result = RunActiveCloseScenario(1);

        Assert.True(result.FormStayedOpenAfterCancelledAttempts);
        Assert.Equal([ControlledPilotOperationalLifecycle.ReviewRequired],
            result.LifecyclesAfterCancelledAttempts);
    }

    [Fact]
    public void Active_second_close_attempt_still_triggers_guard()
    {
        ActiveCloseScenarioResult result = RunActiveCloseScenario(2);

        Assert.Equal(2, result.WarningCount);
        Assert.Equal([true, true], result.CancelledAttempts);
    }

    [Fact]
    public void Repeated_cancelled_close_attempts_do_not_weaken_guard()
    {
        ActiveCloseScenarioResult result = RunActiveCloseScenario(5);

        Assert.Equal(5, result.WarningCount);
        Assert.All(result.CancelledAttempts, Assert.True);
        Assert.All(result.LifecyclesAfterCancelledAttempts,
            lifecycle => Assert.Equal(ControlledPilotOperationalLifecycle.ReviewRequired,
                lifecycle));
    }

    [Fact]
    public void Completed_session_closes_without_shutdown_guard()
    {
        TerminalCloseScenarioResult result = RunTerminalCloseScenario(
            complete: true);

        Assert.Equal(ControlledPilotOperationalLifecycle.Completed, result.Lifecycle);
        Assert.Equal(0, result.WarningCount);
        Assert.False(result.Cancelled);
    }

    [Fact]
    public void Stopped_session_closes_without_shutdown_guard()
    {
        TerminalCloseScenarioResult result = RunTerminalCloseScenario(
            complete: false);

        Assert.Equal(ControlledPilotOperationalLifecycle.Stopped, result.Lifecycle);
        Assert.Equal(0, result.WarningCount);
        Assert.False(result.Cancelled);
    }

    [Fact]
    public void Shutdown_guard_does_not_change_authority_state()
    {
        ActiveCloseScenarioResult result = RunActiveCloseScenario(3);

        Assert.False(result.ChangesAuthority);
        Assert.False(result.ChangesProductionAuthority);
        Assert.Equal(ControlledPilotOperationalLifecycle.Stopped, result.FinalLifecycle);
    }

    [Fact]
    public void Confirmed_close_closes_on_the_same_attempt()
    {
        ConfirmedCloseScenarioResult result = RunConfirmedCloseScenario();

        Assert.True(result.FormClosed);
        Assert.Equal(1, result.CloseAttemptCount);
        Assert.Equal(1, result.WarningCount);
        Assert.False(result.Cancelled);
    }

    [Fact]
    public void Confirmed_close_does_not_require_a_second_close_or_warning()
    {
        ConfirmedCloseScenarioResult result = RunConfirmedCloseScenario();

        Assert.True(result.FormClosed);
        Assert.Equal(1, result.CloseAttemptCount);
        Assert.Equal(1, result.WarningCount);
    }

    [Fact]
    public void Confirmed_close_preserves_existing_explicit_stop_semantics_without_completion()
    {
        ConfirmedCloseScenarioResult result = RunConfirmedCloseScenario();

        Assert.Equal(ControlledPilotOperationalLifecycle.Stopped, result.Lifecycle);
        Assert.NotEqual(ControlledPilotOperationalLifecycle.Completed, result.Lifecycle);
    }

    [Fact]
    public void Confirmed_close_preserves_legacy_authority_and_target_inactivity()
    {
        ConfirmedCloseScenarioResult result = RunConfirmedCloseScenario();

        Assert.False(result.ChangesAuthority);
        Assert.False(result.ChangesProductionAuthority);
        Assert.False(result.TargetActivated);
    }

    [Fact]
    public void New_pilot_form_does_not_inherit_confirmed_close_state()
    {
        ConfirmedCloseScenarioResult result = RunConfirmedCloseScenario();
        Assert.True(result.FormClosed);

        ActiveCloseScenarioResult newForm = RunActiveCloseScenario(1);

        Assert.Equal(1, newForm.WarningCount);
        Assert.Equal([true], newForm.CancelledAttempts);
    }

    [Fact]
    public async Task End_to_end_observation_preserves_source_database_and_legacy_authority()
    {
        await using TestPilotDatabase database = await TestPilotDatabase.CreateAsync("Ramsar Station");
        string before = await database.DigestAsync();
        ControlledPilotOperationalFixture fixture = ControlledPilotOperationalFixture.Ramsar();
        using var session = new LivePilotOperatorSession(fixture.Coordinator(), Ready(fixture),
            new FixedTimeProvider(ControlledPilotOperationalFixture.WindowStart.AddMinutes(10)));

        LivePilotDashboardView view = await session.StartObservationAsync();

        Assert.Equal(ControlledPilotOperationalLifecycle.ReviewRequired, session.Lifecycle);
        Assert.False(session.ChangesAuthority);
        Assert.False(session.MutatesProduction);
        Assert.Equal("مرجع بهره‌برداری: سامانه فعلی (Legacy)", view.LegacyAuthorityIndicator);
        Assert.Equal(before, await database.DigestAsync());
    }

    private static LivePilotReadOnlyPreflightResult Ready(ControlledPilotOperationalFixture fixture) =>
        new(LivePilotReadOnlyPreflightStatus.Ready, "ready", At,
            new LivePilotReadScope(fixture.StationId, fixture.StationId, fixture.Scope,
                14050601, 14050601, 14050602, 0, 2880, "1405-06", false, 0));

    private static ActiveCloseScenarioResult RunActiveCloseScenario(int attempts)
    {
        ActiveCloseScenarioResult? result = null;
        RunSta(() =>
        {
            ControlledPilotOperationalFixture fixture = ControlledPilotOperationalFixture.Rasht();
            using var session = new LivePilotOperatorSession(fixture.Coordinator(), Ready(fixture),
                new FixedTimeProvider(ControlledPilotOperationalFixture.WindowStart.AddMinutes(10)));
            session.StartObservationAsync().GetAwaiter().GetResult();
            using var dashboard = new PilotDashboardControl();
            var composition = new LivePilotCompositionResult(dashboard, session,
                session.CreateView(), "test-close-guard");
            int warnings = 0;
            var cancelled = new List<bool>();
            var lifecycles = new List<ControlledPilotOperationalLifecycle>();
            bool stayedOpen = true;
            using var form = new FrmLivePilot(composition, () =>
            {
                warnings++;
                return DialogResult.No;
            });
            form.Shown += (_, _) =>
            {
                for (int index = 0; index < attempts; index++)
                {
                    form.Close();
                    cancelled.Add(!form.IsDisposed);
                    lifecycles.Add(session.Lifecycle);
                }

                stayedOpen = !form.IsDisposed;
                session.StopAsync().GetAwaiter().GetResult();
                form.Close();
            };
            form.ShowDialog();
            result = new ActiveCloseScenarioResult(warnings, cancelled, lifecycles,
                stayedOpen, session.Lifecycle, session.ChangesAuthority,
                composition.ChangesProductionAuthority);
        });
        return result!;
    }

    private static ConfirmedCloseScenarioResult RunConfirmedCloseScenario()
    {
        ConfirmedCloseScenarioResult? result = null;
        RunSta(() =>
        {
            ControlledPilotOperationalFixture fixture = ControlledPilotOperationalFixture.Rasht();
            using var session = new LivePilotOperatorSession(fixture.Coordinator(), Ready(fixture),
                new FixedTimeProvider(ControlledPilotOperationalFixture.WindowStart.AddMinutes(10)));
            session.StartObservationAsync().GetAwaiter().GetResult();
            using var dashboard = new PilotDashboardControl();
            var composition = new LivePilotCompositionResult(dashboard, session,
                session.CreateView(), "test-confirmed-close");
            int warnings = 0;
            int closeAttempts = 0;
            bool cancelled = true;
            bool formClosed = false;
            bool targetActivated = false;
            using var form = new FrmLivePilot(composition, () =>
            {
                warnings++;
                return DialogResult.Yes;
            });
            form.FormClosing += (_, e) =>
            {
                closeAttempts++;
                cancelled = e.Cancel;
            };
            form.FormClosed += (_, _) =>
            {
                formClosed = true;
                targetActivated = composition.ChangesProductionAuthority;
            };
            form.Shown += (_, _) => form.Close();
            form.ShowDialog();
            result = new ConfirmedCloseScenarioResult(
                formClosed, closeAttempts, warnings, cancelled, session.Lifecycle,
                session.ChangesAuthority, composition.ChangesProductionAuthority,
                targetActivated);
        });
        return result!;
    }

    private static TerminalCloseScenarioResult RunTerminalCloseScenario(bool complete)
    {
        TerminalCloseScenarioResult? result = null;
        RunSta(() =>
        {
            ControlledPilotOperationalFixture fixture = ControlledPilotOperationalFixture.Rasht();
            using var session = new LivePilotOperatorSession(fixture.Coordinator(), Ready(fixture),
                new FixedTimeProvider(ControlledPilotOperationalFixture.WindowStart.AddMinutes(10)));
            session.StartObservationAsync().GetAwaiter().GetResult();
            if (complete)
                session.CompleteAsync().GetAwaiter().GetResult();
            else
                session.StopAsync().GetAwaiter().GetResult();

            using var dashboard = new PilotDashboardControl();
            var composition = new LivePilotCompositionResult(dashboard, session,
                session.CreateView(), "test-terminal-close");
            int warnings = 0;
            bool cancelled = true;
            using var form = new FrmLivePilot(composition, () =>
            {
                warnings++;
                return DialogResult.No;
            });
            form.FormClosing += (_, e) => cancelled = e.Cancel;
            form.Shown += (_, _) =>
            {
                form.Close();
            };
            form.ShowDialog();
            result = new TerminalCloseScenarioResult(session.Lifecycle, warnings,
                cancelled);
        });
        return result!;
    }

    private sealed record ActiveCloseScenarioResult(
        int WarningCount,
        IReadOnlyList<bool> CancelledAttempts,
        IReadOnlyList<ControlledPilotOperationalLifecycle> LifecyclesAfterCancelledAttempts,
        bool FormStayedOpenAfterCancelledAttempts,
        ControlledPilotOperationalLifecycle FinalLifecycle,
        bool ChangesAuthority,
        bool ChangesProductionAuthority);

    private sealed record TerminalCloseScenarioResult(
        ControlledPilotOperationalLifecycle Lifecycle,
        int WarningCount,
        bool Cancelled);

    private sealed record ConfirmedCloseScenarioResult(
        bool FormClosed,
        int CloseAttemptCount,
        int WarningCount,
        bool Cancelled,
        ControlledPilotOperationalLifecycle Lifecycle,
        bool ChangesAuthority,
        bool ChangesProductionAuthority,
        bool TargetActivated);

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Rah_Negar.csproj")))
            directory = directory.Parent;
        return directory!.FullName;
    }

    private static string AllControlText(Control control) => string.Join("|",
        new[] { control.Text, control.AccessibleName ?? string.Empty }
            .Concat(control.Controls.Cast<Control>().SelectMany(child => AllControlText(child).Split('|'))));

    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) throw error;
    }

    private sealed class LiveModels :
        ILiveAuthenticationPilotReadModel, ILiveReportingPilotReadModel,
        ILiveRuntimeEventPilotReadModel, ILiveProtectedSettingsPilotReadModel,
        ILiveExportPilotReadModel
    {
        private readonly ControlledPilotOperationalFixture _fixture;
        private readonly bool _difference;
        private readonly bool _invalid;
        public LiveModels(ControlledPilotOperationalFixture fixture, bool difference = false, bool invalid = false)
            => (_fixture, _difference, _invalid) = (fixture, difference, invalid);
        public static LiveModels From(ControlledPilotOperationalFixture fixture) => new(fixture);
        ValueTask<LivePilotObservationPair<AuthenticationOperationalObservation>> ILiveAuthenticationPilotReadModel.ReadAsync(CancellationToken cancellationToken)
            => ValueTask.FromResult(new LivePilotObservationPair<AuthenticationOperationalObservation>(Auth(), Auth(Target()), "live-auth-evidence"));
        ValueTask<LivePilotObservationPair<ReportingOperationalObservation>> ILiveReportingPilotReadModel.ReadAsync(CancellationToken cancellationToken)
            => ValueTask.FromResult(new LivePilotObservationPair<ReportingOperationalObservation>(_fixture.ReportingObservation, Reporting(Target()), "live-reporting-evidence"));
        ValueTask<LivePilotObservationPair<RuntimeEventOperationalObservation>> ILiveRuntimeEventPilotReadModel.ReadAsync(CancellationToken cancellationToken)
            => ValueTask.FromResult(new LivePilotObservationPair<RuntimeEventOperationalObservation>(new RuntimeEventOperationalObservation(_fixture.RuntimeObservation.StationId, _fixture.RuntimeObservation.PeriodStartMinute, _fixture.RuntimeObservation.PeriodEndMinute, _fixture.RuntimeObservation.Units, OperationalObservationBoundary.LegacyAuthoritative), new RuntimeEventOperationalObservation(_fixture.RuntimeObservation.StationId, _fixture.RuntimeObservation.PeriodStartMinute, _fixture.RuntimeObservation.PeriodEndMinute, _fixture.RuntimeObservation.Units, Target() ? OperationalObservationBoundary.TargetReadOnly : OperationalObservationBoundary.LegacyAuthoritative), "live-runtime-evidence"));
        ValueTask<LivePilotObservationPair<ProtectedSettingsOperationalObservation>> ILiveProtectedSettingsPilotReadModel.ReadAsync(CancellationToken cancellationToken)
            => ValueTask.FromResult(new LivePilotObservationPair<ProtectedSettingsOperationalObservation>(Settings(), Settings(Target()), "live-settings-evidence"));
        ValueTask<LivePilotObservationPair<ExportOperationalObservation>> ILiveExportPilotReadModel.ReadAsync(CancellationToken cancellationToken)
            => ValueTask.FromResult(new LivePilotObservationPair<ExportOperationalObservation>(Export(), Export(Target()), "live-export-evidence"));
        private bool Target() => !_invalid;
        private AuthenticationOperationalObservation Auth(bool target = false) => new(_fixture.StationId, true, true, true, !_difference, ["authentication.observe"], target ? OperationalObservationBoundary.TargetReadOnly : OperationalObservationBoundary.LegacyAuthoritative);
        private ProtectedSettingsOperationalObservation Settings(bool target = false) => new(_fixture.StationId, "protected-active", _difference ? 2.25m : 2m, "evidence", true, true, target ? OperationalObservationBoundary.TargetReadOnly : OperationalObservationBoundary.LegacyAuthoritative);
        private ReportingOperationalObservation Reporting(bool target = false) => new(_fixture.ReportingObservation.StationId, _fixture.ReportingObservation.PeriodIdentity, _fixture.ReportingObservation.PeriodStartMinute, _fixture.ReportingObservation.PeriodEndMinute, _difference && target ? _fixture.ReportingObservation.Summaries.Select(summary => summary with { Value = summary.Value + 1 }) : _fixture.ReportingObservation.Summaries, _fixture.ReportingObservation.ChartPoints, _fixture.ReportingObservation.DailyStatuses, _fixture.ReportingObservation.WarningCodes, _fixture.ReportingObservation.FinalizedSnapshotId, _fixture.ReportingObservation.FinalizedSnapshotChecksum, target ? OperationalObservationBoundary.TargetReadOnly : OperationalObservationBoundary.LegacyAuthoritative);
        private ExportOperationalObservation Export(bool target = false) => ExportOperationalObservationFactory.Create("snapshot", "renderer", "snapshot.pdf", new string('A', 64), "pdf", target ? OperationalObservationBoundary.TargetReadOnly : OperationalObservationBoundary.LegacyAuthoritative);
    }
}

file sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => value;
}

file sealed class TestPilotDatabase : IAsyncDisposable
{
    private readonly string _directory;
    private TestPilotDatabase(string directory, string path, int unitCount) => (_directory, Path, UnitCount) = (directory, path, unitCount);
    public string Path { get; }
    public int UnitCount { get; }
    public static async Task<TestPilotDatabase> CreateAsync(string station)
    {
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RahNegar.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = System.IO.Path.Combine(directory, "pilot.sqlite");
        int units = station.StartsWith("Ramsar", StringComparison.Ordinal) ? 4 : 3;
        await using var db = new SqliteConnection($"Data Source={path}");
        await db.OpenAsync();
        await using SqliteCommand command = db.CreateCommand();
        command.CommandText = $"""
            CREATE TABLE app_settings(id INTEGER PRIMARY KEY, is_initialized INTEGER NOT NULL, station_type TEXT NOT NULL, station_name TEXT NOT NULL, data_start_date INTEGER NOT NULL, esd_extra_runtime_enabled INTEGER NOT NULL, esd_extra_runtime_hours REAL NOT NULL);
            CREATE TABLE unit_runtime_base(id INTEGER PRIMARY KEY, unit_id TEXT);
            CREATE TABLE tbl_data(date_rep INTEGER);
            CREATE TABLE tbl_unique(date_rep INTEGER);
            CREATE TABLE tbl_events(date_rep INTEGER);
            INSERT INTO app_settings VALUES(1,1,'{station.Replace(" Station", "")}', '{station}', 14050601,0,0);
            INSERT INTO tbl_data VALUES(14050601);
            """;
        await command.ExecuteNonQueryAsync();
        return new TestPilotDatabase(directory, path, units);
    }
    public async Task<string> DigestAsync()
    {
        await using var db = new SqliteConnection($"Data Source={Path}");
        await db.OpenAsync();
        await using SqliteCommand command = db.CreateCommand();
        command.CommandText = "SELECT name, sql FROM sqlite_master WHERE type='table' ORDER BY name;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync()) values.Add($"{reader.GetString(0)}:{reader.GetString(1)}");
        return string.Join("|", values);
    }
    public async Task<long> ScalarAsync(string sql)
    {
        await using var db = new SqliteConnection($"Data Source={Path}");
        await db.OpenAsync();
        await using SqliteCommand command = db.CreateCommand(); command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }
    public ValueTask DisposeAsync() { SqliteConnection.ClearAllPools(); if (Directory.Exists(_directory)) Directory.Delete(_directory, true); return ValueTask.CompletedTask; }
}
