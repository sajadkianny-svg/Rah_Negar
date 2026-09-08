using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Rah_Negar.Data;
using Rah_Negar.Foundation.Application.Database.Readiness;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Infrastructure.Database.Readiness;

namespace Rah_Negar.Services;

/// <summary>
/// Protected maintenance operations for the legacy database. Every mutation
/// requires a caller-issued ManagementCredential proof and fails closed if the
/// proof, integrity, or audit boundary is unavailable.
/// </summary>
public static class DatabaseMaintenanceService
{
    public static void RepairIndexes(ManagementAuthorizationProof managementProof,
        int currentManagementCredentialVersion)
    {
        EnsureAuthorization(managementProof, ProtectedAction.IntegrityRepair,
            "legacy-integrity-repair", currentManagementCredentialVersion);
        using SqliteConnection connection = SqliteDatabaseHelper.CreateConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "REINDEX; ANALYZE; PRAGMA optimize;";
        command.ExecuteNonQuery();
        LegacySecurityAuditService.Write(connection, transaction, managementProof,
            ProtectedAction.IntegrityRepair, "legacy-integrity-repair", true);
        transaction.Commit();
    }

    public static string ExportDatabase(string destinationPath,
        ManagementAuthorizationProof managementProof, int currentManagementCredentialVersion)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("مسیر خروجی معتبر نیست", nameof(destinationPath));
        string databasePath = SqliteDatabaseHelper.GetDatabasePath();
        string destination = Path.GetFullPath(destinationPath);
        string scope = SqliteProtectedActionBinding.CreateBackupScope(
            databasePath, destination, BackupOverwritePolicy.Deny);
        EnsureAuthorization(managementProof, ProtectedAction.BackupPolicy, scope,
            currentManagementCredentialVersion);
        if (!File.Exists(databasePath))
            throw new FileNotFoundException("فایل دیتابیس پیدا نشد", databasePath);
        if (File.Exists(destination))
            throw new IOException("فایل پشتیبان از قبل وجود دارد.");

        string receiptPath = destination + ".sha256";
        if (File.Exists(receiptPath))
            throw new IOException("A backup checksum receipt already exists.");
        string tempDb = CreateTemporaryPath("export");
        bool committed = false;
        try
        {
            CreateVerifiedSqliteCopy(databasePath, tempDb);
            BackupEncryptionService.EncryptFile(tempDb, destination);
            string backupSha256 = ComputeSha256(destination);
            WriteChecksumReceipt(receiptPath, backupSha256, destination);
            using SqliteConnection connection = SqliteDatabaseHelper.CreateConnection();
            using SqliteTransaction transaction = connection.BeginTransaction();
            LegacySecurityAuditService.Write(connection, transaction, managementProof,
                ProtectedAction.BackupPolicy, scope, true);
            transaction.Commit();
            committed = true;
            return backupSha256;
        }
        catch
        {
            if (!committed)
            {
                DeleteIfExists(destination);
                DeleteIfExists(receiptPath);
            }
            throw;
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteIfExists(tempDb);
        }
    }

    public static void ImportDatabase(string backupPath,
        ManagementAuthorizationProof managementProof, int currentManagementCredentialVersion)
    {
        ImportDatabase(backupPath, CreateRestoreRollbackPath(SqliteDatabaseHelper.GetDatabasePath()),
            managementProof, currentManagementCredentialVersion);
    }

    public static string CreateRestoreRollbackPath(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("مسیر دیتابیس معتبر نیست", nameof(databasePath));
        string fullPath = Path.GetFullPath(databasePath);
        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("مسیر دیتابیس معتبر نیست");
        return Path.Combine(directory,
            $"RahNegar_BeforeImport_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.db");
    }

    public static void ImportDatabase(string backupPath, string rollbackPath,
        ManagementAuthorizationProof managementProof, int currentManagementCredentialVersion)
    {
        if (string.IsNullOrWhiteSpace(backupPath))
            throw new ArgumentException("مسیر پشتیبان معتبر نیست", nameof(backupPath));
        string databasePath = SqliteDatabaseHelper.GetDatabasePath();
        if (string.IsNullOrWhiteSpace(rollbackPath))
            throw new ArgumentException("مسیر نسخه بازگشت معتبر نیست", nameof(rollbackPath));
        string backup = Path.GetFullPath(backupPath);
        if (!File.Exists(backup))
            throw new FileNotFoundException("فایل پشتیبان پیدا نشد", backup);
        string backupSha256 = ComputeSha256(backup);
        string rollback = Path.GetFullPath(rollbackPath);
        string scope = SqliteProtectedActionBinding.CreateRestoreScope(
            backup, backupSha256, databasePath, rollback);
        EnsureAuthorization(managementProof, ProtectedAction.Restore, scope,
            currentManagementCredentialVersion);
        ValidateOptionalChecksumReceipt(backup, backupSha256);

        string directory = Path.GetDirectoryName(databasePath)
            ?? throw new InvalidOperationException("مسیر دیتابیس معتبر نیست");
        string decrypted = Path.Combine(directory, $"RahNegar_RestoreStage_{Guid.NewGuid():N}.db");
        string staged = Path.Combine(directory, $"RahNegar_RestoreReplacement_{Guid.NewGuid():N}.db");
        string? walBackup = null;
        string? shmBackup = null;
        try
        {
            // Validate the encrypted input and decrypted SQLite before any live-file mutation.
            BackupEncryptionService.DecryptFile(backup, decrypted);
            ValidateSqliteIntegrity(decrypted);
            ValidateBackupCompatibility(databasePath, decrypted);

            // This is a verified, SQLite-consistent rollback copy, not a raw File.Copy.
            CreateVerifiedSqliteCopy(databasePath, rollback);
            File.SetAttributes(rollback, File.GetAttributes(rollback) | FileAttributes.ReadOnly);

            CopyAndFlush(decrypted, staged);
            ValidateSqliteIntegrity(staged);
            SqliteConnection.ClearAllPools();

            walBackup = MoveSidecarAside(databasePath + "-wal");
            shmBackup = MoveSidecarAside(databasePath + "-shm");
            File.Replace(staged, databasePath, null, ignoreMetadataErrors: true);
            ValidateSqliteIntegrity(databasePath);
            LegacySecurityAuditService.Write(managementProof, ProtectedAction.Restore, scope, true);
            RecoveryRequiredStateStore.ClearAfterVerifiedRecovery(databasePath);
        }
        catch
        {
            SqliteConnection.ClearAllPools();
            try
            {
                TryRestoreRollback(databasePath, rollback);
            }
            catch
            {
                RecoveryRequiredStateStore.MarkRequired(databasePath, "LegacyRestoreRecoveryFailed");
                throw;
            }
            throw;
        }
        finally
        {
            DeleteIfExists(decrypted);
            DeleteIfExists(staged);
            DeleteIfExists(walBackup);
            DeleteIfExists(shmBackup);
            SqliteConnection.ClearAllPools();
        }
    }

    public static void FactoryReset(ManagementAuthorizationProof managementProof,
        int currentManagementCredentialVersion, string verifiedBackupPath)
    {
        if (string.IsNullOrWhiteSpace(verifiedBackupPath) || !File.Exists(verifiedBackupPath))
            throw new InvalidOperationException("ریست بدون نسخه پشتیبان معتبر مجاز نیست.");
        string databasePath = SqliteDatabaseHelper.GetDatabasePath();
        string verifiedBackup = Path.GetFullPath(verifiedBackupPath);
        if (string.Equals(databasePath, verifiedBackup, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Verified backup must be a separate file.");
        string scope = SqliteProtectedActionBinding.CreateBackupScope(
            databasePath, verifiedBackup, BackupOverwritePolicy.Deny);
        EnsureAuthorization(managementProof, ProtectedAction.EmergencyRecovery, scope,
            currentManagementCredentialVersion);
        ValidateSqliteIntegrity(verifiedBackup);
        ValidateBackupCompatibility(databasePath, verifiedBackup);
        SqliteConnection.ClearAllPools();
        File.Delete(databasePath);
        DeleteIfExists(databasePath + "-wal");
        DeleteIfExists(databasePath + "-shm");
        LegacySecurityAuditService.Write(managementProof, ProtectedAction.EmergencyRecovery, scope, true);
    }

    private static void EnsureAuthorization(ManagementAuthorizationProof proof,
        ProtectedAction action, string scope, int currentVersion)
    {
        ArgumentNullException.ThrowIfNull(proof);
        ManagementProofValidationResult validation = ManagementAuthorizationProofValidator.Validate(
            proof, proof.InitiatingShiftProfileId, action, scope, proof.CorrelationId,
            currentVersion, DateTimeOffset.UtcNow);
        if (!validation.IsValid)
            throw new UnauthorizedAccessException("ManagementCredential proof is invalid or expired.");
    }

    private static string CreateTemporaryPath(string purpose) =>
        Path.Combine(Path.GetTempPath(), $"RahNegar_{purpose}_{Guid.NewGuid():N}.db");

    private static void CreateVerifiedSqliteCopy(string sourcePath, string destinationPath)
    {
        DeleteIfExists(destinationPath);
        using SqliteConnection source = new($"Data Source={sourcePath};Mode=ReadOnly;Pooling=False");
        using SqliteConnection destination = new($"Data Source={destinationPath};Pooling=False");
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);
        ValidateSqliteIntegrity(destinationPath);
    }

    private static void ValidateSqliteIntegrity(string path)
    {
        using SqliteConnection connection = new($"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using SqliteCommand integrity = connection.CreateCommand();
        integrity.CommandText = "PRAGMA integrity_check;";
        if (!string.Equals(integrity.ExecuteScalar()?.ToString(), "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("بررسی صحت دیتابیس ناموفق بود.");
        using SqliteCommand foreignKeys = connection.CreateCommand();
        foreignKeys.CommandText = "PRAGMA foreign_key_check;";
        using SqliteDataReader reader = foreignKeys.ExecuteReader();
        if (reader.Read()) throw new InvalidDataException("نقض ارتباط داده‌ها در دیتابیس وجود دارد.");
    }

    private static string ComputeSha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static void WriteChecksumReceipt(string receiptPath, string sha256, string backupPath)
    {
        string temporary = receiptPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new(stream))
            {
                writer.Write(sha256);
                writer.Write("  ");
                writer.WriteLine(Path.GetFileName(backupPath));
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, receiptPath);
        }
        finally { DeleteIfExists(temporary); }
    }

    private static void ValidateOptionalChecksumReceipt(string backupPath, string actualSha256)
    {
        string receiptPath = backupPath + ".sha256";
        if (!File.Exists(receiptPath)) return;
        string expected = File.ReadAllText(receiptPath).Trim().Split((char[]?)null,
            StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        if (!string.Equals(expected, actualSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Backup SHA-256 checksum receipt does not match the backup.");
    }

    private static void CopyAndFlush(string source, string destination)
    {
        using FileStream input = new(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        using FileStream output = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        input.CopyTo(output);
        output.Flush(flushToDisk: true);
    }

    private static string? MoveSidecarAside(string path)
    {
        if (!File.Exists(path)) return null;
        string moved = path + ".before-" + Guid.NewGuid().ToString("N");
        File.Move(path, moved);
        return moved;
    }

    private static void TryRestoreRollback(string databasePath, string rollbackPath)
    {
        try
        {
            if (!File.Exists(rollbackPath)) return;
            string restore = databasePath + ".recovery-" + Guid.NewGuid().ToString("N");
            File.Copy(rollbackPath, restore);
            File.Replace(restore, databasePath, null, ignoreMetadataErrors: true);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("بازیابی پس از خطای جایگزینی ناموفق بود؛ RecoveryRequired.", exception);
        }
    }

    private static void ValidateBackupCompatibility(string currentPath, string importedPath)
    {
        DatabaseIdentity current = ReadDatabaseIdentity(currentPath);
        DatabaseIdentity imported = ReadDatabaseIdentity(importedPath);
        if (!string.Equals(current.StationType, imported.StationType, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(current.StationName, imported.StationName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("این فایل پشتیبان مربوط به پروفایل فعلی برنامه نیست.");
    }

    private static DatabaseIdentity ReadDatabaseIdentity(string path)
    {
        using SqliteConnection connection = new($"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT station_type, station_name FROM app_settings LIMIT 1;";
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read()) throw new InvalidDataException("فایل پشتیبان معتبر نیست.");
        return new(reader.GetString(0), reader.GetString(1));
    }

    private static void DeleteIfExists(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) File.Delete(path);
    }

    private sealed record DatabaseIdentity(string StationType, string StationName);
}

internal static class LegacySecurityAuditService
{
    public static void Write(SqliteConnection connection, SqliteTransaction transaction,
        ManagementAuthorizationProof proof, ProtectedAction action, string scope, bool succeeded)
    {
        using SqliteCommand create = connection.CreateCommand();
        create.Transaction = transaction;
        create.CommandText = """
            CREATE TABLE IF NOT EXISTS tbl_security_audit (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                action TEXT NOT NULL, scope TEXT NOT NULL, actor TEXT NOT NULL,
                correlation_id TEXT NOT NULL, succeeded INTEGER NOT NULL, created_at TEXT NOT NULL);
            """;
        create.ExecuteNonQuery();
        using SqliteCommand insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO tbl_security_audit(action,scope,actor,correlation_id,succeeded,created_at)
            VALUES(@action,@scope,@actor,@correlation,@succeeded,@created);
            """;
        insert.Parameters.AddWithValue("@action", action.ToString());
        insert.Parameters.AddWithValue("@scope", scope);
        insert.Parameters.AddWithValue("@actor", proof.InitiatingShiftProfileId);
        insert.Parameters.AddWithValue("@correlation", proof.CorrelationId);
        insert.Parameters.AddWithValue("@succeeded", succeeded ? 1 : 0);
        insert.Parameters.AddWithValue("@created", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        insert.ExecuteNonQuery();
    }

    public static void Write(ManagementAuthorizationProof proof, ProtectedAction action,
        string scope, bool succeeded)
    {
        using SqliteConnection connection = SqliteDatabaseHelper.CreateConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();
        Write(connection, transaction, proof, action, scope, succeeded);
        transaction.Commit();
    }
}
