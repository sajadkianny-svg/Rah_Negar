using Rah_Negar.Core.Reporting.Snapshot;

namespace Rah_Negar.Foundation.Application.Reporting.Finalized;

public enum FinalizedReportReadStatus
{
    FoundValid,
    NotFound,
    NotFinalized,
    IntegrityInvalid,
    IntegrityUnsupported,
    LockSnapshotMismatch,
    InfrastructureFailed
}

public sealed record FinalizedReportQuery(string StationId, long PeriodStartMinute,
    long PeriodEndMinute, string PeriodKind);

public sealed record FinalizedReportReadError(string Code, string Message);

public sealed class FinalizedReportReadResult
{
    private FinalizedReportReadResult(FinalizedReportReadStatus status,
        FinalizedReportSnapshot? snapshot, IEnumerable<FinalizedReportReadError> errors)
    {
        Status = status;
        Snapshot = snapshot;
        Errors = Array.AsReadOnly(errors.OrderBy(x => x.Code, StringComparer.Ordinal).ToArray());
    }

    public bool IsSuccess => Status == FinalizedReportReadStatus.FoundValid && Snapshot is not null;
    public FinalizedReportReadStatus Status { get; }
    public FinalizedReportSnapshot? Snapshot { get; }
    public IReadOnlyList<FinalizedReportReadError> Errors { get; }

    public static FinalizedReportReadResult Found(FinalizedReportSnapshot snapshot) =>
        new(FinalizedReportReadStatus.FoundValid,
            snapshot ?? throw new ArgumentNullException(nameof(snapshot)), []);

    public static FinalizedReportReadResult Failure(FinalizedReportReadStatus status,
        string code, string message)
    {
        if (status == FinalizedReportReadStatus.FoundValid)
            throw new ArgumentException("A failure cannot have FoundValid status.", nameof(status));
        return new(status, null, [new FinalizedReportReadError(code, message)]);
    }
}

public interface IFinalizedReportReader
{
    Task<FinalizedReportReadResult> GetBySnapshotIdAsync(string snapshotId,
        CancellationToken cancellationToken = default);
    Task<FinalizedReportReadResult> GetEffectiveAsync(FinalizedReportQuery query,
        CancellationToken cancellationToken = default);
}
