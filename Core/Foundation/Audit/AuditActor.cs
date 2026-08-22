namespace Rah_Negar.Foundation.Audit;

public sealed record AuditActor(
    Guid? ShiftProfileId,
    string? PersonnelNoSnapshot,
    string? SupervisorDisplayNameSnapshot,
    string ActorType);
