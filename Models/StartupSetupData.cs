using Rah_Negar.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Models
{
    /// <summary>
    /// داده‌های ورودی فرم Startup که برای ساخت اولیه سیستم لازم هستند.
    /// </summary>
    public sealed class StartupSetupData
    {
        public StationType StationType { get; set; }
        public string ResetPassword { get; set; } = string.Empty;

        /// <summary>
        /// نام ایستگاه
        /// در حالت Custom از TextBox گرفته می‌شود
        /// و در حالت‌های دیگر از نام پروفایل
        /// </summary>
        public string StationName { get; set; } = string.Empty;

        public List<UnitRuntimeBase> UnitRuntimeBases { get; set; } = new();

        public bool EsdExtraRuntimeEnabled { get; set; }

        public double EsdExtraRuntimeHours { get; set; }

        public int ThemeIndex { get; set; }

        public long DataStartDateRep { get; set; }
    }
}
