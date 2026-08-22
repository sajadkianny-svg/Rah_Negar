using Rah_Negar.Core.Event;

namespace Rah_Negar.Foundation.Application.Event.Commands;

public sealed record EventCommandContext(
    Guid ShiftProfileId,
    string PersonnelNoSnapshot,
    string SupervisorDisplayNameSnapshot,
    string StationId,
    string CorrelationId);

public sealed record AddEventCommand(
    EventCommandContext Context,
    string UnitId,
    EventType EventType,
    int EventDate,
    int EventTime,
    string? Remark,
    string Reason);

public sealed record EditEventCommand(
    EventCommandContext Context,
    string EventId,
    long ExpectedRowVersion,
    string UnitId,
    EventType EventType,
    int EventDate,
    int EventTime,
    string? Remark,
    string Reason);

public sealed record DeleteEventCommand(
    EventCommandContext Context,
    string EventId,
    long ExpectedRowVersion,
    string Reason);

public sealed record EventCommandOutcome(
    string Action,
    string EventId,
    string AuditId,
    long RowVersion,
    IReadOnlyDictionary<string, EventOperationalState> AffectedUnitStates);
