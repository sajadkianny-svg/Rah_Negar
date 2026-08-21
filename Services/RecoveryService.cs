using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Rah_Negar.Data;

namespace Rah_Negar.Services;

/// <summary>
/// سرویس مربوط به فراموشی رمز عبور، ساخت درخواست بازیابی،
/// تولید کد یکبارمصرف، و بررسی صحت کد بازیابی
/// </summary>
public static class RecoveryService
{
    /// <summary>
    /// کلید مخفی بازیابی
    /// </summary>
    private static string GetRecoverySecretKey()
    {
        string p1 = "Rn@";
        string p2 = "Recovery";
        string p3 = "Key";
        string p4 = "_2026";

        return p1 + p2 + p3 + p4;
    }

    /// <summary>
    /// ساخت جدول بازیابی رمز عبور در صورت نبودن
    /// </summary>
    public static void EnsureRecoveryTable()
    {
        using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();

        const string sql = @"
CREATE TABLE IF NOT EXISTS tbl_recovery (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    station_name TEXT NOT NULL,
    request_id TEXT NOT NULL,
    is_used INTEGER NOT NULL,
    created_at TEXT NOT NULL
);";

        SqliteCommandHelper.ExecuteNonQuery(conn, sql);

        const string idx1 = @"
CREATE INDEX IF NOT EXISTS idx_tbl_recovery_request_id
ON tbl_recovery(request_id);";

        SqliteCommandHelper.ExecuteNonQuery(conn, idx1);

        const string idx2 = @"
CREATE INDEX IF NOT EXISTS idx_tbl_recovery_station_name
ON tbl_recovery(station_name);";

        SqliteCommandHelper.ExecuteNonQuery(conn, idx2);
    }

    /// <summary>
    /// تولید یک شناسه یکتای 8 کاراکتری شامل حروف بزرگ و عدد
    /// </summary>
    public static string GenerateRequestId()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        StringBuilder sb = new StringBuilder();

        byte[] data = RandomNumberGenerator.GetBytes(8);

        foreach (byte b in data)
        {
            sb.Append(chars[b % chars.Length]);
        }

        return sb.ToString();
    }

    /// <summary>
    /// ایجاد یک درخواست جدید بازیابی رمز عبور
    /// </summary>
    public static string CreateRecoveryRequest(string stationName)
    {
        if (string.IsNullOrWhiteSpace(stationName))
            throw new ArgumentException("Station name is required.");

        EnsureRecoveryTable();

        using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();
        using SqliteTransaction tx = conn.BeginTransaction();

        try
        {
            const string deleteOldSql = @"
DELETE FROM tbl_recovery
WHERE station_name = @station_name
  AND is_used = 0;";

            var deleteParams = new List<SqliteParameter>
            {
                SqliteCommandHelper.Param("@station_name", stationName)
            };

            SqliteCommandHelper.ExecuteNonQuery(conn, deleteOldSql, deleteParams, tx);

            string requestId = GenerateRequestId();

            const string insertSql = @"
INSERT INTO tbl_recovery
(
    station_name,
    request_id,
    is_used,
    created_at
)
VALUES
(
    @station_name,
    @request_id,
    @is_used,
    @created_at
);";

            var insertParams = new List<SqliteParameter>
            {
                SqliteCommandHelper.Param("@station_name", stationName),
                SqliteCommandHelper.Param("@request_id", requestId),
                SqliteCommandHelper.Param("@is_used", 0),
                SqliteCommandHelper.Param("@created_at", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"))
            };

            SqliteCommandHelper.ExecuteNonQuery(conn, insertSql, insertParams, tx);

            tx.Commit();
            return requestId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>
    /// تولید کد بازیابی 10 رقمی بر اساس نام ایستگاه و request_id
    /// </summary>
    public static string GenerateRecoveryCode(string stationName, string requestId)
    {
        if (string.IsNullOrWhiteSpace(stationName))
            throw new ArgumentException("Station name is required.");

        if (string.IsNullOrWhiteSpace(requestId))
            throw new ArgumentException("Request ID is required.");

        string rawData = $"{stationName}|{requestId}";

        using HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(GetRecoverySecretKey()));
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));

        StringBuilder digits = new StringBuilder();

        foreach (byte b in hash)
        {
            digits.Append((b % 10).ToString());
        }

        string code = digits.ToString();

        if (code.Length < 10)
            code = code.PadRight(10, '0');

        return code.Substring(0, 10);
    }

    /// <summary>
    /// بررسی صحت کد بازیابی واردشده توسط کاربر
    /// </summary>
    public static bool ValidateRecoveryCode(string stationName, string requestId, string enteredCode)
    {
        if (string.IsNullOrWhiteSpace(stationName))
            return false;

        if (string.IsNullOrWhiteSpace(requestId))
            return false;

        if (string.IsNullOrWhiteSpace(enteredCode))
            return false;

        EnsureRecoveryTable();

        using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();
        using SqliteTransaction tx = conn.BeginTransaction();

        try
        {
            const string selectSql = @"
SELECT COUNT(*)
FROM tbl_recovery
WHERE station_name = @station_name
  AND request_id = @request_id
  AND is_used = 0;";

            var selectParams = new List<SqliteParameter>
            {
                SqliteCommandHelper.Param("@station_name", stationName),
                SqliteCommandHelper.Param("@request_id", requestId)
            };

            object? result = SqliteCommandHelper.ExecuteScalar(conn, selectSql, selectParams, tx);
            int count = Convert.ToInt32(result ?? 0);

            if (count == 0)
            {
                tx.Commit();
                return false;
            }

            string realCode = GenerateRecoveryCode(stationName, requestId);

            if (!string.Equals(realCode, enteredCode.Trim(), StringComparison.Ordinal))
            {
                tx.Commit();
                return false;
            }

            const string updateSql = @"
UPDATE tbl_recovery
SET is_used = 1
WHERE station_name = @station_name
  AND request_id = @request_id
  AND is_used = 0;";

            var updateParams = new List<SqliteParameter>
            {
                SqliteCommandHelper.Param("@station_name", stationName),
                SqliteCommandHelper.Param("@request_id", requestId)
            };

            SqliteCommandHelper.ExecuteNonQuery(conn, updateSql, updateParams, tx);

            tx.Commit();
            return true;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>
    /// گرفتن آخرین request_id استفاده‌نشده برای یک ایستگاه
    /// </summary>
    public static string? GetActiveRequestId(string stationName)
    {
        if (string.IsNullOrWhiteSpace(stationName))
            return null;

        EnsureRecoveryTable();

        using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();

        const string sql = @"
SELECT request_id
FROM tbl_recovery
WHERE station_name = @station_name
  AND is_used = 0
ORDER BY id DESC
LIMIT 1;";

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@station_name", stationName);

        object? result = cmd.ExecuteScalar();

        return result?.ToString();
    }
}