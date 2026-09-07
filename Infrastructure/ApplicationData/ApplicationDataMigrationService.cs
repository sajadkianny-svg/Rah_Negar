using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Rah_Negar.Infrastructure.ApplicationData;

public enum ApplicationDataMigrationStatus { ExistingCanonicalData, NoDataToMigrate, Migrated }

public sealed record ApplicationDataMigrationResult(
    ApplicationDataMigrationStatus Status, string DestinationPath, string? SourcePath,
    string? SourceSha256, string? DestinationSha256, string Message);

public sealed class ApplicationDataMigrationException : IOException
{
    public ApplicationDataMigrationException(string message) : base(message) { }
    public ApplicationDataMigrationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Safely migrates a historical executable-side database using SQLite's online backup API.
/// WAL/SHM contents are read by SQLite and the original files are retained after verification.
/// </summary>
public static class ApplicationDataMigrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public static ApplicationDataMigrationResult EnsureReady(ApplicationDataPaths paths,
        string executableDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (string.IsNullOrWhiteSpace(executableDirectory))
            throw new ArgumentException("Executable directory is required.", nameof(executableDirectory));
        paths.EnsureDirectories();
        string destination = Path.GetFullPath(paths.DatabasePath);
        string[] sources = FindLegacySources(executableDirectory, destination);
        if (File.Exists(destination))
        {
            if (sources.Length == 0) return Existing(paths, "Canonical data root is already in use.");
            if (sources.Length == 1 && WasCompletedMigration(paths, sources[0], destination, cancellationToken))
                return Existing(paths, "Previously migrated data is present.");
            Audit(paths, "conflict", null, destination,
                "Canonical and historical operational databases both exist without a verified migration record.");
            throw new ApplicationDataMigrationException(
                "Canonical and historical operational databases conflict; refusing to choose or overwrite either file.");
        }

        if (Directory.GetFiles(paths.DataDirectory, "db.sys.migration-*").Length != 0)
        {
            Audit(paths, "interrupted", null, destination, "An unfinished migration artifact is present.");
            throw new ApplicationDataMigrationException("An interrupted data migration requires operator recovery.");
        }
        if (sources.Length == 0)
            return new(ApplicationDataMigrationStatus.NoDataToMigrate, destination, null, null, null,
                "No historical operational database was found.");
        if (sources.Length > 1)
        {
            Audit(paths, "conflict", null, destination, "More than one historical operational database was found.");
            throw new ApplicationDataMigrationException("Multiple historical operational databases were found; refusing to guess.");
        }

        string source = sources[0];
        string staging = destination + ".migration-" + Guid.NewGuid().ToString("N");
        try
        {
            string sourceHash = ValidateAndHash(source, cancellationToken);
            CopyWithSqliteBackup(source, staging, cancellationToken);
            string destinationHash = ValidateAndHash(staging, cancellationToken);
            File.Move(staging, destination, overwrite: false);
            WriteMigrationState(paths, source, destination, sourceHash, destinationHash);
            Audit(paths, "migrated", source, destination,
                "Verified SQLite backup migration completed; source and its sidecars were retained.");
            return new(ApplicationDataMigrationStatus.Migrated, destination, source, sourceHash, destinationHash,
                "Historical operational database migrated to the canonical root.");
        }
        catch (ApplicationDataMigrationException) { DeleteIfExists(staging); throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException or InvalidDataException)
        {
            DeleteIfExists(staging);
            Audit(paths, "failed", source, destination, ex.GetType().Name + ": " + ex.Message);
            throw new ApplicationDataMigrationException("Historical data migration failed; the original data was preserved.", ex);
        }
    }

    private static ApplicationDataMigrationResult Existing(ApplicationDataPaths paths, string message) =>
        new(ApplicationDataMigrationStatus.ExistingCanonicalData, paths.DatabasePath, null, null, null, message);

    private static string[] FindLegacySources(string executableDirectory, string destination) =>
        new[] { Path.Combine(Path.GetFullPath(executableDirectory), "Data", "db.sys") }
            .Select(Path.GetFullPath)
            .Where(path => !string.Equals(path, destination, StringComparison.OrdinalIgnoreCase))
            .Where(path => !IsQualificationPath(path))
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static bool IsQualificationPath(string path) => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Any(part => part.Equals("Qualification", StringComparison.OrdinalIgnoreCase) ||
                    part.Contains("qualification", StringComparison.OrdinalIgnoreCase));

    private static string ValidateAndHash(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(path)) throw new FileNotFoundException("SQLite source was not found.", path);
        try
        {
            using SqliteConnection connection = OpenReadOnly(path);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            if (!string.Equals(Convert.ToString(command.ExecuteScalar()), "ok", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("SQLite integrity check did not pass.");
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
        }
        catch (SqliteException ex) { throw new InvalidDataException("The historical SQLite database is corrupt or unreadable.", ex); }
    }

    private static void CopyWithSqliteBackup(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using SqliteConnection source = OpenReadOnly(sourcePath);
        var builder = new SqliteConnectionStringBuilder { DataSource = destinationPath,
            Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false };
        using SqliteConnection destination = new(builder.ToString());
        destination.Open();
        source.BackupDatabase(destination);
        using SqliteCommand integrity = destination.CreateCommand();
        integrity.CommandText = "PRAGMA integrity_check;";
        if (!string.Equals(Convert.ToString(integrity.ExecuteScalar()), "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Migrated SQLite database failed integrity verification.");
    }

    private static SqliteConnection OpenReadOnly(string path)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = path,
            Mode = SqliteOpenMode.ReadOnly, Cache = SqliteCacheMode.Private, Pooling = false };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        return connection;
    }

    private static bool WasCompletedMigration(ApplicationDataPaths paths, string source, string destination,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.MigrationStatePath)) return false;
        try
        {
            MigrationState? state = JsonSerializer.Deserialize<MigrationState>(File.ReadAllText(paths.MigrationStatePath), JsonOptions);
            if (state?.Version != 1 || !string.Equals(Path.GetFullPath(state.SourcePath), Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetFullPath(state.DestinationPath), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase)) return false;
            return string.Equals(state.SourceSha256, ValidateAndHash(source, cancellationToken), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(state.DestinationSha256, ValidateAndHash(destination, cancellationToken), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or SqliteException) { return false; }
    }

    private static void WriteMigrationState(ApplicationDataPaths paths, string source, string destination,
        string sourceHash, string destinationHash)
    {
        string temp = paths.MigrationStatePath + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temp, JsonSerializer.Serialize(new MigrationState(1, Path.GetFullPath(source),
            Path.GetFullPath(destination), sourceHash, destinationHash, DateTimeOffset.UtcNow), JsonOptions));
        File.Move(temp, paths.MigrationStatePath, overwrite: true);
    }

    private static void Audit(ApplicationDataPaths paths, string result, string? source, string destination, string message)
    {
        try { File.AppendAllText(paths.MigrationAuditPath, JsonSerializer.Serialize(new { timestampUtc = DateTimeOffset.UtcNow,
            operation = "legacy-data-migration", result, source, destination, message }, JsonOptions) + Environment.NewLine); }
        catch { /* A failed audit never turns an unsafe migration into a successful one. */ }
    }
    private static void DeleteIfExists(string path) { if (File.Exists(path)) File.Delete(path); }
    private sealed record MigrationState(int Version, string SourcePath, string DestinationPath,
        string SourceSha256, string DestinationSha256, DateTimeOffset CompletedAtUtc);
}
