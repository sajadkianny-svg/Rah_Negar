namespace Rah_Negar.Foundation.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateTimeOffset LocalNow { get; }
}
