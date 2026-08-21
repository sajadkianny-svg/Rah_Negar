using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Models.Reports;

/// <summary>
/// خلاصه محاسبات رویدادها و ساعات کارکرد یک واحد را نگهداری می‌کند.
/// </summary>
public sealed class UnitEventSummary
{
    public string Unit { get; init; } = string.Empty;

    public double RuntimeHours { get; set; }

    public double RuntimeAfterOH { get; set; }

    public int TotalEvents { get; set; }

    public int StartCount { get; set; }

    public int NSDCount { get; set; }

    public int ESDCount { get; set; }
    
    public double EsdExtraHoursTotal { get; set; }

    public double LongestRunHours { get; set; }

    public int DayStartCount { get; set; }

    public int NightStartCount { get; set; }

    public int DayNSDCount { get; set; }

    public int NightNSDCount { get; set; }

    public int DayESDCount { get; set; }

    public int NightESDCount { get; set; }
}

