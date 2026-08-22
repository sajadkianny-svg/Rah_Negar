using Rah_Negar.Core.Event;
using Rah_Negar.Core.Event.Comparison;
using Rah_Negar.Foundation.Application.Event.Comparison;

namespace Rah_Negar.Tests.Event.Comparison;

public sealed class EventNormalizerTests
{
    [Fact]
    public void Normalize_CanonicalizesRecognizedFormattingWithoutHidingIt()
    {
        var result = new EventNormalizer().Normalize(new EventSourceRecord(
            "legacy-1", "station-rasht", "unit-1", " start ", 14050101, "8:5", 0));

        Assert.True(result.IsSuccess);
        Assert.Equal(EventType.Start, result.Value.EventType);
        Assert.Equal(485, result.Value.EventTimeMinutes);
        Assert.Equal(new[] { "event-type-format", "event-time-format" }, result.Value.FormattingNotes);
    }

    [Theory]
    [InlineData("UNKNOWN", "01:00", "event.comparison.type.invalid")]
    [InlineData("START", "24:00", "event.comparison.time.invalid")]
    public void Normalize_RejectsInvalidSourceValues(string type, string time, string code)
    {
        var result = new EventNormalizer().Normalize(new EventSourceRecord(
            "legacy-1", "station-rasht", "unit-1", type, 14050101, time, 0));

        Assert.True(result.IsFailure);
        Assert.Equal(code, result.Error!.Code);
    }
}
