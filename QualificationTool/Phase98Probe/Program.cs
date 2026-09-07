using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Rah_Negar.Core;
using Rah_Negar.Foundation.Application.Authority;
using Rah_Negar.Foundation.Application.Database.Readiness;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Foundation.Time;
using Rah_Negar.Models;
using Rah_Negar.Qualification;
using Rah_Negar.Services;
using Rah_Negar.Infrastructure.Database;
using Rah_Negar.Infrastructure.Database.Checksums;
using Rah_Negar.Infrastructure.Database.Migrations;
using Rah_Negar.Infrastructure.Database.Migrations.Drafts;
using Rah_Negar.Infrastructure.Database.Readiness;
using Rah_Negar.Infrastructure.Foundation.Time;

try
{
    if (args.Length < 2)
        throw new ArgumentException("Usage: Phase98Probe <initialize | capture | restore | audit | fence | startup> <path> <evidence-directory> [output-path]");

    string mode = args[0].Trim().ToLowerInvariant();
    string firstPath = Path.GetFullPath(args[1]);
    string evidence = Path.GetFullPath(args.Length > 2 ? args[2] : Path.Combine(Path.GetTempPath(), "RahNegar-Phase98"));
    Directory.CreateDirectory(evidence);

    if (mode == "initialize")
        await InitializeAsync(firstPath, evidence, args.Length > 3 ? Path.GetFullPath(args[3]) : throw new ArgumentException("Initialize requires the published App directory."));
    else if (mode == "capture")
        await CaptureAsync(firstPath, evidence);
    else if (mode == "restore")
        await VerifyRestoreAsync(firstPath, evidence, args.Length > 3 ? Path.GetFullPath(args[3]) : null,
            args.Length > 4 ? Path.GetFullPath(args[4]) : null);
    else if (mode == "audit")
        await VerifyAuditAsync(firstPath, evidence, args.Length > 3 ? Path.GetFullPath(args[3]) : Path.Combine(evidence, "audit-tampered.jsonl"));
    else if (mode == "fence")
        await VerifyFenceAsync(firstPath, evidence);
    else if (mode == "startup")
        await VerifyStartupAsync(firstPath, evidence);
    else
        throw new ArgumentException("Unknown mode. Use initialize, capture, restore, audit, fence, or startup.");

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

static async Task InitializeAsync(string databasePath, string evidence, string appDirectory)
{
    if (File.Exists(databasePath)) throw new IOException("Qualification database already exists; refusing to overwrite it.");
    Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
    Directory.CreateDirectory(appDirectory);

    string fixturePath = Path.Combine(AppContext.BaseDirectory, "Data", "db.sys");
    DeleteGenerated(fixturePath);
    StartupSetupService.InitializeApplication(new StartupSetupData
    {
        StationType = StationType.Rasht,
        StationName = "Production-Like Qualification Station",
        ResetPassword = QualificationEnvironment.LoginPassword,
        DataStartDateRep = QualificationEnvironment.DataStartDate,
        EsdExtraRuntimeEnabled = true,
        EsdExtraRuntimeHours = 1.5,
        UnitRuntimeBases = Enumerable.Range(1, 3).Select(unit => new UnitRuntimeBase
        {
            UnitNo = unit,
            BaseRuntimeHours = 100 + unit,
            BaseRuntimeAfterOHHours = 20 + unit,
            InitialIsRunning = false,
            InitialStatus = "OFF"
        }).ToList()
    });
    await CheckpointAsync(fixturePath);
    File.Copy(fixturePath, databasePath, false);
    foreach (string suffix in new[] { "-wal", "-shm" })
        if (File.Exists(fixturePath + suffix)) File.Copy(fixturePath + suffix, databasePath + suffix, false);

    var checksums = new Sha256ChecksumService();
    var factory = new SqliteConnectionFactory(new SqliteDatabaseOptions
    {
        DataSource = databasePath,
        Mode = SqliteOpenMode.ReadWrite,
        Pooling = false
    });
    var runner = new MigrationRunner(new SqliteTransactionManager(factory), new MigrationChecksumValidator(checksums));
    MigrationRunResult migration = await runner.RunPendingAsync(UnifiedTargetMigrationChain.Create(checksums));
    await SeedGenericTargetIdentityAsync(databasePath);

    string dataFiles = Path.Combine(appDirectory, "DataFiles");
    Directory.CreateDirectory(dataFiles);
    string deploymentScope = "phase9.8-production-like-qualification";
    string stationScope = "qualification-station";
    string authorityPath = Path.Combine(dataFiles, "authority-state.json");
    string transitionPath = Path.Combine(dataFiles, "authority-transition.json");
    string auditPath = Path.Combine(dataFiles, "authority-audit.jsonl");
    AuthorityStateRecord legacy = AuthorityStateRecord.Legacy(deploymentScope, stationScope);
    await new FileAuthorityStateStore(authorityPath).SaveAsync(legacy);
    AuthorityTransitionRecord transition = AuthorityTransitionRecord.PreparedFrom(legacy, "phase9.8-qualification-aborted-transition", 1)
        with { Lifecycle = TransitionLifecycle.Aborted, RecordedAtUtc = DateTimeOffset.UtcNow };
    await new FileTransitionStateStore(transitionPath).SaveAsync(transition);
    using (var audit = new TamperEvidentAuthorityAuditSink(auditPath))
    {
        await audit.WriteAsync(new("phase9.8-qualification", DateTimeOffset.UtcNow, deploymentScope, stationScope,
            "qualification", AuthorityState.LegacyAuthoritative, AuthorityState.ActivationPreparedNotExecuted,
            AuthorityAuditAction.Prepare, "PREPARED", "qualification-only-transition", "phase9.8-authority-readback", 1));
        await audit.WriteAsync(new("phase9.8-qualification", DateTimeOffset.UtcNow, deploymentScope, stationScope,
            "qualification", AuthorityState.LegacyAuthoritative, AuthorityState.ActivationPreparedNotExecuted,
            AuthorityAuditAction.Abort, "ABORTED", "qualification-only-transition-aborted", "phase9.8-authority-readback", 1));
    }

    string profilePath = Path.Combine(dataFiles, "deployment-profile.json");
    await WriteJsonAsync(profilePath, new
    {
        classification = "PRODUCTION-LIKE QUALIFICATION DEPLOYMENT",
        production = false,
        authoritative = false,
        identity = "phase9.8-qualification-generic-r3",
        applicationCompatibilityProfile = "Rasht-compatible 3-unit fixture for the current supported application schema",
        qualificationOnly = true,
        targetRoutingEnabled = false,
        productionActivationAuthorized = false,
        productionCutoverAuthorized = false
    });

    AuthorityStartupResult startup = await new AuthorityStartupResolver(new FileAuthorityStateStore(authorityPath))
        .ResolveCanonicalAsync(new FileTransitionStateStore(transitionPath));
    string malformedDirectory = Path.Combine(evidence, "startup-malformed");
    Directory.CreateDirectory(malformedDirectory);
    string malformedAuthorityPath = Path.Combine(malformedDirectory, "authority-state.json");
    await File.WriteAllTextAsync(malformedAuthorityPath, "{not-json");
    AuthorityStartupResult malformed = await new AuthorityStartupResolver(new FileAuthorityStateStore(malformedAuthorityPath))
        .ResolveCanonicalAsync(new FileTransitionStateStore(Path.Combine(malformedDirectory, "authority-transition.json")));

    await WriteJsonAsync(Path.Combine(evidence, "deployment-initialization.json"), new
    {
        generatedUtc = DateTimeOffset.UtcNow,
        classification = "PRODUCTION-LIKE QUALIFICATION DEPLOYMENT - NOT PRODUCTION",
        productionStateUnchanged = true,
        database = await DescribeDatabaseAsync(databasePath),
        migration = new { migration.InitialVersion, migration.FinalVersion, migration.AppliedMigrationIds },
        metadata = new
        {
            authorityState = DescribeFile(authorityPath),
            authorityTransition = DescribeFile(transitionPath),
            authorityAudit = DescribeFile(auditPath),
            deploymentProfile = DescribeFile(profilePath)
        },
        authorityReadback = startup,
        authorityBoundary = new
        {
            legacy = startup.EffectiveAuthority.LegacyAuthoritative ? "AUTHORITATIVE" : "NOT AUTHORITATIVE",
            target = startup.EffectiveAuthority.TargetAuthoritative ? "AUTHORITATIVE" : "NON-AUTHORITATIVE",
            targetRouting = startup.EffectiveAuthority.TargetRoutingEnabled ? "ENABLED" : "DISABLED",
            productionActivation = "UNAUTHORIZED",
            productionCutover = "UNAUTHORIZED",
            legacyRoutingAllowed = AuthorityRoutingGuard.IsLegacyOperationalRoutingAllowed(startup.EffectiveAuthority),
            targetRoutingAllowed = AuthorityRoutingGuard.IsTargetOperationalRoutingAllowed(startup.EffectiveAuthority)
        },
        malformedStartupFailClosed = new
        {
            classification = malformed.Classification,
            routingBlocked = malformed.RoutingBlocked,
            issues = malformed.Issues,
            passed = malformed.RoutingBlocked && malformed.Classification == "InvalidOrCorrupt"
        },
        activationAuthorizationArtifacts = Directory.GetFiles(dataFiles, "*authorization*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFullPath).ToArray()
    });
}

static async Task VerifyRestoreAsync(string source, string evidence, string? backupDirectory, string? restoreDirectory)
{
    if (!File.Exists(source)) throw new FileNotFoundException("Restore source is absent.", source);
    string sourceHashBefore = await Sha256Async(source);
    FileInfo sourceInfoBefore = new(source);
    string backupWork = backupDirectory ?? Path.Combine(evidence, "restore-work");
    string restoreWork = restoreDirectory ?? backupWork;
    Directory.CreateDirectory(backupWork);
    Directory.CreateDirectory(restoreWork);
    string backup = Path.Combine(backupWork, "verified-backup.sqlite");
    string destination = Path.Combine(restoreWork, "disposable-restore-target.sqlite");
    string rollback = Path.Combine(restoreWork, "disposable-restore-rollback.sqlite");
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
        ["sourceClassification"] = "PRODUCTION-LIKE QUALIFICATION DEPLOYMENT SQLite file; not Production",
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

static async Task VerifyAuditAsync(string auditPath, string evidence, string tamperedPath)
{
    Directory.CreateDirectory(Path.GetDirectoryName(auditPath)!);
    Directory.CreateDirectory(Path.GetDirectoryName(tamperedPath)!);
    long linesBefore = File.Exists(auditPath) ? File.ReadLines(auditPath).LongCount(x => !string.IsNullOrWhiteSpace(x)) : 0;
    using (var sink = new TamperEvidentAuthorityAuditSink(auditPath))
    {
        AuditIntegrityResult beforeAppend = await sink.VerifyAsync();
        await sink.WriteAsync(new("phase9.8-audit", DateTimeOffset.UtcNow, "phase9.8-production-like-qualification", "qualification-station", "qualification",
            AuthorityState.LegacyAuthoritative,
            AuthorityState.ActivationPreparedNotExecuted,
            AuthorityAuditAction.ValidationFailed,
            "BLOCKED", "phase9.8-technical-audit-check", "phase9.8-installation-audit", 1));
        AuditIntegrityResult appended = await sink.VerifyAsync();
        File.Copy(auditPath, tamperedPath, true);
        string tampered = await File.ReadAllTextAsync(tamperedPath);
        await File.WriteAllTextAsync(tamperedPath, tampered.Replace("BLOCKED", "TAMPERED", StringComparison.Ordinal));
        AuditIntegrityResult tamperDetection;
        using (var tamperedSink = new TamperEvidentAuthorityAuditSink(tamperedPath))
            tamperDetection = await tamperedSink.VerifyAsync();
        AuditIntegrityResult restartReadback;
        using (var restartedSink = new TamperEvidentAuthorityAuditSink(auditPath))
            restartReadback = await restartedSink.VerifyAsync();
        await WriteJsonAsync(Path.Combine(evidence, "phase9.8-audit-verification.json"), new
        {
            generatedUtc = DateTimeOffset.UtcNow,
            auditPath,
            appendBehavior = new { linesBeforeAppend = linesBefore, linesAfterAppend = appended.LastSequence, sequenceContinues = appended.LastSequence == linesBefore + 1, initial = beforeAppend },
            integrityValidation = appended,
            tamperDetection,
            tamperDetectionPassed = !tamperDetection.IsValid,
            restartPersistence = restartReadback,
            retention = "KeepAll by implementation; no application prune path found in Phase97ProductionExecution.cs",
            actualConfiguredAuditPath = auditPath,
            qualificationOnly = true,
            organizationalCustody = "NOT CLAIMED"
        });
    }
}

static async Task VerifyFenceAsync(string lockPath, string evidence)
{
    Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
    var gate = new ProductionWriteGate();
    WriterFenceAcquireResult acquired;
    LegacyWriteLeaseResult writer;
    WriteDrainLeaseResult drained;
    LegacyWriteLeaseResult blockedWriter;
    TargetWriteLeaseResult blockedTarget;
    var fence = new LocalSingleWriterFence(lockPath);
    {
        acquired = await fence.AcquireAsync(new("phase9.8-production-like-qualification", 1, "phase9.8-fence-drain"));
        if (!acquired.Acquired) throw new InvalidOperationException(acquired.Reason);
        writer = await gate.EnterLegacyWriteAsync(0);
        if (!writer.Acquired) throw new InvalidOperationException(writer.Reason);
        Task<WriteDrainLeaseResult> drainTask = gate.AcquireDrainAsync();
        blockedWriter = await gate.EnterLegacyWriteAsync(0);
        if (blockedWriter.Acquired) throw new InvalidOperationException("A legacy writer survived the drain barrier.");
        await writer.Lease!.DisposeAsync();
        drained = await drainTask;
        if (!drained.Acquired) throw new InvalidOperationException(drained.Reason);
        blockedTarget = await gate.EnterTargetWriteAsync(1);
        if (blockedTarget.Acquired) throw new InvalidOperationException("Target write admitted while routing is disabled.");
        await drained.Lease!.DisposeAsync();
        await acquired.Lease!.DisposeAsync();
    }
    WriterFenceAcquireResult restart;
    var restartedFence = new LocalSingleWriterFence(lockPath);
    {
        restart = await restartedFence.AcquireAsync(new("phase9.8-production-like-qualification", 2, "phase9.8-fence-restart"));
        if (!restart.Acquired) throw new InvalidOperationException(restart.Reason);
        await restart.Lease!.DisposeAsync();
    }
    await WriteJsonAsync(Path.Combine(evidence, "fence-drain-receipt.json"), new
    {
        generatedUtc = DateTimeOffset.UtcNow,
        fencePath = Path.GetFullPath(lockPath),
        writerInventory = new { isolatedWriterCountBeforeDrain = 1, isolatedWriterCountAfterDrain = 0, writerLeaseReleased = true },
        fenceAcquire = new { acquired.Status, acquired.Reason, recoveredFromAbandonment = acquired.Lease?.WasRecoveredFromAbandonment ?? false },
        drain = new { drained.Status, drained.Reason, noWriterSurvivesBarrier = !blockedWriter.Acquired },
        routingGuard = new { blockedTarget.Status, blockedTarget.Reason, targetRoutingEnabled = false },
        restart = new { restart.Status, restart.Reason, restartClassification = "RESTART_READY_NO_ORPHANED_WRITER" },
        result = "PASS",
        qualificationOnly = true
    });
}

static async Task VerifyStartupAsync(string authorityDirectory, string evidence)
{
    string authorityPath = Path.Combine(authorityDirectory, "authority-state.json");
    string transitionPath = Path.Combine(authorityDirectory, "authority-transition.json");
    AuthorityStartupResult startup = await new AuthorityStartupResolver(new FileAuthorityStateStore(authorityPath))
        .ResolveCanonicalAsync(new FileTransitionStateStore(transitionPath));
    await WriteJsonAsync(Path.Combine(evidence, "authority-startup-readback.json"), new
    {
        generatedUtc = DateTimeOffset.UtcNow,
        paths = new { authorityPath, transitionPath, auditPath = Path.Combine(authorityDirectory, "authority-audit.jsonl") },
        startup,
        legacyAuthoritative = startup.EffectiveAuthority.LegacyAuthoritative,
        targetNonAuthoritative = !startup.EffectiveAuthority.TargetAuthoritative,
        targetRoutingDisabled = !startup.EffectiveAuthority.TargetRoutingEnabled,
        productionActivationUnauthorized = true,
        productionCutoverUnauthorized = true,
        targetOperationalRoutingAllowed = AuthorityRoutingGuard.IsTargetOperationalRoutingAllowed(startup.EffectiveAuthority),
        result = startup.EffectiveAuthority.State == AuthorityState.LegacyAuthoritative &&
            startup.EffectiveAuthority.LegacyAuthoritative && !startup.EffectiveAuthority.TargetAuthoritative &&
            !startup.EffectiveAuthority.TargetRoutingEnabled && !AuthorityRoutingGuard.IsTargetOperationalRoutingAllowed(startup.EffectiveAuthority)
            ? "PASS" : "FAIL"
    });
}

static async Task SeedGenericTargetIdentityAsync(string path)
{
    await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
    {
        DataSource = path, Mode = SqliteOpenMode.ReadWrite, Pooling = false
    }.ToString());
    await connection.OpenAsync();
    await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync();
    await using (SqliteCommand command = connection.CreateCommand())
    {
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Stations(StationId,StationName,CreatedAtUtc,Revision)
            VALUES ('qualification-station','Production-Like Qualification Station','2026-09-07T00:00:00Z',1);
            """;
        await command.ExecuteNonQueryAsync();
    }
    for (int unit = 1; unit <= 3; unit++)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Units(StationId,UnitId,UnitNumber,UnitName,IsActive,Revision)
            VALUES ($station,$id,$number,$name,1,1);
            """;
        command.Parameters.AddWithValue("$station", "qualification-station");
        command.Parameters.AddWithValue("$id", $"qualification-unit-{unit}");
        command.Parameters.AddWithValue("$number", unit);
        command.Parameters.AddWithValue("$name", $"Qualification Unit {unit}");
        await command.ExecuteNonQueryAsync();
    }
    await transaction.CommitAsync();
    await using SqliteCommand pragma = connection.CreateCommand();
    pragma.CommandText = $"PRAGMA user_version = {UnifiedTargetMigrationChain.FinalVersion};";
    await pragma.ExecuteNonQueryAsync();
}

static async Task CheckpointAsync(string path)
{
    await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
    {
        DataSource = path, Mode = SqliteOpenMode.ReadWrite, Pooling = false
    }.ToString());
    await connection.OpenAsync();
    await using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
    await command.ExecuteNonQueryAsync();
}

static void DeleteGenerated(string path)
{
    foreach (string candidate in new[] { path, path + "-wal", path + "-shm" })
        if (File.Exists(candidate)) File.Delete(candidate);
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
