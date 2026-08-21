using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Models;

/// <summary>
/// DTO برای یک سطر از گرید Ramsar (بدون وابستگی به UI)
/// </summary>
public class RamsarRowDto
{
    public string TimeRep { get; set; } = "";

    public double? InP { get; set; }
    public double? OutP { get; set; }

    public string? U1St { get; set; }
    public int? U1Rpm { get; set; }

    public string? U2St { get; set; }
    public int? U2Rpm { get; set; }

    public string? U3St { get; set; }
    public int? U3Rpm { get; set; }

    public string? U4St { get; set; }
    public int? U4Rpm { get; set; }

    public double? Rec { get; set; }
    public double? Flow { get; set; }

    public double? InT { get; set; }
    public double? OutT { get; set; }
    public double? AmbT { get; set; }

    public double? Ratio { get; set; }
}