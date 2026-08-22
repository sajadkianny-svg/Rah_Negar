using System.Collections.ObjectModel;
using Rah_Negar.Foundation.Application.Integration;
using Rah_Negar.Foundation.Application.Pilot.Composition;
using Rah_Negar.Foundation.Application.Pilot.Presentation;

namespace Rah_Negar.Foundation.Application.Pilot.Validation;

public sealed class DeterministicPilotWorkflowObservationComparer :
    IPilotWorkflowObservationComparer
{
    public PilotWorkflowComparisonResult Compare(
        PilotWorkflowValidationContext context,
        PilotWorkflowObservationResult legacyObservation,
        PilotWorkflowObservationResult targetObservation)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(legacyObservation);
        ArgumentNullException.ThrowIfNull(targetObservation);
        bool match = StringComparer.Ordinal.Equals(legacyObservation.Fingerprint,
            targetObservation.Fingerprint);
        return new(context.SelectedWorkflow, legacyObservation.Fingerprint,
            targetObservation.Fingerprint,
            match ? PilotDifferenceClassification.Match : PilotDifferenceClassification.Difference,
            match ? ShadowDifferenceSeverity.None : ShadowDifferenceSeverity.Warning,
            $"{context.ValidationId}:comparison");
    }
}

public sealed class PilotValidationEvidenceFactory : IPilotValidationEvidenceFactory
{
    public PilotValidationEvidence Create(
        PilotWorkflowValidationContext context,
        PilotWorkflowComparisonResult comparison)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(comparison);
        PilotValidationResultStatus status = comparison.Classification switch
        {
            PilotDifferenceClassification.Match => PilotValidationResultStatus.Completed,
            PilotDifferenceClassification.Difference => PilotValidationResultStatus.DifferenceDetected,
            _ => PilotValidationResultStatus.Failed
        };
        return new(context.ValidationId, context.PilotId, context.SelectedWorkflow,
            context.ValidationTimestampUtc, status, comparison.Classification,
            comparison.Severity, context.CorrelationId, comparison.EvidenceReference);
    }
}

public sealed class PilotWorkflowValidationCoordinator : IDisposable
{
    private static readonly IReadOnlySet<string> RequiredCapabilities =
        new HashSet<string>(PilotUiCapabilities.All, StringComparer.Ordinal);
    private readonly object _gate = new();
    private readonly IReadOnlyDictionary<(PilotValidationWorkflow, PilotObservationBoundary),
        IPilotWorkflowObserver> _observers;
    private readonly IPilotWorkflowObservationComparer? _comparer;
    private readonly IPilotValidationEvidenceFactory? _evidenceFactory;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly string? _configurationIssue;
    private PilotValidationLifecycleState _lifecycle = PilotValidationLifecycleState.Created;
    private bool _disposed;

    public PilotWorkflowValidationCoordinator(
        IEnumerable<IPilotWorkflowObserver>? observers,
        IPilotWorkflowObservationComparer? comparer,
        IPilotValidationEvidenceFactory? evidenceFactory)
    {
        _comparer = comparer;
        _evidenceFactory = evidenceFactory;
        try
        {
            IPilotWorkflowObserver[] supplied = observers?.ToArray() ?? [];
            if (supplied.Any(observer => observer is null))
            {
                _configurationIssue = "validation-observer-invalid";
                _observers = EmptyObservers();
                return;
            }
            var groups = supplied.GroupBy(observer =>
                (observer.Descriptor.Workflow, observer.Descriptor.Boundary)).ToArray();
            if (groups.Any(group => group.Count() != 1))
            {
                _configurationIssue = "validation-observer-duplicate";
                _observers = EmptyObservers();
                return;
            }
            _observers = new ReadOnlyDictionary<(PilotValidationWorkflow,
                PilotObservationBoundary), IPilotWorkflowObserver>(groups.ToDictionary(
                    group => group.Key, group => group.Single()));
            if (_comparer is null) _configurationIssue = "validation-comparer-required";
            else if (_evidenceFactory is null)
                _configurationIssue = "validation-evidence-factory-required";
        }
        catch
        {
            _configurationIssue = "validation-configuration-failed";
            _observers = EmptyObservers();
        }
    }

    public bool AutomaticallyRuns => false;
    public bool UsesScheduler => false;
    public bool UsesPolling => false;
    public bool Retries => false;
    public bool ExecutesProductionWorkflow => false;
    public bool MutatesState => false;
    public bool FallsBackToProduction => false;
    public bool SwitchesAuthority => false;

    public PilotValidationLifecycleState Lifecycle
    {
        get { lock (_gate) return _lifecycle; }
    }

    public async ValueTask<PilotWorkflowValidationResult> ValidateAsync(
        PilotWorkflowValidationContext? context,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_disposed) return Failure("validation-coordinator-disposed");
            if (_lifecycle != PilotValidationLifecycleState.Created)
                return Failure("validation-already-attempted");
            _lifecycle = PilotValidationLifecycleState.Validating;
        }

        string? contextIssue = ValidateContext(context);
        if (contextIssue is not null) return Fail(contextIssue);
        if (_configurationIssue is not null) return Fail(_configurationIssue);
        if (!TryGetObservers(context!, out IPilotWorkflowObserver? legacy,
                out IPilotWorkflowObserver? target, out string? observerIssue))
            return Fail(observerIssue!);

        CancellationTokenSource? linked = null;
        PilotWorkflowObservationResult? legacyResult = null;
        PilotWorkflowObservationResult? targetResult = null;
        PilotWorkflowComparisonResult? comparison = null;
        try
        {
            linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _lifetime.Token);
            try
            {
                legacyResult = await legacy!.ObserveAsync(context!, linked.Token)
                    .ConfigureAwait(false);
                targetResult = await target!.ObserveAsync(context!, linked.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Fail("validation-canceled", legacyResult, targetResult);
            }
            catch
            {
                return Fail("validation-observer-failed", legacyResult, targetResult);
            }
            if (linked.IsCancellationRequested)
                return Fail("validation-canceled", legacyResult, targetResult);
            if (!ObservationValid(context!, legacyResult, PilotObservationBoundary.LegacyAuthoritative) ||
                !ObservationValid(context!, targetResult, PilotObservationBoundary.TargetReadOnly))
                return Fail("validation-observation-invalid", legacyResult, targetResult);

            try
            {
                comparison = _comparer!.Compare(context!, legacyResult!, targetResult!);
            }
            catch
            {
                return Fail("validation-comparison-failed", legacyResult, targetResult);
            }
            if (!ComparisonValid(context!, comparison, legacyResult!, targetResult!))
                return Fail("validation-comparison-invalid", legacyResult, targetResult, comparison);

            PilotValidationEvidence evidence;
            try
            {
                evidence = _evidenceFactory!.Create(context!, comparison);
            }
            catch
            {
                return Fail("validation-evidence-creation-failed", legacyResult,
                    targetResult, comparison);
            }
            if (!EvidenceValid(context!, comparison, evidence))
                return Fail("validation-evidence-invalid", legacyResult, targetResult, comparison);

            PilotValidationResultStatus status = comparison.Classification ==
                PilotDifferenceClassification.Match
                ? PilotValidationResultStatus.Completed
                : PilotValidationResultStatus.DifferenceDetected;
            lock (_gate)
            {
                if (_disposed) return Failure("validation-coordinator-disposed");
                _lifecycle = PilotValidationLifecycleState.Completed;
            }
            return new(status, status == PilotValidationResultStatus.Completed
                ? "validation-completed" : "validation-difference-recorded",
                legacyResult, targetResult, comparison, evidence);
        }
        catch
        {
            return Fail("validation-failed", legacyResult, targetResult, comparison);
        }
        finally
        {
            linked?.Dispose();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _lifecycle = PilotValidationLifecycleState.Disposed;
        }
        try { _lifetime.Cancel(); }
        catch { }
        try { _lifetime.Dispose(); }
        catch { }
    }

    private bool TryGetObservers(
        PilotWorkflowValidationContext context,
        out IPilotWorkflowObserver? legacy,
        out IPilotWorkflowObserver? target,
        out string? issue)
    {
        _observers.TryGetValue((context.SelectedWorkflow,
            PilotObservationBoundary.LegacyAuthoritative), out legacy);
        _observers.TryGetValue((context.SelectedWorkflow,
            PilotObservationBoundary.TargetReadOnly), out target);
        if (legacy is null || target is null)
        {
            issue = "validation-observer-not-configured";
            return false;
        }
        if (!ObserverValid(context, legacy, PilotObservationBoundary.LegacyAuthoritative) ||
            !ObserverValid(context, target, PilotObservationBoundary.TargetReadOnly))
        {
            issue = "validation-observer-unsafe";
            return false;
        }
        issue = null;
        return true;
    }

    private static bool ObserverValid(
        PilotWorkflowValidationContext context,
        IPilotWorkflowObserver observer,
        PilotObservationBoundary boundary)
    {
        PilotWorkflowObserverDescriptor descriptor = observer.Descriptor;
        string expectedId = boundary == PilotObservationBoundary.LegacyAuthoritative
            ? context.Scope.LegacyObserverId : context.Scope.TargetObserverId;
        return descriptor.Workflow == context.SelectedWorkflow && descriptor.Boundary == boundary &&
            descriptor.Availability == PilotStateAvailability.Available &&
            descriptor.Safety is { IsSafe: true } &&
            StringComparer.Ordinal.Equals(descriptor.ObserverId, expectedId) &&
            ContractMatchesWorkflow(observer, context.SelectedWorkflow);
    }

    private static bool ContractMatchesWorkflow(
        IPilotWorkflowObserver observer,
        PilotValidationWorkflow workflow) => workflow switch
    {
        PilotValidationWorkflow.Authentication =>
            observer is IAuthenticationPilotValidationObserver,
        PilotValidationWorkflow.Reporting => observer is IReportingPilotValidationObserver,
        PilotValidationWorkflow.RuntimeEvent => observer is IRuntimeEventPilotValidationObserver,
        PilotValidationWorkflow.ProtectedSettings =>
            observer is IProtectedSettingsPilotValidationObserver,
        PilotValidationWorkflow.Export => observer is IExportPilotValidationObserver,
        _ => false
    };

    private static string? ValidateContext(PilotWorkflowValidationContext? context)
    {
        if (context is null) return "validation-context-required";
        if (!context.ExplicitlyApproved) return "validation-approval-required";
        if (!Enum.IsDefined(context.SelectedWorkflow) || context.Scope is null ||
            context.Scope.Workflow != context.SelectedWorkflow)
            return "validation-workflow-scope-invalid";
        if (!PilotValidationText.IsSafeIdentifier(context.ValidationId) ||
            !PilotValidationText.IsSafeIdentifier(context.PilotId) ||
            !PilotValidationText.IsSafeIdentifier(context.CorrelationId) ||
            !PilotValidationText.IsSafeIdentifier(context.CompositionId) ||
            !PilotValidationText.IsSafeIdentifier(context.Scope.ScopeId) ||
            !PilotValidationText.IsSafeIdentifier(context.Scope.LegacyObserverId) ||
            !PilotValidationText.IsSafeIdentifier(context.Scope.TargetObserverId))
            return "validation-identifier-invalid";
        if (context.ValidationTimestampUtc.Offset != TimeSpan.Zero ||
            !context.Scope.ObserveLegacy || !context.Scope.ObserveTarget ||
            !context.Scope.CompareResults || context.Scope.SubjectIds.Count == 0 ||
            context.Scope.SubjectIds.Any(subject => !PilotValidationText.IsSafeIdentifier(subject)))
            return "validation-scope-invalid";
        PilotCapabilityEvidence? evidence = context.CapabilityEvidence;
        if (evidence is null || !StringComparer.Ordinal.Equals(context.PilotId, evidence.PilotId) ||
            !StringComparer.Ordinal.Equals(context.CorrelationId, evidence.CorrelationId) ||
            evidence.ObservedAtUtc.Offset != TimeSpan.Zero ||
            evidence.ObservedAtUtc > context.ValidationTimestampUtc ||
            evidence.AvailableCapabilities.Count != RequiredCapabilities.Count ||
            !evidence.AvailableCapabilities.All(RequiredCapabilities.Contains))
            return "validation-capability-evidence-invalid";
        return null;
    }

    private static bool ObservationValid(
        PilotWorkflowValidationContext context,
        PilotWorkflowObservationResult? observation,
        PilotObservationBoundary boundary) => observation is not null &&
        observation.Workflow == context.SelectedWorkflow && observation.Boundary == boundary &&
        observation.Status == PilotObservationStatus.Available &&
        PilotValidationText.IsSafeIdentifier(observation.Fingerprint) &&
        PilotValidationText.IsSafeIdentifier(observation.EvidenceReference) &&
        observation.ObservedAtUtc.Offset == TimeSpan.Zero &&
        observation.ObservedAtUtc <= context.ValidationTimestampUtc;

    private static bool ComparisonValid(
        PilotWorkflowValidationContext context,
        PilotWorkflowComparisonResult? comparison,
        PilotWorkflowObservationResult legacy,
        PilotWorkflowObservationResult target) => comparison is not null &&
        comparison.Workflow == context.SelectedWorkflow &&
        Enum.IsDefined(comparison.Classification) && Enum.IsDefined(comparison.Severity) &&
        comparison.Classification is PilotDifferenceClassification.Match or
            PilotDifferenceClassification.Difference &&
        StringComparer.Ordinal.Equals(comparison.LegacyFingerprint, legacy.Fingerprint) &&
        StringComparer.Ordinal.Equals(comparison.TargetFingerprint, target.Fingerprint) &&
        PilotValidationText.IsSafeIdentifier(comparison.EvidenceReference) &&
        comparison.LegacyRemainsAuthoritative && !comparison.AutomaticallyCorrectsDifference &&
        !comparison.SwitchesAuthority;

    private static bool EvidenceValid(
        PilotWorkflowValidationContext context,
        PilotWorkflowComparisonResult comparison,
        PilotValidationEvidence? evidence) => evidence is not null &&
        StringComparer.Ordinal.Equals(evidence.ValidationId, context.ValidationId) &&
        StringComparer.Ordinal.Equals(evidence.PilotId, context.PilotId) &&
        StringComparer.Ordinal.Equals(evidence.CorrelationId, context.CorrelationId) &&
        evidence.Workflow == context.SelectedWorkflow &&
        evidence.TimestampUtc == context.ValidationTimestampUtc &&
        evidence.ResultStatus == (comparison.Classification == PilotDifferenceClassification.Match
            ? PilotValidationResultStatus.Completed
            : PilotValidationResultStatus.DifferenceDetected) &&
        evidence.ComparisonStatus == comparison.Classification &&
        evidence.Severity == comparison.Severity &&
        PilotValidationText.IsSafeIdentifier(evidence.EvidenceReference) &&
        !evidence.GrantsAuthority;

    private PilotWorkflowValidationResult Fail(
        string reasonCode,
        PilotWorkflowObservationResult? legacy = null,
        PilotWorkflowObservationResult? target = null,
        PilotWorkflowComparisonResult? comparison = null)
    {
        lock (_gate)
        {
            if (_disposed) return Failure("validation-coordinator-disposed");
            _lifecycle = PilotValidationLifecycleState.Failed;
        }
        return Failure(reasonCode, legacy, target, comparison);
    }

    private static PilotWorkflowValidationResult Failure(
        string reasonCode,
        PilotWorkflowObservationResult? legacy = null,
        PilotWorkflowObservationResult? target = null,
        PilotWorkflowComparisonResult? comparison = null) => new(
            PilotValidationResultStatus.Failed, reasonCode, legacy, target, comparison, null);

    private static IReadOnlyDictionary<(PilotValidationWorkflow, PilotObservationBoundary),
        IPilotWorkflowObserver> EmptyObservers() =>
        new ReadOnlyDictionary<(PilotValidationWorkflow, PilotObservationBoundary),
            IPilotWorkflowObserver>(new Dictionary<(PilotValidationWorkflow,
                PilotObservationBoundary), IPilotWorkflowObserver>());
}
