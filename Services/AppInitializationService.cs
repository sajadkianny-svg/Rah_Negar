using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rah_Negar.Data;
using Rah_Negar.Core;

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

            return CanonicalProfileService.TryLoad(conn) is not null;
        }
        catch
        {
            return false;
        }
    }
}
