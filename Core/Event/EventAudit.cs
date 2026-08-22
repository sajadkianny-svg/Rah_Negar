namespace Rah_Negar.Core.Event;

public enum EventAuditAction
{
    Add,
    Edit,
    Delete
}

public sealed record EventAudit(
    string AuditId,
    string EventId,
    EventAuditAction Action,
    string? OldValue,
    string? NewValue,
    Guid ActorShiftProfileId,
    string? PersonnelNoSnapshot,
    string? SupervisorDisplayNameSnapshot,
    DateTimeOffset TimestampUtc,
    string Reason,
    string CorrelationId)
{
    public static EventAudit Create(
        string auditId,
        string eventId,
        EventAuditAction action,
        string? oldValue,
        string? newValue,
        Guid actorShiftProfileId,
        string? personnelNoSnapshot,
        string? supervisorDisplayNameSnapshot,
        DateTimeOffset timestampUtc,
        string reason,
        string correlationId)
    {
        if (!EventIdentity.IsCanonicalUlid(auditId))
            throw new ArgumentException("AuditId must be a canonical uppercase ULID.", nameof(auditId));
        if (!EventIdentity.IsCanonicalUlid(eventId))
            throw new ArgumentException("EventId must be a canonical uppercase ULID.", nameof(eventId));
        if (actorShiftProfileId == Guid.Empty)
            throw new ArgumentException("Actor ShiftProfileId is required.", nameof(actorShiftProfileId));
        if (timestampUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Audit timestamp must use UTC.", nameof(timestampUtc));
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        bool shapeIsValid = action switch
        {
            EventAuditAction.Add => oldValue is null && newValue is not null,
            EventAuditAction.Edit => oldValue is not null && newValue is not null,
            EventAuditAction.Delete => oldValue is not null && newValue is null,
            _ => false
        };
        if (!shapeIsValid)
            throw new ArgumentException("Audit snapshots do not match the action.");

        return new EventAudit(
            auditId, eventId, action, oldValue, newValue, actorShiftProfileId,
            personnelNoSnapshot, supervisorDisplayNameSnapshot, timestampUtc,
            reason.Trim(), correlationId.Trim());
    }
}
