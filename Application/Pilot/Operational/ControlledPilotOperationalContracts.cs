using System.Collections.ObjectModel;
using Rah_Negar.Foundation.Application.Activation.Preparation;
using Rah_Negar.Foundation.Application.Pilot.Production;
using Rah_Negar.Foundation.Application.Pilot.Validation;

namespace Rah_Negar.Foundation.Application.Pilot.Operational;

public enum ControlledPilotOperationalPreflightStatus
{
    Ready,
    Blocked,
    RequiresReview
}

public enum ControlledPilotOperationalLifecycle
{
    Created,
    PreflightPassed,
    Approved,
    Started,
    Observing,
    ReviewRequired,
    Completed,
    Stopped,
    Failed,
    Disposed
}

public enum OperationalEvidenceStatus
{
    Verified,
    RequiresReview,
    Rejected
}

public sealed class ControlledPilotOperationalRehearsalContext
{
    public static readonly TimeSpan MaximumWindow = TimeSpan.FromHours(8);

    public ControlledPilotOperationalRehearsalContext(
        string rehearsalId,
        string pilotId,
        string sessionId,
        string correlationId,
        string releaseId,
        ControlledProductionPilotScope stationScope,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        IEnumerable<PilotValidationWorkflow> selectedWorkflows,
        string operatorReference,
        string phase9PreparationEvidenceReference,
        string rollbackEvidenceReference,
        bool explicitApproval)
    {
        ArgumentNullException.ThrowIfNull(selectedWorkflows);
        if (startUtc.Offset != TimeSpan.Zero || endUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Operational rehearsal boundaries must use UTC.");
        if (endUtc <= startUtc || endUtc - startUtc > MaximumWindow)
            throw new ArgumentOutOfRangeException(nameof(endUtc),
                "Operational rehearsal window must be positive and no longer than eight hours.");

        RehearsalId = OperationalText.SafeIdentifier(rehearsalId, "rehearsal-unavailable");
        PilotId = OperationalText.SafeIdentifier(pilotId, "pilot-unavailable");
        SessionId = OperationalText.SafeIdentifier(sessionId, "session-unavailable");
        CorrelationId = OperationalText.SafeIdentifier(correlationId, "correlation-unavailable");
        ReleaseId = OperationalText.SafeIdentifier(releaseId, "release-unavailable");
        StationScope = stationScope;
        StartUtc = startUtc;
        EndUtc = endUtc;
        SelectedWorkflows = new ReadOnlyCollection<PilotValidationWorkflow>(selectedWorkflows
            .Distinct().Order().ToArray());
        OperatorReference = OperationalText.SafeIdentifier(operatorReference,
            "operator-unavailable");
        Phase9PreparationEvidenceReference = OperationalText.SafeIdentifier(
            phase9PreparationEvidenceReference, "preparation-evidence-unavailable");
        RollbackEvidenceReference = OperationalText.SafeIdentifier(
            rollbackEvidenceReference, "rollback-evidence-unavailable");
        ExplicitApproval = explicitApproval;
    }

    public string RehearsalId { get; }
    public string PilotId { get; }
    public string SessionId { get; }
    public string CorrelationId { get; }
    public string ReleaseId { get; }
    public ControlledProductionPilotScope StationScope { get; }
    public DateTimeOffset StartUtc { get; }
    public DateTimeOffset EndUtc { get; }
    public IReadOnlyList<PilotValidationWorkflow> SelectedWorkflows { get; }
    public string OperatorReference { get; }
    public string Phase9PreparationEvidenceReference { get; }
    public string RollbackEvidenceReference { get; }
    public bool ExplicitApproval { get; }
    public bool UsesAmbientEnvironment => false;
    public bool AutomaticallyActivates => false;
    public bool ChangesProductionAuthority => false;
}

public sealed record OperationalReleaseEvidence(
    string BranchIdentifier,
    string ReleaseId,
    string RuntimeEvidenceReference,
    OperationalEvidenceStatus Status)
{
    public bool ReadsEnvironment => false;
}

public sealed record ControlledPilotPrerequisiteEvidence(
    string PilotId,
    string ReleaseId,
    string EvidenceReference,
    OperationalEvidenceStatus Status,
    bool LegacyRemainsAuthoritative,
    bool CompletedSingleObservationAttempt);

public sealed class ControlledPilotOperationalPreflightResult
{
    internal ControlledPilotOperationalPreflightResult(
        ControlledPilotOperationalPreflightStatus status,
        string reasonCode,
        IEnumerable<string> reasonCodes,
        DateTimeOffset evaluatedAtUtc)
    {
        Status = status;
        ReasonCode = reasonCode;
        ReasonCodes = new ReadOnlyCollection<string>(reasonCodes
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        EvaluatedAtUtc = evaluatedAtUtc;
    }

    public ControlledPilotOperationalPreflightStatus Status { get; }
    public string ReasonCode { get; }
    public IReadOnlyList<string> ReasonCodes { get; }
    public DateTimeOffset EvaluatedAtUtc { get; }
    public bool ExecutedProductionMutation => false;
    public bool AccessedProductionDatabase => false;
}

public interface IControlledPilotOperationalEvidenceDestination
{
    bool IsAvailable { get; }
    bool SupportsCancellation { get; }

    ValueTask<bool> WriteAsync(
        ControlledPilotOperationalEvidenceBundle bundle,
        CancellationToken cancellationToken = default);
}

public sealed class InMemoryControlledPilotOperationalEvidenceDestination :
    IControlledPilotOperationalEvidenceDestination
{
    private readonly object _gate = new();
    private readonly List<ControlledPilotOperationalEvidenceBundle> _bundles = [];

    public bool IsAvailable => true;
    public bool SupportsCancellation => true;
    public bool WritesFiles => false;
    public bool WritesDatabase => false;

    public IReadOnlyList<ControlledPilotOperationalEvidenceBundle> Bundles
    {
        get
        {
            lock (_gate)
                return new ReadOnlyCollection<ControlledPilotOperationalEvidenceBundle>(
                    _bundles.ToArray());
        }
    }

    public ValueTask<bool> WriteAsync(
        ControlledPilotOperationalEvidenceBundle bundle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_bundles.Any(existing => StringComparer.Ordinal.Equals(
                    existing.ContextIdentity.RehearsalId, bundle.ContextIdentity.RehearsalId)))
                return ValueTask.FromResult(false);
            _bundles.Add(bundle);
        }
        return ValueTask.FromResult(true);
    }
}

public sealed class ControlledPilotOperationalPreflight
{
    public const string RequiredBranchIdentifier = "phase9-operational-readiness";

    public ControlledPilotOperationalPreflightResult Evaluate(
        ControlledPilotOperationalRehearsalContext? context,
        OperationalReleaseEvidence? releaseEvidence,
        ProductionActivationReadinessResult? preparationEvidence,
        ControlledPilotPrerequisiteEvidence? pilotPrerequisiteEvidence,
        RollbackVerificationResult? rollbackEvidence,
        IEnumerable<IControlledPilotOperationalWorkflowObserver>? observers,
        IControlledPilotOperationalEvidenceDestination? evidenceDestination,
        DateTimeOffset evaluatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
                return Blocked("operational-preflight-canceled", evaluatedAtUtc);
            if (evaluatedAtUtc.Offset != TimeSpan.Zero)
                return Blocked("operational-preflight-time-invalid", evaluatedAtUtc);
            if (!ContextValid(context, evaluatedAtUtc))
                return Blocked("operational-preflight-context-invalid", evaluatedAtUtc);

            var blockers = new List<string>();
            var reviews = new List<string>();
            ValidateRelease(context!, releaseEvidence, blockers, reviews);
            ValidatePreparation(context!, preparationEvidence, blockers);
            ValidatePilotPrerequisite(context!, pilotPrerequisiteEvidence, blockers, reviews);
            ValidateRollback(context!, rollbackEvidence, blockers, reviews);
            ValidateObservers(context!, observers, blockers, reviews);

            if (evidenceDestination is null || !evidenceDestination.IsAvailable)
                blockers.Add("operational-preflight-evidence-destination-unavailable");
            else if (!evidenceDestination.SupportsCancellation)
                blockers.Add("operational-preflight-cancellation-unsupported");

            if (blockers.Count > 0)
                return Result(ControlledPilotOperationalPreflightStatus.Blocked,
                    "operational-preflight-blocked", blockers.Concat(reviews), evaluatedAtUtc);
            if (reviews.Count > 0)
                return Result(ControlledPilotOperationalPreflightStatus.RequiresReview,
                    "operational-preflight-review-required", reviews, evaluatedAtUtc);
            return Result(ControlledPilotOperationalPreflightStatus.Ready,
                "operational-preflight-ready", ["operational-preflight-ready"], evaluatedAtUtc);
        }
        catch
        {
            return Blocked("operational-preflight-evaluation-failed", evaluatedAtUtc);
        }
    }

    private static bool ContextValid(
        ControlledPilotOperationalRehearsalContext? context,
        DateTimeOffset evaluatedAtUtc) => context is not null && context.ExplicitApproval &&
        OperationalText.IsUsableIdentifier(context.RehearsalId) &&
        OperationalText.IsUsableIdentifier(context.PilotId) &&
        OperationalText.IsUsableIdentifier(context.SessionId) &&
        OperationalText.IsUsableIdentifier(context.CorrelationId) &&
        OperationalText.IsUsableIdentifier(context.ReleaseId) &&
        OperationalText.IsUsableIdentifier(context.OperatorReference) &&
        OperationalText.IsUsableIdentifier(context.Phase9PreparationEvidenceReference) &&
        OperationalText.IsUsableIdentifier(context.RollbackEvidenceReference) &&
        Enum.IsDefined(context.StationScope) && context.SelectedWorkflows.Count > 0 &&
        context.SelectedWorkflows.All(Enum.IsDefined) &&
        context.StartUtc.Offset == TimeSpan.Zero && context.EndUtc.Offset == TimeSpan.Zero &&
        context.StartUtc <= evaluatedAtUtc && evaluatedAtUtc <= context.EndUtc;

    private static void ValidateRelease(
        ControlledPilotOperationalRehearsalContext context,
        OperationalReleaseEvidence? evidence,
        ICollection<string> blockers,
        ICollection<string> reviews)
    {
        if (evidence is null || !OperationalText.IsUsableIdentifier(evidence.BranchIdentifier) ||
            !OperationalText.IsUsableIdentifier(evidence.ReleaseId) ||
            !OperationalText.IsUsableIdentifier(evidence.RuntimeEvidenceReference) ||
            !StringComparer.Ordinal.Equals(evidence.BranchIdentifier, RequiredBranchIdentifier) ||
            !StringComparer.Ordinal.Equals(evidence.ReleaseId, context.ReleaseId) ||
            evidence.Status == OperationalEvidenceStatus.Rejected)
            blockers.Add("operational-preflight-release-evidence-invalid");
        else if (evidence.Status == OperationalEvidenceStatus.RequiresReview)
            reviews.Add("operational-preflight-release-review-required");
    }

    private static void ValidatePreparation(
        ControlledPilotOperationalRehearsalContext context,
        ProductionActivationReadinessResult? evidence,
        ICollection<string> blockers)
    {
        if (evidence?.Decision != ProductionActivationPreparationDecision.ApprovedForPreparation ||
            evidence.EvidencePackage is null || evidence.Blockers.Count != 0 ||
            evidence.ReviewItems.Count != 0 || evidence.EvidencePackage.GrantsActivationPermission ||
            !evidence.EvidencePackage.ValidationSummary.LegacyRemainsAuthoritative ||
            !StringComparer.Ordinal.Equals(context.Phase9PreparationEvidenceReference,
                evidence.EvidencePackage.PackageId))
            blockers.Add("operational-preflight-preparation-evidence-invalid");
    }

    private static void ValidatePilotPrerequisite(
        ControlledPilotOperationalRehearsalContext context,
        ControlledPilotPrerequisiteEvidence? evidence,
        ICollection<string> blockers,
        ICollection<string> reviews)
    {
        if (evidence is null || !OperationalText.IsUsableIdentifier(evidence.EvidenceReference) ||
            !StringComparer.Ordinal.Equals(evidence.PilotId, context.PilotId) ||
            !StringComparer.Ordinal.Equals(evidence.ReleaseId, context.ReleaseId) ||
            !evidence.LegacyRemainsAuthoritative || !evidence.CompletedSingleObservationAttempt ||
            evidence.Status == OperationalEvidenceStatus.Rejected)
            blockers.Add("operational-preflight-pilot-prerequisite-invalid");
        else if (evidence.Status == OperationalEvidenceStatus.RequiresReview)
            reviews.Add("operational-preflight-pilot-prerequisite-review-required");
    }

    private static void ValidateRollback(
        ControlledPilotOperationalRehearsalContext context,
        RollbackVerificationResult? evidence,
        ICollection<string> blockers,
        ICollection<string> reviews)
    {
        if (evidence is null || evidence.ValidationStatus is RollbackEvidenceStatus.Unavailable or
                RollbackEvidenceStatus.Failed ||
            !OperationalText.IsUsableIdentifier(evidence.OwnerReference) ||
            !OperationalText.IsUsableIdentifier(evidence.EvidenceReference) ||
            !StringComparer.Ordinal.Equals(context.RollbackEvidenceReference,
                evidence.EvidenceReference))
            blockers.Add("operational-preflight-rollback-not-ready");
        else if (evidence.ValidationStatus == RollbackEvidenceStatus.RequiresReview)
            reviews.Add("operational-preflight-rollback-review-required");
    }

    private static void ValidateObservers(
        ControlledPilotOperationalRehearsalContext context,
        IEnumerable<IControlledPilotOperationalWorkflowObserver>? observers,
        ICollection<string> blockers,
        ICollection<string> reviews)
    {
        IControlledPilotOperationalWorkflowObserver[] supplied = observers?.ToArray() ?? [];
        if (supplied.Any(observer => observer is null) ||
            supplied.GroupBy(observer => observer.Workflow).Any(group => group.Count() != 1) ||
            supplied.Length != context.SelectedWorkflows.Count ||
            !supplied.Select(observer => observer.Workflow).Order()
                .SequenceEqual(context.SelectedWorkflows))
        {
            blockers.Add("operational-preflight-workflow-availability-invalid");
            return;
        }

        foreach (IControlledPilotOperationalWorkflowObserver observer in supplied)
        {
            if (!observer.IsAvailable || !observer.IsReadOnly ||
                !observer.SupportsCancellation ||
                !ContractMatchesWorkflow(observer))
                blockers.Add("operational-preflight-workflow-unavailable");
            if (!OperationalText.IsUsableIdentifier(observer.FingerprintSpecificationVersion))
                blockers.Add("operational-preflight-fingerprint-specification-unavailable");
            if (observer.RequiresReview)
                reviews.Add("operational-preflight-workflow-review-required");
        }
    }

    private static bool ContractMatchesWorkflow(
        IControlledPilotOperationalWorkflowObserver observer) => observer.Workflow switch
    {
        PilotValidationWorkflow.Authentication => observer is IAuthenticationOperationalObserver,
        PilotValidationWorkflow.Reporting => observer is IReportingOperationalObserver,
        PilotValidationWorkflow.RuntimeEvent => observer is IRuntimeEventOperationalObserver,
        PilotValidationWorkflow.ProtectedSettings => observer is IProtectedSettingsOperationalObserver,
        PilotValidationWorkflow.Export => observer is IExportOperationalObserver,
        _ => false
    };

    private static ControlledPilotOperationalPreflightResult Blocked(
        string code, DateTimeOffset atUtc) => Result(
            ControlledPilotOperationalPreflightStatus.Blocked, code, [code], atUtc);

    private static ControlledPilotOperationalPreflightResult Result(
        ControlledPilotOperationalPreflightStatus status,
        string reasonCode,
        IEnumerable<string> reasons,
        DateTimeOffset atUtc) => new(status, reasonCode, reasons,
            atUtc.Offset == TimeSpan.Zero ? atUtc : DateTimeOffset.UnixEpoch);
}

internal static class OperationalText
{
    private const int MaximumLength = 160;
    private static readonly string[] ForbiddenFragments =
    [
        "password", "passwd", "credential", "secret", "private-key", "private key",
        "exception", "stack-trace", "stack trace", "connection-string", "access-token",
        "authorization-token", "select ", "insert ", "update ", "delete ", "drop ",
        "alter ", "pragma ", "attach ", "../", "..\\"
    ];

    public static bool IsSafeIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= MaximumLength &&
        value.All(character => char.IsAsciiLetterOrDigit(character) ||
            character is '-' or '_' or '.' or ':') &&
        !ForbiddenFragments.Any(fragment => value.Contains(fragment,
            StringComparison.OrdinalIgnoreCase));

    public static bool IsUsableIdentifier(string? value) => IsSafeIdentifier(value) &&
        !value!.EndsWith("-unavailable", StringComparison.Ordinal);

    public static string SafeIdentifier(string? value, string fallback) =>
        IsSafeIdentifier(value) ? value! : fallback;
}
