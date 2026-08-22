using Rah_Negar.Core.Event;
using Rah_Negar.Core.Runtime;

namespace Rah_Negar.Core.Reporting.Projection;

public sealed record OperationalSummary(string ParameterId, string Label, string Unit,
    ReportAggregationType Aggregation, decimal Value, int ContributingCount);
public sealed record DailySummary(string ParameterId, string Label, string Unit,
    decimal Sum, int ContributingCount, IReadOnlyList<string> MissingDates);
public sealed record RuntimeSummary(string UnitId, long PhysicalRuntimeMinutes, long EsdAdjustmentMinutes,
    long AdjustedRuntimeMinutes, long RuntimeAfterOhMinutes, long LongestRunMinutes,
    int ServiceDayCount, UnitOperationalState FinalState);
public sealed record EventSummary(string UnitId, int StartCount, int NsdCount, int EsdCount, int OhCount);
public sealed record ReportEvent(string EventId, string UnitId, EventType EventType,
    long EventMinute, int SourceOrdinal);
public sealed record ServiceSummary(string UnitId, int ServiceDayCount, long PhysicalRuntimeMinutes);
public sealed record ExtremeDateSummary(string ParameterId, decimal Minimum, decimal Maximum,
    IReadOnlyList<string> MinimumDates, IReadOnlyList<string> MaximumDates);

