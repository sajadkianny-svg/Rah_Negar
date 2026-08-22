using Rah_Negar.Core.Reporting.Projection;
using Rah_Negar.Core.Reporting.Snapshot;

namespace Rah_Negar.Foundation.Application.Reporting.Finalization;

public sealed class ReportSnapshotFactory : IReportSnapshotFactory
{
    public ReportFinalizationResult Create(ReportFinalizationRequest request,
        FinalizationValidationResult validation)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(validation);
        if (!validation.IsValid) return ReportFinalizationResult.Rejected(validation);

        ReportProjection projection = request.Projection;
        var identity = new ReportSnapshotIdentity(request.SnapshotId, projection.Identity.ReportId,
            projection.Identity.StationId, projection.Identity.PeriodStartMinute,
            projection.Identity.PeriodEndMinute, projection.Identity.PeriodKind,
            projection.Identity.UnitIds, request.SnapshotSequence, request.SupersedesSnapshotId);
        var evidence = new ReportSnapshotEvidence(projection.Evidence, request.VerifiedSourceRevision,
            request.FinalizationId, request.ActorIdentity, projection.CalculationTimestamp,
            request.FinalizedAt, request.FinalizationPolicyVersion, request.SnapshotIntegrityVersion);
        SnapshotChecksum checksum = SnapshotChecksum.Pending(request.SnapshotIntegrityVersion);
        var snapshot = new FinalizedReportSnapshot(identity, projection.Identity, projection.Completeness,
            evidence, projection.Versions, checksum, projection.OperationalSummaries,
            projection.DailySummaries, projection.RuntimeSummaries, projection.EventSummaries,
            projection.EventLog, projection.ServiceSummaries, projection.ExtremeDateSummaries,
            projection.Warnings);
        return ReportFinalizationResult.Success(snapshot);
    }
}
