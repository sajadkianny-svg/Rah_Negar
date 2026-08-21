using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rah_Negar.Models.Reports;

namespace Rah_Negar.Core.Reports;

/// <summary>
/// تعریف کامل رفتار گزارش‌گیری برای هر ایستگاه.
/// این کلاس مشخص می‌کند:
/// - چه واحدهایی وجود دارند
/// - چه پارامترهایی قابل گزارش هستند
/// </summary>
public sealed class ReportStationProfile
{
    /// <summary>
    /// نام ایستگاه
    /// </summary>
    public string StationName { get; init; } = string.Empty;

    /// <summary>
    /// لیست واحدهای ایستگاه (مثلاً U1, U2, ...)
    /// </summary>
    public IReadOnlyList<string> Units { get; init; } = [];

    /// <summary>
    /// پارامترهای قابل گزارش برای این ایستگاه
    /// </summary>
    public IReadOnlyList<ReportParameterDefinition> Parameters { get; init; } = [];
}
