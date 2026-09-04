using System.Collections.ObjectModel;
using Rah_Negar.Foundation.Application.Pilot.Operational;
using Rah_Negar.Foundation.Application.Pilot.Production;
using Rah_Negar.Foundation.Application.Pilot.Validation;

namespace Rah_Negar.Foundation.Application.Pilot.Live;

public sealed record LivePilotObservationPair<TObservation>(
    TObservation Legacy,
    TObservation Target,
    string EvidenceReference,
    bool RequiresReview = false);

public interface ILiveAuthenticationPilotReadModel
{
    ValueTask<LivePilotObservationPair<AuthenticationOperationalObservation>> ReadAsync(
        CancellationToken cancellationToken = default);
}

public interface ILiveReportingPilotReadModel
{
    ValueTask<LivePilotObservationPair<ReportingOperationalObservation>> ReadAsync(
        CancellationToken cancellationToken = default);
}

public interface ILiveRuntimeEventPilotReadModel
{
    ValueTask<LivePilotObservationPair<RuntimeEventOperationalObservation>> ReadAsync(
        CancellationToken cancellationToken = default);
}

public interface ILiveProtectedSettingsPilotReadModel
{
    ValueTask<LivePilotObservationPair<ProtectedSettingsOperationalObservation>> ReadAsync(
        CancellationToken cancellationToken = default);
}

public interface ILiveExportPilotReadModel
{
    ValueTask<LivePilotObservationPair<ExportOperationalObservation>> ReadAsync(
        CancellationToken cancellationToken = default);
}

public sealed record LivePilotReadScope(
    string StationId,
    string StationName,
    ControlledProductionPilotScope StationScope,
    long DataStartDate,
    long DateFrom,
    long DateTo,
    long PeriodStartMinute,
    long PeriodEndMinute,
    string PeriodIdentity,
    bool EsdAdjustmentEnabled,
    decimal EsdAdjustmentHours);

public enum LivePilotReadOnlyPreflightStatus
{
    Ready,
    Blocked,
    Canceled
}

public sealed class LivePilotReadOnlyPreflightResult
{
    public LivePilotReadOnlyPreflightResult(
        LivePilotReadOnlyPreflightStatus status,
        string reasonCode,
        DateTimeOffset evaluatedAtUtc,
        LivePilotReadScope? scope = null)
    {
        Status = status;
        ReasonCode = reasonCode;
        EvaluatedAtUtc = evaluatedAtUtc;
        Scope = scope;
    }

    public LivePilotReadOnlyPreflightStatus Status { get; }
    public string ReasonCode { get; }
    public DateTimeOffset EvaluatedAtUtc { get; }
    public LivePilotReadScope? Scope { get; }
    public bool IsReady => Status == LivePilotReadOnlyPreflightStatus.Ready && Scope is not null;
    public bool MutatedProduction => false;
    public bool RanMigration => false;
}

public interface ILivePilotReadOnlyPreflight
{
    ValueTask<LivePilotReadOnlyPreflightResult> EvaluateAsync(
        DateTimeOffset evaluatedAtUtc,
        CancellationToken cancellationToken = default);
}

public sealed class LivePilotWorkflowView
{
    public LivePilotWorkflowView(
        PilotValidationWorkflow workflow,
        string status,
        string comparison,
        string fingerprintSpecificationVersion,
        OperationalWorkflowComparisonStatus? resultStatus = null,
        string? evidenceReference = null,
        DateTimeOffset? observedAtUtc = null)
    {
        Workflow = workflow;
        Status = Safe(status, "نامشخص");
        Comparison = Safe(comparison, "نامشخص");
        FingerprintSpecificationVersion = Safe(
            fingerprintSpecificationVersion, "ثبت نشده");
        ResultStatus = resultStatus.HasValue && Enum.IsDefined(resultStatus.Value)
            ? resultStatus
            : null;
        EvidenceReference = OperationalText.IsUsableIdentifier(evidenceReference)
            ? evidenceReference
            : null;
        ObservedAtUtc = observedAtUtc?.Offset == TimeSpan.Zero ? observedAtUtc : null;
    }

    public PilotValidationWorkflow Workflow { get; }
    public string Status { get; }
    public string Comparison { get; }
    public string FingerprintSpecificationVersion { get; }
    public OperationalWorkflowComparisonStatus? ResultStatus { get; }
    public string? EvidenceReference { get; }
    public DateTimeOffset? ObservedAtUtc { get; }

    private static string Safe(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 160 || value.Any(char.IsControl)
            ? fallback
            : value.Trim();
}

public sealed class LivePilotDashboardView
{
    public LivePilotDashboardView(
        string pilotIdentity,
        string station,
        string sessionStatus,
        string preflightStatus,
        IEnumerable<LivePilotWorkflowView> workflows,
        string monitoringStatus,
        string rollbackReadiness,
        string stopReason,
        string completionStatus)
    {
        ArgumentNullException.ThrowIfNull(workflows);
        PilotIdentity = Safe(pilotIdentity, "ثبت نشده");
        Station = Safe(station, "نامشخص");
        SessionStatus = Safe(sessionStatus, "ایجاد نشده");
        PreflightStatus = Safe(preflightStatus, "اجرا نشده");
        Workflows = new ReadOnlyCollection<LivePilotWorkflowView>(workflows
            .Where(item => item is not null)
            .OrderBy(item => item.Workflow)
            .ToArray());
        MonitoringStatus = Safe(monitoringStatus, "اجرا نشده");
        RollbackReadiness = Safe(rollbackReadiness, "نامشخص");
        StopReason = Safe(stopReason, "ندارد");
        CompletionStatus = Safe(completionStatus, "تکمیل نشده");
    }

    public string PilotIdentity { get; }
    public string Station { get; }
    public string SessionStatus { get; }
    public string LegacyAuthorityIndicator => "مرجع بهره‌برداری: سامانه فعلی (Legacy)";
    public string PreflightStatus { get; }
    public IReadOnlyList<LivePilotWorkflowView> Workflows { get; }
    public string MonitoringStatus { get; }
    public string RollbackReadiness { get; }
    public string StopReason { get; }
    public string CompletionStatus { get; }
    public bool IsReadOnly => true;
    public bool CanSwitchAuthority => false;

    public static LivePilotDashboardView Waiting() => new(
        "Pilot / Rehearsal", "نامشخص", "در انتظار شروع", "اجرا نشده",
        Enum.GetValues<PilotValidationWorkflow>().Select(workflow =>
            new LivePilotWorkflowView(workflow, "در انتظار", "بررسی نشده",
                FingerprintVersion(workflow))),
        "اجرا نشده", "در انتظار پیش‌بررسی", "ندارد", "تکمیل نشده");

    public static string FingerprintVersion(PilotValidationWorkflow workflow) => workflow switch
    {
        PilotValidationWorkflow.Authentication => "auth-fingerprint-v1",
        PilotValidationWorkflow.Reporting => "reporting-fingerprint-v1",
        PilotValidationWorkflow.RuntimeEvent => "runtime-event-fingerprint-v1",
        PilotValidationWorkflow.ProtectedSettings => "protected-settings-fingerprint-v1",
        PilotValidationWorkflow.Export => "export-fingerprint-v1",
        _ => "ثبت نشده"
    };

    private static string Safe(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 200 || value.Any(char.IsControl)
            ? fallback
            : value.Trim();
}
