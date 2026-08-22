using Rah_Negar.Foundation.Application.Reporting.Persistence;

namespace Rah_Negar.Foundation.Application.Reporting.Finalization;

public sealed class ReportFinalizationContext
{
    public ReportFinalizationContext(string correlationId, string actorIdentity,
        long expectedLockRevision, string? expectedEffectiveSnapshotId = null)
    {
        CorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? throw new ArgumentException("Correlation identity is required.", nameof(correlationId))
            : correlationId.Trim();
        ActorIdentity = string.IsNullOrWhiteSpace(actorIdentity)
            ? throw new ArgumentException("Actor identity is required.", nameof(actorIdentity))
            : actorIdentity.Trim();
        if (expectedLockRevision < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedLockRevision));
        ExpectedLockRevision = expectedLockRevision;
        ExpectedEffectiveSnapshotId = string.IsNullOrWhiteSpace(expectedEffectiveSnapshotId)
            ? null : expectedEffectiveSnapshotId.Trim();
    }

    public string CorrelationId { get; }
    public string ActorIdentity { get; }
    public long ExpectedLockRevision { get; }
    public string? ExpectedEffectiveSnapshotId { get; }
}

public sealed record ReportFinalizationAuthorizationFailure(string Code, string Message);

public sealed class ReportFinalizationAuthorizationResult
{
    private ReportFinalizationAuthorizationResult(bool isAuthorized,
        IEnumerable<ReportFinalizationAuthorizationFailure> failures)
    {
        IsAuthorized = isAuthorized;
        Failures = Array.AsReadOnly(failures.OrderBy(x => x.Code, StringComparer.Ordinal).ToArray());
    }

    public bool IsAuthorized { get; }
    public IReadOnlyList<ReportFinalizationAuthorizationFailure> Failures { get; }

    public static ReportFinalizationAuthorizationResult Authorized() => new(true, []);
    public static ReportFinalizationAuthorizationResult Rejected(
        params ReportFinalizationAuthorizationFailure[] failures)
    {
        if (failures.Length == 0) throw new ArgumentException("Authorization rejection requires a failure.", nameof(failures));
        return new(false, failures);
    }
}

public interface IReportFinalizationAuthorizer
{
    Task<ReportFinalizationAuthorizationResult> AuthorizeAsync(ReportFinalizationRequest request,
        ReportFinalizationContext context, CancellationToken cancellationToken = default);
}

public enum ReportFinalizationApplicationStatus
{
    Succeeded,
    AlreadyFinalized,
    IncompleteRejected,
    VersionRejected,
    AuthorizationRejected,
    ValidationRejected,
    Conflict,
    InfrastructureFailed
}

public sealed record ReportFinalizationApplicationError(string Code, string Message);

public sealed class ReportFinalizationApplicationResult
{
    public ReportFinalizationApplicationResult(ReportFinalizationApplicationStatus status,
        string? snapshotId, long? lockRevision, IEnumerable<ReportFinalizationApplicationError>? errors = null)
    {
        Status = status;
        SnapshotId = snapshotId;
        LockRevision = lockRevision;
        Errors = Array.AsReadOnly((errors ?? []).OrderBy(x => x.Code, StringComparer.Ordinal).ToArray());
    }

    public bool IsSuccess => Status is ReportFinalizationApplicationStatus.Succeeded
        or ReportFinalizationApplicationStatus.AlreadyFinalized;
    public ReportFinalizationApplicationStatus Status { get; }
    public string? SnapshotId { get; }
    public long? LockRevision { get; }
    public IReadOnlyList<ReportFinalizationApplicationError> Errors { get; }
}

public interface IReportFinalizationService
{
    Task<ReportFinalizationApplicationResult> FinalizeAsync(ReportFinalizationRequest request,
        ReportFinalizationContext context, CancellationToken cancellationToken = default);
}
