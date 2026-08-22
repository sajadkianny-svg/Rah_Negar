namespace Rah_Negar.Core.Reporting.Projection;

public sealed class ReportProjection
{
    internal ReportProjection(ReportIdentity identity, ReportProjectionStatus status,
        DateTimeOffset calculationTimestamp, ReportCompletenessResult completeness,
        ReportEvidence evidence, ReportVersionSet versions,
        IEnumerable<OperationalSummary> operational, IEnumerable<DailySummary> daily,
        IEnumerable<RuntimeSummary> runtime, IEnumerable<EventSummary> eventSummaries,
        IEnumerable<ReportEvent> eventLog, IEnumerable<ServiceSummary> service,
        IEnumerable<ExtremeDateSummary> extremes, IEnumerable<string> warnings,
        IEnumerable<string> blockingReasons)
    {
        Identity = identity;
        Status = status;
        CalculationTimestamp = calculationTimestamp;
        Completeness = completeness;
        Evidence = evidence;
        Versions = versions;
        OperationalSummaries = Copy(operational);
        DailySummaries = Copy(daily);
        RuntimeSummaries = Copy(runtime);
        EventSummaries = Copy(eventSummaries);
        EventLog = Copy(eventLog);
        ServiceSummaries = Copy(service);
        ExtremeDateSummaries = Copy(extremes);
        Warnings = Copy(warnings);
        BlockingReasons = Copy(blockingReasons);
    }
    public ReportIdentity Identity { get; }
    public ReportProjectionStatus Status { get; }
    public DateTimeOffset CalculationTimestamp { get; }
    public ReportCompletenessResult Completeness { get; }
    public ReportEvidence Evidence { get; }
    public ReportVersionSet Versions { get; }
    public IReadOnlyList<OperationalSummary> OperationalSummaries { get; }
    public IReadOnlyList<DailySummary> DailySummaries { get; }
    public IReadOnlyList<RuntimeSummary> RuntimeSummaries { get; }
    public IReadOnlyList<EventSummary> EventSummaries { get; }
    public IReadOnlyList<ReportEvent> EventLog { get; }
    public IReadOnlyList<ServiceSummary> ServiceSummaries { get; }
    public IReadOnlyList<ExtremeDateSummary> ExtremeDateSummaries { get; }
    public IReadOnlyList<string> Warnings { get; }
    public IReadOnlyList<string> BlockingReasons { get; }
    private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values) => Array.AsReadOnly(values.ToArray());
}

