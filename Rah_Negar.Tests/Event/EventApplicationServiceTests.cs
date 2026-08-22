using Rah_Negar.Core.Event;
using Rah_Negar.Foundation.Application.Event;
using Rah_Negar.Foundation.Application.Event.Commands;
using Rah_Negar.Foundation.Application.Transactions;
using Rah_Negar.Foundation.Errors;

namespace Rah_Negar.Tests.Event;

public sealed class EventApplicationServiceTests
{
    [Fact]
    public async Task Start_then_stop_commits_events_and_audits()
    {
        await using EventApplicationTestContext context = await EventApplicationTestContext.CreateAsync();
        EventApplicationService service = context.CreateService();

        Result<EventCommandOutcome> start = await service.AddAsync(Add(EventType.Start, 60));
        Result<EventCommandOutcome> stop = await service.AddAsync(Add(EventType.Nsd, 120));

        Assert.True(start.IsSuccess);
        Assert.True(stop.IsSuccess);
        Assert.Equal(2, await context.ScalarLongAsync("SELECT COUNT(*) FROM Events WHERE IsDeleted=0;"));
        Assert.Equal(2, await context.ScalarLongAsync("SELECT COUNT(*) FROM EventAudit;"));
        Assert.Equal("ADD", await context.ScalarStringAsync("SELECT ActionType FROM EventAudit ORDER BY TimestampUtc DESC LIMIT 1;"));
    }

    [Fact]
    public async Task Stopped_to_start_is_accepted()
    {
        await using EventApplicationTestContext context = await EventApplicationTestContext.CreateAsync();

        Result<EventCommandOutcome> result = await context.CreateService().AddAsync(Add(EventType.Start, 60));

        Assert.True(result.IsSuccess);
        Assert.Equal(EventOperationalState.Running, result.Value.AffectedUnitStates["U1"]);
    }

    [Theory]
    [InlineData(EventType.Start, 120, "event.transition.running.start.invalid")]
    [InlineData(EventType.Oh, 120, "event.transition.running.oh.invalid")]
    public async Task Invalid_event_after_start_is_rejected_without_partial_write(
        EventType secondType, int secondTime, string expectedCode)
    {
        await using EventApplicationTestContext context = await EventApplicationTestContext.CreateAsync();
        EventApplicationService service = context.CreateService();
        Assert.True((await service.AddAsync(Add(EventType.Start, 60))).IsSuccess);

        Result<EventCommandOutcome> result = await service.AddAsync(Add(secondType, secondTime));

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error!.Code);
        Assert.Equal(1, await context.ScalarLongAsync("SELECT COUNT(*) FROM Events;"));
        Assert.Equal(1, await context.ScalarLongAsync("SELECT COUNT(*) FROM EventAudit;"));
    }

    [Fact]
    public async Task Esd_while_stopped_is_rejected()
    {
        await using EventApplicationTestContext context = await EventApplicationTestContext.CreateAsync();

        Result<EventCommandOutcome> result = await context.CreateService().AddAsync(Add(EventType.Esd, 60));

        Assert.True(result.IsFailure);
        Assert.Equal("event.transition.stopped.esd.invalid", result.Error!.Code);
        Assert.Equal(0, await context.ScalarLongAsync("SELECT COUNT(*) FROM Events;"));
    }

    [Fact]
    public async Task Duplicate_timestamp_is_rejected_before_database_write()
    {
        await using EventApplicationTestContext context = await EventApplicationTestContext.CreateAsync();
        EventApplicationService service = context.CreateService();
        Assert.True((await service.AddAsync(Add(EventType.Start, 60))).IsSuccess);

        Result<EventCommandOutcome> result = await service.AddAsync(Add(EventType.Nsd, 60));

        Assert.True(result.IsFailure);
        Assert.Equal("event.chain.duplicate-timestamp", result.Error!.Code);
    }

    [Fact]
    public async Task Edit_that_invalidates_later_event_rolls_back()
    {
        await using EventApplicationTestContext context = await EventApplicationTestContext.CreateAsync();
        EventApplicationService service = context.CreateService();
        string startId = (await service.AddAsync(Add(EventType.Start, 60))).Value.EventId;
        await service.AddAsync(Add(EventType.Nsd, 120));

        Result<EventCommandOutcome> result = await service.EditAsync(new EditEventCommand(
            CommandContext(), startId, 1, "U1", EventType.Oh, 14050531, 60, null, "edit"));

        Assert.True(result.IsFailure);
        Assert.Equal("event.transition.stoppedafteroh.nsd.invalid", result.Error!.Code);
        Assert.Equal("START", await context.ScalarStringAsync($"SELECT EventType FROM Events WHERE EventId='{startId}';"));
        Assert.Equal(2, await context.ScalarLongAsync("SELECT COUNT(*) FROM EventAudit;"));
    }

    [Fact]
    public async Task Delete_that_invalidates_later_chain_rolls_back()
    {
        await using EventApplicationTestContext context = await EventApplicationTestContext.CreateAsync();
        EventApplicationService service = context.CreateService();
        await service.AddAsync(Add(EventType.Start, 60));
        string stopId = (await service.AddAsync(Add(EventType.Nsd, 120))).Value.EventId;
        await service.AddAsync(Add(EventType.Start, 180));

        Result<EventCommandOutcome> result = await service.DeleteAsync(
            new DeleteEventCommand(CommandContext(), stopId, 1, "delete"));

        Assert.True(result.IsFailure);
        Assert.Equal("event.transition.running.start.invalid", result.Error!.Code);
        Assert.Equal(0, await context.ScalarLongAsync($"SELECT IsDeleted FROM Events WHERE EventId='{stopId}';"));
        Assert.Equal(3, await context.ScalarLongAsync("SELECT COUNT(*) FROM EventAudit;"));
    }

    [Fact]
    public async Task Add_edit_delete_commit_exact_audit_history()
    {
        await using EventApplicationTestContext context = await EventApplicationTestContext.CreateAsync();
        EventApplicationService service = context.CreateService();
        EventCommandOutcome added = (await service.AddAsync(Add(EventType.Start, 60))).Value;
        Result<EventCommandOutcome> edited = await service.EditAsync(new EditEventCommand(
            CommandContext(), added.EventId, 1, "U1", EventType.Start, 14050531, 70, "edited", "edit"));
        Result<EventCommandOutcome> deleted = await service.DeleteAsync(new DeleteEventCommand(
            CommandContext(), added.EventId, 2, "delete"));

        Assert.True(edited.IsSuccess);
        Assert.True(deleted.IsSuccess);
        Assert.Equal(1, await context.ScalarLongAsync("SELECT IsDeleted FROM Events;"));
        Assert.Equal(3, await context.ScalarLongAsync("SELECT COUNT(*) FROM EventAudit;"));
        Assert.Equal("ADD,EDIT,DELETE", await context.ScalarStringAsync(
            "SELECT group_concat(ActionType, ',') FROM (SELECT ActionType FROM EventAudit ORDER BY TimestampUtc, AuditId);"));
    }

    [Fact]
    public async Task Locked_period_rejects_mutation()
    {
        await using EventApplicationTestContext context = await EventApplicationTestContext.CreateAsync();
        context.Locked = true;

        Result<EventCommandOutcome> result = await context.CreateService().AddAsync(Add(EventType.Start, 60));

        Assert.True(result.IsFailure);
        Assert.Equal("event.period.finalized", result.Error!.Code);
        Assert.Equal(0, await context.ScalarLongAsync("SELECT COUNT(*) FROM Events;"));
    }

    [Fact]
    public async Task Event_before_trusted_baseline_is_rejected()
    {
        await using EventApplicationTestContext context = await EventApplicationTestContext.CreateAsync();
        context.BaselineBoundary = long.MaxValue;

        Result<EventCommandOutcome> result = await context.CreateService().AddAsync(Add(EventType.Start, 60));

        Assert.Equal("event.baseline.before-boundary", result.Error!.Code);
        Assert.Equal(0, await context.ScalarLongAsync("SELECT COUNT(*) FROM Events;"));
    }

    [Fact]
    public async Task Locked_period_rejects_edit_and_delete_of_existing_event()
    {
        await using EventApplicationTestContext context = await EventApplicationTestContext.CreateAsync();
        EventApplicationService service = context.CreateService();
        string eventId = (await service.AddAsync(Add(EventType.Start, 60))).Value.EventId;
        context.Locked = true;

        Result<EventCommandOutcome> edit = await service.EditAsync(new EditEventCommand(
            CommandContext(), eventId, 1, "U1", EventType.Start, 14050531, 70, null, "edit"));
        Result<EventCommandOutcome> delete = await service.DeleteAsync(new DeleteEventCommand(
            CommandContext(), eventId, 1, "delete"));

        Assert.Equal("event.period.finalized", edit.Error!.Code);
        Assert.Equal("event.period.finalized", delete.Error!.Code);
        Assert.Equal(0, await context.ScalarLongAsync("SELECT IsDeleted FROM Events WHERE EventId='" + eventId + "';"));
        Assert.Equal(1, await context.ScalarLongAsync("SELECT COUNT(*) FROM EventAudit;"));
    }

    [Fact]
    public async Task Audit_failure_rolls_back_event_insert()
    {
        await using EventApplicationTestContext context = await EventApplicationTestContext.CreateAsync();
        EventApplicationService service = context.CreateService(new ThrowingAuditRepository());

        Result<EventCommandOutcome> result = await service.AddAsync(Add(EventType.Start, 60));

        Assert.True(result.IsFailure);
        Assert.Equal("event.infrastructure.failure", result.Error!.Code);
        Assert.Equal(0, await context.ScalarLongAsync("SELECT COUNT(*) FROM Events;"));
    }

    private static AddEventCommand Add(EventType type, int time) =>
        new(CommandContext(), "U1", type, 14050531, time, null, "test");

    private static EventCommandContext CommandContext() =>
        new(EventApplicationTestContext.ActorId, "P-101", "Supervisor", "RASHT", Guid.NewGuid().ToString("N"));

    private sealed class ThrowingAuditRepository : IEventAuditRepository
    {
        public Task AddAsync(ITransactionContext transactionContext, EventAudit audit,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Injected audit failure");

        public Task<IReadOnlyList<EventAudit>> GetForEventAsync(
            ITransactionContext transactionContext, string eventId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EventAudit>>(Array.Empty<EventAudit>());
    }
}
