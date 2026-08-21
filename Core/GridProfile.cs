using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Core;

/// <summary>
/// مشخصات کامل گرید برای یک ایستگاه
/// </summary>
public sealed class GridProfile
{
    /// <summary>
    /// ستون‌های گرید
    /// </summary>
    public List<GridColumnProfile> Columns { get; set; } = new();

    /// <summary>
    /// تنظیمات ظاهری و رفتاری گرید
    /// </summary>
    public GridVisualProfile Visual { get; set; } = new();

    /// <summary>
    /// اندیس ستون ساعت
    /// </summary>
    public int HourColumnIndex { get; set; }

    /// <summary>
    /// اندیس ستون ratio
    /// </summary>
    public int RatioColumnIndex { get; set; }

    /// <summary>
    /// ستون‌هایی که در سطر AVG باید ظاهراً خاموش شوند
    /// </summary>
    public List<int> AverageHiddenColumns { get; set; } = new();
}
