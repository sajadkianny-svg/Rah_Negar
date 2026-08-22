using Microsoft.Data.Sqlite;

namespace Rah_Negar.Infrastructure.Database.Migrations;

public interface IDatabaseMigration
{
    MigrationMetadata Metadata { get; }
    string ChecksumPayload { get; }

    Task ApplyAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken = default);
}
