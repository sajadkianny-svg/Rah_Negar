using System.Globalization;
using Microsoft.Data.Sqlite;
using Rah_Negar.Core.Event;
using Rah_Negar.Core.Event.Comparison;
using Rah_Negar.Core.Reporting.Projection;
using Rah_Negar.Core.Reports;
using Rah_Negar.Core.Runtime;
using Rah_Negar.Core.Runtime.Calculation;
using Rah_Negar.Foundation.Application.Pilot.Live;
using Rah_Negar.Foundation.Application.Pilot.Operational;
using Rah_Negar.Foundation.Application.Reporting.Export;
using Rah_Negar.Infrastructure.Database.Readiness;
using Rah_Negar.Models.Reports;
using Rah_Negar.Services.Reports;
using LegacyAggregation = Rah_Negar.Models.Reports.ReportAggregationType;
using TargetAggregation = Rah_Negar.Core.Reporting.Projection.ReportAggregationType;

namespace Rah_Negar.Infrastructure.Pilot;

/// <summary>
/// Concrete bridge from the current Rasht/Ramsar read models to the Phase 9.2 observation
/// contracts. Every database handle comes from the dedicated read-only factory.
/// </summary>
public sealed class LiveSqlitePilotReadModels :
    ILiveAuthenticationPilotReadModel,
    ILiveReportingPilotReadModel,
    ILiveRuntimeEventPilotReadModel,
    ILiveProtectedSettingsPilotReadModel,
    ILiveExportPilotReadModel
{
    private readonly IPilotReadOnlySqliteConnectionFactory _connections;
    private readonly LivePilotReadScope _scope;
    private readonly RuntimeCalculator _runtimeCalculator;
    private readonly ReportCalculator _reportCalculator;
    private readonly DeterministicReportFileNamePolicy _fileNames;
    private readonly TimeProvider _timeProvider;

    public LiveSqlitePilotReadModels(
        IPilotReadOnlySqliteConnectionFactory connections,
        LivePilotReadScope scope,
        RuntimeCalculator runtimeCalculator,
        ReportCalculator reportCalculator,
        DeterministicReportFileNamePolicy fileNames,
        TimeProvider? timeProvider = null)
    {
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _runtimeCalculator = runtimeCalculator ?? throw new ArgumentNullException(nameof(runtimeCalculator));
        _reportCalculator = reportCalculator ?? throw new ArgumentNullException(nameof(reportCalculator));
        _fileNames = fileNames ?? throw new ArgumentNullException(nameof(fileNames));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    async ValueTask<LivePilotObservationPair<AuthenticationOperationalObservation>>
        ILiveAuthenticationPilotReadModel.ReadAsync(CancellationToken cancellationToken)
    {
        await using SqliteConnection connection =
            await _connections.OpenReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand legacyCommand = connection.CreateCommand();
        legacyCommand.CommandText = """
            SELECT is_initialized,
                   CASE WHEN length(user_reset_password_hash) > 0
                          AND length(user_reset_password_salt) > 0 THEN 1 ELSE 0 END
            FROM app_settings ORDER BY id LIMIT 1;
            """;
        await using SqliteDataReader legacyReader =
            await legacyCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        bool legacyAvailable = await legacyReader.ReadAsync(cancellationToken).ConfigureAwait(false) &&
            legacyReader.GetInt64(0) == 1 && legacyReader.GetInt64(1) == 1;
        await legacyReader.DisposeAsync().ConfigureAwait(false);

        bool targetTables = await TablesExistAsync(connection,
            ["SecurityShiftProfiles", "SecurityShiftProfileCredentials"], cancellationToken)
            .ConfigureAwait(false);
        bool targetAvailable = false;
        if (targetTables)
        {
            await using SqliteCommand targetCommand = connection.CreateCommand();
            targetCommand.CommandText = """
                SELECT EXISTS(
                    SELECT 1
                    FROM SecurityShiftProfiles p
                    JOIN SecurityShiftProfileCredentials c
                      ON c.ShiftProfileId = p.ShiftProfileId
                    WHERE p.StationId = $station AND p.IsActive = 1 AND c.IsCurrent = 1);
                """;
            targetCommand.Parameters.AddWithValue("$station", _scope.StationId);
            targetAvailable = Convert.ToInt64(await targetCommand.ExecuteScalarAsync(
                cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture) == 1;
        }

        var legacy = new AuthenticationOperationalObservation(_scope.StationId,
            legacyAvailable, identifiesShiftProfile: false, acceptsPersonnelNumber: false,
            enforcesStationScope: true,
            ["legacy-password-capability", "legacy-session-observation", "station-scope"],
            OperationalObservationBoundary.LegacyAuthoritative);
        var target = new AuthenticationOperationalObservation(_scope.StationId,
            targetAvailable, identifiesShiftProfile: targetTables,
            acceptsPersonnelNumber: targetTables, enforcesStationScope: true,
            ["shift-profile-capability", "personnel-number-capability", "station-scope"],
            OperationalObservationBoundary.TargetReadOnly);
        return new(legacy, target, "live-authentication-evidence");
    }

    async ValueTask<LivePilotObservationPair<ReportingOperationalObservation>>
        ILiveReportingPilotReadModel.ReadAsync(CancellationToken cancellationToken) =>
        await ReadReportingAsync(cancellationToken).ConfigureAwait(false);

    async ValueTask<LivePilotObservationPair<RuntimeEventOperationalObservation>>
        ILiveRuntimeEventPilotReadModel.ReadAsync(CancellationToken cancellationToken)
    {
        await using SqliteConnection connection =
            await _connections.OpenReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        return BuildRuntimePair(connection, cancellationToken);
    }

    async ValueTask<LivePilotObservationPair<ProtectedSettingsOperationalObservation>>
        ILiveProtectedSettingsPilotReadModel.ReadAsync(CancellationToken cancellationToken)
    {
        await using SqliteConnection connection =
            await _connections.OpenReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        (bool enabled, decimal hours) = await ReadEsdSettingsAsync(
            connection, cancellationToken).ConfigureAwait(false);
        string state = enabled ? "enabled" : "disabled";
        var legacy = new ProtectedSettingsOperationalObservation(_scope.StationId,
            state, hours, "legacy-app-settings-v1", managementProtectionRequired: true,
            externalVendorAuthorizationRequired: false,
            OperationalObservationBoundary.LegacyAuthoritative);
        var target = new ProtectedSettingsOperationalObservation(_scope.StationId,
            state, hours, "legacy-app-settings-v1", managementProtectionRequired: true,
            externalVendorAuthorizationRequired: true,
            OperationalObservationBoundary.TargetReadOnly);
        return new(legacy, target, "live-protected-settings-evidence");
    }

    async ValueTask<LivePilotObservationPair<ExportOperationalObservation>>
        ILiveExportPilotReadModel.ReadAsync(CancellationToken cancellationToken)
    {
        LivePilotObservationPair<ReportingOperationalObservation> reports =
            await ReadReportingAsync(cancellationToken).ConfigureAwait(false);
        var specification = new ReportingFingerprintSpecification();
        string legacyChecksum = specification.CreateFingerprint(reports.Legacy);
        string targetChecksum = specification.CreateFingerprint(reports.Target);
        string snapshotId = $"live-report-{_scope.StationId}-{_scope.PeriodIdentity}";
        string stationName = _scope.StationName.Replace(' ', '-');
        string legacyFileName =
            $"Monthly_Final_Report_{stationName}_{_scope.PeriodIdentity.Replace('-', '_')}.pdf";
        string targetFileName = _fileNames.Create(_scope.StationId,
            _scope.PeriodIdentity, ReportPeriodKind.Monthly, "snapshot-v1",
            ReportExportFormat.Pdf);
        ExportOperationalObservation legacy = ExportOperationalObservationFactory.Create(
            snapshotId, "pdf-renderer", legacyFileName, legacyChecksum, "pdf",
            OperationalObservationBoundary.LegacyAuthoritative);
        ExportOperationalObservation target = ExportOperationalObservationFactory.Create(
            snapshotId, "pdf-renderer", targetFileName, targetChecksum, "pdf",
            OperationalObservationBoundary.TargetReadOnly);
        return new(legacy, target, "live-export-metadata-evidence");
    }

    public bool OpensReadOnlyConnections => true;
    public bool ExposesConnectionString => false;
    public bool InsertsRows => false;
    public bool UpdatesRows => false;
    public bool DeletesRows => false;
    public bool OpensTransactions => false;
    public bool RunsMigrations => false;
    public bool FinalizesReports => false;
    public bool GeneratesArtifacts => false;

    private async ValueTask<LivePilotObservationPair<ReportingOperationalObservation>>
        ReadReportingAsync(CancellationToken cancellationToken)
    {
        await using SqliteConnection connection =
            await _connections.OpenReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        ReportStationProfile profile = ReportStationProfileProvider.GetProfile(_scope.StationName);
        var request = new ReportRequest
        {
            DateFrom = _scope.DateFrom,
            DateTo = _scope.DateTo,
            Granularity = ReportGranularity.Monthly,
            IncludeEvents = false,
            IncludeMissingDays = false
        };
        request.SelectedParameters.AddRange(profile.Parameters.Select(item => item.Key));
        ReportResult legacyResult = ReportEngineService.BuildReport(
            connection, _scope.StationName, request);
        IReadOnlyList<ReportDailyStatus> dailyStatuses = ReadDailyStatuses(connection);
        ReportingOperationalObservation legacy = BuildLegacyReportingObservation(
            legacyResult, dailyStatuses);

        cancellationToken.ThrowIfCancellationRequested();
        RuntimePair runtime = BuildRuntime(connection, cancellationToken);
        ReportProjection targetProjection = BuildTargetReportProjection(
            connection, profile, runtime, dailyStatuses);
        ReportingOperationalObservation target = BuildTargetReportingObservation(
            targetProjection, legacyResult.ChartPoints, dailyStatuses);
        return new(legacy, target, "live-reporting-evidence");
    }

    private ReportingOperationalObservation BuildLegacyReportingObservation(
        ReportResult result,
        IReadOnlyList<ReportDailyStatus> dailyStatuses) => new(
        _scope.StationId,
        _scope.PeriodIdentity,
        _scope.PeriodStartMinute,
        _scope.PeriodEndMinute,
        result.SummaryItems.Where(item => item.Value.HasValue).Select(item =>
            new ReportingSummaryObservation(item.ParameterKey,
                AggregationCode(item.AggregationType), Convert.ToDecimal(item.Value!.Value),
                item.ValueCount)),
        ChartObservations(result.ChartPoints),
        DailyObservations(dailyStatuses),
        WarningCodes(result.SummaryItems.Count, dailyStatuses),
        null,
        null,
        OperationalObservationBoundary.LegacyAuthoritative);

    private ReportingOperationalObservation BuildTargetReportingObservation(
        ReportProjection projection,
        IReadOnlyList<ChartPointModel> chartPoints,
        IReadOnlyList<ReportDailyStatus> dailyStatuses)
    {
        if (projection.Status == ReportProjectionStatus.Rejected)
            throw new InvalidOperationException("The target report read projection was rejected.");
        IEnumerable<ReportingSummaryObservation> hourly = projection.OperationalSummaries.Select(
            item => new ReportingSummaryObservation(RemoveAggregationSuffix(item.ParameterId),
                AggregationCode(item.Aggregation), item.Value, item.ContributingCount));
        IEnumerable<ReportingSummaryObservation> daily = projection.DailySummaries.Select(
            item => new ReportingSummaryObservation(RemoveAggregationSuffix(item.ParameterId),
                "sum", item.Sum, item.ContributingCount));
        return new ReportingOperationalObservation(_scope.StationId, _scope.PeriodIdentity,
            _scope.PeriodStartMinute, _scope.PeriodEndMinute, hourly.Concat(daily),
            ChartObservations(chartPoints), DailyObservations(dailyStatuses),
            WarningCodes(projection.OperationalSummaries.Count + projection.DailySummaries.Count,
                dailyStatuses), null, null, OperationalObservationBoundary.TargetReadOnly);
    }

    private ReportProjection BuildTargetReportProjection(
        SqliteConnection connection,
        ReportStationProfile profile,
        RuntimePair runtime,
        IReadOnlyList<ReportDailyStatus> dailyStatuses)
    {
        ReportRequest request = new()
        {
            DateFrom = _scope.DateFrom,
            DateTo = _scope.DateTo,
            Granularity = ReportGranularity.Monthly,
            IncludeEvents = false,
            IncludeMissingDays = false
        };
        request.SelectedParameters.AddRange(profile.Parameters.Select(item => item.Key));
        List<Dictionary<string, object>> hourlyRows = ReportQueryService.LoadDataRows(
            connection, request, profile.Parameters);
        List<Dictionary<string, object>> dailyRows = ReportQueryService.LoadUniqueRows(
            connection, request, profile.Parameters);

        ReportParameter[] parameters = profile.Parameters.SelectMany(definition =>
            definition.SupportedAggregations.Select(aggregation => new ReportParameter(
                ParameterIdentity(definition.Key, AggregationCode(aggregation)),
                definition.DisplayName, string.Empty, TargetAggregationOf(aggregation),
                Array.IndexOf(profile.Parameters.ToArray(), definition) * 10 +
                    definition.SupportedAggregations.IndexOf(aggregation)))).ToArray();
        NormalizedHourlyValue[] hourly = BuildHourlyValues(hourlyRows, profile.Parameters);
        NormalizedDailyValue[] daily = BuildDailyValues(dailyRows, profile.Parameters);
        AuthoritativeEventInput[] events = runtime.Target.Units.Select(unit =>
            new AuthoritativeEventInput($"live-events-{unit.UnitId}", "event-chain-v1",
                _scope.StationId, unit.UnitId, _scope.PeriodStartMinute,
                _scope.PeriodEndMinute, true, unit.AuthoritativeEvents.Select(item =>
                    new ReportEvent(item.EventId, unit.UnitId, item.EventType,
                        item.EventMinute, item.Sequence)).ToArray())).ToArray();
        AuthoritativeRuntimeInput[] runtimes = runtime.Target.Units.Select(unit =>
            new AuthoritativeRuntimeInput($"live-runtime-{unit.UnitId}", _scope.StationId,
                unit.UnitId, _scope.PeriodStartMinute, _scope.PeriodEndMinute,
                unit.PhysicalRuntimeMinutes, unit.EsdAdjustmentMinutes,
                unit.AdjustedRuntimeMinutes, unit.RuntimeAfterOhMinutes,
                unit.LongestRunMinutes, unit.ServiceDayCount, unit.State,
                "runtime-calculation-v1", "runtime-policy-v1",
                unit.TrustedBaselineReference, "station-profile-v1")).ToArray();
        bool complete = dailyStatuses.All(item => item.IsComplete);
        var completeness = new ReportCompletenessResult(
        [
            new CompletenessDimensionResult(CompletenessDimension.HourlyData,
                complete ? CompletenessState.Complete : CompletenessState.Incomplete,
                complete ? [] : [new CompletenessIssue("hourly.incomplete", "Incomplete hourly input.")]),
            new CompletenessDimensionResult(CompletenessDimension.DailyData,
                dailyStatuses.All(item => item.HasUniqueRow) ? CompletenessState.Complete :
                    CompletenessState.Incomplete,
                dailyStatuses.All(item => item.HasUniqueRow) ? [] :
                    [new CompletenessIssue("daily.incomplete", "Incomplete daily input.")]),
            new CompletenessDimensionResult(CompletenessDimension.EventChain,
                CompletenessState.Complete, []),
            new CompletenessDimensionResult(CompletenessDimension.RuntimeInputs,
                CompletenessState.Complete, [])
        ]);
        var identity = new ReportIdentity($"live-report-{_scope.PeriodIdentity}",
            _scope.StationId, _scope.StationName, _scope.PeriodStartMinute,
            _scope.PeriodEndMinute, _scope.PeriodIdentity, ReportPeriodKind.Monthly,
            profile.Units, ReportSourceMode.OpenProjection);
        var evidence = new ReportEvidence("live-read-v1", "legacy-hourly-v1", hourly.Length,
            "legacy-daily-v1", daily.Length, "station-profile-v1",
            LivePilotReadOnlyPreflight.ToAbsoluteMinute(_scope.DataStartDate),
            "persian-calendar", "date-time-unit");
        var versions = new ReportVersionSet("report-calculation-v1", "report-policy-v1",
            "station-profile-v1", "snapshot-v1", "event-policy-v1",
            "runtime-calculation-v1", "runtime-policy-v1", "persian-calendar-v1",
            profile.Units.ToDictionary(unit => unit, _ => "event-chain-v1"),
            profile.Units.ToDictionary(unit => unit, _ => "legacy-runtime-base-v1"),
            profile.Units.ToDictionary(unit => unit, _ => "station-profile-v1"));
        var input = new NormalizedReportInput(identity,
            _timeProvider.GetUtcNow().ToUniversalTime(), parameters, hourly, daily,
            events, runtimes, completeness, evidence, versions);
        return _reportCalculator.Calculate(input);
    }

    private LivePilotObservationPair<RuntimeEventOperationalObservation> BuildRuntimePair(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        RuntimePair pair = BuildRuntime(connection, cancellationToken);
        return new(pair.Legacy, pair.Target, "live-runtime-event-evidence");
    }

    private RuntimePair BuildRuntime(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReportStationProfile profile = ReportStationProfileProvider.GetProfile(_scope.StationName);
        List<EventLogItem> events = EventReportQueryService.LoadEvents(
            connection, _scope.DateFrom, _scope.DateTo);
        Dictionary<string, UnitInitialEventState> initial =
            EventInitialStateService.LoadInitialStates(connection, profile, _scope.DateFrom);
        Dictionary<string, double> baseRuntime =
            UnitRuntimeBaseQueryService.LoadBaseRuntimeHours(connection);
        Dictionary<string, double> baseAfterOh =
            UnitRuntimeBaseQueryService.LoadBaseRuntimeAfterOHHours(connection);
        EventReportResult legacyResult = EventRuntimeCalculationService.Calculate(profile,
            events, _scope.DateFrom, _scope.DateTo, baseRuntime, baseAfterOh, initial,
            _scope.EsdAdjustmentEnabled, Convert.ToDouble(_scope.EsdAdjustmentHours));

        var contexts = new List<RuntimeCalculationContext>();
        var legacyUnits = new List<RuntimeUnitOperationalObservation>();
        foreach (string unit in profile.Units)
        {
            cancellationToken.ThrowIfCancellationRequested();
            UnitOperationalState initialState = InitialState(initial.GetValueOrDefault(unit));
            NormalizedEvent[] normalized = NormalizeEvents(events, unit).ToArray();
            UnitOperationalState finalState = ReplayStrict(initialState, normalized);
            long baseMinutes = HoursToMinutes(baseRuntime.GetValueOrDefault(unit));
            long baseAfterOhMinutes = HoursToMinutes(baseAfterOh.GetValueOrDefault(unit));
            long esdMinutes = _scope.EsdAdjustmentEnabled
                ? HoursToMinutes(Convert.ToDouble(_scope.EsdAdjustmentHours)) : 0;
            contexts.Add(new RuntimeCalculationContext(
                ValidatedEventChain.Valid(_scope.StationId, unit, normalized,
                    initialState, finalState), _scope.PeriodStartMinute, initialState,
                baseMinutes, baseAfterOhMinutes, _scope.PeriodStartMinute,
                _scope.PeriodEndMinute, esdMinutes, "event-chain-v1",
                "legacy-runtime-base-v1", "runtime-policy-v1",
                "runtime-calculation-v1", _timeProvider.GetUtcNow().ToUniversalTime()));

            UnitEventSummary summary = legacyResult.UnitSummaries.Single(item =>
                StringComparer.Ordinal.Equals(item.Unit, unit));
            long esdAdjustment = HoursToMinutes(summary.EsdExtraHoursTotal);
            long cumulative = HoursToMinutes(summary.RuntimeHours);
            long physical = checked(cumulative - baseMinutes - esdAdjustment);
            if (physical < 0)
                throw new InvalidOperationException("Legacy Runtime components are inconsistent.");
            RuntimeEventItemObservation[] observedEvents = normalized.Select(item =>
                new RuntimeEventItemObservation(item.SourceEventId, item.EventType,
                    item.EventDateTime, item.SourceOrdinal)).ToArray();
            legacyUnits.Add(new RuntimeUnitOperationalObservation(unit, observedEvents,
                physical, esdAdjustment, checked(physical + esdAdjustment),
                HoursToMinutes(summary.RuntimeAfterOH), finalState,
                legacyResult.ServiceDaysByUnit.GetValueOrDefault(unit)?.Count ?? 0,
                HoursToMinutes(summary.LongestRunHours), cumulative,
                "legacy-runtime-base-v1"));
        }

        var legacy = new RuntimeEventOperationalObservation(_scope.StationId,
            _scope.PeriodStartMinute, _scope.PeriodEndMinute, legacyUnits,
            OperationalObservationBoundary.LegacyAuthoritative);
        RuntimeEventOperationalObservation target =
            new TargetRuntimeEventOperationalObservationSource(_runtimeCalculator).Observe(
                _scope.StationId, _scope.PeriodStartMinute, _scope.PeriodEndMinute, contexts);
        return new RuntimePair(legacy, target);
    }

    private IReadOnlyList<ReportDailyStatus> ReadDailyStatuses(SqliteConnection connection)
    {
        var result = new List<ReportDailyStatus>();
        long date = _scope.DateFrom;
        while (date <= _scope.DateTo)
        {
            result.Add(ReportCompletenessService.CheckDay(connection, date));
            date = Rah_Negar.Utils.PersianDateHelper.AddDays(date, 1);
        }
        return result;
    }

    private IEnumerable<NormalizedEvent> NormalizeEvents(
        IReadOnlyList<EventLogItem> events,
        string unit)
    {
        int sequence = 0;
        foreach (EventLogItem item in events.Where(item =>
                     StringComparer.Ordinal.Equals(item.Unit, unit))
                 .OrderBy(item => item.EventDateTime).ThenBy(item => item.EventType,
                     StringComparer.Ordinal))
        {
            if (!EventTypeCode.TryParse(item.EventType, out EventType eventType))
                continue;
            int minuteOfDay = ParseMinute(item.EventTime);
            long eventMinute = checked(LivePilotReadOnlyPreflight.ToAbsoluteMinute(
                item.EventDate) + minuteOfDay);
            sequence++;
            yield return new NormalizedEvent(
                $"legacy-event-{unit}-{item.EventDate}-{minuteOfDay}-{sequence}",
                _scope.StationId, unit, eventType, checked((int)item.EventDate),
                minuteOfDay, eventMinute, sequence, []);
        }
    }

    private static UnitOperationalState ReplayStrict(
        UnitOperationalState state,
        IEnumerable<NormalizedEvent> events)
    {
        long? previous = null;
        foreach (NormalizedEvent item in events)
        {
            if (previous.HasValue && item.EventDateTime <= previous.Value)
                throw new InvalidOperationException("Same-unit event order is ambiguous.");
            state = (state, item.EventType) switch
            {
                (UnitOperationalState.Stopped, EventType.Start) => UnitOperationalState.Running,
                (UnitOperationalState.StoppedAfterOh, EventType.Start) => UnitOperationalState.Running,
                (UnitOperationalState.Running, EventType.Nsd) => UnitOperationalState.Stopped,
                (UnitOperationalState.Running, EventType.Esd) => UnitOperationalState.Stopped,
                (UnitOperationalState.Stopped, EventType.Oh) => UnitOperationalState.StoppedAfterOh,
                _ => throw new InvalidOperationException("The live Event chain is not target-valid.")
            };
            previous = item.EventDateTime;
        }
        return state;
    }

    private static UnitOperationalState InitialState(UnitInitialEventState? state) =>
        state?.IsRunningAtPeriodStart == true
            ? UnitOperationalState.Running
            : state?.HasSeenOHBeforePeriod == true
                ? UnitOperationalState.StoppedAfterOh
                : UnitOperationalState.Stopped;

    private NormalizedHourlyValue[] BuildHourlyValues(
        IReadOnlyList<Dictionary<string, object>> rows,
        IReadOnlyList<ReportParameterDefinition> definitions)
    {
        var values = new List<NormalizedHourlyValue>();
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            Dictionary<string, object> row = rows[rowIndex];
            long date = Convert.ToInt64(row["date_rep"], CultureInfo.InvariantCulture);
            int minuteOfDay = ParseHour(row["time_rep"]?.ToString()) * 60;
            foreach (ReportParameterDefinition definition in definitions.Where(item =>
                         item.DataColumnName is not null))
            {
                if (!TryDecimal(row, definition.DataColumnName!, out decimal value)) continue;
                foreach (LegacyAggregation aggregation in definition.SupportedAggregations.Where(
                             item => item is not LegacyAggregation.Sum))
                {
                    string code = AggregationCode(aggregation);
                    values.Add(new NormalizedHourlyValue(
                        $"hourly-{date}-{minuteOfDay}-{rowIndex}-{code}",
                        ParameterIdentity(definition.Key, code), date.ToString(CultureInfo.InvariantCulture),
                        checked(LivePilotReadOnlyPreflight.ToAbsoluteMinute(date) + minuteOfDay), value));
                }
            }
        }
        return values.ToArray();
    }

    private static NormalizedDailyValue[] BuildDailyValues(
        IReadOnlyList<Dictionary<string, object>> rows,
        IReadOnlyList<ReportParameterDefinition> definitions)
    {
        var values = new List<NormalizedDailyValue>();
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            Dictionary<string, object> row = rows[rowIndex];
            long date = Convert.ToInt64(row["date_rep"], CultureInfo.InvariantCulture);
            foreach (ReportParameterDefinition definition in definitions.Where(item =>
                         item.UniqueColumnName is not null &&
                         item.SupportedAggregations.Contains(LegacyAggregation.Sum)))
            {
                if (!TryDecimal(row, definition.UniqueColumnName!, out decimal value)) continue;
                values.Add(new NormalizedDailyValue($"daily-{date}-{rowIndex}-{definition.Key}",
                    ParameterIdentity(definition.Key, "sum"),
                    date.ToString(CultureInfo.InvariantCulture), value));
            }
        }
        return values.ToArray();
    }

    private static IEnumerable<ReportingChartPointObservation> ChartObservations(
        IEnumerable<ChartPointModel> values) => values.Where(item => item.Value.HasValue).Select(
        (item, index) => new ReportingChartPointObservation(item.ParameterKey,
            $"{item.DateRep}-{ParseHour(item.TimeRep).ToString("00", CultureInfo.InvariantCulture)}-{index}",
            Convert.ToDecimal(item.Value!.Value)));

    private static IEnumerable<ReportingDailyStatusObservation> DailyObservations(
        IEnumerable<ReportDailyStatus> values) => values.Select(item =>
            new ReportingDailyStatusObservation(item.DateRep.ToString(CultureInfo.InvariantCulture),
                item.IsComplete ? "complete" : item.HasNoData ? "no-data" : "incomplete",
                12, item.DataRowCount));

    private static IEnumerable<string> WarningCodes(
        int summaryCount,
        IEnumerable<ReportDailyStatus> dailyStatuses)
    {
        if (summaryCount == 0) yield return "no-report-data";
        if (dailyStatuses.Any(item => !item.IsComplete)) yield return "incomplete-day";
    }

    private static string AggregationCode(LegacyAggregation value) => value switch
    {
        LegacyAggregation.Min => "minimum",
        LegacyAggregation.Max => "maximum",
        LegacyAggregation.Avg => "average",
        LegacyAggregation.Sum => "sum",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static string AggregationCode(TargetAggregation value) => value switch
    {
        TargetAggregation.Minimum => "minimum",
        TargetAggregation.Maximum => "maximum",
        TargetAggregation.Average => "average",
        TargetAggregation.Sum => "sum",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static TargetAggregation TargetAggregationOf(LegacyAggregation value) => value switch
    {
        LegacyAggregation.Min => TargetAggregation.Minimum,
        LegacyAggregation.Max => TargetAggregation.Maximum,
        LegacyAggregation.Avg => TargetAggregation.Average,
        LegacyAggregation.Sum => TargetAggregation.Sum,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static string ParameterIdentity(string key, string aggregation) =>
        $"{key}.{aggregation}";

    private static string RemoveAggregationSuffix(string value) =>
        value[..value.LastIndexOf('.')];

    private static bool TryDecimal(
        IReadOnlyDictionary<string, object> row,
        string column,
        out decimal value)
    {
        value = 0;
        return row.TryGetValue(column, out object? raw) && raw is not null &&
            decimal.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture),
                NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static int ParseHour(string? value)
    {
        string text = (value ?? "0").Split(':')[0];
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture,
            out int hour) && hour is >= 0 and <= 23 ? hour : 0;
    }

    private static int ParseMinute(string? value) =>
        TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out TimeSpan parsed)
            ? checked(parsed.Hours * 60 + parsed.Minutes)
            : 0;

    private static long HoursToMinutes(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
            throw new InvalidOperationException("Runtime read value is invalid.");
        return checked((long)Math.Round(value * 60d, MidpointRounding.AwayFromZero));
    }

    private static async Task<(bool Enabled, decimal Hours)> ReadEsdSettingsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT esd_extra_runtime_enabled, esd_extra_runtime_hours
            FROM app_settings ORDER BY id LIMIT 1;
            """;
        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Settings read model is unavailable.");
        return (reader.GetInt64(0) == 1,
            Convert.ToDecimal(reader.GetDouble(1), CultureInfo.InvariantCulture));
    }

    private static async Task<bool> TablesExistAsync(
        SqliteConnection connection,
        IEnumerable<string> expected,
        CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            names.Add(reader.GetString(0));
        return expected.All(names.Contains);
    }

    private sealed record RuntimePair(
        RuntimeEventOperationalObservation Legacy,
        RuntimeEventOperationalObservation Target);
}
