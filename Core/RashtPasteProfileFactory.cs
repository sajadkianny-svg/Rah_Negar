using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Core;

/// <summary>
/// سازنده پروفایل Paste مخصوص Rasht Station
/// </summary>
public static class RashtPasteProfileFactory
{
    public static PasteProfile Create()
    {
        return new PasteProfile
        {
            ExpectedRows = 12,
            ExpectedColumns = 16,

            GridStartColumn = 1,
            HourGridColumnIndex = 0,
            AverageRowIndex = 12,

            // ستون‌های Status در جدول مبدا اکسل
            // ستون 6 => اندیس 5
            // ستون 8 => اندیس 7
            // ستون 10 => اندیس 9
            StatusSourceColumns = new List<int> { 5, 7, 9 },

            AllowedStatuses = new List<string> { "A", "M", "S", "OH" },

            // تمام ستون‌ها به جز Status باید عددی باشند
            NumericSourceColumns = new List<int>
            {
                0, 1, 2, 3, 4,
                6, 8, 10,
                11, 12, 13, 14, 15
            },

            // in_p و out_p و ratio در گرید
            RatioSourceInGridColumn = 1,
            RatioSourceOutGridColumn = 2,
            RatioTargetGridColumn = 17,

            // flow در گرید
            FlowGridColumn = 13,

            // ستون‌های status در گرید
            UnitStatusGridColumns = new List<int> { 6, 8, 10 },

            // ستون‌هایی که AVG می‌گیرند
            AverageGridColumns = new List<int>
            {
                1, 2, 3, 4, 5,
                13, 14, 15, 16, 17
            }
        };
    }
}