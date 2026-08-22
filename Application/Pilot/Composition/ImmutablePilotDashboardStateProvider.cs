using Rah_Negar.Foundation.Application.Pilot.Presentation;

namespace Rah_Negar.Foundation.Application.Pilot.Composition;

/// <summary>
/// Explicit in-memory source for an already-created immutable dashboard state.
/// It performs no refresh, execution, discovery, persistence, or UI access.
/// </summary>
public sealed class ImmutablePilotDashboardStateProvider : IPilotDashboardStateProvider
{
    private readonly PilotDashboardState _state;

    public ImmutablePilotDashboardStateProvider(
        PilotStateSourceDescriptor descriptor,
        PilotDashboardState state)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public PilotStateSourceDescriptor Descriptor { get; }
    public bool ExecutesCommands => false;
    public bool MutatesState => false;
    public bool AutomaticallyRefreshes => false;

    public ValueTask<PilotDashboardState?> GetDashboardStateAsync(
        PilotCompositionContext context,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromResult<PilotDashboardState?>(null);
        return ValueTask.FromResult<PilotDashboardState?>(_state);
    }
}
