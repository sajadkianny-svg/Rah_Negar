using Rah_Negar.Core.Reporting.Projection;

namespace Rah_Negar.Core.Reporting.Snapshot;

public sealed class FinalizedReportSnapshot
{
    public FinalizedReportSnapshot(ReportSnapshotIdentity identity, ReportIdentity reportIdentity,
        ReportCompletenessResult completeness, ReportSnapshotEvidence evidence,
        ReportVersionSet versions, SnapshotChecksum checksum,
        IEnumerable<OperationalSummary> operationalSummaries, IEnumerable<DailySummary> dailySummaries,
        IEnumerable<RuntimeSummary> runtimeSummaries, IEnumerable<EventSummary> eventSummaries,
        IEnumerable<ReportEvent> eventLog, IEnumerable<ServiceSummary> serviceSummaries,
        IEnumerable<ExtremeDateSummary> extremeDateSummaries, IEnumerable<string> warnings)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        ReportIdentity = Copy(reportIdentity ?? throw new ArgumentNullException(nameof(reportIdentity)));
        Completeness = Copy(completeness ?? throw new ArgumentNullException(nameof(completeness)));
        Evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        Versions = Copy(versions ?? throw new ArgumentNullException(nameof(versions)));
        Checksum = checksum ?? throw new ArgumentNullException(nameof(checksum));
        if (Identity.ReportId != ReportIdentity.ReportId || Identity.StationId != ReportIdentity.StationId ||
            Identity.PeriodStartMinute != ReportIdentity.PeriodStartMinute ||
            Identity.PeriodEndMinute != ReportIdentity.PeriodEndMinute ||
            Identity.PeriodKind != ReportIdentity.PeriodKind ||
            !Identity.UnitIds.SequenceEqual(ReportIdentity.UnitIds, StringComparer.Ordinal))
            throw new ArgumentException("Snapshot and report identities must align.");
        if (!Completeness.IsFinalizationEligible)
            throw new ArgumentException("A finalized snapshot requires complete evidence.", nameof(completeness));
        if (Versions.ValidateFor(Identity.UnitIds).Count != 0)
            throw new ArgumentException("A finalized snapshot requires every mandatory version.", nameof(versions));
        if (!StringComparer.Ordinal.Equals(Evidence.VerifiedSourceRevision, Evidence.SourceEvidence.SourceRevision))
            throw new ArgumentException("Verified and captured source revisions must match.", nameof(evidence));
        OperationalSummaries = ReadOnly(operationalSummaries.OrderBy(x => x.ParameterId, StringComparer.Ordinal));
        DailySummaries = ReadOnly(dailySummaries.OrderBy(x => x.ParameterId, StringComparer.Ordinal)
            .Select(x => new DailySummary(x.ParameterId, x.Label, x.Unit, x.Sum, x.ContributingCount,
                ReadOnly(x.MissingDates.OrderBy(d => d, StringComparer.Ordinal)))));
        RuntimeSummaries = ReadOnly(runtimeSummaries.OrderBy(x => x.UnitId, StringComparer.Ordinal));
        EventSummaries = ReadOnly(eventSummaries.OrderBy(x => x.UnitId, StringComparer.Ordinal));
        EventLog = ReadOnly(eventLog.OrderBy(x => x.EventMinute).ThenBy(x => x.UnitId, StringComparer.Ordinal)
            .ThenBy(x => x.SourceOrdinal).ThenBy(x => x.EventId, StringComparer.Ordinal));
        ServiceSummaries = ReadOnly(serviceSummaries.OrderBy(x => x.UnitId, StringComparer.Ordinal));
        ExtremeDateSummaries = ReadOnly(extremeDateSummaries.OrderBy(x => x.ParameterId, StringComparer.Ordinal)
            .Select(x => new ExtremeDateSummary(x.ParameterId, x.Minimum, x.Maximum,
                ReadOnly(x.MinimumDates.OrderBy(d => d, StringComparer.Ordinal)),
                ReadOnly(x.MaximumDates.OrderBy(d => d, StringComparer.Ordinal)))));
        Warnings = ReadOnly(warnings.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal));
    }

    public ReportSnapshotIdentity Identity { get; }
    public ReportIdentity ReportIdentity { get; }
    public ReportCompletenessResult Completeness { get; }
    public ReportSnapshotEvidence Evidence { get; }
    public ReportVersionSet Versions { get; }
    public SnapshotChecksum Checksum { get; }
    public IReadOnlyList<OperationalSummary> OperationalSummaries { get; }
    public IReadOnlyList<DailySummary> DailySummaries { get; }
    public IReadOnlyList<RuntimeSummary> RuntimeSummaries { get; }
    public IReadOnlyList<EventSummary> EventSummaries { get; }
    public IReadOnlyList<ReportEvent> EventLog { get; }
    public IReadOnlyList<ServiceSummary> ServiceSummaries { get; }
    public IReadOnlyList<ExtremeDateSummary> ExtremeDateSummaries { get; }
    public IReadOnlyList<string> Warnings { get; }

    private static ReportIdentity Copy(ReportIdentity value) => new(value.ReportId, value.StationId,
        value.StationName, value.PeriodStartMinute, value.PeriodEndMinute, value.PersianPeriodLabel,
        value.PeriodKind, value.UnitIds, value.SourceMode);
    private static ReportCompletenessResult Copy(ReportCompletenessResult value) => new(value.Dimensions.Select(x =>
        new CompletenessDimensionResult(x.Dimension, x.State, x.Issues.Select(i =>
            new CompletenessIssue(i.Code, i.Message, i.Date, i.UnitId, i.Field, i.SourceIdentity)))));
    private static ReportVersionSet Copy(ReportVersionSet value) => new(value.ReportCalculationVersion,
        value.ReportPolicyVersion, value.ReportProfileVersion, value.SnapshotFormatVersion,
        value.EventPolicyVersion, value.RuntimeCalculationVersion, value.RuntimePolicyVersion,
        value.CalendarPolicyVersion, value.EventChainVersions, value.RuntimeBaselineVersions,
        value.RuntimeConfigurationVersions);
    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values) => Array.AsReadOnly(values.ToArray());
}
