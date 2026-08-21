using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Models.Reports;

/// <summary>
/// نتیجه بررسی تاریخ وقوع حداقل و حداکثر یک پارامتر گزارش را نگهداری می‌کند.
/// </summary>
public sealed class ExtremeDateItem
{
    /// <summary>
    /// کلید پارامتر در رجیستری گزارش.
    /// </summary>
    public string ParameterKey { get; init; } = string.Empty;

    /// <summary>
    /// عنوان نمایشی پارامتر.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// کمترین مقدار ثبت‌شده.
    /// </summary>
    public double? MinValue { get; init; }

    /// <summary>
    /// تاریخ‌های وقوع کمترین مقدار.
    /// </summary>
    public List<long> MinDates { get; init; } = [];

    /// <summary>
    /// بیشترین مقدار ثبت‌شده.
    /// </summary>
    public double? MaxValue { get; init; }

    /// <summary>
    /// تاریخ‌های وقوع بیشترین مقدار.
    /// </summary>
    public List<long> MaxDates { get; init; } = [];
}



