using System.Collections.ObjectModel;
using Rah_Negar.Foundation.Application.Activation.Preparation;
using Rah_Negar.Foundation.Application.Pilot.Validation;

namespace Rah_Negar.Foundation.Application.Pilot.Production;

public sealed class ControlledProductionPilotCoordinator : IDisposable
{
    private readonly object _gate = new();
    private readonly ControlledProductionPilotContext? _context;
    private readonly ProductionActivationReadinessResult? _preparation;
    private readonly RollbackVerificationResult? _rollback;
    private readonly IReadOnlyList<ControlledPilotOperatorApproval> _approvals;
    private readonly IReadOnlyDictionary<PilotValidationWorkflow,
        IControlledProductionPilotObserver> _observers;
    private readonly IPilotMonitoringEvidenceFactory? _monitoringFactory;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly string? _configurationIssue;
    private ControlledPilotSessionState _state = ControlledPilotSessionState.Created;
    private string? _sessionId;
    private DateTimeOffset? _approvedAtUtc;
    private DateTimeOffset? _startedAtUtc;
    private DateTimeOffset? _observingAtUtc;
    private ControlledPilotEvidence? _lastEvidence;
    private PilotStopDecision? _stopDecision;
    private bool _observationInProgress;
    private bool _disposed;

    public ControlledProductionPilotCoordinator(
        ControlledProductionPilotContext? context,
        ProductionActivationReadinessResult? preparation,
        RollbackVerificationResult? rollback,
        IEnumerable<ControlledPilotOperatorApproval>? approvals,
        IEnumerable<IControlledProductionPilotObserver>? observers,
        IPilotMonitoringEvidenceFactory? monitoringFactory)
    {
        _context = context;
        _preparation = preparation;
        _rollback = rollback;
        _monitoringFactory = monitoringFactory;
        try
        {
            ControlledPilotOperatorApproval[] suppliedApprovals = approvals?.ToArray() ?? [];
            IControlledProductionPilotObserver[] suppliedObservers = observers?.ToArray() ?? [];
            _approvals = new ReadOnlyCollection<ControlledPilotOperatorApproval>(
                suppliedApprovals);
            if (suppliedApprovals.Any(approval => approval is null) ||
                suppliedObservers.Any(observer => observer is null))
            {
                _configurationIssue = "production-pilot-dependency-invalid";
                _observers = EmptyObservers();
                return;
            }
            IGrouping<PilotValidationWorkflow, IControlledProductionPilotObserver>[] groups =
                suppliedObservers.GroupBy(observer => observer.Feature).ToArray();
            if (groups.Any(group => group.Count() != 1))
            {
                _configurationIssue = "production-pilot-observer-duplicate";
                _observers = EmptyObservers();
                return;
            }
            _observers = new ReadOnlyDictionary<PilotValidationWorkflow,
                IControlledProductionPilotObserver>(groups.ToDictionary(group => group.Key,
                    group => group.Single()));
            if (_monitoringFactory is null)
                _configurationIssue = "production-pilot-monitoring-factory-required";
        }
        catch
        {
            _approvals = Array.Empty<ControlledPilotOperatorApproval>();
            _observers = EmptyObservers();
            _configurationIssue = "production-pilot-configuration-failed";
        }
    }

    public bool AutomaticallyActivates => false;
    public bool AutomaticallyRestarts => false;
    public bool UsesScheduler => false;
    public bool UsesBackgroundExecution => false;
    public bool UsesPolling => false;
    public bool ChangesAuthority => false;
    public bool ExecutesMigration => false;
    public bool MutatesProductionData => false;
    public bool ModifiesSettings => false;
    public bool CreatesUsers => false;
    public bool ExecutesEsd => false;
    public bool ActivatesFeatures => false;
    public bool ReplacesLogin => false;
    public bool ReplacesSettings => false;
    public bool ReplacesReporting => false;
    public bool ReplacesRuntimeEvents => false;
    public bool ReplacesExport => false;
    public bool CreatesRbac => false;
    public bool UsesSupportIdentity => false;

    public ControlledPilotSessionState State
    {
        get { lock (_gate) return _state; }
    }

    public ControlledPilotEvidence? LastEvidence
    {
        get { lock (_gate) return _lastEvidence; }
    }

    public PilotStopDecision? StopDecision
    {
        get { lock (_gate) return _stopDecision; }
    }

    public ControlledPilotSessionOperationResult Approve(DateTimeOffset approvedAtUtc)
    {
        lock (_gate)
        {
            if (_disposed) return Disposed();
            if (_state != ControlledPilotSessionState.Created)
                return Reject("production-pilot-approve-transition-invalid");
            string? issue = ValidateApprovalConfiguration(approvedAtUtc);
            if (issue is not null) return FailLocked(issue);
            _approvedAtUtc = approvedAtUtc;
            _state = ControlledPilotSessionState.Approved;
            return Accepted("production-pilot-approved");
        }
    }

    public ControlledPilotSessionOperationResult Start(
        string sessionId,
        DateTimeOffset startedAtUtc)
    {
        lock (_gate)
        {
            if (_disposed) return Disposed();
            if (_state != ControlledPilotSessionState.Approved)
                return Reject("production-pilot-start-transition-invalid");
            if (!ControlledProductionPilotText.IsUsableIdentifier(sessionId) ||
                !TimeInWindow(startedAtUtc) || startedAtUtc < _approvedAtUtc)
                return FailLocked("production-pilot-start-invalid");
            _sessionId = sessionId;
            _startedAtUtc = startedAtUtc;
            _state = ControlledPilotSessionState.Started;
            return Accepted("production-pilot-started");
        }
    }

    public ControlledPilotSessionOperationResult BeginObservation(DateTimeOffset observedAtUtc)
    {
        lock (_gate)
        {
            if (_disposed) return Disposed();
            if (_state != ControlledPilotSessionState.Started)
                return Reject("production-pilot-observe-transition-invalid");
            if (!TimeInWindow(observedAtUtc) || observedAtUtc < _startedAtUtc)
                return FailLocked("production-pilot-observe-time-invalid");
            _observingAtUtc = observedAtUtc;
            _state = ControlledPilotSessionState.Observing;
            return Accepted("production-pilot-observing");
        }
    }

    public async ValueTask<ControlledPilotSessionOperationResult> ObserveAsync(
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_disposed) return Disposed();
            if (_state != ControlledPilotSessionState.Observing ||
                _observationInProgress || _lastEvidence is not null)
                return Reject("production-pilot-observation-transition-invalid");
            if (!TimeInWindow(observedAtUtc) || observedAtUtc < _observingAtUtc)
                return FailLocked("production-pilot-observation-time-invalid");
            _observationInProgress = true;
        }

        CancellationTokenSource? linked = null;
        try
        {
            linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
                _lifetime.Token);
            var results = new List<ControlledPilotObservationResult>();
            foreach (PilotValidationWorkflow feature in _context!.ApprovedFeatures)
            {
                ControlledPilotObservationResult? result;
                try
                {
                    result = await _observers[feature].ObserveAsync(_context, _sessionId!,
                        linked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return Fail("production-pilot-observation-canceled",
                        ControlledPilotOperationStatus.Canceled);
                }
                catch
                {
                    return Fail("production-pilot-observer-failed");
                }
                if (linked.IsCancellationRequested)
                    return Fail("production-pilot-observation-canceled",
                        ControlledPilotOperationStatus.Canceled);
                if (!ObservationValid(result, feature, observedAtUtc))
                    return Fail("production-pilot-observation-invalid");
                results.Add(result!);
            }

            PilotMonitoringEvidence monitoring;
            try
            {
                monitoring = _monitoringFactory!.Create(_context, _sessionId!,
                    results.AsReadOnly(), _rollback!, observedAtUtc);
            }
            catch
            {
                return Fail("production-pilot-monitoring-failed");
            }
            if (!MonitoringValid(monitoring, results, observedAtUtc))
                return Fail("production-pilot-monitoring-invalid");

            var evidence = new ControlledPilotEvidence(
                $"{_context.PilotId}:{_sessionId}:observation", _context.PilotId,
                _sessionId!, results, monitoring, observedAtUtc);
            lock (_gate)
            {
                if (_disposed) return Disposed();
                _lastEvidence = evidence;
                _observationInProgress = false;
                return Accepted("production-pilot-observation-recorded", evidence);
            }
        }
        catch
        {
            return Fail("production-pilot-observation-failed");
        }
        finally
        {
            linked?.Dispose();
        }
    }

    public ControlledPilotSessionOperationResult Complete(DateTimeOffset completedAtUtc)
    {
        lock (_gate)
        {
            if (_disposed) return Disposed();
            if (_state != ControlledPilotSessionState.Observing ||
                _observationInProgress || _lastEvidence is null)
                return Reject("production-pilot-complete-transition-invalid");
            if (!TimeInWindow(completedAtUtc) ||
                completedAtUtc < _lastEvidence.ObservedAtUtc)
                return FailLocked("production-pilot-complete-time-invalid");
            _state = ControlledPilotSessionState.Completed;
            return Accepted("production-pilot-completed", _lastEvidence);
        }
    }

    public ControlledPilotSessionOperationResult Stop(PilotStopDecision? decision)
    {
        lock (_gate)
        {
            if (_disposed) return Disposed();
            if (_state is not (ControlledPilotSessionState.Started or
                ControlledPilotSessionState.Observing))
                return Reject("production-pilot-stop-transition-invalid");
            if (!StopDecisionValid(decision))
                return FailLocked("production-pilot-stop-decision-invalid");
            _stopDecision = decision;
            _state = ControlledPilotSessionState.Stopped;
            try { _lifetime.Cancel(); }
            catch { }
            return new(ControlledPilotOperationStatus.Accepted, _state,
                "production-pilot-stopped", _lastEvidence, decision);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _state = ControlledPilotSessionState.Disposed;
        }
        try { _lifetime.Cancel(); }
        catch { }
        try { _lifetime.Dispose(); }
        catch { }
    }

    private string? ValidateApprovalConfiguration(DateTimeOffset approvedAtUtc)
    {
        if (_configurationIssue is not null) return _configurationIssue;
        if (!ContextValid(_context)) return "production-pilot-context-invalid";
        if (approvedAtUtc.Offset != TimeSpan.Zero ||
            approvedAtUtc > _context!.EndWindowUtc)
            return "production-pilot-approval-time-invalid";
        if (_preparation?.Decision !=
                ProductionActivationPreparationDecision.ApprovedForPreparation ||
            _preparation.EvidencePackage is null ||
            !StringComparer.Ordinal.Equals(_context.ActivationPreparationReference,
                _preparation.EvidencePackage.PackageId) ||
            !_preparation.EvidencePackage.ValidationSummary.LegacyRemainsAuthoritative ||
            _preparation.EvidencePackage.GrantsActivationPermission ||
            _preparation.Blockers.Count != 0 || _preparation.ReviewItems.Count != 0)
            return "production-pilot-preparation-evidence-invalid";
        if (_rollback is null ||
            _rollback.ValidationStatus != RollbackEvidenceStatus.Verified ||
            !StringComparer.Ordinal.Equals(_context.RollbackReference,
                _rollback.RollbackPlanReference) ||
            !ControlledProductionPilotText.IsUsableIdentifier(_rollback.OwnerReference) ||
            !ControlledProductionPilotText.IsUsableIdentifier(_rollback.EvidenceReference))
            return "production-pilot-rollback-not-ready";
        if (!ApprovalsValid(approvedAtUtc))
            return "production-pilot-operator-approval-invalid";
        if (!ObserversValid()) return "production-pilot-observer-scope-invalid";
        return null;
    }

    private static bool ContextValid(ControlledProductionPilotContext? context) =>
        context is not null &&
        ControlledProductionPilotText.IsUsableIdentifier(context.PilotId) &&
        ControlledProductionPilotText.IsUsableIdentifier(context.ReleaseIdentifier) &&
        ControlledProductionPilotText.IsUsableIdentifier(
            context.ActivationPreparationReference) &&
        ControlledProductionPilotText.IsUsableIdentifier(context.RollbackReference) &&
        ControlledProductionPilotText.IsUsableIdentifier(context.MonitoringReference) &&
        Enum.IsDefined(context.TargetScope) && context.SelectedOperators.Count > 0 &&
        context.SelectedOperators.All(ControlledProductionPilotText.IsUsableIdentifier) &&
        context.ApprovedFeatures.Count > 0 &&
        context.ApprovedFeatures.All(Enum.IsDefined) &&
        context.StartWindowUtc.Offset == TimeSpan.Zero &&
        context.EndWindowUtc.Offset == TimeSpan.Zero &&
        context.StartWindowUtc < context.EndWindowUtc;

    private bool ApprovalsValid(DateTimeOffset approvedAtUtc)
    {
        if (_approvals.Count != _context!.SelectedOperators.Count ||
            _approvals.GroupBy(approval => approval.OperatorReference)
                .Any(group => group.Count() != 1)) return false;
        string[] approvedOperators = _approvals.Select(approval => approval.OperatorReference)
            .Order(StringComparer.Ordinal).ToArray();
        if (!approvedOperators.SequenceEqual(_context.SelectedOperators,
                StringComparer.Ordinal)) return false;
        return _approvals.All(approval =>
            ControlledProductionPilotText.IsUsableIdentifier(approval.OperatorReference) &&
            ControlledProductionPilotText.IsUsableIdentifier(approval.ApprovalReference) &&
            approval.ApprovedScope == _context.TargetScope &&
            approval.ApprovedAtUtc.Offset == TimeSpan.Zero &&
            approval.ApprovedAtUtc <= approvedAtUtc);
    }

    private bool ObserversValid()
    {
        if (_observers.Count != _context!.ApprovedFeatures.Count ||
            !_observers.Keys.Order().SequenceEqual(_context.ApprovedFeatures)) return false;
        return _observers.All(pair => ContractMatchesFeature(pair.Value, pair.Key));
    }

    private static bool ContractMatchesFeature(
        IControlledProductionPilotObserver observer,
        PilotValidationWorkflow feature) => feature switch
    {
        PilotValidationWorkflow.Authentication =>
            observer is IControlledAuthenticationPilotObserver,
        PilotValidationWorkflow.Reporting => observer is IControlledReportingPilotObserver,
        PilotValidationWorkflow.RuntimeEvent => observer is IControlledRuntimeEventPilotObserver,
        PilotValidationWorkflow.ProtectedSettings =>
            observer is IControlledProtectedSettingsPilotObserver,
        PilotValidationWorkflow.Export => observer is IControlledExportPilotObserver,
        _ => false
    };

    private bool ObservationValid(
        ControlledPilotObservationResult? observation,
        PilotValidationWorkflow feature,
        DateTimeOffset observedAtUtc) => observation is not null &&
        observation.Feature == feature && observation.Status is
            ControlledPilotObservationStatus.Match or
            ControlledPilotObservationStatus.Difference &&
        ControlledProductionPilotText.IsUsableIdentifier(observation.ResultFingerprint) &&
        ControlledProductionPilotText.IsUsableIdentifier(observation.ValidationSummary) &&
        ControlledProductionPilotText.IsUsableIdentifier(observation.DifferenceSummary) &&
        ControlledProductionPilotText.IsUsableIdentifier(observation.EvidenceReference) &&
        observation.ObservedAtUtc.Offset == TimeSpan.Zero &&
        observation.ObservedAtUtc >= _context!.StartWindowUtc &&
        observation.ObservedAtUtc <= observedAtUtc && observation.IsReadOnly &&
        !observation.MutatesProduction && observation.LegacyAuthorityPreserved;

    private bool MonitoringValid(
        PilotMonitoringEvidence? monitoring,
        IReadOnlyCollection<ControlledPilotObservationResult> observations,
        DateTimeOffset observedAtUtc)
    {
        bool difference = observations.Any(observation =>
            observation.Status == ControlledPilotObservationStatus.Difference);
        ControlledPilotHealthStatus expected = difference
            ? ControlledPilotHealthStatus.AttentionRequired
            : ControlledPilotHealthStatus.Healthy;
        return monitoring is not null &&
            StringComparer.Ordinal.Equals(monitoring.PilotId, _context!.PilotId) &&
            StringComparer.Ordinal.Equals(monitoring.SessionId, _sessionId) &&
            monitoring.TimestampUtc == observedAtUtc &&
            monitoring.HealthStatus == expected &&
            ControlledProductionPilotText.IsUsableIdentifier(monitoring.ValidationSummary) &&
            ControlledProductionPilotText.IsUsableIdentifier(monitoring.DifferenceSummary) &&
            monitoring.RollbackStatus == _rollback!.ValidationStatus &&
            !monitoring.ContainsSecrets && !monitoring.ContainsCredentialMaterial &&
            !monitoring.ContainsRawLogs && !monitoring.ContainsDatabaseContent &&
            !monitoring.ImplementsTelemetry;
    }

    private bool StopDecisionValid(PilotStopDecision? decision) => decision is not null &&
        ControlledProductionPilotText.IsUsableIdentifier(decision.DecisionId) &&
        StringComparer.Ordinal.Equals(decision.PilotId, _context!.PilotId) &&
        StringComparer.Ordinal.Equals(decision.SessionId, _sessionId) &&
        Enum.IsDefined(decision.Reason) &&
        ControlledProductionPilotText.IsUsableIdentifier(decision.EvidenceReference) &&
        decision.DecidedAtUtc.Offset == TimeSpan.Zero && TimeInWindow(decision.DecidedAtUtc) &&
        decision.DecidedAtUtc >= _startedAtUtc && !decision.ExecutesRollback &&
        !decision.PerformsDestructiveAction && !decision.AutomaticallyStopsProduction;

    private bool TimeInWindow(DateTimeOffset value) => value.Offset == TimeSpan.Zero &&
        value >= _context!.StartWindowUtc && value <= _context.EndWindowUtc;

    private ControlledPilotSessionOperationResult Fail(
        string reasonCode,
        ControlledPilotOperationStatus status = ControlledPilotOperationStatus.Failed)
    {
        lock (_gate)
        {
            if (_disposed) return Disposed();
            _observationInProgress = false;
            _state = ControlledPilotSessionState.Failed;
            return new(status, _state, reasonCode);
        }
    }

    private ControlledPilotSessionOperationResult FailLocked(string reasonCode)
    {
        _observationInProgress = false;
        _state = ControlledPilotSessionState.Failed;
        return new(ControlledPilotOperationStatus.Failed, _state, reasonCode);
    }

    private ControlledPilotSessionOperationResult Accepted(
        string reasonCode,
        ControlledPilotEvidence? evidence = null) =>
        new(ControlledPilotOperationStatus.Accepted, _state, reasonCode, evidence);

    private ControlledPilotSessionOperationResult Reject(string reasonCode) =>
        new(ControlledPilotOperationStatus.Blocked, _state, reasonCode);

    private ControlledPilotSessionOperationResult Disposed() =>
        new(ControlledPilotOperationStatus.Disposed,
            ControlledPilotSessionState.Disposed, "production-pilot-disposed");

    private static IReadOnlyDictionary<PilotValidationWorkflow,
        IControlledProductionPilotObserver> EmptyObservers() =>
        new ReadOnlyDictionary<PilotValidationWorkflow,
            IControlledProductionPilotObserver>(
            new Dictionary<PilotValidationWorkflow,
                IControlledProductionPilotObserver>());
}
