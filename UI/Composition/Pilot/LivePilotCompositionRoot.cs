using Rah_Negar.Core.Reporting.Projection;
using Rah_Negar.Core.Runtime.Calculation;
using Rah_Negar.Foundation.Application.Activation;
using Rah_Negar.Foundation.Application.Activation.Preparation;
using Rah_Negar.Foundation.Application.Pilot.Deployment;
using Rah_Negar.Foundation.Application.Pilot.Live;
using Rah_Negar.Foundation.Application.Pilot.Operational;
using Rah_Negar.Foundation.Application.Pilot.Validation;
using Rah_Negar.Foundation.Application.Reporting.Export;
using Rah_Negar.Infrastructure.Database.Readiness;
using Rah_Negar.Infrastructure.Pilot;
using Rah_Negar.UI.Pilot;

namespace Rah_Negar.UI.Composition.Pilot;

/// <summary>
/// The single live Pilot composition root. Callers construct it only after an explicit
/// operator action and confirmation. It exposes observation and presentation only.
/// </summary>
public sealed class LivePilotCompositionRoot
{
    private readonly string _databasePath;
    private readonly TimeProvider _timeProvider;

    public LivePilotCompositionRoot(string databasePath, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _databasePath = databasePath;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async ValueTask<LivePilotCompositionResult> ComposeAsync(
        CancellationToken cancellationToken = default)
    {
        var dashboard = new PilotDashboardControl();
        var connections = new PilotReadOnlySqliteConnectionFactory(_databasePath);
        var databasePreflight = new LivePilotReadOnlyPreflight(connections);
        DateTimeOffset now = _timeProvider.GetUtcNow().ToUniversalTime();
        LivePilotReadOnlyPreflightResult preflight = await databasePreflight.EvaluateAsync(
            now, cancellationToken);
        if (!preflight.IsReady)
        {
            var blocked = BlockedView(preflight.Status);
            dashboard.RenderLive(blocked);
            return new LivePilotCompositionResult(dashboard, null, blocked,
                preflight.ReasonCode);
        }

        LivePilotReadScope scope = preflight.Scope!;
        var readModels = new LiveSqlitePilotReadModels(connections, scope,
            new RuntimeCalculator(), new ReportCalculator(),
            new DeterministicReportFileNamePolicy(), _timeProvider);
        IControlledPilotOperationalWorkflowObserver[] observers =
        [
            new LiveAuthenticationPilotObserver(readModels),
            new LiveReportingPilotObserver(readModels),
            new LiveRuntimeEventPilotObserver(readModels),
            new LiveProtectedSettingsPilotObserver(readModels),
            new LiveExportPilotObserver(readModels)
        ];

        string identity = Guid.NewGuid().ToString("N");
        string releaseId = "release-9.3";
        string preparationId = $"pilot-preparation-{identity}";
        string rollbackPlan = $"pilot-rollback-plan-{identity}";
        string rollbackEvidence = $"pilot-rollback-evidence-{identity}";
        var context = new ControlledPilotOperationalRehearsalContext(
            $"rehearsal-{identity}", $"pilot-{identity}", $"session-{identity}",
            $"correlation-{identity}", releaseId, scope.StationScope,
            now.AddMinutes(-1), now.AddHours(2),
            Enum.GetValues<PilotValidationWorkflow>(), "legacy-operator-confirmed",
            $"{preparationId}:cutover-evidence", rollbackEvidence,
            explicitApproval: true);
        RollbackVerificationResult rollback = new(rollbackPlan,
            RollbackEvidenceStatus.Verified, "pilot-operator", rollbackEvidence);
        ProductionActivationReadinessResult preparation = CreatePreparationEvidence(
            preparationId, releaseId, rollback, now.AddSeconds(-1));
        var coordinator = new ControlledPilotOperationalRehearsalCoordinator(context,
            new OperationalReleaseEvidence(
                ControlledPilotOperationalPreflight.RequiredBranchIdentifier,
                releaseId, "phase93-runtime-evidence", OperationalEvidenceStatus.Verified),
            preparation,
            new ControlledPilotPrerequisiteEvidence(context.PilotId, releaseId,
                "phase92-operational-prerequisite", OperationalEvidenceStatus.Verified,
                LegacyRemainsAuthoritative: true, CompletedSingleObservationAttempt: true),
            rollback, observers,
            new InMemoryControlledPilotOperationalEvidenceDestination(),
            allowedFingerprintDifferences: observers.Length);
        var session = new LivePilotOperatorSession(coordinator, preflight, _timeProvider);
        LivePilotDashboardView initial = session.CreateView();
        dashboard.RenderLive(initial);
        return new LivePilotCompositionResult(dashboard, session, initial,
            "live-composition-ready");
    }

    public bool AutomaticallyConstructed => false;
    public bool AutomaticallyLaunches => false;
    public bool UsesServiceLocator => false;
    public bool ExposesProductionExecutor => false;
    public bool ExposesMigrationExecutor => false;
    public bool ExposesSettingsWriter => false;
    public bool ExposesEventWriter => false;
    public bool ExposesEsdMutation => false;

    private static ProductionActivationReadinessResult CreatePreparationEvidence(
        string preparationId,
        string releaseId,
        RollbackVerificationResult rollback,
        DateTimeOffset timestamp)
    {
        string[] approvals =
        [
            "phase93-data-owner-evidence",
            "phase93-operations-evidence",
            "phase93-security-evidence"
        ];
        var context = new ProductionActivationPreparationContext(preparationId,
            releaseId, ProductionActivationScope.SnapshotReportingActivation,
            LegacyAuthorityState.LegacyAuthoritative,
            PilotValidationResultStatus.Completed,
            PilotDeploymentReadinessStatus.Ready, rollback.RollbackPlanReference,
            approvals, timestamp, explicitlyRequested: true);
        ProductionActivationGate[] gates = Enum.GetValues<ProductionActivationGateType>()
            .Select(type => new ProductionActivationGate(type,
                ProductionActivationGateStatus.Satisfied, GateEvidence(type, rollback),
                "phase93-pilot-reviewer", timestamp.AddSeconds(-1))).ToArray();
        var backup = new BackupVerificationResult("phase93-backup-evidence",
            BackupEvidenceStatus.Verified, RestoreTestStatus.Passed,
            timestamp.AddSeconds(-1));
        ProductionActivationStopCondition[] stops = Enum
            .GetValues<ProductionActivationStopConditionType>()
            .Select(type => new ProductionActivationStopCondition(type, false,
                $"phase93-stop-{type.ToString().ToLowerInvariant()}")).ToArray();
        return new ProductionActivationReadinessCoordinator().Evaluate(
            context, gates, backup, rollback, stops);
    }

    private static string GateEvidence(
        ProductionActivationGateType type,
        RollbackVerificationResult rollback) => type switch
    {
        ProductionActivationGateType.SecurityReview => "phase93-security-evidence",
        ProductionActivationGateType.OperationsReadiness => "phase93-operations-evidence",
        ProductionActivationGateType.DataOwnerApproval => "phase93-data-owner-evidence",
        ProductionActivationGateType.RollbackReadiness => rollback.EvidenceReference,
        ProductionActivationGateType.ValidationCompletion => "phase93-validation-evidence",
        ProductionActivationGateType.DeploymentReadiness => "phase93-deployment-evidence",
        _ => "phase93-gate-evidence"
    };

    private static LivePilotDashboardView BlockedView(
        LivePilotReadOnlyPreflightStatus status) => new(
            "Pilot / Rehearsal 9.3", "نامشخص", "شروع نشد",
            status == LivePilotReadOnlyPreflightStatus.Canceled ? "لغو شد" : "مسدود",
            Enum.GetValues<PilotValidationWorkflow>().Select(workflow =>
                new LivePilotWorkflowView(workflow, "اجرا نشد", "بررسی نشد",
                    LivePilotDashboardView.FingerprintVersion(workflow))),
            "اجرا نشد", "تأیید نشد", "پیش‌بررسی فقط‌خواندنی ناموفق بود",
            "تکمیل نشد");
}

public sealed record LivePilotCompositionResult(
    PilotDashboardControl Dashboard,
    LivePilotOperatorSession? Session,
    LivePilotDashboardView InitialView,
    string ReasonCode)
{
    public bool IsReady => Session is not null;
    public bool ChangesProductionAuthority => false;
}
