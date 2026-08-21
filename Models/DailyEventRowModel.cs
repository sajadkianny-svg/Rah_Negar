using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Models;

/// <summary>
/// مدل یک رویداد برای جدول tbl_events
/// </summary>
public sealed class DailyEventRowModel
{
    public long DateRep { get; set; }

    public string Unit { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string EventTime { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
}
