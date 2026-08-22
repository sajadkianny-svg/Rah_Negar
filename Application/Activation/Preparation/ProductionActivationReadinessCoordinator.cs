using System.Collections.ObjectModel;
using Rah_Negar.Foundation.Application.Pilot.Deployment;
using Rah_Negar.Foundation.Application.Pilot.Validation;

namespace Rah_Negar.Foundation.Application.Activation.Preparation;

public sealed class ProductionActivationReadinessCoordinator
{
    private static readonly IReadOnlySet<ProductionActivationGateType> RequiredGates =
        new HashSet<ProductionActivationGateType>(Enum.GetValues<ProductionActivationGateType>());
    private static readonly IReadOnlySet<ProductionActivationStopConditionType>
        RequiredStopConditions = new HashSet<ProductionActivationStopConditionType>(
            Enum.GetValues<ProductionActivationStopConditionType>());
    private static readonly IReadOnlySet<ProductionActivationGateType> ApprovalGateTypes =
        new HashSet<ProductionActivationGateType>(
        [
            ProductionActivationGateType.SecurityReview,
            ProductionActivationGateType.OperationsReadiness,
            ProductionActivationGateType.DataOwnerApproval
        ]);

    public bool ActivatesFeatures => false;
    public bool ExecutesDeployment => false;
    public bool RunsMigration => false;
    public bool ModifiesDatabase => false;
    public bool PerformsEsdCutover => false;
    public bool SwitchesAuthority => false;
    public bool RegistersRoutes => false;
    public bool UsesServiceLocator => false;
    public bool AutomaticallyRuns => false;
    public bool HandlesPasswords => false;
    public bool MutatesCredentials => false;
    public bool CreatesRbac => false;
    public bool UsesSupportIdentity => false;
    public bool StoresSecrets => false;
    public bool EscalatesPermissions => false;

    public ProductionActivationReadinessResult Evaluate(
        ProductionActivationPreparationContext? context,
        IEnumerable<ProductionActivationGate>? gates,
        BackupVerificationResult? backup,
        RollbackVerificationResult? rollback,
        IEnumerable<ProductionActivationStopCondition>? stopConditions)
    {
        try
        {
            string? contextIssue = ValidateContext(context);
            if (contextIssue is not null) return Blocked(contextIssue);

            ProductionActivationGate[] suppliedGates = gates?.ToArray() ?? [];
            ProductionActivationStopCondition[] suppliedStops =
                stopConditions?.ToArray() ?? [];
            var blockers = new List<string>();
            var reviewItems = new List<string>();

            ValidateGates(context!, suppliedGates, blockers, reviewItems);
            ValidateBackup(context!, backup, blockers, reviewItems);
            ValidateRollback(context!, rollback, blockers, reviewItems);
            ValidateStopConditions(suppliedStops, blockers);
            ValidateStatusBindings(context!, suppliedGates, rollback, blockers, reviewItems);

            if (backup is null || rollback is null)
            {
                IReadOnlyList<string> normalizedBlockers = Normalize(blockers);
                return new(ProductionActivationPreparationDecision.Blocked,
                    "activation-preparation-blocked", normalizedBlockers,
                    Normalize(reviewItems), null);
            }

            ProductionActivationPreparationDecision decision;
            string reasonCode;
            if (blockers.Count > 0)
            {
                decision = ProductionActivationPreparationDecision.Blocked;
                reasonCode = "activation-preparation-blocked";
            }
            else if (reviewItems.Count > 0)
            {
                decision = ProductionActivationPreparationDecision.RequiresReview;
                reasonCode = "activation-preparation-review-required";
            }
            else
            {
                decision = ProductionActivationPreparationDecision.ApprovedForPreparation;
                reasonCode = "activation-approved-for-preparation";
            }

            IReadOnlyList<string> normalizedBlockerList = Normalize(blockers);
            IReadOnlyList<string> normalizedReviewList = Normalize(reviewItems);
            ProductionActivationGate? validationGate = suppliedGates.FirstOrDefault(gate =>
                gate.GateType == ProductionActivationGateType.ValidationCompletion);
            ProductionActivationGate? deploymentGate = suppliedGates.FirstOrDefault(gate =>
                gate.GateType == ProductionActivationGateType.DeploymentReadiness);
            var summary = new ProductionActivationValidationSummary(
                context!.PilotValidationStatus, context.DeploymentReadinessStatus,
                context.LegacyAuthorityState,
                validationGate?.EvidenceReference ?? "validation-evidence-missing",
                deploymentGate?.EvidenceReference ?? "deployment-evidence-missing");
            var package = new ProductionCutoverEvidencePackage(
                $"{context.PreparationId}:cutover-evidence", decision, suppliedGates,
                summary, rollback, backup, normalizedBlockerList, normalizedReviewList,
                context.TimestampUtc);
            return new(decision, reasonCode, normalizedBlockerList,
                normalizedReviewList, package);
        }
        catch
        {
            return Blocked("activation-preparation-evaluation-failed");
        }
    }

    private static string? ValidateContext(ProductionActivationPreparationContext? context)
    {
        if (context is null) return "activation-preparation-context-required";
        if (!context.ExplicitlyRequested)
            return "activation-preparation-explicit-request-required";
        if (!ProductionPreparationText.IsUsableIdentifier(context.PreparationId) ||
            !ProductionPreparationText.IsUsableIdentifier(context.ReleaseIdentifier) ||
            !ProductionPreparationText.IsUsableIdentifier(context.RollbackReference) ||
            context.ApprovalReferences.Any(reference =>
                !ProductionPreparationText.IsUsableIdentifier(reference)))
            return "activation-preparation-identifier-invalid";
        if (!Enum.IsDefined(context.TargetScope) ||
            context.LegacyAuthorityState != LegacyAuthorityState.LegacyAuthoritative ||
            !Enum.IsDefined(context.PilotValidationStatus) ||
            !Enum.IsDefined(context.DeploymentReadinessStatus) ||
            context.ApprovalReferences.Count != ApprovalGateTypes.Count ||
            context.TimestampUtc.Offset != TimeSpan.Zero)
            return "activation-preparation-context-invalid";
        return null;
    }

    private static void ValidateGates(
        ProductionActivationPreparationContext context,
        IReadOnlyCollection<ProductionActivationGate> gates,
        ICollection<string> blockers,
        ICollection<string> reviews)
    {
        if (gates.Any(gate => gate is null) || gates.Count != RequiredGates.Count ||
            gates.GroupBy(gate => gate.GateType).Any(group => group.Count() != 1) ||
            !gates.Select(gate => gate.GateType).All(RequiredGates.Contains))
        {
            blockers.Add("activation-gates-incomplete");
        }
        foreach (ProductionActivationGate gate in gates)
        {
            if (!Enum.IsDefined(gate.GateType) || !Enum.IsDefined(gate.Status) ||
                !ProductionPreparationText.IsUsableIdentifier(gate.EvidenceReference) ||
                !ProductionPreparationText.IsUsableIdentifier(gate.ReviewerReference) ||
                gate.ReviewedAtUtc.Offset != TimeSpan.Zero ||
                gate.ReviewedAtUtc > context.TimestampUtc)
            {
                blockers.Add("activation-gate-invalid");
                continue;
            }
            if (gate.Status is ProductionActivationGateStatus.Missing or
                ProductionActivationGateStatus.Failed)
                blockers.Add("activation-gate-not-satisfied");
            else if (gate.Status == ProductionActivationGateStatus.RequiresReview)
                reviews.Add("activation-gate-review-required");
        }

        string[] approvalEvidence = gates.Where(gate => ApprovalGateTypes.Contains(gate.GateType))
            .Select(gate => gate.EvidenceReference).Order(StringComparer.Ordinal).ToArray();
        if (!approvalEvidence.SequenceEqual(context.ApprovalReferences, StringComparer.Ordinal))
            blockers.Add("activation-approval-references-mismatch");
    }

    private static void ValidateBackup(
        ProductionActivationPreparationContext context,
        BackupVerificationResult? backup,
        ICollection<string> blockers,
        ICollection<string> reviews)
    {
        if (backup is null || !ProductionPreparationText.IsUsableIdentifier(
                backup.BackupReference) || !Enum.IsDefined(backup.VerificationStatus) ||
            !Enum.IsDefined(backup.RestoreTestStatus) ||
            backup.VerifiedAtUtc.Offset != TimeSpan.Zero ||
            backup.VerifiedAtUtc > context.TimestampUtc)
        {
            blockers.Add("backup-verification-invalid");
            return;
        }
        if (backup.VerificationStatus is BackupEvidenceStatus.Unavailable or
            BackupEvidenceStatus.Failed || backup.RestoreTestStatus is
            RestoreTestStatus.NotPerformed or RestoreTestStatus.Failed)
            blockers.Add("backup-or-restore-verification-failed");
        else if (backup.VerificationStatus == BackupEvidenceStatus.RequiresReview ||
            backup.RestoreTestStatus == RestoreTestStatus.RequiresReview)
            reviews.Add("backup-or-restore-review-required");
    }

    private static void ValidateRollback(
        ProductionActivationPreparationContext context,
        RollbackVerificationResult? rollback,
        ICollection<string> blockers,
        ICollection<string> reviews)
    {
        if (rollback is null || !StringComparer.Ordinal.Equals(context.RollbackReference,
                rollback.RollbackPlanReference) ||
            !ProductionPreparationText.IsUsableIdentifier(rollback.RollbackPlanReference) ||
            !ProductionPreparationText.IsUsableIdentifier(rollback.OwnerReference) ||
            !ProductionPreparationText.IsUsableIdentifier(rollback.EvidenceReference) ||
            !Enum.IsDefined(rollback.ValidationStatus))
        {
            blockers.Add("rollback-verification-invalid");
            return;
        }
        if (rollback.ValidationStatus is RollbackEvidenceStatus.Unavailable or
            RollbackEvidenceStatus.Failed)
            blockers.Add("rollback-verification-failed");
        else if (rollback.ValidationStatus == RollbackEvidenceStatus.RequiresReview)
            reviews.Add("rollback-review-required");
    }

    private static void ValidateStopConditions(
        IReadOnlyCollection<ProductionActivationStopCondition> conditions,
        ICollection<string> blockers)
    {
        if (conditions.Any(condition => condition is null) ||
            conditions.Count != RequiredStopConditions.Count ||
            conditions.GroupBy(condition => condition.ConditionType)
                .Any(group => group.Count() != 1) ||
            !conditions.Select(condition => condition.ConditionType)
                .All(RequiredStopConditions.Contains))
        {
            blockers.Add("activation-stop-conditions-incomplete");
            return;
        }
        foreach (ProductionActivationStopCondition condition in conditions)
        {
            if (!Enum.IsDefined(condition.ConditionType) ||
                !ProductionPreparationText.IsUsableIdentifier(condition.EvidenceReference))
                blockers.Add("activation-stop-condition-invalid");
            else if (condition.Triggered)
                blockers.Add($"activation-stop-{condition.ConditionType.ToString().ToLowerInvariant()}");
        }
    }

    private static void ValidateStatusBindings(
        ProductionActivationPreparationContext context,
        IEnumerable<ProductionActivationGate> gates,
        RollbackVerificationResult? rollback,
        ICollection<string> blockers,
        ICollection<string> reviews)
    {
        ProductionActivationGate? validation = gates.FirstOrDefault(gate =>
            gate.GateType == ProductionActivationGateType.ValidationCompletion);
        ProductionActivationGate? deployment = gates.FirstOrDefault(gate =>
            gate.GateType == ProductionActivationGateType.DeploymentReadiness);
        ProductionActivationGate? rollbackGate = gates.FirstOrDefault(gate =>
            gate.GateType == ProductionActivationGateType.RollbackReadiness);

        if (context.PilotValidationStatus is PilotValidationResultStatus.Failed or
            PilotValidationResultStatus.Blocked)
            blockers.Add("pilot-validation-incomplete");
        else if (context.PilotValidationStatus == PilotValidationResultStatus.DifferenceDetected)
            reviews.Add("pilot-validation-review-required");

        if (context.DeploymentReadinessStatus == PilotDeploymentReadinessStatus.Blocked)
            blockers.Add("pilot-deployment-readiness-blocked");
        else if (context.DeploymentReadinessStatus ==
            PilotDeploymentReadinessStatus.RequiresReview)
            reviews.Add("pilot-deployment-review-required");

        if (!GateStatusMatchesValidation(validation, context.PilotValidationStatus) ||
            !GateStatusMatchesDeployment(deployment, context.DeploymentReadinessStatus) ||
            !GateStatusMatchesRollback(rollbackGate, rollback))
            blockers.Add("activation-gate-evidence-mismatch");
        if (rollbackGate is not null && rollback is not null &&
            !StringComparer.Ordinal.Equals(rollbackGate.EvidenceReference,
                rollback.EvidenceReference))
            blockers.Add("activation-rollback-evidence-mismatch");
    }

    private static bool GateStatusMatchesValidation(
        ProductionActivationGate? gate,
        PilotValidationResultStatus status) => gate is not null && status switch
    {
        PilotValidationResultStatus.Completed =>
            gate.Status == ProductionActivationGateStatus.Satisfied,
        PilotValidationResultStatus.DifferenceDetected =>
            gate.Status == ProductionActivationGateStatus.RequiresReview,
        _ => gate.Status is ProductionActivationGateStatus.Failed or
            ProductionActivationGateStatus.Missing
    };

    private static bool GateStatusMatchesDeployment(
        ProductionActivationGate? gate,
        PilotDeploymentReadinessStatus status) => gate is not null && status switch
    {
        PilotDeploymentReadinessStatus.Ready =>
            gate.Status == ProductionActivationGateStatus.Satisfied,
        PilotDeploymentReadinessStatus.RequiresReview =>
            gate.Status == ProductionActivationGateStatus.RequiresReview,
        _ => gate.Status is ProductionActivationGateStatus.Failed or
            ProductionActivationGateStatus.Missing
    };

    private static bool GateStatusMatchesRollback(
        ProductionActivationGate? gate,
        RollbackVerificationResult? rollback) => gate is not null && rollback is not null &&
        rollback.ValidationStatus switch
        {
            RollbackEvidenceStatus.Verified =>
                gate.Status == ProductionActivationGateStatus.Satisfied,
            RollbackEvidenceStatus.RequiresReview =>
                gate.Status == ProductionActivationGateStatus.RequiresReview,
            _ => gate.Status is ProductionActivationGateStatus.Failed or
                ProductionActivationGateStatus.Missing
        };

    private static ProductionActivationReadinessResult Blocked(string reasonCode) => new(
        ProductionActivationPreparationDecision.Blocked, reasonCode, [reasonCode], [], null);

    private static IReadOnlyList<string> Normalize(IEnumerable<string> values) =>
        new ReadOnlyCollection<string>(values.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray());
}
