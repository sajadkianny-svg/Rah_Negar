using Rah_Negar.Core.Event;

namespace Rah_Negar.Tests.Event;

public sealed class EventAuditTests
{
    [Fact]
    public void Create_builds_add_audit_with_shift_identity_snapshots()
    {
        Guid actor = Guid.NewGuid();
        EventAudit audit = EventAudit.Create(
            "01ARZ3NDEKTSV4RRFFQ69G5FAW",
            "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            EventAuditAction.Add,
            null,
            "{\"schemaVersion\":1}",
            actor,
            "P-101",
            "Supervisor",
            DateTimeOffset.UtcNow,
            "Initial entry",
            "correlation-1");

        Assert.Equal(actor, audit.ActorShiftProfileId);
        Assert.Equal("P-101", audit.PersonnelNoSnapshot);
        Assert.Equal(EventAuditAction.Add, audit.Action);
    }

    [Fact]
    public void Create_rejects_snapshot_shape_that_does_not_match_action()
    {
        Assert.Throws<ArgumentException>(() => EventAudit.Create(
            "01ARZ3NDEKTSV4RRFFQ69G5FAW",
            "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            EventAuditAction.Delete,
            null,
            "{}",
            Guid.NewGuid(),
            null,
            null,
            DateTimeOffset.UtcNow,
            "Delete",
            "correlation-1"));
    }
}
