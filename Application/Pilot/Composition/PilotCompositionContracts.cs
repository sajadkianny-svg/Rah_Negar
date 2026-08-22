using System.Collections.ObjectModel;
using Rah_Negar.Foundation.Application.Pilot.Presentation;

namespace Rah_Negar.Foundation.Application.Pilot.Composition;

public enum PilotStateAvailability
{
    Available,
    Unavailable,
    Failed
}

public enum PilotCompositionStatus
{
    Created,
    Blocked,
    Failed
}

public enum PilotBindingLifecycleState
{
    Created,
    Attaching,
    Attached,
    Detached,
    Failed,
    Disposed
}

public enum PilotBindingOperationStatus
{
    Attached,
    Detached,
    Blocked,
    Failed,
    Canceled,
    Disposed
}

public sealed class PilotCapabilityEvidence
{
    public PilotCapabilityEvidence(
        string pilotId,
        string correlationId,
        IEnumerable<string> availableCapabilities,
        DateTimeOffset observedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(availableCapabilities);
        PilotId = pilotId;
        CorrelationId = correlationId;
        AvailableCapabilities = new ReadOnlyCollection<string>(availableCapabilities
            .Where(PilotCompositionText.IsSafeIdentifier)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        ObservedAtUtc = observedAtUtc;
    }

    public string PilotId { get; }
    public string CorrelationId { get; }
    public IReadOnlyList<string> AvailableCapabilities { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public bool IsReadOnly => true;
    public bool ImplementsRbac => false;
    public bool CreatesPermissions => false;
}

public sealed class PilotCompositionContext
{
    public PilotCompositionContext(
        string compositionId,
        string pilotId,
        string correlationId,
        string surfaceId,
        string stateSourceId,
        bool explicitlyApproved,
        DateTimeOffset approvedAtUtc,
        DateTimeOffset expiresAtUtc,
        DateTimeOffset evaluationTimeUtc,
        PilotCapabilityEvidence capabilityEvidence)
    {
        CompositionId = compositionId;
        PilotId = pilotId;
        CorrelationId = correlationId;
        SurfaceId = surfaceId;
        StateSourceId = stateSourceId;
        ExplicitlyApproved = explicitlyApproved;
        ApprovedAtUtc = approvedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        EvaluationTimeUtc = evaluationTimeUtc;
        CapabilityEvidence = capabilityEvidence;
    }

    public string CompositionId { get; }
    public string PilotId { get; }
    public string CorrelationId { get; }
    public string SurfaceId { get; }
    public string StateSourceId { get; }
    public bool ExplicitlyApproved { get; }
    public DateTimeOffset ApprovedAtUtc { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
    public DateTimeOffset EvaluationTimeUtc { get; }
    public PilotCapabilityEvidence CapabilityEvidence { get; }
    public bool AutomaticallyActivates => false;
    public bool AllowsExecution => false;
    public bool AllowsAuthoritySwitch => false;
    public bool FallsBackToProduction => false;
}

public sealed class PilotSurfaceDescriptor
{
    public PilotSurfaceDescriptor(
        string surfaceId,
        string safeName,
        PilotUiSurfaceKind surfaceKind,
        bool readOnly,
        bool automaticallyOpens,
        bool supportsCommands)
    {
        SurfaceId = PilotCompositionText.SafeIdentifier(surfaceId, "surface-unavailable");
        SafeName = PilotCompositionText.SafeLabel(safeName, "Pilot surface");
        SurfaceKind = surfaceKind;
        ReadOnly = readOnly;
        AutomaticallyOpens = automaticallyOpens;
        SupportsCommands = supportsCommands;
    }

    public string SurfaceId { get; }
    public string SafeName { get; }
    public PilotUiSurfaceKind SurfaceKind { get; }
    public bool ReadOnly { get; }
    public bool AutomaticallyOpens { get; }
    public bool SupportsCommands { get; }
    public bool ReplacesLegacyShell => false;
    public bool ReplacesLogin => false;
    public bool ReplacesSettings => false;
    public bool ReplacesReportingAuthority => false;
    public bool ReplacesRuntimeEventAuthority => false;
}

public sealed class PilotStateSourceDescriptor
{
    public PilotStateSourceDescriptor(
        string sourceId,
        string safeName,
        PilotStateAvailability availability,
        bool readOnly,
        bool executesWorkflows,
        DateTimeOffset observedAtUtc,
        IEnumerable<KeyValuePair<string, string>>? safeMetadata = null)
    {
        SourceId = PilotCompositionText.SafeIdentifier(sourceId, "source-unavailable");
        SafeName = PilotCompositionText.SafeLabel(safeName, "Pilot state source");
        Availability = availability;
        ReadOnly = readOnly;
        ExecutesWorkflows = executesWorkflows;
        ObservedAtUtc = observedAtUtc;
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> item in safeMetadata ?? [])
        {
            if (PilotCompositionText.IsSafeIdentifier(item.Key) &&
                PilotCompositionText.IsSafeIdentifier(item.Value))
                metadata[item.Key] = item.Value;
        }
        SafeMetadata = new ReadOnlyDictionary<string, string>(metadata);
    }

    public string SourceId { get; }
    public string SafeName { get; }
    public PilotStateAvailability Availability { get; }
    public bool ReadOnly { get; }
    public bool ExecutesWorkflows { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public IReadOnlyDictionary<string, string> SafeMetadata { get; }
    public bool AccessesUiControls => false;
    public bool AccessesProductionForms => false;
    public bool WritesDatabase => false;
}

public interface IPilotDashboardStateProvider
{
    PilotStateSourceDescriptor Descriptor { get; }

    ValueTask<PilotDashboardState?> GetDashboardStateAsync(
        PilotCompositionContext context,
        CancellationToken cancellationToken = default);
}

public sealed class PilotCompositionResult
{
    internal PilotCompositionResult(
        PilotCompositionStatus status,
        string reasonCode,
        PilotSurfaceBinding? binding)
    {
        Status = status;
        ReasonCode = reasonCode;
        Binding = binding;
    }

    public PilotCompositionStatus Status { get; }
    public string ReasonCode { get; }
    public PilotSurfaceBinding? Binding { get; }
    public bool IsCreated => Status == PilotCompositionStatus.Created && Binding is not null;
    public bool ActivatedProduction => false;
    public bool SwitchedAuthority => false;
}

public sealed record PilotBindingOperationResult(
    PilotBindingOperationStatus Status,
    string ReasonCode)
{
    public bool IsAttached => Status == PilotBindingOperationStatus.Attached;
    public bool ExecutedWorkflow => false;
    public bool ActivatedFeature => false;
    public bool SwitchedAuthority => false;
}

internal static class PilotCompositionText
{
    private const int MaximumIdentifierLength = 128;
    private const int MaximumLabelLength = 128;
    private static readonly string[] ForbiddenFragments =
    [
        "password", "passwd", "pwd", "credential", "secret", "private key", "private-key",
        "signature", "exception", "stack trace", "stack-trace", "sqlite", "sql-error",
        "select", "insert", "update", "delete", "drop", "alter", "pragma", "attach",
        "connection string", "authorization"
    ];

    public static bool IsSafeIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= MaximumIdentifierLength &&
        value.All(character => char.IsAsciiLetterOrDigit(character) ||
            character is '-' or '_' or '.' or ':') &&
        !ForbiddenFragments.Any(fragment => value.Contains(fragment,
            StringComparison.OrdinalIgnoreCase));

    public static string SafeIdentifier(string? value, string fallback) =>
        IsSafeIdentifier(value) ? value! : fallback;

    public static string SafeLabel(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        string candidate = value.Trim();
        if (candidate.Length > MaximumLabelLength || candidate.Any(char.IsControl) ||
            candidate.Contains('/') || candidate.Contains('\\') ||
            ForbiddenFragments.Any(fragment => candidate.Contains(fragment,
                StringComparison.OrdinalIgnoreCase))) return fallback;
        return candidate;
    }
}
