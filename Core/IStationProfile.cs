using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace Rah_Negar.Core;

/// <summary>
/// قرارداد پروفایل ایستگاه برای Startup.
/// این کلاس فقط مشخصات ایستگاه را معرفی می‌کند و نباید SQL اجرا کند.
/// </summary>
public interface IStationProfile
{
    StationType StationType { get; }
    string ProfileName { get; }
    int UnitCount { get; }
    bool HasLinePressureColumns { get; }

    Color DefaultAccentColor { get; }

    IStationDataSchema GetDataSchema();
}
