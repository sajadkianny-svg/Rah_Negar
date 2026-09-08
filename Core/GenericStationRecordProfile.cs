namespace Rah_Negar.Core;

public sealed class GenericStationRecordProfile : IStationUiProfile
{
    private readonly CanonicalProfileDefinition? _definition;

    public GenericStationRecordProfile(string stationName, int unitCount)
    {
        StationName = stationName;
        UnitCount = unitCount;
    }

    public GenericStationRecordProfile(CanonicalProfileDefinition definition)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        StationName = definition.StationName;
        UnitCount = definition.UnitCount;
    }

    public string StationName { get; }
    public int UnitCount { get; }
    public GridProfile GetGridProfile() => _definition is null
        ? GenericGridProfileFactory.Create(UnitCount)
        : GenericGridProfileFactory.Create(_definition);
    public PasteProfile GetPasteProfile() => _definition is null
        ? GenericPasteProfileFactory.Create(UnitCount)
        : GenericPasteProfileFactory.Create(_definition);
}
