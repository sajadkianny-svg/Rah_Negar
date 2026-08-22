using Rah_Negar.Core.Event;

namespace Rah_Negar.Core.Reporting.Projection;

public sealed class ReportCalculator : IReportCalculator
{
    public ReportProjection Calculate(NormalizedReportInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        string[] errors = Validate(input).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (errors.Length != 0) return Projection(input, ReportProjectionStatus.Rejected, blocking: errors);

        ReportProjectionStatus status = input.Completeness.IsFinalizationEligible
            ? ReportProjectionStatus.Complete : ReportProjectionStatus.Incomplete;
        string[] warnings = input.Completeness.Dimensions.SelectMany(x => x.Issues)
            .Select(x => x.Code).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();

        ReportParameter[] parameters = input.Parameters.OrderBy(x => x.SortOrder)
            .ThenBy(x => x.ParameterId, StringComparer.Ordinal).ToArray();
        var operational = parameters.Where(x => x.Aggregation != ReportAggregationType.Sum)
            .Select(p => AggregateHourly(p, input.HourlyValues)).Where(x => x is not null)!;
        var daily = parameters.Where(x => x.Aggregation == ReportAggregationType.Sum)
            .Select(p => AggregateDaily(p, input.DailyValues));
        var runtime = input.Runtimes.OrderBy(x => x.UnitId, StringComparer.Ordinal).Select(x =>
            new RuntimeSummary(x.UnitId, x.PhysicalRuntimeMinutes, x.EsdAdjustmentMinutes,
                x.AdjustedRuntimeMinutes, x.RuntimeAfterOhMinutes, x.LongestRunMinutes,
                x.ServiceDayCount, x.FinalState));
        AuthoritativeEventInput[] eventInputs = input.Events.OrderBy(x => x.UnitId, StringComparer.Ordinal).ToArray();
        var eventSummaries = eventInputs.Select(x => new EventSummary(x.UnitId,
            x.Events.Count(e => e.EventType == EventType.Start), x.Events.Count(e => e.EventType == EventType.Nsd),
            x.Events.Count(e => e.EventType == EventType.Esd), x.Events.Count(e => e.EventType == EventType.Oh)));
        var eventLog = eventInputs.SelectMany(x => x.Events).OrderBy(x => x.EventMinute)
            .ThenBy(x => x.UnitId, StringComparer.Ordinal).ThenBy(x => x.SourceOrdinal)
            .ThenBy(x => x.EventId, StringComparer.Ordinal);
        var service = input.Runtimes.OrderBy(x => x.UnitId, StringComparer.Ordinal)
            .Select(x => new ServiceSummary(x.UnitId, x.ServiceDayCount, x.PhysicalRuntimeMinutes));
        var extremes = parameters.Where(x => x.Aggregation != ReportAggregationType.Sum)
            .Select(p => Extreme(p, input.HourlyValues)).Where(x => x is not null)!;

        return new ReportProjection(input.Identity, status, input.CalculationTimestamp, input.Completeness,
            input.Evidence, input.Versions, operational!, daily, runtime, eventSummaries, eventLog,
            service, extremes!, warnings, Array.Empty<string>());
    }

    private static IEnumerable<string> Validate(NormalizedReportInput input)
    {
        foreach (string versionError in input.Versions.ValidateFor(input.Identity.UnitIds)) yield return versionError;
        if (input.Parameters.GroupBy(x => x.ParameterId, StringComparer.Ordinal).Any(x => x.Count() > 1))
            yield return "input.parameter.duplicate";
        foreach (string unit in input.Identity.UnitIds)
        {
            AuthoritativeEventInput[] events = input.Events.Where(x => x.UnitId == unit).ToArray();
            AuthoritativeRuntimeInput[] runtimes = input.Runtimes.Where(x => x.UnitId == unit).ToArray();
            if (events.Length != 1) yield return $"input.event.count:{unit}";
            if (runtimes.Length != 1) yield return $"input.runtime.count:{unit}";
        }
        foreach (AuthoritativeEventInput value in input.Events)
        {
            if (!Matches(input.Identity, value.StationId, value.PeriodStartMinute, value.PeriodEndMinute) ||
                !input.Identity.UnitIds.Contains(value.UnitId, StringComparer.Ordinal)) yield return $"input.event.identity:{value.UnitId}";
            if (!value.IsValidated) yield return $"input.event.not-validated:{value.UnitId}";
            if (!input.Versions.EventChainVersions.TryGetValue(value.UnitId, out string? version) || version != value.ChainVersion)
                yield return $"input.event.version-mismatch:{value.UnitId}";
        }
        foreach (AuthoritativeRuntimeInput value in input.Runtimes)
        {
            if (!Matches(input.Identity, value.StationId, value.PeriodStartMinute, value.PeriodEndMinute) ||
                !input.Identity.UnitIds.Contains(value.UnitId, StringComparer.Ordinal)) yield return $"input.runtime.identity:{value.UnitId}";
            if (value.AdjustedRuntimeMinutes != value.PhysicalRuntimeMinutes + value.EsdAdjustmentMinutes)
                yield return $"input.runtime.component-invariant:{value.UnitId}";
            if (value.CalculationVersion != input.Versions.RuntimeCalculationVersion || value.PolicyVersion != input.Versions.RuntimePolicyVersion ||
                !MatchesVersion(input.Versions.RuntimeBaselineVersions, value.UnitId, value.BaselineVersion) ||
                !MatchesVersion(input.Versions.RuntimeConfigurationVersions, value.UnitId, value.ConfigurationVersion))
                yield return $"input.runtime.version-mismatch:{value.UnitId}";
        }
    }

    private static bool Matches(ReportIdentity id, string station, long start, long end) =>
        id.StationId == station && id.PeriodStartMinute == start && id.PeriodEndMinute == end;
    private static bool MatchesVersion(IReadOnlyDictionary<string, string> versions, string unit, string value) =>
        versions.TryGetValue(unit, out string? expected) && expected == value;
    private static OperationalSummary? AggregateHourly(ReportParameter p, IEnumerable<NormalizedHourlyValue> source)
    {
        decimal[] values = source.Where(x => x.ParameterId == p.ParameterId).Select(x => x.Value).ToArray();
        if (values.Length == 0) return null;
        decimal result = p.Aggregation switch
        {
            ReportAggregationType.Minimum => values.Min(),
            ReportAggregationType.Maximum => values.Max(),
            ReportAggregationType.Average => values.Average(),
            _ => throw new InvalidOperationException("Hourly parameters support minimum, maximum, or average only.")
        };
        return new(p.ParameterId, p.Label, p.Unit, p.Aggregation, result, values.Length);
    }
    private static DailySummary AggregateDaily(ReportParameter p, IEnumerable<NormalizedDailyValue> source)
    {
        NormalizedDailyValue[] values = source.Where(x => x.ParameterId == p.ParameterId).ToArray();
        return new(p.ParameterId, p.Label, p.Unit, values.Sum(x => x.Value), values.Length, Array.Empty<string>());
    }
    private static ExtremeDateSummary? Extreme(ReportParameter p, IEnumerable<NormalizedHourlyValue> source)
    {
        NormalizedHourlyValue[] values = source.Where(x => x.ParameterId == p.ParameterId).ToArray();
        if (values.Length == 0) return null;
        decimal min = values.Min(x => x.Value), max = values.Max(x => x.Value);
        string[] minDates = values.Where(x => x.Value == min).Select(x => x.PersianDate).Distinct()
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        string[] maxDates = values.Where(x => x.Value == max).Select(x => x.PersianDate).Distinct()
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        return new(p.ParameterId, min, max, Array.AsReadOnly(minDates), Array.AsReadOnly(maxDates));
    }
    private static ReportProjection Projection(NormalizedReportInput input, ReportProjectionStatus status,
        IEnumerable<string> blocking) => new(input.Identity, status, input.CalculationTimestamp,
            input.Completeness, input.Evidence, input.Versions, [], [], [], [], [], [], [], [], blocking);
}
