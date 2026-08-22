using Microsoft.Data.Sqlite;
using Rah_Negar.Infrastructure.Database;
using Rah_Negar.Infrastructure.Database.Checksums;
using Rah_Negar.Infrastructure.Database.Migrations;

namespace Rah_Negar.Tests.Database;

public sealed class MigrationFrameworkTests
{
    [Fact]
    public async Task Runner_orders_migrations_and_records_history()
    {
        await using TemporarySqliteDatabase database = TemporarySqliteDatabase.Create();
        var checksum = new Sha256ChecksumService();
        MigrationRunner runner = CreateRunner(database, checksum);
        SqlMigration first = SqlMigration.Create("001", 0, 1, "CREATE TABLE alpha (id INTEGER PRIMARY KEY);", checksum);
        SqlMigration second = SqlMigration.Create("002", 1, 2, "CREATE TABLE beta (id INTEGER PRIMARY KEY);", checksum);

        MigrationRunResult result = await runner.RunPendingAsync(new IDatabaseMigration[] { second, first });
        MigrationHistory history = await runner.ReadHistoryAsync();

        Assert.Equal(0, result.InitialVersion);
        Assert.Equal(2, result.FinalVersion);
        Assert.Equal(new[] { "001", "002" }, result.AppliedMigrationIds);
        Assert.Equal(2, history.SchemaVersion.CurrentVersion);
        Assert.Equal(new[] { "001", "002" }, history.AppliedMigrations.Select(x => x.MigrationId));
        Assert.True(await TableExistsAsync(database, "alpha"));
        Assert.True(await TableExistsAsync(database, "beta"));
    }

    [Fact]
    public async Task Runner_is_idempotent_for_applied_migrations()
    {
        await using TemporarySqliteDatabase database = TemporarySqliteDatabase.Create();
        var checksum = new Sha256ChecksumService();
        MigrationRunner runner = CreateRunner(database, checksum);
        SqlMigration migration = SqlMigration.Create("001", 0, 1, "CREATE TABLE alpha (id INTEGER PRIMARY KEY);", checksum);

        await runner.RunPendingAsync(new[] { migration });
        MigrationRunResult second = await runner.RunPendingAsync(new[] { migration });

        Assert.Empty(second.AppliedMigrationIds);
        Assert.Equal(1, second.FinalVersion);
    }

    [Fact]
    public async Task Checksum_failure_rolls_back_ledger_and_schema()
    {
        await using TemporarySqliteDatabase database = TemporarySqliteDatabase.Create();
        var checksum = new Sha256ChecksumService();
        MigrationRunner runner = CreateRunner(database, checksum);
        var invalid = new SqlMigration(
            new MigrationMetadata("bad", 0, 1, "Invalid", new string('0', 64)),
            "CREATE TABLE should_not_exist (id INTEGER PRIMARY KEY);");

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunPendingAsync(new[] { invalid }));

        Assert.False(await TableExistsAsync(database, "should_not_exist"));
        Assert.False(await TableExistsAsync(database, "__rahnegar_schema_version"));
    }

    [Fact]
    public async Task Failed_migration_rolls_back_all_framework_changes()
    {
        await using TemporarySqliteDatabase database = TemporarySqliteDatabase.Create();
        var checksum = new Sha256ChecksumService();
        MigrationRunner runner = CreateRunner(database, checksum);
        SqlMigration failing = SqlMigration.Create(
            "001", 0, 1,
            "CREATE TABLE transient_table (id INTEGER PRIMARY KEY); SELECT * FROM missing_table;",
            checksum);

        await Assert.ThrowsAsync<SqliteException>(() => runner.RunPendingAsync(new[] { failing }));

        Assert.False(await TableExistsAsync(database, "transient_table"));
        Assert.False(await TableExistsAsync(database, "__rahnegar_migration_history"));
    }

    private static MigrationRunner CreateRunner(TemporarySqliteDatabase database, IChecksumService checksum) =>
        new(new SqliteTransactionManager(database.Factory), new MigrationChecksumValidator(checksum));

    private static async Task<bool> TableExistsAsync(TemporarySqliteDatabase database, string name)
    {
        await using SqliteConnection connection = await database.Factory.OpenConnectionAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name;";
        command.Parameters.AddWithValue("$name", name);
        return (long)(await command.ExecuteScalarAsync())! == 1;
    }

    private sealed class SqlMigration : IDatabaseMigration
    {
        public SqlMigration(MigrationMetadata metadata, string sql)
        {
            Metadata = metadata;
            ChecksumPayload = sql;
        }

        public MigrationMetadata Metadata { get; }
        public string ChecksumPayload { get; }

        public static SqlMigration Create(
            string id, int from, int to, string sql, IChecksumService checksum) =>
            new(new MigrationMetadata(id, from, to, id, checksum.Compute(sql)), sql);

        public async Task ApplyAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = ChecksumPayload;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
