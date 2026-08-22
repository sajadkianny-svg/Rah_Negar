namespace Rah_Negar.Core.Event;

public enum EventType
{
    Start,
    Nsd,
    Esd,
    Oh
}

public static class EventTypeCode
{
    public static string ToCode(this EventType value) => value switch
    {
        EventType.Start => "START",
        EventType.Nsd => "NSD",
        EventType.Esd => "ESD",
        EventType.Oh => "OH",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static bool TryParse(string? code, out EventType value)
    {
        value = code switch
        {
            "START" => EventType.Start,
            "NSD" => EventType.Nsd,
            "ESD" => EventType.Esd,
            "OH" => EventType.Oh,
            _ => default
        };
        return code is "START" or "NSD" or "ESD" or "OH";
    }
}
