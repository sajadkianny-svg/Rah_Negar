using System.Collections.ObjectModel;

namespace Rah_Negar.Core.Reporting.Projection;

public enum ReportPeriodKind { Monthly, HalfYear, Yearly, ArbitraryRange }
public enum ReportSourceMode { OpenProjection, FinalizedSnapshot }
public enum ReportProjectionStatus { Complete, Incomplete, Rejected }
public enum CompletenessState { Complete, Incomplete, Invalid, Unavailable }
public enum CompletenessDimension { HourlyData, DailyData, EventChain, RuntimeInputs }
public enum ReportAggregationType { Minimum, Maximum, Average, Sum }

public sealed class ReportIdentity
{
    public ReportIdentity(string reportId, string stationId, string stationName,
        long periodStartMinute, long periodEndMinute, string persianPeriodLabel,
        ReportPeriodKind periodKind, IEnumerable<string> unitIds, ReportSourceMode sourceMode)
    {
        ReportId = Required(reportId, nameof(reportId));
        StationId = Required(stationId, nameof(stationId));
        StationName = Required(stationName, nameof(stationName));
        PersianPeriodLabel = Required(persianPeriodLabel, nameof(persianPeriodLabel));
        if (periodStartMinute >= periodEndMinute)
            throw new ArgumentException("The report period must be a non-empty half-open interval.");
        PeriodStartMinute = periodStartMinute;
        PeriodEndMinute = periodEndMinute;
        PeriodKind = periodKind;
        SourceMode = sourceMode;

        string[] units = unitIds?.Select(RequiredUnit).OrderBy(x => x, StringComparer.Ordinal).ToArray()
            ?? throw new ArgumentNullException(nameof(unitIds));
        if (units.Length == 0) throw new ArgumentException("At least one Unit is required.", nameof(unitIds));
        if (units.Distinct(StringComparer.Ordinal).Count() != units.Length)
            throw new ArgumentException("Duplicate Unit identities are not allowed.", nameof(unitIds));
        UnitIds = Array.AsReadOnly(units);
    }

    public string ReportId { get; }
    public string StationId { get; }
    public string StationName { get; }
    public long PeriodStartMinute { get; }
    public long PeriodEndMinute { get; }
    public string PersianPeriodLabel { get; }
    public ReportPeriodKind PeriodKind { get; }
    public IReadOnlyList<string> UnitIds { get; }
    public ReportSourceMode SourceMode { get; }

    private static string Required(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", name) : value.Trim();
    private static string RequiredUnit(string value) => Required(value, "unitId");
}

public sealed record CompletenessIssue(string Code, string Message, string? Date = null,
    string? UnitId = null, string? Field = null, string? SourceIdentity = null);

public sealed class CompletenessDimensionResult
{
    public CompletenessDimensionResult(CompletenessDimension dimension, CompletenessState state,
        IEnumerable<CompletenessIssue>? issues = null)
    {
        Dimension = dimension;
        State = state;
        Issues = Array.AsReadOnly((issues ?? []).OrderBy(x => x.Date, StringComparer.Ordinal)
            .ThenBy(x => x.UnitId, StringComparer.Ordinal).ThenBy(x => x.Field, StringComparer.Ordinal)
            .ThenBy(x => x.Code, StringComparer.Ordinal).ToArray());
        if (state == CompletenessState.Complete && Issues.Count != 0)
            throw new ArgumentException("A complete dimension cannot contain issues.", nameof(issues));
    }
    public CompletenessDimension Dimension { get; }
    public CompletenessState State { get; }
    public IReadOnlyList<CompletenessIssue> Issues { get; }
}

public sealed class ReportCompletenessResult
{
    public ReportCompletenessResult(IEnumerable<CompletenessDimensionResult> dimensions)
    {
        CompletenessDimensionResult[] values = dimensions?.OrderBy(x => x.Dimension).ToArray()
            ?? throw new ArgumentNullException(nameof(dimensions));
        if (values.Select(x => x.Dimension).Distinct().Count() != values.Length)
            throw new ArgumentException("Each completeness dimension may occur only once.", nameof(dimensions));
        Dimensions = Array.AsReadOnly(values);
    }
    public IReadOnlyList<CompletenessDimensionResult> Dimensions { get; }
    public CompletenessState State => Dimensions.Any(x => x.State == CompletenessState.Unavailable) ? CompletenessState.Unavailable
        : Dimensions.Any(x => x.State == CompletenessState.Invalid) ? CompletenessState.Invalid
        : Dimensions.Any(x => x.State == CompletenessState.Incomplete) ? CompletenessState.Incomplete
        : Dimensions.Count == Enum.GetValues<CompletenessDimension>().Length ? CompletenessState.Complete
        : CompletenessState.Unavailable;
    public bool IsFinalizationEligible => State == CompletenessState.Complete;
}

public sealed class ReportEvidence
{
    public ReportEvidence(string sourceRevision, string hourlyRevision, int hourlyRecordCount,
        string dailyRevision, int dailyRecordCount, string stationProfileIdentity,
        long dataStartMinute, string calendarIdentity, string orderingConvention)
    {
        SourceRevision = sourceRevision;
        HourlyRevision = hourlyRevision;
        HourlyRecordCount = hourlyRecordCount;
        DailyRevision = dailyRevision;
        DailyRecordCount = dailyRecordCount;
        StationProfileIdentity = stationProfileIdentity;
        DataStartMinute = dataStartMinute;
        CalendarIdentity = calendarIdentity;
        OrderingConvention = orderingConvention;
    }
    public string SourceRevision { get; }
    public string HourlyRevision { get; }
    public int HourlyRecordCount { get; }
    public string DailyRevision { get; }
    public int DailyRecordCount { get; }
    public string StationProfileIdentity { get; }
    public long DataStartMinute { get; }
    public string CalendarIdentity { get; }
    public string OrderingConvention { get; }
}

public sealed class ReportVersionSet
{
    public ReportVersionSet(string reportCalculationVersion, string reportPolicyVersion,
        string reportProfileVersion, string snapshotFormatVersion, string eventPolicyVersion,
        string runtimeCalculationVersion, string runtimePolicyVersion, string calendarPolicyVersion,
        IReadOnlyDictionary<string, string> eventChainVersions,
        IReadOnlyDictionary<string, string> runtimeBaselineVersions,
        IReadOnlyDictionary<string, string> runtimeConfigurationVersions)
    {
        ReportCalculationVersion = reportCalculationVersion;
        ReportPolicyVersion = reportPolicyVersion;
        ReportProfileVersion = reportProfileVersion;
        SnapshotFormatVersion = snapshotFormatVersion;
        EventPolicyVersion = eventPolicyVersion;
        RuntimeCalculationVersion = runtimeCalculationVersion;
        RuntimePolicyVersion = runtimePolicyVersion;
        CalendarPolicyVersion = calendarPolicyVersion;
        EventChainVersions = Copy(eventChainVersions);
        RuntimeBaselineVersions = Copy(runtimeBaselineVersions);
        RuntimeConfigurationVersions = Copy(runtimeConfigurationVersions);
    }
    public string ReportCalculationVersion { get; }
    public string ReportPolicyVersion { get; }
    public string ReportProfileVersion { get; }
    public string SnapshotFormatVersion { get; }
    public string EventPolicyVersion { get; }
    public string RuntimeCalculationVersion { get; }
    public string RuntimePolicyVersion { get; }
    public string CalendarPolicyVersion { get; }
    public IReadOnlyDictionary<string, string> EventChainVersions { get; }
    public IReadOnlyDictionary<string, string> RuntimeBaselineVersions { get; }
    public IReadOnlyDictionary<string, string> RuntimeConfigurationVersions { get; }

    public IReadOnlyList<string> ValidateFor(IEnumerable<string> unitIds)
    {
        var errors = new List<string>();
        Check(ReportCalculationVersion, "version.report-calculation.missing", errors);
        Check(ReportPolicyVersion, "version.report-policy.missing", errors);
        Check(ReportProfileVersion, "version.report-profile.missing", errors);
        Check(SnapshotFormatVersion, "version.snapshot-format.missing", errors);
        Check(EventPolicyVersion, "version.event-policy.missing", errors);
        Check(RuntimeCalculationVersion, "version.runtime-calculation.missing", errors);
        Check(RuntimePolicyVersion, "version.runtime-policy.missing", errors);
        Check(CalendarPolicyVersion, "version.calendar-policy.missing", errors);
        foreach (string unit in unitIds.OrderBy(x => x, StringComparer.Ordinal))
        {
            CheckUnit(EventChainVersions, unit, "version.event-chain.missing", errors);
            CheckUnit(RuntimeBaselineVersions, unit, "version.runtime-baseline.missing", errors);
            CheckUnit(RuntimeConfigurationVersions, unit, "version.runtime-configuration.missing", errors);
        }
        return Array.AsReadOnly(errors.ToArray());
    }

    private static ReadOnlyDictionary<string, string> Copy(IReadOnlyDictionary<string, string> source) =>
        new(new Dictionary<string, string>(source ?? throw new ArgumentNullException(nameof(source)), StringComparer.Ordinal));
    private static void Check(string value, string code, ICollection<string> errors) { if (string.IsNullOrWhiteSpace(value)) errors.Add(code); }
    private static void CheckUnit(IReadOnlyDictionary<string, string> values, string unit, string code, ICollection<string> errors)
    { if (!values.TryGetValue(unit, out string? value) || string.IsNullOrWhiteSpace(value)) errors.Add($"{code}:{unit}"); }
}
