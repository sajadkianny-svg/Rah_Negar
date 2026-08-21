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