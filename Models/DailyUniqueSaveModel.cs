using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Models;

/// <summary>
/// مدل داده‌های خلاصه روزانه برای جدول tbl_unique
/// </summary>
public sealed class DailyUniqueSaveModel
{
    public long DateRep { get; set; }

    public double IrFuel { get; set; }
    public double TurbineFuel { get; set; }
    public double TurbineFlow { get; set; }
    public double NonTurbineFlow { get; set; }
    public double Vent { get; set; }
}