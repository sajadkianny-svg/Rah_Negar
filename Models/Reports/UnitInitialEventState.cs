
namespace Rah_Negar.Models.Reports;

/// <summary>
/// وضعیت اولیه هر واحد را در ابتدای بازه گزارش نگهداری می‌کند.
/// این وضعیت از آخرین رویدادهای قبل از شروع بازه یا از تنظیمات اولیه واحد استخراج می‌شود.
/// </summary>
public sealed class UnitInitialEventState
{
    /// <summary>
    /// نام واحد
    /// مثال: U1, U2, U3
    /// </summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>
    /// مشخص می‌کند واحد در ابتدای بازه گزارش روشن فرض می‌شود یا خیر
    /// </summary>
    public bool IsRunningAtPeriodStart { get; init; }

    /// <summary>
    /// مشخص می‌کند قبل از شروع بازه برای این واحد OH ثبت شده یا خیر
    /// این مقدار بیشتر برای تحلیل وضعیت تاریخی واحد استفاده می‌شود
    /// </summary>
    public bool HasSeenOHBeforePeriod { get; init; }

    /// <summary>
    /// مشخص می‌کند در ابتدای بازه، شمارش RuntimeAfterOH نیز باید فعال باشد یا خیر
    /// طبق منطق فعلی، اگر واحد در ابتدای بازه روشن باشد، این مقدار نیز معمولاً true است
    /// </summary>
    public bool IsRunningAfterOHAtPeriodStart { get; init; }
}