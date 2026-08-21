using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Models.Reports;

/// <summary>
/// یک آیتم خلاصه آماری در خروجی گزارش را نگهداری می‌کند.
/// مثال: حداقل فشار ورودی، میانگین دمای خروجی، مجموع مصرف سوخت.
/// </summary>
public sealed class ReportSummaryItem
{
    /// <summary>
    /// کلید پارامتر.
    /// مثال: in_p, flow, turbine_fuel
    /// </summary>
    public string ParameterKey { get; init; } = string.Empty;

    /// <summary>
    /// نام نمایشی پارامتر برای نمایش در UI.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// نوع محاسبه انجام‌شده.
    /// مثال: Min, Max, Avg, Sum
    /// </summary>
    public ReportAggregationType AggregationType { get; init; }

    /// <summary>
    /// مقدار محاسبه‌شده.
    /// </summary>
    public double? Value { get; init; }

    public int ValueCount { get; init; }
}

