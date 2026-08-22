using Microsoft.Data.Sqlite;
using Rah_Negar.Core.Event;
using Rah_Negar.Foundation.Application.Event;
using Rah_Negar.Foundation.Application.Transactions;

namespace Rah_Negar.Infrastructure.Event;

public sealed class SqliteEventRepository : IEventRepository
{
    private const string Columns = """
        EventId, StationId, UnitId, EventType, EventDate, EventTime, EventDateTime,
        Remark, CreatedAt, CreatedByShiftProfileId, UpdatedAt, IsDeleted,
        DeletedAt, DeletedByShiftProfileId, RowVersion
        """;

    public async Task<Core.Event.Event?> GetByIdAsync(
        ITransactionContext transactionContext, string eventId,
        CancellationToken cancellationToken = default)
    {
        (SqliteConnection connection, SqliteTransaction transaction) = GetSqlite(transactionContext);
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT {Columns} FROM Events WHERE EventId=$id;";
        command.Parameters.AddWithValue("$id", eventId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Map(reader) : null;
    }

    public async Task<IReadOnlyList<Core.Event.Event>> LoadUnitChainAsync(
        ITransactionContext transactionContext, string stationId, string unitId,
        long baselineBoundaryEventDateTime,
        CancellationToken cancellationToken = default)
    {
        (SqliteConnection connection, SqliteTransaction transaction) = GetSqlite(transactionContext);
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT {Columns} FROM Events
            WHERE StationId=$station AND UnitId=$unit AND IsDeleted=0
                AND EventDateTime >= $baselineBoundary
            ORDER BY EventDateTime, EventId;
            """;
        command.Parameters.AddWithValue("$station", stationId);
        command.Parameters.AddWithValue("$unit", unitId);
        command.Parameters.AddWithValue("$baselineBoundary", baselineBoundaryEventDateTime);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var values = new List<Core.Event.Event>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            values.Add(Map(reader));
        return values;
    }

    public async Task AddAsync(
        ITransactionContext transactionContext, Core.Event.Event value,
        CancellationToken cancellationToken = default)
    {
        (SqliteConnection connection, SqliteTransaction transaction) = GetSqlite(transactionContext);
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Events (
                EventId, StationId, UnitId, EventType, EventDate, EventTime, EventDateTime,
                Remark, CreatedAt, CreatedByShiftProfileId, UpdatedAt, IsDeleted,
                DeletedAt, DeletedByShiftProfileId, RowVersion)
            VALUES ($id,$station,$unit,$type,$date,$time,$dateTime,$remark,$createdAt,
                $createdBy,$updatedAt,$deleted,$deletedAt,$deletedBy,$version);
            """;
        BindAll(command, value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> UpdateAsync(
        ITransactionContext transactionContext, Core.Event.Event value, long expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        (SqliteConnection connection, SqliteTransaction transaction) = GetSqlite(transactionContext);
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE Events SET UnitId=$unit, EventType=$type, EventDate=$date,
                EventTime=$time, EventDateTime=$dateTime, Remark=$remark,
                UpdatedAt=$updatedAt, RowVersion=$version
            WHERE EventId=$id AND StationId=$station AND IsDeleted=0 AND RowVersion=$expected;
            """;
        BindAll(command, value);
        command.Parameters.AddWithValue("$expected", expectedRowVersion);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public async Task<bool> TombstoneAsync(
        ITransactionContext transactionContext, string eventId, long expectedRowVersion,
        DateTimeOffset deletedAtUtc, Guid deletedByShiftProfileId,
        CancellationToken cancellationToken = default)
    {
        (SqliteConnection connection, SqliteTransaction transaction) = GetSqlite(transactionContext);
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE Events SET IsDeleted=1, DeletedAt=$deletedAt,
                DeletedByShiftProfileId=$deletedBy, RowVersion=RowVersion+1
            WHERE EventId=$id AND IsDeleted=0 AND RowVersion=$expected;
            """;
        command.Parameters.AddWithValue("$deletedAt", FormatUtc(deletedAtUtc));
        command.Parameters.AddWithValue("$deletedBy", deletedByShiftProfileId.ToString("D"));
        command.Parameters.AddWithValue("$id", eventId);
        command.Parameters.AddWithValue("$expected", expectedRowVersion);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    private static void BindAll(SqliteCommand command, Core.Event.Event value)
    {
        command.Parameters.AddWithValue("$id", value.EventId);
        command.Parameters.AddWithValue("$station", value.StationId);
        command.Parameters.AddWithValue("$unit", value.UnitId);
        command.Parameters.AddWithValue("$type", value.EventType.ToCode());
        command.Parameters.AddWithValue("$date", value.EventDate);
        command.Parameters.AddWithValue("$time", value.EventTime);
        command.Parameters.AddWithValue("$dateTime", value.EventDateTime);
        command.Parameters.AddWithValue("$remark", (object?)value.Remark ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAt", FormatUtc(value.CreatedAtUtc));
        command.Parameters.AddWithValue("$createdBy", value.CreatedByShiftProfileId.ToString("D"));
        command.Parameters.AddWithValue("$updatedAt", value.UpdatedAtUtc is null ? DBNull.Value : FormatUtc(value.UpdatedAtUtc.Value));
        command.Parameters.AddWithValue("$deleted", value.Status == EventStatus.Deleted ? 1 : 0);
        command.Parameters.AddWithValue("$deletedAt", value.DeletedAtUtc is null ? DBNull.Value : FormatUtc(value.DeletedAtUtc.Value));
        command.Parameters.AddWithValue("$deletedBy", value.DeletedByShiftProfileId is null ? DBNull.Value : value.DeletedByShiftProfileId.Value.ToString("D"));
        command.Parameters.AddWithValue("$version", value.RowVersion);
    }

    private static Core.Event.Event Map(SqliteDataReader reader)
    {
        if (!EventTypeCode.TryParse(reader.GetString(3), out EventType eventType))
            throw new InvalidDataException("Stored EventType is not canonical.");
        return Core.Event.Event.Rehydrate(
            reader.GetString(0), reader.GetString(1), reader.GetString(2), eventType,
            reader.GetInt32(4), reader.GetInt32(5), reader.GetInt64(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            ParseUtc(reader.GetString(8)), Guid.Parse(reader.GetString(9)),
            reader.IsDBNull(10) ? null : ParseUtc(reader.GetString(10)),
            reader.GetInt32(11) == 0 ? EventStatus.Active : EventStatus.Deleted,
            reader.IsDBNull(12) ? null : ParseUtc(reader.GetString(12)),
            reader.IsDBNull(13) ? null : Guid.Parse(reader.GetString(13)), reader.GetInt64(14));
    }

    private static (SqliteConnection, SqliteTransaction) GetSqlite(ITransactionContext context) =>
        ((SqliteConnection)context.Connection, (SqliteTransaction)context.Transaction);
    private static string FormatUtc(DateTimeOffset value) =>
        value.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
    private static DateTimeOffset ParseUtc(string value) =>
        DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture).ToUniversalTime();
}
