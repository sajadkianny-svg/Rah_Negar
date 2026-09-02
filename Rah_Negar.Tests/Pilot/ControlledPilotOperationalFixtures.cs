using Rah_Negar.Core.Event;
using Rah_Negar.Core.Event.Comparison;
using Rah_Negar.Core.Runtime;
using Rah_Negar.Core.Runtime.Calculation;
using Rah_Negar.Foundation.Application.Activation;
using Rah_Negar.Foundation.Application.Activation.Preparation;
using Rah_Negar.Foundation.Application.Pilot.Deployment;
using Rah_Negar.Foundation.Application.Pilot.Operational;
using Rah_Negar.Foundation.Application.Pilot.Production;
using Rah_Negar.Foundation.Application.Pilot.Validation;

namespace Rah_Negar.Tests.Pilot;

internal sealed class ControlledPilotOperationalFixture
{
    public static readonly DateTimeOffset WindowStart =
        new(2026, 9, 2, 8, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset WindowEnd = WindowStart.AddHours(2);
    private const long PeriodStart = 0;
    private const long PeriodEnd = 2_880;

    private ControlledPilotOperationalFixture(
        string stationId,
        ControlledProductionPilotScope scope,
        int unitCount,
        RuntimeEventOperationalObservation runtimeObservation,
        ReportingOperationalObservation reportingObservation)
    {
        StationId = stationId;
        Scope = scope;
        UnitCount = unitCount;
        RuntimeObservation = runtimeObservation;
        ReportingObservation = reportingObservation;
    }

    public string StationId { get; }
    public ControlledProductionPilotScope Scope { get; }
    public int UnitCount { get; }
    public RuntimeEventOperationalObservation RuntimeObservation { get; }
    public ReportingOperationalObservation ReportingObservation { get; }

    public static ControlledPilotOperationalFixture Rasht()
    {
        RuntimeCalculationContext[] contexts =
        [
            Context("rasht-unit-1", UnitOperationalState.Stopped,
                UnitOperationalState.Stopped,
                EventAt("rasht-u1-start", "rasht-unit-1", EventType.Start, 1_380, 1),
                EventAt("rasht-u1-nsd", "rasht-unit-1", EventType.Nsd, 1_500, 2)),
            Context("rasht-unit-2", UnitOperationalState.Stopped,
                UnitOperationalState.Stopped),
            Context("rasht-unit-3", UnitOperationalState.Stopped,
                UnitOperationalState.Stopped,
                EventAt("rasht-u3-oh", "rasht-unit-3", EventType.Oh, 100, 1),
                EventAt("rasht-u3-start-1", "rasht-unit-3", EventType.Start, 200, 2),
                EventAt("rasht-u3-nsd", "rasht-unit-3", EventType.Nsd, 260, 3),
                EventAt("rasht-u3-start-2", "rasht-unit-3", EventType.Start, 600, 4),
                EventAt("rasht-u3-esd", "rasht-unit-3", EventType.Esd, 700, 5))
        ];
        RuntimeEventOperationalObservation runtime = Runtime("station-rasht", contexts);
        return new("station-rasht",
            ControlledProductionPilotScope.RashtReadOnlyObservation, 3, runtime,
            Reporting("station-rasht", "1405-06", 3));
    }

    public static ControlledPilotOperationalFixture Ramsar()
    {
        RuntimeCalculationContext[] contexts =
        [
            Context("ramsar-unit-1", UnitOperationalState.Stopped,
                UnitOperationalState.Stopped,
                EventAt("ramsar-u1-start", "ramsar-unit-1", EventType.Start, 1_380, 1),
                EventAt("ramsar-u1-nsd", "ramsar-unit-1", EventType.Nsd, 1_500, 2)),
            Context("ramsar-unit-2", UnitOperationalState.Stopped,
                UnitOperationalState.Stopped,
                EventAt("ramsar-u2-start", "ramsar-unit-2", EventType.Start, 60, 1),
                EventAt("ramsar-u2-nsd", "ramsar-unit-2", EventType.Nsd, 120, 2)),
            Context("ramsar-unit-3", UnitOperationalState.Stopped,
                UnitOperationalState.Stopped,
                EventAt("ramsar-u3-start", "ramsar-unit-3", EventType.Start, 300, 1),
                EventAt("ramsar-u3-esd", "ramsar-unit-3", EventType.Esd, 420, 2)),
            Context("ramsar-unit-4", UnitOperationalState.Stopped,
                UnitOperationalState.Stopped,
                EventAt("ramsar-u4-start", "ramsar-unit-4", EventType.Start, 1_400, 1),
                EventAt("ramsar-u4-nsd", "ramsar-unit-4", EventType.Nsd, 1_440, 2))
        ];
        RuntimeEventOperationalObservation runtime = Runtime("station-ramsar", contexts);
        return new("station-ramsar",
            ControlledProductionPilotScope.RamsarReadOnlyObservation, 4, runtime,
            Reporting("station-ramsar", "1405-06", 4));
    }

    public ControlledPilotOperationalRehearsalContext Context(
        IEnumerable<PilotValidationWorkflow>? workflows = null,
        bool approved = true) => new(
            $"rehearsal-{StationId}",
            $"pilot-{StationId}",
            $"session-{StationId}",
            $"correlation-{StationId}",
            "release-9.2",
            Scope,
            WindowStart,
            WindowEnd,
            workflows ?? Enum.GetValues<PilotValidationWorkflow>(),
            "operator-primary",
            "preparation-92:cutover-evidence",
            "rollback-evidence-92",
            approved);

    public OperationalReleaseEvidence Release(
        OperationalEvidenceStatus status = OperationalEvidenceStatus.Verified,
        string branch = ControlledPilotOperationalPreflight.RequiredBranchIdentifier) =>
        new(branch, "release-9.2", "runtime-release-evidence-92", status);

    public ControlledPilotPrerequisiteEvidence Prerequisite(
        OperationalEvidenceStatus status = OperationalEvidenceStatus.Verified) => new(
            $"pilot-{StationId}", "release-9.2", "phase91-prerequisite-evidence",
            status, LegacyRemainsAuthoritative: true,
            CompletedSingleObservationAttempt: true);

    public RollbackVerificationResult Rollback(
        RollbackEvidenceStatus status = RollbackEvidenceStatus.Verified) => new(
            "rollback-plan-92", status, "rollback-owner-92", "rollback-evidence-92");

    public ProductionActivationReadinessResult Preparation()
    {
        DateTimeOffset timestamp = WindowStart.AddMinutes(-30);
        var context = new ProductionActivationPreparationContext("preparation-92",
            "release-9.2", ProductionActivationScope.SnapshotReportingActivation,
            LegacyAuthorityState.LegacyAuthoritative, PilotValidationResultStatus.Completed,
            PilotDeploymentReadinessStatus.Ready, "rollback-plan-92",
            ["approval-dataowner-92", "approval-operations-92", "approval-security-92"],
            timestamp, explicitlyRequested: true);
        ProductionActivationGate[] gates = Enum.GetValues<ProductionActivationGateType>()
            .Select(type => new ProductionActivationGate(type,
                ProductionActivationGateStatus.Satisfied, GateEvidence(type),
                "reviewer-92", timestamp.AddMinutes(-1))).ToArray();
        var backup = new BackupVerificationResult("backup-reference-92",
            BackupEvidenceStatus.Verified, RestoreTestStatus.Passed,
            timestamp.AddMinutes(-1));
        ProductionActivationStopCondition[] stops = Enum
            .GetValues<ProductionActivationStopConditionType>().Select(type =>
                new ProductionActivationStopCondition(type, false,
                    $"preparation-stop-{type.ToString().ToLowerInvariant()}")).ToArray();
        return new ProductionActivationReadinessCoordinator().Evaluate(context, gates,
            backup, Rollback(), stops);
    }

    public IControlledPilotOperationalWorkflowObserver[] Observers(
        PilotValidationWorkflow? differenceWorkflow = null,
        IEnumerable<PilotValidationWorkflow>? workflows = null)
    {
        PilotValidationWorkflow[] selected = (workflows ??
            Enum.GetValues<PilotValidationWorkflow>()).ToArray();
        AuthenticationOperationalObservation authentication = Authentication();
        AuthenticationOperationalObservation targetAuthentication = Authentication(
            boundary: OperationalObservationBoundary.TargetReadOnly);
        ProtectedSettingsOperationalObservation settings = Settings();
        ProtectedSettingsOperationalObservation targetSettings = Settings(
            boundary: OperationalObservationBoundary.TargetReadOnly);
        ExportOperationalObservation export = Export();
        ExportOperationalObservation targetExport = Export(
            boundary: OperationalObservationBoundary.TargetReadOnly);
        ReportingOperationalObservation targetReporting = Reporting(StationId, "1405-06",
            UnitCount, boundary: OperationalObservationBoundary.TargetReadOnly);
        RuntimeEventOperationalObservation legacyRuntime = RuntimeWithBoundary(RuntimeObservation,
            OperationalObservationBoundary.LegacyAuthoritative);
        return selected.Select<PilotValidationWorkflow,
            IControlledPilotOperationalWorkflowObserver>(workflow => workflow switch
        {
            PilotValidationWorkflow.Authentication => new AuthenticationOperationalObserver(
                authentication,
                differenceWorkflow == workflow ? Authentication(changed: true,
                    boundary: OperationalObservationBoundary.TargetReadOnly) : targetAuthentication,
                new AuthenticationFingerprintSpecification(), "auth-observation-evidence"),
            PilotValidationWorkflow.Reporting => new ReportingOperationalObserver(
                ReportingObservation,
                differenceWorkflow == workflow ? Reporting(StationId, "1405-06", UnitCount,
                    changed: true, boundary: OperationalObservationBoundary.TargetReadOnly) :
                    targetReporting,
                new ReportingFingerprintSpecification(), "report-observation-evidence"),
            PilotValidationWorkflow.RuntimeEvent => new RuntimeEventOperationalObserver(
                legacyRuntime,
                differenceWorkflow == workflow ? ChangedRuntime(RuntimeObservation) :
                    RuntimeObservation,
                new RuntimeEventFingerprintSpecification(), "runtime-observation-evidence"),
            PilotValidationWorkflow.ProtectedSettings => new ProtectedSettingsOperationalObserver(
                settings, differenceWorkflow == workflow ? Settings(changed: true,
                    boundary: OperationalObservationBoundary.TargetReadOnly) : targetSettings,
                new ProtectedSettingsFingerprintSpecification(), "settings-observation-evidence"),
            PilotValidationWorkflow.Export => new ExportOperationalObserver(
                export, differenceWorkflow == workflow ? Export(changed: true,
                    boundary: OperationalObservationBoundary.TargetReadOnly) : targetExport,
                new ExportFingerprintSpecification(), "export-observation-evidence"),
            _ => throw new ArgumentOutOfRangeException(nameof(workflow))
        }).ToArray();
    }

    public ControlledPilotOperationalRehearsalCoordinator Coordinator(
        PilotValidationWorkflow? differenceWorkflow = null,
        int allowedDifferences = 0,
        IEnumerable<IControlledPilotOperationalWorkflowObserver>? observers = null,
        IControlledPilotOperationalEvidenceDestination? destination = null,
        OperationalReleaseEvidence? release = null,
        ControlledPilotPrerequisiteEvidence? prerequisite = null,
        RollbackVerificationResult? rollback = null,
        IEnumerable<PilotValidationWorkflow>? workflows = null)
    {
        PilotValidationWorkflow[] selected = (workflows ??
            Enum.GetValues<PilotValidationWorkflow>()).ToArray();
        return new(Context(selected), release ?? Release(), Preparation(),
            prerequisite ?? Prerequisite(), rollback ?? Rollback(),
            observers ?? Observers(differenceWorkflow, selected),
            destination ?? new InMemoryControlledPilotOperationalEvidenceDestination(),
            allowedDifferences);
    }

    private AuthenticationOperationalObservation Authentication(
        bool changed = false,
        OperationalObservationBoundary boundary =
            OperationalObservationBoundary.LegacyAuthoritative) => new(
        StationId, true, identifiesShiftProfile: true, acceptsPersonnelNumber: true,
        enforcesStationScope: !changed,
        ["authentication.observe", "shift-profile.identify", "station-scope.evaluate"],
        boundary);

    private ProtectedSettingsOperationalObservation Settings(
        bool changed = false,
        OperationalObservationBoundary boundary =
            OperationalObservationBoundary.LegacyAuthoritative) => new(
        StationId, "protected-active", changed ? 2.25m : 2.00m,
        "esd-effective-revision-7", managementProtectionRequired: true,
        externalVendorAuthorizationRequired: true, boundary);

    private ExportOperationalObservation Export(
        bool changed = false,
        OperationalObservationBoundary boundary =
            OperationalObservationBoundary.LegacyAuthoritative) =>
        ExportOperationalObservationFactory.Create("snapshot-1405-06",
            "pdf-renderer-v1", changed ? $"{StationId}_1405-07.pdf" :
                $"{StationId}_1405-06.pdf", new string('A', 64), "pdf", boundary);

    private static RuntimeEventOperationalObservation Runtime(
        string stationId,
        IEnumerable<RuntimeCalculationContext> contexts) =>
        new TargetRuntimeEventOperationalObservationSource(new RuntimeCalculator()).Observe(
            stationId, PeriodStart, PeriodEnd, contexts);

    private static RuntimeEventOperationalObservation ChangedRuntime(
        RuntimeEventOperationalObservation source)
    {
        RuntimeUnitOperationalObservation first = source.Units[0];
        var changed = new RuntimeUnitOperationalObservation(first.UnitId,
            first.AuthoritativeEvents, first.PhysicalRuntimeMinutes + 1,
            first.EsdAdjustmentMinutes, first.AdjustedRuntimeMinutes + 1,
            first.RuntimeAfterOhMinutes, first.State, first.ServiceDayCount,
            first.LongestRunMinutes, first.CumulativeRuntimeMinutes + 1,
            first.TrustedBaselineReference);
        return new RuntimeEventOperationalObservation(source.StationId,
            source.PeriodStartMinute, source.PeriodEndMinute,
            new[] { changed }.Concat(source.Units.Skip(1)), source.Boundary);
    }

    private static RuntimeEventOperationalObservation RuntimeWithBoundary(
        RuntimeEventOperationalObservation source,
        OperationalObservationBoundary boundary) => new(source.StationId,
            source.PeriodStartMinute, source.PeriodEndMinute, source.Units, boundary);

    private static ReportingOperationalObservation Reporting(
        string stationId,
        string period,
        int units,
        bool changed = false,
        OperationalObservationBoundary boundary =
            OperationalObservationBoundary.LegacyAuthoritative) => new(stationId, period,
        PeriodStart, PeriodEnd,
        [
            new("pressure", "minimum", changed ? 4.9m : 5.0m, 24),
            new("pressure", "maximum", 8.5m, 24),
            new("pressure", "average", 6.75m, 24),
            new("daily-gas", "sum", units * 100m, 2)
        ],
        [
            new("pressure-series", "14050601-01", 5.0m),
            new("pressure-series", "14050601-03", 5.5m)
        ],
        [
            new("14050601", "complete", 12, 12),
            new("14050602", "complete", 12, 12)
        ],
        [], "snapshot-1405-06", new string('B', 64), boundary);

    private static RuntimeCalculationContext Context(
        string unitId,
        UnitOperationalState initial,
        UnitOperationalState final,
        params NormalizedEvent[] events) => new(
            ValidatedEventChain.Valid(events.FirstOrDefault()?.StationId ??
                (unitId.StartsWith("rasht", StringComparison.Ordinal) ? "station-rasht" :
                    "station-ramsar"), unitId, events, initial, final),
            BaselineMinute: 0,
            BaselineState: initial,
            BaselineTotalRuntimeMinutes: 10_000,
            BaselineRuntimeAfterOhMinutes: 500,
            PeriodStartMinute: PeriodStart,
            PeriodEndMinute: PeriodEnd,
            CurrentEsdAdjustmentMinutes: 90,
            EventChainVersion: "event-chain-v1",
            BaselineVersion: "trusted-runtime-baseline-v1",
            PolicyVersion: "runtime-policy-v1",
            CalculationVersion: "runtime-calculation-v1",
            CalculationTimestamp: WindowStart.AddMinutes(-10));

    private static NormalizedEvent EventAt(
        string id,
        string unitId,
        EventType type,
        long minute,
        int sequence)
    {
        string stationId = unitId.StartsWith("rasht", StringComparison.Ordinal)
            ? "station-rasht" : "station-ramsar";
        int date = minute >= 1_440 ? 14050602 : 14050601;
        return new(id, stationId, unitId, type, date,
            checked((int)(minute % 1_440)), minute, sequence, Array.Empty<string>());
    }

    private static string GateEvidence(ProductionActivationGateType type) => type switch
    {
        ProductionActivationGateType.SecurityReview => "approval-security-92",
        ProductionActivationGateType.OperationsReadiness => "approval-operations-92",
        ProductionActivationGateType.DataOwnerApproval => "approval-dataowner-92",
        ProductionActivationGateType.RollbackReadiness => "rollback-evidence-92",
        ProductionActivationGateType.ValidationCompletion => "validation-evidence-92",
        ProductionActivationGateType.DeploymentReadiness => "deployment-evidence-92",
        _ => "gate-evidence-92"
    };
}
