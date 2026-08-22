using Microsoft.Data.Sqlite;
using Rah_Negar.Core.Reporting.Snapshot;
using Rah_Negar.Foundation.Application.Reporting.Persistence;
using Rah_Negar.Infrastructure.Database;

namespace Rah_Negar.Infrastructure.Reporting.Snapshot;

public sealed class SQLiteReportSnapshotStore : IReportSnapshotStore
{
    private readonly ISqliteConnectionFactory _connections;
    private readonly IReportSnapshotSerializer _serializer;

    public SQLiteReportSnapshotStore(ISqliteConnectionFactory connections, IReportSnapshotSerializer serializer)
    {
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    public async Task<SnapshotInsertResult> InsertAsync(FinalizedReportSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await _connections.OpenConnectionAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        SnapshotInsertResult result = await InsertAsync(snapshot, connection, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<FinalizedReportSnapshot?> GetByIdAsync(string snapshotId,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await _connections.OpenConnectionAsync(cancellationToken);
        return await GetByIdAsync(snapshotId, connection, null, cancellationToken);
    }

    public async Task<SnapshotInsertResult> InsertAsync(FinalizedReportSnapshot snapshot,
        SqliteConnection connection, SqliteTransaction transaction, CancellationToken token)
    {
        SerializedReportSnapshot serialized = _serializer.Serialize(snapshot);
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO ReportSnapshots
                (SnapshotId, ReportId, StationId, PeriodStartMinute, PeriodEndMinute, PeriodKind,
                 SnapshotSequence, SupersedesSnapshotId, PayloadSchemaVersion, CanonicalJson,
                 ChecksumAlgorithm, IntegrityFormatVersion, ChecksumValue, CanonicalPayloadLength,
                 SourceRevision, FinalizedAt)
            VALUES
                ($snapshotId, $reportId, $stationId, $start, $end, $kind,
                 $sequence, $supersedes, $schema, $json, $algorithm, $integrity,
                 $checksum, $length, $sourceRevision, $finalizedAt);
            """;
        BindSnapshot(command, snapshot, serialized);
        try
        {
            await command.ExecuteNonQueryAsync(token);
            return new(SnapshotInsertOutcome.Inserted, snapshot.Identity.SnapshotId);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            StoredPayload? existing = await ReadStoredAsync(snapshot.Identity.SnapshotId, connection, transaction, token);
            if (existing is not null && existing.SchemaVersion == serialized.SchemaVersion &&
                existing.Json == serialized.CanonicalJson && existing.ChecksumValue == serialized.Checksum.Value)
                return new(SnapshotInsertOutcome.AlreadyExistsSameContent, snapshot.Identity.SnapshotId);
            return new(SnapshotInsertOutcome.Conflict, snapshot.Identity.SnapshotId);
        }
    }

    public async Task<FinalizedReportSnapshot?> GetByIdAsync(string snapshotId, SqliteConnection connection,
        SqliteTransaction? transaction, CancellationToken token)
    {
        StoredPayload? stored = await ReadStoredAsync(snapshotId, connection, transaction, token);
        if (stored is null) return null;
        var checksum = new SnapshotChecksum(stored.Algorithm, stored.IntegrityVersion,
            SnapshotChecksumState.Calculated, stored.ChecksumValue, stored.PayloadLength);
        return _serializer.Deserialize(new(stored.SchemaVersion, stored.Json, checksum));
    }

    private static void BindSnapshot(SqliteCommand command, FinalizedReportSnapshot snapshot,
        SerializedReportSnapshot serialized)
    {
        command.Parameters.AddWithValue("$snapshotId", snapshot.Identity.SnapshotId);
        command.Parameters.AddWithValue("$reportId", snapshot.Identity.ReportId);
        command.Parameters.AddWithValue("$stationId", snapshot.Identity.StationId);
        command.Parameters.AddWithValue("$start", snapshot.Identity.PeriodStartMinute);
        command.Parameters.AddWithValue("$end", snapshot.Identity.PeriodEndMinute);
        command.Parameters.AddWithValue("$kind", snapshot.Identity.PeriodKind.ToString());
        command.Parameters.AddWithValue("$sequence", snapshot.Identity.SnapshotSequence);
        command.Parameters.AddWithValue("$supersedes", (object?)snapshot.Identity.SupersedesSnapshotId ?? DBNull.Value);
        command.Parameters.AddWithValue("$schema", serialized.SchemaVersion);
        command.Parameters.AddWithValue("$json", serialized.CanonicalJson);
        command.Parameters.AddWithValue("$algorithm", serialized.Checksum.Algorithm);
        command.Parameters.AddWithValue("$integrity", serialized.Checksum.IntegrityFormatVersion);
        command.Parameters.AddWithValue("$checksum", serialized.Checksum.Value!);
        command.Parameters.AddWithValue("$length", serialized.Checksum.CanonicalPayloadLength!.Value);
        command.Parameters.AddWithValue("$sourceRevision", snapshot.Evidence.VerifiedSourceRevision);
        command.Parameters.AddWithValue("$finalizedAt", snapshot.Evidence.FinalizedAt.ToString("O"));
    }

    private static async Task<StoredPayload?> ReadStoredAsync(string snapshotId, SqliteConnection connection,
        SqliteTransaction? transaction, CancellationToken token)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT PayloadSchemaVersion, CanonicalJson, ChecksumAlgorithm, IntegrityFormatVersion,
                   ChecksumValue, CanonicalPayloadLength
            FROM ReportSnapshots WHERE SnapshotId = $id;
            """;
        command.Parameters.AddWithValue("$id", snapshotId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) return null;
        return new(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
            reader.GetString(4), reader.GetInt64(5));
    }

    private sealed record StoredPayload(int SchemaVersion, string Json, string Algorithm,
        string IntegrityVersion, string ChecksumValue, long PayloadLength);
}
