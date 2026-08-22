using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Windows.Forms;
using Rah_Negar.Foundation.Application.Integration;
using Rah_Negar.Foundation.Application.Pilot;
using Rah_Negar.Foundation.Application.Pilot.Presentation;
using Rah_Negar.UI.Pilot;

namespace Rah_Negar.Tests.UI;

public sealed class PilotDashboardHardeningTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 8, 22, 20, 15, 0, TimeSpan.Zero);

    public static IEnumerable<object[]> HostileTextCases()
    {
        yield return [new string('x', 513)];
        yield return ["unsafe\u0001control"];
        yield return [@"C:\production\pilot.db"];
        yield return ["SELECT password FROM operators"];
        yield return ["DROP TABLE pilot_evidence"];
        yield return ["System.InvalidOperationException: stack trace"];
        yield return ["credential password=secret private key"];
    }

    public static IEnumerable<object[]> UnsafeIdentifierCases()
    {
        yield return ["contains spaces"];
        yield return [@"C:\evidence\pilot.db"];
        yield return ["pilot/relative/path"];
        yield return ["pilot\nnewline"];
        yield return [new string('a', 129)];
        yield return ["password=secret"];
    }

    [Fact]
    public void Primary_and_fallback_visual_failures_are_both_non_throwing_and_sanitized()
    {
        RunSta(() =>
        {
            using var surface = new PilotDashboardControl();
            TextBox pilotId = Fields(surface).Single(field =>
                field.AccessibleName == "Current pilot ID");
            pilotId.TextChanged += (_, _) =>
                throw new InvalidOperationException("password=secret SQL C:\\production.db");

            Exception? error = Record.Exception(() => surface.ReplaceState(State()));

            Assert.Null(error);
            Assert.False(surface.HasState);
            Assert.True(surface.Snapshot.UsesSafeFallback);
            Assert.Equal("Pilot unavailable", surface.Snapshot.PilotId);
            Assert.DoesNotContain("password", SnapshotText(surface),
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SQL", SnapshotText(surface),
                StringComparison.OrdinalIgnoreCase);
            Assert.False(surface.ExecutesCommands);
        });
    }

    [Fact]
    public void Empty_clear_and_fallback_visual_failures_never_escape()
    {
        RunSta(() =>
        {
            using var surface = new PilotDashboardControl();
            surface.ReplaceState(State());
            TextBox pilotId = Fields(surface).Single(field =>
                field.AccessibleName == "Current pilot ID");
            pilotId.TextChanged += (_, _) => throw new InvalidOperationException("raw exception");

            Exception? clearError = Record.Exception(surface.ClearState);
            Exception? fallbackError = Record.Exception(() => surface.ReplaceState(null));

            Assert.Null(clearError);
            Assert.Null(fallbackError);
            Assert.False(surface.HasState);
            Assert.True(surface.Snapshot.UsesSafeFallback);
            Assert.False(surface.RequestsRefresh);
        });
    }

    [Theory]
    [MemberData(nameof(HostileTextCases))]
    public void Hostile_dynamic_text_maps_to_fixed_safe_fallbacks(string hostile)
    {
        RunSta(() =>
        {
            using var surface = new PilotDashboardControl();
            surface.ReplaceState(State(comparison: hostile, warnings: [hostile],
                blockedReasons: [hostile]));

            Assert.Equal("Comparison details are unavailable.",
                surface.Snapshot.ComparisonStatus);
            Assert.Contains("Warning details are unavailable.", surface.Snapshot.Warnings);
            Assert.Contains("A safety condition blocked the pilot result.",
                surface.Snapshot.BlockedReasons);
            Assert.True(surface.Snapshot.UsesSafeFallback);
            Assert.DoesNotContain(hostile, SnapshotText(surface), StringComparison.Ordinal);
        });
    }

    [Theory]
    [MemberData(nameof(UnsafeIdentifierCases))]
    public void Unsafe_identifiers_and_evidence_references_are_withheld(string hostile)
    {
        RunSta(() =>
        {
            using var surface = new PilotDashboardControl();
            surface.ReplaceState(State(pilotId: hostile, correlationId: hostile,
                evidenceReference: hostile, evidenceAvailable: true));

            Assert.Equal("No active pilot", surface.Snapshot.PilotId);
            Assert.Equal("Correlation unavailable", surface.Snapshot.CorrelationId);
            Assert.Equal("Available; reference unavailable", surface.Snapshot.EvidenceSummary);
            Assert.True(surface.Snapshot.UsesSafeFallback);
            Assert.DoesNotContain(hostile, SnapshotText(surface), StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Layout_contract_supports_DPI_minimum_dimensions_and_resilient_creation()
    {
        RunSta(() =>
        {
            using var surface = new PilotDashboardControl();
            PilotLayoutRequirements contract = surface.LayoutContract;

            Assert.Equal(AutoScaleMode.Dpi, surface.AutoScaleMode);
            Assert.True(contract.DpiScalingRequired);
            Assert.True(contract.AutoScrollRequired);
            Assert.True(contract.ResponsiveLayoutRequired);
            Assert.Equal(new Size(contract.MinimumSupportedWidth,
                contract.MinimumSupportedHeight), surface.MinimumSize);
            Assert.True(contract.MinimumSupportedWidth >= 720);
            Assert.True(contract.MinimumSupportedHeight >= 560);
            Assert.True(surface.AutoScroll);

            Exception? minimumError = Record.Exception(() =>
            {
                surface.Size = surface.MinimumSize;
                surface.CreateControl();
                surface.PerformLayout();
            });
            Exception? constrainedError = Record.Exception(() =>
            {
                surface.Size = new Size(400, 300);
                surface.PerformLayout();
            });

            Assert.Null(minimumError);
            Assert.Null(constrainedError);
        });
    }

    [Fact]
    public void Accessibility_contract_keeps_read_only_fields_focusable_and_prohibits_activation()
    {
        RunSta(() =>
        {
            using var surface = new PilotDashboardControl();
            PilotAccessibilityRequirements contract = surface.Accessibility;
            Control[] controls = Descendants(surface).ToArray();

            Assert.True(contract.DpiScalingRequired);
            Assert.True(contract.KeyboardNavigationRequired);
            Assert.True(contract.AccessibleNamesRequired);
            Assert.True(contract.ReadOnlyControlsFocusableRequired);
            Assert.True(contract.ActivationControlsProhibited);
            Assert.All(controls.OfType<TextBox>(), field =>
            {
                Assert.True(field.ReadOnly);
                Assert.True(field.TabStop);
                Assert.False(string.IsNullOrWhiteSpace(field.AccessibleName));
            });
            Assert.DoesNotContain(controls, control => control is ButtonBase);
            Assert.DoesNotContain(controls, control => control is LinkLabel);
        });
    }

    [Fact]
    public void Localization_boundary_has_fixed_keys_and_safe_missing_resource_fallbacks()
    {
        var fallback = new PilotLocalizationBoundary();
        var missing = new PilotLocalizationBoundary(new FixedTextProvider(null));
        var throwing = new PilotLocalizationBoundary(new ThrowingTextProvider());
        var unsafeProvider = new PilotLocalizationBoundary(
            new FixedTextProvider("SELECT password FROM C:\\production.db"));
        var oversized = new PilotLocalizationBoundary(
            new FixedTextProvider(new string('x', 513)));

        Assert.Equal(13, Enum.GetValues<PilotLocalizedTextKey>().Length);
        Assert.All(Enum.GetValues<PilotLocalizedTextKey>(), key =>
            Assert.False(string.IsNullOrWhiteSpace(fallback.GetText(key))));
        Assert.Equal("Current pilot ID", missing.GetText(PilotLocalizedTextKey.PilotId));
        Assert.Equal("Selected feature", throwing.GetText(PilotLocalizedTextKey.SelectedFeature));
        Assert.Equal("Warnings", unsafeProvider.GetText(PilotLocalizedTextKey.Warnings));
        Assert.Equal("Blocked reasons", oversized.GetText(PilotLocalizedTextKey.BlockedReasons));
        Assert.Equal("Unavailable", fallback.GetText((PilotLocalizedTextKey)999));
    }

    [Fact]
    public void Dynamic_state_cannot_replace_fixed_control_labels()
    {
        RunSta(() =>
        {
            using var surface = new PilotDashboardControl();
            string[] before = Descendants(surface).OfType<Label>().Select(label => label.Text).ToArray();

            surface.ReplaceState(State(comparison: "credential password=secret",
                warnings: ["DROP TABLE labels"], blockedReasons: [@"C:\labels"]));
            string[] after = Descendants(surface).OfType<Label>().Select(label => label.Text).ToArray();

            Assert.Equal(before, after);
            Assert.DoesNotContain(after, label => label.Contains("password",
                StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void Full_lifecycle_is_non_throwing_and_non_executing()
    {
        RunSta(() =>
        {
            var surface = new PilotDashboardControl();
            Exception? error = Record.Exception(() =>
            {
                surface.RenderAsync(State()).GetAwaiter().GetResult();
                surface.ReplaceState(State(status: PilotUiViewStatus.Blocked));
                surface.ClearState();
                using var cancellation = new CancellationTokenSource();
                cancellation.Cancel();
                surface.RenderAsync(State(), cancellation.Token).GetAwaiter().GetResult();
                surface.Dispose();
                surface.RenderAsync(State()).GetAwaiter().GetResult();
                surface.ReplaceState(State());
                surface.ClearState();
            });

            Assert.Null(error);
            Assert.True(surface.IsDisposed);
            Assert.False(surface.ExecutesCommands);
        });
    }

    [Fact]
    public void Background_update_without_a_handle_is_safely_ignored()
    {
        RunSta(() =>
        {
            using var surface = new PilotDashboardControl();
            Assert.False(surface.IsHandleCreated);

            Exception? error = null;
            Task caller = Task.Run(() =>
            {
                try { surface.RenderAsync(State()).GetAwaiter().GetResult(); }
                catch (Exception exception) { error = exception; }
            });
            caller.GetAwaiter().GetResult();

            Assert.Null(error);
            Assert.False(surface.HasState);
            Assert.False(surface.StartsBackgroundWork);
        });
    }

    [Fact]
    public void Background_update_with_a_handle_marshals_to_the_UI_thread()
    {
        RunSta(() =>
        {
            using var surface = new PilotDashboardControl();
            _ = surface.Handle;
            bool previousCheck = Control.CheckForIllegalCrossThreadCalls;
            Control.CheckForIllegalCrossThreadCalls = true;
            try
            {
                Task render = Task.Run(async () => await surface.RenderAsync(State()));
                var timeout = Stopwatch.StartNew();
                while (!render.IsCompleted && timeout.Elapsed < TimeSpan.FromSeconds(5))
                {
                    Application.DoEvents();
                    Thread.Yield();
                }

                Assert.True(render.IsCompleted);
                render.GetAwaiter().GetResult();
                Assert.True(surface.HasState);
                Assert.Equal("pilot-hardening-1", surface.Snapshot.PilotId);
                Assert.False(surface.ExecutesCommands);
            }
            finally
            {
                Control.CheckForIllegalCrossThreadCalls = previousCheck;
            }
        });
    }

    [Fact]
    public void Cancellation_during_marshaled_render_and_disposed_handle_are_non_throwing()
    {
        RunSta(() =>
        {
            var surface = new PilotDashboardControl();
            _ = surface.Handle;
            using var cancellation = new CancellationTokenSource();
            using var queued = new ManualResetEventSlim();
            Task render = Task.Run(async () =>
            {
                Task pendingRender = surface.RenderAsync(State(), cancellation.Token);
                queued.Set();
                await pendingRender;
            });
            Assert.True(queued.Wait(TimeSpan.FromSeconds(5)));
            cancellation.Cancel();

            var timeout = Stopwatch.StartNew();
            while (!render.IsCompleted && timeout.Elapsed < TimeSpan.FromSeconds(5))
            {
                Application.DoEvents();
                Thread.Yield();
            }

            Exception? cancellationError = Record.Exception(render.GetAwaiter().GetResult);
            surface.Dispose();
            Exception? disposedError = null;
            Task disposedCaller = Task.Run(() =>
            {
                try { surface.RenderAsync(State()).GetAwaiter().GetResult(); }
                catch (Exception exception) { disposedError = exception; }
            });
            disposedCaller.GetAwaiter().GetResult();

            Assert.Null(cancellationError);
            Assert.Null(disposedError);
            Assert.False(surface.HasState);
            Assert.False(surface.ExecutesCommands);
        });
    }

    [Fact]
    public void Refresh_reuses_control_tree_and_has_no_polling_timer_or_background_work()
    {
        RunSta(() =>
        {
            using var surface = new PilotDashboardControl();
            Control[] before = Descendants(surface).ToArray();

            for (int index = 0; index < 20; index++)
                surface.ReplaceState(State(status: index % 2 == 0
                    ? PilotUiViewStatus.Completed : PilotUiViewStatus.Blocked));
            Control[] after = Descendants(surface).ToArray();

            Assert.Equal(before.Length, after.Length);
            Assert.True(before.Zip(after).All(pair => ReferenceEquals(pair.First, pair.Second)));
            Assert.False(surface.UsesPolling);
            Assert.False(surface.UsesTimer);
            Assert.False(surface.StartsBackgroundWork);
            Assert.False(surface.ResolvesServices);
            Assert.False(surface.RecreatesControlsOnRefresh);

            Type[] fieldTypes = SurfaceTypes().SelectMany(type => type.GetFields(
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic))
                .Select(field => field.FieldType).ToArray();
            Assert.DoesNotContain(fieldTypes, type => typeof(System.Threading.Tasks.Task)
                .IsAssignableFrom(type));
            Assert.DoesNotContain(fieldTypes, type => type.Name.Contains("Timer",
                StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void Production_and_execution_boundaries_remain_absent()
    {
        string root = RepositoryRoot();
        string program = File.ReadAllText(Path.Combine(root, "Program.cs"));
        string protectedSources = string.Join(Environment.NewLine,
            Directory.GetFiles(Path.Combine(root, "UI", "Startup"), "*.cs",
                    SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(Path.Combine(root, "UI", "Forms"), "*.cs",
                    SearchOption.AllDirectories)).Select(File.ReadAllText));
        string pilotSources = string.Join(Environment.NewLine,
            Directory.GetFiles(Path.Combine(root, "UI", "Pilot"), "*.cs",
                SearchOption.AllDirectories).Select(File.ReadAllText));

        string programHash = Convert.ToHexString(SHA256.HashData(
            File.ReadAllBytes(Path.Combine(root, "Program.cs"))));
        Assert.Equal("33985F732E77AFC7249DDA0174E8BCC58601B5E0B3E22B93E31933F01ACCAA76",
            programHash);
        Assert.DoesNotContain("Rah_Negar.UI.Pilot", program, StringComparison.Ordinal);
        Assert.DoesNotContain("Rah_Negar.UI.Pilot", protectedSources, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(PilotDashboardControl), protectedSources,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.Data.Sqlite", pilotSources,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MigrationRunner", pilotSources,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PilotExecutionCoordinator", pilotSources,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IPilotHost", pilotSources, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Esd", pilotSources, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Cutover", pilotSources, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IServiceProvider", pilotSources,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Task.Run", pilotSources, StringComparison.Ordinal);
        Assert.DoesNotContain("new Timer", pilotSources, StringComparison.Ordinal);
    }

    private static PilotDashboardState State(
        PilotUiViewStatus status = PilotUiViewStatus.Completed,
        string pilotId = "pilot-hardening-1",
        string correlationId = "correlation-hardening-1",
        string comparison = "Legacy and target observations matched.",
        string? evidenceReference = "evidence-hardening-1",
        bool evidenceAvailable = false,
        IEnumerable<string>? warnings = null,
        IEnumerable<string>? blockedReasons = null)
    {
        var featureState = new PilotFeatureViewState(pilotId, PilotFeature.ReportingPilot,
            "Reporting pilot", status,
            "Reporting pilot observation. Legacy remains authoritative.",
            ShadowDifferenceSeverity.None, comparison,
            evidenceAvailable ? PilotEvidenceState.Complete : PilotEvidenceState.Incomplete,
            evidenceReference, Timestamp, warnings ?? Array.Empty<string>(),
            blockedReasons ?? Array.Empty<string>(), correlationId);
        return new(pilotId, PilotFeature.ReportingPilot, status, comparison,
            evidenceAvailable, false, featureState);
    }

    private static TextBox[] Fields(Control root) =>
        Descendants(root).OfType<TextBox>().ToArray();

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control descendant in Descendants(child)) yield return descendant;
        }
    }

    private static string SnapshotText(PilotDashboardControl surface) => string.Join('|',
        surface.Snapshot.PilotId, surface.Snapshot.SelectedFeature,
        surface.Snapshot.ExecutionState, surface.Snapshot.ComparisonStatus,
        surface.Snapshot.Severity, surface.Snapshot.EvidenceSummary,
        surface.Snapshot.RollbackSummary, surface.Snapshot.CorrelationId,
        surface.Snapshot.Timestamp, string.Join('|', surface.Snapshot.Warnings),
        string.Join('|', surface.Snapshot.BlockedReasons));

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

    private sealed class FixedTextProvider(string? text) : IPilotLocalizedTextProvider
    {
        public string GetText(PilotLocalizedTextKey key) => text!;
    }

    private sealed class ThrowingTextProvider : IPilotLocalizedTextProvider
    {
        public string GetText(PilotLocalizedTextKey key) =>
            throw new InvalidOperationException("secret resource failure");
    }
}
