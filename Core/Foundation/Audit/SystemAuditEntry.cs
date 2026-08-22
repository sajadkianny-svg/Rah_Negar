namespace Rah_Negar.Foundation.Audit;

public sealed record SystemAuditEntry(
    Guid SystemAuditId,
    string ActionCode,
    DateTimeOffset TimestampUtc,
    AuditActor Actor,
    string ResultCode,
    string CorrelationId,
    string? TargetType = null,
    string? TargetId = null,
    bool ManagementAuthorizationSupplied = false,
    string? Reason = null);
