using Rah_Negar.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// مدل تنظیمات اصلی برنامه
/// </summary>
public sealed class AppSettingsModel
{
    public int Id { get; set; }

    public bool IsInitialized { get; set; }

    public StationType StationType { get; set; }

    public string StationName { get; set; } = string.Empty;

    public string UserResetPasswordHash { get; set; } = string.Empty;

    public string UserResetPasswordSalt { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// رنگ فعلی تم به‌صورت ARGB.
    /// اگر null باشد، رنگ پیش‌فرض پروفایل استفاده می‌شود.
    /// </summary>
    public int ThemeIndex { get; set; }

    /// <summary>
    /// فعال بودن افزودن ساعت کارکرد بعد از NSD.
    /// </summary>
    public bool EsdExtraRuntimeEnabled { get; set; }

    /// <summary>
    /// مقدار ساعت اضافه‌شونده به کارکرد واحد بعد از هر NSD.
    /// </summary>
    public double EsdExtraRuntimeHours { get; set; }

    public long DataStartDateRep {  get; set; } 
}

