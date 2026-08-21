using System.Globalization;
using Microsoft.Data.Sqlite;
using Rah_Negar.Data;
using Rah_Negar.Models;

namespace Rah_Negar.Services;

public static class MissingDaysService
{
    /// <summary>
    /// گرفتن روزهای ناقص برای ماه‌های انتخابی از یک سال شمسی
    /// </summary>
    public static MissingDaysResultModel GetMissingDays(int year, List<int> months)
    {
        if (year <= 0)
            throw new ArgumentException("سال نامعتبر است.");

        if (months == null || months.Count == 0)
            throw new ArgumentException("هیچ ماهی انتخاب نشده است.");

        List<int> normalizedMonths = months
            .Where(m => m >= 1 && m <= 12)
            .Distinct()
            .OrderBy(m => m)
            .ToList();

        if (normalizedMonths.Count == 0)
            throw new ArgumentException("لیست ماه‌ها نامعتبر است.");

        HashSet<long> existingDates = GetExistingDatesForYear(year);
        List<string> missingDates = new();
        long dataStartDate = AppSettingsService.GetDataStartDate();

        foreach (int month in normalizedMonths)
        {
            int daysInMonth = GetDaysInShamsiMonth(year, month);

            for (int day = 1; day <= daysInMonth; day++)
            {
                long dateRep = BuildDateRep(year, month, day);

                if (dataStartDate > 0 && dateRep < dataStartDate)
                    continue;

                if (!existingDates.Contains(dateRep))
                {
                    missingDates.Add($"{year:0000}/{month:00}/{day:00}");
                }
            }
        }

        return new MissingDaysResultModel
        {
            Year = year,
            Months = normalizedMonths,
            MissingDates = missingDates
        };
    }

    /// <summary>
    /// گرفتن روزهای ناقص برای یک ماه
    /// </summary>
    public static MissingDaysResultModel GetMissingDaysForMonth(int year, int month)
    {
        return GetMissingDays(year, new List<int> { month });
    }

    /// <summary>
    /// گرفتن روزهای ناقص نیمه اول سال
    /// </summary>
    public static MissingDaysResultModel GetMissingDaysForFirstHalf(int year)
    {
        return GetMissingDays(year, new List<int> { 1, 2, 3, 4, 5, 6 });
    }

    /// <summary>
    /// گرفتن روزهای ناقص نیمه دوم سال
    /// </summary>
    public static MissingDaysResultModel GetMissingDaysForSecondHalf(int year)
    {
        return GetMissingDays(year, new List<int> { 7, 8, 9, 10, 11, 12 });
    }

    /// <summary>
    /// گرفتن روزهای ناقص کل سال
    /// </summary>
    public static MissingDaysResultModel GetMissingDaysForFullYear(int year)
    {
        return GetMissingDays(year, Enumerable.Range(1, 12).ToList());
    }

    /// <summary>
    /// گرفتن تاریخ‌های ثبت‌شده یک سال از جدول tbl_unique
    /// </summary>
    private static HashSet<long> GetExistingDatesForYear(int year)
    {
        using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();

        long fromDate = BuildDateRep(year, 1, 1);
        long toDate = BuildDateRep(year, 12, 31);

        const string sql = @"
SELECT date_rep
FROM tbl_unique
WHERE date_rep BETWEEN @fromDate AND @toDate;";

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@fromDate", fromDate);
        cmd.Parameters.AddWithValue("@toDate", toDate);

        using SqliteDataReader reader = cmd.ExecuteReader();

        HashSet<long> dates = new();

        while (reader.Read())
        {
            dates.Add(Convert.ToInt64(reader["date_rep"]));
        }

        return dates;
    }

    /// <summary>
    /// ساخت مقدار date_rep از سال و ماه و روز
    /// مثال: 14050418
    /// </summary>
    private static long BuildDateRep(int year, int month, int day)
    {
        return (year * 10000L) + (month * 100L) + day;
    }

    /// <summary>
    /// گرفتن تعداد روزهای ماه شمسی به روش رسمی و دقیق
    /// </summary>
    private static int GetDaysInShamsiMonth(int year, int month)
    {
        PersianCalendar pc = new PersianCalendar();
        return pc.GetDaysInMonth(year, month);
    }
}
