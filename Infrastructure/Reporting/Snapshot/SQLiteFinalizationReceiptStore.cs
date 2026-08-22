using Microsoft.Data.Sqlite;
using Rah_Negar.Foundation.Application.Reporting.Persistence;
using Rah_Negar.Infrastructure.Database;

namespace Rah_Negar.Infrastructure.Reporting.Snapshot;

public sealed class SQLiteFinalizationReceiptStore : IFinalizationReceiptStore
{
    private readonly ISqliteConnectionFactory _connections;
    public SQLiteFinalizationReceiptStore(ISqliteConnectionFactory connections) =>
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));

    public async Task<ReportFinalizationReceipt?> GetAsync(string finalizationId,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await _connections.OpenConnectionAsync(cancellationToken);
        return await GetAsync(finalizationId, connection, null, cancellationToken);
    }

    public async Task<ReceiptInsertResult> InsertAsync(ReportFinalizationReceipt receipt,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await _connections.OpenConnectionAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        ReceiptInsertResult result = await InsertAsync(receipt, connection, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<ReceiptInsertResult> InsertAsync(ReportFinalizationReceipt receipt,
        SqliteConnection connection, SqliteTransaction transaction, CancellationToken token)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO ReportFinalizationReceipts
                (FinalizationId, RequestFingerprint, SnapshotId, StationId, PeriodStartMinute,
                 PeriodEndMinute, PeriodKind, LockRevision, FinalizedAt, ActorIdentity)
            VALUES ($id, $fingerprint, $snapshot, $station, $start, $end, $kind, $revision, $at, $actor);
            """;
        Bind(command, receipt);
        try
        {
            await command.ExecuteNonQueryAsync(token);
            return new(ReceiptInsertOutcome.Inserted, receipt);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            ReportFinalizationReceipt? existing = await GetAsync(receipt.FinalizationId, connection, transaction, token);
            if (existing is not null && existing.RequestFingerprint == receipt.RequestFingerprint)
                return new(ReceiptInsertOutcome.AlreadyExistsSameRequest, existing);
            return new(ReceiptInsertOutcome.Conflict, existing ?? receipt);
        }
    }

    public static async Task<ReportFinalizationReceipt?> GetAsync(string finalizationId,
        SqliteConnection connection, SqliteTransaction? transaction, CancellationToken token)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT RequestFingerprint, SnapshotId, StationId, PeriodStartMinute, PeriodEndMinute,
                   PeriodKind, LockRevision, FinalizedAt, ActorIdentity
            FROM ReportFinalizationReceipts WHERE FinalizationId = $id;
            """;
        command.Parameters.AddWithValue("$id", finalizationId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) return null;
        return new(finalizationId, reader.GetString(0), reader.GetString(1), reader.GetString(2),
            reader.GetInt64(3), reader.GetInt64(4), reader.GetString(5), reader.GetInt64(6),
            DateTimeOffset.Parse(reader.GetString(7), System.Globalization.CultureInfo.InvariantCulture),
            reader.GetString(8));
    }

    private static void Bind(SqliteCommand command, ReportFinalizationReceipt receipt)
    {
        command.Parameters.AddWithValue("$id", receipt.FinalizationId);
        command.Parameters.AddWithValue("$fingerprint", receipt.RequestFingerprint);
        command.Parameters.AddWithValue("$snapshot", receipt.SnapshotId);
        command.Parameters.AddWithValue("$station", receipt.StationId);
        command.Parameters.AddWithValue("$start", receipt.PeriodStartMinute);
        command.Parameters.AddWithValue("$end", receipt.PeriodEndMinute);
        command.Parameters.AddWithValue("$kind", receipt.PeriodKind);
        command.Parameters.AddWithValue("$revision", receipt.LockRevision);
        command.Parameters.AddWithValue("$at", receipt.FinalizedAt.ToString("O"));
        command.Parameters.AddWithValue("$actor", receipt.ActorIdentity);
    }
}
