using Microsoft.Data.Sqlite;
using Rah_Negar.Data;
using Rah_Negar.Infrastructure.Database.Readiness;
using Rah_Negar.Infrastructure.Pilot;
using Rah_Negar.Foundation.Application.Pilot.Live;
using Rah_Negar.Qualification;

namespace Rah_Negar.Tests.Qualification;

public sealed class QualificationEnvironmentTests
{
    [Fact]
    public async Task Scenarios_are_isolated_deterministic_and_usable()
    {
        string root = Path.Combine(Path.GetTempPath(), "rah-negar-qualification", Guid.NewGuid().ToString("N"));
        try
        {
            QualificationEnvironment.Prepare(root);
            string rasht = Path.Combine(root, "Rasht", "db.sys");
            string ramsar = Path.Combine(root, "Ramsar", "db.sys");
            Assert.True(File.Exists(rasht)); Assert.True(File.Exists(ramsar));
            string first = await Fingerprint(rasht);
            QualificationEnvironment.Prepare(root);
            Assert.Equal(first, await Fingerprint(rasht));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("Rasht", 3)]
    [InlineData("Ramsar", 4)]
    public async Task Each_scenario_has_expected_units_and_pilot_source_data(string station, int units)
    {
        string root = Path.Combine(Path.GetTempPath(), "rah-negar-qualification", Guid.NewGuid().ToString("N"));
        try
        {
            QualificationEnvironment.Prepare(root);
            await using (var db = Open(Path.Combine(root, station, "db.sys")))
            {
                Assert.Equal(units, await Scalar(db, "SELECT COUNT(*) FROM Units;"));
                Assert.Equal(2, await Scalar(db, "SELECT COUNT(*) FROM tbl_unique;"));
                Assert.Equal(units * 2 * 2, await Scalar(db, "SELECT COUNT(*) FROM tbl_events;"));
                Assert.Equal(1, await Scalar(db, "SELECT COUNT(*) FROM SecurityShiftProfiles WHERE IsActive=1;"));
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("Rasht", "station-rasht")]
    [InlineData("Ramsar", "station-ramsar")]
    public async Task Each_scenario_passes_the_existing_read_only_pilot_preflight(string station, string stationId)
    {
        string root = Path.Combine(Path.GetTempPath(), "rah-negar-qualification", Guid.NewGuid().ToString("N"));
        try
        {
            QualificationEnvironment.Prepare(root);
            string path = Path.Combine(root, station, "db.sys");
            var preflight = new LivePilotReadOnlyPreflight(new PilotReadOnlySqliteConnectionFactory(path));
            var result = await preflight.EvaluateAsync(new DateTimeOffset(2026, 9, 3, 0, 0, 0, TimeSpan.Zero));
            Assert.Equal(LivePilotReadOnlyPreflightStatus.Ready, result.Status);
            Assert.Equal(stationId, result.Scope!.StationId);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Normal_production_database_path_is_unchanged()
    {
        string production = SqliteDatabaseHelper.GetDatabasePath();
        Assert.EndsWith(Path.Combine("Data", "db.sys"), production, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RAH_NEGAR_QUALIFICATION_DB", File.ReadAllText(
            Path.Combine(RepositoryRoot(), "Data", "SqliteDatabaseHelper.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void Preparation_rejects_the_production_Data_directory_and_launcher_isolated_copy_is_scoped()
    {
        string productionData = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        Assert.Throws<InvalidOperationException>(() => QualificationEnvironment.Prepare(productionData));
        string launcher = File.ReadAllText(Path.Combine(RepositoryRoot(), "Qualification", "launch-qualification.ps1"));
        Assert.Contains("qualification-run", launcher, StringComparison.Ordinal);
        Assert.Contains("Copy-Item", launcher, StringComparison.Ordinal);
        Assert.Contains("WorkingDirectory", launcher, StringComparison.Ordinal);
        string ignore = File.ReadAllText(Path.Combine(RepositoryRoot(), ".gitignore"));
        Assert.Contains("Qualification/qualification-data/", ignore, StringComparison.Ordinal);
        Assert.Contains("Qualification/qualification-run/", ignore, StringComparison.Ordinal);
    }

    private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static SqliteConnection Open(string path) { var c = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False"); c.Open(); return c; }
    private static async Task<long> Scalar(SqliteConnection c, string sql) { await using var cmd = c.CreateCommand(); cmd.CommandText = sql; return Convert.ToInt64(await cmd.ExecuteScalarAsync()); }
    private static async Task<string> Fingerprint(string path)
    {
        byte[] bytes = await File.ReadAllBytesAsync(path);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    }
}
