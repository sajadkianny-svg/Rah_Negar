using System.Text.Json;

namespace Rah_Negar.Infrastructure.Database.Readiness;

/// <summary>
/// Durable marker used when a restore outcome cannot be proven. The marker is
/// cleared only by a later verified successful restore; normal startup never
/// ignores it or falls through to an operational form.
/// </summary>
public static class RecoveryRequiredStateStore
{
    private sealed record RecoveryMarker(string DatabasePath, string Reason, DateTimeOffset CreatedAtUtc);

    public static bool IsRequired(string databasePath)
    {
        string path = MarkerPath(databasePath);
        return File.Exists(path);
    }

    public static void MarkRequired(string databasePath, string reason)
    {
        string marker = MarkerPath(databasePath);
        string directory = Path.GetDirectoryName(marker)
            ?? throw new InvalidOperationException("Recovery marker path is invalid.");
        Directory.CreateDirectory(directory);
        string temporary = marker + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temporary, JsonSerializer.Serialize(
            new RecoveryMarker(databasePath, reason, DateTimeOffset.UtcNow)));
        File.Move(temporary, marker, overwrite: true);
    }

    public static void ClearAfterVerifiedRecovery(string databasePath) =>
        DeleteMarker(databasePath);

    private static string MarkerPath(string databasePath) =>
        string.Equals(Path.GetFullPath(databasePath), Rah_Negar.Infrastructure.ApplicationData.ApplicationDataPaths.Default.DatabasePath,
            StringComparison.OrdinalIgnoreCase)
            ? Rah_Negar.Infrastructure.ApplicationData.ApplicationDataPaths.Default.RecoveryRequiredPath
            : Path.GetFullPath(databasePath) + ".recovery-required";

    private static void DeleteMarker(string databasePath)
    {
        string path = MarkerPath(databasePath);
        if (File.Exists(path)) File.Delete(path);
    }
}
