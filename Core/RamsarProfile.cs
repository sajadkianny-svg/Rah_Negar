namespace Rah_Negar.Core;

/// <summary>
/// پروفایل پایه ایستگاه Ramsar برای Startup.
/// </summary>
public sealed class RamsarProfile : IStationProfile
{
    public StationType StationType => StationType.Ramsar;
    public string ProfileName => "Ramsar Station";
    public int UnitCount => 4;
    public bool HasLinePressureColumns => false;

    public IStationDataSchema GetDataSchema()
    {
        return new RamsarDataSchema();
    }
    public Color DefaultAccentColor => Color.FromArgb(0, 150, 136);
}
