using Rah_Negar.Core.Reporting.Projection;

namespace Rah_Negar.Core.Reporting.Snapshot;

public sealed class ReportSnapshotEvidence
{
    public ReportSnapshotEvidence(ReportEvidence sourceEvidence, string verifiedSourceRevision,
        string finalizationId, string actorIdentity, DateTimeOffset projectionCalculatedAt,
        DateTimeOffset finalizedAt, string finalizationPolicyVersion, string snapshotIntegrityVersion)
    {
        ArgumentNullException.ThrowIfNull(sourceEvidence);
        SourceEvidence = new ReportEvidence(sourceEvidence.SourceRevision, sourceEvidence.HourlyRevision,
            sourceEvidence.HourlyRecordCount, sourceEvidence.DailyRevision, sourceEvidence.DailyRecordCount,
            sourceEvidence.StationProfileIdentity, sourceEvidence.DataStartMinute,
            sourceEvidence.CalendarIdentity, sourceEvidence.OrderingConvention);
        VerifiedSourceRevision = Required(verifiedSourceRevision, nameof(verifiedSourceRevision));
        FinalizationId = Required(finalizationId, nameof(finalizationId));
        ActorIdentity = Required(actorIdentity, nameof(actorIdentity));
        ProjectionCalculatedAt = projectionCalculatedAt;
        FinalizedAt = finalizedAt;
        FinalizationPolicyVersion = Required(finalizationPolicyVersion, nameof(finalizationPolicyVersion));
        SnapshotIntegrityVersion = Required(snapshotIntegrityVersion, nameof(snapshotIntegrityVersion));
    }

    public ReportEvidence SourceEvidence { get; }
    public string VerifiedSourceRevision { get; }
    public string FinalizationId { get; }
    public string ActorIdentity { get; }
    public DateTimeOffset ProjectionCalculatedAt { get; }
    public DateTimeOffset FinalizedAt { get; }
    public string FinalizationPolicyVersion { get; }
    public string SnapshotIntegrityVersion { get; }

    private static string Required(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", name) : value.Trim();
}
