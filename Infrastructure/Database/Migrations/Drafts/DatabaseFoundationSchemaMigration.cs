using Microsoft.Data.Sqlite;
using Rah_Negar.Infrastructure.Database.Checksums;

namespace Rah_Negar.Infrastructure.Database.Migrations.Drafts;

/// <summary>Unregistered generalized target database identity foundation.</summary>
public sealed class DatabaseFoundationSchemaMigration : IDatabaseMigration
{
    public const string MigrationId = "target-database-foundation-v1";
    public const int FromVersion = 0;
    public const int ToVersion = 1;
    public const string Sql = """
        CREATE TABLE Stations (
            StationId TEXT COLLATE BINARY PRIMARY KEY NOT NULL CHECK (length(trim(StationId)) > 0),
            StationName TEXT NOT NULL CHECK (length(trim(StationName)) > 0),
            CreatedAtUtc TEXT NOT NULL,
            Revision INTEGER NOT NULL CHECK (Revision > 0)
        );
        CREATE TABLE Units (
            StationId TEXT COLLATE BINARY NOT NULL,
            UnitId TEXT COLLATE BINARY NOT NULL CHECK (length(trim(UnitId)) > 0),
            UnitNumber INTEGER NOT NULL CHECK (UnitNumber > 0),
            UnitName TEXT NOT NULL CHECK (length(trim(UnitName)) > 0),
            IsActive INTEGER NOT NULL CHECK (IsActive IN (0,1)),
            Revision INTEGER NOT NULL CHECK (Revision > 0),
            PRIMARY KEY (StationId, UnitId),
            UNIQUE (StationId, UnitNumber),
            FOREIGN KEY (StationId) REFERENCES Stations (StationId) ON UPDATE RESTRICT ON DELETE RESTRICT
        );
        """;

    public DatabaseFoundationSchemaMigration(IChecksumService checksums)
    {
        ArgumentNullException.ThrowIfNull(checksums);
        Metadata = new(MigrationId, FromVersion, ToVersion,
            "Create generalized Station and configurable Unit identity foundation.", checksums.Compute(Sql));
    }
    public MigrationMetadata Metadata { get; }
    public string ChecksumPayload => Sql;
    public async Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction; command.CommandText = Sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
