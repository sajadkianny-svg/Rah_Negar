using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Models;

/// <summary>
/// مدل کامل داده‌های روزانه برای ذخیره در tbl_data
/// </summary>
public sealed class DailyDataSaveModel
{
    /// <summary>
    /// تاریخ گزارش به‌صورت عددی
    /// مثال: 14050418
    /// </summary>
    public long DateRep { get; set; }

    /// <summary>
    /// ردیف‌های اصلی داده
    /// </summary>
    public List<DailyDataRowModel> Rows { get; set; } = new();
}
