using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Windows.Forms;
using Rah_Negar.Foundation.Application.Integration;
using Rah_Negar.Foundation.Application.Pilot;
using Rah_Negar.Foundation.Application.Pilot.Composition;
using Rah_Negar.Foundation.Application.Pilot.Presentation;
using Rah_Negar.UI.Pilot;

namespace Rah_Negar.Tests.Pilot;

public sealed class PilotCompositionIntegrationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 22, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Composition_contracts_are_explicit_immutable_and_defensively_copied()
    {
        var capabilities = new List<string>(PilotUiCapabilities.All);
        var metadata = new Dictionary<string, string> { ["source-version"] = "version-1" };
        PilotCapabilityEvidence evidence = Evidence(capabilities);
        PilotCompositionContext context = Context(evidence: evidence);
        var source = new PilotStateSourceDescriptor("source-1", "Immutable state source",
            PilotStateAvailability.Available, true, false, Now, metadata);
        capabilities.Add("unexpected.capability");
        metadata["late-key"] = "late-value";

        Assert.Equal(PilotUiCapabilities.All.Order(StringComparer.Ordinal),
            evidence.AvailableCapabilities);
        Assert.Single(source.SafeMetadata);
        Assert.False(context.AutomaticallyActivates);
        Assert.False(context.AllowsExecution);
        Assert.False(context.AllowsAuthoritySwitch);
        Assert.False(context.FallsBackToProduction);
        Assert.All(new[] { typeof(PilotCompositionContext), typeof(PilotSurfaceDescriptor),
            typeof(PilotStateSourceDescriptor), typeof(PilotCompositionResult),
            typeof(PilotCapabilityEvidence) }, type =>
            Assert.DoesNotContain(type.GetProperties(), property => property.SetMethod is not null));
    }

    [Fact]
    public void Safe_source_metadata_withholds_paths_queries_exceptions_and_secrets()
    {
        KeyValuePair<string, string>[] metadata =
        [
            new("safe-key", "safe-value"),
            new("path", @"C:\production\pilot.db"),
            new("query", "SELECT password"),
            new("exception", "stack-trace"),
            new("credential", "secret")
        ];
        var descriptor = new PilotStateSourceDescriptor("source-1",
            "SELECT password FROM operators", PilotStateAvailability.Available,
            true, false, Now, metadata);

        Assert.Equal("Pilot state source", descriptor.SafeName);
        Assert.Single(descriptor.SafeMetadata);
        Assert.Equal("safe-value", descriptor.SafeMetadata["safe-key"]);
        Assert.False(descriptor.AccessesUiControls);
        Assert.False(descriptor.AccessesProductionForms);
        Assert.False(descriptor.WritesDatabase);
    }

    [Fact]
    public void Composer_creates_an_inactive_binding_without_reading_state()
    {
        var provider = new RecordingProvider(State());
        var consumer = new RecordingConsumer();
        var composer = new PilotSurfaceComposer();

        PilotCompositionResult result = composer.Create(Context(), Surface(), provider, consumer);

        Assert.True(result.IsCreated);
        Assert.Equal(PilotCompositionStatus.Created, result.Status);
        Assert.Equal("composition-created", result.ReasonCode);
        Assert.Equal(PilotBindingLifecycleState.Created, result.Binding!.Lifecycle);
        Assert.Equal(0, provider.ReadCount);
        Assert.Equal(0, consumer.RenderCount);
        Assert.False(composer.AutomaticallyRegisters);
        Assert.False(composer.AutomaticallyAttaches);
        Assert.False(composer.ExecutesWorkflows);
        Assert.False(composer.FallsBackToProduction);
        Assert.False(composer.ActivatesFeatures);
    }

    [Fact]
    public async Task Attach_transfers_one_immutable_state_in_one_direction()
    {
        PilotDashboardState state = State();
        var provider = new RecordingProvider(state);
        var consumer = new RecordingConsumer();
        PilotSurfaceBinding binding = Assert.IsType<PilotSurfaceBinding>(
            new PilotSurfaceComposer().Create(Context(), Surface(), provider, consumer).Binding);

        PilotBindingOperationResult attached = await binding.AttachAsync();

        Assert.True(attached.IsAttached);
        Assert.Same(state, consumer.State);
        Assert.Equal(1, provider.ReadCount);
        Assert.Equal(1, consumer.RenderCount);
        Assert.Equal(PilotBindingLifecycleState.Attached, binding.Lifecycle);
        Assert.False(binding.AutomaticallyRefreshes);
        Assert.False(binding.UsesPolling);
        Assert.False(binding.ExecutesWorkflows);
        Assert.False(binding.ExposesCommandChannel);
        Assert.False(binding.FallsBackToProduction);
        Assert.False(attached.ExecutedWorkflow);
        Assert.False(attached.ActivatedFeature);
        Assert.False(attached.SwitchedAuthority);
    }

    [Fact]
    public void Binding_connects_to_the_real_read_only_surface_without_execution()
    {
        RunSta(() =>
        {
            using var surface = new PilotDashboardControl();
            var provider = new ImmutablePilotDashboardStateProvider(Source(), State());
            PilotCompositionResult composition = new PilotSurfaceComposer().Create(
                Context(), Surface(), provider, surface);

            PilotBindingOperationResult attached = composition.Binding!.AttachAsync()
                .AsTask().GetAwaiter().GetResult();

            Assert.True(attached.IsAttached);
            Assert.True(surface.HasState);
            Assert.Equal("pilot-composition-1", surface.Snapshot.PilotId);
            Assert.False(surface.ExecutesCommands);
            Assert.False(provider.ExecutesCommands);
            Assert.False(provider.MutatesState);
            Assert.False(provider.AutomaticallyRefreshes);
        });
    }

    [Fact]
    public async Task Lifecycle_is_explicit_single_attach_detach_and_dispose()
    {
        var provider = new RecordingProvider(State());
        var consumer = new RecordingConsumer();
        PilotSurfaceBinding binding = new PilotSurfaceComposer()
            .Create(Context(), Surface(), provider, consumer).Binding!;

        Assert.Equal(PilotBindingLifecycleState.Created, binding.Lifecycle);
        Assert.Equal(PilotBindingOperationStatus.Attached,
            (await binding.AttachAsync()).Status);
        Assert.Equal(PilotBindingOperationStatus.Blocked,
            (await binding.AttachAsync()).Status);
        Assert.Equal(PilotBindingOperationStatus.Detached, binding.Detach().Status);
        Assert.Equal(PilotBindingLifecycleState.Detached, binding.Lifecycle);
        Assert.Equal(PilotBindingOperationStatus.Blocked,
            (await binding.AttachAsync()).Status);

        binding.Dispose();
        Assert.Equal(PilotBindingLifecycleState.Disposed, binding.Lifecycle);
        Assert.Equal(PilotBindingOperationStatus.Disposed, binding.Detach().Status);
        Assert.Equal(PilotBindingOperationStatus.Disposed,
            (await binding.AttachAsync()).Status);
        binding.Dispose();
        Assert.Equal(1, provider.ReadCount);
        Assert.Equal(1, consumer.RenderCount);
    }

    [Fact]
    public void Composer_blocks_missing_invalid_expired_and_unapproved_contexts()
    {
        var composer = new PilotSurfaceComposer();
        var provider = new RecordingProvider(State());
        var consumer = new RecordingConsumer();

        Assert.Equal("composition-context-required",
            composer.Create(null, Surface(), provider, consumer).ReasonCode);
        Assert.Equal("composition-surface-required",
            composer.Create(Context(), null, provider, consumer).ReasonCode);
        Assert.Equal("composition-state-provider-required",
            composer.Create(Context(), Surface(), null, consumer).ReasonCode);
        Assert.Equal("composition-consumer-required",
            composer.Create(Context(), Surface(), provider, null).ReasonCode);
        Assert.Equal("composition-approval-required",
            composer.Create(Context(approved: false), Surface(), provider, consumer).ReasonCode);
        Assert.Equal("composition-identifier-invalid",
            composer.Create(Context(compositionId: "unsafe path/value"), Surface(), provider,
                consumer).ReasonCode);
        Assert.Equal("composition-approval-window-invalid",
            composer.Create(Context(approvedAtUtc: Now.AddMinutes(1)), Surface(), provider,
                consumer).ReasonCode);
        Assert.All(new[]
        {
            composer.Create(null, Surface(), provider, consumer),
            composer.Create(Context(approved: false), Surface(), provider, consumer)
        }, result =>
        {
            Assert.Equal(PilotCompositionStatus.Blocked, result.Status);
            Assert.Null(result.Binding);
            Assert.False(result.ActivatedProduction);
            Assert.False(result.SwitchedAuthority);
        });
    }

    [Fact]
    public void Composer_validates_surface_source_capability_and_direction_boundaries()
    {
        var composer = new PilotSurfaceComposer();
        var consumer = new RecordingConsumer();

        Assert.Equal("composition-surface-unsafe", composer.Create(Context(),
            new PilotSurfaceDescriptor("surface-1", "Unsafe", PilotUiSurfaceKind.EmbeddedPilotPanel,
                false, true, true), new RecordingProvider(State()), consumer).ReasonCode);
        Assert.Equal("composition-source-unavailable", composer.Create(Context(), Surface(),
            new RecordingProvider(State(), availability: PilotStateAvailability.Unavailable),
            consumer).ReasonCode);
        Assert.Equal("composition-source-unsafe", composer.Create(Context(), Surface(),
            new RecordingProvider(State(), readOnly: false, executesWorkflows: true),
            consumer).ReasonCode);
        Assert.Equal("composition-capability-evidence-invalid", composer.Create(
            Context(evidence: Evidence([PilotUiCapabilities.PilotView])), Surface(),
            new RecordingProvider(State()), consumer).ReasonCode);
        var dual = new DualBoundary(State());
        Assert.Equal("composition-direction-invalid",
            composer.Create(Context(), Surface(), dual, dual).ReasonCode);
    }

    [Fact]
    public void Provider_descriptor_failure_is_contained_by_the_composer()
    {
        Exception? error = null;
        PilotCompositionResult? result = null;

        error = Record.Exception(() => result = new PilotSurfaceComposer().Create(
            Context(), Surface(), new ThrowingDescriptorProvider(), new RecordingConsumer()));

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal(PilotCompositionStatus.Failed, result!.Status);
        Assert.Equal("composition-validation-failed", result.ReasonCode);
        Assert.Null(result.Binding);
    }

    [Fact]
    public async Task Provider_failure_is_contained_and_consumer_is_not_called()
    {
        var consumer = new RecordingConsumer();
        PilotSurfaceBinding binding = new PilotSurfaceComposer().Create(Context(), Surface(),
            new ThrowingStateProvider(), consumer).Binding!;

        PilotBindingOperationResult result = await binding.AttachAsync();

        Assert.Equal(PilotBindingOperationStatus.Failed, result.Status);
        Assert.Equal("binding-update-failed", result.ReasonCode);
        Assert.Equal(PilotBindingLifecycleState.Failed, binding.Lifecycle);
        Assert.Equal(0, consumer.RenderCount);
        Assert.False(result.ExecutedWorkflow);
    }

    [Fact]
    public async Task Consumer_failure_is_contained_without_provider_retry_or_fallback()
    {
        var provider = new RecordingProvider(State());
        var consumer = new RecordingConsumer(throwOnRender: true);
        PilotSurfaceBinding binding = new PilotSurfaceComposer()
            .Create(Context(), Surface(), provider, consumer).Binding!;

        PilotBindingOperationResult result = await binding.AttachAsync();

        Assert.Equal(PilotBindingOperationStatus.Failed, result.Status);
        Assert.Equal("binding-update-failed", result.ReasonCode);
        Assert.Equal(1, provider.ReadCount);
        Assert.Equal(1, consumer.RenderCount);
        Assert.False(binding.FallsBackToProduction);
        Assert.False(result.ActivatedFeature);
    }

    [Fact]
    public async Task Invalid_provider_state_fails_before_UI_consumption()
    {
        var provider = new RecordingProvider(State(pilotId: "different-pilot"));
        var consumer = new RecordingConsumer();
        PilotSurfaceBinding binding = new PilotSurfaceComposer()
            .Create(Context(), Surface(), provider, consumer).Binding!;

        PilotBindingOperationResult result = await binding.AttachAsync();

        Assert.Equal(PilotBindingOperationStatus.Failed, result.Status);
        Assert.Equal("binding-state-invalid", result.ReasonCode);
        Assert.Equal(0, consumer.RenderCount);
        Assert.Equal(PilotBindingLifecycleState.Failed, binding.Lifecycle);
    }

    [Fact]
    public async Task Disposal_during_provider_update_is_non_throwing_and_skips_UI()
    {
        var provider = new BlockingProvider();
        var consumer = new RecordingConsumer();
        PilotSurfaceBinding binding = new PilotSurfaceComposer()
            .Create(Context(), Surface(), provider, consumer).Binding!;
        Task<PilotBindingOperationResult> update = binding.AttachAsync().AsTask();
        Assert.True(provider.Started.Wait(TimeSpan.FromSeconds(5)));

        Exception? disposeError = Record.Exception(binding.Dispose);
        provider.Release(State());
        PilotBindingOperationResult result = await update;

        Assert.Null(disposeError);
        Assert.Equal(PilotBindingOperationStatus.Disposed, result.Status);
        Assert.Equal(PilotBindingLifecycleState.Disposed, binding.Lifecycle);
        Assert.Equal(0, consumer.RenderCount);
        Assert.False(result.ExecutedWorkflow);
    }

    [Fact]
    public void Capability_evidence_is_read_only_metadata_not_RBAC_or_permissions()
    {
        PilotCapabilityEvidence evidence = Evidence(PilotUiCapabilities.All);

        Assert.Equal(new[] { "comparison.view", "evidence.view", "pilot.view" },
            evidence.AvailableCapabilities);
        Assert.True(evidence.IsReadOnly);
        Assert.False(evidence.ImplementsRbac);
        Assert.False(evidence.CreatesPermissions);
        Assert.Equal("pilot-composition-1", evidence.PilotId);
        Assert.Equal("correlation-composition-1", evidence.CorrelationId);
    }

    [Fact]
    public void Composition_namespace_has_no_execution_database_migration_UI_or_identity_dependency()
    {
        Type[] types = typeof(PilotSurfaceComposer).Assembly.GetTypes().Where(type =>
            type.Namespace == typeof(PilotSurfaceComposer).Namespace).ToArray();
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
        Assert.DoesNotContain("Migration", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.Windows.Forms", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Rah_Negar.UI.Forms", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PilotHost", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ExecutionCoordinator", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Activate", methods, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Execute", methods, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Migrate", methods, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Rbac", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupportRole", surface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupportLogin", surface, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Composition_has_no_polling_background_scheduler_or_startup_registration()
    {
        string root = RepositoryRoot();
        string compositionSource = string.Join(Environment.NewLine,
            Directory.GetFiles(Path.Combine(root, "Application", "Pilot", "Composition"),
                "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        string programPath = Path.Combine(root, "Program.cs");
        string productionSource = File.ReadAllText(programPath) + Environment.NewLine +
            string.Join(Environment.NewLine,
                Directory.GetFiles(Path.Combine(root, "UI", "Startup"), "*.cs",
                        SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(Path.Combine(root, "UI", "Forms"), "*.cs",
                        SearchOption.AllDirectories)).Select(File.ReadAllText));

        Assert.Equal("33985F732E77AFC7249DDA0174E8BCC58601B5E0B3E22B93E31933F01ACCAA76",
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(programPath))));
        Assert.DoesNotContain("PilotSurfaceComposer", productionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IPilotDashboardStateProvider", productionSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Run", compositionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new Timer", compositionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("PeriodicTimer", compositionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.Data.Sqlite", compositionSource,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MigrationRunner", compositionSource,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EsdReconciliation", compositionSource,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EsdAuthority", compositionSource,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ProtectedEsd", compositionSource,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Cutover", compositionSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupportIdentity", compositionSource,
            StringComparison.OrdinalIgnoreCase);
    }

    private static PilotCompositionContext Context(
        bool approved = true,
        string compositionId = "composition-1",
        DateTimeOffset? approvedAtUtc = null,
        PilotCapabilityEvidence? evidence = null) => new(compositionId,
            "pilot-composition-1", "correlation-composition-1", "surface-1", "source-1",
            approved, approvedAtUtc ?? Now.AddMinutes(-5), Now.AddMinutes(30), Now,
            evidence ?? Evidence(PilotUiCapabilities.All));

    private static PilotCapabilityEvidence Evidence(IEnumerable<string> capabilities) => new(
        "pilot-composition-1", "correlation-composition-1", capabilities, Now.AddMinutes(-1));

    private static PilotSurfaceDescriptor Surface() => new("surface-1",
        "Pilot observation surface", PilotUiSurfaceKind.EmbeddedPilotPanel,
        readOnly: true, automaticallyOpens: false, supportsCommands: false);

    private static PilotDashboardState State(string pilotId = "pilot-composition-1")
    {
        var feature = new PilotFeatureViewState(pilotId, PilotFeature.ReportingPilot,
            "Reporting pilot", PilotUiViewStatus.Completed,
            "Reporting pilot observation. Legacy remains authoritative.",
            ShadowDifferenceSeverity.None, "Legacy and target observations matched.",
            PilotEvidenceState.Complete, "evidence-composition-1", Now,
            Array.Empty<string>(), Array.Empty<string>(), "correlation-composition-1");
        return new(pilotId, PilotFeature.ReportingPilot, PilotUiViewStatus.Completed,
            "Legacy and target observations matched.", true, true, feature);
    }

    private static PilotStateSourceDescriptor Source(
        PilotStateAvailability availability = PilotStateAvailability.Available,
        bool readOnly = true,
        bool executesWorkflows = false) => new("source-1", "Immutable state source",
            availability, readOnly, executesWorkflows, Now,
            [new("source-version", "version-1")]);

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

    private sealed class RecordingProvider : IPilotDashboardStateProvider
    {
        private readonly PilotDashboardState? _state;

        public RecordingProvider(PilotDashboardState? state,
            PilotStateAvailability availability = PilotStateAvailability.Available,
            bool readOnly = true,
            bool executesWorkflows = false)
        {
            _state = state;
            Descriptor = Source(availability, readOnly, executesWorkflows);
        }

        public PilotStateSourceDescriptor Descriptor { get; }
        public int ReadCount { get; private set; }

        public ValueTask<PilotDashboardState?> GetDashboardStateAsync(
            PilotCompositionContext context,
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return ValueTask.FromResult(_state);
        }
    }

    private sealed class RecordingConsumer(bool throwOnRender = false) : IPilotWinFormsStateConsumer
    {
        public PilotUiSurfaceKind SurfaceKind => PilotUiSurfaceKind.EmbeddedPilotPanel;
        public int RenderCount { get; private set; }
        public PilotDashboardState? State { get; private set; }

        public Task RenderAsync(PilotDashboardState state,
            CancellationToken cancellationToken = default)
        {
            RenderCount++;
            if (throwOnRender) throw new InvalidOperationException("secret consumer exception");
            State = state;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingDescriptorProvider : IPilotDashboardStateProvider
    {
        public PilotStateSourceDescriptor Descriptor =>
            throw new InvalidOperationException("secret descriptor failure");

        public ValueTask<PilotDashboardState?> GetDashboardStateAsync(
            PilotCompositionContext context,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<PilotDashboardState?>(null);
    }

    private sealed class ThrowingStateProvider : IPilotDashboardStateProvider
    {
        public PilotStateSourceDescriptor Descriptor => Source();

        public ValueTask<PilotDashboardState?> GetDashboardStateAsync(
            PilotCompositionContext context,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("password database exception");
    }

    private sealed class BlockingProvider : IPilotDashboardStateProvider
    {
        private readonly TaskCompletionSource<PilotDashboardState?> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public PilotStateSourceDescriptor Descriptor => Source();
        public ManualResetEventSlim Started { get; } = new();

        public async ValueTask<PilotDashboardState?> GetDashboardStateAsync(
            PilotCompositionContext context,
            CancellationToken cancellationToken = default)
        {
            Started.Set();
            return await _completion.Task.WaitAsync(cancellationToken);
        }

        public void Release(PilotDashboardState state) => _completion.TrySetResult(state);
    }

    private sealed class DualBoundary(PilotDashboardState state) :
        IPilotDashboardStateProvider, IPilotWinFormsStateConsumer
    {
        public PilotStateSourceDescriptor Descriptor => Source();
        public PilotUiSurfaceKind SurfaceKind => PilotUiSurfaceKind.EmbeddedPilotPanel;

        public ValueTask<PilotDashboardState?> GetDashboardStateAsync(
            PilotCompositionContext context,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(state)!;

        public Task RenderAsync(PilotDashboardState dashboardState,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
