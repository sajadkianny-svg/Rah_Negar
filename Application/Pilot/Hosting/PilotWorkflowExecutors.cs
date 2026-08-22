using Rah_Negar.Foundation.Application.Integration;
using Rah_Negar.Foundation.Time;

namespace Rah_Negar.Foundation.Application.Pilot.Hosting;

public sealed class AuthenticationPilotWorkflowExecutor : IPilotWorkflowExecutor
{
    private readonly AuthenticationPilotService _service;
    private readonly IClock _clock;
    private readonly PilotAdapterDescriptor _legacyDescriptor;
    private readonly PilotAdapterDescriptor _targetDescriptor;

    public AuthenticationPilotWorkflowExecutor(IClock clock,
        ILegacyAuthenticationPilotObserver legacy,
        IShiftProfileAuthenticationPilotObserver target)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _legacyDescriptor = PilotExecutorMapping.RequireReadOnlyDescriptor(legacy, "legacy-authentication");
        _targetDescriptor = PilotExecutorMapping.RequireReadOnlyDescriptor(target, "target-authentication");
        _service = new(clock, legacy, target);
    }

    public PilotFeature Feature => PilotFeature.AuthenticationPilot;
    public Type InputType => typeof(AuthenticationPilotInput);

    public async Task<PilotWorkflowAdapterExecution> ExecuteAsync(PilotHostRequest request,
        CancellationToken cancellationToken = default)
    {
        var input = (AuthenticationPilotInput)request.Input;
        var result = await _service.ObserveAsync(new(request.Context, request.Permit,
            input.ShiftProfileId), cancellationToken).ConfigureAwait(false);
        DateTimeOffset observed = _clock.UtcNow.ToUniversalTime();
        return new(result.Decision,
            PilotExecutorMapping.Observation(result.LegacyObservation?.ResultFingerprint,
                result.LegacyObservation?.ResultCategory, _legacyDescriptor, observed),
            PilotExecutorMapping.Observation(result.TargetObservation?.ResultFingerprint,
                result.TargetObservation?.ResultCategory, _targetDescriptor, observed),
            result.Evidence, result.Reasons, result.LegacyObservation is not null &&
                PilotExecutorMapping.TargetFailed(result.Reasons));
    }
}

public sealed class ReportingPilotWorkflowExecutor : IPilotWorkflowExecutor
{
    private readonly ReportingPilotService _service;
    private readonly IClock _clock;
    private readonly PilotAdapterDescriptor _legacyDescriptor;
    private readonly PilotAdapterDescriptor _targetDescriptor;

    public ReportingPilotWorkflowExecutor(IClock clock,
        ILegacyReportPilotObserver legacy,
        ITargetSnapshotPilotObserver target,
        IExportArtifactPilotValidator export)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _legacyDescriptor = PilotExecutorMapping.RequireReadOnlyDescriptor(legacy, "legacy-reporting");
        _targetDescriptor = PilotExecutorMapping.RequireReadOnlyDescriptor(target, "target-snapshot");
        _ = PilotExecutorMapping.RequireReadOnlyDescriptor(export, "target-export-validation");
        _service = new(clock, legacy, target, export);
    }

    public PilotFeature Feature => PilotFeature.ReportingPilot;
    public Type InputType => typeof(ReportingPilotInput);

    public async Task<PilotWorkflowAdapterExecution> ExecuteAsync(PilotHostRequest request,
        CancellationToken cancellationToken = default)
    {
        var input = (ReportingPilotInput)request.Input;
        var result = await _service.ObserveAsync(new(request.Context, request.Permit,
            input.ReportScope, input.SnapshotId), cancellationToken).ConfigureAwait(false);
        DateTimeOffset observed = _clock.UtcNow.ToUniversalTime();
        return new(result.Decision,
            PilotExecutorMapping.Observation(result.LegacyObservation?.ResultFingerprint,
                result.LegacyObservation?.ResultCategory, _legacyDescriptor, observed),
            PilotExecutorMapping.Observation(result.TargetObservation?.ResultFingerprint,
                result.TargetObservation?.ResultCategory, _targetDescriptor, observed),
            result.Evidence, result.Reasons, result.LegacyObservation is not null &&
                PilotExecutorMapping.TargetFailed(result.Reasons));
    }
}

public sealed class RuntimeEventPilotWorkflowExecutor : IPilotWorkflowExecutor
{
    private readonly RuntimeEventPilotService _service;
    private readonly IClock _clock;
    private readonly PilotAdapterDescriptor _legacyDescriptor;
    private readonly PilotAdapterDescriptor _targetDescriptor;

    public RuntimeEventPilotWorkflowExecutor(IClock clock,
        ILegacyRuntimeEventPilotObserver legacy,
        ITargetRuntimeEventPilotObserver target)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _legacyDescriptor = PilotExecutorMapping.RequireReadOnlyDescriptor(legacy, "legacy-runtime-event");
        _targetDescriptor = PilotExecutorMapping.RequireReadOnlyDescriptor(target, "target-runtime-event");
        _service = new(clock, legacy, target);
    }

    public PilotFeature Feature => PilotFeature.RuntimeEventPilot;
    public Type InputType => typeof(RuntimeEventPilotInput);

    public async Task<PilotWorkflowAdapterExecution> ExecuteAsync(PilotHostRequest request,
        CancellationToken cancellationToken = default)
    {
        var input = (RuntimeEventPilotInput)request.Input;
        var result = await _service.ObserveAsync(new(request.Context, request.Permit,
            input.ProjectionScope), cancellationToken).ConfigureAwait(false);
        DateTimeOffset observed = _clock.UtcNow.ToUniversalTime();
        string? legacyFingerprint = result.LegacyObservation is null ? null :
            PilotSafeFingerprint.Create(result.LegacyObservation.RuntimeFingerprint,
                result.LegacyObservation.EventFingerprint);
        string? targetFingerprint = result.TargetObservation is null ? null :
            PilotSafeFingerprint.Create(result.TargetObservation.RuntimeFingerprint,
                result.TargetObservation.EventFingerprint);
        return new(result.Decision,
            PilotExecutorMapping.Observation(legacyFingerprint,
                result.LegacyObservation?.ResultCategory, _legacyDescriptor, observed),
            PilotExecutorMapping.Observation(targetFingerprint,
                result.TargetObservation?.ResultCategory, _targetDescriptor, observed),
            result.Evidence, result.Reasons, result.LegacyObservation is not null &&
                PilotExecutorMapping.TargetFailed(result.Reasons));
    }
}

public sealed class ProtectedSettingsPilotWorkflowExecutor : IPilotWorkflowExecutor
{
    private readonly ProtectedSettingsPilotService _service;
    private readonly IClock _clock;
    private readonly PilotAdapterDescriptor _legacyDescriptor;
    private readonly PilotAdapterDescriptor _targetDescriptor;

    public ProtectedSettingsPilotWorkflowExecutor(IClock clock,
        ILegacySettingsPilotObserver legacy,
        ITargetProtectedSettingsPilotObserver target)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _legacyDescriptor = PilotExecutorMapping.RequireReadOnlyDescriptor(legacy, "legacy-settings");
        _targetDescriptor = PilotExecutorMapping.RequireReadOnlyDescriptor(target, "target-settings");
        _service = new(clock, legacy, target);
    }

    public PilotFeature Feature => PilotFeature.ProtectedSettingsPilot;
    public Type InputType => typeof(ProtectedSettingsPilotInput);

    public async Task<PilotWorkflowAdapterExecution> ExecuteAsync(PilotHostRequest request,
        CancellationToken cancellationToken = default)
    {
        var input = (ProtectedSettingsPilotInput)request.Input;
        var result = await _service.ObserveAsync(new(request.Context, request.Permit,
            input.SettingsScope, input.SettingsMutationRequested, input.TargetProvisioningRequested,
            input.EsdCutoverRequested), cancellationToken).ConfigureAwait(false);
        DateTimeOffset observed = _clock.UtcNow.ToUniversalTime();
        return new(result.Decision,
            PilotExecutorMapping.Observation(result.LegacyObservation?.ResultFingerprint,
                result.LegacyObservation?.ResultCategory, _legacyDescriptor, observed),
            PilotExecutorMapping.Observation(result.TargetObservation?.DecisionFingerprint,
                result.TargetObservation?.ResultCategory, _targetDescriptor, observed),
            result.Evidence, result.Reasons, result.LegacyObservation is not null &&
                PilotExecutorMapping.TargetFailed(result.Reasons));
    }
}

public sealed class ExportPilotWorkflowExecutor : IPilotWorkflowExecutor
{
    private readonly ExportPilotService _service;
    private readonly IClock _clock;
    private readonly PilotAdapterDescriptor _legacyDescriptor;
    private readonly PilotAdapterDescriptor _targetDescriptor;

    public ExportPilotWorkflowExecutor(IClock clock,
        ILegacyExportPilotObserver legacy,
        ITargetExportPilotObserver target)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _legacyDescriptor = PilotExecutorMapping.RequireReadOnlyDescriptor(legacy, "legacy-export");
        _targetDescriptor = PilotExecutorMapping.RequireReadOnlyDescriptor(target, "target-export");
        _service = new(clock, legacy, target);
    }

    public PilotFeature Feature => PilotFeature.ExportPilot;
    public Type InputType => typeof(ExportPilotInput);

    public async Task<PilotWorkflowAdapterExecution> ExecuteAsync(PilotHostRequest request,
        CancellationToken cancellationToken = default)
    {
        var input = (ExportPilotInput)request.Input;
        var result = await _service.ObserveAsync(new(request.Context, request.Permit,
            input.SnapshotId, input.ExportFormat), cancellationToken).ConfigureAwait(false);
        DateTimeOffset observed = _clock.UtcNow.ToUniversalTime();
        return new(result.Decision,
            PilotExecutorMapping.Observation(result.LegacyObservation?.ArtifactFingerprint,
                result.LegacyObservation?.ResultCategory, _legacyDescriptor, observed),
            PilotExecutorMapping.Observation(result.TargetObservation?.ArtifactFingerprint,
                result.TargetObservation?.ResultCategory, _targetDescriptor, observed),
            result.Evidence, result.Reasons, result.LegacyObservation is not null &&
                PilotExecutorMapping.TargetFailed(result.Reasons));
    }
}

internal static class PilotExecutorMapping
{
    public static PilotAdapterDescriptor RequireReadOnlyDescriptor(object adapter, string category)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        if (adapter is not IPilotAdapterDescriptorProvider provider ||
            string.IsNullOrWhiteSpace(provider.Descriptor.AdapterId) ||
            string.IsNullOrWhiteSpace(provider.Descriptor.AdapterVersion) ||
            string.IsNullOrWhiteSpace(provider.Descriptor.SourceVersion) ||
            !provider.Descriptor.ReadOnly || !provider.Descriptor.PreservesLegacyAuthority)
            throw new ArgumentException($"The {category} adapter lacks read-only pilot metadata.", nameof(adapter));
        return provider.Descriptor;
    }

    public static PilotObservationResult? Observation(string? fingerprint, string? safeStatus,
        PilotAdapterDescriptor descriptor, DateTimeOffset observedAtUtc) =>
        string.IsNullOrWhiteSpace(fingerprint) || string.IsNullOrWhiteSpace(safeStatus)
            ? null
            : new(fingerprint, NormalizeSafeCategory(safeStatus),
                new(descriptor.AdapterId, descriptor.AdapterVersion, descriptor.SourceVersion,
                    observedAtUtc, descriptor.ReadOnly, descriptor.PreservesLegacyAuthority));

    public static bool TargetFailed(IReadOnlyList<string> reasons) => reasons.Any(reason =>
        reason.EndsWith("pilot-observation-failed", StringComparison.Ordinal) ||
        reason.Contains("target", StringComparison.Ordinal) &&
        reason.EndsWith("observation-failed", StringComparison.Ordinal));

    private static string NormalizeSafeCategory(string value) => value.Length <= 80 &&
        value.All(character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-')
            ? value
            : "AdapterStatusUnavailable";
}
