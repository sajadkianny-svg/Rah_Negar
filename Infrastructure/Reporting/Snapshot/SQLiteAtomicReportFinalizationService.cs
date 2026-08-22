using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Rah_Negar.Foundation.Application.Reporting.Finalization;
using Rah_Negar.Foundation.Application.Reporting.Persistence;
using Rah_Negar.Infrastructure.Database;

namespace Rah_Negar.Infrastructure.Reporting.Snapshot;

public sealed class SQLiteAtomicReportFinalizationService : IAtomicReportFinalizationService
{
    private readonly ISqliteConnectionFactory _connections;
    private readonly IReportFinalizationValidator _validator;
    private readonly IReportSnapshotFactory _factory;
    private readonly IReportSnapshotSerializer _serializer;
    private readonly SQLiteReportSnapshotStore _snapshots;
    private readonly SQLiteReportPeriodLockStore _locks;
    private readonly SQLiteFinalizationReceiptStore _receipts;

    public SQLiteAtomicReportFinalizationService(ISqliteConnectionFactory connections,
        IReportFinalizationValidator validator, IReportSnapshotFactory factory,
        IReportSnapshotSerializer serializer, SQLiteReportSnapshotStore snapshots,
        SQLiteReportPeriodLockStore locks, SQLiteFinalizationReceiptStore receipts)
    {
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        _locks = locks ?? throw new ArgumentNullException(nameof(locks));
        _receipts = receipts ?? throw new ArgumentNullException(nameof(receipts));
    }

    public async Task<AtomicFinalizationResult> FinalizeAsync(ReportFinalizationRequest request,
        long expectedLockRevision, string? expectedEffectiveSnapshotId = null,
        CancellationToken cancellationToken = default)
    {
        FinalizationValidationResult validation = _validator.Validate(request);
        if (!validation.IsValid)
            return new(AtomicFinalizationOutcome.ValidationRejected, null, null,
                Array.AsReadOnly(validation.Issues.Select(x => x.Code).ToArray()));
        ReportFinalizationResult candidateResult = _factory.Create(request, validation);
        if (!candidateResult.IsSuccess)
            return new(AtomicFinalizationOutcome.ValidationRejected, null, null,
                Array.AsReadOnly(candidateResult.Issues.Select(x => x.Code).ToArray()));

        var candidate = candidateResult.Snapshot!;
        SerializedReportSnapshot serialized = _serializer.Serialize(candidate);
        string fingerprint = Fingerprint(serialized, expectedLockRevision, expectedEffectiveSnapshotId);

        await using SqliteConnection connection = await _connections.OpenConnectionAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        try
        {
            ReportFinalizationReceipt? existing = await SQLiteFinalizationReceiptStore.GetAsync(
                request.FinalizationId, connection, transaction, cancellationToken);
            if (existing is not null)
            {
                if (existing.RequestFingerprint != fingerprint)
                    throw new PersistenceConflictException(AtomicFinalizationOutcome.ReceiptConflict,
                        "report.receipt.idempotency-conflict");
                await transaction.CommitAsync(cancellationToken);
                return new(AtomicFinalizationOutcome.IdempotentReplay, existing.SnapshotId,
                    existing.LockRevision, Array.Empty<string>());
            }

            SnapshotInsertResult snapshotInsert = await _snapshots.InsertAsync(candidate, connection,
                transaction, cancellationToken);
            if (snapshotInsert.Outcome == SnapshotInsertOutcome.Conflict)
                throw new PersistenceConflictException(AtomicFinalizationOutcome.SnapshotConflict,
                    "report.snapshot.conflict");

            PeriodLockTransitionResult lockResult = await _locks.TryFinalizeAsync(candidate.Identity,
                request.FinalizationId, request.FinalizedAt, request.ActorIdentity, expectedLockRevision,
                expectedEffectiveSnapshotId, connection, transaction, cancellationToken);
            if (!lockResult.Succeeded)
                throw new PersistenceConflictException(AtomicFinalizationOutcome.LockConflict,
                    lockResult.FailureCode ?? "report.lock.conflict");

            long lockRevision = lockResult.Lock!.Revision;
            var receipt = new ReportFinalizationReceipt(request.FinalizationId, fingerprint,
                candidate.Identity.SnapshotId, candidate.Identity.StationId,
                candidate.Identity.PeriodStartMinute, candidate.Identity.PeriodEndMinute,
                candidate.Identity.PeriodKind.ToString(), lockRevision, request.FinalizedAt,
                request.ActorIdentity);
            ReceiptInsertResult receiptInsert = await _receipts.InsertAsync(receipt, connection,
                transaction, cancellationToken);
            if (receiptInsert.Outcome == ReceiptInsertOutcome.Conflict)
                throw new PersistenceConflictException(AtomicFinalizationOutcome.ReceiptConflict,
                    "report.receipt.conflict");

            await transaction.CommitAsync(cancellationToken);
            return new(AtomicFinalizationOutcome.Committed, candidate.Identity.SnapshotId,
                lockRevision, Array.Empty<string>());
        }
        catch (PersistenceConflictException exception)
        {
            await RollbackAsync(transaction);
            return new(exception.Outcome, null, null, new[] { exception.Code });
        }
        catch
        {
            await RollbackAsync(transaction);
            return new(AtomicFinalizationOutcome.InfrastructureFailed, null, null,
                new[] { "report.finalization.infrastructure-failure" });
        }
    }

    private static string Fingerprint(SerializedReportSnapshot snapshot, long expectedRevision,
        string? expectedSnapshotId)
    {
        string value = $"finalization-fingerprint-v1\n{snapshot.CanonicalJson}\n{expectedRevision}\n{expectedSnapshotId ?? ""}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static async Task RollbackAsync(SqliteTransaction transaction)
    {
        try { await transaction.RollbackAsync(CancellationToken.None); }
        catch { }
    }

    private sealed class PersistenceConflictException : Exception
    {
        public PersistenceConflictException(AtomicFinalizationOutcome outcome, string code)
        {
            Outcome = outcome;
            Code = code;
        }
        public AtomicFinalizationOutcome Outcome { get; }
        public string Code { get; }
    }
}
