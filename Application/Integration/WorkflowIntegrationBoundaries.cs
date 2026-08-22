using Rah_Negar.Foundation.Application.Security;

namespace Rah_Negar.Foundation.Application.Integration;

public enum AuthenticationIntegrationMode
{
    LegacyLogin,
    ShiftProfileShadow,
    ShiftProfilePilot,
    ShiftProfileAuthority
}

public sealed record LegacyLoginObservation(
    bool Succeeded,
    string? LegacySessionReference,
    string ResultCategory);

public sealed record TargetShiftProfileAuthenticationObservation(
    bool Succeeded,
    string? ShiftProfileId,
    string? StationId,
    int? CredentialVersion,
    string ResultCategory);

public sealed record AuthenticationIntegrationRequest(
    AuthenticationIntegrationMode Mode,
    LegacyLoginObservation Legacy,
    TargetShiftProfileAuthenticationObservation Target,
    IntegrationAuthorityRoutingDecision Routing,
    bool MigrationReadinessPassed);

public sealed record AuthenticationIntegrationDecision(
    IntegrationControlDecision Decision,
    bool LegacyLoginAuthoritative,
    string? TargetShiftProfileId,
    string ResultCategory);

public static class AuthenticationIntegrationPolicy
{
    public static AuthenticationIntegrationDecision Evaluate(AuthenticationIntegrationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(request.Mode)) return Blocked("UnknownAuthenticationMode");
        IntegrationAuthorityMode expected = request.Mode switch
        {
            AuthenticationIntegrationMode.LegacyLogin => IntegrationAuthorityMode.LegacyOnly,
            AuthenticationIntegrationMode.ShiftProfileShadow => IntegrationAuthorityMode.ShadowValidation,
            AuthenticationIntegrationMode.ShiftProfilePilot => IntegrationAuthorityMode.PilotTarget,
            AuthenticationIntegrationMode.ShiftProfileAuthority => IntegrationAuthorityMode.FullTarget,
            _ => throw new ArgumentOutOfRangeException()
        };
        if (request.Routing.Decision != IntegrationControlDecision.Allowed ||
            request.Routing.EffectiveMode != expected ||
            request.Routing.ProductionMutationAllowed)
            return Blocked("AuthenticationRoutingNotApproved");
        if (request.Mode is AuthenticationIntegrationMode.ShiftProfilePilot or
                AuthenticationIntegrationMode.ShiftProfileAuthority && !request.MigrationReadinessPassed)
            return Blocked("AuthenticationMigrationReadinessRequired");
        if (request.Mode != AuthenticationIntegrationMode.LegacyLogin &&
            (!request.Target.Succeeded || string.IsNullOrWhiteSpace(request.Target.ShiftProfileId) ||
             string.IsNullOrWhiteSpace(request.Target.StationId) || request.Target.CredentialVersion is null or <= 0))
            return Blocked("TargetShiftProfileAuthenticationInvalid");
        bool legacyAuthority = request.Mode is AuthenticationIntegrationMode.LegacyLogin or
            AuthenticationIntegrationMode.ShiftProfileShadow;
        return new(IntegrationControlDecision.Allowed, legacyAuthority,
            request.Target.ShiftProfileId, "AuthenticationIntegrationAllowed");

        static AuthenticationIntegrationDecision Blocked(string category) =>
            new(IntegrationControlDecision.Blocked, true, null, category);
    }
}

public enum ReportingIntegrationMode
{
    LegacyReporting,
    SnapshotShadow,
    SnapshotPilot,
    SnapshotAuthority
}

public sealed record ReportingIntegrationEvidence(
    bool SnapshotValidated,
    bool FinalizedSnapshotImmutable,
    bool LegacyReportsReadable,
    bool ExportValidated,
    bool ReadRoutingValidated,
    string ComparisonEvidenceId);

public sealed record ReportingIntegrationRequest(
    ReportingIntegrationMode Mode,
    IntegrationAuthorityRoutingDecision Routing,
    ReportingIntegrationEvidence Evidence);

public sealed record ReportingIntegrationDecision(
    IntegrationControlDecision Decision,
    bool LegacyReportingAuthoritative,
    bool SnapshotReadAllowed,
    string ResultCategory);

public static class ReportingIntegrationPolicy
{
    public static ReportingIntegrationDecision Evaluate(ReportingIntegrationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(request.Mode)) return Blocked("UnknownReportingMode");
        IntegrationAuthorityMode expected = request.Mode switch
        {
            ReportingIntegrationMode.LegacyReporting => IntegrationAuthorityMode.LegacyOnly,
            ReportingIntegrationMode.SnapshotShadow => IntegrationAuthorityMode.ShadowValidation,
            ReportingIntegrationMode.SnapshotPilot => IntegrationAuthorityMode.PilotTarget,
            ReportingIntegrationMode.SnapshotAuthority => IntegrationAuthorityMode.FullTarget,
            _ => throw new ArgumentOutOfRangeException()
        };
        if (request.Routing.Decision != IntegrationControlDecision.Allowed ||
            request.Routing.EffectiveMode != expected || request.Routing.ProductionMutationAllowed)
            return Blocked("ReportingRoutingNotApproved");
        if (request.Mode == ReportingIntegrationMode.LegacyReporting)
            return new(IntegrationControlDecision.Allowed, true, false, "LegacyReportingRetained");
        if (!request.Evidence.SnapshotValidated || !request.Evidence.FinalizedSnapshotImmutable ||
            !request.Evidence.LegacyReportsReadable || string.IsNullOrWhiteSpace(request.Evidence.ComparisonEvidenceId))
            return Blocked("SnapshotValidationRequired");
        if (request.Mode is ReportingIntegrationMode.SnapshotPilot or ReportingIntegrationMode.SnapshotAuthority &&
            !request.Evidence.ExportValidated)
            return Blocked("SnapshotExportValidationRequired");
        if (request.Mode == ReportingIntegrationMode.SnapshotAuthority && !request.Evidence.ReadRoutingValidated)
            return Blocked("SnapshotReadRoutingValidationRequired");
        return new(IntegrationControlDecision.Allowed,
            request.Mode != ReportingIntegrationMode.SnapshotAuthority, true, "ReportingIntegrationAllowed");

        static ReportingIntegrationDecision Blocked(string category) =>
            new(IntegrationControlDecision.Blocked, true, false, category);
    }
}

public enum RuntimeEventIntegrationMode
{
    LegacyRuntimeEvent,
    TargetProjection,
    ShadowComparison,
    PilotReadOnly
}

public sealed record RuntimeEventIntegrationEvidence(
    string RuntimeComparisonEvidenceId,
    string EventComparisonEvidenceId,
    bool TargetReadOnly,
    bool MutationAttempted,
    bool RecalculationSideEffects,
    bool EvidencePreserved);

public sealed record RuntimeEventIntegrationRequest(
    RuntimeEventIntegrationMode Mode,
    IntegrationAuthorityRoutingDecision Routing,
    RuntimeEventIntegrationEvidence Evidence);

public sealed record RuntimeEventIntegrationDecision(
    IntegrationControlDecision Decision,
    bool LegacyProjectionAuthoritative,
    bool TargetProjectionReadOnly,
    string ResultCategory);

public static class RuntimeEventIntegrationPolicy
{
    public static RuntimeEventIntegrationDecision Evaluate(RuntimeEventIntegrationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(request.Mode)) return Blocked("UnknownRuntimeEventMode");
        IntegrationAuthorityMode expected = request.Mode switch
        {
            RuntimeEventIntegrationMode.LegacyRuntimeEvent => IntegrationAuthorityMode.LegacyOnly,
            RuntimeEventIntegrationMode.TargetProjection => IntegrationAuthorityMode.FullTarget,
            RuntimeEventIntegrationMode.ShadowComparison => IntegrationAuthorityMode.ShadowValidation,
            RuntimeEventIntegrationMode.PilotReadOnly => IntegrationAuthorityMode.PilotTarget,
            _ => throw new ArgumentOutOfRangeException()
        };
        if (request.Routing.Decision != IntegrationControlDecision.Allowed ||
            request.Routing.EffectiveMode != expected || request.Routing.ProductionMutationAllowed)
            return Blocked("RuntimeEventRoutingNotApproved");
        if (request.Mode == RuntimeEventIntegrationMode.LegacyRuntimeEvent)
            return new(IntegrationControlDecision.Allowed, true, true, "LegacyRuntimeEventRetained");
        if (!request.Evidence.TargetReadOnly || request.Evidence.MutationAttempted ||
            request.Evidence.RecalculationSideEffects || !request.Evidence.EvidencePreserved ||
            string.IsNullOrWhiteSpace(request.Evidence.RuntimeComparisonEvidenceId) ||
            string.IsNullOrWhiteSpace(request.Evidence.EventComparisonEvidenceId))
            return Blocked("ReadOnlyProjectionEvidenceRequired");
        return new(IntegrationControlDecision.Allowed,
            request.Mode != RuntimeEventIntegrationMode.TargetProjection, true,
            "RuntimeEventIntegrationAllowed");

        static RuntimeEventIntegrationDecision Blocked(string category) =>
            new(IntegrationControlDecision.Blocked, true, true, category);
    }
}

public enum ProtectedSettingsIntegrationMode
{
    LegacySettings,
    ProtectedSettingsShadow,
    ProtectedSettingsPilot
}

public sealed record ProtectedSettingsIntegrationEvidence(
    EsdAuthorityMode EsdAuthorityMode,
    bool LegacySettingsReadable,
    bool TargetProvisioningRequested,
    bool EsdCutoverRequested,
    bool MutationAttempted,
    string EvidenceId);

public sealed record ProtectedSettingsIntegrationRequest(
    ProtectedSettingsIntegrationMode Mode,
    IntegrationAuthorityRoutingDecision Routing,
    ProtectedSettingsIntegrationEvidence Evidence);

public sealed record ProtectedSettingsIntegrationDecision(
    IntegrationControlDecision Decision,
    bool LegacySettingsAuthoritative,
    bool TargetProvisioningAllowed,
    string ResultCategory);

public static class ProtectedSettingsIntegrationPolicy
{
    public static ProtectedSettingsIntegrationDecision Evaluate(ProtectedSettingsIntegrationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(request.Mode)) return Blocked("UnknownProtectedSettingsMode");
        IntegrationAuthorityMode expected = request.Mode switch
        {
            ProtectedSettingsIntegrationMode.LegacySettings => IntegrationAuthorityMode.LegacyOnly,
            ProtectedSettingsIntegrationMode.ProtectedSettingsShadow => IntegrationAuthorityMode.ShadowValidation,
            ProtectedSettingsIntegrationMode.ProtectedSettingsPilot => IntegrationAuthorityMode.PilotTarget,
            _ => throw new ArgumentOutOfRangeException()
        };
        if (request.Routing.Decision != IntegrationControlDecision.Allowed ||
            request.Routing.EffectiveMode != expected || request.Routing.ProductionMutationAllowed)
            return Blocked("ProtectedSettingsRoutingNotApproved");
        if (request.Evidence.EsdAuthorityMode != EsdAuthorityMode.LegacyAuthoritative ||
            request.Evidence.TargetProvisioningRequested || request.Evidence.EsdCutoverRequested ||
            request.Evidence.MutationAttempted || !request.Evidence.LegacySettingsReadable ||
            string.IsNullOrWhiteSpace(request.Evidence.EvidenceId))
            return Blocked("ProtectedSettingsMustRemainLegacyAuthoritative");
        return new(IntegrationControlDecision.Allowed, true, false,
            request.Mode == ProtectedSettingsIntegrationMode.LegacySettings
                ? "LegacySettingsRetained" : "ProtectedSettingsObservationAllowed");

        static ProtectedSettingsIntegrationDecision Blocked(string category) =>
            new(IntegrationControlDecision.Blocked, true, false, category);
    }
}
