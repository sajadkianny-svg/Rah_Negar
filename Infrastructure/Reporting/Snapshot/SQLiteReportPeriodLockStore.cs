using Microsoft.Data.Sqlite;
using Rah_Negar.Core.Reporting.Snapshot;
using Rah_Negar.Foundation.Application.Reporting.Persistence;
using Rah_Negar.Infrastructure.Database;

namespace Rah_Negar.Infrastructure.Reporting.Snapshot;

public sealed class SQLiteReportPeriodLockStore : IReportPeriodLockStore
{
    private readonly ISqliteConnectionFactory _connections;
    public SQLiteReportPeriodLockStore(ISqliteConnectionFactory connections) =>
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));

    public async Task<ReportPeriodLock?> ReadAsync(string stationId, long periodStartMinute,
        long periodEndMinute, string periodKind, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await _connections.OpenConnectionAsync(cancellationToken);
        return await ReadAsync(stationId, periodStartMinute, periodEndMinute, periodKind,
            connection, null, cancellationToken);
    }

    public async Task<PeriodLockTransitionResult> TryFinalizeAsync(ReportSnapshotIdentity snapshotIdentity,
        string finalizationId, DateTimeOffset finalizedAt, string actorIdentity,
        long expectedRevision, string? expectedEffectiveSnapshotId = null,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await _connections.OpenConnectionAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        PeriodLockTransitionResult result = await TryFinalizeAsync(snapshotIdentity, finalizationId,
            finalizedAt, actorIdentity, expectedRevision, expectedEffectiveSnapshotId,
            connection, transaction, cancellationToken);
        if (result.Succeeded) await transaction.CommitAsync(cancellationToken);
        else await transaction.RollbackAsync(cancellationToken);
        return result;
    }

    public async Task<PeriodLockTransitionResult> TryFinalizeAsync(ReportSnapshotIdentity identity,
        string finalizationId, DateTimeOffset finalizedAt, string actorIdentity, long expectedRevision,
        string? expectedEffectiveSnapshotId, SqliteConnection connection, SqliteTransaction transaction,
        CancellationToken token)
    {
        string kind = identity.PeriodKind.ToString();
        int affected;
        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            if (expectedRevision == 0)
            {
                command.CommandText = """
                    INSERT OR IGNORE INTO ReportPeriodLocks
                        (StationId, PeriodStartMinute, PeriodEndMinute, PeriodKind, LockState,
                         EffectiveSnapshotId, Revision, FinalizationId, FinalizedAt, ActorIdentity)
                    VALUES ($station, $start, $end, $kind, 'Finalized', $snapshot, 1,
                            $finalization, $finalizedAt, $actor);
                    """;
            }
            else
            {
                command.CommandText = """
                    UPDATE ReportPeriodLocks
                    SET EffectiveSnapshotId = $snapshot, Revision = Revision + 1,
                        FinalizationId = $finalization, FinalizedAt = $finalizedAt, ActorIdentity = $actor
                    WHERE StationId = $station AND PeriodStartMinute = $start AND PeriodEndMinute = $end
                      AND PeriodKind = $kind AND LockState = 'Finalized' AND Revision = $expectedRevision
                      AND EffectiveSnapshotId = $expectedSnapshot;
                    """;
                command.Parameters.AddWithValue("$expectedRevision", expectedRevision);
                command.Parameters.AddWithValue("$expectedSnapshot", (object?)expectedEffectiveSnapshotId ?? DBNull.Value);
            }
            command.Parameters.AddWithValue("$station", identity.StationId);
            command.Parameters.AddWithValue("$start", identity.PeriodStartMinute);
            command.Parameters.AddWithValue("$end", identity.PeriodEndMinute);
            command.Parameters.AddWithValue("$kind", kind);
            command.Parameters.AddWithValue("$snapshot", identity.SnapshotId);
            command.Parameters.AddWithValue("$finalization", finalizationId);
            command.Parameters.AddWithValue("$finalizedAt", finalizedAt.ToString("O"));
            command.Parameters.AddWithValue("$actor", actorIdentity);
            affected = await command.ExecuteNonQueryAsync(token);
        }
        ReportPeriodLock? value = await ReadAsync(identity.StationId, identity.PeriodStartMinute,
            identity.PeriodEndMinute, kind, connection, transaction, token);
        return affected == 1 ? new(true, value, null) : new(false, value, "report.lock.conflict");
    }

    public static async Task<ReportPeriodLock?> ReadAsync(string stationId, long start, long end,
        string kind, SqliteConnection connection, SqliteTransaction? transaction, CancellationToken token)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT LockState, EffectiveSnapshotId, Revision FROM ReportPeriodLocks
            WHERE StationId = $station AND PeriodStartMinute = $start AND PeriodEndMinute = $end
              AND PeriodKind = $kind;
            """;
        command.Parameters.AddWithValue("$station", stationId);
        command.Parameters.AddWithValue("$start", start);
        command.Parameters.AddWithValue("$end", end);
        command.Parameters.AddWithValue("$kind", kind);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) return null;
        return new(stationId, start, end, kind, Enum.Parse<ReportPeriodLockState>(reader.GetString(0)),
            reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetInt64(2));
    }
}
