using Microsoft.Data.Sqlite;
using Rah_Negar.Foundation.Application.Transactions;

namespace Rah_Negar.Infrastructure.Database.Migrations;

public sealed class MigrationRunner
{
    private const string EnsureLedgerSql = """
        CREATE TABLE IF NOT EXISTS __rahnegar_schema_version (
            singleton_id INTEGER PRIMARY KEY CHECK (singleton_id = 1),
            current_version INTEGER NOT NULL CHECK (current_version >= 0)
        );
        INSERT OR IGNORE INTO __rahnegar_schema_version (singleton_id, current_version)
        VALUES (1, 0);
        CREATE TABLE IF NOT EXISTS __rahnegar_migration_history (
            migration_id TEXT PRIMARY KEY NOT NULL,
            from_version INTEGER NOT NULL,
            to_version INTEGER NOT NULL,
            checksum TEXT NOT NULL,
            applied_at_utc TEXT NOT NULL
        );
        """;

    private readonly ITransactionManager _transactionManager;
    private readonly MigrationChecksumValidator _checksumValidator;

    public MigrationRunner(
        ITransactionManager transactionManager,
        MigrationChecksumValidator checksumValidator)
    {
        _transactionManager = transactionManager ?? throw new ArgumentNullException(nameof(transactionManager));
        _checksumValidator = checksumValidator ?? throw new ArgumentNullException(nameof(checksumValidator));
    }

    public async Task<MigrationRunResult> RunPendingAsync(
        IEnumerable<IDatabaseMigration> migrations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(migrations);
        IDatabaseMigration[] ordered = ValidateAndOrder(migrations);

        return await _transactionManager.ExecuteAsync(async (context, token) =>
        {
            var connection = (SqliteConnection)context.Connection;
            var transaction = (SqliteTransaction)context.Transaction;
            await ExecuteAsync(connection, transaction, EnsureLedgerSql, token).ConfigureAwait(false);

            int initialVersion = await ReadCurrentVersionAsync(connection, transaction, token)
                .ConfigureAwait(false);
            Dictionary<string, AppliedMigration> history =
                await ReadAppliedAsync(connection, transaction, token).ConfigureAwait(false);

            ValidateLedger(initialVersion, history);
            ValidateAppliedChecksums(ordered, history);
            if (ordered.Length > 0 && initialVersion > ordered.Max(x => x.Metadata.ToVersion))
                throw new InvalidOperationException("Database schema version is newer than the supplied migration chain.");
            var appliedIds = new List<string>();
            int currentVersion = initialVersion;

            foreach (IDatabaseMigration migration in ordered.Where(x => x.Metadata.ToVersion > currentVersion))
            {
                MigrationMetadata metadata = migration.Metadata;
                if (metadata.FromVersion != currentVersion)
                    throw new InvalidOperationException(
                        $"Migration '{metadata.MigrationId}' expects version {metadata.FromVersion}, current is {currentVersion}.");

                _checksumValidator.Validate(migration);
                await migration.ApplyAsync(connection, transaction, token).ConfigureAwait(false);
                await RecordAppliedAsync(connection, transaction, metadata, token).ConfigureAwait(false);
                currentVersion = metadata.ToVersion;
                appliedIds.Add(metadata.MigrationId);
            }

            return new MigrationRunResult(initialVersion, currentVersion, appliedIds);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MigrationHistory> ReadHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        return await _transactionManager.ExecuteAsync(async (context, token) =>
        {
            var connection = (SqliteConnection)context.Connection;
            var transaction = (SqliteTransaction)context.Transaction;
            await ExecuteAsync(connection, transaction, EnsureLedgerSql, token).ConfigureAwait(false);
            int version = await ReadCurrentVersionAsync(connection, transaction, token).ConfigureAwait(false);
            var applied = await ReadAppliedAsync(connection, transaction, token).ConfigureAwait(false);
            return new MigrationHistory(new SchemaVersion(version), applied.Values.OrderBy(x => x.ToVersion).ToArray());
        }, cancellationToken).ConfigureAwait(false);
    }

    private static IDatabaseMigration[] ValidateAndOrder(IEnumerable<IDatabaseMigration> migrations)
    {
        IDatabaseMigration[] ordered = migrations.OrderBy(x => x.Metadata.FromVersion).ThenBy(x => x.Metadata.ToVersion).ToArray();
        if (ordered.Select(x => x.Metadata.MigrationId).Distinct(StringComparer.Ordinal).Count() != ordered.Length)
            throw new InvalidOperationException("Migration identifiers must be unique.");
        if (ordered.Any(x => x.Metadata.FromVersion < 0 || x.Metadata.ToVersion <= x.Metadata.FromVersion))
            throw new InvalidOperationException("Migration versions must advance monotonically.");
        if (ordered.Select(x => x.Metadata.FromVersion).Distinct().Count() != ordered.Length ||
            ordered.Select(x => x.Metadata.ToVersion).Distinct().Count() != ordered.Length)
            throw new InvalidOperationException("Migration version transitions must be unique.");
        for (int index = 1; index < ordered.Length; index++)
            if (ordered[index].Metadata.FromVersion != ordered[index - 1].Metadata.ToVersion)
                throw new InvalidOperationException("Migration chain contains a missing or overlapping intermediate version.");
        return ordered;
    }

    private static void ValidateLedger(int currentVersion, IReadOnlyDictionary<string, AppliedMigration> applied)
    {
        if (currentVersion == 0 && applied.Count == 0) return;
        if (applied.Count == 0 || applied.Values.Max(x => x.ToVersion) != currentVersion)
            throw new InvalidOperationException("Migration ledger version and history are inconsistent.");
        AppliedMigration[] ordered = applied.Values.OrderBy(x => x.FromVersion).ToArray();
        if (ordered[0].FromVersion != 0 || ordered[^1].ToVersion != currentVersion)
            throw new InvalidOperationException("Migration history does not span the recorded schema version.");
        for (int index = 1; index < ordered.Length; index++)
            if (ordered[index].FromVersion != ordered[index - 1].ToVersion)
                throw new InvalidOperationException("Migration history contains a gap or overlap.");
    }

    private static void ValidateAppliedChecksums(
        IEnumerable<IDatabaseMigration> migrations,
        IReadOnlyDictionary<string, AppliedMigration> applied)
    {
        foreach (IDatabaseMigration migration in migrations)
        {
            if (applied.TryGetValue(migration.Metadata.MigrationId, out AppliedMigration? existing) &&
                (!string.Equals(existing.Checksum, migration.Metadata.Checksum, StringComparison.OrdinalIgnoreCase) ||
                 existing.FromVersion != migration.Metadata.FromVersion ||
                 existing.ToVersion != migration.Metadata.ToVersion))
            {
                throw new InvalidOperationException(
                    $"Applied migration '{migration.Metadata.MigrationId}' checksum differs from the supplied migration.");
            }
        }
    }

    private static async Task<int> ReadCurrentVersionAsync(
        SqliteConnection connection, SqliteTransaction transaction, CancellationToken token)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT current_version FROM __rahnegar_schema_version WHERE singleton_id = 1;";
        object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
        return Convert.ToInt32(value);
    }

    private static async Task<Dictionary<string, AppliedMigration>> ReadAppliedAsync(
        SqliteConnection connection, SqliteTransaction transaction, CancellationToken token)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT migration_id, from_version, to_version, checksum, applied_at_utc FROM __rahnegar_migration_history;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        var result = new Dictionary<string, AppliedMigration>(StringComparer.Ordinal);
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            var item = new AppliedMigration(
                reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetString(3),
                DateTimeOffset.Parse(reader.GetString(4), System.Globalization.CultureInfo.InvariantCulture));
            result.Add(item.MigrationId, item);
        }
        return result;
    }

    private static async Task RecordAppliedAsync(
        SqliteConnection connection, SqliteTransaction transaction, MigrationMetadata metadata, CancellationToken token)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO __rahnegar_migration_history
                (migration_id, from_version, to_version, checksum, applied_at_utc)
            VALUES ($id, $from, $to, $checksum, $appliedAt);
            UPDATE __rahnegar_schema_version SET current_version = $to WHERE singleton_id = 1;
            """;
        command.Parameters.AddWithValue("$id", metadata.MigrationId);
        command.Parameters.AddWithValue("$from", metadata.FromVersion);
        command.Parameters.AddWithValue("$to", metadata.ToVersion);
        command.Parameters.AddWithValue("$checksum", metadata.Checksum);
        command.Parameters.AddWithValue("$appliedAt", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection, SqliteTransaction transaction, string sql, CancellationToken token)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }
}
