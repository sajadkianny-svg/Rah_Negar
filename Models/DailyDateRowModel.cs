using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Models;

/// <summary>
/// مدل یک ردیف از جدول tbl_data
/// </summary>
public sealed class DailyDataRowModel
{
    public string TimeRep { get; set; } = string.Empty;

    public double InP { get; set; }
    public double OutP { get; set; }
    public double LineFP { get; set; }
    public double Line40P { get; set; }
    public double Line30P { get; set; }

    public string U1St { get; set; } = string.Empty;
    public int U1Rpm { get; set; }

    public string U2St { get; set; } = string.Empty;
    public int U2Rpm { get; set; }

    public string U3St { get; set; } = string.Empty;
    public int U3Rpm { get; set; }

    public double Rec { get; set; }
    public double Flow { get; set; }
    public double InT { get; set; }
    public double OutT { get; set; }
    public double AmbT { get; set; }
    public double Ratio { get; set; }
}