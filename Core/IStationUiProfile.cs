using Microsoft.Data.Sqlite;

namespace Rah_Negar.Core;

/// <summary>
/// قرارداد اصلی برای رفتار فرم رکورد در هر ایستگاه.
/// </summary>
public interface IStationUiProfile
{
    string StationName { get; }

    GridProfile GetGridProfile();

    PasteProfile GetPasteProfile();

}


