using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Rah_Negar.Foundation.Application.Database.Readiness;
using Rah_Negar.Foundation.Time;

namespace Rah_Negar.Infrastructure.Database.Readiness;

public sealed class ExplicitDatabaseTargetInspector : IExplicitDatabaseTargetInspector
{
    private static readonly byte[] SqliteHeader = Encoding.ASCII.GetBytes("SQLite format 3\0");
    private readonly IClock _clock;

    public ExplicitDatabaseTargetInspector(IClock clock) =>
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public async Task<DatabaseTargetInspectionResult> InspectAsync(
        string explicitDatabasePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(explicitDatabasePath))
            return Fail(DatabaseTargetFailure.PathRequired, "ExplicitPathRequired");
        string fullPath;
        try { fullPath = Path.GetFullPath(explicitDatabasePath); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        { return Fail(DatabaseTargetFailure.FileNotFound, "InvalidExplicitPath"); }
        if (!File.Exists(fullPath)) return Fail(DatabaseTargetFailure.FileNotFound, "DatabaseFileNotFound");
        try
        {
            if ((File.GetAttributes(fullPath) & FileAttributes.Directory) != 0)
                return Fail(DatabaseTargetFailure.NotAFile, "DatabaseTargetIsNotAFile");
            var info = new FileInfo(fullPath);
            byte[] header = new byte[SqliteHeader.Length];
            await using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                if (await stream.ReadAsync(header, cancellationToken).ConfigureAwait(false) != header.Length ||
                    !header.AsSpan().SequenceEqual(SqliteHeader))
                    return Fail(DatabaseTargetFailure.InvalidSqliteHeader, "InvalidSqliteHeader");
            }

            string identity = HashCanonical($"{Convert.ToHexString(header)}|{info.Length}|{info.LastWriteTimeUtc.Ticks}");
            var target = new DatabaseTargetDescriptor(fullPath, info.Length,
                new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                _clock.UtcNow.ToUniversalTime(), identity);
            return new(true, target, DatabaseTargetFailure.None, "ValidExplicitSqliteTarget");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return Fail(DatabaseTargetFailure.OpenFailed, "DatabaseTargetInspectionFailed"); }
    }

    internal static string HashCanonical(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static DatabaseTargetInspectionResult Fail(DatabaseTargetFailure failure, string category) =>
        new(false, null, failure, category);
}

public sealed class ReadOnlyDatabasePreflightAnalyzer : IReadOnlyDatabasePreflightAnalyzer
{
    private static readonly HashSet<string> TargetTableNames = new(StringComparer.Ordinal)
    {
        "Stations", "Units", "SecurityShiftProfiles", "SecurityShiftProfileCredentials",
        "SecurityManagementCredentials", "SecurityDeviceIdentity", "SecurityTrustedVendorPublicKeys",
        "SecurityDeploymentSettings", "SecurityConsumedVendorAuthorizations",
        "SecurityProtectedExecutionReceipts", "SecurityAuditEntries", "SecurityAuditMetadata",
        "Events", "EventAudit", "ReportSnapshots", "ReportPeriodLocks", "ReportFinalizationReceipts"
    };

    private readonly IExplicitDatabaseTargetInspector _targets;

    public ReadOnlyDatabasePreflightAnalyzer(IExplicitDatabaseTargetInspector targets) =>
        _targets = targets ?? throw new ArgumentNullException(nameof(targets));

    public async Task<DatabasePreflightResult> AnalyzeAsync(string explicitDatabasePath,
        IntegrityCheckStrategy integrityStrategy, CancellationToken cancellationToken = default)
    {
        DatabaseTargetInspectionResult target = await _targets.InspectAsync(explicitDatabasePath, cancellationToken)
            .ConfigureAwait(false);
        if (!target.IsValid) return Failed(target, "TargetInspectionFailed");

        try
        {
            await using SqliteConnection connection = await OpenReadOnlyAsync(target.Target!.ExplicitPath, cancellationToken)
                .ConfigureAwait(false);
            await ExecuteAsync(connection, "PRAGMA query_only=ON;", cancellationToken).ConfigureAwait(false);
            string integritySql = integrityStrategy == IntegrityCheckStrategy.FullIntegrityCheck
                ? "PRAGMA integrity_check;"
                : "PRAGMA quick_check;";
            IReadOnlyList<string> integrity = await ReadStringsAsync(connection, integritySql, cancellationToken)
                .ConfigureAwait(false);
            bool integrityPassed = integrity.Count == 1 &&
                StringComparer.OrdinalIgnoreCase.Equals(integrity[0], "ok");
            IReadOnlyList<string> foreignKeys = await ReadForeignKeysAsync(connection, cancellationToken)
                .ConfigureAwait(false);
            int schemaVersion = Convert.ToInt32(await ScalarAsync(connection, "PRAGMA schema_version;", cancellationToken),
                CultureInfo.InvariantCulture);
            int userVersion = Convert.ToInt32(await ScalarAsync(connection, "PRAGMA user_version;", cancellationToken),
                CultureInfo.InvariantCulture);
            string journal = Convert.ToString(await ScalarAsync(connection, "PRAGMA journal_mode;", cancellationToken),
                CultureInfo.InvariantCulture) ?? "unknown";
            IReadOnlyList<SqliteSchemaObject> objects = await ReadSchemaObjectsAsync(connection, cancellationToken)
                .ConfigureAwait(false);
            string[] tables = objects.Where(x => x.Type == "table").Select(x => x.Name)
                .Where(x => !x.StartsWith("sqlite_", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray();
            IReadOnlyDictionary<string, long> rowCounts = await ReadRowCountsAsync(connection, tables, cancellationToken)
                .ConfigureAwait(false);
            MigrationLedgerInspection ledger = await ReadLedgerAsync(connection, tables, cancellationToken)
                .ConfigureAwait(false);
            EsdValueInspection legacyEsd = await ReadLegacyEsdAsync(connection, tables, cancellationToken)
                .ConfigureAwait(false);
            EsdValueInspection targetEsd = await ReadTargetEsdAsync(connection, tables, cancellationToken)
                .ConfigureAwait(false);
            FinalizedEvidenceInspection finalized = await ReadFinalizedEvidenceAsync(connection, tables, cancellationToken)
                .ConfigureAwait(false);
            string[] targetTables = tables.Where(TargetTableNames.Contains).ToArray();
            string[] legacyTables = tables.Where(x => !TargetTableNames.Contains(x) &&
                !x.StartsWith("__rahnegar_", StringComparison.Ordinal)).ToArray();
            bool fileReadOnly = (File.GetAttributes(target.Target.ExplicitPath) & FileAttributes.ReadOnly) != 0;
            return new(true, target, true, integrityPassed, integrity, foreignKeys, schemaVersion,
                userVersion, ledger, objects, rowCounts, legacyTables, targetTables, legacyEsd, targetEsd,
                finalized, journal, true, fileReadOnly, Array.Empty<string>());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            return Failed(target, "ReadOnlyPreflightFailed");
        }
    }

    internal static async Task<SqliteConnection> OpenReadOnlyAsync(string path, CancellationToken token)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            DefaultTimeout = 5
        };
        var connection = new SqliteConnection(builder.ToString());
        try
        {
            await connection.OpenAsync(token).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    internal static string QuoteIdentifier(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private static async Task<IReadOnlyList<SqliteSchemaObject>> ReadSchemaObjectsAsync(
        SqliteConnection connection, CancellationToken token)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT type,name,tbl_name,COALESCE(sql,'') FROM sqlite_schema " +
                              "WHERE name NOT LIKE 'sqlite_%' ORDER BY type,name;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        var result = new List<SqliteSchemaObject>();
        while (await reader.ReadAsync(token).ConfigureAwait(false))
            result.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                ExplicitDatabaseTargetInspector.HashCanonical(reader.GetString(3))));
        return result.AsReadOnly();
    }

    private static async Task<IReadOnlyDictionary<string, long>> ReadRowCountsAsync(
        SqliteConnection connection, IEnumerable<string> tables, CancellationToken token)
    {
        var counts = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (string table in tables)
        {
            object? value = await ScalarAsync(connection,
                $"SELECT COUNT(*) FROM {QuoteIdentifier(table)};", token).ConfigureAwait(false);
            counts.Add(table, Convert.ToInt64(value, CultureInfo.InvariantCulture));
        }
        return new ReadOnlyDictionary<string, long>(counts);
    }

    private static async Task<MigrationLedgerInspection> ReadLedgerAsync(
        SqliteConnection connection, IReadOnlyCollection<string> tables, CancellationToken token)
    {
        bool versionExists = tables.Contains("__rahnegar_schema_version", StringComparer.Ordinal);
        bool historyExists = tables.Contains("__rahnegar_migration_history", StringComparer.Ordinal);
        if (!versionExists && !historyExists)
            return new(false, false, true, null, Array.Empty<InspectedMigrationEntry>(), null);
        try
        {
            bool schemaMatches = versionExists && historyExists &&
                await ColumnsMatchAsync(connection, "__rahnegar_schema_version", ["singleton_id", "current_version"], token) &&
                await ColumnsMatchAsync(connection, "__rahnegar_migration_history",
                    ["migration_id", "from_version", "to_version", "checksum", "applied_at_utc"], token);
            if (!schemaMatches)
                return new(versionExists, historyExists, false, null,
                    Array.Empty<InspectedMigrationEntry>(), "LedgerSchemaMismatch");
            await using SqliteCommand version = connection.CreateCommand();
            version.CommandText = "SELECT current_version FROM __rahnegar_schema_version WHERE singleton_id=1;";
            object? currentRaw = await version.ExecuteScalarAsync(token).ConfigureAwait(false);
            int? current = currentRaw is null or DBNull ? null : Convert.ToInt32(currentRaw, CultureInfo.InvariantCulture);
            await using SqliteCommand history = connection.CreateCommand();
            history.CommandText = "SELECT migration_id,from_version,to_version,checksum,applied_at_utc " +
                                  "FROM __rahnegar_migration_history ORDER BY from_version,to_version,migration_id;";
            await using SqliteDataReader reader = await history.ExecuteReaderAsync(token).ConfigureAwait(false);
            var entries = new List<InspectedMigrationEntry>();
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                DateTimeOffset? applied = DateTimeOffset.TryParse(reader.GetString(4), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out DateTimeOffset value) ? value : null;
                entries.Add(new(reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetString(3), applied));
            }
            return new(true, true, true, current, entries.AsReadOnly(), null);
        }
        catch
        {
            return new(versionExists, historyExists, false, null,
                Array.Empty<InspectedMigrationEntry>(), "CorruptMigrationLedger");
        }
    }

    private static async Task<bool> ColumnsMatchAsync(SqliteConnection connection, string table,
        IReadOnlyCollection<string> expected, CancellationToken token)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({QuoteIdentifier(table)});";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        var columns = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(token).ConfigureAwait(false)) columns.Add(reader.GetString(1));
        return columns.SetEquals(expected);
    }

    private static async Task<EsdValueInspection> ReadLegacyEsdAsync(SqliteConnection connection,
        IReadOnlyCollection<string> tables, CancellationToken token)
    {
        if (!tables.Contains("app_settings", StringComparer.Ordinal))
            return new(InspectedEsdValueState.Absent, null, 0);
        if (!await ColumnExistsAsync(connection, "app_settings", "esd_extra_runtime_hours", token))
            return new(InspectedEsdValueState.RequiredColumnMissing, null, 0);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT CAST(esd_extra_runtime_hours AS TEXT) FROM app_settings ORDER BY id;";
        return await ReadEsdRowsAsync(command, token).ConfigureAwait(false);
    }

    private static async Task<EsdValueInspection> ReadTargetEsdAsync(SqliteConnection connection,
        IReadOnlyCollection<string> tables, CancellationToken token)
    {
        if (!tables.Contains("SecurityDeploymentSettings", StringComparer.Ordinal))
            return new(InspectedEsdValueState.Absent, null, 0);
        if (!await ColumnExistsAsync(connection, "SecurityDeploymentSettings", "EsdAdjustmentCanonical", token))
            return new(InspectedEsdValueState.RequiredColumnMissing, null, 0);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT EsdAdjustmentCanonical FROM SecurityDeploymentSettings ORDER BY SingletonId;";
        return await ReadEsdRowsAsync(command, token).ConfigureAwait(false);
    }

    private static async Task<EsdValueInspection> ReadEsdRowsAsync(SqliteCommand command, CancellationToken token)
    {
        var values = new List<string?>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false))
            values.Add(reader.IsDBNull(0) ? null : reader.GetString(0));
        if (values.Count == 0) return new(InspectedEsdValueState.Absent, null, 0);
        if (values.Count != 1) return new(InspectedEsdValueState.MultipleRows, null, values.Count);
        if (!decimal.TryParse(values[0], NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out decimal parsed) || parsed < 0)
            return new(InspectedEsdValueState.Invalid, null, 1);
        return new(InspectedEsdValueState.Valid, parsed.ToString("G29", CultureInfo.InvariantCulture), 1);
    }

    private static async Task<FinalizedEvidenceInspection> ReadFinalizedEvidenceAsync(
        SqliteConnection connection, IReadOnlyCollection<string> tables, CancellationToken token)
    {
        var snapshots = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var locks = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (tables.Contains("ReportSnapshots", StringComparer.Ordinal))
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT SnapshotId,CanonicalJson FROM ReportSnapshots ORDER BY SnapshotId;";
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
                snapshots.Add(reader.GetString(0), ExplicitDatabaseTargetInspector.HashCanonical(reader.GetString(1)));
        }
        if (tables.Contains("ReportPeriodLocks", StringComparer.Ordinal))
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT StationId,PeriodStartMinute,PeriodEndMinute,PeriodKind,LockState," +
                                  "COALESCE(EffectiveSnapshotId,''),Revision FROM ReportPeriodLocks " +
                                  "WHERE LockState='Finalized' ORDER BY StationId,PeriodStartMinute,PeriodEndMinute,PeriodKind;";
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                string key = $"{reader.GetString(0)}|{reader.GetInt64(1)}|{reader.GetInt64(2)}|{reader.GetString(3)}";
                string value = $"{reader.GetString(4)}|{reader.GetString(5)}|{reader.GetInt64(6)}";
                locks.Add(key, ExplicitDatabaseTargetInspector.HashCanonical(value));
            }
        }
        return new(snapshots.Count, new ReadOnlyDictionary<string, string>(snapshots), locks.Count,
            new ReadOnlyDictionary<string, string>(locks));
    }

    private static async Task<bool> ColumnExistsAsync(SqliteConnection connection, string table,
        string column, CancellationToken token)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({QuoteIdentifier(table)});";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false))
            if (StringComparer.Ordinal.Equals(reader.GetString(1), column)) return true;
        return false;
    }

    private static async Task<IReadOnlyList<string>> ReadForeignKeysAsync(
        SqliteConnection connection, CancellationToken token)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        var values = new List<string>();
        while (await reader.ReadAsync(token).ConfigureAwait(false))
            values.Add($"{reader.GetString(0)}|{(reader.IsDBNull(1) ? -1 : reader.GetInt64(1))}|{reader.GetString(2)}|{reader.GetInt32(3)}");
        return values.AsReadOnly();
    }

    private static async Task<IReadOnlyList<string>> ReadStringsAsync(
        SqliteConnection connection, string sql, CancellationToken token)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        var values = new List<string>();
        while (await reader.ReadAsync(token).ConfigureAwait(false)) values.Add(reader.GetString(0));
        return values.AsReadOnly();
    }

    private static async Task<object?> ScalarAsync(SqliteConnection connection, string sql, CancellationToken token)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(token).ConfigureAwait(false);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken token)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }

    private static DatabasePreflightResult Failed(DatabaseTargetInspectionResult target, string error) =>
        new(false, target, target.Failure == DatabaseTargetFailure.None, false, Array.Empty<string>(),
            Array.Empty<string>(), 0, 0,
            new(false, false, false, null, Array.Empty<InspectedMigrationEntry>(), error),
            Array.Empty<SqliteSchemaObject>(), new ReadOnlyDictionary<string, long>(new Dictionary<string, long>()),
            Array.Empty<string>(), Array.Empty<string>(), new(InspectedEsdValueState.Absent, null, 0),
            new(InspectedEsdValueState.Absent, null, 0),
            new(0, new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()), 0,
                new ReadOnlyDictionary<string, string>(new Dictionary<string, string>())),
            "unknown", false, false, [error]);
}

public sealed class DatabaseStructuralFingerprintService : IDatabaseStructuralFingerprintService
{
    private static readonly string[] SensitiveTokens =
        ["password", "salt", "credential", "verifier", "privatekey", "secret", "recovery"];
    private readonly IReadOnlyDatabasePreflightAnalyzer _preflight;

    public DatabaseStructuralFingerprintService(IReadOnlyDatabasePreflightAnalyzer preflight) =>
        _preflight = preflight ?? throw new ArgumentNullException(nameof(preflight));

    public async Task<DatabaseStructuralFingerprint> CaptureAsync(
        string explicitDatabasePath, CancellationToken cancellationToken = default)
    {
        DatabasePreflightResult preflight = await _preflight.AnalyzeAsync(explicitDatabasePath,
            IntegrityCheckStrategy.QuickCheck, cancellationToken).ConfigureAwait(false);
        if (!preflight.Succeeded || preflight.TargetInspection.Target is null)
            throw new InvalidOperationException("Database fingerprint preflight failed.");
        await using SqliteConnection connection = await ReadOnlyDatabasePreflightAnalyzer.OpenReadOnlyAsync(
            preflight.TargetInspection.Target.ExplicitPath, cancellationToken).ConfigureAwait(false);
        var representative = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var snapshots = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach ((string key, string value) in preflight.FinalizedEvidence.SnapshotHashes)
            snapshots.Add(key, value);
        var locks = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach ((string key, string value) in preflight.FinalizedEvidence.LockHashes)
            locks.Add(key, value);
        foreach (string table in preflight.RowCounts.Keys)
        {
            if (IsSensitive(table)) continue;
            if (table.Contains("runtime", StringComparison.OrdinalIgnoreCase) ||
                table.Contains("event", StringComparison.OrdinalIgnoreCase))
                representative[table] = await HashTableAsync(connection, table, cancellationToken).ConfigureAwait(false);
            if (table.Contains("monthly_report", StringComparison.OrdinalIgnoreCase))
                snapshots[$"legacy:{table}"] = await HashTableAsync(connection, table, cancellationToken).ConfigureAwait(false);
            if (StringComparer.Ordinal.Equals(table, "tbl_monthly_lock"))
                locks[$"legacy:{table}"] = await HashTableAsync(connection, table, cancellationToken).ConfigureAwait(false);
        }
        string ledgerHash = HashLedger(preflight.MigrationLedger);
        string canonical = string.Join('\n', preflight.SchemaObjects.Select(x =>
                $"S|{x.Type}|{x.Name}|{x.TableName}|{x.DefinitionSha256}")
            .Concat(preflight.RowCounts.Select(x => $"R|{x.Key}|{x.Value}"))
            .Concat(representative.Select(x => $"D|{x.Key}|{x.Value}"))
            .Concat(snapshots.Select(x => $"F|{x.Key}|{x.Value}"))
            .Concat(locks.Select(x => $"L|{x.Key}|{x.Value}"))
            .Append($"M|{ledgerHash}")
            .Append($"E|{preflight.LegacyEsd.CanonicalValue}|{preflight.TargetEsd.CanonicalValue}"));
        return new(ExplicitDatabaseTargetInspector.HashCanonical(canonical), preflight.SchemaObjects,
            preflight.RowCounts, new ReadOnlyDictionary<string, string>(representative),
            new ReadOnlyDictionary<string, string>(snapshots), new ReadOnlyDictionary<string, string>(locks),
            ledgerHash, preflight.LegacyEsd.CanonicalValue, preflight.TargetEsd.CanonicalValue);
    }

    private static async Task<string> HashTableAsync(SqliteConnection connection, string table, CancellationToken token)
    {
        await using SqliteCommand columnsCommand = connection.CreateCommand();
        columnsCommand.CommandText = $"PRAGMA table_info({ReadOnlyDatabasePreflightAnalyzer.QuoteIdentifier(table)});";
        await using SqliteDataReader columnsReader = await columnsCommand.ExecuteReaderAsync(token).ConfigureAwait(false);
        var columns = new List<string>();
        while (await columnsReader.ReadAsync(token).ConfigureAwait(false))
        {
            string column = columnsReader.GetString(1);
            if (!IsSensitive(column)) columns.Add(column);
        }
        if (columns.Count == 0) return ExplicitDatabaseTargetInspector.HashCanonical("no-approved-columns");
        string projection = string.Join(',', columns.Select(ReadOnlyDatabasePreflightAnalyzer.QuoteIdentifier));
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT {projection} FROM {ReadOnlyDatabasePreflightAnalyzer.QuoteIdentifier(table)} " +
                              $"ORDER BY {projection};";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] separator = [0];
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            for (int index = 0; index < reader.FieldCount; index++)
            {
                string value = reader.IsDBNull(index) ? "<null>" : reader.GetValue(index) switch
                {
                    byte[] bytes => Convert.ToHexString(bytes),
                    IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
                    object item => item.ToString() ?? string.Empty
                };
                hash.AppendData(Encoding.UTF8.GetBytes(value));
                hash.AppendData(separator);
            }
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string HashLedger(MigrationLedgerInspection ledger)
    {
        string canonical = $"{ledger.VersionTableExists}|{ledger.HistoryTableExists}|{ledger.SchemaMatches}|{ledger.CurrentVersion}|" +
            string.Join(';', ledger.Entries.Select(x => $"{x.MigrationId}|{x.FromVersion}|{x.ToVersion}|{x.Checksum}"));
        return ExplicitDatabaseTargetInspector.HashCanonical(canonical);
    }

    private static bool IsSensitive(string value) => SensitiveTokens.Any(token =>
        value.Replace("_", string.Empty, StringComparison.Ordinal).Contains(token, StringComparison.OrdinalIgnoreCase));
}
