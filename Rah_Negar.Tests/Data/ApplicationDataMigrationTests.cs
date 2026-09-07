using Microsoft.Data.Sqlite;
using Rah_Negar.Infrastructure.ApplicationData;

namespace Rah_Negar.Tests.Data;

public sealed class ApplicationDataMigrationTests
{
    [Fact]
    public void No_legacy_database_leaves_canonical_root_empty()
    {
        using Fixture fixture = new();
        ApplicationDataMigrationResult result = ApplicationDataMigrationService.EnsureReady(fixture.Paths, fixture.ExecutableDirectory);
        Assert.Equal(ApplicationDataMigrationStatus.NoDataToMigrate, result.Status);
        Assert.False(File.Exists(fixture.Paths.DatabasePath));
    }

    [Fact]
    public async Task Valid_legacy_database_is_migrated_with_wal_contents_and_source_is_retained()
    {
        using Fixture fixture = new();
        string source = fixture.CreateLegacyDatabase(withWal: true);
        ApplicationDataMigrationResult result = ApplicationDataMigrationService.EnsureReady(fixture.Paths, fixture.ExecutableDirectory);
        Assert.Equal(ApplicationDataMigrationStatus.Migrated, result.Status);
        Assert.True(File.Exists(source));
        Assert.True(File.Exists(fixture.Paths.DatabasePath));
        Assert.Equal(1L, await ScalarAsync(fixture.Paths.DatabasePath));
        Assert.Contains("migrated", File.ReadAllText(fixture.Paths.MigrationAuditPath), StringComparison.Ordinal);
        ApplicationDataMigrationResult restart = ApplicationDataMigrationService.EnsureReady(fixture.Paths, fixture.ExecutableDirectory);
        Assert.Equal(ApplicationDataMigrationStatus.ExistingCanonicalData, restart.Status);
    }

    [Fact]
    public async Task Existing_canonical_and_legacy_databases_fail_closed_without_overwrite()
    {
        using Fixture fixture = new();
        fixture.CreateLegacyDatabase();
        Fixture.CreateDatabase(fixture.Paths.DatabasePath, 2);
        Assert.Throws<ApplicationDataMigrationException>(() =>
            ApplicationDataMigrationService.EnsureReady(fixture.Paths, fixture.ExecutableDirectory));
        Assert.Equal(2L, await ScalarAsync(fixture.Paths.DatabasePath));
    }

    [Fact]
    public void Corrupt_legacy_database_is_rejected_and_not_created_at_destination()
    {
        using Fixture fixture = new();
        Directory.CreateDirectory(Path.Combine(fixture.ExecutableDirectory, "Data"));
        File.WriteAllText(Path.Combine(fixture.ExecutableDirectory, "Data", "db.sys"), "not sqlite");
        Assert.Throws<ApplicationDataMigrationException>(() =>
            ApplicationDataMigrationService.EnsureReady(fixture.Paths, fixture.ExecutableDirectory));
        Assert.False(File.Exists(fixture.Paths.DatabasePath));
    }

    [Fact]
    public void Qualification_database_is_never_considered_a_legacy_source()
    {
        using Fixture fixture = new(Path.Combine(Path.GetTempPath(), "Qualification", Guid.NewGuid().ToString("N")));
        fixture.CreateLegacyDatabase();
        ApplicationDataMigrationResult result = ApplicationDataMigrationService.EnsureReady(fixture.Paths, fixture.ExecutableDirectory);
        Assert.Equal(ApplicationDataMigrationStatus.NoDataToMigrate, result.Status);
    }

    private static async Task<long> ScalarAsync(string path)
    {
        await using SqliteConnection connection = new($"Data Source={path};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM marker;";
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root;
        public string ExecutableDirectory { get; }
        public ApplicationDataPaths Paths { get; }

        public Fixture(string? executableDirectory = null)
        {
            _root = Path.Combine(Path.GetTempPath(), "rah-negar-batch2-" + Guid.NewGuid().ToString("N"));
            ExecutableDirectory = executableDirectory ?? Path.Combine(_root, "App");
            Paths = ApplicationDataPaths.ForRoot(Path.Combine(_root, "ProgramData", "RahNegar"));
            Directory.CreateDirectory(ExecutableDirectory);
        }

        public string CreateLegacyDatabase(bool withWal = false)
        {
            string path = Path.Combine(ExecutableDirectory, "Data", "db.sys");
            CreateDatabase(path, 1, withWal);
            return path;
        }

        public static void CreateDatabase(string path, long value, bool withWal = false)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using SqliteConnection connection = new($"Data Source={path};Mode=ReadWriteCreate;Pooling=False");
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = (withWal ? "PRAGMA journal_mode=WAL;" : "") +
                "CREATE TABLE marker(value INTEGER NOT NULL); INSERT INTO marker VALUES ($value);";
            command.Parameters.AddWithValue("$value", value);
            command.ExecuteNonQuery();
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
    }
}
