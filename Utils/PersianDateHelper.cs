
namespace Rah_Negar.Utils;

/// <summary>
/// ابزارهای کمکی برای کار با تاریخ شمسی عددی
/// </summary>
public static class PersianDateHelper
{
    /// <summary>
    /// به تاریخ شمسی عددی، تعداد روز مشخص اضافه می‌کند
    /// مثال ورودی: 14040501
    /// </summary>
    public static long AddDays(long dateRep, int days)
    {
        string value = dateRep.ToString();

        if (value.Length != 8)
            throw new ArgumentException("فرمت تاریخ شمسی معتبر نیست", nameof(dateRep));

        int year = int.Parse(value[..4]);
        int month = int.Parse(value.Substring(4, 2));
        int day = int.Parse(value.Substring(6, 2));

        System.Globalization.PersianCalendar calendar = new();

        DateTime date = calendar.ToDateTime(year, month, day, 0, 0, 0, 0);
        DateTime newDate = date.AddDays(days);

        return calendar.GetYear(newDate) * 10000L
            + calendar.GetMonth(newDate) * 100L
            + calendar.GetDayOfMonth(newDate);
    }

    /// <summary>
    /// تعداد روزهای یک ماه شمسی را بر اساس سال و ماه برمی‌گرداند.
    /// برای ماه ۱۲، کبیسه بودن سال را با PersianCalendar بررسی می‌کند.
    /// </summary>
    public static int GetDaysInMonth(int year, int month)
    {
        if (month < 1 || month > 12)
            throw new ArgumentOutOfRangeException(nameof(month));

        System.Globalization.PersianCalendar calendar = new();

        return calendar.GetDaysInMonth(year, month);
    }
}