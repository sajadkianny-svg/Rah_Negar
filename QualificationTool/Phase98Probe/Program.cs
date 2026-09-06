using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Rah_Negar.Foundation.Application.Authority;
using Rah_Negar.Foundation.Application.Database.Readiness;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Foundation.Time;
using Rah_Negar.Infrastructure.Database;
using Rah_Negar.Infrastructure.Database.Checksums;
using Rah_Negar.Infrastructure.Database.Migrations;
using Rah_Negar.Infrastructure.Database.Migrations.Drafts;
using Rah_Negar.Infrastructure.Database.Readiness;
using Rah_Negar.Infrastructure.Foundation.Time;

try
{
    if (args.Length < 2)
        throw new ArgumentException("Usage: Phase98Probe <capture | restore | audit> <path> <evidence-directory>");

    string mode = args[0].Trim().ToLowerInvariant();
    string firstPath = Path.GetFullPath(args[1]);
    string evidence = Path.GetFullPath(args.Length > 2 ? args[2] : Path.Combine(Path.GetTempPath(), "RahNegar-Phase98"));
    Directory.CreateDirectory(evidence);

    if (mode == "capture")
        await CaptureAsync(firstPath, evidence);
    else if (mode == "restore")
        await VerifyRestoreAsync(firstPath, evidence);
    else if (mode == "audit")
        await VerifyAuditAsync(evidence);
    else
        throw new ArgumentException("Unknown mode. Use capture, restore, or audit.");

    Environment.ExitCode = 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Phase98Probe failed: {ex.GetType().Name}: {ex.Message}");
    Environment.ExitCode = 1;
}

static async Task CaptureAsync(string repo, string evidence)
{
    string releaseBase = Path.Combine(repo, "bin", "Release", "net8.0-windows");
    string[] candidates =
    [
        Path.Combine(repo, "Data", "db.sys"),
        Path.Combine(releaseBase, "Data", "db.sys"),
        Path.Combine(repo, "bin", "Debug", "net8.0-windows", "Data", "db.sys")
    ];
    string[] metadata =
    [
        Path.Combine(repo, "DataFiles", "authority-state.json"),
        Path.Combine(repo, "DataFiles", "authority-transition.json"),
        Path.Combine(repo, "DataFiles", "authority-audit.jsonl"),
        Path.Combine(repo, "DataFiles", "activation-audit.jsonl"),
        Path.Combine(releaseBase, "DataFiles", "authority-state.json"),
        Path.Combine(releaseBase, "DataFiles", "authority-transition.json"),
        Path.Combine(releaseBase, "DataFiles", "authority-audit.jsonl"),
        Path.Combine(releaseBase, "DataFiles", "activation-audit.jsonl")
    ];
    var result = new
    {
        capturedUtc = DateTimeOffset.UtcNow,
        repository = repo,
        runtimeDatabaseRule = "Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Data, db.sys)",
        applicationBuild = DescribeAssembly(Path.Combine(releaseBase, "Rah_Negar.dll")),
        candidates = await Task.WhenAll(candidates.Select(DescribeDatabaseAsync)),
        metadata = metadata.Select(DescribeFile).ToArray(),
        sourceVersion = new { targetMigrationFinalVersion = UnifiedTargetMigrationChain.FinalVersion, targetMigrationChain = GetMigrationIds() },
        supportedStations = new[] { "Rasht", "Ramsar" },
        supportedUnitCount = "3-5 inclusive; station profiles are Rasht=3 and Ramsar=4"
    };
    await WriteJsonAsync(Path.Combine(evidence, "phase9.8-installation-discovery.json"), result);
}

static async Task VerifyRestoreAsync(string source, string evidence)
{
    if (!File.Exists(source)) throw new FileNotFoundException("Restore source is absent.", source);
    string sourceHashBefore = await Sha256Async(source);
    FileInfo sourceInfoBefore = new(source);
    string work = Path.Combine(evidence, "restore-work");
    Directory.CreateDirectory(work);
    string backup = Path.Combine(work, "verified-backup.sqlite");
    string destination = Path.Combine(work, "disposable-restore-target.sqlite");
    string rollback = Path.Combine(work, "disposable-restore-rollback.sqlite");
    File.Copy(source, destination, false);
    foreach (string suffix in new[] { "-wal", "-shm" })
        if (File.Exists(source + suffix)) File.Copy(source + suffix, destination + suffix, false);

    ManagedSqliteBackupRestoreBoundary services = CreateServices();
    string backupScope = SqliteProtectedActionBinding.CreateBackupScope(source, backup, BackupOverwritePolicy.Deny);
    DateTimeOffset now = DateTimeOffset.UtcNow;
    var proof = new ManagementAuthorizationProof("phase9.8-qualification", ProtectedAction.BackupPolicy,
        backupScope, 7, now.AddMinutes(-1), now.AddMinutes(10), "phase9.8-restore-verification");
    ManagedSqliteBackupResult backupResult = await services.CreateVerifiedBackupAsync(
        source, backup, BackupOverwritePolicy.Deny, proof, 7);
    var record = new Dictionary<string, object?>
    {
        ["generatedUtc"] = DateTimeOffset.UtcNow,
        ["source"] = DescribeFile(source),
        ["sourceClassification"] = "Release build-output SQLite file; not proven as Production",
        ["backupArtifact"] = DescribeFile(backup),
        ["backupReceipt"] = backupResult.Receipt,
        ["backupVerification"] = backupResult.Verification,
        ["backupResult"] = backupResult.Succeeded ? "PASS" : "FAIL",
        ["sourceHashBefore"] = sourceHashBefore
    };
    if (backupResult.Succeeded)
    {
        string restoreScope = SqliteProtectedActionBinding.CreateRestoreScope(backup, backupResult.Receipt.BackupSha256!, destination, rollback);
        var restoreProof = proof with
        {
            Action = ProtectedAction.Restore,
            ActionScope = restoreScope
        };
        ManagedSqliteRestoreResult restoreResult = await services.RestoreAsync(
            backup, backupResult.Receipt.BackupSha256!, destination, rollback, restoreProof, 7);
        record["restoreReceipt"] = restoreResult.Receipt;
        record["restoreResult"] = restoreResult.Succeeded ? "PASS" : "FAIL";
        record["restoredTarget"] = DescribeFile(destination);
        record["rollbackCopy"] = DescribeFile(rollback);
        record["restoredPreflight"] = await DescribeDatabaseAsync(destination);
        record["dataCompatibility"] = await CompareDataShapeAsync(source, destination);
    }
    string sourceHashAfter = await Sha256Async(source);
    FileInfo sourceInfoAfter = new(source);
    record["sourceHashAfter"] = sourceHashAfter;
    record["productionIsolation"] = new
    {
        sourceUnchanged = sourceHashBefore == sourceHashAfter && sourceInfoBefore.Length == sourceInfoAfter.Length && sourceInfoBefore.LastWriteTimeUtc == sourceInfoAfter.LastWriteTimeUtc,
        sourcePath = source,
        writableRestoreInputs = new[] { backup, destination, rollback }.Select(Path.GetFullPath).ToArray()
    };
    record["overallResult"] = backupResult.Succeeded && sourceHashBefore == sourceHashAfter && sourceInfoBefore.Length == sourceInfoAfter.Length
        ? (record.TryGetValue("restoreResult", out object? v) && Equals(v, "PASS") ? "PASS" : "FAIL") : "FAIL";
    await WriteJsonAsync(Path.Combine(evidence, "phase9.8-restore-verification.json"), record);
}

static async Task VerifyAuditAsync(string evidence)
{
    string work = Path.Combine(evidence, "audit-work");
    Directory.CreateDirectory(work);
    string auditPath = Path.Combine(work, "authority-audit.jsonl");
    string tamperedPath = Path.Combine(work, "authority-audit-tampered.jsonl");
    using (var sink = new TamperEvidentAuthorityAuditSink(auditPath))
    {
        await sink.WriteAsync(new("phase9.8-audit", DateTimeOffset.UtcNow, "qualification", "all", "1.0.0.0",
            AuthorityState.LegacyAuthoritative,
            AuthorityState.ActivationPreparedNotExecuted,
            AuthorityAuditAction.Prepare,
            "PREPARED", "phase9.8-technical-audit-check", "phase9.8-installation-audit"));
        await sink.WriteAsync(new("phase9.8-audit", DateTimeOffset.UtcNow, "qualification", "all", "1.0.0.0",
            AuthorityState.LegacyAuthoritative,
            AuthorityState.ActivationPreparedNotExecuted,
            AuthorityAuditAction.ValidationFailed,
            "BLOCKED", "phase9.8-technical-audit-check", "phase9.8-installation-audit"));
        AuditIntegrityResult initial = await sink.VerifyAsync();
        await sink.WriteAsync(new("phase9.8-audit", DateTimeOffset.UtcNow, "qualification", "all", "1.0.0.0",
            AuthorityState.LegacyAuthoritative,
            AuthorityState.ActivationPreparedNotExecuted,
            AuthorityAuditAction.Abort,
            "ABORTED", "phase9.8-technical-audit-check", "phase9.8-installation-audit"));
        AuditIntegrityResult appended = await sink.VerifyAsync();
        File.Copy(auditPath, tamperedPath, true);
        string tampered = await File.ReadAllTextAsync(tamperedPath);
        await File.WriteAllTextAsync(tamperedPath, tampered.Replace("ABORTED", "TAMPERED", StringComparison.Ordinal));
        AuditIntegrityResult tamperDetection;
        using (var tamperedSink = new TamperEvidentAuthorityAuditSink(tamperedPath))
            tamperDetection = await tamperedSink.VerifyAsync();
        await WriteJsonAsync(Path.Combine(evidence, "phase9.8-audit-verification.json"), new
        {
            generatedUtc = DateTimeOffset.UtcNow,
            auditPath,
            appendBehavior = new { linesBeforeAppend = 2, linesAfterAppend = 3, sequenceContinues = appended.LastSequence == 3, initial = initial },
            integrityValidation = appended,
            tamperDetection,
            tamperDetectionPassed = !tamperDetection.IsValid,
            retention = "KeepAll by implementation; no application prune path found in Phase97ProductionExecution.cs",
            qualificationOnly = true,
            productionAuditPath = "not configured/present in repository or Release build output"
        });
    }
}

static ManagedSqliteBackupRestoreBoundary CreateServices()
{
    var clock = SystemClock.Instance;
    var checksums = new Sha256ChecksumService();
    IReadOnlyList<IDatabaseMigration> migrations = UnifiedTargetMigrationChain.Create(checksums);
    var classifier = new MigrationHistoryClassifier(migrations.Select(x => new SupportedMigrationDefinition(
        x.Metadata.MigrationId, x.Metadata.FromVersion, x.Metadata.ToVersion, x.Metadata.Checksum)),
        UnifiedTargetMigrationChain.FinalVersion);
    var inspector = new ExplicitDatabaseTargetInspector(clock);
    var preflight = new ReadOnlyDatabasePreflightAnalyzer(inspector);
    var fingerprints = new DatabaseStructuralFingerprintService(preflight);
    var backup = new ExplicitSqliteBackupService(preflight, fingerprints, classifier, clock);
    var restore = new RestoreValidationService(preflight, classifier);
    return new ManagedSqliteBackupRestoreBoundary(backup, restore, preflight, clock);
}

static async Task<object> DescribeDatabaseAsync(string path)
{
    var file = DescribeFile(path);
    if (!File.Exists(path)) return new { file, readable = false, status = "ABSENT" };
    try
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        await connection.OpenAsync();
        string journal = await ScalarStringAsync(connection, "PRAGMA journal_mode;");
        string schema = await ScalarStringAsync(connection, "PRAGMA schema_version;");
        string user = await ScalarStringAsync(connection, "PRAGMA user_version;");
        string integrity = await ScalarStringAsync(connection, "PRAGMA integrity_check;");
        List<string> fk = await QueryFirstColumnAsync(connection, "PRAGMA foreign_key_check;");
        List<string> tables = await QueryFirstColumnAsync(connection, "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;");
        long? units = tables.Contains("Units", StringComparer.Ordinal) ? await ScalarLongAsync(connection, "SELECT COUNT(*) FROM Units;") : null;
        string? station = tables.Contains("app_settings", StringComparer.Ordinal) ? await ScalarNullableStringAsync(connection, "SELECT station_type FROM app_settings ORDER BY id LIMIT 1;") : null;
        long? dataUnits = tables.Contains("unit_runtime_base", StringComparer.Ordinal) ? await ScalarLongAsync(connection, "SELECT COUNT(*) FROM unit_runtime_base;") : null;
        return new { file, readable = true, status = "PRESENT", journalMode = journal, schemaVersion = schema, userVersion = user, integrityCheck = integrity, foreignKeyViolations = fk, tables, configuredStation = station, units, unitRuntimeRows = dataUnits };
    }
    catch (Exception ex)
    {
        return new { file, readable = false, status = "READ_FAILED", error = ex.GetType().Name + ": " + ex.Message };
    }
}

static async Task<object> CompareDataShapeAsync(string source, string restored)
{
    object a = await DescribeDatabaseAsync(source);
    object b = await DescribeDatabaseAsync(restored);
    return new { source = a, restored = b, comparison = "Both source and restored descriptors retained; restore target is disposable." };
}

static object DescribeFile(string path)
{
    string full = Path.GetFullPath(path);
    if (!File.Exists(full)) return new { path = full, exists = false };
    FileInfo info = new(full);
    return new { path = full, exists = true, sizeBytes = info.Length, sha256 = Sha256Sync(full), lastWriteUtc = info.LastWriteTimeUtc.ToString("o", CultureInfo.InvariantCulture), attributes = info.Attributes.ToString() };
}

static object DescribeAssembly(string path)
{
    if (!File.Exists(path)) return new { path = Path.GetFullPath(path), exists = false };
    AssemblyName name = AssemblyName.GetAssemblyName(path);
    FileInfo info = new(path);
    return new { path = Path.GetFullPath(path), exists = true, assemblyVersion = name.Version?.ToString(), fileSizeBytes = info.Length, sha256 = Sha256Sync(path), lastWriteUtc = info.LastWriteTimeUtc.ToString("o", CultureInfo.InvariantCulture), targetFramework = "net8.0-windows" };
}

static string[] GetMigrationIds()
{
    var chain = UnifiedTargetMigrationChain.Create(new Sha256ChecksumService());
    return chain.Select(x => x.Metadata.MigrationId).ToArray();
}

static async Task<string> ScalarStringAsync(SqliteConnection c, string sql) => Convert.ToString(await ScalarAsync(c, sql), CultureInfo.InvariantCulture) ?? string.Empty;
static async Task<string?> ScalarNullableStringAsync(SqliteConnection c, string sql) => (await ScalarAsync(c, sql)) as string;
static async Task<long?> ScalarLongAsync(SqliteConnection c, string sql) => Convert.ToInt64(await ScalarAsync(c, sql), CultureInfo.InvariantCulture);
static async Task<object?> ScalarAsync(SqliteConnection c, string sql)
{
    await using var command = c.CreateCommand(); command.CommandText = sql; return await command.ExecuteScalarAsync();
}
static async Task<List<string>> QueryFirstColumnAsync(SqliteConnection c, string sql)
{
    await using var command = c.CreateCommand(); command.CommandText = sql;
    await using var reader = await command.ExecuteReaderAsync(); var result = new List<string>();
    while (await reader.ReadAsync()) result.Add(Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty);
    return result;
}
static string Sha256Sync(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream));
}
static async Task<string> Sha256Async(string path)
{
    await using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(await SHA256.HashDataAsync(stream));
}
static async Task WriteJsonAsync(string path, object value)
{
    await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
}
