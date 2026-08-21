using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rah_Negar.Data;

namespace Rah_Negar.Services;

/// <summary>
/// بررسی اینکه برنامه قبلاً راه‌اندازی شده یا نه
/// </summary>
public static class AppInitializationService
{
    public static bool IsInitialized()
    {
        try
        {
            using var conn = SqliteDatabaseHelper.CreateConnection();

            const string sql = "SELECT COUNT(*) FROM app_settings WHERE is_initialized = 1;";
            object? result = SqliteCommandHelper.ExecuteScalar(conn, sql);

            int count = Convert.ToInt32(result ?? 0);
            return count > 0;
        }
        catch
        {
            return false;
        }
    }
}
