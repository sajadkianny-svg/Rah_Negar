using System.Collections.ObjectModel;
using Rah_Negar.Foundation.Application.Activation.Preparation;
using Rah_Negar.Foundation.Application.Pilot.Validation;

namespace Rah_Negar.Foundation.Application.Pilot.Operational;

public enum ControlledPilotOperationalOperationStatus
{
    Accepted,
    Blocked,
    Failed,
    Canceled,
    Disposed
}

public sealed class ControlledPilotOperationalOperationResult
{
    internal ControlledPilotOperationalOperationResult(
        ControlledPilotOperationalOperationStatus status,
        ControlledPilotOperationalLifecycle lifecycle,
        string reasonCode,
        ControlledPilotOperationalPreflightResult? preflight = null,
        IReadOnlyList<ControlledPilotOperationalWorkflowResult>? workflowResults = null,
        ControlledPilotOperationalMonitoringEvidence? monitoringEvidence = null,
        ControlledPilotOperationalStopDecision? stopDecision = null,
        ControlledPilotOperationalEvidenceBundle? evidenceBundle = null)
    {
        Status = status;
        Lifecycle = lifecycle;
        ReasonCode = reasonCode;
        Preflight = preflight;
        WorkflowResults = new ReadOnlyCollection<ControlledPilotOperationalWorkflowResult>(
            (workflowResults ?? []).ToArray());
        MonitoringEvidence = monitoringEvidence;
        StopDecision = stopDecision;
        EvidenceBundle = evidenceBundle;
    }

    public ControlledPilotOperationalOperationStatus Status { get; }
    public ControlledPilotOperationalLifecycle Lifecycle { get; }
    public string ReasonCode { get; }
    public ControlledPilotOperationalPreflightResult? Preflight { get; }
    public IReadOnlyList<ControlledPilotOperationalWorkflowResult> WorkflowResults { get; }
    public ControlledPilotOperationalMonitoringEvidence? MonitoringEvidence { get; }
    public ControlledPilotOperationalStopDecision? StopDecision { get; }
    public ControlledPilotOperationalEvidenceBundle? EvidenceBundle { get; }
    public bool MutatedProduction => false;
    public bool SwitchedAuthority => false;
    public bool ExecutedMigration => false;
    public bool ExecutedEsdCutover => false;
}

public sealed class ControlledPilotOperationalRehearsalCoordinator : IDisposable
{
    private readonly object _gate = new();
    private readonly ControlledPilotOperationalRehearsalContext _context;
    private readonly OperationalReleaseEvidence _releaseEvidence;
    private readonly ProductionActivationReadinessResult _preparationEvidence;
    private readonly ControlledPilotPrerequisiteEvidence _pilotPrerequisiteEvidence;
    private readonly RollbackVerificationResult _rollbackEvidence;
    private readonly IReadOnlyDictionary<PilotValidationWorkflow,
        IControlledPilotOperationalWorkflowObserver> _observers;
    private readonly IControlledPilotOperationalEvidenceDestination _evidenceDestination;
    private readonly ControlledPilotOperationalPreflight _preflight;
    private readonly ControlledPilotOperationalStopEvaluator _stopEvaluator;
    private readonly ControlledPilotOperationalRunbookDefinition _runbook;
    private readonly int _allowedFingerprintDifferences;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly HashSet<string> _completedRunbookSteps = new(StringComparer.Ordinal);
    private readonly string? _configurationIssue;
    private ControlledPilotOperationalLifecycle _lifecycle =
        ControlledPilotOperationalLifecycle.Created;
    private ControlledPilotOperationalPreflightResult? _preflightResult;
    private IReadOnlyList<ControlledPilotOperationalWorkflowResult> _workflowResults =
        Array.Empty<ControlledPilotOperationalWorkflowResult>();
    private ControlledPilotOperationalMonitoringEvidence? _monitoringEvidence;
    private ControlledPilotOperationalStopDecision? _stopDecision;
    private ControlledPilotOperationalEvidenceBundle? _bundle;
    private DateTimeOffset? _startedAtUtc;
    private RollbackEvidenceStatus _currentRollbackReadiness;
    private bool _attemptedObservation;
    private bool _disposed;

    public ControlledPilotOperationalRehearsalCoordinator(
        ControlledPilotOperationalRehearsalContext context,
        OperationalReleaseEvidence releaseEvidence,
        ProductionActivationReadinessResult preparationEvidence,
        ControlledPilotPrerequisiteEvidence pilotPrerequisiteEvidence,
        RollbackVerificationResult rollbackEvidence,
        IEnumerable<IControlledPilotOperationalWorkflowObserver> observers,
        IControlledPilotOperationalEvidenceDestination evidenceDestination,
        int allowedFingerprintDifferences = 0,
        ControlledPilotOperationalPreflight? preflight = null,
        ControlledPilotOperationalStopEvaluator? stopEvaluator = null,
        ControlledPilotOperationalRunbookDefinition? runbook = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _releaseEvidence = releaseEvidence ?? throw new ArgumentNullException(nameof(releaseEvidence));
        _preparationEvidence = preparationEvidence ?? throw new ArgumentNullException(
            nameof(preparationEvidence));
        _pilotPrerequisiteEvidence = pilotPrerequisiteEvidence ?? throw new ArgumentNullException(
            nameof(pilotPrerequisiteEvidence));
        _rollbackEvidence = rollbackEvidence ?? throw new ArgumentNullException(nameof(rollbackEvidence));
        _currentRollbackReadiness = _rollbackEvidence.ValidationStatus;
        _evidenceDestination = evidenceDestination ?? throw new ArgumentNullException(
            nameof(evidenceDestination));
        _preflight = preflight ?? new ControlledPilotOperationalPreflight();
        _stopEvaluator = stopEvaluator ?? new ControlledPilotOperationalStopEvaluator();
        _runbook = runbook ?? ControlledPilotOperationalRunbookDefinition.Standard;
        _allowedFingerprintDifferences = allowedFingerprintDifferences;
        try
        {
            IControlledPilotOperationalWorkflowObserver[] supplied = observers?.ToArray() ?? [];
            if (allowedFingerprintDifferences < 0 || supplied.Any(observer => observer is null) ||
                supplied.GroupBy(observer => observer.Workflow).Any(group => group.Count() != 1))
            {
                _configurationIssue = "operational-rehearsal-configuration-invalid";
                _observers = EmptyObservers();
                return;
            }
            _observers = new ReadOnlyDictionary<PilotValidationWorkflow,
                IControlledPilotOperationalWorkflowObserver>(supplied.ToDictionary(
                    observer => observer.Workflow));
        }
        catch
        {
            _configurationIssue = "operational-rehearsal-configuration-failed";
            _observers = EmptyObservers();
        }
    }

    public ControlledPilotOperationalLifecycle Lifecycle
    {
        get { lock (_gate) return _lifecycle; }
    }

    public ControlledPilotOperationalEvidenceBundle? EvidenceBundle
    {
        get { lock (_gate) return _bundle; }
    }

    public bool AutomaticallyRuns => false;
    public bool AutomaticallyRetries => false;
    public bool UsesTimer => false;
    public bool UsesScheduler => false;
    public bool UsesPolling => false;
    public bool UsesBackgroundWorker => false;
    public bool MutatesProductionDatabase => false;
    public bool RunsMigration => false;
    public bool PerformsEsdCutover => false;
    public bool ChangesProductionAuthority => false;
    public bool ReplacesProductionUi => false;
    public bool ImplementsRbac => false;
    public bool CreatesIdentities => false;

    public ControlledPilotOperationalOperationResult RunPreflight(
        DateTimeOffset evaluatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_disposed) return Disposed();
            if (_lifecycle != ControlledPilotOperationalLifecycle.Created)
                return Blocked("operational-preflight-transition-invalid");
            if (_configurationIssue is not null)
                return FailLocked(_configurationIssue);
        }

        ControlledPilotOperationalPreflightResult result = _preflight.Evaluate(
            _context, _releaseEvidence, _preparationEvidence, _pilotPrerequisiteEvidence,
            _rollbackEvidence, _observers.Values, _evidenceDestination, evaluatedAtUtc,
            cancellationToken);
        lock (_gate)
        {
            if (_disposed) return Disposed();
            _preflightResult = result;
            if (result.Status == ControlledPilotOperationalPreflightStatus.Ready)
            {
                _lifecycle = ControlledPilotOperationalLifecycle.PreflightPassed;
                CompleteStep(OperationalRunbookStepKind.Preflight);
                return Accepted(result.ReasonCode, preflight: result);
            }
            _lifecycle = result.Status == ControlledPilotOperationalPreflightStatus.RequiresReview
                ? ControlledPilotOperationalLifecycle.ReviewRequired
                : ControlledPilotOperationalLifecycle.Failed;
            return new(result.Status == ControlledPilotOperationalPreflightStatus.RequiresReview
                    ? ControlledPilotOperationalOperationStatus.Blocked
                    : cancellationToken.IsCancellationRequested
                        ? ControlledPilotOperationalOperationStatus.Canceled
                        : ControlledPilotOperationalOperationStatus.Failed,
                _lifecycle, result.ReasonCode, preflight: result);
        }
    }

    public ControlledPilotOperationalOperationResult Approve(DateTimeOffset approvedAtUtc)
    {
        lock (_gate)
        {
            if (_disposed) return Disposed();
            if (_lifecycle != ControlledPilotOperationalLifecycle.PreflightPassed)
                return Blocked("operational-approve-transition-invalid");
            if (!_context.ExplicitApproval || !TimeInWindow(approvedAtUtc) ||
                approvedAtUtc < _preflightResult!.EvaluatedAtUtc)
                return FailLocked("operational-approval-invalid");
            _lifecycle = ControlledPilotOperationalLifecycle.Approved;
            CompleteStep(OperationalRunbookStepKind.Approve);
            return Accepted("operational-rehearsal-approved");
        }
    }

    public ControlledPilotOperationalOperationResult Start(DateTimeOffset startedAtUtc)
    {
        lock (_gate)
        {
            if (_disposed) return Disposed();
            if (_lifecycle != ControlledPilotOperationalLifecycle.Approved)
                return Blocked("operational-start-transition-invalid");
            if (!TimeInWindow(startedAtUtc))
                return FailLocked("operational-start-time-invalid");
            _startedAtUtc = startedAtUtc;
            _lifecycle = ControlledPilotOperationalLifecycle.Started;
            CompleteStep(OperationalRunbookStepKind.Start);
            return Accepted("operational-rehearsal-started");
        }
    }

    public async ValueTask<ControlledPilotOperationalOperationResult> ObserveAsync(
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_disposed) return Disposed();
            if (_lifecycle != ControlledPilotOperationalLifecycle.Started ||
                _attemptedObservation)
                return Blocked("operational-observe-transition-invalid");
            if (!TimeInWindow(observedAtUtc) || observedAtUtc < _startedAtUtc)
                return FailLocked("operational-observe-time-invalid");
            _attemptedObservation = true;
            _lifecycle = ControlledPilotOperationalLifecycle.Observing;
        }

        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _lifetime.Token);
        var results = new List<ControlledPilotOperationalWorkflowResult>();
        ControlledPilotOperationalStopDecision? terminalStop = null;
        try
        {
            foreach (PilotValidationWorkflow workflow in _context.SelectedWorkflows)
            {
                if (linked.IsCancellationRequested)
                {
                    terminalStop = EvaluateStop(results, observedAtUtc,
                        cancellationRequested: true);
                    break;
                }
                ControlledPilotOperationalWorkflowResult? result;
                try
                {
                    result = await _observers[workflow].ObserveAsync(_context,
                        observedAtUtc, linked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    terminalStop = EvaluateStop(results, observedAtUtc,
                        cancellationRequested: true);
                    break;
                }
                catch
                {
                    result = FailedWorkflow(workflow, observedAtUtc,
                        "observer-failure-evidence");
                }
                if (!WorkflowResultValid(result, workflow, observedAtUtc))
                    result = FailedWorkflow(workflow, observedAtUtc,
                        "observer-invalid-evidence");
                results.Add(result!);
                if (result!.Status == OperationalWorkflowComparisonStatus.Failed)
                {
                    terminalStop = EvaluateStop(results, observedAtUtc);
                    break;
                }
            }

            bool rollbackReady = RollbackReady();
            _monitoringEvidence = ControlledPilotOperationalMonitoringFactory.Create(
                results, rollbackReady, observedAtUtc);
            _workflowResults = new ReadOnlyCollection<ControlledPilotOperationalWorkflowResult>(
                results.OrderBy(result => result.Workflow).ToArray());
            CompleteStep(OperationalRunbookStepKind.Observe);
            CompleteStep(OperationalRunbookStepKind.Compare);

            terminalStop ??= EvaluateStop(results, observedAtUtc,
                rollbackReady: rollbackReady);
            if (terminalStop is not null)
                return await FinishStoppedAsync(terminalStop, observedAtUtc,
                    CancellationToken.None).ConfigureAwait(false);

            lock (_gate)
            {
                if (_disposed) return Disposed();
                _lifecycle = ControlledPilotOperationalLifecycle.ReviewRequired;
                return Accepted("operational-review-required", _workflowResults,
                    _monitoringEvidence);
            }
        }
        catch
        {
            ControlledPilotOperationalWorkflowResult failure = FailedWorkflow(
                _context.SelectedWorkflows.First(), observedAtUtc,
                "observation-failure-evidence");
            if (results.All(result => result.Workflow != failure.Workflow))
                results.Add(failure);
            _workflowResults = new ReadOnlyCollection<ControlledPilotOperationalWorkflowResult>(
                results.OrderBy(result => result.Workflow).ToArray());
            _monitoringEvidence = ControlledPilotOperationalMonitoringFactory.Create(
                _workflowResults, RollbackReady(), observedAtUtc);
            terminalStop = EvaluateStop(_workflowResults, observedAtUtc);
            return await FinishStoppedAsync(terminalStop!, observedAtUtc,
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    public async ValueTask<ControlledPilotOperationalOperationResult> RecordOperatorDecisionAsync(
        ControlledPilotOperationalOperatorDecision? decision,
        DateTimeOffset completedAtUtc,
        bool rollbackReady = true,
        bool securityBoundaryViolated = false,
        bool evidenceIntegrityValid = true,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_disposed) return Disposed();
            if (_lifecycle != ControlledPilotOperationalLifecycle.ReviewRequired)
                return Blocked("operational-review-transition-invalid");
            if (!DecisionValid(decision, completedAtUtc))
                return FailLocked("operational-operator-decision-invalid");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            ControlledPilotOperationalStopDecision canceled = EvaluateStop(
                _workflowResults, completedAtUtc, cancellationRequested: true)!;
            return await FinishStoppedAsync(canceled, completedAtUtc,
                CancellationToken.None).ConfigureAwait(false);
        }

        CompleteStep(OperationalRunbookStepKind.Review);
        bool effectiveRollbackReady = rollbackReady && RollbackReady();
        _currentRollbackReadiness = effectiveRollbackReady
            ? RollbackEvidenceStatus.Verified : RollbackEvidenceStatus.Unavailable;
        ControlledPilotOperationalStopDecision? stop = EvaluateStop(_workflowResults,
            completedAtUtc, effectiveRollbackReady, securityBoundaryViolated,
            evidenceIntegrityValid, operatorDecision: decision);
        if (stop is not null)
            return await FinishStoppedAsync(stop, completedAtUtc,
                cancellationToken).ConfigureAwait(false);

        CompleteStep(OperationalRunbookStepKind.Complete);
        ControlledPilotOperationalEvidenceBundle bundle = BuildBundle(completedAtUtc, null,
            OperationalRunbookStepKind.Complete);
        return await PersistTerminalAsync(bundle, ControlledPilotOperationalLifecycle.Completed,
            "operational-rehearsal-completed", cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _lifecycle = ControlledPilotOperationalLifecycle.Disposed;
        }
        try { _lifetime.Cancel(); }
        catch { }
        _lifetime.Dispose();
    }

    private async ValueTask<ControlledPilotOperationalOperationResult> FinishStoppedAsync(
        ControlledPilotOperationalStopDecision stop,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken)
    {
        _stopDecision = stop;
        CompleteStep(OperationalRunbookStepKind.Stop);
        if (stop.Reason == ControlledPilotOperationalStopReason.RollbackRequested)
            CompleteStep(OperationalRunbookStepKind.RollbackRequestEvidence);
        ControlledPilotOperationalEvidenceBundle bundle = BuildBundle(completedAtUtc, stop,
            OperationalRunbookStepKind.Stop);
        return await PersistTerminalAsync(bundle, ControlledPilotOperationalLifecycle.Stopped,
            stop.ReasonCode, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ControlledPilotOperationalOperationResult> PersistTerminalAsync(
        ControlledPilotOperationalEvidenceBundle bundle,
        ControlledPilotOperationalLifecycle terminalState,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        bool persisted;
        try
        {
            persisted = await _evidenceDestination.WriteAsync(bundle, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            lock (_gate)
            {
                _bundle = bundle;
                _lifecycle = ControlledPilotOperationalLifecycle.Stopped;
                return new(ControlledPilotOperationalOperationStatus.Canceled, _lifecycle,
                    "operational-evidence-write-canceled", _preflightResult, _workflowResults,
                    _monitoringEvidence, _stopDecision, bundle);
            }
        }
        catch
        {
            persisted = false;
        }

        lock (_gate)
        {
            _bundle = bundle;
            if (!persisted)
            {
                _lifecycle = ControlledPilotOperationalLifecycle.Failed;
                return new(ControlledPilotOperationalOperationStatus.Failed, _lifecycle,
                    "operational-evidence-destination-failed", _preflightResult,
                    _workflowResults, _monitoringEvidence, _stopDecision, bundle);
            }
            _lifecycle = terminalState;
            return new(ControlledPilotOperationalOperationStatus.Accepted, _lifecycle,
                reasonCode, _preflightResult, _workflowResults, _monitoringEvidence,
                _stopDecision, bundle);
        }
    }

    private ControlledPilotOperationalEvidenceBundle BuildBundle(
        DateTimeOffset completedAtUtc,
        ControlledPilotOperationalStopDecision? stop,
        OperationalRunbookStepKind currentStep) =>
        ControlledPilotOperationalEvidenceBundleFactory.Create(_context, _preflightResult!,
            _workflowResults, _monitoringEvidence!, stop,
            new ControlledPilotOperationalRunbookStatus(_runbook.Version,
                _completedRunbookSteps, Step(currentStep).StepId, terminal: true),
            _currentRollbackReadiness, completedAtUtc);

    private ControlledPilotOperationalStopDecision? EvaluateStop(
        IReadOnlyList<ControlledPilotOperationalWorkflowResult> results,
        DateTimeOffset atUtc,
        bool? rollbackReady = null,
        bool securityBoundaryViolated = false,
        bool evidenceIntegrityValid = true,
        bool cancellationRequested = false,
        ControlledPilotOperationalOperatorDecision? operatorDecision = null) =>
        _stopEvaluator.Evaluate(new(results, _allowedFingerprintDifferences,
            evidenceIntegrityValid, rollbackReady ?? RollbackReady(),
            securityBoundaryViolated, cancellationRequested, operatorDecision,
            operatorDecision?.EvidenceReference ?? "operational-stop-evidence", atUtc));

    private bool RollbackReady() =>
        _rollbackEvidence.ValidationStatus == RollbackEvidenceStatus.Verified &&
        StringComparer.Ordinal.Equals(_rollbackEvidence.EvidenceReference,
            _context.RollbackEvidenceReference);

    private bool WorkflowResultValid(
        ControlledPilotOperationalWorkflowResult? result,
        PilotValidationWorkflow workflow,
        DateTimeOffset observedAtUtc) => result is not null && result.Workflow == workflow &&
        result.Status is OperationalWorkflowComparisonStatus.Match or
            OperationalWorkflowComparisonStatus.Difference &&
        OperationalText.IsUsableIdentifier(result.FingerprintSpecificationVersion) &&
        result.LegacyFingerprint.Length == 64 && result.TargetFingerprint.Length == 64 &&
        result.SemanticDifferenceCount == (result.Status ==
            OperationalWorkflowComparisonStatus.Match ? 0 : 1) &&
        OperationalText.IsUsableIdentifier(result.EvidenceReference) &&
        result.ObservedAtUtc == observedAtUtc && result.LegacyRemainsAuthoritative &&
        !result.MutatedProduction && !result.ContainsRawRows && !result.ContainsSql;

    private ControlledPilotOperationalWorkflowResult FailedWorkflow(
        PilotValidationWorkflow workflow,
        DateTimeOffset atUtc,
        string evidenceReference) => new(workflow,
            OperationalWorkflowComparisonStatus.Failed,
            _observers.TryGetValue(workflow, out var observer)
                ? observer.FingerprintSpecificationVersion : "fingerprint-version-failed",
            new string('0', 64), new string('0', 64), 0, evidenceReference, atUtc);

    private bool DecisionValid(
        ControlledPilotOperationalOperatorDecision? decision,
        DateTimeOffset completedAtUtc) => decision is not null &&
        Enum.IsDefined(decision.Kind) &&
        OperationalText.IsUsableIdentifier(decision.DecisionId) &&
        OperationalText.IsUsableIdentifier(decision.EvidenceReference) &&
        decision.DecidedAtUtc.Offset == TimeSpan.Zero &&
        decision.DecidedAtUtc >= _startedAtUtc && decision.DecidedAtUtc <= completedAtUtc &&
        TimeInWindow(completedAtUtc) && !decision.ExecutesRollback && !decision.StopsProduction;

    private bool TimeInWindow(DateTimeOffset value) => value.Offset == TimeSpan.Zero &&
        value >= _context.StartUtc && value <= _context.EndUtc;

    private void CompleteStep(OperationalRunbookStepKind kind) =>
        _completedRunbookSteps.Add(Step(kind).StepId);

    private ControlledPilotOperationalRunbookStep Step(OperationalRunbookStepKind kind) =>
        _runbook.Steps.Single(step => step.Kind == kind);

    private ControlledPilotOperationalOperationResult Accepted(
        string reasonCode,
        IReadOnlyList<ControlledPilotOperationalWorkflowResult>? workflowResults = null,
        ControlledPilotOperationalMonitoringEvidence? monitoring = null,
        ControlledPilotOperationalPreflightResult? preflight = null) => new(
            ControlledPilotOperationalOperationStatus.Accepted, _lifecycle, reasonCode,
            preflight ?? _preflightResult, workflowResults, monitoring);

    private ControlledPilotOperationalOperationResult Blocked(string reasonCode) => new(
        ControlledPilotOperationalOperationStatus.Blocked, _lifecycle, reasonCode,
        _preflightResult, _workflowResults, _monitoringEvidence, _stopDecision, _bundle);

    private ControlledPilotOperationalOperationResult FailLocked(string reasonCode)
    {
        _lifecycle = ControlledPilotOperationalLifecycle.Failed;
        return new(ControlledPilotOperationalOperationStatus.Failed, _lifecycle, reasonCode,
            _preflightResult, _workflowResults, _monitoringEvidence, _stopDecision, _bundle);
    }

    private ControlledPilotOperationalOperationResult Disposed() => new(
        ControlledPilotOperationalOperationStatus.Disposed,
        ControlledPilotOperationalLifecycle.Disposed, "operational-rehearsal-disposed",
        _preflightResult, _workflowResults, _monitoringEvidence, _stopDecision, _bundle);

    private static IReadOnlyDictionary<PilotValidationWorkflow,
        IControlledPilotOperationalWorkflowObserver> EmptyObservers() =>
        new ReadOnlyDictionary<PilotValidationWorkflow,
            IControlledPilotOperationalWorkflowObserver>(
            new Dictionary<PilotValidationWorkflow,
                IControlledPilotOperationalWorkflowObserver>());
}
