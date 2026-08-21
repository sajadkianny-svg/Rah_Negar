using Rah_Negar.Core.Reports;
using Rah_Negar.Models.Reports;

namespace Rah_Negar.Services.Reports;

/// <summary>
/// سرویس محاسبه ساعات کارکرد، تعداد رویدادها و روزهای سرویس واحدها
/// منطق RuntimeAfterOH:
/// 1- قبل از OH مانند RuntimeHours رشد می‌کند
/// 2- با ثبت OH صفر می‌شود
/// 3- بعد از OH از STARTهای بعدی دوباره رشد می‌کند
/// </summary>
public static class EventRuntimeCalculationService
{
    public static EventReportResult Calculate(
        ReportStationProfile profile,
        IReadOnlyList<EventLogItem> events,
        long dateFrom,
        long dateTo,
        Dictionary<string, double> baseRuntimeHours,
        Dictionary<string, double> baseRuntimeAfterOHHours,
        Dictionary<string, UnitInitialEventState> initialStates,
        bool esdExtraEnabled,
        double esdExtraHours)
    {
        return CalculateLegacyCore(
            profile,
            events,
            dateFrom,
            dateTo,
            baseRuntimeHours,
            baseRuntimeAfterOHHours,
            initialStates,
            esdExtraEnabled,
            esdExtraHours);
    }

    private static EventReportResult CalculateLegacyCore(
        ReportStationProfile profile,
        IReadOnlyList<EventLogItem> events,
        long dateFrom,
        long dateTo,
        Dictionary<string, double> baseRuntimeHours,
        Dictionary<string, double> baseRuntimeAfterOHHours,
        Dictionary<string, UnitInitialEventState> initialStates,
        bool esdExtraEnabled,
        double esdExtraHours)
    {
        DateTime periodStart = ConvertPersianDateTimeToGregorian(dateFrom, "00:00");

        long nextDate = GetNextPersianDate(dateTo);
        DateTime periodEndExclusive = ConvertPersianDateTimeToGregorian(nextDate, "00:00");

        Dictionary<string, UnitEventSummary> summaries = profile.Units.ToDictionary(
            unit => unit,
            unit => new UnitEventSummary
            {
                Unit = unit,
                RuntimeHours = baseRuntimeHours.TryGetValue(unit, out double baseRuntime)
                    ? baseRuntime
                    : 0,

                RuntimeAfterOH = baseRuntimeAfterOHHours.TryGetValue(unit, out double baseAfterOH)
                    ? baseAfterOH
                    : 0
            });

        Dictionary<string, HashSet<long>> serviceDaysByUnit = profile.Units.ToDictionary(
            unit => unit,
            _ => new HashSet<long>());

        Dictionary<string, DateTime?> currentRunStart = profile.Units.ToDictionary(
            unit => unit,
            unit =>
            {
                if (initialStates.TryGetValue(unit, out UnitInitialEventState? state) &&
                    state.IsRunningAtPeriodStart)
                {
                    return (DateTime?)periodStart;
                }

                return null;
            });

        Dictionary<string, DateTime?> currentRunAfterOHStart = profile.Units.ToDictionary(
            unit => unit,
            unit =>
            {
                if (initialStates.TryGetValue(unit, out UnitInitialEventState? state) &&
                    state.IsRunningAfterOHAtPeriodStart)
                {
                    return (DateTime?)periodStart;
                }

                return null;
            });

        List<EventLogItem> orderedEvents = events
            .Where(e => profile.Units.Contains(e.Unit))
            .Where(e => IsSupportedEventType(e.EventType))
            .OrderBy(e => e.EventDateTime)
            .ThenBy(e => e.Unit)
            .ToList();

        foreach (EventLogItem ev in orderedEvents)
        {
            string unit = ev.Unit;
            string eventType = NormalizeEventType(ev.EventType);
            DateTime eventDateTime = ev.EventDateTime;

            UnitEventSummary summary = summaries[unit];

            if (eventType == "START")
            {
                HandleStartEvent(
                    unit,
                    eventDateTime,
                    summary,
                    currentRunStart,
                    currentRunAfterOHStart,
                    serviceDaysByUnit);
            }
            else if (eventType == "NSD")
            {
                HandleStopEvent(
                    unit,
                    eventDateTime,
                    summary,
                    currentRunStart,
                    currentRunAfterOHStart,
                    serviceDaysByUnit,
                    isEsd: false,
                    esdExtraEnabled,
                    esdExtraHours);
            }
            else if (eventType == "ESD")
            {
                HandleStopEvent(
                    unit,
                    eventDateTime,
                    summary,
                    currentRunStart,
                    currentRunAfterOHStart,
                    serviceDaysByUnit,
                    isEsd: true,
                    esdExtraEnabled,
                    esdExtraHours);
            }
            else if (eventType == "OH")
            {
                HandleOverhaulEvent(
                    unit,
                    eventDateTime,
                    summary,
                    currentRunStart,
                    currentRunAfterOHStart,
                    serviceDaysByUnit);
            }
        }

        CloseOpenRunsAtPeriodEnd(
            summaries,
            currentRunStart,
            currentRunAfterOHStart,
            serviceDaysByUnit,
            periodEndExclusive);

        return new EventReportResult
        {
            UnitSummaries = summaries.Values.ToList(),
            EventLogItems = orderedEvents,
            ServiceDaysByUnit = serviceDaysByUnit
        };
    }

    private static EventReportResult CalculateStateMachineCore(
        ReportStationProfile profile,
        IReadOnlyList<EventLogItem> events,
        long calculationStartDate,
        long dateFrom,
        long dateTo,
        Dictionary<string, double> baseRuntimeHours,
        Dictionary<string, double> baseRuntimeAfterOHHours,
        Dictionary<string, UnitInitialEventState> initialStates,
        bool esdExtraEnabled,
        double esdExtraHours)
    {
        DateTime calculationStart =
            ConvertPersianDateTimeToGregorian(calculationStartDate, "00:00");

        DateTime periodStart = ConvertPersianDateTimeToGregorian(dateFrom, "00:00");

        long nextDate = GetNextPersianDate(dateTo);
        DateTime periodEndExclusive = ConvertPersianDateTimeToGregorian(nextDate, "00:00");

        var window = new CalculationWindow(
            calculationStart,
            periodStart,
            periodEndExclusive);

        if (window.CalculationStart > window.PeriodStart)
            throw new ArgumentException("Calculation start must not be after the report start.");

        if (window.PeriodStart >= window.PeriodEndExclusive)
            throw new ArgumentException("Report date range is invalid.");

        Dictionary<string, UnitRuntimeState> states = profile.Units.ToDictionary(
            unit => unit,
            unit => new UnitRuntimeState
            {
                Unit = unit,
                CurrentTime = window.CalculationStart,
                IsRunning =
                    initialStates.TryGetValue(unit, out UnitInitialEventState? initialState) &&
                    initialState.IsRunningAtPeriodStart,
                CumulativeRuntimeHours =
                    baseRuntimeHours.TryGetValue(unit, out double baseRuntime)
                        ? baseRuntime
                        : 0,
                RuntimeAfterOH =
                    baseRuntimeAfterOHHours.TryGetValue(unit, out double baseAfterOH)
                        ? baseAfterOH
                        : 0
            });

        // TODO: EventLogItem currently does not expose tbl_events.id.
        // Use that id as the final ordering key when it becomes available.
        // Until then, preserve source order for events with identical date/time and unit.
        List<EventLogItem> orderedEvents = events
            .Select((eventItem, sourceOrder) => new OrderedRuntimeEvent(eventItem, sourceOrder))
            .Where(e => states.ContainsKey(e.Unit))
            .Where(e => IsSupportedEventType(e.EventType))
            .Where(e => e.EventDateTime >= window.CalculationStart)
            .Where(e => e.EventDateTime < window.PeriodEndExclusive)
            .OrderBy(e => e.EventDateTime)
            .ThenBy(e => e.Unit)
            .ThenBy(e => e.SourceOrder)
            .Select(e => e.Event)
            .ToList();

        foreach (EventLogItem ev in orderedEvents)
        {
            UnitRuntimeState state = states[ev.Unit];

            AdvanceState(state, ev.EventDateTime, window);

            string eventType = NormalizeEventType(ev.EventType);

            if (eventType == "START")
            {
                ApplyStart(state, ev.EventDateTime, window);
            }
            else if (eventType == "NSD")
            {
                ApplyNSD(state, ev.EventDateTime, window);
            }
            else if (eventType == "ESD")
            {
                ApplyESD(
                    state,
                    ev.EventDateTime,
                    window,
                    esdExtraEnabled,
                    esdExtraHours);
            }
            else if (eventType == "OH")
            {
                ApplyOH(state, ev.EventDateTime, window);
            }
        }

        foreach (UnitRuntimeState state in states.Values)
            AdvanceState(state, window.PeriodEndExclusive, window);

        return BuildStateMachineResult(states, orderedEvents, window);
    }

    private static RuntimeCalculationComparison CompareLegacyAndStateMachine(
        ReportStationProfile profile,
        IReadOnlyList<EventLogItem> events,
        long calculationStartDate,
        long dateFrom,
        long dateTo,
        Dictionary<string, double> baseRuntimeHours,
        Dictionary<string, double> baseRuntimeAfterOHHours,
        Dictionary<string, UnitInitialEventState> initialStates,
        bool esdExtraEnabled,
        double esdExtraHours)
    {
        EventReportResult legacy = CalculateLegacyCore(
            profile,
            events,
            dateFrom,
            dateTo,
            baseRuntimeHours,
            baseRuntimeAfterOHHours,
            initialStates,
            esdExtraEnabled,
            esdExtraHours);

        EventReportResult stateMachine = CalculateStateMachineCore(
            profile,
            events,
            calculationStartDate,
            dateFrom,
            dateTo,
            baseRuntimeHours,
            baseRuntimeAfterOHHours,
            initialStates,
            esdExtraEnabled,
            esdExtraHours);

        return new RuntimeCalculationComparison
        {
            Legacy = legacy,
            StateMachine = stateMachine,
            InvariantDifferences =
            [
                "Invariant comparison has not been implemented."
            ]
        };
    }

    private static void AdvanceState(
        UnitRuntimeState state,
        DateTime targetTime,
        CalculationWindow window)
    {
        if (targetTime < state.CurrentTime)
            throw new InvalidOperationException("Runtime events are not in chronological order.");

        if (targetTime == state.CurrentTime)
            return;

        if (state.IsRunning)
        {
            double elapsedHours = (targetTime - state.CurrentTime).TotalHours;

            state.CumulativeRuntimeHours += elapsedHours;
            state.RuntimeAfterOH += elapsedHours;

            DateTime overlapStart = state.CurrentTime > window.PeriodStart
                ? state.CurrentTime
                : window.PeriodStart;

            DateTime overlapEnd = targetTime < window.PeriodEndExclusive
                ? targetTime
                : window.PeriodEndExclusive;

            if (overlapEnd > overlapStart)
            {
                AddServiceDaysForRange(
                    state.PeriodServiceDays,
                    overlapStart,
                    overlapEnd);
            }
        }

        state.CurrentTime = targetTime;
    }

    private static void ApplyStart(
        UnitRuntimeState state,
        DateTime eventDateTime,
        CalculationWindow window)
    {
        if (IsInsidePeriod(eventDateTime, window))
        {
            state.TotalEvents++;
            state.StartCount++;

            if (IsDayShiftTime(eventDateTime))
                state.DayStartCount++;
            else
                state.NightStartCount++;
        }

        if (state.IsRunning)
            return;

        state.IsRunning = true;

        if (IsInsidePeriod(eventDateTime, window))
            state.PeriodRunStart = eventDateTime;
    }

    private static void ApplyNSD(
        UnitRuntimeState state,
        DateTime eventDateTime,
        CalculationWindow window)
    {
        if (IsInsidePeriod(eventDateTime, window))
        {
            state.TotalEvents++;
            state.NSDCount++;

            if (IsDayShiftTime(eventDateTime))
                state.DayNSDCount++;
            else
                state.NightNSDCount++;
        }

        ClosePeriodRun(state, eventDateTime, window);
        state.IsRunning = false;
    }

    private static void ApplyESD(
        UnitRuntimeState state,
        DateTime eventDateTime,
        CalculationWindow window,
        bool esdExtraEnabled,
        double esdExtraHours)
    {
        bool isInsidePeriod = IsInsidePeriod(eventDateTime, window);

        if (isInsidePeriod)
        {
            state.TotalEvents++;
            state.ESDCount++;

            if (IsDayShiftTime(eventDateTime))
                state.DayESDCount++;
            else
                state.NightESDCount++;
        }

        if (esdExtraEnabled && esdExtraHours > 0)
        {
            state.CumulativeRuntimeHours += esdExtraHours;
            state.RuntimeAfterOH += esdExtraHours;

            if (isInsidePeriod)
                state.PeriodEsdExtraHours += esdExtraHours;
        }

        ClosePeriodRun(state, eventDateTime, window);
        state.IsRunning = false;
    }

    private static void ApplyOH(
        UnitRuntimeState state,
        DateTime eventDateTime,
        CalculationWindow window)
    {
        if (IsInsidePeriod(eventDateTime, window))
            state.TotalEvents++;

        ClosePeriodRun(state, eventDateTime, window);
        state.IsRunning = false;
        state.RuntimeAfterOH = 0;
    }

    private static void ClosePeriodRun(
        UnitRuntimeState state,
        DateTime runEnd,
        CalculationWindow window)
    {
        if (!state.PeriodRunStart.HasValue)
            return;

        DateTime effectiveEnd = runEnd < window.PeriodEndExclusive
            ? runEnd
            : window.PeriodEndExclusive;

        if (effectiveEnd > state.PeriodRunStart.Value)
        {
            double runHours =
                (effectiveEnd - state.PeriodRunStart.Value).TotalHours;

            if (runHours > state.LongestPeriodRunHours)
                state.LongestPeriodRunHours = runHours;
        }

        state.PeriodRunStart = null;
    }

    private static EventReportResult BuildStateMachineResult(
        Dictionary<string, UnitRuntimeState> states,
        IReadOnlyList<EventLogItem> orderedEvents,
        CalculationWindow window)
    {
        foreach (UnitRuntimeState state in states.Values)
            ClosePeriodRun(state, window.PeriodEndExclusive, window);

        List<UnitEventSummary> summaries = states.Values
            .Select(state => new UnitEventSummary
            {
                Unit = state.Unit,
                RuntimeHours = state.CumulativeRuntimeHours,
                RuntimeAfterOH = state.RuntimeAfterOH,
                TotalEvents = state.TotalEvents,
                StartCount = state.StartCount,
                NSDCount = state.NSDCount,
                ESDCount = state.ESDCount,
                EsdExtraHoursTotal = state.PeriodEsdExtraHours,
                LongestRunHours = state.LongestPeriodRunHours,
                DayStartCount = state.DayStartCount,
                NightStartCount = state.NightStartCount,
                DayNSDCount = state.DayNSDCount,
                NightNSDCount = state.NightNSDCount,
                DayESDCount = state.DayESDCount,
                NightESDCount = state.NightESDCount
            })
            .ToList();

        List<EventLogItem> periodEvents = orderedEvents
            .Where(e => IsInsidePeriod(e.EventDateTime, window))
            .ToList();

        Dictionary<string, HashSet<long>> serviceDaysByUnit = states
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value.PeriodServiceDays);

        return new EventReportResult
        {
            UnitSummaries = summaries,
            EventLogItems = periodEvents,
            ServiceDaysByUnit = serviceDaysByUnit
        };
    }

    private static bool IsInsidePeriod(
        DateTime eventDateTime,
        CalculationWindow window)
    {
        return eventDateTime >= window.PeriodStart &&
               eventDateTime < window.PeriodEndExclusive;
    }

    private sealed class UnitRuntimeState
    {
        public string Unit { get; init; } = string.Empty;

        public DateTime CurrentTime { get; set; }

        public bool IsRunning { get; set; }

        public double CumulativeRuntimeHours { get; set; }

        public double RuntimeAfterOH { get; set; }

        public DateTime? PeriodRunStart { get; set; }

        public double LongestPeriodRunHours { get; set; }

        public HashSet<long> PeriodServiceDays { get; } = [];

        public int TotalEvents { get; set; }

        public int StartCount { get; set; }

        public int NSDCount { get; set; }

        public int ESDCount { get; set; }

        public double PeriodEsdExtraHours { get; set; }

        public int DayStartCount { get; set; }

        public int NightStartCount { get; set; }

        public int DayNSDCount { get; set; }

        public int NightNSDCount { get; set; }

        public int DayESDCount { get; set; }

        public int NightESDCount { get; set; }
    }

    private readonly record struct CalculationWindow(
        DateTime CalculationStart,
        DateTime PeriodStart,
        DateTime PeriodEndExclusive);

    private readonly record struct OrderedRuntimeEvent(
        EventLogItem Event,
        int SourceOrder)
    {
        public string Unit => Event.Unit;

        public string EventType => Event.EventType;

        public DateTime EventDateTime => Event.EventDateTime;
    }

    private sealed class RuntimeCalculationComparison
    {
        public required EventReportResult Legacy { get; init; }

        public EventReportResult? StateMachine { get; init; }

        public IReadOnlyList<string> InvariantDifferences { get; init; } = [];
    }

    private static void HandleStartEvent(
        string unit,
        DateTime eventDateTime,
        UnitEventSummary summary,
        Dictionary<string, DateTime?> currentRunStart,
        Dictionary<string, DateTime?> currentRunAfterOHStart,
        Dictionary<string, HashSet<long>> serviceDaysByUnit)
    {
        summary.TotalEvents++;
        summary.StartCount++;

        if (IsDayShiftTime(eventDateTime))
            summary.DayStartCount++;
        else
            summary.NightStartCount++;

        if (currentRunStart[unit].HasValue)
        {
            CloseRuntimeRun(
                summary,
                serviceDaysByUnit[unit],
                currentRunStart[unit]!.Value,
                eventDateTime);
        }

        if (currentRunAfterOHStart[unit].HasValue)
        {
            CloseAfterOHRun(
                summary,
                currentRunAfterOHStart[unit]!.Value,
                eventDateTime);
        }

        currentRunStart[unit] = eventDateTime;
        currentRunAfterOHStart[unit] = eventDateTime;
    }

    private static void HandleStopEvent(
        string unit,
        DateTime eventDateTime,
        UnitEventSummary summary,
        Dictionary<string, DateTime?> currentRunStart,
        Dictionary<string, DateTime?> currentRunAfterOHStart,
        Dictionary<string, HashSet<long>> serviceDaysByUnit,
        bool isEsd,
        bool esdExtraEnabled,
        double esdExtraHours)
    {
        summary.TotalEvents++;

        if (isEsd)
        {
            summary.ESDCount++;

            if (IsDayShiftTime(eventDateTime))
                summary.DayESDCount++;
            else
                summary.NightESDCount++;

            if (esdExtraEnabled && esdExtraHours > 0)
            {
                summary.RuntimeHours += esdExtraHours;
                summary.RuntimeAfterOH += esdExtraHours;
                summary.EsdExtraHoursTotal += esdExtraHours;
            }
        }
        else
        {
            summary.NSDCount++;

            if (IsDayShiftTime(eventDateTime))
                summary.DayNSDCount++;
            else
                summary.NightNSDCount++;
        }

        if (currentRunStart[unit].HasValue)
        {
            CloseRuntimeRun(
                summary,
                serviceDaysByUnit[unit],
                currentRunStart[unit]!.Value,
                eventDateTime);

            currentRunStart[unit] = null;
        }

        if (currentRunAfterOHStart[unit].HasValue)
        {
            CloseAfterOHRun(
                summary,
                currentRunAfterOHStart[unit]!.Value,
                eventDateTime);

            currentRunAfterOHStart[unit] = null;
        }
    }

    private static void HandleOverhaulEvent(
        string unit,
        DateTime eventDateTime,
        UnitEventSummary summary,
        Dictionary<string, DateTime?> currentRunStart,
        Dictionary<string, DateTime?> currentRunAfterOHStart,
        Dictionary<string, HashSet<long>> serviceDaysByUnit)
    {
        summary.TotalEvents++;

        if (currentRunStart[unit].HasValue)
        {
            CloseRuntimeRun(
                summary,
                serviceDaysByUnit[unit],
                currentRunStart[unit]!.Value,
                eventDateTime);

            currentRunStart[unit] = null;
        }

        if (currentRunAfterOHStart[unit].HasValue)
        {
            CloseAfterOHRun(
                summary,
                currentRunAfterOHStart[unit]!.Value,
                eventDateTime);

            currentRunAfterOHStart[unit] = null;
        }

        // قانون اصلی:
        // با ثبت OH، کارکرد بعد از اورهال صفر می‌شود
        summary.RuntimeAfterOH = 0;
    }

    private static void CloseRuntimeRun(
        UnitEventSummary summary,
        HashSet<long> serviceDays,
        DateTime runStart,
        DateTime runEnd)
    {
        if (runEnd <= runStart)
            return;

        double runHours = (runEnd - runStart).TotalHours;

        summary.RuntimeHours += runHours;

        if (runHours > summary.LongestRunHours)
            summary.LongestRunHours = runHours;

        AddServiceDaysForRange(serviceDays, runStart, runEnd);
    }

    private static void CloseAfterOHRun(
        UnitEventSummary summary,
        DateTime runStart,
        DateTime runEnd)
    {
        if (runEnd <= runStart)
            return;

        summary.RuntimeAfterOH += (runEnd - runStart).TotalHours;
    }

    private static void CloseOpenRunsAtPeriodEnd(
        Dictionary<string, UnitEventSummary> summaries,
        Dictionary<string, DateTime?> currentRunStart,
        Dictionary<string, DateTime?> currentRunAfterOHStart,
        Dictionary<string, HashSet<long>> serviceDaysByUnit,
        DateTime periodEndExclusive)
    {
        foreach (string unit in summaries.Keys)
        {
            UnitEventSummary summary = summaries[unit];

            if (currentRunStart[unit].HasValue)
            {
                CloseRuntimeRun(
                    summary,
                    serviceDaysByUnit[unit],
                    currentRunStart[unit]!.Value,
                    periodEndExclusive);
            }

            if (currentRunAfterOHStart[unit].HasValue)
            {
                CloseAfterOHRun(
                    summary,
                    currentRunAfterOHStart[unit]!.Value,
                    periodEndExclusive);
            }
        }
    }

    private static void AddServiceDaysForRange(
        HashSet<long> serviceDays,
        DateTime start,
        DateTime end)
    {
        DateTime current = start.Date;
        DateTime last = end.AddTicks(-1).Date;

        while (current <= last)
        {
            serviceDays.Add(ConvertGregorianDateToPersianLong(current));
            current = current.AddDays(1);
        }
    }

    private static bool IsDayShiftTime(DateTime dateTime)
    {
        TimeSpan time = dateTime.TimeOfDay;

        return time >= new TimeSpan(7, 0, 0)
            && time < new TimeSpan(19, 0, 0);
    }

    private static bool IsSupportedEventType(string eventType)
    {
        return NormalizeEventType(eventType) is "START" or "NSD" or "ESD" or "OH";
    }

    private static string NormalizeEventType(string eventType)
    {
        return eventType.Trim().ToUpperInvariant();
    }

    private static DateTime ConvertPersianDateTimeToGregorian(long persianDate, string time)
    {
        int year = (int)(persianDate / 10000);
        int month = (int)((persianDate / 100) % 100);
        int day = (int)(persianDate % 100);

        TimeSpan timeSpan = TimeSpan.TryParse(time, out TimeSpan parsed)
            ? parsed
            : TimeSpan.Zero;

        System.Globalization.PersianCalendar calendar = new();

        return calendar.ToDateTime(
            year,
            month,
            day,
            timeSpan.Hours,
            timeSpan.Minutes,
            0,
            0);
    }

    private static long ConvertGregorianDateToPersianLong(DateTime date)
    {
        System.Globalization.PersianCalendar calendar = new();

        int year = calendar.GetYear(date);
        int month = calendar.GetMonth(date);
        int day = calendar.GetDayOfMonth(date);

        return year * 10000L + month * 100L + day;
    }

    private static long GetNextPersianDate(long persianDate)
    {
        DateTime gregorian = ConvertPersianDateTimeToGregorian(persianDate, "00:00");
        DateTime next = gregorian.AddDays(1);

        return ConvertGregorianDateToPersianLong(next);
    }
}
