using System.Collections.ObjectModel;
using Rah_Negar.Foundation.Application.Activation;
using Rah_Negar.Foundation.Application.Integration;

namespace Rah_Negar.Foundation.Application.Pilot;

public enum PilotFeature
{
    AuthenticationPilot,
    ReportingPilot,
    RuntimeEventPilot,
    ProtectedSettingsPilot,
    ExportPilot
}

public enum PilotFeatureRiskLevel
{
    Low,
    Moderate,
    High,
    Critical
}

public sealed class PilotExecutionContext
{
    public PilotExecutionContext(
        string pilotId,
        string stationId,
        IEnumerable<string> selectedShiftProfileIds,
        IEnumerable<PilotFeature> enabledPilotFeatures,
        string evidencePackageId,
        string correlationId,
        string rollbackReference,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(selectedShiftProfileIds);
        ArgumentNullException.ThrowIfNull(enabledPilotFeatures);
        PilotId = pilotId;
        StationId = stationId;
        SelectedShiftProfileIds = new ReadOnlyCollection<string>(selectedShiftProfileIds
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        EnabledPilotFeatures = new ReadOnlyCollection<PilotFeature>(enabledPilotFeatures
            .Distinct().Order().ToArray());
        EvidencePackageId = evidencePackageId;
        CorrelationId = correlationId;
        RollbackReference = rollbackReference;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public string PilotId { get; }
    public string StationId { get; }
    public IReadOnlyList<string> SelectedShiftProfileIds { get; }
    public IReadOnlyList<PilotFeature> EnabledPilotFeatures { get; }
    public string EvidencePackageId { get; }
    public string CorrelationId { get; }
    public string RollbackReference { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset? ExpiresAtUtc { get; }
    public bool EnabledByDefault => false;
    public bool ProductionRegistrationAllowed => false;
    public bool ProductionMutationAllowed => false;
}

public sealed record PilotContextValidationResult(bool IsValid, IReadOnlyList<string> Issues);

public static class PilotExecutionContextValidator
{
    public static PilotContextValidationResult Validate(PilotExecutionContext? context, DateTimeOffset nowUtc)
    {
        var issues = new List<string>();
        if (context is null) return new(false, ["pilot-context-required"]);
        if (nowUtc.Offset != TimeSpan.Zero) issues.Add("current-time-must-be-utc");
        AddExplicit(context.PilotId, "pilot-id-required", "pilot-id-wildcard-prohibited");
        AddExplicit(context.StationId, "pilot-station-required", "pilot-station-wildcard-prohibited");
        if (context.SelectedShiftProfileIds.Count == 0 ||
            context.SelectedShiftProfileIds.Any(string.IsNullOrWhiteSpace))
            issues.Add("selected-shift-profile-required");
        if (context.SelectedShiftProfileIds.Any(IsWildcard))
            issues.Add("shift-profile-wildcard-prohibited");
        if (context.EnabledPilotFeatures.Count == 0)
            issues.Add("pilot-feature-scope-required");
        if (context.EnabledPilotFeatures.Any(feature => !Enum.IsDefined(feature)))
            issues.Add("unknown-pilot-feature");
        AddRequired(context.EvidencePackageId, "pilot-evidence-package-required");
        AddRequired(context.CorrelationId, "pilot-correlation-required");
        AddRequired(context.RollbackReference, "pilot-rollback-reference-required");
        if (context.CreatedAtUtc.Offset != TimeSpan.Zero) issues.Add("pilot-created-time-must-be-utc");
        if (context.CreatedAtUtc > nowUtc) issues.Add("pilot-not-yet-valid");
        if (context.ExpiresAtUtc is { } expiry)
        {
            if (expiry.Offset != TimeSpan.Zero || expiry <= context.CreatedAtUtc)
                issues.Add("pilot-expiration-invalid");
            else if (nowUtc >= expiry) issues.Add("pilot-expired");
        }
        return new(issues.Count == 0, issues.AsReadOnly());

        void AddExplicit(string? value, string missing, string wildcard)
        {
            AddRequired(value, missing);
            if (IsWildcard(value)) issues.Add(wildcard);
        }
        void AddRequired(string? value, string issue)
        {
            if (string.IsNullOrWhiteSpace(value)) issues.Add(issue);
        }
    }

    private static bool IsWildcard(string? value) =>
        StringComparer.OrdinalIgnoreCase.Equals(value?.Trim(), "all") || value?.Trim() == "*";
}

public sealed record PilotFeatureDefinition(
    PilotFeature Feature,
    string FeatureId,
    ControlledIntegrationFeature IntegrationFeature,
    ProductionActivationScope RequiredApprovalScope,
    IReadOnlyList<string> RequiredDependencies,
    IReadOnlyList<string> RequiredApprovals,
    PilotFeatureRiskLevel RiskLevel,
    bool RollbackRequired,
    bool EnabledByDefault);

public interface IPilotFeatureRegistry
{
    IReadOnlyList<PilotFeatureDefinition> Features { get; }
    bool TryGet(PilotFeature feature, out PilotFeatureDefinition? definition);
}

public sealed class PilotFeatureRegistry : IPilotFeatureRegistry
{
    private readonly IReadOnlyDictionary<PilotFeature, PilotFeatureDefinition> _byFeature;

    public PilotFeatureRegistry()
    {
        PilotFeatureDefinition[] definitions =
        [
            Create(PilotFeature.AuthenticationPilot, "authentication-pilot",
                ControlledIntegrationFeature.Authentication,
                ProductionActivationScope.AuthenticationWorkflowActivation,
                PilotFeatureRiskLevel.High,
                ["phase7-security-persistence", "phase7-migration-readiness", "legacy-login-observation"],
                ["feature-specific-approval"]),
            Create(PilotFeature.ReportingPilot, "reporting-pilot",
                ControlledIntegrationFeature.SnapshotReporting,
                ProductionActivationScope.SnapshotReportingActivation,
                PilotFeatureRiskLevel.Moderate,
                ["immutable-snapshot-validation", "legacy-report-readability"],
                ["feature-specific-approval"]),
            Create(PilotFeature.RuntimeEventPilot, "runtime-event-pilot",
                ControlledIntegrationFeature.RuntimeProjection,
                ProductionActivationScope.RuntimeEventProjectionActivation,
                PilotFeatureRiskLevel.Moderate,
                ["runtime-event-projection-validation", "read-only-target-adapter"],
                ["feature-specific-approval"]),
            Create(PilotFeature.ProtectedSettingsPilot, "protected-settings-pilot",
                ControlledIntegrationFeature.ProtectedSettings,
                ProductionActivationScope.ProtectedSettingsActivation,
                PilotFeatureRiskLevel.Critical,
                ["legacy-settings-authority", "esd-cutover-prohibition"],
                ["feature-specific-approval", "separate-future-esd-authorization"]),
            Create(PilotFeature.ExportPilot, "export-pilot",
                ControlledIntegrationFeature.ReportExport,
                ProductionActivationScope.ReportExportActivation,
                PilotFeatureRiskLevel.Moderate,
                ["immutable-snapshot-validation", "export-artifact-validation"],
                ["feature-specific-approval"])
        ];
        Features = new ReadOnlyCollection<PilotFeatureDefinition>(definitions);
        _byFeature = definitions.ToDictionary(item => item.Feature);
    }

    public IReadOnlyList<PilotFeatureDefinition> Features { get; }

    public bool TryGet(PilotFeature feature, out PilotFeatureDefinition? definition) =>
        _byFeature.TryGetValue(feature, out definition);

    private static PilotFeatureDefinition Create(
        PilotFeature feature,
        string id,
        ControlledIntegrationFeature integrationFeature,
        ProductionActivationScope scope,
        PilotFeatureRiskLevel risk,
        string[] dependencies,
        string[] approvals) => new(feature, id, integrationFeature, scope,
            Array.AsReadOnly(dependencies), Array.AsReadOnly(approvals), risk, true, false);
}

public enum PilotRollbackStatus
{
    Available,
    Unavailable,
    Requested,
    ReturnedToLegacy,
    Closed
}

public enum PilotEvidenceState
{
    Complete,
    Incomplete,
    Blocked
}

public sealed class PilotEvidenceRecord
{
    public PilotEvidenceRecord(
        string evidenceId,
        string pilotId,
        PilotFeature feature,
        DateTimeOffset timestampUtc,
        string correlationId,
        string legacyResultFingerprint,
        string targetResultFingerprint,
        ShadowDifferenceSeverity comparisonSeverity,
        string operatorVisibleSafeMessage,
        PilotRollbackStatus rollbackStatus)
    {
        EvidenceId = evidenceId;
        PilotId = pilotId;
        Feature = feature;
        TimestampUtc = timestampUtc;
        CorrelationId = correlationId;
        LegacyResultFingerprint = legacyResultFingerprint;
        TargetResultFingerprint = targetResultFingerprint;
        ComparisonSeverity = comparisonSeverity;
        OperatorVisibleSafeMessage = operatorVisibleSafeMessage;
        RollbackStatus = rollbackStatus;
    }

    public string EvidenceId { get; }
    public string PilotId { get; }
    public PilotFeature Feature { get; }
    public DateTimeOffset TimestampUtc { get; }
    public string CorrelationId { get; }
    public string LegacyResultFingerprint { get; }
    public string TargetResultFingerprint { get; }
    public ShadowDifferenceSeverity ComparisonSeverity { get; }
    public string OperatorVisibleSafeMessage { get; }
    public PilotRollbackStatus RollbackStatus { get; }
    public bool ContainsCredentialMaterial => false;
}

public static class PilotEvidenceValidator
{
    public static PilotEvidenceState Validate(PilotEvidenceRecord? evidence, PilotExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (evidence is null || string.IsNullOrWhiteSpace(evidence.EvidenceId) ||
            string.IsNullOrWhiteSpace(evidence.LegacyResultFingerprint) ||
            string.IsNullOrWhiteSpace(evidence.TargetResultFingerprint) ||
            string.IsNullOrWhiteSpace(evidence.OperatorVisibleSafeMessage) ||
            evidence.TimestampUtc.Offset != TimeSpan.Zero || !Enum.IsDefined(evidence.Feature) ||
            !Enum.IsDefined(evidence.ComparisonSeverity) || !Enum.IsDefined(evidence.RollbackStatus))
            return PilotEvidenceState.Incomplete;
        if (!StringComparer.Ordinal.Equals(evidence.PilotId, context.PilotId) ||
            !StringComparer.Ordinal.Equals(evidence.CorrelationId, context.CorrelationId) ||
            !context.EnabledPilotFeatures.Contains(evidence.Feature))
            return PilotEvidenceState.Blocked;
        return PilotEvidenceState.Complete;
    }
}
