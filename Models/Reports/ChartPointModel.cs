using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Models.Reports;

/// <summary>
/// یک نقطه داده برای نمایش در نمودار گزارش.
/// </summary>
public sealed class ChartPointModel
{
    /// <summary>
    /// تاریخ گزارش به‌صورت عددی.
    /// مثال: 14030101
    /// </summary>
    public long DateRep { get; init; }

    /// <summary>
    /// زمان رکورد، در صورت ساعتی بودن داده.
    /// مثال: 01, 03, 05
    /// </summary>
    public string? TimeRep { get; init; }

    /// <summary>
    /// کلید پارامتر.
    /// </summary>
    public string ParameterKey { get; init; } = string.Empty;

    /// <summary>
    /// مقدار نقطه نمودار.
    /// </summary>
    public double? Value { get; init; }
}