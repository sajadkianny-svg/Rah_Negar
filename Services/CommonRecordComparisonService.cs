using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rah_Negar.Models;

namespace Rah_Negar.Services;

public static class CommonRecordComparisonService
{
    /// <summary>
    /// بررسی می‌کند آیا بین داده‌های لودشده از tbl_unique
    /// و داده‌های فعلی فرم تفاوتی وجود دارد یا نه.
    /// </summary>
    public static bool HasUniqueChanges(DailyUniqueLoadModel? loaded, DailyUniqueSaveModel current)
    {
        if (loaded == null)
            return true;

        if (loaded.IrFuel != current.IrFuel) return true;
        if (loaded.TurbineFuel != current.TurbineFuel) return true;
        if (loaded.TurbineFlow != current.TurbineFlow) return true;
        if (loaded.NonTurbineFlow != current.NonTurbineFlow) return true;
        if (loaded.Vent != current.Vent) return true;

        return false;
    }

    /// <summary>
    /// بررسی می‌کند آیا بین رویدادهای لودشده از tbl_events
    /// و رویدادهای فعلی فرم تفاوتی وجود دارد یا نه.
    /// ترتیب سطرها نیز در این مقایسه مهم است.
    /// </summary>
    public static bool HasEventsChanges(List<DailyEventRowModel> loaded, List<DailyEventRowModel> current)
    {
        if (loaded.Count != current.Count)
            return true;

        for (int i = 0; i < current.Count; i++)
        {
            DailyEventRowModel a = loaded[i];
            DailyEventRowModel b = current[i];

            if (a.Unit != b.Unit) return true;
            if (a.EventType != b.EventType) return true;
            if (a.EventTime != b.EventTime) return true;
            if (a.Remark != b.Remark) return true;
        }

        return false;
    }
}