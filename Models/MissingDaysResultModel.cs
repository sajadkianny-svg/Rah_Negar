using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Models;

public sealed class MissingDaysResultModel
{
    public int Year { get; set; }

    public List<int> Months { get; set; } = new();

    /// <summary>
    /// تاریخ‌های ناقص به فرمت yyyy/MM/dd شمسی
    /// </summary>
    public List<string> MissingDates { get; set; } = new();

    public bool HasMissingDays => MissingDates.Count > 0;
}