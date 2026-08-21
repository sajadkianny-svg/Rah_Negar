using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Data.Sqlite;
using Rah_Negar.Data;

namespace Rah_Negar.Services;

/// <summary>
/// عملیات نگهداری و بهینه‌سازی دیتابیس SQLite را انجام می‌دهد.
/// </summary>
public static class DatabaseMaintenanceService
{

    /// <summary>
    /// اطلاعات شناسایی دیتابیس برای بررسی سازگاری Backup.
    /// </summary>
    private sealed class DatabaseIdentity
    {
        public string StationType { get; init; } = string.Empty;

        public string StationName { get; init; } = string.Empty;
    }
    /// <summary>
    /// ایندکس‌های دیتابیس را بازسازی و آمارهای SQLite را به‌روزرسانی می‌کند.
    /// </summary>
    public static void RepairIndexes()
    {
        using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();

        using SqliteCommand cmd = conn.CreateCommand();

        cmd.CommandText = @"
REINDEX;
ANALYZE;
PRAGMA optimize;
";

        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// خروجی رمزنگاری‌شده از دیتابیس تهیه می‌کند.
    /// ابتدا با SQLite Backup API یک نسخه موقت سالم ساخته می‌شود،
    /// سپس پس از بسته شدن کامل اتصال‌ها، همان نسخه رمزنگاری می‌شود.
    /// </summary>
    public static void ExportDatabase(string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("مسیر خروجی معتبر نیست", nameof(destinationPath));

        string databasePath = SqliteDatabaseHelper.GetDatabasePath();

        if (!File.Exists(databasePath))
            throw new FileNotFoundException("فایل دیتابیس پیدا نشد", databasePath);

        string tempPath = Path.Combine(
            Path.GetTempPath(),
            $"RahNegar_ExportTemp_{Guid.NewGuid():N}.db");

        try
        {
            using (SqliteConnection sourceConn = SqliteDatabaseHelper.CreateConnection())
            using (SqliteConnection backupConn = new($"Data Source={tempPath};Pooling=False"))
            {
                backupConn.Open();
                sourceConn.BackupDatabase(backupConn);
            }

            SqliteConnection.ClearAllPools();

            BackupEncryptionService.EncryptFile(tempPath, destinationPath);

            AppSettingsService.SaveLastBackupDate(DateTime.UtcNow);
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
    /// <summary>
    /// فایل Backup رمزنگاری‌شده را وارد کرده و جایگزین دیتابیس فعلی می‌کند.
    /// قبل از جایگزینی، سازگاری پروفایل Backup با دیتابیس فعلی بررسی می‌شود.
    /// </summary>
    public static void ImportDatabase(string backupPath)
    {
        if (string.IsNullOrWhiteSpace(backupPath))
            throw new ArgumentException("مسیر پشتیبانی معتبر نیست", nameof(backupPath));

        if (!File.Exists(backupPath))
            throw new FileNotFoundException("فایل پشتیبانی پیدا نشد", backupPath);

        string databasePath = SqliteDatabaseHelper.GetDatabasePath();

        string dbDir = Path.GetDirectoryName(databasePath)
            ?? throw new InvalidOperationException("مسیر دیتابیس معتبر نیست");

        string tempDbPath = Path.Combine(
            dbDir,
            $"RahNegar_ImportTemp_{Guid.NewGuid():N}.db");

        string safetyBackupPath = Path.Combine(
            dbDir,
            $"RahNegar_BeforeImport_{DateTime.Now:yyyyMMdd_HHmmss}.db");

        try
        {
            // 1. رمزگشایی Backup در فایل موقت
            BackupEncryptionService.DecryptFile(backupPath, tempDbPath);

            if (!File.Exists(tempDbPath))
                throw new InvalidOperationException("فایل پشتیبانی معتبر نیست");

            // 2. بستن اتصال‌های قبلی
            SqliteConnection.ClearAllPools();

            // 3. بررسی سازگاری پروفایل Backup با دیتابیس فعلی
            ValidateBackupCompatibility(databasePath, tempDbPath);

            // 4. جایگزینی دیتابیس
            File.Copy(tempDbPath, databasePath, overwrite: true);
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            if (File.Exists(tempDbPath))
                File.Delete(tempDbPath);
        }
    }

    /// <summary>
    /// دیتابیس فعلی را حذف می‌کند تا برنامه در اجرای بعدی از ابتدا راه‌اندازی شود.
    /// قبل از اجرای این عملیات، کاربر باید به صورت دستی از دیتابیس Backup تهیه کرده باشد.
    /// </summary>
    public static void FactoryReset()
    {
        string databasePath = SqliteDatabaseHelper.GetDatabasePath();

        if (!File.Exists(databasePath))
            return;

        SqliteConnection.ClearAllPools();

        File.Delete(databasePath);
    }



    /// <summary>
    /// اطلاعات پروفایل ذخیره‌شده در جدول app_settings را از دیتابیس مشخص‌شده می‌خواند.
    /// </summary>
    private static DatabaseIdentity ReadDatabaseIdentity(string databasePath)
    {
        if (!File.Exists(databasePath))
            throw new FileNotFoundException("فایل دیتابیس برای بررسی هویت پیدا نشد", databasePath);

        string connectionString = $"Data Source={databasePath};Pooling=False";

        using SqliteConnection conn = new(connectionString);
        conn.Open();

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT station_type, station_name
FROM app_settings
LIMIT 1;";

        using SqliteDataReader reader = cmd.ExecuteReader();

        if (!reader.Read())
            throw new InvalidDataException("فایل پشتیبانی معتبر نیست");

        return new DatabaseIdentity
        {
            StationType = reader["station_type"]?.ToString() ?? string.Empty,
            StationName = reader["station_name"]?.ToString() ?? string.Empty
        };
    }


    /// <summary>
    /// بررسی می‌کند که دیتابیس Backup با دیتابیس فعلی از نظر پروفایل ایستگاه سازگار باشد.
    /// </summary>
    private static void ValidateBackupCompatibility(string currentDatabasePath, string importedDatabasePath)
    {
        DatabaseIdentity current = ReadDatabaseIdentity(currentDatabasePath);
        DatabaseIdentity imported = ReadDatabaseIdentity(importedDatabasePath);

        bool isSameStationType = string.Equals(
            current.StationType,
            imported.StationType,
            StringComparison.OrdinalIgnoreCase);

        bool isSameStationName = string.Equals(
            current.StationName,
            imported.StationName,
            StringComparison.OrdinalIgnoreCase);

        if (!isSameStationType || !isSameStationName)
        {
            throw new InvalidOperationException(
                "این فایل پشتیبانی مربوط به پروفایل فعلی برنامه نیست" +
                Environment.NewLine + Environment.NewLine +
                $"Current: {current.StationName} ({current.StationType})" +
                Environment.NewLine +
                $"Backup: {imported.StationName} ({imported.StationType})");
        }
    }


}
