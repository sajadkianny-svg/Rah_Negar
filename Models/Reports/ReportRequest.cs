using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Models.Reports;

/// <summary>
/// نوع بازه یا سطح گزارش‌گیری را مشخص می‌کند.
/// </summary>
public enum ReportGranularity
{
    /// <summary>
    /// گزارش روزانه.
    /// </summary>
    Daily,

    /// <summary>
    /// گزارش ماهانه.
    /// </summary>
    Monthly,

    /// <summary>
    /// گزارش سالانه.
    /// </summary>
    Yearly,

    /// <summary>
    /// گزارش بر اساس بازه دلخواه کاربر.
    /// </summary>
    CustomRange
}

/// <summary>
/// درخواست گزارش‌گیری را نگهداری می‌کند.
/// این کلاس مشخص می‌کند گزارش برای چه بازه‌ای،
/// با چه پارامترهایی و در چه سطحی ساخته شود.
/// </summary>
public sealed class ReportRequest
{
    /// <summary>
    /// تاریخ شروع گزارش به‌صورت عددی.
    /// مثال: 14030101
    /// </summary>
    public long DateFrom { get; set; }

    /// <summary>
    /// تاریخ پایان گزارش به‌صورت عددی.
    /// مثال: 14030131
    /// </summary>
    public long DateTo { get; set; }

    /// <summary>
    /// نوع گزارش‌گیری؛ روزانه، ماهانه، سالانه یا بازه دلخواه.
    /// </summary>
    public ReportGranularity Granularity { get; set; }

    /// <summary>
    /// نام پارامترهایی که کاربر برای گزارش انتخاب کرده است.
    /// مثال: in_p, out_p, flow, ratio
    /// </summary>
    public List<string> SelectedParameters { get; } = [];

    /// <summary>
    /// مشخص می‌کند لیست رویدادها در گزارش آورده شود یا نه.
    /// </summary>
    public bool IncludeEvents { get; set; } = true;

    /// <summary>
    /// مشخص می‌کند روزهای ناقص یا فاقد داده در گزارش بررسی شوند یا نه.
    /// </summary>
    public bool IncludeMissingDays { get; set; } = true;
}