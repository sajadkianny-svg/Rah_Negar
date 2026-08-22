using Rah_Negar.Foundation.Time;

namespace Rah_Negar.Infrastructure.Foundation.Time;

public sealed class SystemClock : IClock
{
    public static SystemClock Instance { get; } = new();

    private SystemClock()
    {
    }

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateTimeOffset LocalNow => DateTimeOffset.Now;
}
