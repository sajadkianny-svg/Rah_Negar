using Microsoft.Data.Sqlite;
using Rah_Negar.Infrastructure.Database;

namespace Rah_Negar.Tests.Database;

public sealed class TransactionManagerTests
{
    [Fact]
    public async Task ExecuteAsync_commits_successful_operation()
    {
        await using TemporarySqliteDatabase database = TemporarySqliteDatabase.Create();
        await CreateItemsTableAsync(database);
        var manager = new SqliteTransactionManager(database.Factory);

        int result = await manager.ExecuteAsync(async (context, token) =>
        {
            var connection = (SqliteConnection)context.Connection;
            await using SqliteCommand command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)context.Transaction;
            command.CommandText = "INSERT INTO items (value) VALUES ('committed');";
            await command.ExecuteNonQueryAsync(token);
            return 7;
        });

        Assert.Equal(7, result);
        Assert.Equal(1L, await CountItemsAsync(database));
    }

    [Fact]
    public async Task ExecuteAsync_rolls_back_when_operation_throws()
    {
        await using TemporarySqliteDatabase database = TemporarySqliteDatabase.Create();
        await CreateItemsTableAsync(database);
        var manager = new SqliteTransactionManager(database.Factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.ExecuteAsync<int>(async (context, token) =>
        {
            var connection = (SqliteConnection)context.Connection;
            await using SqliteCommand command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)context.Transaction;
            command.CommandText = "INSERT INTO items (value) VALUES ('rolled-back');";
            await command.ExecuteNonQueryAsync(token);
            throw new InvalidOperationException("Injected failure");
        }));

        Assert.Equal(0L, await CountItemsAsync(database));
    }

    private static async Task CreateItemsTableAsync(TemporarySqliteDatabase database)
    {
        await using SqliteConnection connection = await database.Factory.OpenConnectionAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE items (id INTEGER PRIMARY KEY, value TEXT NOT NULL);";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountItemsAsync(TemporarySqliteDatabase database)
    {
        await using SqliteConnection connection = await database.Factory.OpenConnectionAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM items;";
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
