using Rah_Negar.Core.Reporting.Projection;

namespace Rah_Negar.Foundation.Application.Reporting.Input;

/// <summary>
/// Coordinates reporting adapters and normalizes their outputs. This class owns no persistence,
/// clock, UI, or production registration concern.
/// </summary>
public sealed class ReportInputComposer : IReportInputComposer
{
    private readonly IHourlyDataReportingAdapter _hourly;
    private readonly IDailyDataReportingAdapter _daily;
    private readonly IEventProjectionReportingAdapter _events;
    private readonly IRuntimeProjectionReportingAdapter _runtimes;
    private readonly IStationProfileReportingAdapter _profile;

    public ReportInputComposer(IHourlyDataReportingAdapter hourly, IDailyDataReportingAdapter daily,
        IEventProjectionReportingAdapter events, IRuntimeProjectionReportingAdapter runtimes,
        IStationProfileReportingAdapter profile)
    {
        _hourly = hourly ?? throw new ArgumentNullException(nameof(hourly));
        _daily = daily ?? throw new ArgumentNullException(nameof(daily));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _runtimes = runtimes ?? throw new ArgumentNullException(nameof(runtimes));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
    }

    public async Task<ReportInputCompositionResult> ComposeAsync(
        ReportInputCompositionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Identity);
        var adapterRequest = new ReportingInputRequest(request.Identity.StationId,
            request.Identity.PeriodStartMinute, request.Identity.PeriodEndMinute, request.Identity.UnitIds);

        Task<ReportingAdapterResult<HourlyDataReportingOutput>> hourlyTask = _hourly.ReadAsync(adapterRequest, cancellationToken);
        Task<ReportingAdapterResult<DailyDataReportingOutput>> dailyTask = _daily.ReadAsync(adapterRequest, cancellationToken);
        Task<ReportingAdapterResult<IReadOnlyList<EventProjectionReportingOutput>>> eventTask = _events.ReadAsync(adapterRequest, cancellationToken);
        Task<ReportingAdapterResult<IReadOnlyList<RuntimeProjectionReportingOutput>>> runtimeTask = _runtimes.ReadAsync(adapterRequest, cancellationToken);
        Task<ReportingAdapterResult<StationProfileReportingOutput>> profileTask = _profile.ReadAsync(adapterRequest, cancellationToken);

        await Task.WhenAll(hourlyTask, dailyTask, eventTask, runtimeTask, profileTask).ConfigureAwait(false);
        ReportingAdapterResult<HourlyDataReportingOutput> hourly = await hourlyTask.ConfigureAwait(false);
        ReportingAdapterResult<DailyDataReportingOutput> daily = await dailyTask.ConfigureAwait(false);
        ReportingAdapterResult<IReadOnlyList<EventProjectionReportingOutput>> events = await eventTask.ConfigureAwait(false);
        ReportingAdapterResult<IReadOnlyList<RuntimeProjectionReportingOutput>> runtimes = await runtimeTask.ConfigureAwait(false);
        ReportingAdapterResult<StationProfileReportingOutput> profile = await profileTask.ConfigureAwait(false);

        ReportingInputFailure[] sourceFailures = new[] { hourly.Failure, daily.Failure, events.Failure, runtimes.Failure, profile.Failure }
            .Where(x => x is not null).Cast<ReportingInputFailure>().ToArray();
        if (sourceFailures.Length != 0) return ReportInputCompositionResult.Failed(sourceFailures);

        HourlyDataReportingOutput hourlyValue = hourly.Value!;
        DailyDataReportingOutput dailyValue = daily.Value!;
        EventProjectionReportingOutput[] eventValues = events.Value!.ToArray();
        RuntimeProjectionReportingOutput[] runtimeValues = runtimes.Value!.ToArray();
        StationProfileReportingOutput profileValue = profile.Value!;

        ReportingInputFailure[] validationFailures = Validate(request.Identity, hourlyValue, dailyValue,
            eventValues, runtimeValues, profileValue).ToArray();
        if (validationFailures.Length != 0) return ReportInputCompositionResult.Failed(validationFailures);

        EventProjectionReportingOutput[] orderedEvents = eventValues.OrderBy(x => x.UnitId, StringComparer.Ordinal).ToArray();
        RuntimeProjectionReportingOutput[] orderedRuntimes = runtimeValues.OrderBy(x => x.UnitId, StringComparer.Ordinal).ToArray();
        string eventPolicyVersion = orderedEvents.Select(x => x.EventPolicyVersion).Distinct(StringComparer.Ordinal).Single();
        string runtimeCalculationVersion = orderedRuntimes.Select(x => x.RuntimeCalculationVersion).Distinct(StringComparer.Ordinal).Single();
        string runtimePolicyVersion = orderedRuntimes.Select(x => x.RuntimePolicyVersion).Distinct(StringComparer.Ordinal).Single();

        var versions = new ReportVersionSet(profileValue.ReportCalculationVersion, profileValue.ReportPolicyVersion,
            profileValue.ReportProfileVersion, profileValue.SnapshotFormatVersion, eventPolicyVersion,
            runtimeCalculationVersion, runtimePolicyVersion, profileValue.CalendarPolicyVersion,
            orderedEvents.ToDictionary(x => x.UnitId, x => x.EventChainVersion, StringComparer.Ordinal),
            orderedRuntimes.ToDictionary(x => x.UnitId, x => x.BaselineVersion, StringComparer.Ordinal),
            orderedRuntimes.ToDictionary(x => x.UnitId, x => x.ConfigurationVersion, StringComparer.Ordinal));

        var evidence = new ReportEvidence(profileValue.SourceRevision, hourlyValue.SourceRevision,
            hourlyValue.Values.Count, dailyValue.SourceRevision, dailyValue.Values.Count,
            profileValue.SourceIdentity, profileValue.DataStartMinute, profileValue.CalendarIdentity,
            profileValue.OrderingConvention);
        var completeness = new ReportCompletenessResult([
            hourlyValue.Completeness,
            dailyValue.Completeness,
            new CompletenessDimensionResult(CompletenessDimension.EventChain,
                orderedEvents.All(x => x.IsValidated) ? CompletenessState.Complete : CompletenessState.Invalid,
                EventIssues(orderedEvents)),
            new CompletenessDimensionResult(CompletenessDimension.RuntimeInputs, CompletenessState.Complete)
        ]);

        var normalizedEvents = orderedEvents.Select(x => new AuthoritativeEventInput(x.SourceIdentity,
            x.EventChainVersion, x.StationId, x.UnitId, x.PeriodStartMinute, x.PeriodEndMinute,
            x.IsValidated, Array.AsReadOnly(x.Events.OrderBy(e => e.EventMinute).ThenBy(e => e.SourceOrdinal)
                .ThenBy(e => e.EventId, StringComparer.Ordinal).ToArray())));
        var normalizedRuntimes = orderedRuntimes.Select(x => new AuthoritativeRuntimeInput(x.SourceIdentity,
            x.StationId, x.UnitId, x.PeriodStartMinute, x.PeriodEndMinute, x.PhysicalRuntimeMinutes,
            x.EsdAdjustmentMinutes, x.AdjustedRuntimeMinutes, x.RuntimeAfterOhMinutes, x.LongestRunMinutes,
            x.ServiceDayCount, x.FinalState, x.RuntimeCalculationVersion, x.RuntimePolicyVersion,
            x.BaselineVersion, x.ConfigurationVersion));
        var input = new NormalizedReportInput(request.Identity, request.CalculationTimestamp,
            profileValue.Parameters.OrderBy(x => x.SortOrder).ThenBy(x => x.ParameterId, StringComparer.Ordinal),
            hourlyValue.Values.OrderBy(x => x.ObservationMinute).ThenBy(x => x.ParameterId, StringComparer.Ordinal)
                .ThenBy(x => x.RecordId, StringComparer.Ordinal),
            dailyValue.Values.OrderBy(x => x.PersianDate, StringComparer.Ordinal).ThenBy(x => x.ParameterId, StringComparer.Ordinal)
                .ThenBy(x => x.RecordId, StringComparer.Ordinal),
            normalizedEvents, normalizedRuntimes, completeness, evidence, versions);
        return ReportInputCompositionResult.Success(input);
    }

    private static IEnumerable<ReportingInputFailure> Validate(ReportIdentity identity,
        HourlyDataReportingOutput hourly, DailyDataReportingOutput daily,
        IReadOnlyList<EventProjectionReportingOutput> events,
        IReadOnlyList<RuntimeProjectionReportingOutput> runtimes,
        StationProfileReportingOutput profile)
    {
        foreach ((string source, string station) in new[]
        {
            ("daily", daily.StationId), ("hourly", hourly.StationId), ("station-profile", profile.StationId)
        })
            if (!StringComparer.Ordinal.Equals(identity.StationId, station)) yield return WrongStation(source);
        foreach (EventProjectionReportingOutput value in events)
            if (!StringComparer.Ordinal.Equals(identity.StationId, value.StationId)) yield return WrongStation("event", value.UnitId);
        foreach (RuntimeProjectionReportingOutput value in runtimes)
            if (!StringComparer.Ordinal.Equals(identity.StationId, value.StationId)) yield return WrongStation("runtime", value.UnitId);

        if (!PeriodMatches(identity, hourly.PeriodStartMinute, hourly.PeriodEndMinute)) yield return WrongPeriod("hourly");
        if (!PeriodMatches(identity, daily.PeriodStartMinute, daily.PeriodEndMinute)) yield return WrongPeriod("daily");
        foreach (EventProjectionReportingOutput value in events)
            if (!PeriodMatches(identity, value.PeriodStartMinute, value.PeriodEndMinute)) yield return WrongPeriod("event", value.UnitId);
        foreach (RuntimeProjectionReportingOutput value in runtimes)
            if (!PeriodMatches(identity, value.PeriodStartMinute, value.PeriodEndMinute)) yield return WrongPeriod("runtime", value.UnitId);

        foreach (string unit in identity.UnitIds)
        {
            if (events.Count(x => x.UnitId == unit) != 1) yield return MissingUnit("event", unit);
            if (runtimes.Count(x => x.UnitId == unit) != 1) yield return MissingUnit("runtime", unit);
            if (!profile.UnitIds.Contains(unit, StringComparer.Ordinal)) yield return MissingUnit("station-profile", unit);
        }
        foreach (string unit in events.Select(x => x.UnitId).Concat(runtimes.Select(x => x.UnitId))
                     .Where(x => !identity.UnitIds.Contains(x, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal))
            yield return new(ReportingInputFailureKind.MissingUnit, "reporting.input.unit.unexpected",
                "An adapter returned a Unit outside the report identity.", "identity", unit);

        string[] revisions = new[] { hourly.SourceRevision, daily.SourceRevision, profile.SourceRevision }
            .Concat(events.Select(x => x.SourceRevision)).Concat(runtimes.Select(x => x.SourceRevision)).ToArray();
        if (revisions.Any(string.IsNullOrWhiteSpace) || revisions.Distinct(StringComparer.Ordinal).Count() != 1)
            yield return VersionFailure("source", "reporting.input.source-revision.incompatible");
        if (events.Select(x => x.EventPolicyVersion).Any(string.IsNullOrWhiteSpace) ||
            events.Select(x => x.EventPolicyVersion).Distinct(StringComparer.Ordinal).Count() != 1)
            yield return VersionFailure("event", "reporting.input.event-policy-version.incompatible");
        if (runtimes.Select(x => x.RuntimeCalculationVersion).Any(string.IsNullOrWhiteSpace) ||
            runtimes.Select(x => x.RuntimeCalculationVersion).Distinct(StringComparer.Ordinal).Count() != 1 ||
            runtimes.Select(x => x.RuntimePolicyVersion).Any(string.IsNullOrWhiteSpace) ||
            runtimes.Select(x => x.RuntimePolicyVersion).Distinct(StringComparer.Ordinal).Count() != 1)
            yield return VersionFailure("runtime", "reporting.input.runtime-version.incompatible");
        if (hourly.Completeness.Dimension != CompletenessDimension.HourlyData ||
            daily.Completeness.Dimension != CompletenessDimension.DailyData)
            yield return VersionFailure("completeness", "reporting.input.completeness-dimension.incompatible");
    }

    private static IEnumerable<CompletenessIssue> EventIssues(IEnumerable<EventProjectionReportingOutput> events) =>
        events.Where(x => !x.IsValidated).OrderBy(x => x.UnitId, StringComparer.Ordinal)
            .Select(x => new CompletenessIssue("event.chain.invalid", "The authoritative Event chain is invalid.",
                UnitId: x.UnitId, SourceIdentity: x.SourceIdentity));
    private static bool PeriodMatches(ReportIdentity identity, long start, long end) =>
        identity.PeriodStartMinute == start && identity.PeriodEndMinute == end;
    private static ReportingInputFailure WrongStation(string source, string? unit = null) =>
        new(ReportingInputFailureKind.WrongStation, "reporting.input.station.mismatch",
            "Adapter output does not match the requested Station.", source, unit);
    private static ReportingInputFailure WrongPeriod(string source, string? unit = null) =>
        new(ReportingInputFailureKind.WrongPeriod, "reporting.input.period.mismatch",
            "Adapter output does not match the requested half-open period.", source, unit);
    private static ReportingInputFailure MissingUnit(string source, string unit) =>
        new(ReportingInputFailureKind.MissingUnit, "reporting.input.unit.missing",
            "A configured Unit is missing from adapter output.", source, unit);
    private static ReportingInputFailure VersionFailure(string source, string code) =>
        new(ReportingInputFailureKind.IncompatibleVersion, code,
            "Adapter outputs contain missing or incompatible version evidence.", source);
}
