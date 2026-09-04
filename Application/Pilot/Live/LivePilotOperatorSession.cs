using Rah_Negar.Foundation.Application.Pilot.Operational;
using Rah_Negar.Foundation.Application.Pilot.Validation;

namespace Rah_Negar.Foundation.Application.Pilot.Live;

/// <summary>
/// Explicit operator-driven facade over the Phase 9.2 coordinator. It contains no timer,
/// polling, retry, authority switch, or production command path.
/// </summary>
public sealed class LivePilotOperatorSession : IDisposable
{
    private readonly ControlledPilotOperationalRehearsalCoordinator _coordinator;
    private readonly LivePilotReadOnlyPreflightResult _databasePreflight;
    private readonly LivePilotReadScope _scope;
    private readonly TimeProvider _timeProvider;
    private ControlledPilotOperationalOperationResult? _lastOperation;
    private bool _disposed;

    public LivePilotOperatorSession(
        ControlledPilotOperationalRehearsalCoordinator coordinator,
        LivePilotReadOnlyPreflightResult databasePreflight,
        TimeProvider? timeProvider = null)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _databasePreflight = databasePreflight ??
            throw new ArgumentNullException(nameof(databasePreflight));
        _scope = databasePreflight.Scope ??
            throw new ArgumentException("A ready read-only preflight is required.",
                nameof(databasePreflight));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public ControlledPilotOperationalLifecycle Lifecycle => _coordinator.Lifecycle;
    public bool IsTerminal => Lifecycle is ControlledPilotOperationalLifecycle.Completed or
        ControlledPilotOperationalLifecycle.Stopped or
        ControlledPilotOperationalLifecycle.Failed or
        ControlledPilotOperationalLifecycle.Disposed;
    public bool AutomaticallyStarts => false;
    public bool AutomaticallyRetries => false;
    public bool UsesPolling => false;
    public bool UsesTimer => false;
    public bool UsesBackgroundWork => false;
    public bool ChangesAuthority => false;
    public bool MutatesProduction => false;

    public async ValueTask<LivePilotDashboardView> StartObservationAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        DateTimeOffset now = UtcNow();
        _lastOperation = _coordinator.RunPreflight(now, cancellationToken);
        if (_lastOperation.Status != ControlledPilotOperationalOperationStatus.Accepted)
            return CreateView();

        _lastOperation = _coordinator.Approve(UtcNow());
        if (_lastOperation.Status != ControlledPilotOperationalOperationStatus.Accepted)
            return CreateView();
        _lastOperation = _coordinator.Start(UtcNow());
        if (_lastOperation.Status != ControlledPilotOperationalOperationStatus.Accepted)
            return CreateView();
        _lastOperation = await _coordinator.ObserveAsync(UtcNow(), cancellationToken)
            .ConfigureAwait(false);
        return CreateView();
    }

    public async ValueTask<LivePilotDashboardView> CompleteAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        DateTimeOffset now = UtcNow();
        _lastOperation = await _coordinator.RecordOperatorDecisionAsync(
            new ControlledPilotOperationalOperatorDecision(
                $"complete-{Guid.NewGuid():N}", OperationalOperatorDecisionKind.Complete,
                "operator-complete-evidence", now), now,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return CreateView();
    }

    public async ValueTask<LivePilotDashboardView> StopAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_coordinator.Lifecycle != ControlledPilotOperationalLifecycle.ReviewRequired)
        {
            Dispose();
            return LivePilotDashboardView.Waiting();
        }
        DateTimeOffset now = UtcNow();
        _lastOperation = await _coordinator.RecordOperatorDecisionAsync(
            new ControlledPilotOperationalOperatorDecision(
                $"stop-{Guid.NewGuid():N}", OperationalOperatorDecisionKind.Stop,
                "operator-stop-evidence", now), now,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return CreateView();
    }

    public LivePilotDashboardView CreateView()
    {
        ControlledPilotOperationalOperationResult? result = _lastOperation;
        var byWorkflow = (result?.WorkflowResults ?? [])
            .ToDictionary(item => item.Workflow);
        LivePilotWorkflowView[] workflows = Enum.GetValues<PilotValidationWorkflow>()
            .Select(workflow => byWorkflow.TryGetValue(workflow, out var item)
                ? new LivePilotWorkflowView(workflow, WorkflowStatus(item.Status),
                    ComparisonStatus(item.Status), item.FingerprintSpecificationVersion,
                    item.Status, item.EvidenceReference, item.ObservedAtUtc)
                : new LivePilotWorkflowView(workflow, "در انتظار", "بررسی نشده",
                    LivePilotDashboardView.FingerprintVersion(workflow)))
            .ToArray();
        string preflight = result?.Preflight?.Status switch
        {
            ControlledPilotOperationalPreflightStatus.Ready => "آماده",
            ControlledPilotOperationalPreflightStatus.RequiresReview => "نیازمند بررسی",
            ControlledPilotOperationalPreflightStatus.Blocked => "مسدود",
            _ => _databasePreflight.IsReady ? "اتصال فقط‌خواندنی تأیید شد" : "ناموفق"
        };
        string monitoring = result?.MonitoringEvidence?.Status switch
        {
            ControlledPilotOperationalHealthStatus.Healthy => "سالم",
            ControlledPilotOperationalHealthStatus.AttentionRequired => "نیازمند توجه",
            ControlledPilotOperationalHealthStatus.Failed => "ناموفق",
            ControlledPilotOperationalHealthStatus.Stopped => "متوقف",
            _ => "اجرا نشده"
        };
        string completion = result?.Lifecycle switch
        {
            ControlledPilotOperationalLifecycle.Completed => "تکمیل شد",
            ControlledPilotOperationalLifecycle.Stopped => "متوقف شد",
            ControlledPilotOperationalLifecycle.Failed => "ناموفق",
            _ => "تکمیل نشده"
        };
        return new LivePilotDashboardView("Pilot / Rehearsal 9.3",
            StationText(_scope.StationId), LifecycleText(_coordinator.Lifecycle), workflows: workflows,
            preflightStatus: preflight, monitoringStatus: monitoring,
            rollbackReadiness: "آماده؛ فقط شواهد و بدون اجرای بازگشت",
            stopReason: StopReason(result?.StopDecision?.Reason),
            completionStatus: completion);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _coordinator.Dispose();
    }

    private DateTimeOffset UtcNow() => _timeProvider.GetUtcNow().ToUniversalTime();

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static string StationText(string stationId) => stationId switch
    {
        "station-rasht" => "رشت",
        "station-ramsar" => "رامسر",
        _ => "نامشخص"
    };

    private static string LifecycleText(ControlledPilotOperationalLifecycle value) => value switch
    {
        ControlledPilotOperationalLifecycle.Created => "ایجاد شده",
        ControlledPilotOperationalLifecycle.PreflightPassed => "پیش‌بررسی موفق",
        ControlledPilotOperationalLifecycle.Approved => "تأیید شده",
        ControlledPilotOperationalLifecycle.Started => "شروع شده",
        ControlledPilotOperationalLifecycle.Observing => "در حال مشاهده",
        ControlledPilotOperationalLifecycle.ReviewRequired => "آماده تصمیم اپراتور",
        ControlledPilotOperationalLifecycle.Completed => "تکمیل شده",
        ControlledPilotOperationalLifecycle.Stopped => "متوقف شده",
        ControlledPilotOperationalLifecycle.Failed => "ناموفق",
        ControlledPilotOperationalLifecycle.Disposed => "بسته شده",
        _ => "نامشخص"
    };

    private static string WorkflowStatus(OperationalWorkflowComparisonStatus value) => value switch
    {
        OperationalWorkflowComparisonStatus.Match => "انجام شد",
        OperationalWorkflowComparisonStatus.Difference => "انجام شد",
        _ => "ناموفق"
    };

    private static string ComparisonStatus(OperationalWorkflowComparisonStatus value) => value switch
    {
        OperationalWorkflowComparisonStatus.Match => "مطابق",
        OperationalWorkflowComparisonStatus.Difference => "تفاوت مشاهده شد",
        _ => "ناموفق"
    };

    private static string StopReason(ControlledPilotOperationalStopReason? value) => value switch
    {
        ControlledPilotOperationalStopReason.ObserverFailure => "خطا در مشاهده فقط‌خواندنی",
        ControlledPilotOperationalStopReason.FingerprintMismatchAbovePolicy =>
            "تعداد تفاوت بیش از حد مجاز",
        ControlledPilotOperationalStopReason.EvidenceIntegrityFailure => "اعتبار شواهد ناموفق",
        ControlledPilotOperationalStopReason.RollbackReadinessLost => "آمادگی بازگشت از دست رفت",
        ControlledPilotOperationalStopReason.SecurityBoundaryViolation => "نقض مرز ایمنی",
        ControlledPilotOperationalStopReason.Cancellation => "لغو توسط اپراتور",
        ControlledPilotOperationalStopReason.ExplicitOperatorStop => "توقف توسط اپراتور",
        ControlledPilotOperationalStopReason.RollbackRequested => "درخواست بازگشت ثبت شد",
        _ => "ندارد"
    };
}
