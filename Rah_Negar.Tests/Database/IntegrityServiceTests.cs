using Microsoft.Data.Sqlite;
using Rah_Negar.Infrastructure.Database.Integrity;

namespace Rah_Negar.Tests.Database;

public sealed class IntegrityServiceTests
{
    [Fact]
    public async Task CheckAsync_reports_valid_temporary_database_and_runs_hooks()
    {
        await using TemporarySqliteDatabase database = TemporarySqliteDatabase.Create();
        await using (SqliteConnection connection = await database.Factory.OpenConnectionAsync())
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE marker (id INTEGER PRIMARY KEY);";
            await command.ExecuteNonQueryAsync();
        }

        var service = new DatabaseIntegrityService(database.Factory, new[] { new RequiredTableHook("marker") });
        DatabaseIntegrityResult result = await service.CheckAsync();

        Assert.True(result.IsValid);
        Assert.Equal("ok", Assert.Single(result.IntegrityMessages));
        Assert.Empty(result.ForeignKeyViolations);
        Assert.Empty(result.SchemaValidationErrors);
    }

    private sealed class RequiredTableHook(string tableName) : IDatabaseSchemaValidationHook
    {
        public async Task<IReadOnlyList<string>> ValidateAsync(
            SqliteConnection connection,
            CancellationToken cancellationToken = default)
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name;";
            command.Parameters.AddWithValue("$name", tableName);
            long count = (long)(await command.ExecuteScalarAsync(cancellationToken))!;
            return count == 1 ? Array.Empty<string>() : new[] { $"Missing table: {tableName}" };
        }
    }
}
