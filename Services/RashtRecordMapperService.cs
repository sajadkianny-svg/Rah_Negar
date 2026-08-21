using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rah_Negar.Models;

namespace Rah_Negar.Services;

/// <summary>
/// سرویس تبدیل DTOهای رشت به مدل ذخیره tbl_data.
/// این کلاس هیچ وابستگی به فرم یا DataGridView ندارد.
/// </summary>
public static class RashtRecordMapperService
{
    /// <summary>
    /// تبدیل داده‌های استخراج‌شده از گرید رشت به مدل ذخیره tbl_data.
    /// </summary>
    public static DailyDataSaveModel BuildSaveModel(List<RashtRowDto> rows, long dateRep)
    {
        return new DailyDataSaveModel
        {
            DateRep = dateRep,
            Rows = rows.Select(x => new DailyDataRowModel
            {
                TimeRep = x.TimeRep,

                InP = x.InP ?? 0,
                OutP = x.OutP ?? 0,
                LineFP = x.LineFP ?? 0,
                Line40P = x.Line40P ?? 0,
                Line30P = x.Line30P ?? 0,

                U1St = x.U1St ?? "",
                U1Rpm = x.U1Rpm ?? 0,

                U2St = x.U2St ?? "",
                U2Rpm = x.U2Rpm ?? 0,

                U3St = x.U3St ?? "",
                U3Rpm = x.U3Rpm ?? 0,

                Rec = x.Rec ?? 0,
                Flow = x.Flow ?? 0,

                InT = x.InT ?? 0,
                OutT = x.OutT ?? 0,
                AmbT = x.AmbT ?? 0,

                Ratio = x.Ratio ?? 0
            }).ToList()
        };
    }
}
