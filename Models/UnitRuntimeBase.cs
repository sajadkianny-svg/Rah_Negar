using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Models
{
    /// <summary>
    /// مقادیر پایه کارکرد هر واحد در شروع کار سیستم
    /// </summary>
    public sealed class UnitRuntimeBase
    {
        public int UnitNo { get; set; }
        public double BaseRuntimeHours { get; set; }
        public double BaseRuntimeAfterOHHours { get; set; }

        public bool InitialIsRunning { get; set; }

        public string InitialStatus { get; set; } = "OFF";

    }
}