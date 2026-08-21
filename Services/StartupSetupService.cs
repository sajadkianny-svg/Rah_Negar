
using Microsoft.Data.Sqlite;
using Rah_Negar.Core;
using Rah_Negar.Data;
using Rah_Negar.Models;
using Rah_Negar.Utils;

namespace Rah_Negar.Services;

/// <summary>
/// مسئول ساخت اولیه سیستم بر اساس اطلاعات فرم Startup
/// </summary>
public static class StartupSetupService
{
    /// <summary>
    /// راه‌اندازی اولیه برنامه:
    /// 1- ساخت جداول پایه
    /// 2- ساخت جداول اختصاصی پروفایل
    /// 3- ذخیره تنظیمات اولیه
    /// 4- ذخیره کارکرد پایه واحدها
    /// </summary>
    public static void InitializeApplication(StartupSetupData setupData)
    {
        if (setupData == null)
            throw new ArgumentNullException(nameof(setupData));

        IStationProfile profile = ProfileManager.GetProfile(setupData.StationType);

        using SqliteConnection conn = SqliteDatabaseHelper.CreateConnection();
        using SqliteTransaction tx = conn.BeginTransaction();

        try
        {
            CreateBaseTables(conn, tx);

            // ساخت جداول مشترک و tbl_data اختصاصی ایستگاه
            StationSchemaBuilderService.Build(profile, conn, tx);

            SaveAppSettings(conn, tx, setupData);
            SaveUnitRuntimeBase(conn, tx, setupData.UnitRuntimeBases);

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>
    /// ساخت جدول‌های پایه سیستم
    /// </summary>
    private static void CreateBaseTables(SqliteConnection conn, SqliteTransaction tx)
    {
        const string appSettingsSql = @"
CREATE TABLE IF NOT EXISTS app_settings (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    is_initialized INTEGER NOT NULL,
    station_type TEXT NOT NULL,
    station_name TEXT NOT NULL,
    user_reset_password_hash TEXT NOT NULL,
    user_reset_password_salt TEXT NOT NULL,
    created_at TEXT NOT NULL,
    last_backup_at TEXT,
    password_changed_at TEXT,
    theme_index INTEGER NOT NULL DEFAULT 0,
    esd_extra_runtime_enabled INTEGER NOT NULL DEFAULT 0,
    esd_extra_runtime_hours REAL NOT NULL DEFAULT 0,
    data_start_date INTEGER NOT NULL DEFAULT 0

);";

        const string unitRuntimeSql = @"
CREATE TABLE IF NOT EXISTS unit_runtime_base (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    unit_no INTEGER NOT NULL,
    base_runtime_hours REAL NOT NULL,
    base_runtime_after_oh_hours REAL NOT NULL,
    initial_is_running INTEGER NOT NULL DEFAULT 0,
    initial_status TEXT NOT NULL DEFAULT 'OFF'
);";


        // وضعیت قفل بودن ماه‌ها برای جلوگیری از ویرایش پس از نهایی‌سازی
        const string monthlyLockSql = @"
CREATE TABLE IF NOT EXISTS tbl_monthly_lock (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    year_rep INTEGER NOT NULL,
    month_rep INTEGER NOT NULL,
    is_locked INTEGER NOT NULL DEFAULT 0,
    locked_at TEXT,
    locked_by TEXT,
    UNIQUE(year_rep, month_rep)
);";

        // اطلاعات کلی گزارش نهایی‌شده ماهانه (متادیتا گزارش)
        const string monthlyReportHeaderSql = @"
CREATE TABLE IF NOT EXISTS tbl_monthly_report_header (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    year_rep INTEGER NOT NULL,
    month_rep INTEGER NOT NULL,
    station_name TEXT NOT NULL,
    finalized_at TEXT NOT NULL,
    finalized_by TEXT,
    data_start_date INTEGER NOT NULL,
    report_title TEXT NOT NULL,
    UNIQUE(year_rep, month_rep)
);";

        // خلاصه پارامترهای اصلی tbl_data به صورت min / max / avg برای هر ماه
        const string monthlyReportSummarySql = @"
CREATE TABLE IF NOT EXISTS tbl_monthly_report_summary (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    year_rep INTEGER NOT NULL,
    month_rep INTEGER NOT NULL,
    parameter_key TEXT NOT NULL,
    parameter_title TEXT NOT NULL,
    aggregation_type TEXT NOT NULL,
    value REAL,
    value_count INTEGER NOT NULL DEFAULT 0,
    UNIQUE(year_rep, month_rep, parameter_key, aggregation_type)
);";


        // مقادیر تجمیعی از tbl_unique مانند مصرف سوخت، فلوی توربینی و غیرتوربینی
        const string monthlyReportUniqueSummarySql = @"
CREATE TABLE IF NOT EXISTS tbl_monthly_report_unique_summary (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    year_rep INTEGER NOT NULL,
    month_rep INTEGER NOT NULL,
    item_key TEXT NOT NULL,
    item_title TEXT NOT NULL,
    value REAL NOT NULL DEFAULT 0,
    UNIQUE(year_rep, month_rep, item_key)
);";


        // خلاصه تعداد رویدادها به تفکیک واحد و نوع رویداد در هر ماه

        const string monthlyReportEventSummarySql = @"
CREATE TABLE IF NOT EXISTS tbl_monthly_report_event_summary (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    year_rep INTEGER NOT NULL,
    month_rep INTEGER NOT NULL,
    unit TEXT NOT NULL,
    event_type TEXT NOT NULL,
    event_count INTEGER NOT NULL DEFAULT 0,
    UNIQUE(year_rep, month_rep, unit, event_type)
);";


        // شاخص‌های عملکردی ماه مانند روزهای سرویس، runtime و سایر محاسبات نهایی
        const string monthlyReportServiceSummarySql = @"
CREATE TABLE IF NOT EXISTS tbl_monthly_report_service_summary (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    year_rep INTEGER NOT NULL,
    month_rep INTEGER NOT NULL,
    item_key TEXT NOT NULL,
    item_title TEXT NOT NULL,
    value REAL NOT NULL DEFAULT 0,
    UNIQUE(year_rep, month_rep, item_key)
);";


        // خلاصه محاسبات رویدادها و Runtime هر واحد در گزارش ماهانه نهایی‌شده
        const string monthlyReportUnitEventSummarySql = @"
CREATE TABLE IF NOT EXISTS tbl_monthly_report_unit_event_summary (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    year_rep INTEGER NOT NULL,
    month_rep INTEGER NOT NULL,
    unit TEXT NOT NULL,
    runtime_hours REAL NOT NULL DEFAULT 0,
    runtime_after_oh REAL NOT NULL DEFAULT 0,
    total_events INTEGER NOT NULL DEFAULT 0,
    start_count INTEGER NOT NULL DEFAULT 0,
    nsd_count INTEGER NOT NULL DEFAULT 0,
    esd_count INTEGER NOT NULL DEFAULT 0,
    esd_extra_hours_total REAL NOT NULL DEFAULT 0,
    longest_run_hours REAL NOT NULL DEFAULT 0,
    day_start_count INTEGER NOT NULL DEFAULT 0,
    night_start_count INTEGER NOT NULL DEFAULT 0,
    day_nsd_count INTEGER NOT NULL DEFAULT 0,
    night_nsd_count INTEGER NOT NULL DEFAULT 0,
    day_esd_count INTEGER NOT NULL DEFAULT 0,
    night_esd_count INTEGER NOT NULL DEFAULT 0,
    UNIQUE(year_rep, month_rep, unit)
);";

        SqliteCommandHelper.ExecuteNonQuery(conn, appSettingsSql, transaction: tx);
        SqliteCommandHelper.ExecuteNonQuery(conn, unitRuntimeSql, transaction: tx);
        SqliteCommandHelper.ExecuteNonQuery(conn, monthlyLockSql, transaction: tx);
        SqliteCommandHelper.ExecuteNonQuery(conn, monthlyReportHeaderSql, transaction: tx);
        SqliteCommandHelper.ExecuteNonQuery(conn, monthlyReportSummarySql, transaction: tx);
        SqliteCommandHelper.ExecuteNonQuery(conn, monthlyReportUniqueSummarySql, transaction: tx);
        SqliteCommandHelper.ExecuteNonQuery(conn, monthlyReportEventSummarySql, transaction: tx);
        SqliteCommandHelper.ExecuteNonQuery(conn, monthlyReportServiceSummarySql, transaction: tx);
        SqliteCommandHelper.ExecuteNonQuery(conn, monthlyReportUnitEventSummarySql, transaction: tx);

    }

    /// <summary>
    /// ذخیره اطلاعات پایه برنامه
    /// </summary>
    private static void SaveAppSettings(SqliteConnection conn, SqliteTransaction tx, StartupSetupData setupData)
    {
        string salt = PasswordHelper.CreateSalt();
        string hash = PasswordHelper.HashPassword(setupData.ResetPassword, salt);

        // برای اطمینان، قبل از ثبت رکورد جدید جدول پاک می‌شود
        SqliteCommandHelper.ExecuteNonQuery(conn, "DELETE FROM app_settings;", transaction: tx);

        const string sql = @"
INSERT INTO app_settings
(
    is_initialized,
    station_type,
    station_name,
    user_reset_password_hash,
    user_reset_password_salt,
    created_at,
    last_backup_at,
    password_changed_at,
    theme_index,
    esd_extra_runtime_enabled,
    esd_extra_runtime_hours,
    data_start_date
)
VALUES
(
    @is_initialized,
    @station_type,
    @station_name,
    @user_reset_password_hash,
    @user_reset_password_salt,
    @created_at,
    @last_backup_at,
    @password_changed_at,
    @theme_index,
    @esd_extra_runtime_enabled,
    @esd_extra_runtime_hours,
    @data_start_date
);";

        var parameters = new List<SqliteParameter>
        {
            SqliteCommandHelper.Param("@is_initialized", 1),
            SqliteCommandHelper.Param("@station_type", setupData.StationType.ToString()),
            SqliteCommandHelper.Param("@station_name", setupData.StationName),
            SqliteCommandHelper.Param("@user_reset_password_hash", hash),
            SqliteCommandHelper.Param("@user_reset_password_salt", salt),
            SqliteCommandHelper.Param("@created_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
            SqliteCommandHelper.Param("@last_backup_at", DBNull.Value),
            SqliteCommandHelper.Param("@password_changed_at", DBNull.Value),
            SqliteCommandHelper.Param("@theme_index", 0),
            SqliteCommandHelper.Param("@esd_extra_runtime_enabled",
                setupData.EsdExtraRuntimeEnabled ? 1 : 0),

            SqliteCommandHelper.Param("@esd_extra_runtime_hours",
                setupData.EsdExtraRuntimeHours),

            SqliteCommandHelper.Param("@data_start_date", setupData.DataStartDateRep)

        };

        SqliteCommandHelper.ExecuteNonQuery(conn, sql, parameters, tx);
    }

    /// <summary>
    /// ذخیره مقادیر پایه کارکرد واحدها
    /// </summary>
    private static void SaveUnitRuntimeBase(
        SqliteConnection conn,
        SqliteTransaction tx,
        List<UnitRuntimeBase> items)
    {
        SqliteCommandHelper.ExecuteNonQuery(
            conn,
            "DELETE FROM unit_runtime_base;",
            transaction: tx);

        const string sql = @"
INSERT INTO unit_runtime_base
(
    unit_no,
    base_runtime_hours,
    base_runtime_after_oh_hours,
    initial_is_running,
    initial_status
)
VALUES
(
    @unit_no,
    @base_runtime_hours,
    @base_runtime_after_oh_hours,
    @initial_is_running,
    @initial_status
);";

        foreach (UnitRuntimeBase item in items)
        {
            string initialStatus = item.InitialStatus.Trim().ToUpperInvariant();
            int initialIsRunning = initialStatus == "ON" ? 1 : 0;

            var parameters = new List<SqliteParameter>
        {
            SqliteCommandHelper.Param("@unit_no", item.UnitNo),
            SqliteCommandHelper.Param("@base_runtime_hours", item.BaseRuntimeHours),
            SqliteCommandHelper.Param("@base_runtime_after_oh_hours", item.BaseRuntimeAfterOHHours),
            SqliteCommandHelper.Param("@initial_is_running", initialIsRunning),
            SqliteCommandHelper.Param("@initial_status", initialStatus)
        };

            SqliteCommandHelper.ExecuteNonQuery(conn, sql, parameters, tx);
        }
    }
}