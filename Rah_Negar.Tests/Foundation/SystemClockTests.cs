using Rah_Negar.Infrastructure.Foundation.Time;

namespace Rah_Negar.Tests.Foundation;

public sealed class SystemClockTests
{
    [Fact]
    public void UtcNow_returns_current_utc_time()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow;
        DateTimeOffset actual = SystemClock.Instance.UtcNow;
        DateTimeOffset after = DateTimeOffset.UtcNow;

        Assert.Equal(TimeSpan.Zero, actual.Offset);
        Assert.InRange(actual, before, after);
    }

    [Fact]
    public void LocalNow_uses_local_offset()
    {
        DateTimeOffset actual = SystemClock.Instance.LocalNow;

        Assert.Equal(TimeZoneInfo.Local.GetUtcOffset(actual), actual.Offset);
    }
}
