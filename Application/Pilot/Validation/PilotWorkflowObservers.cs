using Rah_Negar.Foundation.Application.Pilot.Composition;

namespace Rah_Negar.Foundation.Application.Pilot.Validation;

public abstract class ImmutablePilotWorkflowObserverBase : IPilotWorkflowObserver
{
    private readonly PilotWorkflowObservationResult _result;

    protected ImmutablePilotWorkflowObserverBase(
        PilotWorkflowObserverDescriptor descriptor,
        PilotWorkflowObservationResult result)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        _result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public PilotWorkflowObserverDescriptor Descriptor { get; }
    public bool ExecutesCommands => false;
    public bool MutatesState => false;
    public bool AutomaticallyRuns => false;

    public ValueTask<PilotWorkflowObservationResult?> ObserveAsync(
        PilotWorkflowValidationContext context,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromResult<PilotWorkflowObservationResult?>(null);
        return ValueTask.FromResult<PilotWorkflowObservationResult?>(_result);
    }
}

public sealed class AuthenticationPilotValidationObserver :
    ImmutablePilotWorkflowObserverBase, IAuthenticationPilotValidationObserver
{
    public AuthenticationPilotValidationObserver(string observerId, string safeName,
        PilotObservationBoundary boundary, PilotWorkflowObservationResult result)
        : base(ObserverDescriptorFactory.Create(observerId, safeName, PilotValidationWorkflow.Authentication,
            boundary), result) { }
}

public sealed class ReportingPilotValidationObserver :
    ImmutablePilotWorkflowObserverBase, IReportingPilotValidationObserver
{
    public ReportingPilotValidationObserver(string observerId, string safeName,
        PilotObservationBoundary boundary, PilotWorkflowObservationResult result)
        : base(ObserverDescriptorFactory.Create(observerId, safeName, PilotValidationWorkflow.Reporting,
            boundary), result) { }
}

public sealed class RuntimeEventPilotValidationObserver :
    ImmutablePilotWorkflowObserverBase, IRuntimeEventPilotValidationObserver
{
    public RuntimeEventPilotValidationObserver(string observerId, string safeName,
        PilotObservationBoundary boundary, PilotWorkflowObservationResult result)
        : base(ObserverDescriptorFactory.Create(observerId, safeName, PilotValidationWorkflow.RuntimeEvent,
            boundary), result) { }
}

public sealed class ProtectedSettingsPilotValidationObserver :
    ImmutablePilotWorkflowObserverBase, IProtectedSettingsPilotValidationObserver
{
    public ProtectedSettingsPilotValidationObserver(string observerId, string safeName,
        PilotObservationBoundary boundary, PilotWorkflowObservationResult result)
        : base(ObserverDescriptorFactory.Create(observerId, safeName, PilotValidationWorkflow.ProtectedSettings,
            boundary), result) { }
}

public sealed class ExportPilotValidationObserver :
    ImmutablePilotWorkflowObserverBase, IExportPilotValidationObserver
{
    public ExportPilotValidationObserver(string observerId, string safeName,
        PilotObservationBoundary boundary, PilotWorkflowObservationResult result)
        : base(ObserverDescriptorFactory.Create(observerId, safeName, PilotValidationWorkflow.Export,
            boundary), result) { }
}

file static class ObserverDescriptorFactory
{
    public static PilotWorkflowObserverDescriptor Create(
        string observerId,
        string safeName,
        PilotValidationWorkflow workflow,
        PilotObservationBoundary boundary) => new(observerId, safeName, workflow, boundary,
            PilotStateAvailability.Available, PilotObservationSafetyProfile.ReadOnlyObservation);
}
