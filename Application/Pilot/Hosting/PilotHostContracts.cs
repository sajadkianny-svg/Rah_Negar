using System.Collections.ObjectModel;
using Rah_Negar.Foundation.Application.Integration;

namespace Rah_Negar.Foundation.Application.Pilot.Hosting;

public abstract record PilotWorkflowInput;

public sealed record AuthenticationPilotInput(string ShiftProfileId) : PilotWorkflowInput;

public sealed record ReportingPilotInput(string ReportScope, string SnapshotId) : PilotWorkflowInput;

public sealed record RuntimeEventPilotInput(string ProjectionScope) : PilotWorkflowInput;

public sealed record ProtectedSettingsPilotInput(
    string SettingsScope,
    bool SettingsMutationRequested = false,
    bool TargetProvisioningRequested = false,
    bool EsdCutoverRequested = false) : PilotWorkflowInput;

public sealed record ExportPilotInput(string SnapshotId, string ExportFormat) : PilotWorkflowInput;

public sealed record PilotHostRequest(
    PilotExecutionContext Context,
    PilotExecutionPermit? Permit,
    PilotFeature Feature,
    PilotWorkflowInput Input);

public sealed record PilotAdapterDescriptor(
    string AdapterId,
    string AdapterVersion,
    string SourceVersion,
    bool ReadOnly,
    bool PreservesLegacyAuthority);

public interface IPilotAdapterDescriptorProvider
{
    PilotAdapterDescriptor Descriptor { get; }
}

public sealed record PilotAdapterEvidenceMetadata(
    string AdapterId,
    string AdapterVersion,
    string SourceVersion,
    DateTimeOffset ObservedAtUtc,
    bool ReadOnly,
    bool PreservesLegacyAuthority);

public sealed record PilotObservationResult(
    string ResultFingerprint,
    string SafeStatus,
    PilotAdapterEvidenceMetadata Metadata);

public sealed class PilotComparisonResult
{
    public PilotComparisonResult(
        bool isMatch,
        ShadowDifferenceSeverity severity,
        string safeSummary,
        IEnumerable<string> differences)
    {
        IsMatch = isMatch;
        Severity = severity;
        SafeSummary = safeSummary;
        Differences = new ReadOnlyCollection<string>((differences ??
            throw new ArgumentNullException(nameof(differences)))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }

    public bool IsMatch { get; }
    public ShadowDifferenceSeverity Severity { get; }
    public string SafeSummary { get; }
    public IReadOnlyList<string> Differences { get; }
}

public enum PilotExecutionStatus
{
    Completed,
    CompletedWithDifference,
    Blocked,
    TargetFailed,
    Failed
}

public sealed class PilotExecutionResult
{
    public PilotExecutionResult(
        string pilotId,
        PilotFeature feature,
        PilotExecutionStatus status,
        PilotObservationResult? legacyResult,
        PilotObservationResult? targetResult,
        PilotComparisonResult comparison,
        string? evidenceId,
        string correlationId,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        IEnumerable<string> blockedReasons)
    {
        PilotId = pilotId;
        Feature = feature;
        Status = status;
        LegacyResult = legacyResult;
        TargetResult = targetResult;
        Comparison = comparison ?? throw new ArgumentNullException(nameof(comparison));
        EvidenceId = evidenceId;
        CorrelationId = correlationId;
        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = completedAtUtc;
        BlockedReasons = new ReadOnlyCollection<string>((blockedReasons ??
            throw new ArgumentNullException(nameof(blockedReasons)))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }

    public string PilotId { get; }
    public PilotFeature Feature { get; }
    public PilotExecutionStatus Status { get; }
    public PilotObservationResult? LegacyResult { get; }
    public PilotObservationResult? TargetResult { get; }
    public PilotComparisonResult Comparison { get; }
    public ShadowDifferenceSeverity Severity => Comparison.Severity;
    public string? EvidenceId { get; }
    public string CorrelationId { get; }
    public DateTimeOffset StartedAtUtc { get; }
    public DateTimeOffset CompletedAtUtc { get; }
    public IReadOnlyList<string> BlockedReasons { get; }
    public bool LegacyAuthorityPreserved => true;
    public bool ProductionMutationAllowed => false;
    public bool AuthoritySwitchPerformed => false;
}

public sealed record PilotWorkflowAdapterExecution(
    IntegrationControlDecision Decision,
    PilotObservationResult? Legacy,
    PilotObservationResult? Target,
    PilotEvidenceRecord? Evidence,
    IReadOnlyList<string> Reasons,
    bool TargetFailed);

public interface IPilotWorkflowExecutor
{
    PilotFeature Feature { get; }
    Type InputType { get; }
    Task<PilotWorkflowAdapterExecution> ExecuteAsync(
        PilotHostRequest request,
        CancellationToken cancellationToken = default);
}

public interface IPilotHost
{
    Task<PilotExecutionResult> ExecuteAsync(
        PilotHostRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PilotHostPresentation(
    string PilotId,
    PilotFeature Feature,
    PilotExecutionStatus Status,
    string ComparisonSummary,
    ShadowDifferenceSeverity Severity,
    string? EvidenceId,
    PilotEvidenceState EvidenceState,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> BlockedReasons,
    string CorrelationId);

/// <summary>Future UI consumption boundary only; Phase 8.3 has no WinForms implementation.</summary>
public interface IPilotHostPresenter
{
    Task PresentAsync(PilotHostPresentation presentation,
        CancellationToken cancellationToken = default);
}
