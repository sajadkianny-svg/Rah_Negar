using Rah_Negar.Core.Event;
using Rah_Negar.Core.Runtime;

namespace Rah_Negar.Core.Reporting.Projection;

public sealed record ReportParameter(string ParameterId, string Label, string Unit,
    ReportAggregationType Aggregation, int SortOrder);
public sealed record NormalizedHourlyValue(string RecordId, string ParameterId, string PersianDate,
    long ObservationMinute, decimal Value);
public sealed record NormalizedDailyValue(string RecordId, string ParameterId, string PersianDate, decimal Value);
public sealed record AuthoritativeEventInput(string ChainId, string ChainVersion, string StationId,
    string UnitId, long PeriodStartMinute, long PeriodEndMinute, bool IsValidated,
    IReadOnlyList<ReportEvent> Events);
public sealed record AuthoritativeRuntimeInput(string ProjectionId, string StationId, string UnitId,
    long PeriodStartMinute, long PeriodEndMinute, long PhysicalRuntimeMinutes,
    long EsdAdjustmentMinutes, long AdjustedRuntimeMinutes, long RuntimeAfterOhMinutes,
    long LongestRunMinutes, int ServiceDayCount, UnitOperationalState FinalState,
    string CalculationVersion, string PolicyVersion, string BaselineVersion, string ConfigurationVersion);

public sealed class NormalizedReportInput
{
    public NormalizedReportInput(ReportIdentity identity, DateTimeOffset calculationTimestamp,
        IEnumerable<ReportParameter> parameters, IEnumerable<NormalizedHourlyValue> hourlyValues,
        IEnumerable<NormalizedDailyValue> dailyValues, IEnumerable<AuthoritativeEventInput> events,
        IEnumerable<AuthoritativeRuntimeInput> runtimes, ReportCompletenessResult completeness,
        ReportEvidence evidence, ReportVersionSet versions)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        CalculationTimestamp = calculationTimestamp;
        Parameters = Copy(parameters);
        HourlyValues = Copy(hourlyValues);
        DailyValues = Copy(dailyValues);
        Events = Copy(events);
        Runtimes = Copy(runtimes);
        Completeness = completeness ?? throw new ArgumentNullException(nameof(completeness));
        Evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        Versions = versions ?? throw new ArgumentNullException(nameof(versions));
    }
    public ReportIdentity Identity { get; }
    public DateTimeOffset CalculationTimestamp { get; }
    public IReadOnlyList<ReportParameter> Parameters { get; }
    public IReadOnlyList<NormalizedHourlyValue> HourlyValues { get; }
    public IReadOnlyList<NormalizedDailyValue> DailyValues { get; }
    public IReadOnlyList<AuthoritativeEventInput> Events { get; }
    public IReadOnlyList<AuthoritativeRuntimeInput> Runtimes { get; }
    public ReportCompletenessResult Completeness { get; }
    public ReportEvidence Evidence { get; }
    public ReportVersionSet Versions { get; }
    private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values) =>
        Array.AsReadOnly((values ?? throw new ArgumentNullException(nameof(values))).ToArray());
}
