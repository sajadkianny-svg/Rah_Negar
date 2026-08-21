using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Services;

public static class UnitMapper
{
    /// <summary>
    /// تبدیل مقدار دیتابیس به مقدار قابل نمایش در UI
    /// مثال: U1 → Unit 1
    /// </summary>
    public static string ToDisplay(string? dbUnit)
    {
        if (string.IsNullOrWhiteSpace(dbUnit))
            return string.Empty;

        string value = dbUnit.Trim().ToUpperInvariant();

        if (value.StartsWith("U") &&
            int.TryParse(value[1..], out int unitNo))
        {
            return $"Unit {unitNo}";
        }

        return dbUnit.Trim();
    }

    /// <summary>
    /// تبدیل مقدار UI به مقدار دیتابیس
    /// مثال: Unit 1 → U1
    /// </summary>
    public static string ToDatabase(string? displayUnit)
    {
        if (string.IsNullOrWhiteSpace(displayUnit))
            return string.Empty;

        string value = displayUnit.Trim().ToUpperInvariant();

        if (value.StartsWith("UNIT "))
        {
            string numberPart = value.Replace("UNIT", "").Trim();

            if (int.TryParse(numberPart, out int unitNo))
                return $"U{unitNo}";
        }

        if (value.StartsWith("U") &&
            int.TryParse(value[1..], out _))
        {
            return value;
        }

        return value;
    }

}