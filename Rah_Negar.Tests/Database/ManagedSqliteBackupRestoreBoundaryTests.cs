using Microsoft.Data.Sqlite;
using Rah_Negar.Foundation.Application.Database.Readiness;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Foundation.Time;
using Rah_Negar.Infrastructure.Database;
using Rah_Negar.Infrastructure.Database.Checksums;
using Rah_Negar.Infrastructure.Database.Migrations;
using Rah_Negar.Infrastructure.Database.Migrations.Drafts;
using Rah_Negar.Infrastructure.Database.Readiness;

namespace Rah_Negar.Tests.Database;

public sealed class ManagedSqliteBackupRestoreBoundaryTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Managed_backup_requires_exact_management_proof_and_returns_wal_receipt()
    {
        await using TemporarySqliteDatabase source = TemporarySqliteDatabase.Create();
        await CreateMarkerDatabaseAsync(source.Path, "wal-value");

        string backupPath = Path.Combine(Path.GetDirectoryName(source.Path)!, "verified-backup.sqlite");
        Services services = CreateServices();
        string scope = SqliteProtectedActionBinding.CreateBackupScope(source.Path, backupPath, BackupOverwritePolicy.Deny);
        ManagementAuthorizationProof proof = Proof(ProtectedAction.BackupPolicy, scope, "backup-correlation");

        ManagedSqliteBackupResult result = await services.Boundary.CreateVerifiedBackupAsync(
            source.Path, backupPath, BackupOverwritePolicy.Deny, proof, 7);

        Assert.True(result.Succeeded, $"{result.Failure}: {string.Join(',', result.Errors)}");
        Assert.Equal(SqliteBoundaryFailure.None, result.Failure);
        Assert.Equal("backup-correlation", result.Receipt.CorrelationId);
        Assert.Equal(result.Verification.BackupSha256, result.Receipt.BackupSha256);
        Assert.Contains(result.Receipt.SourceSidecars, sidecar => sidecar.Suffix == "-wal");
        Assert.Equal("wal-value", await ScalarAsync(backupPath, "SELECT value FROM marker;"));

        string wrongScope = SqliteProtectedActionBinding.CreateBackupScope(
            source.Path, Path.Combine(Path.GetDirectoryName(source.Path)!, "other.sqlite"), BackupOverwritePolicy.Deny);
        ManagedSqliteBackupResult denied = await services.Boundary.CreateVerifiedBackupAsync(
            source.Path, backupPath, BackupOverwritePolicy.Deny,
            proof with { ActionScope = wrongScope }, 7);

        Assert.False(denied.Succeeded);
        Assert.Equal(SqliteBoundaryFailure.AuthorizationRejected, denied.Failure);
    }

    [Fact]
    public async Task Restore_creates_verified_rollback_copy_and_atomically_replaces_destination()
    {
        await using TemporarySqliteDatabase source = TemporarySqliteDatabase.Create();
        await CreateMarkerDatabaseAsync(source.Path, "backup-value");
        string directory = Path.GetDirectoryName(source.Path)!;
        string backupPath = Path.Combine(directory, "verified-backup.sqlite");
        string destinationPath = Path.Combine(directory, "live.sqlite");
        string rollbackPath = Path.Combine(directory, "rollback.sqlite");
        await CreateMarkerDatabaseAsync(destinationPath, "live-value");

        Services services = CreateServices();
        string backupScope = SqliteProtectedActionBinding.CreateBackupScope(source.Path, backupPath, BackupOverwritePolicy.Deny);
        ManagedSqliteBackupResult backup = await services.Boundary.CreateVerifiedBackupAsync(
            source.Path, backupPath, BackupOverwritePolicy.Deny,
            Proof(ProtectedAction.BackupPolicy, backupScope, "backup-correlation"), 7);
        Assert.True(backup.Succeeded, $"{backup.Failure}: {string.Join(',', backup.Errors)}");

        string restoreScope = SqliteProtectedActionBinding.CreateRestoreScope(
            backupPath, backup.Receipt.BackupSha256!, destinationPath, rollbackPath);
        ManagedSqliteRestoreResult result = await services.Boundary.RestoreAsync(
            backupPath, backup.Receipt.BackupSha256!, destinationPath, rollbackPath,
            Proof(ProtectedAction.Restore, restoreScope, "restore-correlation"), 7);

        Assert.True(result.Succeeded, $"{result.Failure}: {string.Join(',', result.Errors)}");
        Assert.Equal(SqliteBoundaryFailure.None, result.Failure);
        Assert.True(result.Receipt.PreRestoreValidationPassed);
        Assert.True(result.Receipt.PostRestoreValidationPassed);
        Assert.True(File.Exists(rollbackPath));
        Assert.True((File.GetAttributes(rollbackPath) & FileAttributes.ReadOnly) != 0);
        Assert.Equal("backup-value", await ScalarAsync(destinationPath, "SELECT value FROM marker;"));
        Assert.Equal("live-value", await ScalarAsync(rollbackPath, "SELECT value FROM marker;"));
        Assert.Equal(backup.Receipt.BackupSha256, result.Receipt.DestinationAfterSha256);
        Assert.True(File.Exists(destinationPath + ".prior-" + result.Receipt.ActionScope[^16..].ToLowerInvariant()));
        File.SetAttributes(rollbackPath, FileAttributes.Normal);
    }

    [Fact]
    public async Task Injected_post_swap_failure_restores_original_live_database_without_ambiguity()
    {
        await using TemporarySqliteDatabase source = TemporarySqliteDatabase.Create();
        await CreateMarkerDatabaseAsync(source.Path, "replacement-value");
        string directory = Path.GetDirectoryName(source.Path)!;
        string backupPath = Path.Combine(directory, "verified-backup.sqlite");
        string destinationPath = Path.Combine(directory, "live.sqlite");
        string rollbackPath = Path.Combine(directory, "rollback-after-fault.sqlite");
        await CreateMarkerDatabaseAsync(destinationPath, "original-value");
        Services services = CreateServices();

        string backupScope = SqliteProtectedActionBinding.CreateBackupScope(source.Path, backupPath, BackupOverwritePolicy.Deny);
        ManagedSqliteBackupResult backup = await services.Boundary.CreateVerifiedBackupAsync(
            source.Path, backupPath, BackupOverwritePolicy.Deny,
            Proof(ProtectedAction.BackupPolicy, backupScope, "fault-backup"), 7);
        string restoreScope = SqliteProtectedActionBinding.CreateRestoreScope(
            backupPath, backup.Receipt.BackupSha256!, destinationPath, rollbackPath);

        ManagedSqliteRestoreResult result = await services.Boundary.RestoreAsync(
            backupPath, backup.Receipt.BackupSha256!, destinationPath, rollbackPath,
            Proof(ProtectedAction.Restore, restoreScope, "fault-restore"), 7,
            SqliteRestoreFailureInjectionPoint.AfterSwapBeforeValidation);

        Assert.False(result.Succeeded);
        Assert.Equal(SqliteBoundaryFailure.FailureInjected, result.Failure);
        Assert.Equal("original-value", await ScalarAsync(destinationPath, "SELECT value FROM marker;"));
        Assert.True(File.Exists(rollbackPath));
        Assert.True(File.Exists(destinationPath + ".failed-" + result.Receipt.ActionScope[^16..].ToLowerInvariant()));
        File.SetAttributes(rollbackPath, FileAttributes.Normal);
    }

    private static ManagementAuthorizationProof Proof(ProtectedAction action, string scope, string correlation) =>
        new("shift-profile-1", action, scope, 7, Now.AddMinutes(-1), Now.AddMinutes(10), correlation);

    private static Services CreateServices()
    {
        var clock = new FixedClock(Now);
        var checksums = new Sha256ChecksumService();
        IReadOnlyList<IDatabaseMigration> migrations = UnifiedTargetMigrationChain.Create(checksums);
        var classifier = new MigrationHistoryClassifier(migrations.Select(x => new SupportedMigrationDefinition(
            x.Metadata.MigrationId, x.Metadata.FromVersion, x.Metadata.ToVersion, x.Metadata.Checksum)),
            UnifiedTargetMigrationChain.FinalVersion);
        var inspector = new ExplicitDatabaseTargetInspector(clock);
        var preflight = new ReadOnlyDatabasePreflightAnalyzer(inspector);
        var fingerprints = new DatabaseStructuralFingerprintService(preflight);
        var backup = new ExplicitSqliteBackupService(preflight, fingerprints, classifier, clock);
        var restore = new RestoreValidationService(preflight, classifier);
        return new(new ManagedSqliteBackupRestoreBoundary(backup, restore, preflight, clock));
    }

    private static async Task CreateMarkerDatabaseAsync(string path, string value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using SqliteConnection connection = new(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString());
        await connection.OpenAsync();
        await ExecuteAsync(connection, "PRAGMA journal_mode=WAL; CREATE TABLE marker(value TEXT NOT NULL);");
        await using SqliteCommand command = connection.CreateCommand();
        command.Parameters.AddWithValue("$value", value);
        command.CommandText = "INSERT INTO marker VALUES($value);";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(string path, string sql)
    {
        await using SqliteConnection connection = new(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private sealed record Services(ManagedSqliteBackupRestoreBoundary Boundary);

    private sealed record FixedClock(DateTimeOffset UtcNow) : IClock
    {
        public DateTimeOffset LocalNow => UtcNow.ToOffset(TimeSpan.FromHours(3.5));
    }
}
