using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace Rah_Negar.Models.Reports;

/// <summary>
/// وضعیت یک روز از نظر کامل بودن داده‌ها را مشخص می‌کند.
/// برای تشخیص روزهای ناقص، بدون داده یا مشکوک استفاده می‌شود.
/// </summary>
public sealed class ReportDailyStatus
{
    /// <summary>
    /// تاریخ روز مورد بررسی.
    /// مثال: 14030115
    /// </summary>
    public long DateRep { get; init; }

    /// <summary>
    /// آیا این روز کامل است یا خیر.
    /// </summary>
    public bool IsComplete { get; init; }

    /// <summary>
    /// آیا این روز هیچ داده‌ای ندارد.
    /// </summary>
    public bool HasNoData { get; init; }

    /// <summary>
    /// تعداد رکوردهای ثبت‌شده در tbl_data.
    /// </summary>
    public int DataRowCount { get; init; }

    /// <summary>
    /// آیا رکورد tbl_unique برای این روز وجود دارد یا خیر.
    /// </summary>
    public bool HasUniqueRow { get; init; }

    /// <summary>
    /// لیست ساعات موجود برای این روز.
    /// مثال: 01, 03, 05, ...
    /// </summary>
    public List<string> ExistingTimes { get; init; } = [];

    /// <summary>
    /// لیست ساعات مورد انتظار که در این روز وجود ندارند.
    /// مثال: 07, 11, ...
    /// </summary>
    public List<string> MissingTimes { get; init; } = [];
}