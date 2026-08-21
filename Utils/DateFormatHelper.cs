namespace Rah_Negar.Utils;

/// <summary>
/// ابزارهای کمکی برای فرمت تاریخ
/// </summary>
public static class DateFormatHelper
{
    /// <summary>
    /// تبدیل تاریخ عددی به فرمت نمایشی yyyy/MM/dd
    /// </summary>
    public static string FormatDateRep(long dateRep)
    {
        string value = dateRep.ToString();

        if (value.Length != 8)
            return dateRep.ToString();

        return $"{value[..4]}/{value.Substring(4, 2)}/{value.Substring(6, 2)}";
    }
}