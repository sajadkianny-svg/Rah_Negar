namespace Rah_Negar.Core;

/// <summary>
/// Runtime station profile created from the persisted canonical definition.
/// </summary>
public sealed class GenericStationProfile : IStationProfile
{
    public GenericStationProfile(CanonicalProfileDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public CanonicalProfileDefinition Definition { get; }
    public StationType StationType => StationType.Custom;
    public string ProfileName => Definition.StationName;
    public int UnitCount => Definition.UnitCount;
    public bool HasLinePressureColumns => Definition.HasLinePressureColumns;
    public Color DefaultAccentColor => Color.FromArgb(0, 122, 204);
    public IStationDataSchema GetDataSchema() => new GenericDataSchema(Definition);
}
