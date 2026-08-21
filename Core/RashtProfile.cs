namespace Rah_Negar.Core;

/// <summary>
/// پروفایل پایه ایستگاه Rasht برای Startup.
/// </summary>
public sealed class RashtProfile : IStationProfile
{
    public StationType StationType => StationType.Rasht;
    public string ProfileName => "Rasht Station";
    public int UnitCount => 3;
    public bool HasLinePressureColumns => true;

    public IStationDataSchema GetDataSchema()
    {
        return new RashtDataSchema();
    }
    public Color DefaultAccentColor => Color.FromArgb(0, 122, 204);
}