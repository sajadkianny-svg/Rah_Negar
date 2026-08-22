namespace Rah_Negar.Core.Runtime.Calculation;

/// <summary>A physical Running interval in canonical local chronological minutes.</summary>
public sealed record RuntimeInterval(
    long StartMinute,
    long EndMinute,
    string? StartEventId,
    string? EndEventId,
    bool IsOpenAtCalculationEnd)
{
    public long DurationMinutes => checked(EndMinute - StartMinute);
}
