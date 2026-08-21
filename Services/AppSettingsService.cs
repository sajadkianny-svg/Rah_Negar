using Microsoft.Data.Sqlite;
using Rah_Negar.Core;
using Rah_Negar.Data;
using Rah_Negar.Models;
using Rah_Negar.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace Rah_Negar.Services;

/// <summary>
/// سرویس خواندن و بروزرسانی تنظیمات اصلی برنامه از دیتابیس
/// </summary>
public static class AppSettingsService
{
    /// <summary>
    /// اولین و تنها رکورد جدول app_settings را می‌خواند.
    /// اگر رکوردی وجود نداشته باشد null برمی‌گرداند.
    /// </summary>
    public static AppSettingsModel? GetSettings()
    {
        using var conn = SqliteDatabaseHelper.CreateConnection();

        const string sql = @"
SELECT 
    is_initialized,
    station_type,
    station_name,
    user_reset_password_hash,
    user_reset_password_salt,
    created_at,
    theme_index,
    esd_extra_runtime_enabled,
    esd_extra_runtime_hours,
    data_start_date
FROM app_settings
LIMIT 1;";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        using var reader = cmd.ExecuteReader();

        if (!reader.Read())
            return null;

        return new AppSettingsModel
        {
            IsInitialized = Convert.ToInt32(reader["is_initialized"]) == 1,
            StationType = Enum.TryParse(reader["station_type"]?.ToString(),
            out StationType stationType)
                ? stationType
                : StationType.Unknown,
            StationName = reader["station_name"]?.ToString() ?? string.Empty,
            UserResetPasswordHash = reader["user_reset_password_hash"]?.ToString() ?? string.Empty,
            UserResetPasswordSalt = reader["user_reset_password_salt"]?.ToString() ?? string.Empty,
            CreatedAt = DateTime.TryParse(reader["created_at"]?.ToString(), out DateTime dt) ? dt : DateTime.MinValue,
            ThemeIndex = reader["theme_index"] == DBNull.Value
                ? 0
                : Convert.ToInt32(reader["theme_index"]),
            EsdExtraRuntimeEnabled =
                    Convert.ToInt32(reader["esd_extra_runtime_enabled"]) == 1,

            EsdExtraRuntimeHours =
                    Convert.ToDouble(reader["esd_extra_runtime_hours"]),

            DataStartDateRep = reader["data_start_date"] == DBNull.Value
                ? 0 : Convert.ToInt64(reader["data_start_date"])

        };
    }


    /// <summary>
    /// ذخیره تنظیمات مربوط به افزودن ساعت کارکرد بعد از NSD.
    /// </summary>
    public static void SaveNsdRuntimeSettings(bool enabled, double extraHours)
    {
        using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();

        const string sql = @"
UPDATE app_settings
SET
    esd_extra_runtime_enabled = @enabled,
    esd_extra_runtime_hours = @hours
WHERE id = (
    SELECT id FROM app_settings
    ORDER BY id
    LIMIT 1
);";

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        cmd.Parameters.AddWithValue("@enabled", enabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@hours", extraHours);

        cmd.ExecuteNonQuery();
    }
    /// <summary>
    /// بروزرسانی هش و Salt رمز عبور در جدول app_settings
    /// </summary>
    public static void UpdatePassword(string passwordHash, string passwordSalt)
    {
        using var conn = SqliteDatabaseHelper.CreateConnection();
        using var tx = conn.BeginTransaction();

        try
        {
            const string sql = @"
UPDATE app_settings
SET
    user_reset_password_hash = @hash,
    user_reset_password_salt = @salt,
    password_changed_at = @password_changed_at;";

            var parameters = new List<SqliteParameter>
        {
            SqliteCommandHelper.Param("@hash", passwordHash),

            SqliteCommandHelper.Param("@salt", passwordSalt),

            SqliteCommandHelper.Param(
                "@password_changed_at",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
        };

            SqliteCommandHelper.ExecuteNonQuery(
                conn,
                sql,
                parameters,
                tx);

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }


    /// <summary>
    /// ذخیره شماره تم فعال برنامه در جدول تنظیمات.
    /// </summary>
    public static void SaveThemeIndex(int themeIndex)
    {
        using var conn = SqliteDatabaseHelper.CreateConnection();

        const string sql = @"
UPDATE app_settings
SET theme_index = @theme_index;";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@theme_index", themeIndex);
        cmd.ExecuteNonQuery();
    }


    /// <summary>
    /// رمز ورود واردشده را با هش و salt ذخیره‌شده در تنظیمات برنامه مقایسه می‌کند.
    /// </summary>
    public static bool VerifyLoginPassword(string password)
    {
        using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();

        const string sql = @"
SELECT user_reset_password_hash, user_reset_password_salt
FROM app_settings
LIMIT 1;";

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        using SqliteDataReader reader = cmd.ExecuteReader();

        if (!reader.Read())
            return false;

        string hash = reader["user_reset_password_hash"]?.ToString() ?? "";
        string salt = reader["user_reset_password_salt"]?.ToString() ?? "";

        if (string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(salt))
            return false;

        string enteredHash = PasswordHelper.HashPassword(password, salt);

        return string.Equals(hash, enteredHash, StringComparison.Ordinal);
    }

    /// <summary>
    /// تاریخ مبنای شروع داده‌ها را از تنظیمات برنامه می‌خواند
    /// </summary>
    public static long GetDataStartDate()
    {
        AppSettingsModel? settings = GetSettings();

        long dataStartDate = settings?.DataStartDateRep ?? 0;

        if (dataStartDate <= 0)
        {
            string shownDate = dataStartDate > 0
                ? DateFormatHelper.FormatDateRep(dataStartDate)
                : "ثبت نشده";
            MessageBox.Show(settings?.DataStartDateRep.ToString() ?? "NULL");

            MessageBox.Show(
                "تاریخ مبنای شروع داده‌ها معتبر نیست" +
                Environment.NewLine +
                Environment.NewLine +
                "تاریخ ثبت‌شده" +
                Environment.NewLine +
                shownDate +
                Environment.NewLine +
                Environment.NewLine +
                "اولین ثبت داده باید دقیقاً از تاریخ مبنای شروع انجام شود" +
                Environment.NewLine +
                "در صورت نیاز می‌توانید قبل از اولین ثبت، این تاریخ را از تنظیمات اصلاح کنید",
                "خطا در تنظیمات",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            return 0;
        }

        return dataStartDate;
    }


    /// <summary>
    /// تاریخ مبنای شروع داده‌ها را در تنظیمات برنامه ذخیره می‌کند
    /// این متد فقط باید قبل از اولین ثبت داده استفاده شود
    /// </summary>
    public static void SaveDataStartDate(long dataStartDate)
    {
        if (dataStartDate <= 0)
            throw new InvalidOperationException("تاریخ مبنای شروع داده‌ها معتبر نیست");

        using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();
        using SqliteCommand cmd = conn.CreateCommand();

        cmd.CommandText = @"
UPDATE app_settings
SET data_start_date = @data_start_date
WHERE id = (
    SELECT id
    FROM app_settings
    ORDER BY id
    LIMIT 1
);";

        cmd.Parameters.AddWithValue("@data_start_date", dataStartDate);

        cmd.ExecuteNonQuery();
    }


    /// <summary>
    /// بررسی می‌کند تاریخ انتخاب‌شده قبل از تاریخ مبنای شروع داده‌ها نباشد
    /// </summary>
    public static bool IsDateAllowedByDataStartDate(long selectedDate)
    {
        long dataStartDate = AppSettingsService.GetDataStartDate();

        if (dataStartDate <= 0)
            return false;

        return selectedDate >= dataStartDate;
    }


    public static string BuildDataStartDateViolationMessage(long selectedDate)
    {
        long dataStartDate = AppSettingsService.GetDataStartDate();

        return
            "تاریخ انتخاب‌شده قبل از تاریخ مبنای شروع داده‌ها است" +
            Environment.NewLine +
            Environment.NewLine +
            "تاریخ انتخاب‌شده" +
            Environment.NewLine +
            DateFormatHelper.FormatDateRep(selectedDate) +
            Environment.NewLine +
            Environment.NewLine +
            "اولین تاریخ مجاز" +
            Environment.NewLine +
            DateFormatHelper.FormatDateRep(dataStartDate);
    }

    /// <summary>
    /// /ذخیره تاریخ آخرین بک اپ تهیه شده 
    /// </summary>
    public static void SaveLastBackupDate(DateTime dateTime)
    {
        using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();

        using SqliteCommand cmd = conn.CreateCommand();

        cmd.CommandText = @"
UPDATE app_settings
SET last_backup_at = @last_backup_at;";

        cmd.Parameters.AddWithValue(
            "@last_backup_at",
            dateTime.ToString("yyyy-MM-dd HH:mm:ss"));

        cmd.ExecuteNonQuery();
    }
}

