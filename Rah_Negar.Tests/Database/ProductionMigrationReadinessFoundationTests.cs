using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Rah_Negar.Foundation.Application.Database.Readiness;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Foundation.Time;
using Rah_Negar.Infrastructure.Database;
using Rah_Negar.Infrastructure.Database.Checksums;
using Rah_Negar.Infrastructure.Database.Migrations;
using Rah_Negar.Infrastructure.Database.Migrations.Drafts;
using Rah_Negar.Infrastructure.Database.Readiness;

namespace Rah_Negar.Tests.Database;

public sealed class ProductionMigrationReadinessFoundationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Explicit_target_requires_a_caller_path_and_rejects_non_sqlite_content()
    {
        await using var fixture = ReadinessFixture.Create();
        await File.WriteAllTextAsync(fixture.SourcePath, "not sqlite");
        ExplicitDatabaseTargetInspector inspector = CreateServices().Inspector;

        DatabaseTargetInspectionResult missing = await inspector.InspectAsync(" ");
        DatabaseTargetInspectionResult invalid = await inspector.InspectAsync(fixture.SourcePath);

        Assert.Equal(DatabaseTargetFailure.PathRequired, missing.Failure);
        Assert.Equal(DatabaseTargetFailure.InvalidSqliteHeader, invalid.Failure);
        Assert.DoesNotContain(typeof(ReadOnlyDatabasePreflightAnalyzer).GetMethods(),
            method => method.Name.Contains("Discover", StringComparison.OrdinalIgnoreCase) ||
                      method.Name.Contains("Scan", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Preflight_is_read_only_and_classifies_a_clean_legacy_baseline()
    {
        await using var fixture = ReadinessFixture.Create();
        await fixture.CreateLegacyAsync();
        Services services = CreateServices();
        byte[] before = await File.ReadAllBytesAsync(fixture.SourcePath);

        DatabasePreflightResult preflight = await services.Preflight.AnalyzeAsync(
            fixture.SourcePath, IntegrityCheckStrategy.FullIntegrityCheck);
        byte[] after = await File.ReadAllBytesAsync(fixture.SourcePath);

        Assert.True(preflight.Succeeded);
        Assert.True(preflight.ReadOnlyConnectionEnforced);
        Assert.True(preflight.IntegrityPassed);
        Assert.Empty(preflight.ForeignKeyViolations);
        Assert.False(preflight.MigrationLedger.VersionTableExists);
        Assert.Contains("app_settings", preflight.LegacyTables);
        Assert.Equal(before, after);
        Assert.Equal(MigrationHistoryClassification.CleanLegacyBaseline,
            services.Classifier.Classify(preflight).Classification);
    }

    [Fact]
    public async Task Complete_unified_chain_is_clean_target_and_rehearsal_chain_is_idempotent()
    {
        await using var fixture = ReadinessFixture.Create();
        await fixture.CreateLegacyAsync();
        await RunUnifiedChainAsync(fixture.Factory);
        Services services = CreateServices();

        DatabasePreflightResult preflight = await services.Preflight.AnalyzeAsync(
            fixture.SourcePath, IntegrityCheckStrategy.QuickCheck);

        Assert.Equal(MigrationHistoryClassification.CleanUnifiedTarget,
            services.Classifier.Classify(preflight).Classification);
        MigrationRunResult rerun = await RunUnifiedChainAsync(fixture.Factory);
        Assert.Empty(rerun.AppliedMigrationIds);
        Assert.Equal(UnifiedTargetMigrationChain.FinalVersion, rerun.FinalVersion);
    }

    [Fact]
    public async Task Ledger_shape_checksum_unknown_and_newer_histories_are_classified_fail_closed()
    {
        Services services = CreateServices();
        await using var mismatch = ReadinessFixture.Create();
        await mismatch.ExecuteAsync("CREATE TABLE __rahnegar_schema_version(singleton_id INTEGER PRIMARY KEY,current_version INTEGER); INSERT INTO __rahnegar_schema_version VALUES(1,0);");
        Assert.Equal(MigrationHistoryClassification.LedgerSchemaMismatch,
            services.Classifier.Classify(await AnalyzeAsync(services, mismatch)).Classification);

        await using var checksum = ReadinessFixture.Create();
        await checksum.CreateLegacyAsync();
        await RunUnifiedChainAsync(checksum.Factory);
        await checksum.ExecuteAsync("UPDATE __rahnegar_migration_history SET checksum='BAD' WHERE from_version=0;");
        Assert.Equal(MigrationHistoryClassification.ChecksumMismatch,
            services.Classifier.Classify(await AnalyzeAsync(services, checksum)).Classification);

        await using var unknown = ReadinessFixture.Create();
        await unknown.CreateLedgerAsync(1, "unknown-migration", 0, 1, new string('A', 64));
        Assert.Equal(MigrationHistoryClassification.UnknownMigrationHistory,
            services.Classifier.Classify(await AnalyzeAsync(services, unknown)).Classification);

        await using var newer = ReadinessFixture.Create();
        await newer.CreateEmptyLedgerAsync(99);
        Assert.Equal(MigrationHistoryClassification.UnsupportedNewerVersion,
            services.Classifier.Classify(await AnalyzeAsync(services, newer)).Classification);
    }

    [Fact]
    public async Task Known_historical_draft_is_detected_and_requires_schema_validation_review()
    {
        await using var fixture = ReadinessFixture.Create();
        await fixture.CreateLedgerAsync(1, SecurityPersistenceSchemaMigration.MigrationId,
            0, 1, new string('B', 64));
        await fixture.ExecuteAsync("CREATE TABLE SecurityShiftProfiles(ShiftProfileId TEXT PRIMARY KEY);");
        Services services = CreateServices();
        DatabasePreflightResult preflight = await AnalyzeAsync(services, fixture);
        MigrationHistoryClassificationResult classification = services.Classifier.Classify(preflight);
        HistoricalDraftAdoptionPlan plan = new HistoricalDraftAdoptionPlanner().Plan(preflight, classification);

        Assert.Equal(MigrationHistoryClassification.HistoricalDraftRecognized, classification.Classification);
        Assert.True(plan.ManualReviewRequired);
        Assert.Contains(HistoricalDraftAdoptionAction.ValidateExistingSecuritySchema, plan.Actions);
        Assert.False(plan.AutomaticAdoptionRejected);
    }

    [Fact]
    public async Task Backup_is_sqlite_safe_verified_and_does_not_change_source()
    {
        await using var fixture = ReadinessFixture.Create();
        await fixture.CreateLegacyAsync();
        Services services = CreateServices();
        byte[] sourceBefore = await File.ReadAllBytesAsync(fixture.SourcePath);

        DatabaseBackupVerificationResult result = await services.Backup.CreateVerifiedBackupAsync(
            fixture.SourcePath, fixture.BackupPath, BackupOverwritePolicy.Deny);

        Assert.True(result.IsVerified);
        Assert.True(result.IntegrityPassed);
        Assert.Equal(DatabaseBackupFailure.None, result.Failure);
        Assert.True(File.Exists(fixture.BackupPath));
        Assert.Equal(await ComputeSha256Async(fixture.BackupPath), result.BackupSha256);
        Assert.Equal(sourceBefore, await File.ReadAllBytesAsync(fixture.SourcePath));
        RestoreValidationResult restore = await services.Restore.ValidateAsync(
            fixture.BackupPath, result.BackupSha256!);
        Assert.True(restore.IsValid);
    }

    [Fact]
    public async Task Backup_rejects_same_path_and_unapproved_overwrite()
    {
        await using var fixture = ReadinessFixture.Create();
        await fixture.CreateLegacyAsync();
        Services services = CreateServices();

        DatabaseBackupVerificationResult same = await services.Backup.CreateVerifiedBackupAsync(
            fixture.SourcePath, fixture.SourcePath, BackupOverwritePolicy.Deny);
        DatabaseBackupVerificationResult first = await services.Backup.CreateVerifiedBackupAsync(
            fixture.SourcePath, fixture.BackupPath, BackupOverwritePolicy.Deny);
        DatabaseBackupVerificationResult overwrite = await services.Backup.CreateVerifiedBackupAsync(
            fixture.SourcePath, fixture.BackupPath, BackupOverwritePolicy.Deny);

        Assert.Equal(DatabaseBackupFailure.SameSourceAndDestination, same.Failure);
        Assert.True(first.IsVerified);
        Assert.Equal(DatabaseBackupFailure.DestinationExists, overwrite.Failure);
    }

    [Fact]
    public async Task Backup_captures_committed_WAL_content()
    {
        await using var fixture = ReadinessFixture.Create();
        await using SqliteConnection held = await fixture.Factory.OpenConnectionAsync();
        await ExecuteAsync(held, "PRAGMA wal_autocheckpoint=0; CREATE TABLE WalEvidence(Id INTEGER PRIMARY KEY, Value TEXT); INSERT INTO WalEvidence VALUES(1,'committed-in-wal');");
        Services services = CreateServices();

        DatabaseBackupVerificationResult result = await services.Backup.CreateVerifiedBackupAsync(
            fixture.SourcePath, fixture.BackupPath, BackupOverwritePolicy.Deny);

        Assert.True(result.IsVerified);
        await using var backupConnection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = fixture.BackupPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        await backupConnection.OpenAsync();
        Assert.Equal("committed-in-wal", await ScalarAsync(backupConnection, "SELECT Value FROM WalEvidence WHERE Id=1;"));
    }

    [Fact]
    public async Task Restore_validation_rejects_checksum_mismatch_and_corrupt_backup()
    {
        await using var fixture = ReadinessFixture.Create();
        await fixture.CreateLegacyAsync();
        Services services = CreateServices();
        DatabaseBackupVerificationResult backup = await services.Backup.CreateVerifiedBackupAsync(
            fixture.SourcePath, fixture.BackupPath, BackupOverwritePolicy.Deny);
        Assert.True(backup.IsVerified);

        await using (var stream = new FileStream(fixture.BackupPath, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            stream.Position = 0;
            stream.WriteByte(0);
        }
        RestoreValidationResult mismatch = await services.Restore.ValidateAsync(
            fixture.BackupPath, backup.BackupSha256!);
        RestoreValidationResult corrupt = await services.Restore.ValidateAsync(
            fixture.BackupPath, await ComputeSha256Async(fixture.BackupPath));

        Assert.Equal(RestoreValidationFailure.ChecksumMismatch, mismatch.Failure);
        Assert.Equal(RestoreValidationFailure.InvalidSqlite, corrupt.Failure);
    }

    [Fact]
    public async Task Rehearsal_migrates_only_isolated_copy_and_preserves_legacy_evidence()
    {
        await using var fixture = ReadinessFixture.Create();
        await fixture.CreateLegacyAsync(includePreservationEvidence: true);
        Services services = CreateServices();
        string sourceHash = await ComputeSha256Async(fixture.SourcePath);
        DatabaseBackupVerificationResult backup = await services.Backup.CreateVerifiedBackupAsync(
            fixture.SourcePath, fixture.BackupPath, BackupOverwritePolicy.Deny);

        MigrationRehearsalResult result = await services.Rehearsal.RehearseAsync(backup);

        Assert.True(result.Passed);
        Assert.True(result.IdempotentRerun);
        Assert.True(result.OriginalBackupUnchanged);
        Assert.Equal(UnifiedTargetMigrationChain.FinalVersion, result.FinalVersion);
        Assert.NotNull(result.Preservation);
        Assert.True(result.Preservation!.LegacySchemaPreserved);
        Assert.True(result.Preservation.RepresentativeDataPreserved);
        Assert.True(result.Preservation.FinalizedSnapshotsPreserved);
        Assert.True(result.Preservation.ReportLocksPreserved);
        Assert.True(result.Preservation.LegacyEsdPreserved);
        Assert.True(result.Preservation.NoRbacIntroduced);
        Assert.True(result.Preservation.NoSupportIdentityIntroduced);
        Assert.Equal(EsdAuthorityMode.LegacyAuthoritative, result.EsdAuthorityMode);
        Assert.Equal(sourceHash, await ComputeSha256Async(fixture.SourcePath));
        Assert.False(await fixture.TableExistsAsync("SecurityDeploymentSettings"));
    }

    [Fact]
    public async Task Rehearsal_preserves_target_snapshot_bytes_lock_state_and_ESD_authority()
    {
        await using var fixture = ReadinessFixture.Create();
        await fixture.CreateLegacyAsync();
        await RunUnifiedChainAsync(fixture.Factory);
        await fixture.ExecuteAsync("""
            INSERT INTO ReportSnapshots
              (SnapshotId,ReportId,StationId,PeriodStartMinute,PeriodEndMinute,PeriodKind,SnapshotSequence,
               SupersedesSnapshotId,PayloadSchemaVersion,CanonicalJson,ChecksumAlgorithm,IntegrityFormatVersion,
               ChecksumValue,CanonicalPayloadLength,SourceRevision,FinalizedAt)
            VALUES('snap-1','report-1','rasht',1,2,'Monthly',1,NULL,1,'{\"value\":1}','SHA256','v1','ABC',11,'rev-1','2026-08-22T08:00:00Z');
            INSERT INTO ReportPeriodLocks
              (StationId,PeriodStartMinute,PeriodEndMinute,PeriodKind,LockState,EffectiveSnapshotId,Revision,
               FinalizationId,FinalizedAt,ActorIdentity)
            VALUES('rasht',1,2,'Monthly','Finalized','snap-1',1,'fin-1','2026-08-22T08:00:00Z','shift-1');
            INSERT INTO SecurityDeploymentSettings
              (SingletonId,EsdAdjustmentCanonical,Revision,UpdatedAtUtc,UpdatedByShiftProfileId)
            VALUES(1,'2.5',1,'2026-08-22T08:00:00Z',NULL);
            """);
        Services services = CreateServices();
        DatabaseStructuralFingerprint before = await services.Fingerprints.CaptureAsync(fixture.SourcePath);
        DatabaseBackupVerificationResult backup = await services.Backup.CreateVerifiedBackupAsync(
            fixture.SourcePath, fixture.BackupPath, BackupOverwritePolicy.Deny);

        MigrationRehearsalResult result = await services.Rehearsal.RehearseAsync(backup);
        DatabaseStructuralFingerprint after = await services.Fingerprints.CaptureAsync(fixture.SourcePath);

        Assert.True(result.Passed);
        Assert.Equal(EsdReconciliationState.TargetAlreadyProvisionedSameValue, result.EsdReconciliationState);
        Assert.Equal(before.FinalizedSnapshotHashes, after.FinalizedSnapshotHashes);
        Assert.Equal(before.ReportLockHashes, after.ReportLockHashes);
        Assert.Equal("2.5", after.TargetEsdCanonicalValue);
    }

    [Fact]
    public async Task ESD_conflict_blocks_rehearsal_without_cutover()
    {
        await using var fixture = ReadinessFixture.Create();
        await fixture.CreateLegacyAsync();
        await RunUnifiedChainAsync(fixture.Factory);
        await fixture.ExecuteAsync("INSERT INTO SecurityDeploymentSettings(SingletonId,EsdAdjustmentCanonical,Revision,UpdatedAtUtc,UpdatedByShiftProfileId) VALUES(1,'9',1,'2026-08-22T08:00:00Z',NULL);");
        Services services = CreateServices();
        DatabaseBackupVerificationResult backup = await services.Backup.CreateVerifiedBackupAsync(
            fixture.SourcePath, fixture.BackupPath, BackupOverwritePolicy.Deny);

        MigrationRehearsalResult result = await services.Rehearsal.RehearseAsync(backup);

        Assert.False(result.Passed);
        Assert.Equal(MigrationRehearsalFailure.EsdConflict, result.Failure);
        Assert.Equal(EsdAuthorityMode.LegacyAuthoritative, result.EsdAuthorityMode);
        Assert.Equal("9", (string?)await fixture.ScalarAsync("SELECT EsdAdjustmentCanonical FROM SecurityDeploymentSettings WHERE SingletonId=1;"));
    }

    [Fact]
    public async Task Busy_policy_is_bounded_fail_closed_and_honors_cancellation()
    {
        var policy = new SqliteLockBusyPolicy(TimeSpan.FromSeconds(2), 2,
            new FixedSqliteRetryDelayPolicy(TimeSpan.Zero));
        var executor = new SqliteBusyRetryExecutor(policy);
        int invocations = 0;

        SqliteException busy = await Assert.ThrowsAsync<SqliteException>(() => executor.ExecuteAsync<int>((_, _) =>
        {
            invocations++;
            throw new SqliteException("busy", 5);
        }));
        Assert.Equal(5, busy.SqliteErrorCode);
        Assert.Equal(3, invocations);
        Assert.Equal(2, SqliteLockReadinessEvaluator.Evaluate(policy).MaximumRetryCount);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => executor.ExecuteAsync(
            (_, _) => Task.FromResult(1), cancellation.Token));
    }

    [Theory]
    [InlineData(10L, DiskSpaceReadinessStatus.InsufficientSpace)]
    [InlineData(10000L, DiskSpaceReadinessStatus.Ready)]
    [InlineData(null, DiskSpaceReadinessStatus.Unknown)]
    public async Task Disk_policy_reports_bounded_capacity_state(long? available,
        DiskSpaceReadinessStatus expected)
    {
        var service = new DiskSpaceReadinessService(new FixedCapacityProvider(available));
        DiskSpaceReadinessResult result = await service.EvaluateAsync(1000, "explicit-destination",
            new DiskSpaceSafetyPolicy(0.25m, 0.5m, 100));

        Assert.Equal(expected, result.Status);
        Assert.Equal(2850, result.Estimate.TotalRequiredBytes);
    }

    [Fact]
    public void Readiness_gate_blocks_missing_approval_and_describes_non_destructive_rollback_states()
    {
        DatabasePreflightResult preflight = SuccessfulPreflight();
        var classification = new MigrationHistoryClassificationResult(
            MigrationHistoryClassification.CleanLegacyBaseline, 4, Array.Empty<string>());
        DatabaseBackupVerificationResult backup = VerifiedBackup();
        MigrationRehearsalResult rehearsal = PassedRehearsal();
        var disk = new DiskSpaceReadinessResult(DiskSpaceReadinessStatus.Ready, 10_000,
            new(1, 1, 1, 1, 1, 5), "Ready");
        var locks = new SqliteLockReadinessResult(true, TimeSpan.FromSeconds(5), 2, "BoundedFailClosedPolicy");

        MaintenanceWindowReadinessResult blocked = MaintenanceWindowReadinessEvaluator.Evaluate(
            new(preflight, classification, backup, rehearsal, disk, locks, false));
        MaintenanceWindowReadinessResult ready = MaintenanceWindowReadinessEvaluator.Evaluate(
            new(preflight, classification, backup, rehearsal, disk, locks, true));

        Assert.Equal(MaintenanceReadinessStatus.Blocked, blocked.Status);
        Assert.Contains("future-authorization-not-available", blocked.Blockers);
        Assert.Equal(MaintenanceReadinessStatus.ReadyForFutureMigrationApproval, ready.Status);
        foreach (OperationalRollbackState state in Enum.GetValues<OperationalRollbackState>())
            Assert.False(string.IsNullOrWhiteSpace(OperationalRollbackExpectations.Describe(state)));
    }

    [Fact]
    public void Unified_chain_introduces_neither_RBAC_nor_a_Support_identity()
    {
        string migrationDdl = string.Join('\n', UnifiedTargetMigrationChain.Create(new Sha256ChecksumService())
            .Select(migration => migration.ChecksumPayload));

        Assert.DoesNotContain("RBAC", migrationDdl, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Permission", migrationDdl, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Role", migrationDdl, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Support", migrationDdl, StringComparison.OrdinalIgnoreCase);
    }

    private static Services CreateServices()
    {
        var clock = new FixedClock(Now);
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
        var rehearsal = new MigrationRehearsalService(restore,
            new SystemTemporaryRehearsalWorkspaceFactory(), preflight, fingerprints, checksums,
            new BoundedEsdAdjustmentReconciliationPolicy(10_000m), clock);
        return new(inspector, preflight, fingerprints, classifier, backup, restore, rehearsal);
    }

    private static Task<DatabasePreflightResult> AnalyzeAsync(Services services, ReadinessFixture fixture) =>
        services.Preflight.AnalyzeAsync(fixture.SourcePath, IntegrityCheckStrategy.QuickCheck);

    private static async Task<MigrationRunResult> RunUnifiedChainAsync(SqliteConnectionFactory factory)
    {
        var checksums = new Sha256ChecksumService();
        var runner = new MigrationRunner(new SqliteTransactionManager(factory),
            new MigrationChecksumValidator(checksums));
        return await runner.RunPendingAsync(UnifiedTargetMigrationChain.Create(checksums));
    }

    private static async Task<string> ComputeSha256Async(string path)
    {
        await using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream));
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    private static DatabasePreflightResult SuccessfulPreflight() => new(
        true, new(true, new("x", 1, Now, Now, "id"), DatabaseTargetFailure.None, "Valid"), true, true,
        ["ok"], Array.Empty<string>(), 1, 0, new(false, false, false, null,
            Array.Empty<InspectedMigrationEntry>(), null), Array.Empty<SqliteSchemaObject>(),
        new Dictionary<string, long>(), Array.Empty<string>(), Array.Empty<string>(),
        new(InspectedEsdValueState.Absent, null, 0), new(InspectedEsdValueState.Absent, null, 0),
        new(0, new Dictionary<string, string>(), 0, new Dictionary<string, string>()),
        "wal", true, false, Array.Empty<string>());

    private static DatabaseBackupVerificationResult VerifiedBackup() => new(true, "backup", null, null,
        "ABC", 1, Now, 0, MigrationHistoryClassification.CleanLegacyBaseline, true,
        DatabaseBackupFailure.None, Array.Empty<string>());

    private static MigrationRehearsalResult PassedRehearsal() => new(true, MigrationRehearsalFailure.None,
        0, 4, Array.Empty<string>(), true, true, null, EsdReconciliationState.ReadyToProvision,
        EsdAuthorityMode.LegacyAuthoritative, Array.Empty<string>());

    private sealed record Services(
        ExplicitDatabaseTargetInspector Inspector,
        ReadOnlyDatabasePreflightAnalyzer Preflight,
        DatabaseStructuralFingerprintService Fingerprints,
        MigrationHistoryClassifier Classifier,
        ExplicitSqliteBackupService Backup,
        RestoreValidationService Restore,
        MigrationRehearsalService Rehearsal);

    private sealed record FixedClock(DateTimeOffset UtcNow) : IClock
    {
        public DateTimeOffset LocalNow => UtcNow.ToOffset(TimeSpan.FromHours(3.5));
    }

    private sealed record FixedCapacityProvider(long? Available) : IDiskCapacityProvider
    {
        public Task<long?> GetAvailableBytesAsync(string explicitDestinationPath,
            CancellationToken cancellationToken = default) => Task.FromResult(Available);
    }

    private sealed class ReadinessFixture : IAsyncDisposable
    {
        private readonly string _directory;

        private ReadinessFixture(string directory)
        {
            _directory = directory;
            Directory.CreateDirectory(directory);
            SourcePath = Path.Combine(directory, "explicit-source.sqlite");
            BackupPath = Path.Combine(directory, "explicit-backup.sqlite");
            Factory = new SqliteConnectionFactory(new SqliteDatabaseOptions
            {
                DataSource = SourcePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false
            });
        }

        public string SourcePath { get; }
        public string BackupPath { get; }
        public SqliteConnectionFactory Factory { get; }

        public static ReadinessFixture Create()
        {
            string directory = Path.Combine(Path.GetTempPath(), "RahNegar.Tests", "Phase79",
                Guid.NewGuid().ToString("N"));
            string productionSuffix = Path.Combine("Data", "db.sys");
            if (directory.EndsWith(productionSuffix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Tests cannot select the production database.");
            return new(directory);
        }

        public Task CreateLegacyAsync(bool includePreservationEvidence = false) => ExecuteAsync(includePreservationEvidence
            ? """
              CREATE TABLE app_settings(id INTEGER PRIMARY KEY,esd_extra_runtime_hours REAL NOT NULL);
              INSERT INTO app_settings VALUES(1,2.5);
              CREATE TABLE unit_runtime_base(id INTEGER PRIMARY KEY,hours REAL NOT NULL);
              INSERT INTO unit_runtime_base VALUES(1,123.5);
              CREATE TABLE tbl_events(id INTEGER PRIMARY KEY,kind TEXT NOT NULL);
              INSERT INTO tbl_events VALUES(1,'legacy-event');
              CREATE TABLE unrelated_legacy(id INTEGER PRIMARY KEY,value TEXT NOT NULL);
              INSERT INTO unrelated_legacy VALUES(1,'preserve-me');
              CREATE TABLE tbl_monthly_report_header(id INTEGER PRIMARY KEY,payload BLOB NOT NULL);
              INSERT INTO tbl_monthly_report_header VALUES(1,X'000102FEFF');
              CREATE TABLE tbl_monthly_lock(id INTEGER PRIMARY KEY,is_finalized INTEGER NOT NULL);
              INSERT INTO tbl_monthly_lock VALUES(1,1);
              """
            : """
              CREATE TABLE app_settings(id INTEGER PRIMARY KEY,esd_extra_runtime_hours REAL NOT NULL);
              INSERT INTO app_settings VALUES(1,2.5);
              CREATE TABLE unit_runtime_base(id INTEGER PRIMARY KEY,hours REAL NOT NULL);
              INSERT INTO unit_runtime_base VALUES(1,123.5);
              CREATE TABLE tbl_events(id INTEGER PRIMARY KEY,kind TEXT NOT NULL);
              INSERT INTO tbl_events VALUES(1,'legacy-event');
              """);

        public Task CreateEmptyLedgerAsync(int version) => ExecuteAsync($"""
            CREATE TABLE __rahnegar_schema_version(
              singleton_id INTEGER PRIMARY KEY CHECK(singleton_id=1),
              current_version INTEGER NOT NULL CHECK(current_version>=0));
            INSERT INTO __rahnegar_schema_version VALUES(1,{version});
            CREATE TABLE __rahnegar_migration_history(
              migration_id TEXT PRIMARY KEY NOT NULL,
              from_version INTEGER NOT NULL,
              to_version INTEGER NOT NULL,
              checksum TEXT NOT NULL,
              applied_at_utc TEXT NOT NULL);
            """);

        public async Task CreateLedgerAsync(int version, string migrationId, int from, int to, string checksum)
        {
            await CreateEmptyLedgerAsync(version);
            await using SqliteConnection connection = await Factory.OpenConnectionAsync();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO __rahnegar_migration_history VALUES($id,$from,$to,$checksum,$at);";
            command.Parameters.AddWithValue("$id", migrationId);
            command.Parameters.AddWithValue("$from", from);
            command.Parameters.AddWithValue("$to", to);
            command.Parameters.AddWithValue("$checksum", checksum);
            command.Parameters.AddWithValue("$at", Now.ToString("O"));
            await command.ExecuteNonQueryAsync();
        }

        public async Task ExecuteAsync(string sql)
        {
            await using SqliteConnection connection = await Factory.OpenConnectionAsync();
            await ProductionMigrationReadinessFoundationTests.ExecuteAsync(connection, sql);
        }

        public async Task<object?> ScalarAsync(string sql)
        {
            await using SqliteConnection connection = await Factory.OpenConnectionAsync();
            return await ProductionMigrationReadinessFoundationTests.ScalarAsync(connection, sql);
        }

        public async Task<bool> TableExistsAsync(string table)
        {
            await using SqliteConnection connection = await Factory.OpenConnectionAsync();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name;";
            command.Parameters.AddWithValue("$name", table);
            return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
        }

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "RahNegar.Tests", "Phase79"))
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string target = Path.GetFullPath(_directory);
            if (target.StartsWith(root, StringComparison.OrdinalIgnoreCase) && Directory.Exists(target))
                Directory.Delete(target, recursive: true);
            return ValueTask.CompletedTask;
        }
    }
}
