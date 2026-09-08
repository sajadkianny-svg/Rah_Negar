using System.Text.Json;
using Rah_Negar.Core;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Infrastructure.ApplicationData;
using Rah_Negar.Infrastructure.Database;
using Rah_Negar.Infrastructure.Security;

namespace Rah_Negar.Qualification;

/// <summary>
/// Qualification-only adapter for native inspection of protected UI paths.
/// It has no production activation switch: the official harness marker, the
/// isolated data root, and the copied qualification application must all agree.
/// </summary>
public static class QualificationManagementAccess
{
    public const string IsolationMarkerFileName = "qualification-isolation.json";
    public const string ManagementSecretEnvironmentVariable = "RAH_NEGAR_QUALIFICATION_MANAGEMENT_SECRET";
    private const string OfficialHarnessId = "Rah_Negar.run-final-ui-acceptance.v1";

    public static bool IsEnabled(out string reason) => TryGetSession(out _, out reason);

    public static bool TryGetSyntheticCredentialForInspection(out string credential)
    {
        credential = string.Empty;
        if (!TryGetSession(out _, out _)) return false;

        string? value = Environment.GetEnvironmentVariable(ManagementSecretEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(value) || value.Length > 256) return false;
        credential = value;
        return true;
    }

    public static bool TryAuthorize(
        ProtectedAction action,
        string actionScope,
        string correlationId,
        ReadOnlyMemory<char> credential,
        out ManagementAuthorizationProof? proof,
        out int credentialVersion,
        out string reason)
    {
        proof = null;
        credentialVersion = 0;
        if (!TryGetSession(out QualificationSession? session, out reason)) return false;
        if (!AppSession.IsLoggedIn)
        {
            reason = "فعالیت عادی برنامه وارد نشده است.";
            return false;
        }
        if (!Enum.IsDefined(action) || string.IsNullOrWhiteSpace(actionScope) ||
            string.IsNullOrWhiteSpace(correlationId) || credential.IsEmpty)
        {
            reason = "درخواست مجوز مدیریتی معتبر نیست.";
            return false;
        }

        try
        {
            var factory = new SqliteConnectionFactory(new SqliteDatabaseOptions
            {
                DataSource = session!.DatabasePath,
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWrite,
                Cache = Microsoft.Data.Sqlite.SqliteCacheMode.Private,
                Pooling = false
            });
            var authorization = new TargetManagementAuthorizationService(
                new SQLiteManagementCredentialRepository(factory),
                new SQLiteSecurityAuditSink(factory),
                Rah_Negar.Infrastructure.Foundation.Time.SystemClock.Instance,
                TimeSpan.FromMinutes(5));
            DateTimeOffset now = DateTimeOffset.UtcNow;
            TargetShiftProfileSession shiftSession = new(
                session.ShiftProfileId, session.StationScope, 1, now.AddSeconds(-1), now.AddMinutes(5));
            TargetManagementAuthorizationResult result = authorization.AuthorizeAsync(
                shiftSession, session.StationScope, action, actionScope, correlationId, credential)
                .GetAwaiter().GetResult();
            if (!result.Succeeded || result.Proof is null)
            {
                reason = "مجوز مدیریتی پذیرفته نشد.";
                return false;
            }

            proof = result.Proof;
            credentialVersion = result.Proof.CredentialVersion;
            reason = string.Empty;
            return true;
        }
        catch
        {
            proof = null;
            credentialVersion = 0;
            reason = "مجوز مدیریتی در محیط qualification در دسترس نیست.";
            return false;
        }
    }

    internal static bool ValidateForPaths(
        string? qualificationRoot,
        string? appBaseDirectory,
        string markerPath,
        string? productionRoot,
        out string reason)
    {
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(qualificationRoot) || string.IsNullOrWhiteSpace(appBaseDirectory))
            return Fail("نشانه جداسازی یا مسیر qualification وجود ندارد.", out reason);

        try
        {
            string root = Path.GetFullPath(qualificationRoot);
            string appBase = Path.GetFullPath(appBaseDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string production = Path.GetFullPath(productionRoot ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                ApplicationDataPaths.ProductDirectoryName));
            if (string.Equals(root, production, StringComparison.OrdinalIgnoreCase))
                return Fail("مسیر qualification با ریشه Production یکسان است.", out reason);
            if (!File.Exists(markerPath))
                return Fail("نشانه جداسازی qualification یافت نشد.", out reason);

            QualificationIsolationMarker? marker = JsonSerializer.Deserialize<QualificationIsolationMarker>(
                File.ReadAllText(markerPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (marker is null || marker.MarkerVersion != 1 ||
                !string.Equals(marker.HarnessId, OfficialHarnessId, StringComparison.Ordinal) ||
                !marker.QualificationOnly || !marker.ManagementProofEnabled || marker.ProductionDataTouched ||
                !string.Equals(Path.GetFullPath(marker.DataRoot), root, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetFullPath(marker.AppDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                    appBase, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetFullPath(marker.DatabasePath), Path.Combine(root, "Data", "db.sys"), StringComparison.OrdinalIgnoreCase))
                return Fail("نشانه جداسازی qualification معتبر نیست.", out reason);

            string markerDirectory = Path.GetDirectoryName(Path.GetFullPath(markerPath))!;
            if (!IsWithin(root, markerDirectory) ||
                !AreSessionSiblings(markerDirectory, appBaseDirectory) ||
                !IsWithinQualificationHarness(appBaseDirectory))
                return Fail("برنامه خارج از مسیر رسمی qualification اجرا شده است.", out reason);

            string databasePath = Path.Combine(root, "Data", "db.sys");
            if (!File.Exists(databasePath))
                return Fail("دیتابیس qualification یافت نشد.", out reason);
            return true;
        }
        catch
        {
            return Fail("بررسی جداسازی qualification ناموفق بود.", out reason);
        }
    }

    public static bool TryGetSession(out QualificationSession? session, out string reason)
    {
        session = null;
        reason = string.Empty;
        string? root = Environment.GetEnvironmentVariable(ApplicationDataPaths.QualificationRootEnvironmentVariable);
        string markerPath = string.IsNullOrWhiteSpace(root)
            ? string.Empty
            : Path.Combine(Path.GetFullPath(root), IsolationMarkerFileName);
        if (!ValidateForPaths(root, AppContext.BaseDirectory, markerPath, null, out reason)) return false;

        try
        {
            QualificationIsolationMarker marker = JsonSerializer.Deserialize<QualificationIsolationMarker>(
                File.ReadAllText(markerPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            string? secret = Environment.GetEnvironmentVariable(ManagementSecretEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(secret) || secret.Length > 256)
            {
                reason = "مدرک synthetic qualification در این نشست فراهم نیست.";
                return false;
            }

            session = new QualificationSession(marker.SessionId, Path.GetFullPath(marker.DataRoot),
                Path.GetFullPath(marker.DatabasePath), marker.StationScope, marker.ShiftProfileId);
            return true;
        }
        catch
        {
            reason = "نشست qualification معتبر نیست.";
            return false;
        }
    }

    private static bool IsWithin(string parent, string child)
    {
        string parentPath = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string childPath = Path.GetFullPath(child).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return childPath.StartsWith(parentPath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWithinQualificationHarness(string appBaseDirectory)
    {
        string normalized = Path.GetFullPath(appBaseDirectory);
        string[] parts = normalized.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < parts.Length - 4; index++)
            if (parts[index].Equals("Qualification", StringComparison.OrdinalIgnoreCase) &&
                parts[index + 1].Equals("qualification-run", StringComparison.OrdinalIgnoreCase) &&
                parts[index + 2].Equals("final-ui-acceptance", StringComparison.OrdinalIgnoreCase) &&
                parts[index + 4].Equals("app", StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static bool AreSessionSiblings(string dataRoot, string appBaseDirectory)
    {
        string dataParent = Path.GetDirectoryName(Path.GetFullPath(dataRoot).TrimEnd(Path.DirectorySeparatorChar)) ?? string.Empty;
        string appParent = Path.GetDirectoryName(Path.GetFullPath(appBaseDirectory).TrimEnd(Path.DirectorySeparatorChar)) ?? string.Empty;
        return string.Equals(dataParent, appParent, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Fail(string value, out string reason)
    {
        reason = value;
        return false;
    }

    public sealed record QualificationSession(
        string SessionId,
        string DataRoot,
        string DatabasePath,
        string StationScope,
        string ShiftProfileId);

    private sealed record QualificationIsolationMarker(
        int MarkerVersion,
        string HarnessId,
        string SessionId,
        bool QualificationOnly,
        bool ManagementProofEnabled,
        bool ProductionDataTouched,
        string DataRoot,
        string DatabasePath,
        string AppDirectory,
        string StationScope,
        string ShiftProfileId);
}
