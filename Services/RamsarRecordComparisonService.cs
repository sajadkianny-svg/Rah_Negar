using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using Rah_Negar.Models;

namespace Rah_Negar.Services;

/// <summary>
/// سرویس مقایسه داده‌های tbl_data برای Ramsar Station.
/// </summary>
public static class RamsarRecordComparisonService
{
    /// <summary>
    /// بررسی می‌کند آیا بین داده‌های لودشده و داده‌های فعلی Ramsar تفاوتی وجود دارد یا نه.
    /// </summary>
    public static bool HasDailyDataChanges(List<RamsarDailyDataRowModel> loaded, RamsarDailyDataSaveModel current)
    {
        if (loaded.Count != current.Rows.Count)
            return true;

        for (int i = 0; i < current.Rows.Count; i++)
        {
            RamsarDailyDataRowModel a = loaded[i];
            RamsarDailyDataRowModel b = current.Rows[i];

            if (a.TimeRep != b.TimeRep) return true;
            if (a.InP != b.InP) return true;
            if (a.OutP != b.OutP) return true;
            if (a.U1St != b.U1St) return true;
            if (a.U1Rpm != b.U1Rpm) return true;
            if (a.U2St != b.U2St) return true;
            if (a.U2Rpm != b.U2Rpm) return true;
            if (a.U3St != b.U3St) return true;
            if (a.U3Rpm != b.U3Rpm) return true;
            if (a.U4St != b.U4St) return true;
            if (a.U4Rpm != b.U4Rpm) return true;
            if (a.Rec != b.Rec) return true;
            if (a.Flow != b.Flow) return true;
            if (a.InT != b.InT) return true;
            if (a.OutT != b.OutT) return true;
            if (a.AmbT != b.AmbT) return true;
            if (a.Ratio != b.Ratio) return true;
        }

        return false;
    }
}

