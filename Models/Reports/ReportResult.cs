using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Models.Reports;

/// <summary>
/// خروجی نهایی سیستم گزارش‌گیری را نگهداری می‌کند.
/// شامل خلاصه آماری، داده‌های نمودار، وضعیت روزها و پیام‌های تشخیصی است.
/// </summary>
public sealed class ReportResult
{
    /// <summary>
    /// درخواست اصلی که این گزارش بر اساس آن ساخته شده است.
    /// </summary>
    public ReportRequest Request { get; init; } = new();

    /// <summary>
    /// آیتم‌های خلاصه آماری گزارش.
    /// مثال: حداقل، حداکثر، میانگین یا مجموع پارامترها.
    /// </summary>
    public List<ReportSummaryItem> SummaryItems { get; init; } = [];

    /// <summary>
    /// نقاط داده آماده برای نمایش در نمودار.
    /// </summary>
    public List<ChartPointModel> ChartPoints { get; init; } = [];

    /// <summary>
    /// وضعیت کامل یا ناقص بودن روزهای بازه گزارش.
    /// </summary>
    public List<ReportDailyStatus> DailyStatuses { get; init; } = [];

    /// <summary>
    /// پیام‌های هشدار یا نکات تشخیصی گزارش.
    /// مثال: وجود روز ناقص، نبود داده، یا نبود پارامتر معتبر.
    /// </summary>
    public List<string> Warnings { get; init; } = [];

    /// <summary>
    /// مشخص می‌کند آیا گزارش داده قابل نمایش دارد یا خیر.
    /// </summary>
    public bool HasData => SummaryItems.Count > 0 || ChartPoints.Count > 0;
}