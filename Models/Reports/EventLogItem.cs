using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Rah_Negar.Models.Reports;

/// <summary>
/// یک رویداد ثبت‌شده در جدول tbl_events را برای گزارش‌گیری نگهداری می‌کند.
/// </summary>
public sealed class EventLogItem
{
    /// <summary>
    /// نام واحد.
    /// مثال: U1, U2, U3
    /// </summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>
    /// نوع رویداد.
    /// مثال: START, NSD, ESD, OH
    /// </summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>
    /// تاریخ رویداد به‌صورت عددی.
    /// مثال: 14030115
    /// </summary>
    public long EventDate { get; init; }

    /// <summary>
    /// ساعت رویداد.
    /// مثال: 07:30
    /// </summary>
    public string EventTime { get; init; } = string.Empty;

    /// <summary>
    /// تاریخ و زمان تبدیل‌شده برای مرتب‌سازی و محاسبات.
    /// </summary>
    public DateTime EventDateTime { get; init; }

    /// <summary>
    /// توضیحات رویداد، در صورت وجود.
    /// </summary>
    public string Remark { get; init; } = string.Empty;
}
