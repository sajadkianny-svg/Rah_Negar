using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace Rah_Negar.Models.Reports;

/// <summary>
/// خروجی کامل محاسبات مربوط به رویدادها را نگهداری می‌کند.
/// شامل خلاصه هر واحد، لاگ رویدادها، روزهای سرویس و هشدارهای احتمالی است.
/// </summary>
public sealed class EventReportResult
{
    /// <summary>
    /// خلاصه محاسبات رویدادها برای هر واحد.
    /// </summary>
    public List<UnitEventSummary> UnitSummaries { get; init; } = [];

    /// <summary>
    /// لاگ رویدادهای خوانده‌شده در بازه گزارش.
    /// </summary>
    public List<EventLogItem> EventLogItems { get; init; } = [];

    /// <summary>
    /// روزهای سرویس هر واحد.
    /// کلید: نام واحد مثل U1
    /// مقدار: لیست تاریخ‌های سرویس.
    /// </summary>
    public Dictionary<string, HashSet<long>> ServiceDaysByUnit { get; init; } = [];

    /// <summary>
    /// پیام‌های هشدار یا خطاهای تشخیصی مربوط به گزارش رویدادها.
    /// </summary>
    public List<string> Warnings { get; init; } = [];
}