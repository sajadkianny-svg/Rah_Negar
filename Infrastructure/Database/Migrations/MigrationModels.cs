namespace Rah_Negar.Infrastructure.Database.Migrations;

public sealed record MigrationMetadata(
    string MigrationId,
    int FromVersion,
    int ToVersion,
    string Description,
    string Checksum);

public sealed record SchemaVersion(int CurrentVersion);

public sealed record AppliedMigration(
    string MigrationId,
    int FromVersion,
    int ToVersion,
    string Checksum,
    DateTimeOffset AppliedAtUtc);

public sealed record MigrationHistory(
    SchemaVersion SchemaVersion,
    IReadOnlyList<AppliedMigration> AppliedMigrations);

public sealed record MigrationRunResult(
    int InitialVersion,
    int FinalVersion,
    IReadOnlyList<string> AppliedMigrationIds);
