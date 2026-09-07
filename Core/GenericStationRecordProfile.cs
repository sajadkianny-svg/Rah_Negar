namespace Rah_Negar.Core;

public sealed class GenericStationRecordProfile : IStationUiProfile
{
    public GenericStationRecordProfile(string stationName, int unitCount)
    {
        StationName = stationName;
        UnitCount = unitCount;
    }

    public string StationName { get; }
    public int UnitCount { get; }
    public GridProfile GetGridProfile() => GenericGridProfileFactory.Create(UnitCount);
    public PasteProfile GetPasteProfile() => GenericPasteProfileFactory.Create(UnitCount);
}
