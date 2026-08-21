using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Core;

/// <summary>
/// مسئول انتخاب و برگرداندن پروفایل ایستگاه
/// </summary>
public static class ProfileManager
{
    public static IStationProfile GetProfile(StationType stationType)
    {
        return stationType switch
        {
            StationType.Rasht => new RashtProfile(),
            StationType.Ramsar => new RamsarProfile(),
            // فعلاً تا وقتی جزئیات کامل مشخص نشده، عمداً خطا می‌دهیم
            StationType.Custom => throw new NotSupportedException("Custom profile is not implemented yet."),

            _ => throw new InvalidOperationException("Unknown station type.")
        };
    }
}
