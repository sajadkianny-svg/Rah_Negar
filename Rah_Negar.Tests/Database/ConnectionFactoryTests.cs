using Microsoft.Data.Sqlite;
using Rah_Negar.Infrastructure.Database;

namespace Rah_Negar.Tests.Database;

public sealed class ConnectionFactoryTests
{
    [Fact]
    public void Options_reject_invalid_timeout()
    {
        var options = new SqliteDatabaseOptions { DataSource = "test.sqlite", DefaultTimeoutSeconds = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
    }

    [Fact]
    public async Task OpenConnection_applies_standard_pragmas()
    {
        await using TemporarySqliteDatabase database = TemporarySqliteDatabase.Create();
        await using SqliteConnection connection = await database.Factory.OpenConnectionAsync();

        Assert.Equal(1L, await ScalarAsync(connection, "PRAGMA foreign_keys;"));
        Assert.Equal("wal", await ScalarAsync(connection, "PRAGMA journal_mode;"));
        Assert.Equal(1L, await ScalarAsync(connection, "PRAGMA synchronous;"));
        Assert.Equal(2L, await ScalarAsync(connection, "PRAGMA temp_store;"));
    }

    private static async Task<object?> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }
}
