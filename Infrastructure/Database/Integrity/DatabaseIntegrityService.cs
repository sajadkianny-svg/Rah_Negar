using Microsoft.Data.Sqlite;

namespace Rah_Negar.Infrastructure.Database.Integrity;

public sealed class DatabaseIntegrityService
{
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly IReadOnlyList<IDatabaseSchemaValidationHook> _schemaHooks;

    public DatabaseIntegrityService(
        ISqliteConnectionFactory connectionFactory,
        IEnumerable<IDatabaseSchemaValidationHook>? schemaHooks = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _schemaHooks = schemaHooks?.ToArray() ?? Array.Empty<IDatabaseSchemaValidationHook>();
    }

    public async Task<DatabaseIntegrityResult> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection =
            await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string> integrity =
            await ReadSingleColumnAsync(connection, "PRAGMA integrity_check;", cancellationToken)
                .ConfigureAwait(false);
        bool integrityValid = integrity.Count == 1 &&
            string.Equals(integrity[0], "ok", StringComparison.OrdinalIgnoreCase);
        IReadOnlyList<ForeignKeyViolation> foreignKeys =
            await ReadForeignKeyViolationsAsync(connection, cancellationToken).ConfigureAwait(false);

        var schemaErrors = new List<string>();
        foreach (IDatabaseSchemaValidationHook hook in _schemaHooks)
        {
            schemaErrors.AddRange(
                await hook.ValidateAsync(connection, cancellationToken).ConfigureAwait(false));
        }

        return new DatabaseIntegrityResult(integrityValid, integrity, foreignKeys, schemaErrors);
    }

    private static async Task<IReadOnlyList<string>> ReadSingleColumnAsync(
        SqliteConnection connection, string sql, CancellationToken token)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        var values = new List<string>();
        while (await reader.ReadAsync(token).ConfigureAwait(false))
            values.Add(reader.GetString(0));
        return values;
    }

    private static async Task<IReadOnlyList<ForeignKeyViolation>> ReadForeignKeyViolationsAsync(
        SqliteConnection connection, CancellationToken token)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        var values = new List<ForeignKeyViolation>();
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            values.Add(new ForeignKeyViolation(
                reader.GetString(0),
                reader.IsDBNull(1) ? -1 : reader.GetInt64(1),
                reader.GetString(2),
                reader.GetInt32(3)));
        }
        return values;
    }
}
