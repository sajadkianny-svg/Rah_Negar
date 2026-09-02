using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Rah_Negar.Foundation.Application.Activation.Preparation;
using Rah_Negar.Foundation.Application.Pilot.Validation;

namespace Rah_Negar.Foundation.Application.Pilot.Operational;

public enum OperationalRunbookStepKind
{
    Preflight,
    Approve,
    Start,
    Observe,
    Compare,
    Review,
    Complete,
    Stop,
    RollbackRequestEvidence
}

public sealed record ControlledPilotOperationalRunbookStep(
    OperationalRunbookStepKind Kind,
    string StepId,
    string ExpectedOutcomeCode);

public sealed class ControlledPilotOperationalRunbookDefinition
{
    public ControlledPilotOperationalRunbookDefinition(
        string version,
        IEnumerable<ControlledPilotOperationalRunbookStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        Version = OperationalText.SafeIdentifier(version, "runbook-version-unavailable");
        ControlledPilotOperationalRunbookStep[] supplied = steps.Select(step =>
            new ControlledPilotOperationalRunbookStep(
            step.Kind,
            OperationalText.SafeIdentifier(step.StepId, "runbook-step-unavailable"),
            OperationalText.SafeIdentifier(step.ExpectedOutcomeCode,
                "runbook-outcome-unavailable"))).ToArray();
        if (supplied.Length != Enum.GetValues<OperationalRunbookStepKind>().Length ||
            supplied.GroupBy(step => step.Kind).Any(group => group.Count() != 1) ||
            supplied.GroupBy(step => step.StepId, StringComparer.Ordinal)
                .Any(group => group.Count() != 1))
            throw new ArgumentException("Operational runbook must define every unique step.",
                nameof(steps));
        Steps = new ReadOnlyCollection<ControlledPilotOperationalRunbookStep>(supplied);
    }

    public string Version { get; }
    public IReadOnlyList<ControlledPilotOperationalRunbookStep> Steps { get; }
    public bool AutomatesDestructiveActions => false;

    public static ControlledPilotOperationalRunbookDefinition Standard { get; } = new(
        "operational-runbook-v1",
        [
            new(OperationalRunbookStepKind.Preflight, "OPR-01-PREFLIGHT", "readiness-evaluated"),
            new(OperationalRunbookStepKind.Approve, "OPR-02-APPROVE", "approval-confirmed"),
            new(OperationalRunbookStepKind.Start, "OPR-03-START", "session-started"),
            new(OperationalRunbookStepKind.Observe, "OPR-04-OBSERVE", "read-only-observations-recorded"),
            new(OperationalRunbookStepKind.Compare, "OPR-05-COMPARE", "fingerprints-compared"),
            new(OperationalRunbookStepKind.Review, "OPR-06-REVIEW", "operator-decision-recorded"),
            new(OperationalRunbookStepKind.Complete, "OPR-07-COMPLETE", "rehearsal-completed"),
            new(OperationalRunbookStepKind.Stop, "OPR-08-STOP", "rehearsal-session-stopped"),
            new(OperationalRunbookStepKind.RollbackRequestEvidence,
                "OPR-09-ROLLBACK-REQUEST", "rollback-request-recorded-only")
        ]);
}

public sealed class ControlledPilotOperationalRunbookStatus
{
    public ControlledPilotOperationalRunbookStatus(
        string runbookVersion,
        IEnumerable<string> completedStepIds,
        string currentStepId,
        bool terminal)
    {
        ArgumentNullException.ThrowIfNull(completedStepIds);
        RunbookVersion = OperationalText.SafeIdentifier(runbookVersion,
            "runbook-version-unavailable");
        CompletedStepIds = OperationalCollections.SafeSortedIdentifiers(completedStepIds);
        CurrentStepId = OperationalText.SafeIdentifier(currentStepId,
            "runbook-step-unavailable");
        Terminal = terminal;
    }

    public string RunbookVersion { get; }
    public IReadOnlyList<string> CompletedStepIds { get; }
    public string CurrentStepId { get; }
    public bool Terminal { get; }
}

public enum OperationalOperatorDecisionKind
{
    Complete,
    Stop,
    RequestRollback
}

public sealed class ControlledPilotOperationalOperatorDecision
{
    public ControlledPilotOperationalOperatorDecision(
        string decisionId,
        OperationalOperatorDecisionKind kind,
        string evidenceReference,
        DateTimeOffset decidedAtUtc)
    {
        DecisionId = OperationalText.SafeIdentifier(decisionId, "decision-unavailable");
        Kind = kind;
        EvidenceReference = OperationalText.SafeIdentifier(evidenceReference,
            "decision-evidence-unavailable");
        DecidedAtUtc = decidedAtUtc;
    }

    public string DecisionId { get; }
    public OperationalOperatorDecisionKind Kind { get; }
    public string EvidenceReference { get; }
    public DateTimeOffset DecidedAtUtc { get; }
    public bool ExecutesRollback => false;
    public bool StopsProduction => false;
}

public enum ControlledPilotOperationalStopReason
{
    ObserverFailure,
    FingerprintMismatchAbovePolicy,
    EvidenceIntegrityFailure,
    RollbackReadinessLost,
    SecurityBoundaryViolation,
    Cancellation,
    ExplicitOperatorStop,
    RollbackRequested
}

public sealed class ControlledPilotOperationalStopDecision
{
    internal ControlledPilotOperationalStopDecision(
        ControlledPilotOperationalStopReason reason,
        string reasonCode,
        string evidenceReference,
        DateTimeOffset decidedAtUtc)
    {
        Reason = reason;
        ReasonCode = reasonCode;
        EvidenceReference = OperationalText.SafeIdentifier(evidenceReference,
            "stop-evidence-unavailable");
        DecidedAtUtc = decidedAtUtc;
    }

    public ControlledPilotOperationalStopReason Reason { get; }
    public string ReasonCode { get; }
    public string EvidenceReference { get; }
    public DateTimeOffset DecidedAtUtc { get; }
    public bool AutomaticallyStopsProduction => false;
    public bool StopsRehearsalOnly => true;
    public bool ExecutesRollback => false;
}

public sealed record ControlledPilotOperationalStopEvaluation(
    IReadOnlyList<ControlledPilotOperationalWorkflowResult> WorkflowResults,
    int AllowedFingerprintDifferences,
    bool EvidenceIntegrityValid,
    bool RollbackReady,
    bool SecurityBoundaryViolated,
    bool CancellationRequested,
    ControlledPilotOperationalOperatorDecision? OperatorDecision,
    string EvidenceReference,
    DateTimeOffset EvaluatedAtUtc);

public sealed class ControlledPilotOperationalStopEvaluator
{
    public ControlledPilotOperationalStopDecision? Evaluate(
        ControlledPilotOperationalStopEvaluation input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.SecurityBoundaryViolated)
            return Stop(ControlledPilotOperationalStopReason.SecurityBoundaryViolation,
                "operational-stop-security-boundary", input);
        if (!input.EvidenceIntegrityValid)
            return Stop(ControlledPilotOperationalStopReason.EvidenceIntegrityFailure,
                "operational-stop-evidence-integrity", input);
        if (!input.RollbackReady)
            return Stop(ControlledPilotOperationalStopReason.RollbackReadinessLost,
                "operational-stop-rollback-readiness-lost", input);
        if (input.CancellationRequested)
            return Stop(ControlledPilotOperationalStopReason.Cancellation,
                "operational-stop-canceled", input);
        if (input.WorkflowResults.Any(result =>
                result.Status == OperationalWorkflowComparisonStatus.Failed))
            return Stop(ControlledPilotOperationalStopReason.ObserverFailure,
                "operational-stop-observer-failure", input);
        if (input.WorkflowResults.Sum(result => result.SemanticDifferenceCount) >
            input.AllowedFingerprintDifferences)
            return Stop(ControlledPilotOperationalStopReason.FingerprintMismatchAbovePolicy,
                "operational-stop-fingerprint-policy", input);
        if (input.OperatorDecision?.Kind == OperationalOperatorDecisionKind.RequestRollback)
            return Stop(ControlledPilotOperationalStopReason.RollbackRequested,
                "operational-stop-rollback-requested", input);
        if (input.OperatorDecision?.Kind == OperationalOperatorDecisionKind.Stop)
            return Stop(ControlledPilotOperationalStopReason.ExplicitOperatorStop,
                "operational-stop-operator", input);
        return null;
    }

    private static ControlledPilotOperationalStopDecision Stop(
        ControlledPilotOperationalStopReason reason,
        string code,
        ControlledPilotOperationalStopEvaluation input) => new(reason, code,
            input.EvidenceReference, input.EvaluatedAtUtc);
}

public enum ControlledPilotOperationalHealthStatus
{
    Healthy,
    AttentionRequired,
    Failed,
    Stopped
}

public sealed class ControlledPilotOperationalMonitoringEvidence
{
    public ControlledPilotOperationalMonitoringEvidence(
        ControlledPilotOperationalHealthStatus status,
        IEnumerable<string> signalCodes,
        string evidenceReference,
        DateTimeOffset observedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(signalCodes);
        Status = status;
        SignalCodes = OperationalCollections.SafeSortedIdentifiers(signalCodes);
        EvidenceReference = OperationalText.SafeIdentifier(evidenceReference,
            "monitoring-evidence-unavailable");
        ObservedAtUtc = observedAtUtc;
    }

    public ControlledPilotOperationalHealthStatus Status { get; }
    public IReadOnlyList<string> SignalCodes { get; }
    public string EvidenceReference { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public bool ContainsRawLogs => false;
    public bool StartsTelemetry => false;
}

public static class ControlledPilotOperationalMonitoringFactory
{
    public static ControlledPilotOperationalMonitoringEvidence Create(
        IReadOnlyCollection<ControlledPilotOperationalWorkflowResult> results,
        bool rollbackReady,
        DateTimeOffset observedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(results);
        bool failure = results.Any(result => result.Status ==
            OperationalWorkflowComparisonStatus.Failed);
        bool difference = results.Any(result => result.Status ==
            OperationalWorkflowComparisonStatus.Difference);
        ControlledPilotOperationalHealthStatus status = failure
            ? ControlledPilotOperationalHealthStatus.Failed
            : difference || !rollbackReady
                ? ControlledPilotOperationalHealthStatus.AttentionRequired
                : ControlledPilotOperationalHealthStatus.Healthy;
        var signals = new List<string>
        {
            failure ? "observer-failure" : "observers-complete",
            difference ? "fingerprint-difference" : "fingerprints-match",
            rollbackReady ? "rollback-ready" : "rollback-not-ready"
        };
        return new(status, signals, "operational-monitoring-evidence", observedAtUtc);
    }
}

public sealed record ControlledPilotOperationalContextIdentity(
    string RehearsalId,
    string PilotId,
    string SessionId,
    string CorrelationId,
    string ReleaseId,
    string StationScope,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    IReadOnlyList<PilotValidationWorkflow> SelectedWorkflows,
    string OperatorReference,
    string Phase9PreparationEvidenceReference,
    string RollbackEvidenceReference,
    bool ExplicitApproval);

public sealed record ControlledPilotFingerprintComparison(
    PilotValidationWorkflow Workflow,
    OperationalWorkflowComparisonStatus Status,
    string LegacyFingerprint,
    string TargetFingerprint,
    int SemanticDifferenceCount);

public sealed class ControlledPilotOperationalEvidenceBundle
{
    internal ControlledPilotOperationalEvidenceBundle(
        ControlledPilotOperationalContextIdentity contextIdentity,
        ControlledPilotOperationalPreflightResult preflightResult,
        IEnumerable<ControlledPilotOperationalWorkflowResult> workflowResults,
        IEnumerable<KeyValuePair<PilotValidationWorkflow, string>> fingerprintVersions,
        IEnumerable<ControlledPilotFingerprintComparison> comparisons,
        ControlledPilotOperationalMonitoringEvidence monitoringEvidence,
        ControlledPilotOperationalStopDecision? stopDecision,
        ControlledPilotOperationalRunbookStatus runbookCompletionStatus,
        RollbackEvidenceStatus rollbackReadiness,
        DateTimeOffset completionTimestampUtc,
        string bundleChecksum)
    {
        ContextIdentity = contextIdentity;
        PreflightResult = preflightResult;
        WorkflowResults = OperationalCollections.ReadOnly(workflowResults
            .OrderBy(result => result.Workflow));
        FingerprintVersions = new ReadOnlyDictionary<PilotValidationWorkflow, string>(
            fingerprintVersions.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key,
                pair => pair.Value));
        Comparisons = OperationalCollections.ReadOnly(comparisons
            .OrderBy(comparison => comparison.Workflow));
        MonitoringEvidence = monitoringEvidence;
        StopDecision = stopDecision;
        RunbookCompletionStatus = runbookCompletionStatus;
        RollbackReadiness = rollbackReadiness;
        CompletionTimestampUtc = completionTimestampUtc;
        BundleChecksum = FingerprintSafety.SafeSha256(bundleChecksum);
    }

    public ControlledPilotOperationalContextIdentity ContextIdentity { get; }
    public ControlledPilotOperationalPreflightResult PreflightResult { get; }
    public IReadOnlyList<ControlledPilotOperationalWorkflowResult> WorkflowResults { get; }
    public IReadOnlyDictionary<PilotValidationWorkflow, string> FingerprintVersions { get; }
    public IReadOnlyList<ControlledPilotFingerprintComparison> Comparisons { get; }
    public ControlledPilotOperationalMonitoringEvidence MonitoringEvidence { get; }
    public ControlledPilotOperationalStopDecision? StopDecision { get; }
    public ControlledPilotOperationalRunbookStatus RunbookCompletionStatus { get; }
    public RollbackEvidenceStatus RollbackReadiness { get; }
    public DateTimeOffset CompletionTimestampUtc { get; }
    public string BundleChecksum { get; }
    public bool ContainsSensitiveMaterial => false;
    public bool IsImmutable => true;
    public bool ChangesAuthority => false;
    public bool HasValidChecksum =>
        ControlledPilotOperationalEvidenceBundleFactory.Verify(this);
}

internal static class ControlledPilotOperationalEvidenceBundleFactory
{
    public static ControlledPilotOperationalEvidenceBundle Create(
        ControlledPilotOperationalRehearsalContext context,
        ControlledPilotOperationalPreflightResult preflight,
        IEnumerable<ControlledPilotOperationalWorkflowResult> workflowResults,
        ControlledPilotOperationalMonitoringEvidence monitoring,
        ControlledPilotOperationalStopDecision? stopDecision,
        ControlledPilotOperationalRunbookStatus runbook,
        RollbackEvidenceStatus rollbackReadiness,
        DateTimeOffset completedAtUtc)
    {
        ControlledPilotOperationalWorkflowResult[] results = workflowResults
            .OrderBy(result => result.Workflow).ToArray();
        var identity = new ControlledPilotOperationalContextIdentity(
            context.RehearsalId, context.PilotId, context.SessionId, context.CorrelationId,
            context.ReleaseId, context.StationScope.ToString(), context.StartUtc, context.EndUtc,
            new ReadOnlyCollection<PilotValidationWorkflow>(context.SelectedWorkflows.ToArray()),
            context.OperatorReference, context.Phase9PreparationEvidenceReference,
            context.RollbackEvidenceReference, context.ExplicitApproval);
        KeyValuePair<PilotValidationWorkflow, string>[] versions = results.Select(result =>
            KeyValuePair.Create(result.Workflow, result.FingerprintSpecificationVersion)).ToArray();
        ControlledPilotFingerprintComparison[] comparisons = results.Select(result => new
            ControlledPilotFingerprintComparison(result.Workflow, result.Status,
                result.LegacyFingerprint, result.TargetFingerprint,
                result.SemanticDifferenceCount)).ToArray();
        string checksum = CalculateChecksum(identity, preflight, results, versions, comparisons,
            monitoring, stopDecision, runbook, rollbackReadiness, completedAtUtc);
        return new(identity, preflight, results, versions, comparisons, monitoring,
            stopDecision, runbook, rollbackReadiness, completedAtUtc, checksum);
    }

    public static bool Verify(ControlledPilotOperationalEvidenceBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        string expected = CalculateChecksum(bundle.ContextIdentity, bundle.PreflightResult,
            bundle.WorkflowResults, bundle.FingerprintVersions, bundle.Comparisons,
            bundle.MonitoringEvidence, bundle.StopDecision, bundle.RunbookCompletionStatus,
            bundle.RollbackReadiness, bundle.CompletionTimestampUtc);
        return StringComparer.Ordinal.Equals(expected, bundle.BundleChecksum);
    }

    private static string CalculateChecksum(
        ControlledPilotOperationalContextIdentity identity,
        ControlledPilotOperationalPreflightResult preflight,
        IEnumerable<ControlledPilotOperationalWorkflowResult> results,
        IEnumerable<KeyValuePair<PilotValidationWorkflow, string>> versions,
        IEnumerable<ControlledPilotFingerprintComparison> comparisons,
        ControlledPilotOperationalMonitoringEvidence monitoring,
        ControlledPilotOperationalStopDecision? stop,
        ControlledPilotOperationalRunbookStatus runbook,
        RollbackEvidenceStatus rollback,
        DateTimeOffset completedAtUtc)
    {
        var writer = new CanonicalFingerprintWriter();
        writer.Add("bundle", "operational-evidence-bundle-v1");
        writer.Add("rehearsal", identity.RehearsalId);
        writer.Add("pilot", identity.PilotId);
        writer.Add("session", identity.SessionId);
        writer.Add("correlation", identity.CorrelationId);
        writer.Add("release", identity.ReleaseId);
        writer.Add("station-scope", identity.StationScope);
        writer.Add("start", identity.StartUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        writer.Add("end", identity.EndUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        foreach (PilotValidationWorkflow workflow in identity.SelectedWorkflows.Order())
            writer.Add("selected-workflow", workflow.ToString());
        writer.Add("operator", identity.OperatorReference);
        writer.Add("preparation-evidence", identity.Phase9PreparationEvidenceReference);
        writer.Add("rollback-evidence", identity.RollbackEvidenceReference);
        writer.Add("explicit-approval", identity.ExplicitApproval);
        writer.Add("preflight-status", preflight.Status.ToString());
        writer.Add("preflight-reason", preflight.ReasonCode);
        writer.Add("preflight-time", preflight.EvaluatedAtUtc.ToUniversalTime().ToString("O",
            CultureInfo.InvariantCulture));
        foreach (string code in preflight.ReasonCodes.Order(StringComparer.Ordinal))
            writer.Add("preflight-code", code);
        foreach (ControlledPilotOperationalWorkflowResult result in results.OrderBy(x => x.Workflow))
        {
            writer.Add("result-workflow", result.Workflow.ToString());
            writer.Add("result-status", result.Status.ToString());
            writer.Add("result-version", result.FingerprintSpecificationVersion);
            writer.Add("result-legacy", result.LegacyFingerprint);
            writer.Add("result-target", result.TargetFingerprint);
            writer.Add("result-differences", result.SemanticDifferenceCount);
            writer.Add("result-evidence", result.EvidenceReference);
            writer.Add("result-time", result.ObservedAtUtc.ToUniversalTime().ToString("O",
                CultureInfo.InvariantCulture));
        }
        foreach (KeyValuePair<PilotValidationWorkflow, string> version in versions.OrderBy(x => x.Key))
        {
            writer.Add("version-workflow", version.Key.ToString());
            writer.Add("version-value", version.Value);
        }
        foreach (ControlledPilotFingerprintComparison comparison in comparisons.OrderBy(x => x.Workflow))
        {
            writer.Add("comparison-workflow", comparison.Workflow.ToString());
            writer.Add("comparison-status", comparison.Status.ToString());
            writer.Add("comparison-legacy", comparison.LegacyFingerprint);
            writer.Add("comparison-target", comparison.TargetFingerprint);
            writer.Add("comparison-differences", comparison.SemanticDifferenceCount);
        }
        writer.Add("monitoring-status", monitoring.Status.ToString());
        foreach (string signal in monitoring.SignalCodes.Order(StringComparer.Ordinal))
            writer.Add("monitoring-signal", signal);
        writer.Add("monitoring-evidence", monitoring.EvidenceReference);
        writer.Add("monitoring-time", monitoring.ObservedAtUtc.ToUniversalTime().ToString("O",
            CultureInfo.InvariantCulture));
        writer.Add("stop-reason", stop?.Reason.ToString() ?? "none");
        writer.Add("stop-code", stop?.ReasonCode ?? "none");
        writer.Add("stop-evidence", stop?.EvidenceReference ?? "none");
        writer.Add("stop-time", stop?.DecidedAtUtc.ToUniversalTime().ToString("O",
            CultureInfo.InvariantCulture) ?? "none");
        writer.Add("runbook-version", runbook.RunbookVersion);
        foreach (string step in runbook.CompletedStepIds.Order(StringComparer.Ordinal))
            writer.Add("runbook-step", step);
        writer.Add("runbook-current", runbook.CurrentStepId);
        writer.Add("runbook-terminal", runbook.Terminal);
        writer.Add("rollback", rollback.ToString());
        writer.Add("completed", completedAtUtc.ToUniversalTime().ToString("O",
            CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(writer.ToString())));
    }
}
