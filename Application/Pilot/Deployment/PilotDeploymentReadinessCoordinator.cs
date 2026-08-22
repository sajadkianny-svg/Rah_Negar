using System.Collections.ObjectModel;
using Rah_Negar.Foundation.Application.Pilot.Validation;

namespace Rah_Negar.Foundation.Application.Pilot.Deployment;

public sealed class PilotDeploymentReadinessCoordinator
{
    private static readonly IReadOnlySet<PilotEnvironmentValidationKind>
        RequiredEnvironmentValidations = new HashSet<PilotEnvironmentValidationKind>(
            Enum.GetValues<PilotEnvironmentValidationKind>());
    private static readonly IReadOnlySet<PilotApprovalGateKind> RequiredApprovalGates =
        new HashSet<PilotApprovalGateKind>(Enum.GetValues<PilotApprovalGateKind>());
    private static readonly IReadOnlySet<PilotStopConditionKind> RequiredStopConditions =
        new HashSet<PilotStopConditionKind>(Enum.GetValues<PilotStopConditionKind>());
    private static readonly IReadOnlySet<PilotMonitoringSignalKind> RequiredMonitoringSignals =
        new HashSet<PilotMonitoringSignalKind>(Enum.GetValues<PilotMonitoringSignalKind>());
    private static readonly IReadOnlySet<PilotDeploymentChecklistItem> RequiredChecklistItems =
        new HashSet<PilotDeploymentChecklistItem>(Enum.GetValues<PilotDeploymentChecklistItem>());
    private readonly IReadOnlyDictionary<PilotEnvironmentValidationKind,
        IPilotEnvironmentReadinessValidator> _validators;
    private readonly string? _configurationIssue;

    public PilotDeploymentReadinessCoordinator(
        IEnumerable<IPilotEnvironmentReadinessValidator>? validators)
    {
        try
        {
            IPilotEnvironmentReadinessValidator[] supplied = validators?.ToArray() ?? [];
            if (supplied.Any(validator => validator is null))
            {
                _configurationIssue = "readiness-validator-invalid";
                _validators = EmptyValidators();
                return;
            }
            IGrouping<PilotEnvironmentValidationKind, IPilotEnvironmentReadinessValidator>[]
                groups = supplied.GroupBy(validator => validator.Kind).ToArray();
            if (groups.Any(group => group.Count() != 1))
            {
                _configurationIssue = "readiness-validator-duplicate";
                _validators = EmptyValidators();
                return;
            }
            _validators = new ReadOnlyDictionary<PilotEnvironmentValidationKind,
                IPilotEnvironmentReadinessValidator>(groups.ToDictionary(
                    group => group.Key, group => group.Single()));
            if (_validators.Count != RequiredEnvironmentValidations.Count ||
                !_validators.Keys.All(RequiredEnvironmentValidations.Contains))
                _configurationIssue = "readiness-validator-set-incomplete";
        }
        catch
        {
            _configurationIssue = "readiness-configuration-failed";
            _validators = EmptyValidators();
        }
    }

    public bool Deploys => false;
    public bool Activates => false;
    public bool Migrates => false;
    public bool ModifiesDatabase => false;
    public bool PerformsEsdCutover => false;
    public bool SwitchesAuthority => false;
    public bool UsesServiceLocator => false;
    public bool AutomaticallyRuns => false;
    public bool FallsBackToProduction => false;

    public PilotDeploymentReadinessResult Evaluate(
        PilotDeploymentReadinessContext? context,
        PilotDeploymentManifest? manifest,
        PilotRollbackReadiness? rollback,
        IEnumerable<PilotApprovalGate>? approvals,
        IEnumerable<PilotStopCondition>? stopConditions,
        PilotDeploymentChecklist? checklist,
        PilotMonitoringReadinessPlan? monitoring)
    {
        try
        {
            string? contextIssue = ValidateContext(context);
            if (contextIssue is not null) return Blocked(contextIssue);
            if (_configurationIssue is not null) return Blocked(_configurationIssue);

            PilotApprovalGate[] suppliedApprovals = approvals?.ToArray() ?? [];
            PilotStopCondition[] suppliedStopConditions = stopConditions?.ToArray() ?? [];
            var blockers = new List<string>();
            var reviews = new List<string>();

            ValidateManifest(manifest, blockers, reviews);
            ValidateRollback(context!, rollback, blockers, reviews);
            ValidateApprovals(context!, suppliedApprovals, blockers, reviews);
            ValidateStopConditions(suppliedStopConditions, blockers);
            ValidateChecklist(checklist, blockers, reviews);
            ValidateMonitoring(monitoring, blockers);
            ValidateWorkflowEvidence(context!, blockers, reviews);

            if (manifest is null || rollback is null)
            {
                IReadOnlyList<string> incomplete = Normalize(blockers.Concat(reviews));
                return new(PilotDeploymentReadinessStatus.Blocked, "readiness-blocked",
                    incomplete, null);
            }

            var validations = new List<PilotEnvironmentValidationEvidence>();
            foreach (PilotEnvironmentValidationKind kind in
                RequiredEnvironmentValidations.Order())
            {
                PilotEnvironmentValidationEvidence? evidence;
                try
                {
                    evidence = _validators[kind].Validate(context!, manifest!);
                }
                catch
                {
                    return Blocked("readiness-environment-validator-failed");
                }
                if (!EnvironmentEvidenceValid(context!, evidence, kind))
                {
                    blockers.Add("environment-evidence-invalid");
                    continue;
                }
                validations.Add(evidence!);
                if (evidence!.Status == PilotReadinessGateStatus.Failed)
                    blockers.Add("environment-validation-failed");
                else if (evidence.Status == PilotReadinessGateStatus.RequiresReview)
                    reviews.Add("environment-validation-review-required");
            }

            PilotDeploymentReadinessStatus status;
            string reasonCode;
            IReadOnlyList<string> findings;
            if (blockers.Count > 0)
            {
                status = PilotDeploymentReadinessStatus.Blocked;
                reasonCode = "readiness-blocked";
                findings = Normalize(blockers.Concat(reviews));
            }
            else if (reviews.Count > 0)
            {
                status = PilotDeploymentReadinessStatus.RequiresReview;
                reasonCode = "readiness-review-required";
                findings = Normalize(reviews);
            }
            else
            {
                status = PilotDeploymentReadinessStatus.Ready;
                reasonCode = "readiness-ready";
                findings = Array.Empty<string>();
            }

            var package = new PilotDeploymentEvidencePackage(
                $"{context!.ReadinessId}:evidence", status, validations,
                suppliedApprovals, findings, rollback!, manifest!.ManifestId,
                context.TimestampUtc);
            return new(status, reasonCode, findings, package);
        }
        catch
        {
            return Blocked("readiness-evaluation-failed");
        }
    }

    private static string? ValidateContext(PilotDeploymentReadinessContext? context)
    {
        if (context is null) return "readiness-context-required";
        if (!context.ExplicitlyRequested) return "readiness-explicit-request-required";
        if (!PilotDeploymentText.IsUsableIdentifier(context.ReadinessId) ||
            !PilotDeploymentText.IsUsableIdentifier(context.PilotScope) ||
            !PilotDeploymentText.IsUsableIdentifier(context.TargetEnvironmentId) ||
            !PilotDeploymentText.IsUsableIdentifier(context.RollbackReference))
            return "readiness-context-identifier-invalid";
        if (context.TimestampUtc.Offset != TimeSpan.Zero ||
            context.RequiredFeatures.Count == 0 ||
            context.RequiredFeatures.Any(feature => !Enum.IsDefined(feature)) ||
            context.ApprovalReferences.Count != RequiredApprovalGates.Count ||
            context.ApprovalReferences.Any(reference =>
                !PilotDeploymentText.IsUsableIdentifier(reference)) ||
            !Enum.IsDefined(context.ValidationStatus))
            return "readiness-context-invalid";
        return null;
    }

    private static void ValidateManifest(
        PilotDeploymentManifest? manifest,
        ICollection<string> blockers,
        ICollection<string> reviews)
    {
        if (manifest is null || !PilotDeploymentText.IsUsableIdentifier(manifest.ManifestId) ||
            !PilotDeploymentText.IsUsableIdentifier(manifest.Version) ||
            !PilotDeploymentText.IsUsableIdentifier(manifest.BuildFingerprint) ||
            manifest.ArtifactIdentifiers.Count == 0 || manifest.DependencySummary.Count == 0 ||
            manifest.ArtifactIdentifiers.Any(id => !PilotDeploymentText.IsUsableIdentifier(id)) ||
            manifest.DependencySummary.Any(id => !PilotDeploymentText.IsUsableIdentifier(id)) ||
            !Enum.IsDefined(manifest.ValidationStatus))
        {
            blockers.Add("deployment-manifest-invalid");
            return;
        }
        if (manifest.ValidationStatus == PilotReadinessGateStatus.Failed)
            blockers.Add("deployment-manifest-validation-failed");
        else if (manifest.ValidationStatus == PilotReadinessGateStatus.RequiresReview)
            reviews.Add("deployment-manifest-review-required");
    }

    private static void ValidateRollback(
        PilotDeploymentReadinessContext context,
        PilotRollbackReadiness? rollback,
        ICollection<string> blockers,
        ICollection<string> reviews)
    {
        if (rollback is null ||
            !StringComparer.Ordinal.Equals(context.RollbackReference, rollback.RollbackPlanId) ||
            !PilotDeploymentText.IsUsableIdentifier(rollback.RollbackPlanId) ||
            !PilotDeploymentText.IsUsableIdentifier(rollback.RestorePointReference) ||
            !PilotDeploymentText.IsUsableIdentifier(rollback.OwnerReference) ||
            !PilotDeploymentText.IsUsableIdentifier(rollback.EvidenceReference) ||
            !Enum.IsDefined(rollback.ValidationStatus))
        {
            blockers.Add("rollback-readiness-invalid");
            return;
        }
        if (rollback.ValidationStatus == PilotRollbackValidationStatus.Unavailable)
            blockers.Add("rollback-unavailable");
        else if (rollback.ValidationStatus == PilotRollbackValidationStatus.RequiresReview)
            reviews.Add("rollback-review-required");
    }

    private static void ValidateApprovals(
        PilotDeploymentReadinessContext context,
        IReadOnlyCollection<PilotApprovalGate> approvals,
        ICollection<string> blockers,
        ICollection<string> reviews)
    {
        if (approvals.Any(approval => approval is null) ||
            approvals.Count != RequiredApprovalGates.Count ||
            approvals.GroupBy(approval => approval.Kind).Any(group => group.Count() != 1) ||
            !approvals.Select(approval => approval.Kind).All(RequiredApprovalGates.Contains))
        {
            blockers.Add("approval-gates-incomplete");
            return;
        }
        string[] references = approvals.Select(approval => approval.ApprovalReference)
            .Order(StringComparer.Ordinal).ToArray();
        if (!references.SequenceEqual(context.ApprovalReferences, StringComparer.Ordinal))
            blockers.Add("approval-references-mismatch");
        foreach (PilotApprovalGate approval in approvals)
        {
            if (!Enum.IsDefined(approval.Kind) || !Enum.IsDefined(approval.Status) ||
                !PilotDeploymentText.IsUsableIdentifier(approval.ApprovalReference) ||
                !PilotDeploymentText.IsUsableIdentifier(approval.EvidenceReference) ||
                approval.ReviewedAtUtc.Offset != TimeSpan.Zero ||
                approval.ReviewedAtUtc > context.TimestampUtc)
            {
                blockers.Add("approval-gate-invalid");
                continue;
            }
            if (approval.Status == PilotApprovalGateStatus.Missing)
                blockers.Add("approval-missing");
            else if (approval.Status == PilotApprovalGateStatus.RequiresReview)
                reviews.Add("approval-review-required");
        }
    }

    private static void ValidateStopConditions(
        IReadOnlyCollection<PilotStopCondition> conditions,
        ICollection<string> blockers)
    {
        if (conditions.Any(condition => condition is null) ||
            conditions.Count != RequiredStopConditions.Count ||
            conditions.GroupBy(condition => condition.Kind).Any(group => group.Count() != 1) ||
            !conditions.Select(condition => condition.Kind).All(RequiredStopConditions.Contains))
        {
            blockers.Add("stop-conditions-incomplete");
            return;
        }
        foreach (PilotStopCondition condition in conditions)
        {
            if (!Enum.IsDefined(condition.Kind) ||
                !PilotDeploymentText.IsUsableIdentifier(condition.EvidenceReference))
                blockers.Add("stop-condition-invalid");
            else if (condition.Triggered)
                blockers.Add($"stop-condition-{condition.Kind.ToString().ToLowerInvariant()}");
        }
    }

    private static void ValidateMonitoring(
        PilotMonitoringReadinessPlan? monitoring,
        ICollection<string> blockers)
    {
        if (monitoring is null || !PilotDeploymentText.IsUsableIdentifier(monitoring.PlanId) ||
            !PilotDeploymentText.IsUsableIdentifier(monitoring.OwnerReference) ||
            !PilotDeploymentText.IsUsableIdentifier(monitoring.EscalationReference) ||
            monitoring.RequiredSignals.Count != RequiredMonitoringSignals.Count ||
            !monitoring.RequiredSignals.All(RequiredMonitoringSignals.Contains))
            blockers.Add("monitoring-readiness-incomplete");
    }

    private static void ValidateChecklist(
        PilotDeploymentChecklist? checklist,
        ICollection<string> blockers,
        ICollection<string> reviews)
    {
        if (checklist is null || checklist.Entries.Any(entry => entry is null) ||
            checklist.Entries.Count != RequiredChecklistItems.Count ||
            checklist.Entries.GroupBy(entry => entry.Item).Any(group => group.Count() != 1) ||
            !checklist.Entries.Select(entry => entry.Item).All(RequiredChecklistItems.Contains))
        {
            blockers.Add("deployment-checklist-incomplete");
            return;
        }
        foreach (PilotDeploymentChecklistEntry entry in checklist.Entries)
        {
            if (!Enum.IsDefined(entry.Item) || !Enum.IsDefined(entry.Status) ||
                !PilotDeploymentText.IsUsableIdentifier(entry.EvidenceReference))
                blockers.Add("deployment-checklist-entry-invalid");
            else if (entry.Status == PilotReadinessGateStatus.Failed)
                blockers.Add("deployment-checklist-failed");
            else if (entry.Status == PilotReadinessGateStatus.RequiresReview)
                reviews.Add("deployment-checklist-review-required");
        }
    }

    private static void ValidateWorkflowEvidence(
        PilotDeploymentReadinessContext context,
        ICollection<string> blockers,
        ICollection<string> reviews)
    {
        if (context.ValidationStatus is PilotValidationResultStatus.Failed or
            PilotValidationResultStatus.Blocked)
            blockers.Add("pilot-validation-failed");
        else if (context.ValidationStatus == PilotValidationResultStatus.DifferenceDetected)
            reviews.Add("pilot-validation-difference-review-required");
    }

    private static bool EnvironmentEvidenceValid(
        PilotDeploymentReadinessContext context,
        PilotEnvironmentValidationEvidence? evidence,
        PilotEnvironmentValidationKind expectedKind) => evidence is not null &&
        evidence.Kind == expectedKind && Enum.IsDefined(evidence.Status) &&
        PilotDeploymentText.IsUsableIdentifier(evidence.EvidenceReference) &&
        evidence.ObservedAtUtc.Offset == TimeSpan.Zero &&
        evidence.ObservedAtUtc <= context.TimestampUtc && evidence.IsReadOnly &&
        evidence.IsDeterministic && !evidence.ModifiesEnvironment;

    private static PilotDeploymentReadinessResult Blocked(string reasonCode) =>
        new(PilotDeploymentReadinessStatus.Blocked, reasonCode, [reasonCode], null);

    private static IReadOnlyList<string> Normalize(IEnumerable<string> findings) =>
        new ReadOnlyCollection<string>(findings.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray());

    private static IReadOnlyDictionary<PilotEnvironmentValidationKind,
        IPilotEnvironmentReadinessValidator> EmptyValidators() =>
        new ReadOnlyDictionary<PilotEnvironmentValidationKind,
            IPilotEnvironmentReadinessValidator>(
            new Dictionary<PilotEnvironmentValidationKind,
                IPilotEnvironmentReadinessValidator>());
}
