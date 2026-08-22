using Rah_Negar.Core.Reporting.Snapshot;

namespace Rah_Negar.Foundation.Application.Reporting.Persistence;

public sealed record SerializedReportSnapshot(
    int SchemaVersion,
    string CanonicalJson,
    SnapshotChecksum Checksum);

public interface IReportSnapshotSerializer
{
    SerializedReportSnapshot Serialize(FinalizedReportSnapshot snapshot);
    FinalizedReportSnapshot Deserialize(SerializedReportSnapshot serialized);
}

public enum SnapshotInsertOutcome { Inserted, AlreadyExistsSameContent, Conflict }
public sealed record SnapshotInsertResult(SnapshotInsertOutcome Outcome, string SnapshotId);

public interface IReportSnapshotStore
{
    Task<SnapshotInsertResult> InsertAsync(FinalizedReportSnapshot snapshot,
        CancellationToken cancellationToken = default);
    Task<FinalizedReportSnapshot?> GetByIdAsync(string snapshotId,
        CancellationToken cancellationToken = default);
}

public enum ReportPeriodLockState { Open, Finalized }
public sealed record ReportPeriodLock(string StationId, long PeriodStartMinute, long PeriodEndMinute,
    string PeriodKind, ReportPeriodLockState State, string? EffectiveSnapshotId, long Revision);
public sealed record PeriodLockTransitionResult(bool Succeeded, ReportPeriodLock? Lock, string? FailureCode);

public interface IReportPeriodLockStore
{
    Task<ReportPeriodLock?> ReadAsync(string stationId, long periodStartMinute, long periodEndMinute,
        string periodKind, CancellationToken cancellationToken = default);
    Task<PeriodLockTransitionResult> TryFinalizeAsync(ReportSnapshotIdentity snapshotIdentity,
        string finalizationId, DateTimeOffset finalizedAt, string actorIdentity,
        long expectedRevision, string? expectedEffectiveSnapshotId = null,
        CancellationToken cancellationToken = default);
}

public sealed record ReportFinalizationReceipt(string FinalizationId, string RequestFingerprint,
    string SnapshotId, string StationId, long PeriodStartMinute, long PeriodEndMinute,
    string PeriodKind, long LockRevision, DateTimeOffset FinalizedAt, string ActorIdentity);

public enum ReceiptInsertOutcome { Inserted, AlreadyExistsSameRequest, Conflict }
public sealed record ReceiptInsertResult(ReceiptInsertOutcome Outcome, ReportFinalizationReceipt Receipt);

public interface IFinalizationReceiptStore
{
    Task<ReportFinalizationReceipt?> GetAsync(string finalizationId,
        CancellationToken cancellationToken = default);
    Task<ReceiptInsertResult> InsertAsync(ReportFinalizationReceipt receipt,
        CancellationToken cancellationToken = default);
}

public enum AtomicFinalizationOutcome
{
    Committed,
    IdempotentReplay,
    ValidationRejected,
    SnapshotConflict,
    LockConflict,
    ReceiptConflict,
    InfrastructureFailed
}

public sealed record AtomicFinalizationResult(AtomicFinalizationOutcome Outcome,
    string? SnapshotId, long? LockRevision, IReadOnlyList<string> Errors)
{
    public bool IsSuccess => Outcome is AtomicFinalizationOutcome.Committed or AtomicFinalizationOutcome.IdempotentReplay;
}

public interface IAtomicReportFinalizationService
{
    Task<AtomicFinalizationResult> FinalizeAsync(
        Finalization.ReportFinalizationRequest request,
        long expectedLockRevision,
        string? expectedEffectiveSnapshotId = null,
        CancellationToken cancellationToken = default);
}
