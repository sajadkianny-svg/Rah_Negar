using Microsoft.Data.Sqlite;
using Rah_Negar.Data;
using Rah_Negar.Services;

namespace Rah_Negar.Core;

/// <summary>
/// Loads the persisted profile definition. UI and runtime code should depend on
/// this service instead of interpreting station names.
/// </summary>
public static class CanonicalProfileService
{
    public static CanonicalProfileDefinition? TryLoad()
    {
        AppSettingsModel? settings = AppSettingsService.GetSettings();
        return settings?.ProfileDefinition;
    }

    public static CanonicalProfileDefinition Require()
    {
        return TryLoad() ?? throw new InvalidOperationException(
            "پروفایل عملیاتی معتبر ثبت نشده است. ابتدا راه‌انداز اولیه را تکمیل کنید.");
    }

    public static CanonicalProfileDefinition? TryLoad(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT profile_id, profile_revision, station_name, unit_count, profile_optional_parameters FROM app_settings WHERE is_initialized = 1 ORDER BY id LIMIT 1;";
        try
        {
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
                return null;

            return CanonicalProfileDefinition.TryCreateFromLegacySettings(
                reader["profile_id"]?.ToString(),
                Convert.ToInt32(reader["profile_revision"]),
                reader["station_name"]?.ToString(),
                Convert.ToInt32(reader["unit_count"]),
                reader["profile_optional_parameters"]?.ToString(),
                out CanonicalProfileDefinition? definition)
                ? definition
                : null;
        }
        catch (SqliteException)
        {
            return null;
        }
    }
}
