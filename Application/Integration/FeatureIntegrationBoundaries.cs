using System.Collections.ObjectModel;
using Rah_Negar.Foundation.Application.Activation;
using Rah_Negar.Foundation.Application.Database.Readiness;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Foundation.Time;

namespace Rah_Negar.Foundation.Application.Integration;

public sealed record FeatureIntegrationApproval(
    ProductionActivationApproval ActivationApproval,
    ControlledIntegrationFeature ApprovedFeature,
    string ApprovedTargetScope);

public sealed record FeatureIntegrationActivationRequest(
    ActivationEvidencePackage EvidencePackage,
    FeatureIntegrationApproval? Approval,
    ControlledIntegrationFeature Feature,
    string TargetScope,
    string CorrelationId);

public interface IFeatureIntegrationActivationCoordinator
{
    FeatureIntegrationActivationDecision Evaluate(FeatureIntegrationActivationRequest request);
}

/// <summary>Evaluation only. This coordinator has no feature executor or configuration writer.</summary>
public sealed class FeatureIntegrationActivationCoordinator : IFeatureIntegrationActivationCoordinator
{
    private readonly IClock _clock;

    public FeatureIntegrationActivationCoordinator(IClock clock) =>
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public FeatureIntegrationActivationDecision Evaluate(FeatureIntegrationActivationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reasons = new List<string>();
        bool manualReview = request.EvidencePackage.Migration.Classification is
            MigrationHistoryClassification.HistoricalDraftRecognized or
            MigrationHistoryClassification.AdoptionRequired;
        bool knownFeature = Enum.IsDefined(request.Feature);
        if (!knownFeature) reasons.Add("unknown-integration-feature");
        if (string.IsNullOrWhiteSpace(request.TargetScope)) reasons.Add("target-scope-required");
        if (string.IsNullOrWhiteSpace(request.CorrelationId) ||
            !StringComparer.Ordinal.Equals(request.CorrelationId, request.EvidencePackage.CorrelationId))
            reasons.Add("correlation-binding-mismatch");
        ActivationEvidenceValidationResult evidence =
            ActivationEvidencePackageValidator.Validate(request.EvidencePackage);
        if (!evidence.IsComplete) reasons.Add("activation-evidence-incomplete");
        ProductionActivationScope? expectedScope = knownFeature ? MapScope(request.Feature) : null;
        if (expectedScope is { } requiredScope &&
            request.EvidencePackage.ApprovalBoundary.RequiredScope != requiredScope)
            reasons.Add("evidence-approval-scope-mismatch");
        if (request.Approval is null) reasons.Add("feature-approval-required");
        else if (expectedScope is { } approvalScope)
        {
            if (request.Approval.ApprovedFeature != request.Feature ||
                !StringComparer.Ordinal.Equals(request.Approval.ApprovedTargetScope, request.TargetScope))
                reasons.Add("feature-approval-scope-mismatch");
            ActivationApprovalValidationResult approval = ProductionActivationApprovalValidator.Validate(
                request.Approval.ActivationApproval, approvalScope,
                request.EvidencePackage.DatabaseIdentityFingerprint,
                request.EvidencePackage.EvidencePackageId,
                request.EvidencePackage.CorrelationId,
                _clock.UtcNow.ToUniversalTime());
            if (!approval.IsValid) reasons.Add($"feature-approval-{approval.ResultCategory}");
        }

        if (manualReview) reasons.Add("migration-adoption-requires-manual-review");
        return new(manualReview ? IntegrationControlDecision.RequiresManualReview :
                reasons.Count == 0 ? IntegrationControlDecision.Allowed : IntegrationControlDecision.Blocked,
            request.Feature, request.TargetScope, request.EvidencePackage.EvidencePackageId,
            request.CorrelationId, request.Approval?.ActivationApproval.ApprovalId,
            reasons.AsReadOnly());
    }

    public static ProductionActivationScope MapScope(ControlledIntegrationFeature feature) => feature switch
    {
        ControlledIntegrationFeature.Authentication => ProductionActivationScope.AuthenticationWorkflowActivation,
        ControlledIntegrationFeature.SnapshotReporting => ProductionActivationScope.SnapshotReportingActivation,
        ControlledIntegrationFeature.RuntimeProjection or ControlledIntegrationFeature.EventProjection =>
            ProductionActivationScope.RuntimeEventProjectionActivation,
        ControlledIntegrationFeature.ProtectedSettings => ProductionActivationScope.ProtectedSettingsActivation,
        ControlledIntegrationFeature.ReportExport => ProductionActivationScope.ReportExportActivation,
        ControlledIntegrationFeature.MigrationTooling => ProductionActivationScope.MigrationToolingActivation,
        _ => throw new ArgumentOutOfRangeException(nameof(feature))
    };
}

public sealed class PilotEnvironmentBoundary
{
    public PilotEnvironmentBoundary(
        string pilotId,
        bool isIsolated,
        string selectedStationId,
        IEnumerable<string> selectedShiftProfileIds,
        IEnumerable<ControlledIntegrationFeature> limitedFeatures,
        bool rollbackToLegacyRequired,
        string evidencePackageId,
        string correlationId)
    {
        PilotId = pilotId;
        IsIsolated = isIsolated;
        SelectedStationId = selectedStationId;
        SelectedShiftProfileIds = new ReadOnlyCollection<string>(selectedShiftProfileIds.Distinct(
            StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        LimitedFeatures = new ReadOnlyCollection<ControlledIntegrationFeature>(limitedFeatures.Distinct()
            .Order().ToArray());
        RollbackToLegacyRequired = rollbackToLegacyRequired;
        EvidencePackageId = evidencePackageId;
        CorrelationId = correlationId;
    }

    public string PilotId { get; }
    public bool IsIsolated { get; }
    public string SelectedStationId { get; }
    public IReadOnlyList<string> SelectedShiftProfileIds { get; }
    public IReadOnlyList<ControlledIntegrationFeature> LimitedFeatures { get; }
    public bool RollbackToLegacyRequired { get; }
    public bool ProductionRegistrationAllowed => false;
    public bool ActivationPerformed => false;
    public string EvidencePackageId { get; }
    public string CorrelationId { get; }
}

public static class PilotEnvironmentBoundaryValidator
{
    public static PilotBoundaryValidationResult Validate(PilotEnvironmentBoundary pilot)
    {
        ArgumentNullException.ThrowIfNull(pilot);
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(pilot.PilotId)) issues.Add("pilot-id-required");
        if (!pilot.IsIsolated) issues.Add("pilot-must-be-isolated");
        if (string.IsNullOrWhiteSpace(pilot.SelectedStationId)) issues.Add("pilot-station-required");
        if (pilot.SelectedShiftProfileIds.Count == 0 || pilot.SelectedShiftProfileIds.Any(string.IsNullOrWhiteSpace))
            issues.Add("pilot-shifts-required");
        if (pilot.LimitedFeatures.Count == 0 ||
            pilot.LimitedFeatures.Count >= Enum.GetValues<ControlledIntegrationFeature>().Length ||
            pilot.LimitedFeatures.Any(feature => !Enum.IsDefined(feature)) ||
            pilot.LimitedFeatures.Contains(ControlledIntegrationFeature.MigrationTooling))
            issues.Add("pilot-feature-set-must-be-limited");
        if (!pilot.RollbackToLegacyRequired) issues.Add("pilot-rollback-to-legacy-required");
        if (string.IsNullOrWhiteSpace(pilot.EvidencePackageId)) issues.Add("pilot-evidence-required");
        if (string.IsNullOrWhiteSpace(pilot.CorrelationId)) issues.Add("pilot-correlation-required");
        return new(issues.Count == 0, pilot.PilotId, pilot.RollbackToLegacyRequired,
            issues.AsReadOnly());
    }
}

public enum IntegrationActivationTrack
{
    Security,
    Reporting,
    RuntimeEvent,
    ProtectedSettings,
    Migration
}

public sealed record IntegrationDependencyNode(
    string DependencyId,
    IntegrationActivationTrack Track,
    int ActivationOrder,
    IReadOnlyList<string> RequiredPreviousPhases,
    IReadOnlyList<string> RequiredDependencies,
    IReadOnlyList<ProductionActivationScope> RequiredApprovals,
    IReadOnlyList<string> Blockers);

public sealed class IntegrationDependencyGraph
{
    public IntegrationDependencyGraph(IEnumerable<IntegrationDependencyNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        Nodes = new ReadOnlyCollection<IntegrationDependencyNode>(nodes.Select(node => node with
        {
            RequiredPreviousPhases = Array.AsReadOnly(node.RequiredPreviousPhases.ToArray()),
            RequiredDependencies = Array.AsReadOnly(node.RequiredDependencies.ToArray()),
            RequiredApprovals = Array.AsReadOnly(node.RequiredApprovals.ToArray()),
            Blockers = Array.AsReadOnly(node.Blockers.ToArray())
        }).OrderBy(node => node.ActivationOrder).ToArray());
    }

    public IReadOnlyList<IntegrationDependencyNode> Nodes { get; }

    public static IntegrationDependencyGraph CreateDefault() => new(
        [
            Node("migration-readiness", IntegrationActivationTrack.Migration, 10,
                ["7.8", "7.9", "8.0"], [], [ProductionActivationScope.UnifiedMigrationActivation],
                ["backup", "rehearsal", "integrity", "approval"]),
            Node("security-persistence", IntegrationActivationTrack.Security, 20,
                ["7.5", "7.7", "8.0"], ["migration-readiness"],
                [ProductionActivationScope.AuthenticationWorkflowActivation],
                ["ShiftProfile provisioning", "credential recovery", "approval"]),
            Node("snapshot-validation", IntegrationActivationTrack.Reporting, 30,
                ["5", "7.9"], ["migration-readiness"],
                [ProductionActivationScope.SnapshotReportingActivation],
                ["snapshot comparison", "export validation", "read routing"]),
            Node("runtime-event-validation", IntegrationActivationTrack.RuntimeEvent, 40,
                ["4", "7.9"], ["migration-readiness"],
                [ProductionActivationScope.RuntimeEventProjectionActivation],
                ["Runtime comparison", "Event comparison", "read-only pilot"]),
            Node("protected-settings-validation", IntegrationActivationTrack.ProtectedSettings, 50,
                ["7.5", "7.6", "7.7"], ["security-persistence"],
                [ProductionActivationScope.ProtectedSettingsActivation],
                ["management proof", "vendor authorization", "replay", "separate ESD cutover"])
        ]);

    private static IntegrationDependencyNode Node(string id, IntegrationActivationTrack track, int order,
        IReadOnlyList<string> phases, IReadOnlyList<string> dependencies,
        IReadOnlyList<ProductionActivationScope> approvals, IReadOnlyList<string> blockers) =>
        new(id, track, order, phases, dependencies, approvals, blockers);
}

public static class IntegrationDependencyGraphValidator
{
    public static IReadOnlyList<string> Validate(IntegrationDependencyGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var issues = new List<string>();
        if (graph.Nodes.Select(node => node.DependencyId).Distinct(StringComparer.Ordinal).Count() != graph.Nodes.Count)
            issues.Add("duplicate-dependency-id");
        Dictionary<string, IntegrationDependencyNode> byId = graph.Nodes
            .GroupBy(node => node.DependencyId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (IntegrationDependencyNode node in graph.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.DependencyId) || node.ActivationOrder <= 0)
                issues.Add("invalid-dependency-node");
            foreach (string required in node.RequiredDependencies)
                if (!byId.TryGetValue(required, out IntegrationDependencyNode? predecessor) ||
                    predecessor.ActivationOrder >= node.ActivationOrder)
                    issues.Add($"invalid-dependency-order:{node.DependencyId}:{required}");
            if (node.RequiredPreviousPhases.Count == 0 || node.RequiredApprovals.Count == 0)
                issues.Add($"incomplete-dependency-evidence:{node.DependencyId}");
        }
        return issues.AsReadOnly();
    }
}

public enum IntegrationMonitoringSignalKind
{
    AuthenticationComparison,
    ReportComparison,
    RuntimeEventDifference,
    SecurityFailure,
    MigrationStatus,
    RollbackReadiness
}

public sealed record IntegrationMonitoringSignal(
    string SignalId,
    IntegrationMonitoringSignalKind Kind,
    ShadowDifferenceSeverity Severity,
    string EvidenceId,
    string CorrelationId,
    string TargetScope,
    DateTimeOffset ObservedAtUtc,
    string ResultCategory);

public interface IIntegrationMonitoringSink
{
    Task RecordAsync(IntegrationMonitoringSignal signal,
        CancellationToken cancellationToken = default);
}

public sealed class IntegrationMonitoringPlan
{
    public IntegrationMonitoringPlan(IEnumerable<IntegrationMonitoringSignalKind> requiredSignals,
        string monitoringOwnerReference, string rollbackEscalationReference)
    {
        RequiredSignals = new ReadOnlyCollection<IntegrationMonitoringSignalKind>(requiredSignals
            .Distinct().Order().ToArray());
        MonitoringOwnerReference = monitoringOwnerReference;
        RollbackEscalationReference = rollbackEscalationReference;
    }

    public IReadOnlyList<IntegrationMonitoringSignalKind> RequiredSignals { get; }
    public string MonitoringOwnerReference { get; }
    public string RollbackEscalationReference { get; }

    public bool IsComplete => RequiredSignals.SequenceEqual(Enum.GetValues<IntegrationMonitoringSignalKind>()) &&
        !string.IsNullOrWhiteSpace(MonitoringOwnerReference) &&
        !string.IsNullOrWhiteSpace(RollbackEscalationReference);
}
