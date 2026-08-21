

using Rah_Negar.Models;


namespace Rah_Negar.Services;

/// <summary>
/// سرویس تبدیل داده‌های فرم Ramsar به مدل ذخیره
/// و بالعکس.
/// این کلاس فقط مسئول mapping است و نباید منطق دیتابیس در آن قرار بگیرد.
/// </summary>
public static class RamsarRecordMapperService
{
    /// <summary>
    /// داده‌های فعلی گرید فرم را به مدل ذخیره Ramsar تبدیل می‌کند.
    /// </summary>
    public static RamsarDailyDataSaveModel BuildSaveModel(List<RamsarRowDto> rows,long dateRep)
    {
        return new RamsarDailyDataSaveModel
        {
            DateRep = dateRep,
            Rows = rows.Select(x => new RamsarDailyDataRowModel
            {
                TimeRep = x.TimeRep,
                InP = x.InP ?? 0,
                OutP = x.OutP ?? 0,

                U1St = x.U1St ?? "",
                U1Rpm = x.U1Rpm ?? 0,

                U2St = x.U2St ?? "",
                U2Rpm = x.U2Rpm ?? 0,

                U3St = x.U3St ?? "",
                U3Rpm = x.U3Rpm ?? 0,

                U4St = x.U4St ?? "",
                U4Rpm = x.U4Rpm ?? 0,

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