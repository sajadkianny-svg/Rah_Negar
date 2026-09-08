using System.Text.Json;
using Microsoft.Data.Sqlite;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Qualification;

namespace Rah_Negar.Tests.Security;

public sealed class QualificationManagementAccessTests
{
    [Fact]
    public void Missing_or_malformed_isolation_marker_fails_closed()
    {
        using TempDirectory temp = new();
        string sessionRoot = Path.Combine(temp.Path, "Qualification", "qualification-run", "final-ui-acceptance", "session");
        string root = Path.Combine(sessionRoot, "isolated-data");
        string app = Path.Combine(sessionRoot, "app");
        string database = Path.Combine(root, "Data", "db.sys");
        Directory.CreateDirectory(app);
        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        File.WriteAllText(database, "qualification-db");

        Assert.False(QualificationManagementAccess.ValidateForPaths(root, app,
            Path.Combine(root, QualificationManagementAccess.IsolationMarkerFileName),
            Path.Combine(temp.Path, "production"), out _));
    }

    [Fact]
    public void Production_root_and_normal_app_location_are_rejected()
    {
        using TempDirectory temp = new();
        string sessionRoot = Path.Combine(temp.Path, "Qualification", "qualification-run", "final-ui-acceptance", "session");
        string root = Path.Combine(sessionRoot, "isolated-data");
        string app = Path.Combine(sessionRoot, "app");
        string database = Path.Combine(root, "Data", "db.sys");
        string markerPath = Path.Combine(root, QualificationManagementAccess.IsolationMarkerFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        File.WriteAllText(database, "qualification-db");
        File.WriteAllText(markerPath, Marker(root, app, database));

        Assert.False(QualificationManagementAccess.ValidateForPaths(root, temp.Path,
            markerPath, Path.Combine(temp.Path, "production"), out _));
        Assert.False(QualificationManagementAccess.ValidateForPaths(
            Path.Combine(temp.Path, "production"), app, markerPath,
            Path.Combine(temp.Path, "production"), out _));
    }

    [Fact]
    public void Official_marker_requires_the_isolated_database_and_app_copy()
    {
        using TempDirectory temp = new();
        string sessionRoot = Path.Combine(temp.Path, "Qualification", "qualification-run", "final-ui-acceptance", "session");
        string root = Path.Combine(sessionRoot, "isolated-data");
        string app = Path.Combine(sessionRoot, "app");
        string database = Path.Combine(root, "Data", "db.sys");
        string markerPath = Path.Combine(root, QualificationManagementAccess.IsolationMarkerFileName);
        Directory.CreateDirectory(app);
        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        File.WriteAllText(database, "qualification-db");
        File.WriteAllText(markerPath, Marker(root, app, database));

        Assert.True(QualificationManagementAccess.ValidateForPaths(root, app, markerPath,
            Path.Combine(temp.Path, "production"), out string reason), reason);
    }

    [Fact]
    public async Task Qualification_seed_stores_only_a_random_verifier_and_not_a_universal_secret()
    {
        using TempDirectory temp = new();
        string previous = Environment.GetEnvironmentVariable(QualificationManagementAccess.ManagementSecretEnvironmentVariable) ?? string.Empty;
        const string secret = "synthetic-per-run-management-proof-2026";
        try
        {
            Environment.SetEnvironmentVariable(QualificationManagementAccess.ManagementSecretEnvironmentVariable, secret);
            QualificationEnvironment.PrepareGeneric(temp.Path, 3);
            string database = Path.Combine(temp.Path, "Generic-3", "db.sys");
            await using SqliteConnection connection = new(new SqliteConnectionStringBuilder
            {
                DataSource = database,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString());
            await connection.OpenAsync();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT CredentialVersion,KdfAlgorithm,KdfParameters,Salt,PasswordVerifier,IsCurrent,IsActive FROM SecurityManagementCredentials WHERE SingletonId=1;";
            await using SqliteDataReader reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            ManagementCredentialRecord record = new(reader.GetInt32(0), reader.GetString(1), reader.GetString(2),
                (byte[])reader[3], (byte[])reader[4], reader.GetInt32(5) == 1, reader.GetInt32(6) == 1,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
            Assert.True(Pbkdf2TargetPasswordVerifier.Instance.Verify(secret, record));
            Assert.NotEqual(secret, Convert.ToHexString(record.PasswordVerifier));
        }
        finally
        {
            Environment.SetEnvironmentVariable(QualificationManagementAccess.ManagementSecretEnvironmentVariable,
                string.IsNullOrEmpty(previous) ? null : previous);
        }
    }

    [Fact]
    public void Privileged_dialogs_use_persian_management_wording_and_hide_internal_names()
    {
        string root = RepositoryRoot();
        string prompt = File.ReadAllText(Path.Combine(root, "UI", "Forms", "FrmPasswordConfirm.cs"));
        string settings = File.ReadAllText(Path.Combine(root, "UI", "Forms", "FrmSettings.cs"));
        Assert.Contains("مجوز مدیریتی", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("ManagementCredential", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("ManagementCredential", settings, StringComparison.Ordinal);
    }

    private static string Marker(string root, string app, string database) => JsonSerializer.Serialize(new
    {
        markerVersion = 1,
        harnessId = "Rah_Negar.run-final-ui-acceptance.v1",
        sessionId = "test-session",
        qualificationOnly = true,
        managementProofEnabled = true,
        productionDataTouched = false,
        dataRoot = root,
        databasePath = database,
        appDirectory = app,
        stationScope = "qualification",
        shiftProfileId = "qualification-generic"
    });

    private static string RepositoryRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RahNegar.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
