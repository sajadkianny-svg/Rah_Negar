using System.Collections.ObjectModel;
using Rah_Negar.Foundation.Application.Activation;

namespace Rah_Negar.Foundation.Application.Integration;

public enum IntegrationBoundaryArea
{
    Authentication,
    ReportingSnapshots,
    RuntimeEventProjection,
    SecurityPersistence,
    MigrationReadiness,
    ProtectedSettings,
    ReportExport,
    ActivationControls
}

public sealed record IntegrationBoundaryInventoryItem(
    IntegrationBoundaryArea Area,
    string CurrentOwner,
    string LegacyOwner,
    string FutureOwner,
    string IntegrationPoint,
    string ActivationDependency,
    IReadOnlyList<string> RequiredPreviousPhases);

public sealed class IntegrationBoundaryInventory
{
    public IntegrationBoundaryInventory(IEnumerable<IntegrationBoundaryInventoryItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        Items = new ReadOnlyCollection<IntegrationBoundaryInventoryItem>(items.Select(item => item with
        {
            RequiredPreviousPhases = new ReadOnlyCollection<string>(item.RequiredPreviousPhases.ToArray())
        }).ToArray());
    }

    public IReadOnlyList<IntegrationBoundaryInventoryItem> Items { get; }

    public static IntegrationBoundaryInventory CreateCurrent() => new(
        [
            Item(IntegrationBoundaryArea.Authentication,
                "Phase 7 ShiftProfile and credential contracts",
                "FrmLogin, AppSession, and AppSettingsService legacy login",
                "ShiftProfile authentication adapter",
                "Legacy login observation followed by target comparison",
                "Migration-ready credential persistence and scoped approval", "7.5", "7.7", "8.0"),
            Item(IntegrationBoundaryArea.ReportingSnapshots,
                "Phase 5 immutable snapshot/read contracts",
                "FrmReportCenter and Services.Reports legacy report pipeline",
                "IFinalizedReportReader and snapshot-backed reporting",
                "Read-side shadow comparison before any route change",
                "Snapshot and export validation plus reporting approval", "5", "7.8", "8.0"),
            Item(IntegrationBoundaryArea.RuntimeEventProjection,
                "Phase 4 Runtime shadow and Event comparison contracts",
                "Services.Reports Runtime/Event calculations and legacy event tables",
                "Runtime projection engine and normalized Event chain",
                "Read-only copied input and generalized comparison evidence",
                "Stable comparison evidence and pilot approval", "4", "7.9", "8.0"),
            Item(IntegrationBoundaryArea.SecurityPersistence,
                "Phase 7.7 inactive SQLite security adapters",
                "Legacy app_settings/session behavior",
                "ShiftProfile, ManagementCredential, public-key, audit, and replay persistence",
                "Future authenticated composition after migration",
                "Unified migration, provisioning, recovery, and approval", "7.5", "7.7", "7.9", "8.0"),
            Item(IntegrationBoundaryArea.MigrationReadiness,
                "Phase 7.9 explicit preflight/backup/rehearsal services",
                "No production startup migration authority",
                "Future IProductionMigrationExecutor implementation",
                "Explicitly selected installation under maintenance authorization",
                "Verified evidence package, approval, and migration authorization", "7.8", "7.9", "8.0"),
            Item(IntegrationBoundaryArea.ProtectedSettings,
                "Phase 7 protected-settings and exactly-once ESD contracts",
                "FrmSettings and app_settings remain authoritative",
                "Management proof plus external vendor authorization path",
                "Legacy-authoritative shadow/pilot presentation adapter",
                "Security persistence, approval, and separate ESD cutover", "7.5", "7.6", "7.7", "8.0"),
            Item(IntegrationBoundaryArea.ReportExport,
                "Phase 5 snapshot export contracts/renderers",
                "Legacy report/PDF/Excel services",
                "IReportExporter over integrity-validated snapshots",
                "Artifact comparison before selected pilot reads",
                "Snapshot validation, export validation, and approval", "5", "7.9", "8.0"),
            Item(IntegrationBoundaryArea.ActivationControls,
                "Phase 8.0 state/evidence/approval/guard contracts",
                "No legacy automatic activation owner",
                "Future controlled feature and migration executors",
                "Feature-specific guard and explicit routing decision",
                "Complete evidence, approval, checklist, and rollback readiness", "7.9", "8.0")
        ]);

    private static IntegrationBoundaryInventoryItem Item(IntegrationBoundaryArea area,
        string current, string legacy, string future, string point, string dependency,
        params string[] phases) => new(area, current, legacy, future, point, dependency,
            Array.AsReadOnly(phases));
}

public enum IntegrationAuthorityMode
{
    LegacyOnly,
    ShadowValidation,
    PilotTarget,
    FullTarget
}

public enum ControlledIntegrationFeature
{
    Authentication,
    SnapshotReporting,
    RuntimeProjection,
    EventProjection,
    ProtectedSettings,
    ReportExport,
    MigrationTooling
}

public enum IntegrationControlDecision
{
    Allowed,
    Blocked,
    RequiresManualReview
}

public sealed record FeatureIntegrationActivationDecision(
    IntegrationControlDecision Decision,
    ControlledIntegrationFeature Feature,
    string TargetScope,
    string EvidencePackageId,
    string CorrelationId,
    string? ApprovalId,
    IReadOnlyList<string> Reasons);

public sealed record PilotBoundaryValidationResult(
    bool IsValid,
    string PilotId,
    bool RollbackToLegacyAvailable,
    IReadOnlyList<string> Issues);

public sealed record IntegrationSafetyContext(
    ControlledIntegrationFeature Feature,
    IntegrationAuthorityMode RequestedMode,
    string EvidencePackageId,
    string CorrelationId,
    bool MigrationReadinessPassed,
    bool SnapshotValidationPassed,
    bool EsdCutoverRequested,
    FeatureIntegrationActivationDecision? FeatureDecision,
    PilotBoundaryValidationResult? PilotBoundary);

public sealed record IntegrationSafetyValidationResult(
    IntegrationControlDecision Decision,
    IReadOnlyList<string> Reasons);

public static class IntegrationSafetyValidator
{
    public static IntegrationSafetyValidationResult Validate(IntegrationSafetyContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var reasons = new List<string>();
        if (!Enum.IsDefined(context.RequestedMode)) reasons.Add("unknown-integration-mode");
        if (!Enum.IsDefined(context.Feature)) reasons.Add("unknown-integration-feature");
        if (string.IsNullOrWhiteSpace(context.EvidencePackageId)) reasons.Add("integration-evidence-required");
        if (string.IsNullOrWhiteSpace(context.CorrelationId)) reasons.Add("integration-correlation-required");
        if (context.EsdCutoverRequested) reasons.Add("esd-cutover-prohibited-during-feature-integration");

        bool targetMode = context.RequestedMode is IntegrationAuthorityMode.PilotTarget or
            IntegrationAuthorityMode.FullTarget;
        if (targetMode && context.FeatureDecision?.Decision != IntegrationControlDecision.Allowed)
            reasons.Add("target-routing-requires-approved-feature-decision");
        if (targetMode && string.IsNullOrWhiteSpace(context.FeatureDecision?.ApprovalId))
            reasons.Add("target-routing-requires-explicit-approval-id");
        if (targetMode && (context.FeatureDecision is null ||
            context.FeatureDecision.Feature != context.Feature ||
            !StringComparer.Ordinal.Equals(context.FeatureDecision.EvidencePackageId, context.EvidencePackageId) ||
            !StringComparer.Ordinal.Equals(context.FeatureDecision.CorrelationId, context.CorrelationId)))
            reasons.Add("feature-decision-binding-mismatch");
        if (context.RequestedMode == IntegrationAuthorityMode.PilotTarget &&
            context.PilotBoundary is not { IsValid: true, RollbackToLegacyAvailable: true })
            reasons.Add("pilot-boundary-or-rollback-not-ready");
        if (context.Feature == ControlledIntegrationFeature.Authentication && targetMode &&
            !context.MigrationReadinessPassed)
            reasons.Add("authentication-target-requires-migration-readiness");
        if (context.Feature is ControlledIntegrationFeature.SnapshotReporting or
                ControlledIntegrationFeature.ReportExport && targetMode &&
            !context.SnapshotValidationPassed)
            reasons.Add("reporting-target-requires-snapshot-validation");

        if (reasons.Count > 0)
            return new(IntegrationControlDecision.Blocked, reasons.AsReadOnly());
        return new(IntegrationControlDecision.Allowed, Array.Empty<string>());
    }
}

public sealed record IntegrationRoutingRequest(
    ControlledIntegrationFeature Feature,
    IntegrationAuthorityMode RequestedMode,
    string TargetScope,
    string EvidencePackageId,
    string CorrelationId,
    IntegrationSafetyContext SafetyContext);

public sealed record IntegrationAuthorityRoutingDecision(
    IntegrationControlDecision Decision,
    IntegrationAuthorityMode RequestedMode,
    IntegrationAuthorityMode? EffectiveMode,
    string TargetScope,
    string EvidencePackageId,
    string CorrelationId,
    bool LegacyRemainsAuthoritative,
    bool TargetReadOnly,
    bool ProductionMutationAllowed,
    IReadOnlyList<string> Reasons);

public static class IntegrationAuthorityRoutingPolicy
{
    public static IntegrationAuthorityRoutingDecision Route(IntegrationRoutingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var bindingIssues = new List<string>();
        if (string.IsNullOrWhiteSpace(request.TargetScope)) bindingIssues.Add("target-scope-required");
        if (request.Feature != request.SafetyContext.Feature ||
            request.RequestedMode != request.SafetyContext.RequestedMode ||
            !StringComparer.Ordinal.Equals(request.EvidencePackageId, request.SafetyContext.EvidencePackageId) ||
            !StringComparer.Ordinal.Equals(request.CorrelationId, request.SafetyContext.CorrelationId))
            bindingIssues.Add("routing-safety-binding-mismatch");
        IntegrationSafetyValidationResult safety = IntegrationSafetyValidator.Validate(request.SafetyContext);
        bindingIssues.AddRange(safety.Reasons);
        if (bindingIssues.Count > 0)
            return new(IntegrationControlDecision.Blocked, request.RequestedMode, null,
                request.TargetScope, request.EvidencePackageId, request.CorrelationId,
                true, true, false, bindingIssues.AsReadOnly());

        return request.RequestedMode switch
        {
            IntegrationAuthorityMode.LegacyOnly => Allowed(true, false),
            IntegrationAuthorityMode.ShadowValidation => Allowed(true, true),
            IntegrationAuthorityMode.PilotTarget => Allowed(true, true),
            IntegrationAuthorityMode.FullTarget => Allowed(false, false),
            _ => new(IntegrationControlDecision.Blocked, request.RequestedMode, null,
                request.TargetScope, request.EvidencePackageId, request.CorrelationId,
                true, true, false, ["unknown-integration-mode"])
        };

        IntegrationAuthorityRoutingDecision Allowed(bool legacyAuthority, bool targetReadOnly) =>
            new(IntegrationControlDecision.Allowed, request.RequestedMode, request.RequestedMode,
                request.TargetScope, request.EvidencePackageId, request.CorrelationId,
                legacyAuthority, targetReadOnly, false, Array.Empty<string>());
    }
}
