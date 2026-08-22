using System.Text.Json;
using Microsoft.Data.Sqlite;
using Rah_Negar.Core.Event;
using Rah_Negar.Core.Event.Rules;
using Rah_Negar.Foundation.Application.Event.Commands;
using Rah_Negar.Foundation.Application.Event.Policies;
using Rah_Negar.Foundation.Application.Transactions;
using Rah_Negar.Foundation.Errors;
using Rah_Negar.Foundation.Time;

namespace Rah_Negar.Foundation.Application.Event;

public sealed class EventApplicationService
{
    private readonly ITransactionManager _transactions;
    private readonly IEventRepository _events;
    private readonly IEventAuditRepository _audits;
    private readonly IEventChainEvaluator _chains;
    private readonly IEventOwnershipPolicy _ownership;
    private readonly IFinalizedPeriodPolicy _finalized;
    private readonly IOperatingDayPolicy _operatingDay;
    private readonly IEventBaselineStateProvider _baselines;
    private readonly IEventIdGenerator _ids;
    private readonly IEventDateTimeConverter _dateTimes;
    private readonly IClock _clock;

    public EventApplicationService(
        ITransactionManager transactions,
        IEventRepository events,
        IEventAuditRepository audits,
        IEventChainEvaluator chains,
        IEventOwnershipPolicy ownership,
        IFinalizedPeriodPolicy finalized,
        IOperatingDayPolicy operatingDay,
        IEventBaselineStateProvider baselines,
        IEventIdGenerator ids,
        IEventDateTimeConverter dateTimes,
        IClock clock)
    {
        _transactions = transactions;
        _events = events;
        _audits = audits;
        _chains = chains;
        _ownership = ownership;
        _finalized = finalized;
        _operatingDay = operatingDay;
        _baselines = baselines;
        _ids = ids;
        _dateTimes = dateTimes;
        _clock = clock;
    }

    public Task<Result<EventCommandOutcome>> AddAsync(
        AddEventCommand command, CancellationToken cancellationToken = default) =>
        ExecuteSafelyAsync(async () => await _transactions.ExecuteAsync(async (tx, token) =>
        {
            ValidateContext(command.Context, command.Reason);
            await ValidatePoliciesAsync(tx, command.Context.StationId, command.UnitId, command.EventDate, token);
            EventBaseline baseline = await GetBaselineAsync(tx, command.Context.StationId, command.UnitId, token);
            long chronologicalMinute = ConvertDateTime(command.EventDate, command.EventTime);
            DateTimeOffset now = _clock.UtcNow;
            EventCreationResult creation = Core.Event.Event.Create(
                _ids.NewId(), command.Context.StationId, command.UnitId, command.EventType,
                command.EventDate, command.EventTime, chronologicalMinute, command.Remark,
                now, command.Context.ShiftProfileId);
            EnsureValid(creation.Validation);
            Core.Event.Event value = creation.Event!;
            List<Core.Event.Event> chain = (await _events.LoadUnitChainAsync(
                tx, value.StationId, value.UnitId, baseline.EffectiveFromEventDateTime, token)).ToList();
            EnsureAtOrAfterBaseline(value, baseline);
            chain.Add(value);
            EventOperationalState finalState = EnsureValidChain(baseline.InitialState, chain);

            await _events.AddAsync(tx, value, token);
            EventAudit audit = CreateAudit(command.Context, value.EventId, EventAuditAction.Add,
                null, Serialize(value), now, command.Reason);
            await _audits.AddAsync(tx, audit, token);
            return Outcome("ADD", value, audit.AuditId, new Dictionary<string, EventOperationalState>
            {
                [value.UnitId] = finalState
            });
        }, cancellationToken), cancellationToken);

    public Task<Result<EventCommandOutcome>> EditAsync(
        EditEventCommand command, CancellationToken cancellationToken = default) =>
        ExecuteSafelyAsync(async () => await _transactions.ExecuteAsync(async (tx, token) =>
        {
            ValidateContext(command.Context, command.Reason);
            Core.Event.Event current = await GetEditableTargetAsync(
                tx, command.Context, command.EventId, command.ExpectedRowVersion, token);
            if (!await _ownership.IsUnitOwnedByStationAsync(
                tx, current.StationId, current.UnitId, current.EventDate, token))
                Reject("event.ownership.invalid", "The stored Unit does not belong to the Station.");
            await EnsureUnlockedAsync(tx, current.StationId, current.EventDate, token);
            await ValidatePoliciesAsync(tx, current.StationId, command.UnitId, command.EventDate, token);

            long chronologicalMinute = ConvertDateTime(command.EventDate, command.EventTime);
            EventCreationResult creation = Core.Event.Event.Create(
                current.EventId, current.StationId, command.UnitId, command.EventType,
                command.EventDate, command.EventTime, chronologicalMinute, command.Remark,
                current.CreatedAtUtc, current.CreatedByShiftProfileId);
            EnsureValid(creation.Validation);
            Core.Event.Event replacement = creation.Event! with
            {
                UpdatedAtUtc = _clock.UtcNow,
                RowVersion = current.RowVersion + 1
            };

            var states = new Dictionary<string, EventOperationalState>(StringComparer.Ordinal);
            EventBaseline oldBaseline = await GetBaselineAsync(tx, current.StationId, current.UnitId, token);
            List<Core.Event.Event> oldChain = (await _events.LoadUnitChainAsync(
                tx, current.StationId, current.UnitId, oldBaseline.EffectiveFromEventDateTime, token)).Where(x => x.EventId != current.EventId).ToList();
            if (current.UnitId == replacement.UnitId)
            {
                EnsureAtOrAfterBaseline(replacement, oldBaseline);
                oldChain.Add(replacement);
                states[current.UnitId] = EnsureValidChain(oldBaseline.InitialState, oldChain);
            }
            else
            {
                states[current.UnitId] = EnsureValidChain(oldBaseline.InitialState, oldChain);
                EventBaseline newBaseline = await GetBaselineAsync(tx, current.StationId, replacement.UnitId, token);
                List<Core.Event.Event> newChain = (await _events.LoadUnitChainAsync(
                    tx, current.StationId, replacement.UnitId, newBaseline.EffectiveFromEventDateTime, token)).ToList();
                EnsureAtOrAfterBaseline(replacement, newBaseline);
                newChain.Add(replacement);
                states[replacement.UnitId] = EnsureValidChain(newBaseline.InitialState, newChain);
            }

            if (!await _events.UpdateAsync(tx, replacement, command.ExpectedRowVersion, token))
                Reject("event.concurrency.conflict", "The Event was changed by another operation.");
            EventAudit audit = CreateAudit(command.Context, current.EventId, EventAuditAction.Edit,
                Serialize(current), Serialize(replacement), replacement.UpdatedAtUtc!.Value, command.Reason);
            await _audits.AddAsync(tx, audit, token);
            return Outcome("EDIT", replacement, audit.AuditId, states);
        }, cancellationToken), cancellationToken);

    public Task<Result<EventCommandOutcome>> DeleteAsync(
        DeleteEventCommand command, CancellationToken cancellationToken = default) =>
        ExecuteSafelyAsync(async () => await _transactions.ExecuteAsync(async (tx, token) =>
        {
            ValidateContext(command.Context, command.Reason);
            Core.Event.Event current = await GetEditableTargetAsync(
                tx, command.Context, command.EventId, command.ExpectedRowVersion, token);
            if (!await _ownership.IsUnitOwnedByStationAsync(
                tx, current.StationId, current.UnitId, current.EventDate, token))
                Reject("event.ownership.invalid", "The stored Unit does not belong to the Station.");
            await EnsureUnlockedAsync(tx, current.StationId, current.EventDate, token);
            EventBaseline baseline = await GetBaselineAsync(tx, current.StationId, current.UnitId, token);
            List<Core.Event.Event> chain = (await _events.LoadUnitChainAsync(
                tx, current.StationId, current.UnitId, baseline.EffectiveFromEventDateTime, token)).Where(x => x.EventId != current.EventId).ToList();
            EventOperationalState finalState = EnsureValidChain(baseline.InitialState, chain);
            DateTimeOffset now = _clock.UtcNow;
            if (!await _events.TombstoneAsync(tx, current.EventId, command.ExpectedRowVersion,
                now, command.Context.ShiftProfileId, token))
                Reject("event.concurrency.conflict", "The Event was changed by another operation.");
            EventAudit audit = CreateAudit(command.Context, current.EventId, EventAuditAction.Delete,
                Serialize(current), null, now, command.Reason);
            await _audits.AddAsync(tx, audit, token);
            Core.Event.Event deleted = current with
            {
                Status = EventStatus.Deleted,
                DeletedAtUtc = now,
                DeletedByShiftProfileId = command.Context.ShiftProfileId,
                RowVersion = current.RowVersion + 1
            };
            return Outcome("DELETE", deleted, audit.AuditId,
                new Dictionary<string, EventOperationalState> { [current.UnitId] = finalState });
        }, cancellationToken), cancellationToken);

    private async Task ValidatePoliciesAsync(
        ITransactionContext tx, string stationId, string unitId, int eventDate, CancellationToken token)
    {
        if (!await _ownership.IsUnitOwnedByStationAsync(tx, stationId, unitId, eventDate, token))
            Reject("event.ownership.invalid", "The Unit does not belong to the Station.");
        if (!await _operatingDay.IsEligibleAsync(tx, stationId, eventDate, token))
            Reject("event.operating-day.ineligible", "The Event date is not eligible for entry.");
        await EnsureUnlockedAsync(tx, stationId, eventDate, token);
    }

    private async Task EnsureUnlockedAsync(
        ITransactionContext tx, string stationId, int eventDate, CancellationToken token)
    {
        if (await _finalized.IsLockedAsync(tx, stationId, eventDate, token))
            Reject("event.period.finalized", "The Event period is finalized and locked.");
    }

    private async Task<EventBaseline> GetBaselineAsync(
        ITransactionContext tx, string stationId, string unitId, CancellationToken token) =>
        await _baselines.GetBaselineAsync(tx, stationId, unitId, token)
            ?? throw new EventCommandRejectedException(
                ApplicationError.Create("event.baseline.missing", "A trusted runtime baseline is required."));

    private static void EnsureAtOrAfterBaseline(Core.Event.Event value, EventBaseline baseline)
    {
        if (value.EventDateTime < baseline.EffectiveFromEventDateTime)
            Reject("event.baseline.before-boundary", "The Event occurs before the trusted runtime baseline.");
    }

    private async Task<Core.Event.Event> GetEditableTargetAsync(
        ITransactionContext tx, EventCommandContext context, string eventId,
        long expectedRowVersion, CancellationToken token)
    {
        Core.Event.Event? value = await _events.GetByIdAsync(tx, eventId, token);
        if (value is null || value.Status != EventStatus.Active || value.StationId != context.StationId)
            Reject("event.not-found", "The active Event was not found in the current Station.");
        if (expectedRowVersion <= 0 || value!.RowVersion != expectedRowVersion)
            Reject("event.concurrency.conflict", "The Event version is stale.");
        return value!;
    }

    private EventOperationalState EnsureValidChain(
        EventOperationalState baseline, IReadOnlyList<Core.Event.Event> chain)
    {
        EventChainEvaluationResult result = _chains.Evaluate(baseline, chain);
        if (!result.IsValid)
            Reject(result.FailureCode ?? "event.chain.invalid", "The complete Event chain is invalid.");
        return result.FinalState;
    }

    private static void EnsureValid(EventValidationResult result)
    {
        if (!result.IsValid)
            throw new EventCommandRejectedException(ApplicationError.Create(
                result.Errors[0].Code, result.Errors[0].Message));
    }

    private long ConvertDateTime(int date, int time)
    {
        try { return _dateTimes.ToChronologicalMinute(date, time); }
        catch (ArgumentOutOfRangeException) { Reject("event.datetime.invalid", "The Persian date or time is invalid."); throw; }
    }

    private static void ValidateContext(EventCommandContext context, string reason)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.ShiftProfileId == Guid.Empty || string.IsNullOrWhiteSpace(context.PersonnelNoSnapshot) ||
            string.IsNullOrWhiteSpace(context.StationId) ||
            string.IsNullOrWhiteSpace(context.CorrelationId))
            Reject("event.identity.invalid", "A trusted Shift Profile, Station, and correlation are required.");
        if (string.IsNullOrWhiteSpace(reason))
            Reject("event.reason.required", "A reason is required.");
    }

    private EventAudit CreateAudit(
        EventCommandContext context, string eventId, EventAuditAction action,
        string? oldValue, string? newValue, DateTimeOffset timestamp, string reason) =>
        EventAudit.Create(_ids.NewId(), eventId, action, oldValue, newValue,
            context.ShiftProfileId, context.PersonnelNoSnapshot,
            context.SupervisorDisplayNameSnapshot, timestamp, reason, context.CorrelationId);

    private static string Serialize(Core.Event.Event value) => JsonSerializer.Serialize(new
    {
        SchemaVersion = 1,
        value.EventId,
        value.StationId,
        value.UnitId,
        EventType = value.EventType.ToCode(),
        value.EventDate,
        value.EventTime,
        value.EventDateTime,
        value.Remark,
        CreatedAt = value.CreatedAtUtc.ToUniversalTime().ToString("O"),
        value.CreatedByShiftProfileId,
        UpdatedAt = value.UpdatedAtUtc?.ToUniversalTime().ToString("O"),
        value.Status,
        DeletedAt = value.DeletedAtUtc?.ToUniversalTime().ToString("O"),
        value.DeletedByShiftProfileId,
        value.RowVersion
    });

    private static EventCommandOutcome Outcome(
        string action, Core.Event.Event value, string auditId,
        IReadOnlyDictionary<string, EventOperationalState> states) =>
        new(action, value.EventId, auditId, value.RowVersion, states);

    private static async Task<Result<EventCommandOutcome>> ExecuteSafelyAsync(
        Func<Task<EventCommandOutcome>> operation, CancellationToken token)
    {
        try { return Result<EventCommandOutcome>.Success(await operation().ConfigureAwait(false)); }
        catch (EventCommandRejectedException exception) { return Result<EventCommandOutcome>.Failure(exception.Error); }
        catch (SqliteException) { return Result<EventCommandOutcome>.Failure(ApplicationError.Create("event.persistence.failure", "The Event operation could not be saved.")); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { return Result<EventCommandOutcome>.Failure(ApplicationError.Create("event.infrastructure.failure", "The Event operation failed safely.")); }
    }

    private static void Reject(string code, string message) =>
        throw new EventCommandRejectedException(ApplicationError.Create(code, message));

    private sealed class EventCommandRejectedException(ApplicationError error) : Exception(error.Message)
    {
        public ApplicationError Error { get; } = error;
    }
}
