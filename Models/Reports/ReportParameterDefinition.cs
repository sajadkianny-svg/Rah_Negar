using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace Rah_Negar.Models.Reports;

/// <summary>
/// نوع پارامتر را مشخص می‌کند (برای دسته‌بندی در UI و تحلیل).
/// </summary>
public enum ReportParameterCategory
{
    Pressure,
    Temperature,
    Flow,
    Ratio,
    Recycle,
    Status,
    RPM,
    Fuel
}

/// <summary>
/// نوع تجمیع (Aggregation) قابل اعمال روی پارامتر.
/// </summary>
public enum ReportAggregationType
{
    Min,
    Max,
    Avg,
    Sum
}

/// <summary>
/// تعریف کامل یک پارامتر قابل استفاده در سیستم گزارش‌گیری.
/// مشخص می‌کند هر پارامتر از چه نوعی است،
/// از کدام فیلد دیتابیس خوانده می‌شود
/// و چه نوع محاسباتی روی آن انجام می‌شود.
/// </summary>
public sealed class ReportParameterDefinition
{
    /// <summary>
    /// نام داخلی پارامتر (کلید یکتا).
    /// مثال: in_p
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// نام نمایشی برای UI.
    /// مثال: "فشار ورودی"
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// دسته‌بندی پارامتر (فشار، دما، دبی و...).
    /// </summary>
    public ReportParameterCategory Category { get; init; }

    /// <summary>
    /// نام ستون در جدول tbl_data (در صورت وجود).
    /// </summary>
    public string? DataColumnName { get; init; }

    /// <summary>
    /// نام ستون در جدول tbl_unique (برای پارامترهای روزانه مثل fuel).
    /// </summary>
    public string? UniqueColumnName { get; init; }

    /// <summary>
    /// لیست نوع محاسبات قابل انجام روی این پارامتر.
    /// </summary>
    public List<ReportAggregationType> SupportedAggregations { get; init; } = [];

    /// <summary>
    /// مشخص می‌کند این پارامتر از نوع تجمعی (مثل fuel) است یا ساعتی.
    /// </summary>
    public bool IsCumulative { get; init; }
}