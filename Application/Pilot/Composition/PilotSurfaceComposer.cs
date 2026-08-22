using Rah_Negar.Foundation.Application.Pilot.Presentation;

namespace Rah_Negar.Foundation.Application.Pilot.Composition;

public sealed class PilotSurfaceComposer
{
    private static readonly IReadOnlySet<string> SupportedCapabilities =
        new HashSet<string>(PilotUiCapabilities.All, StringComparer.Ordinal);

    public bool AutomaticallyRegisters => false;
    public bool AutomaticallyAttaches => false;
    public bool ExecutesWorkflows => false;
    public bool FallsBackToProduction => false;
    public bool ActivatesFeatures => false;

    public PilotCompositionResult Create(
        PilotCompositionContext? context,
        PilotSurfaceDescriptor? surface,
        IPilotDashboardStateProvider? stateProvider,
        IPilotWinFormsStateConsumer? consumer)
    {
        try
        {
            string? issue = Validate(context, surface, stateProvider, consumer);
            if (issue is not null)
                return new(PilotCompositionStatus.Blocked, issue, null);
            return new(PilotCompositionStatus.Created, "composition-created",
                new PilotSurfaceBinding(context!, surface!, stateProvider!, consumer!));
        }
        catch
        {
            return new(PilotCompositionStatus.Failed, "composition-validation-failed", null);
        }
    }

    private static string? Validate(
        PilotCompositionContext? context,
        PilotSurfaceDescriptor? surface,
        IPilotDashboardStateProvider? stateProvider,
        IPilotWinFormsStateConsumer? consumer)
    {
        if (context is null) return "composition-context-required";
        if (surface is null) return "composition-surface-required";
        if (stateProvider is null) return "composition-state-provider-required";
        if (consumer is null) return "composition-consumer-required";
        if (!context.ExplicitlyApproved) return "composition-approval-required";
        if (!PilotCompositionText.IsSafeIdentifier(context.CompositionId) ||
            !PilotCompositionText.IsSafeIdentifier(context.PilotId) ||
            !PilotCompositionText.IsSafeIdentifier(context.CorrelationId) ||
            !PilotCompositionText.IsSafeIdentifier(context.SurfaceId) ||
            !PilotCompositionText.IsSafeIdentifier(context.StateSourceId))
            return "composition-identifier-invalid";
        if (!Utc(context.ApprovedAtUtc) || !Utc(context.ExpiresAtUtc) ||
            !Utc(context.EvaluationTimeUtc) || context.ApprovedAtUtc > context.EvaluationTimeUtc ||
            context.EvaluationTimeUtc >= context.ExpiresAtUtc)
            return "composition-approval-window-invalid";
        if (context.CapabilityEvidence is null)
            return "composition-capability-evidence-required";
        if (!CapabilityEvidenceValid(context))
            return "composition-capability-evidence-invalid";
        if (!StringComparer.Ordinal.Equals(context.SurfaceId, surface.SurfaceId))
            return "composition-surface-mismatch";
        if (!surface.ReadOnly || surface.AutomaticallyOpens || surface.SupportsCommands ||
            !Enum.IsDefined(surface.SurfaceKind) || surface.SurfaceKind != consumer.SurfaceKind)
            return "composition-surface-unsafe";
        if (stateProvider is IPilotWinFormsStateConsumer ||
            consumer is IPilotDashboardStateProvider)
            return "composition-direction-invalid";

        PilotStateSourceDescriptor descriptor = stateProvider.Descriptor;
        if (!StringComparer.Ordinal.Equals(context.StateSourceId, descriptor.SourceId))
            return "composition-source-mismatch";
        if (descriptor.Availability != PilotStateAvailability.Available)
            return "composition-source-unavailable";
        if (!descriptor.ReadOnly || descriptor.ExecutesWorkflows ||
            !Utc(descriptor.ObservedAtUtc))
            return "composition-source-unsafe";
        return null;
    }

    private static bool CapabilityEvidenceValid(PilotCompositionContext context)
    {
        PilotCapabilityEvidence evidence = context.CapabilityEvidence;
        return StringComparer.Ordinal.Equals(context.PilotId, evidence.PilotId) &&
            StringComparer.Ordinal.Equals(context.CorrelationId, evidence.CorrelationId) &&
            Utc(evidence.ObservedAtUtc) && evidence.ObservedAtUtc <= context.EvaluationTimeUtc &&
            evidence.AvailableCapabilities.Count == SupportedCapabilities.Count &&
            evidence.AvailableCapabilities.All(SupportedCapabilities.Contains);
    }

    private static bool Utc(DateTimeOffset value) => value.Offset == TimeSpan.Zero;
}

public sealed class PilotSurfaceBinding : IDisposable
{
    private readonly object _gate = new();
    private readonly PilotCompositionContext _context;
    private readonly PilotSurfaceDescriptor _surface;
    private readonly IPilotDashboardStateProvider _stateProvider;
    private readonly IPilotWinFormsStateConsumer _consumer;
    private readonly CancellationTokenSource _lifetime = new();
    private PilotBindingLifecycleState _lifecycle = PilotBindingLifecycleState.Created;
    private bool _disposed;

    internal PilotSurfaceBinding(
        PilotCompositionContext context,
        PilotSurfaceDescriptor surface,
        IPilotDashboardStateProvider stateProvider,
        IPilotWinFormsStateConsumer consumer)
    {
        _context = context;
        _surface = surface;
        _stateProvider = stateProvider;
        _consumer = consumer;
    }

    public string CompositionId => _context.CompositionId;
    public PilotSurfaceDescriptor Surface => _surface;
    public bool AutomaticallyRefreshes => false;
    public bool UsesPolling => false;
    public bool ExecutesWorkflows => false;
    public bool ExposesCommandChannel => false;
    public bool FallsBackToProduction => false;

    public PilotBindingLifecycleState Lifecycle
    {
        get { lock (_gate) return _lifecycle; }
    }

    public async ValueTask<PilotBindingOperationResult> AttachAsync(
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_disposed)
                return Result(PilotBindingOperationStatus.Disposed, "binding-disposed");
            if (_lifecycle != PilotBindingLifecycleState.Created)
                return Result(PilotBindingOperationStatus.Blocked, "binding-attach-not-allowed");
            _lifecycle = PilotBindingLifecycleState.Attaching;
        }

        CancellationTokenSource? linked = null;
        try
        {
            linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _lifetime.Token);
            PilotDashboardState? state = await _stateProvider.GetDashboardStateAsync(
                _context, linked.Token).ConfigureAwait(false);
            if (linked.IsCancellationRequested)
                return CompleteCanceled();
            if (!StateMatchesContext(state))
                return CompleteFailure("binding-state-invalid");

            await _consumer.RenderAsync(state!, linked.Token).ConfigureAwait(false);
            if (linked.IsCancellationRequested)
                return CompleteCanceled();
            lock (_gate)
            {
                if (_disposed)
                    return Result(PilotBindingOperationStatus.Disposed, "binding-disposed");
                _lifecycle = PilotBindingLifecycleState.Attached;
            }
            return Result(PilotBindingOperationStatus.Attached, "binding-attached");
        }
        catch (OperationCanceledException)
        {
            return CompleteCanceled();
        }
        catch
        {
            return CompleteFailure("binding-update-failed");
        }
        finally
        {
            linked?.Dispose();
        }
    }

    public PilotBindingOperationResult Detach()
    {
        PilotBindingOperationResult result;
        lock (_gate)
        {
            if (_disposed)
                return Result(PilotBindingOperationStatus.Disposed, "binding-disposed");
            if (_lifecycle is PilotBindingLifecycleState.Detached)
                return Result(PilotBindingOperationStatus.Detached, "binding-already-detached");
            _lifecycle = PilotBindingLifecycleState.Detached;
            result = Result(PilotBindingOperationStatus.Detached, "binding-detached");
        }
        TryCancelLifetime();
        return result;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _lifecycle = PilotBindingLifecycleState.Disposed;
        }
        TryCancelLifetime();
        try { _lifetime.Dispose(); }
        catch { }
    }

    private bool StateMatchesContext(PilotDashboardState? state)
    {
        if (state is null || state.CanActivateFeature || state.CanSwitchAuthority ||
            state.FeatureState.AllowsExecution || state.FeatureState.AllowsRouting ||
            state.FeatureState.AllowsActivation)
            return false;
        if (!StringComparer.Ordinal.Equals(state.FeatureState.PilotId, _context.PilotId) ||
            !StringComparer.Ordinal.Equals(state.FeatureState.CorrelationId,
                _context.CorrelationId))
            return false;
        return state.ActivePilotId is null ||
            StringComparer.Ordinal.Equals(state.ActivePilotId, _context.PilotId);
    }

    private PilotBindingOperationResult CompleteCanceled()
    {
        lock (_gate)
        {
            if (_disposed)
                return Result(PilotBindingOperationStatus.Disposed, "binding-disposed");
            _lifecycle = PilotBindingLifecycleState.Detached;
            return Result(PilotBindingOperationStatus.Canceled, "binding-update-canceled");
        }
    }

    private PilotBindingOperationResult CompleteFailure(string reasonCode)
    {
        lock (_gate)
        {
            if (_disposed)
                return Result(PilotBindingOperationStatus.Disposed, "binding-disposed");
            _lifecycle = PilotBindingLifecycleState.Failed;
            return Result(PilotBindingOperationStatus.Failed, reasonCode);
        }
    }

    private static PilotBindingOperationResult Result(
        PilotBindingOperationStatus status,
        string reasonCode) => new(status, reasonCode);

    private void TryCancelLifetime()
    {
        try { _lifetime.Cancel(); }
        catch { }
    }
}
