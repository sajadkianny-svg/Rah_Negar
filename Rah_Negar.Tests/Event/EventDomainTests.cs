using Rah_Negar.Core.Event;

namespace Rah_Negar.Tests.Event;

public sealed class EventDomainTests
{
    private const string EventId = "01ARZ3NDEKTSV4RRFFQ69G5FAV";

    [Fact]
    public void Create_returns_canonical_active_event_for_valid_input()
    {
        Guid actor = Guid.NewGuid();

        EventCreationResult result = Core.Event.Event.Create(
            EventId, "RASHT", "U1", EventType.Start, 14050531, 60,
            100_000L * 1440 + 60, "  start  ", DateTimeOffset.UtcNow, actor);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Event);
        Assert.Equal("start", result.Event.Remark);
        Assert.Equal(EventStatus.Active, result.Event.Status);
        Assert.Equal(1, result.Event.RowVersion);
        Assert.Equal(actor, result.Event.CreatedByShiftProfileId);
    }

    [Fact]
    public void Create_reports_all_structural_errors_without_creating_event()
    {
        EventCreationResult result = Core.Event.Event.Create(
            "bad", " ", "", (EventType)99, 14051340, 1440,
            1, null, DateTimeOffset.Now, Guid.Empty);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Event);
        Assert.Contains(result.Validation.Errors, x => x.Code == "event.id.invalid");
        Assert.Contains(result.Validation.Errors, x => x.Code == "event.type.invalid");
        Assert.Contains(result.Validation.Errors, x => x.Code == "event.date.invalid");
        Assert.Contains(result.Validation.Errors, x => x.Code == "event.time.invalid");
        Assert.Contains(result.Validation.Errors, x => x.Code == "event.actor.required");
    }

    [Fact]
    public void Create_rejects_invalid_persian_month_day()
    {
        EventCreationResult result = Core.Event.Event.Create(
            EventId, "RASHT", "U1", EventType.Start, 14050232, 60,
            100_000L * 1440 + 60, null, DateTimeOffset.UtcNow, Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Validation.Errors, x => x.Code == "event.date.invalid");
    }

    [Theory]
    [InlineData("START", EventType.Start)]
    [InlineData("NSD", EventType.Nsd)]
    [InlineData("ESD", EventType.Esd)]
    [InlineData("OH", EventType.Oh)]
    public void EventType_accepts_only_canonical_codes(string code, EventType expected)
    {
        Assert.True(EventTypeCode.TryParse(code, out EventType actual));
        Assert.Equal(expected, actual);
        Assert.Equal(code, actual.ToCode());
    }

    [Theory]
    [InlineData("start")]
    [InlineData("STOP")]
    [InlineData(" OH")]
    [InlineData("")]
    public void EventType_rejects_aliases_and_noncanonical_codes(string code)
    {
        Assert.False(EventTypeCode.TryParse(code, out _));
    }
}
