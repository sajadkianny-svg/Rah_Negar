using Microsoft.Data.Sqlite;
using Rah_Negar.Core.Event;
using Rah_Negar.Foundation.Application.Event;
using Rah_Negar.Foundation.Application.Transactions;

namespace Rah_Negar.Infrastructure.Event;

public sealed class SqliteEventAuditRepository : IEventAuditRepository
{
    public async Task AddAsync(
        ITransactionContext transactionContext, EventAudit audit,
        CancellationToken cancellationToken = default)
    {
        var connection = (SqliteConnection)transactionContext.Connection;
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transactionContext.Transaction;
        command.CommandText = """
            INSERT INTO EventAudit (
                AuditId, EventId, ActionType, OldValue, NewValue, ActorShiftProfileId,
                PersonnelNoSnapshot, SupervisorDisplayNameSnapshot, TimestampUtc,
                Reason, CorrelationId)
            VALUES ($id,$eventId,$action,$old,$new,$actor,$personnel,$name,$time,$reason,$correlation);
            """;
        command.Parameters.AddWithValue("$id", audit.AuditId);
        command.Parameters.AddWithValue("$eventId", audit.EventId);
        command.Parameters.AddWithValue("$action", audit.Action.ToString().ToUpperInvariant());
        command.Parameters.AddWithValue("$old", (object?)audit.OldValue ?? DBNull.Value);
        command.Parameters.AddWithValue("$new", (object?)audit.NewValue ?? DBNull.Value);
        command.Parameters.AddWithValue("$actor", audit.ActorShiftProfileId.ToString("D"));
        command.Parameters.AddWithValue("$personnel", (object?)audit.PersonnelNoSnapshot ?? DBNull.Value);
        command.Parameters.AddWithValue("$name", (object?)audit.SupervisorDisplayNameSnapshot ?? DBNull.Value);
        command.Parameters.AddWithValue("$time", audit.TimestampUtc.UtcDateTime.ToString(
            "O", System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$reason", audit.Reason);
        command.Parameters.AddWithValue("$correlation", audit.CorrelationId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<EventAudit>> GetForEventAsync(
        ITransactionContext transactionContext, string eventId,
        CancellationToken cancellationToken = default)
    {
        var connection = (SqliteConnection)transactionContext.Connection;
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transactionContext.Transaction;
        command.CommandText = """
            SELECT AuditId,EventId,ActionType,OldValue,NewValue,ActorShiftProfileId,
                PersonnelNoSnapshot,SupervisorDisplayNameSnapshot,TimestampUtc,Reason,CorrelationId
            FROM EventAudit WHERE EventId=$eventId ORDER BY TimestampUtc, AuditId;
            """;
        command.Parameters.AddWithValue("$eventId", eventId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var values = new List<EventAudit>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            values.Add(new EventAudit(
                reader.GetString(0), reader.GetString(1),
                Enum.Parse<EventAuditAction>(reader.GetString(2), ignoreCase: true),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4), Guid.Parse(reader.GetString(5)),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                DateTimeOffset.Parse(reader.GetString(8), System.Globalization.CultureInfo.InvariantCulture).ToUniversalTime(),
                reader.GetString(9), reader.GetString(10)));
        }
        return values;
    }
}
