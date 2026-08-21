using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace Rah_Negar.Core;

/// <summary>
/// سازنده پروفایل Paste مخصوص Ramsar Station.
/// ساختار Paste این ایستگاه مشابه Rasht Station است
/// با این تفاوت که سه ستون line_f_p و line40_p و line30_p وجود ندارند
/// و یک واحد چهارم به آن اضافه شده است.
/// </summary>
public static class RamsarPasteProfileFactory
{
    /// <summary>
    /// PasteProfile اختصاصی Ramsar Station را برمی‌گرداند.
    /// </summary>
    public static PasteProfile Create()
    {
        return new PasteProfile
        {
            ExpectedRows = 12,
            ExpectedColumns = 15,
            GridStartColumn = 1,
            HourGridColumnIndex = 0,
            AverageRowIndex = 12,

            RatioSourceInGridColumn = 1,
            RatioSourceOutGridColumn = 2,
            RatioTargetGridColumn = 16,

            FlowGridColumn = 12,

            StatusSourceColumns = new List<int> { 2, 4, 6, 8 },
            AllowedStatuses = new List<string> { "S", "M", "A", "OH" },
            NumericSourceColumns = new List<int> { 0, 1, 3, 5, 7, 9, 10, 11, 12, 13, 14 },

            UnitStatusGridColumns = new List<int> { 3, 5, 7, 9 },

            AverageGridColumns = new List<int> { 1, 2, 12, 13, 14, 15, 16 }
        };
    }
}