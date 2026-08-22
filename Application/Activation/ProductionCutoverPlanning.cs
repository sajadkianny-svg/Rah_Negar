using System.Collections.ObjectModel;

namespace Rah_Negar.Foundation.Application.Activation;

public enum RollbackDecisionBoundary
{
    NotEstablished,
    ManualDecisionRequired,
    ExplicitRestoreAuthorizationRequired
}

public sealed record RollbackReadinessEvidence(
    bool BackupAvailable,
    bool BackupVerified,
    bool RestoreValidationPassed,
    string? RollbackOwnerActorReference,
    RollbackDecisionBoundary DecisionBoundary);

public enum RollbackReadinessStatus
{
    Ready,
    Blocked
}

public sealed record RollbackReadinessResult(
    RollbackReadinessStatus Status,
    IReadOnlyList<string> Blockers);

public static class RollbackReadinessEvaluator
{
    public static RollbackReadinessResult Evaluate(RollbackReadinessEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var blockers = new List<string>();
        if (!evidence.BackupAvailable) blockers.Add("rollback-backup-unavailable");
        if (!evidence.BackupVerified) blockers.Add("rollback-backup-not-verified");
        if (!evidence.RestoreValidationPassed) blockers.Add("restore-validation-not-passed");
        if (string.IsNullOrWhiteSpace(evidence.RollbackOwnerActorReference))
            blockers.Add("rollback-owner-not-assigned");
        if (evidence.DecisionBoundary == RollbackDecisionBoundary.NotEstablished)
            blockers.Add("rollback-decision-boundary-not-established");
        return new(blockers.Count == 0 ? RollbackReadinessStatus.Ready : RollbackReadinessStatus.Blocked,
            blockers.AsReadOnly());
    }
}

public enum ProductionCutoverChecklistItem
{
    BuildVerified,
    TestsPassed,
    MigrationRehearsalPassed,
    BackupVerified,
    RestoreValidated,
    DiskCapacityChecked,
    LockPolicyChecked,
    MaintenanceWindowApproved,
    OperatorAssigned,
    RollbackOwnerAssigned,
    SupportContactAvailable,
    MonitoringPlanAvailable,
    ShiftProfileModelConfirmed,
    ManagementCredentialModelConfirmed,
    VendorAuthorizationBoundaryConfirmed,
    NoSupportIdentity,
    NoRbacIntroduced
}

public enum ProductionCutoverChecklistCategory
{
    Technical,
    Operational,
    Security
}

public enum ChecklistItemStatus
{
    Pending,
    Confirmed,
    Blocked
}

public sealed record ProductionCutoverChecklistEntry(
    ProductionCutoverChecklistItem Item,
    ProductionCutoverChecklistCategory Category,
    ChecklistItemStatus Status,
    string? EvidenceReference);

public sealed class ProductionCutoverChecklist
{
    public ProductionCutoverChecklist(IEnumerable<ProductionCutoverChecklistEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Entries = new ReadOnlyCollection<ProductionCutoverChecklistEntry>(entries.ToArray());
    }

    public IReadOnlyList<ProductionCutoverChecklistEntry> Entries { get; }
}

public sealed record ProductionCutoverChecklistResult(
    bool IsComplete,
    bool AllConfirmed,
    IReadOnlyList<ProductionCutoverChecklistItem> MissingItems,
    IReadOnlyList<ProductionCutoverChecklistItem> UnconfirmedItems,
    IReadOnlyList<string> Issues);

public static class ProductionCutoverChecklistEvaluator
{
    private static readonly IReadOnlyDictionary<ProductionCutoverChecklistItem,
        ProductionCutoverChecklistCategory> Required =
        new ReadOnlyDictionary<ProductionCutoverChecklistItem, ProductionCutoverChecklistCategory>(
            new Dictionary<ProductionCutoverChecklistItem, ProductionCutoverChecklistCategory>
            {
                [ProductionCutoverChecklistItem.BuildVerified] = ProductionCutoverChecklistCategory.Technical,
                [ProductionCutoverChecklistItem.TestsPassed] = ProductionCutoverChecklistCategory.Technical,
                [ProductionCutoverChecklistItem.MigrationRehearsalPassed] = ProductionCutoverChecklistCategory.Technical,
                [ProductionCutoverChecklistItem.BackupVerified] = ProductionCutoverChecklistCategory.Technical,
                [ProductionCutoverChecklistItem.RestoreValidated] = ProductionCutoverChecklistCategory.Technical,
                [ProductionCutoverChecklistItem.DiskCapacityChecked] = ProductionCutoverChecklistCategory.Technical,
                [ProductionCutoverChecklistItem.LockPolicyChecked] = ProductionCutoverChecklistCategory.Technical,
                [ProductionCutoverChecklistItem.MaintenanceWindowApproved] = ProductionCutoverChecklistCategory.Operational,
                [ProductionCutoverChecklistItem.OperatorAssigned] = ProductionCutoverChecklistCategory.Operational,
                [ProductionCutoverChecklistItem.RollbackOwnerAssigned] = ProductionCutoverChecklistCategory.Operational,
                [ProductionCutoverChecklistItem.SupportContactAvailable] = ProductionCutoverChecklistCategory.Operational,
                [ProductionCutoverChecklistItem.MonitoringPlanAvailable] = ProductionCutoverChecklistCategory.Operational,
                [ProductionCutoverChecklistItem.ShiftProfileModelConfirmed] = ProductionCutoverChecklistCategory.Security,
                [ProductionCutoverChecklistItem.ManagementCredentialModelConfirmed] = ProductionCutoverChecklistCategory.Security,
                [ProductionCutoverChecklistItem.VendorAuthorizationBoundaryConfirmed] = ProductionCutoverChecklistCategory.Security,
                [ProductionCutoverChecklistItem.NoSupportIdentity] = ProductionCutoverChecklistCategory.Security,
                [ProductionCutoverChecklistItem.NoRbacIntroduced] = ProductionCutoverChecklistCategory.Security
            });

    public static ProductionCutoverChecklistResult Evaluate(ProductionCutoverChecklist checklist)
    {
        ArgumentNullException.ThrowIfNull(checklist);
        var issues = new List<string>();
        ProductionCutoverChecklistItem[] duplicates = checklist.Entries.GroupBy(x => x.Item)
            .Where(x => x.Count() > 1).Select(x => x.Key).ToArray();
        if (duplicates.Length > 0) issues.Add("duplicate-checklist-items");
        ProductionCutoverChecklistItem[] missing = Required.Keys.Except(checklist.Entries.Select(x => x.Item))
            .Order().ToArray();
        foreach (ProductionCutoverChecklistEntry entry in checklist.Entries)
            if (!Required.TryGetValue(entry.Item, out ProductionCutoverChecklistCategory expected) ||
                entry.Category != expected)
                issues.Add($"wrong-category:{entry.Item}");
        ProductionCutoverChecklistItem[] unconfirmed = checklist.Entries
            .Where(x => x.Status != ChecklistItemStatus.Confirmed || string.IsNullOrWhiteSpace(x.EvidenceReference))
            .Select(x => x.Item).Distinct().Order().ToArray();
        bool complete = missing.Length == 0 && duplicates.Length == 0 && issues.Count == 0;
        return new(complete, complete && unconfirmed.Length == 0,
            Array.AsReadOnly(missing), Array.AsReadOnly(unconfirmed), issues.AsReadOnly());
    }

    public static ProductionCutoverChecklist CreateDisabledPlanningChecklist() => new(
        Required.Select(x => new ProductionCutoverChecklistEntry(
            x.Key, x.Value, ChecklistItemStatus.Pending, null)));
}

public enum ControlledProductionFeature
{
    NewAuthenticationWorkflow,
    SnapshotReportingWorkflow,
    ProtectedSettingsWorkflow,
    MigrationTooling
}

public enum FeatureActivationState
{
    Disabled,
    PlannedForFutureApproval,
    Enabled
}

public sealed record FeatureActivationBoundaryEntry(
    ControlledProductionFeature Feature,
    FeatureActivationState State,
    string ResultCategory);

public sealed class FeatureActivationBoundarySnapshot
{
    public FeatureActivationBoundarySnapshot(IEnumerable<FeatureActivationBoundaryEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Entries = new ReadOnlyCollection<FeatureActivationBoundaryEntry>(entries.ToArray());
    }

    public IReadOnlyList<FeatureActivationBoundaryEntry> Entries { get; }

    public static FeatureActivationBoundarySnapshot Inactive { get; } = new(
        Enum.GetValues<ControlledProductionFeature>().Select(feature =>
            new FeatureActivationBoundaryEntry(feature, FeatureActivationState.Disabled,
                "Phase8PlanningOnly")));
}

public sealed record FutureFeatureActivationRequest(
    ControlledProductionFeature Feature,
    string EvidencePackageId,
    string ApprovalId,
    string CorrelationId);

/// <summary>Future boundary only. Phase 8.0 intentionally provides no implementation.</summary>
public interface IFutureFeatureActivationExecutor
{
    Task<bool> ActivateAsync(FutureFeatureActivationRequest request,
        CancellationToken cancellationToken = default);
}

public enum ProductionReadinessDimension
{
    Authentication,
    Reporting,
    Snapshots,
    EsdSettings,
    SecurityPersistence,
    MigrationState
}

public enum AuthorityReadinessState
{
    CurrentLegacyAuthority,
    InactiveTargetFoundation,
    FutureActivationRequired
}

public sealed record ProductionAuthorityComparisonItem(
    ProductionReadinessDimension Dimension,
    string CurrentLegacyAuthority,
    string FutureTargetAuthority,
    AuthorityReadinessState TargetState,
    string RemainingGap);

public sealed class ProductionReadinessComparison
{
    public ProductionReadinessComparison(IEnumerable<ProductionAuthorityComparisonItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        Items = new ReadOnlyCollection<ProductionAuthorityComparisonItem>(items.ToArray());
    }

    public IReadOnlyList<ProductionAuthorityComparisonItem> Items { get; }

    public static ProductionReadinessComparison CreateCurrent() => new(
        [
            new(ProductionReadinessDimension.Authentication,
                "Existing production composition and legacy login behavior",
                "ShiftProfile authentication backed by internal credential records",
                AuthorityReadinessState.InactiveTargetFoundation,
                "Approved UI/composition, credential provisioning, recovery, and activation are still required."),
            new(ProductionReadinessDimension.Reporting,
                "Legacy live reporting and ordinary ShiftProfile Finalize workflow",
                "Snapshot-backed finalized reporting while retaining ordinary ShiftProfile Finalize",
                AuthorityReadinessState.InactiveTargetFoundation,
                "Production read routing and UI adoption remain disabled."),
            new(ProductionReadinessDimension.Snapshots,
                "Legacy finalized-month protection and report evidence",
                "Immutable ReportSnapshots and ReportPeriodLocks target schema",
                AuthorityReadinessState.InactiveTargetFoundation,
                "Installation preservation proof and controlled read cutover are required."),
            new(ProductionReadinessDimension.EsdSettings,
                "app_settings.esd_extra_runtime_hours is authoritative",
                "SecurityDeploymentSettings with protected exactly-once adjustment",
                AuthorityReadinessState.FutureActivationRequired,
                "Conflict resolution, approved provisioning, and explicit authority cutover are required."),
            new(ProductionReadinessDimension.SecurityPersistence,
                "Legacy production behavior without target security composition",
                "ShiftProfile credentials, singleton ManagementCredential, public keys, audit, and replay receipts",
                AuthorityReadinessState.InactiveTargetFoundation,
                "Provisioning, operational procedures, and production composition remain absent."),
            new(ProductionReadinessDimension.MigrationState,
                "No unified migration registered at production startup",
                "Explicit checksummed unified target chain through version 4",
                AuthorityReadinessState.FutureActivationRequired,
                "Installation assessment, approval, maintenance execution, and validation remain future work.")
        ]);
}
