using Microsoft.Data.Sqlite;
using Rah_Negar.Data;
using Rah_Negar.Models;

namespace Rah_Negar.Services.Reports;

/// <summary>
/// نتیجه اعتبارسنجی توالی رویدادها
/// </summary>
public sealed class EventSequenceValidationResult
{
    public bool IsValid { get; init; }

    public string Message { get; init; } = string.Empty;

    public static EventSequenceValidationResult Success()
    {
        return new EventSequenceValidationResult { IsValid = true };
    }

    public static EventSequenceValidationResult Fail(string message)
    {
        return new EventSequenceValidationResult
        {
            IsValid = false,
            Message = message
        };
    }
}

/// <summary>
/// اعتبارسنجی توالی منطقی رویدادهای واحدها هنگام ذخیره روزانه
/// </summary>
public static class EventSequenceValidationService
{
    /// <summary>
    /// اعتبارسنجی رویدادهای یک روز قبل از ذخیره نهایی
    /// </summary>
    public static EventSequenceValidationResult ValidateDailyEvents(
        long dateRep,
        List<DailyEventRowModel> dailyEvents)
    {
        List<DailyEventRowModel> normalizedEvents = dailyEvents
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.Unit) &&
                !string.IsNullOrWhiteSpace(x.EventType) &&
                !string.IsNullOrWhiteSpace(x.EventTime))
            .Select(x => new DailyEventRowModel
            {
                DateRep = dateRep,
                Unit = NormalizeUnit(x.Unit),
                EventType = NormalizeEventType(x.EventType),
                EventTime = NormalizeTime(x.EventTime),
                Remark = x.Remark ?? string.Empty
            })
            .OrderBy(x => x.EventTime)
            .ThenBy(x => x.Unit)
            .ToList();

        EventSequenceValidationResult sameTimeCheck =
            ValidateSameTimeEvents(normalizedEvents);

        if (!sameTimeCheck.IsValid)
            return sameTimeCheck;

        using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();

        foreach (IGrouping<string, DailyEventRowModel> unitGroup in normalizedEvents.GroupBy(x => x.Unit))
        {
            string unit = unitGroup.Key;

            List<DailyEventRowModel> chain = [];

            DailyEventRowModel? previousEvent =
                LoadPreviousEventBeforeDate(conn, unit, dateRep);

            if (previousEvent != null)
            {
                chain.Add(previousEvent);
            }
            else
            {
                chain.Add(LoadInitialStateAsEvent(conn, unit));
            }

            chain.AddRange(unitGroup.OrderBy(x => x.EventTime));

            DailyEventRowModel? nextEvent =
                LoadNextEventAfterDate(conn, unit, dateRep);

            if (nextEvent != null)
                chain.Add(nextEvent);

            EventSequenceValidationResult chainCheck =
                ValidateEventChain(unit, chain);

            if (!chainCheck.IsValid)
                return chainCheck;
        }

        return EventSequenceValidationResult.Success();
    }



    private static DailyEventRowModel LoadInitialStateAsEvent(
        SqliteConnection conn,
        string unit)
    {
        int unitNo = ExtractUnitNo(unit);

        using SqliteCommand cmd = conn.CreateCommand();

        cmd.CommandText = """
        SELECT initial_is_running
        FROM unit_runtime_base
        WHERE unit_no = $unit_no
        LIMIT 1;
        """;

        cmd.Parameters.AddWithValue("$unit_no", unitNo);

        object? result = cmd.ExecuteScalar();

        if (result == null || result == DBNull.Value)
        {
            throw new InvalidOperationException(
                $"وضعیت اولیه {GetUnitDisplayName(unit)} در تنظیمات اولیه برنامه ثبت نشده است.");
        }

        bool isRunning = Convert.ToInt32(result) == 1;

        return new DailyEventRowModel
        {
            DateRep = 0,
            Unit = unit,
            EventType = isRunning ? "__INITIAL_RUNNING__" : "__INITIAL_STOPPED__",
            EventTime = "00:00",
            Remark = "__INITIAL_STATE__"
        };
    }


    /// <summary>
    /// واحدهایی را که در تاریخ جاری از قبل رویداد دارند برمی‌گرداند.
    /// این برای حالت ویرایش مهم است؛ چون ممکن است کاربر رویدادهای یک روز را حذف کند
    /// و حذف آن‌ها زنجیره رویدادهای روزهای بعد را خراب کند.
    /// </summary>
    private static List<string> LoadUnitsHavingEventsOnDate(
        SqliteConnection conn,
        long dateRep)
    {
        List<string> units = [];

        using SqliteCommand cmd = conn.CreateCommand();

        cmd.CommandText = """
        SELECT DISTINCT unit
        FROM tbl_events
        WHERE date_rep = $dateRep;
        """;

        cmd.Parameters.AddWithValue("$dateRep", dateRep);

        using SqliteDataReader reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            string unit = NormalizeUnit(reader["unit"]?.ToString());

            if (!string.IsNullOrWhiteSpace(unit))
                units.Add(unit);
        }

        return units;
    }


    private static bool LoadInitialIsRunning(SqliteConnection conn, string unit)
    {
        int unitNo = ExtractUnitNo(unit);

        using SqliteCommand cmd = conn.CreateCommand();

        cmd.CommandText = """
        SELECT initial_is_running
        FROM unit_runtime_base
        WHERE unit_no = $unit_no
        LIMIT 1;
        """;

        cmd.Parameters.AddWithValue("$unit_no", unitNo);

        object? result = cmd.ExecuteScalar();

        if (result == null || result == DBNull.Value)
            return false;

        return Convert.ToInt32(result) == 1;
    }

    private static int ExtractUnitNo(string unit)
    {
        string normalized = NormalizeUnit(unit);

        return normalized switch
        {
            "U1" => 1,
            "U2" => 2,
            "U3" => 3,
            "U4" => 4,
            _ => 0
        };
    }


    /// <summary>
    /// جلوگیری از ثبت دو رویداد هم‌زمان برای یک واحد در همان روز
    /// </summary>
    private static EventSequenceValidationResult ValidateSameTimeEvents(
        List<DailyEventRowModel> events)
    {
        var duplicate = events
            .GroupBy(x => new { x.Unit, x.EventTime })
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicate == null)
            return EventSequenceValidationResult.Success();

        return EventSequenceValidationResult.Fail(
            $"برای {GetUnitDisplayName(duplicate.Key.Unit)} در ساعت {duplicate.Key.EventTime} بیش از یک رویداد ثبت شده است");
    }

    /// <summary>
    /// بررسی زنجیره رویدادهای یک واحد
    /// </summary>
    private static EventSequenceValidationResult ValidateEventChain(
        string unit,
        List<DailyEventRowModel> chain)
    {
        if (chain.Count == 0)
            return EventSequenceValidationResult.Success();

        string unitDisplayName = GetUnitDisplayName(unit);

        for (int i = 1; i < chain.Count; i++)
        {
            DailyEventRowModel previous = chain[i - 1];
            DailyEventRowModel current = chain[i];

            if (IsTransitionAllowed(previous.EventType, current.EventType))
                continue;
           
            if (previous.Remark == "__INITIAL_STATE__")
            {
                string initialStateText =
                    previous.EventType == "__INITIAL_RUNNING__"
                        ? "روشن"
                        : "خاموش";

                string messageInitial =
                    $"{unitDisplayName} در تنظیمات اولیه برنامه {initialStateText} تعریف شده است" +
                    Environment.NewLine +
                    Environment.NewLine +
                    "ثبت این رویداد مجاز نیست:" +
                    Environment.NewLine +
                    "\u200F" + FormatEventLine(current);

                ShowValidationMessage(messageInitial);

                return EventSequenceValidationResult.Fail(messageInitial);
            }

            string message =
                $"ثبت این رویداد برای {unitDisplayName} مجاز نیست" +
                Environment.NewLine +
                Environment.NewLine +
                "رویداد قبلی:" +
                Environment.NewLine +
                "\u200F" + FormatEventLine(previous) +
                Environment.NewLine +
                Environment.NewLine +
                "رویداد جدید:" +
                Environment.NewLine +
                "\u200F" + FormatEventLine(current);

            ShowValidationMessage(message);

            return EventSequenceValidationResult.Fail(message);
        }

        return EventSequenceValidationResult.Success();
    }


    private static void ShowValidationMessage(string message)
    {
        MessageBox.Show(
            message,
            "خطا",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button1,
            MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading
        );
    }


    /// <summary>
    /// ساخت خط فنی رویداد با فرمت ثابت و خوانا
    /// </summary>

    private static string FormatEventLine(DailyEventRowModel item)
    {
        string eventType = item.EventType switch
        {
            "__INITIAL_RUNNING__" => "روشن",
            "__INITIAL_STOPPED__" => "خاموش",
            _ => item.EventType
        };

        return $"{item.EventTime} @ {FormatPersianDate(item.DateRep)} : {eventType}";
    }

    /// <summary>
    /// قوانین مجاز بودن تغییر وضعیت واحد
    /// </summary>
    private static bool IsTransitionAllowed(
        string previousEventType,
        string currentEventType)
    {
        previousEventType = NormalizeEventType(previousEventType);
        currentEventType = NormalizeEventType(currentEventType);

        return previousEventType switch
        {
            "__INITIAL_RUNNING__" => currentEventType is "NSD" or "ESD" or "OH",
            "__INITIAL_STOPPED__" => currentEventType is "START" or "OH",

            "START" => currentEventType is "NSD" or "ESD" or "OH",
            "NSD" => currentEventType is "START" or "OH",
            "ESD" => currentEventType is "START" or "OH",
            "OH" => currentEventType is "START",

            _ => false
        };
    }


    /// <summary>
    /// خواندن آخرین رویداد قبل از تاریخ جاری از دیتابیس
    /// رویدادهای همان تاریخ نادیده گرفته می‌شوند چون قرار است جایگزین شوند
    /// </summary>
    private static DailyEventRowModel? LoadPreviousEventBeforeDate(
        SqliteConnection conn,
        string unit,
        long dateRep)
    {
        using SqliteCommand cmd = conn.CreateCommand();

        cmd.CommandText = """
            SELECT date_rep, unit, event_type, event_time, remark
            FROM tbl_events
            WHERE unit = $unit
              AND date_rep < $dateRep
            ORDER BY date_rep DESC, event_time DESC, id DESC
            LIMIT 1;
            """;

        cmd.Parameters.AddWithValue("$unit", unit);
        cmd.Parameters.AddWithValue("$dateRep", dateRep);

        using SqliteDataReader reader = cmd.ExecuteReader();

        if (!reader.Read())
            return null;

        return ReadEvent(reader);
    }

    /// <summary>
    /// خواندن اولین رویداد بعد از تاریخ جاری از دیتابیس
    /// برای جلوگیری از خراب شدن زنجیره بعدی هنگام ویرایش روزانه
    /// </summary>
    private static DailyEventRowModel? LoadNextEventAfterDate(
        SqliteConnection conn,
        string unit,
        long dateRep)
    {
        using SqliteCommand cmd = conn.CreateCommand();

        cmd.CommandText = """
            SELECT date_rep, unit, event_type, event_time, remark
            FROM tbl_events
            WHERE unit = $unit
              AND date_rep > $dateRep
            ORDER BY date_rep ASC, event_time ASC, id ASC
            LIMIT 1;
            """;

        cmd.Parameters.AddWithValue("$unit", unit);
        cmd.Parameters.AddWithValue("$dateRep", dateRep);

        using SqliteDataReader reader = cmd.ExecuteReader();

        if (!reader.Read())
            return null;

        return ReadEvent(reader);
    }

    private static DailyEventRowModel ReadEvent(SqliteDataReader reader)
    {
        return new DailyEventRowModel
        {
            DateRep = Convert.ToInt64(reader["date_rep"]),
            Unit = NormalizeUnit(reader["unit"]?.ToString()),
            EventType = NormalizeEventType(reader["event_type"]?.ToString()),
            EventTime = NormalizeTime(reader["event_time"]?.ToString()),
            Remark = reader["remark"]?.ToString() ?? string.Empty
        };
    }

    private static string NormalizeUnit(string? value)
    {
        return (value ?? string.Empty)
            .Trim()
            .ToUpperInvariant()
            .Replace("UNIT", "U")
            .Replace(" ", "");
    }

    private static string NormalizeEventType(string? value)
    {
        return (value ?? string.Empty)
            .Trim()
            .ToUpperInvariant()
            .Replace(".", "");
    }

    private static string NormalizeTime(string? value)
    {
        string text = (value ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(text))
            return "00:00";

        return TimeSpan.TryParse(text, out TimeSpan ts)
            ? ts.ToString(@"hh\:mm")
            : "00:00";
    }

    private static string FormatPersianDate(long dateRep)
    {
        string s = dateRep.ToString();

        if (s.Length != 8)
            return dateRep.ToString();

        return $"{s[..4]}/{s.Substring(4, 2)}/{s.Substring(6, 2)}";
    }

    private static string GetUnitDisplayName(string unit)
    {
        return unit switch
        {
            "U1" => "واحد ۱",
            "U2" => "واحد ۲",
            "U3" => "واحد ۳",
            "U4" => "واحد ۴",
            _ => unit
        };
    }

}
