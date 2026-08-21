using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Core;

/// <summary>
/// مشخصات کامل ساختار Paste برای هر ایستگاه
/// </summary>
public sealed class PasteProfile
{
    /// <summary>
    /// تعداد سطرهای مورد انتظار از داده کپی‌شده
    /// </summary>
    public int ExpectedRows { get; set; }

    /// <summary>
    /// تعداد ستون‌های مورد انتظار از داده کپی‌شده
    /// </summary>
    public int ExpectedColumns { get; set; }

    /// <summary>
    /// اولین ستون مقصد در گرید برای Paste
    /// چون ستون اول گرید معمولاً مخصوص ساعت است
    /// </summary>
    public int GridStartColumn { get; set; }

    /// <summary>
    /// ستون‌های Status در جدول کپی‌شده از اکسل
    /// این اندیس‌ها بر اساس جدول مبدا هستند، نه گرید
    /// </summary>
    public List<int> StatusSourceColumns { get; set; } = new();

    /// <summary>
    /// مقادیر مجاز برای ستون‌های Status
    /// </summary>
    public List<string> AllowedStatuses { get; set; } = new();

    /// <summary>
    /// ستون‌هایی که باید مقدار عددی داشته باشند
    /// این اندیس‌ها بر اساس جدول مبدا هستند
    /// </summary>
    public List<int> NumericSourceColumns { get; set; } = new();

    /// <summary>
    /// شماره ستون ساعت در گرید
    /// </summary>
    public int HourGridColumnIndex { get; set; }

    /// <summary>
    /// شماره ردیف AVG در گرید
    /// </summary>
    public int AverageRowIndex { get; set; }

    /// <summary>
    /// شماره ستون in_p در گرید
    /// </summary>
    public int RatioSourceInGridColumn { get; set; }

    /// <summary>
    /// شماره ستون out_p در گرید
    /// </summary>
    public int RatioSourceOutGridColumn { get; set; }

    /// <summary>
    /// شماره ستون ratio در گرید
    /// </summary>
    public int RatioTargetGridColumn { get; set; }

    /// <summary>
    /// شماره ستون flow در گرید
    /// </summary>
    public int FlowGridColumn { get; set; }

    /// <summary>
    /// ستون‌های status واحدها در گرید
    /// برای محاسبه turbine / non-turbine flow
    /// </summary>
    public List<int> UnitStatusGridColumns { get; set; } = new();

    /// <summary>
    /// ستون‌هایی که باید برای آن‌ها میانگین محاسبه شود
    /// این اندیس‌ها مربوط به گرید هستند
    /// </summary>
    public List<int> AverageGridColumns { get; set; } = new();
}
