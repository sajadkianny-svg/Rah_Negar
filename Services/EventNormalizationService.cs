using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Services;

/// <summary>
/// نرمال‌سازی مقادیر واحد و نوع رویداد برای ذخیره در دیتابیس
/// </summary>
public static class EventNormalizationService
{
    /// <summary>
    /// تبدیل نام واحد از فرم به فرمت استاندارد دیتابیس
    /// </summary>
    public static string NormalizeUnitForDatabase(string rawUnit)
    {
        return rawUnit.Trim().ToUpper() switch
        {
            "U1" => "U1",
            "UNIT1" => "U1",
            "UNIT 1" => "U1",

            "U2" => "U2",
            "UNIT2" => "U2",
            "UNIT 2" => "U2",

            "U3" => "U3",
            "UNIT3" => "U3",
            "UNIT 3" => "U3",

            _ => string.Empty
        };
    }

    /// <summary>
    /// تبدیل نوع رویداد از فرم به فرمت استاندارد دیتابیس
    /// </summary>
    public static string NormalizeEventTypeForDatabase(string rawEventType)
    {
        return rawEventType.Trim().ToUpper() switch
        {
            "START" => "START",
            "OH" => "OH",
            "NSD" => "NSD",
            "ESD" => "ESD",
            _ => string.Empty
        };
    }
}