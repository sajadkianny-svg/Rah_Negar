using Rah_Negar.Core.Reporting.Projection;

namespace Rah_Negar.Foundation.Application.Reporting.Input;

public sealed record ReportingInputRequest(
    string StationId,
    long PeriodStartMinute,
    long PeriodEndMinute,
    IReadOnlyList<string> UnitIds);

public enum ReportingInputFailureKind
{
    MissingSource,
    IncompatibleVersion,
    WrongStation,
    WrongPeriod,
    MissingUnit
}

public sealed record ReportingInputFailure(
    ReportingInputFailureKind Kind,
    string Code,
    string Message,
    string Source,
    string? UnitId = null);

public sealed class ReportingAdapterResult<T>
{
    private ReportingAdapterResult(T? value, ReportingInputFailure? failure)
    {
        Value = value;
        Failure = failure;
    }

    public bool IsSuccess => Failure is null;
    public T? Value { get; }
    public ReportingInputFailure? Failure { get; }

    public static ReportingAdapterResult<T> Success(T value) =>
        new(value ?? throw new ArgumentNullException(nameof(value)), null);

    public static ReportingAdapterResult<T> Failed(ReportingInputFailure failure) =>
        new(default, failure ?? throw new ArgumentNullException(nameof(failure)));
}

public sealed record HourlyDataReportingOutput(
    string StationId,
    long PeriodStartMinute,
    long PeriodEndMinute,
    string SourceIdentity,
    string SourceRevision,
    IReadOnlyList<NormalizedHourlyValue> Values,
    CompletenessDimensionResult Completeness);

public sealed record DailyDataReportingOutput(
    string StationId,
    long PeriodStartMinute,
    long PeriodEndMinute,
    string SourceIdentity,
    string SourceRevision,
    IReadOnlyList<NormalizedDailyValue> Values,
    CompletenessDimensionResult Completeness);

public sealed record EventProjectionReportingOutput(
    string StationId,
    string UnitId,
    long PeriodStartMinute,
    long PeriodEndMinute,
    string SourceIdentity,
    string SourceRevision,
    string EventChainVersion,
    string EventPolicyVersion,
    bool IsValidated,
    IReadOnlyList<ReportEvent> Events);

public sealed record RuntimeProjectionReportingOutput(
    string StationId,
    string UnitId,
    long PeriodStartMinute,
    long PeriodEndMinute,
    string SourceIdentity,
    string SourceRevision,
    long PhysicalRuntimeMinutes,
    long EsdAdjustmentMinutes,
    long AdjustedRuntimeMinutes,
    long RuntimeAfterOhMinutes,
    long LongestRunMinutes,
    int ServiceDayCount,
    Core.Runtime.UnitOperationalState FinalState,
    string RuntimeCalculationVersion,
    string RuntimePolicyVersion,
    string BaselineVersion,
    string ConfigurationVersion);

public sealed record StationProfileReportingOutput(
    string StationId,
    string StationName,
    string SourceIdentity,
    string SourceRevision,
    long DataStartMinute,
    string CalendarIdentity,
    string OrderingConvention,
    string ReportCalculationVersion,
    string ReportPolicyVersion,
    string ReportProfileVersion,
    string SnapshotFormatVersion,
    string CalendarPolicyVersion,
    IReadOnlyList<string> UnitIds,
    IReadOnlyList<ReportParameter> Parameters);

public interface IHourlyDataReportingAdapter
{
    Task<ReportingAdapterResult<HourlyDataReportingOutput>> ReadAsync(
        ReportingInputRequest request, CancellationToken cancellationToken = default);
}

public interface IDailyDataReportingAdapter
{
    Task<ReportingAdapterResult<DailyDataReportingOutput>> ReadAsync(
        ReportingInputRequest request, CancellationToken cancellationToken = default);
}

public interface IEventProjectionReportingAdapter
{
    Task<ReportingAdapterResult<IReadOnlyList<EventProjectionReportingOutput>>> ReadAsync(
        ReportingInputRequest request, CancellationToken cancellationToken = default);
}

public interface IRuntimeProjectionReportingAdapter
{
    Task<ReportingAdapterResult<IReadOnlyList<RuntimeProjectionReportingOutput>>> ReadAsync(
        ReportingInputRequest request, CancellationToken cancellationToken = default);
}

public interface IStationProfileReportingAdapter
{
    Task<ReportingAdapterResult<StationProfileReportingOutput>> ReadAsync(
        ReportingInputRequest request, CancellationToken cancellationToken = default);
}
