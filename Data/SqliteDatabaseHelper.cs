using Microsoft.Data.Sqlite;

namespace Rah_Negar.Data;

/// <summary>
/// مدیریت اتصال به دیتابیس SQLite و اعمال تنظیمات عملکرد
/// </summary>
public static class SqliteDatabaseHelper
{
    public static string GetDataDirectoryPath()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
    }

    public static string GetDatabasePath()
    {
        return Path.Combine(GetDataDirectoryPath(), "db.sys");
    }

    public static void EnsureDataDirectoryExists()
    {
        string path = GetDataDirectoryPath();

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    public static SqliteConnection CreateConnection()
    {
        EnsureDataDirectoryExists();

        string dbPath = GetDatabasePath();

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Default,
            Pooling = true,
            DefaultTimeout = 10
        };

        var conn = new SqliteConnection(builder.ToString());
        conn.Open();

        // تنظیمات اصلی برای عملکرد بهتر و پایداری بیشتر
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;
PRAGMA foreign_keys = ON;
PRAGMA temp_store = MEMORY;
";
        cmd.ExecuteNonQuery();

        return conn;
    }
}