using Rah_Negar.Core.Reporting.Projection;

namespace Rah_Negar.Core.Reporting.Snapshot;

public sealed class ReportSnapshotIdentity
{
    public ReportSnapshotIdentity(string snapshotId, string reportId, string stationId,
        long periodStartMinute, long periodEndMinute, ReportPeriodKind periodKind,
        IEnumerable<string> unitIds, int snapshotSequence, string? supersedesSnapshotId = null)
    {
        SnapshotId = Required(snapshotId, nameof(snapshotId));
        ReportId = Required(reportId, nameof(reportId));
        StationId = Required(stationId, nameof(stationId));
        if (periodStartMinute >= periodEndMinute)
            throw new ArgumentException("The snapshot period must be a non-empty half-open interval.");
        if (snapshotSequence < 1)
            throw new ArgumentOutOfRangeException(nameof(snapshotSequence), "Snapshot sequence must be positive.");
        if (snapshotSequence == 1 && !string.IsNullOrWhiteSpace(supersedesSnapshotId))
            throw new ArgumentException("An original snapshot cannot supersede another snapshot.", nameof(supersedesSnapshotId));
        if (snapshotSequence > 1 && string.IsNullOrWhiteSpace(supersedesSnapshotId))
            throw new ArgumentException("A correction snapshot requires a superseded snapshot identity.", nameof(supersedesSnapshotId));

        string[] units = unitIds?.Select(x => Required(x, "unitId"))
            .OrderBy(x => x, StringComparer.Ordinal).ToArray()
            ?? throw new ArgumentNullException(nameof(unitIds));
        if (units.Length == 0 || units.Distinct(StringComparer.Ordinal).Count() != units.Length)
            throw new ArgumentException("Snapshot Units must be non-empty and unique.", nameof(unitIds));

        PeriodStartMinute = periodStartMinute;
        PeriodEndMinute = periodEndMinute;
        PeriodKind = periodKind;
        UnitIds = Array.AsReadOnly(units);
        SnapshotSequence = snapshotSequence;
        SupersedesSnapshotId = string.IsNullOrWhiteSpace(supersedesSnapshotId) ? null : supersedesSnapshotId.Trim();
    }

    public string SnapshotId { get; }
    public string ReportId { get; }
    public string StationId { get; }
    public long PeriodStartMinute { get; }
    public long PeriodEndMinute { get; }
    public ReportPeriodKind PeriodKind { get; }
    public IReadOnlyList<string> UnitIds { get; }
    public int SnapshotSequence { get; }
    public string? SupersedesSnapshotId { get; }

    private static string Required(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", name) : value.Trim();
}
