using Microsoft.Data.Sqlite;
using Rah_Negar.Foundation.Application.Activation;
using Rah_Negar.Foundation.Application.Database.Readiness;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Foundation.Time;
using Rah_Negar.Infrastructure.Database;
using Rah_Negar.Infrastructure.Database.Checksums;
using Rah_Negar.Infrastructure.Database.Migrations;
using Rah_Negar.Infrastructure.Database.Migrations.Drafts;
using Rah_Negar.Infrastructure.Database.Readiness;

namespace Rah_Negar.Tests.Database;

public sealed class Phase95B6ProductionMigrationExecutorTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Approved_executor_migrates_explicit_disposable_copy_and_returns_immutable_receipt()
    {
        await using TemporarySqliteDatabase database = TemporarySqliteDatabase.Create();
        await CreateLegacyDatabaseAsync(database.Path);
        Services services = CreateServices();
        ApprovedProductionMigrationContext context = await CreateContextAsync(database, services, "success");
        byte[] backupBefore = await File.ReadAllBytesAsync(context.ExplicitVerifiedBackupPath!);

        ProductionMigrationExecutionResult result = await services.Executor.ExecuteAsync(context);

        Assert.Equal(ProductionMigrationExecutionStatus.Succeeded, result.Status);
        Assert.NotNull(result.Receipt);
        Assert.Equal(result.SafeReceiptId, result.Receipt!.ReceiptId);
        Assert.Equal(MigrationHistoryClassification.CleanLegacyBaseline, result.Receipt.InitialClassification);
        Assert.Equal(UnifiedTargetMigrationChain.FinalVersion, result.Receipt.FinalVersion);
        Assert.Equal(4, result.Receipt.AppliedMigrationIds.Count);
        Assert.False(result.Receipt.IdempotentRerun);
        Assert.True(result.Receipt.OriginalBackupUnchanged);
        Assert.True(result.Receipt.PostValidationPassed);
        Assert.True(result.Receipt.Preservation.Passed);
        Assert.True(result.Receipt.LegacyRemainsAuthoritative);
        Assert.True(result.Receipt.TargetRoutingDisabled);
        Assert.Equal(backupBefore, await File.ReadAllBytesAsync(context.ExplicitVerifiedBackupPath!));

        ProductionMigrationExecutionResult repeated = await services.Executor.ExecuteAsync(context);
        Assert.Equal(ProductionMigrationExecutionStatus.Rejected, repeated.Status);
        Assert.Equal("CurrentDatabaseIdentityDoesNotMatchApproval", repeated.ResultCategory);
    }

    [Fact]
    public async Task A_new_approved_context_reruns_idempotently_without_authority_change()
    {
        await using TemporarySqliteDatabase database = TemporarySqliteDatabase.Create();
        await CreateLegacyDatabaseAsync(database.Path);
        Services services = CreateServices();
        ApprovedProductionMigrationContext first = await CreateContextAsync(database, services, "first");
        ProductionMigrationExecutionResult firstResult = await services.Executor.ExecuteAsync(first);
        Assert.Equal(ProductionMigrationExecutionStatus.Succeeded, firstResult.Status);

        ApprovedProductionMigrationContext second = await CreateContextAsync(database, services, "second");
        ProductionMigrationExecutionResult secondResult = await services.Executor.ExecuteAsync(second);

        Assert.Equal(ProductionMigrationExecutionStatus.Succeeded, secondResult.Status);
        Assert.NotNull(secondResult.Receipt);
        Assert.True(secondResult.Receipt!.IdempotentRerun);
        Assert.Empty(secondResult.Receipt.AppliedMigrationIds);
        Assert.Equal(UnifiedTargetMigrationChain.FinalVersion, secondResult.Receipt.FinalVersion);
        Assert.True(secondResult.Receipt.LegacyRemainsAuthoritative);
        Assert.True(secondResult.Receipt.TargetRoutingDisabled);
    }

    [Fact]
    public async Task Hostile_context_backup_and_capacity_failures_reject_without_mutation()
    {
        await using TemporarySqliteDatabase database = TemporarySqliteDatabase.Create();
        await CreateLegacyDatabaseAsync(database.Path);
        Services services = CreateServices();
        ApprovedProductionMigrationContext context = await CreateContextAsync(database, services, "reject");
        byte[] before = await File.ReadAllBytesAsync(database.Path);
        Services limitedCapacityServices = CreateServices(0);

        ProductionMigrationExecutionResult badBackup = await services.Executor.ExecuteAsync(
            context with { VerifiedBackupSha256 = new string('B', 64) });
        ProductionMigrationExecutionResult missingBackup = await services.Executor.ExecuteAsync(
            context with { ExplicitVerifiedBackupPath = null, VerifiedBackupSha256 = null });
        ProductionMigrationExecutionResult blockedGuard = await services.Executor.ExecuteAsync(
            context with { GuardResult = new(ActivationGuardDecision.Blocked, ["manual-block"]) });
        ProductionMigrationExecutionResult insufficientCapacity = await limitedCapacityServices.Executor.ExecuteAsync(context);

        Assert.Equal(ProductionMigrationExecutionStatus.Rejected, badBackup.Status);
        Assert.Equal("VerifiedBackupPrerequisiteRejected", badBackup.ResultCategory);
        Assert.Equal(ProductionMigrationExecutionStatus.Rejected, missingBackup.Status);
        Assert.Equal("ApprovedMigrationContextRejected", missingBackup.ResultCategory);
        Assert.Equal(ProductionMigrationExecutionStatus.Rejected, blockedGuard.Status);
        Assert.Equal("ApprovedMigrationContextRejected", blockedGuard.ResultCategory);
        Assert.Equal(ProductionMigrationExecutionStatus.Rejected, insufficientCapacity.Status);
        Assert.Equal("DiskCapacityNotReady", insufficientCapacity.ResultCategory);
        Assert.Equal(before, await File.ReadAllBytesAsync(database.Path));
    }

    [Fact]
    public async Task Cancellation_is_honored_before_execution_and_does_not_mutate_database()
    {
        await using TemporarySqliteDatabase database = TemporarySqliteDatabase.Create();
        await CreateLegacyDatabaseAsync(database.Path);
        Services services = CreateServices();
        ApprovedProductionMigrationContext context = await CreateContextAsync(database, services, "cancel");
        byte[] before = await File.ReadAllBytesAsync(database.Path);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            services.Executor.ExecuteAsync(context, cancellation.Token));

        Assert.Equal(before, await File.ReadAllBytesAsync(database.Path));
        Assert.False(await TableExistsAsync(database.Path, "__rahnegar_schema_version"));
    }

    private static Services CreateServices(long capacity = 1_000_000_000)
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
        var restore = new RestoreValidationService(preflight, classifier);
        var backup = new ExplicitSqliteBackupService(preflight, fingerprints, classifier, clock);
        var disk = new DiskSpaceReadinessService(new FixedCapacityProvider(capacity));
        var lockPolicy = new SqliteLockBusyPolicy(TimeSpan.FromSeconds(2), 2,
            new FixedSqliteRetryDelayPolicy(TimeSpan.Zero));
        var busy = new SqliteBusyRetryExecutor(lockPolicy);
        var executor = new ProductionMigrationExecutor(clock, restore, preflight, fingerprints,
            checksums, disk, busy, new DiskSpaceSafetyPolicy(0.25m, 0.5m, 1024), lockPolicy);
        var rehearsal = new MigrationRehearsalService(restore,
            new SystemTemporaryRehearsalWorkspaceFactory(), preflight, fingerprints, checksums,
            new BoundedEsdAdjustmentReconciliationPolicy(10_000m), clock);
        return new(clock, preflight, classifier, backup, rehearsal, executor);
    }

    private static async Task<ApprovedProductionMigrationContext> CreateContextAsync(
        TemporarySqliteDatabase database, Services services, string suffix)
    {
        DatabasePreflightResult preflight = await services.Preflight.AnalyzeAsync(
            database.Path, IntegrityCheckStrategy.FullIntegrityCheck);
        MigrationHistoryClassificationResult classification = services.Classifier.Classify(preflight);
        string backupPath = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(database.Path)!, $"backup-{suffix}.sqlite");
        DatabaseBackupVerificationResult backup = await services.Backup.CreateVerifiedBackupAsync(
            database.Path, backupPath, BackupOverwritePolicy.Deny);
        Assert.True(backup.IsVerified, string.Join(',', backup.Errors));
        MigrationRehearsalResult rehearsal = await services.Rehearsal.RehearseAsync(backup);
        Assert.True(rehearsal.Passed, string.Join(',', rehearsal.Errors));

        string evidenceId = $"evidence-{suffix}";
        string correlation = $"correlation-{suffix}";
        var evidence = new ActivationEvidencePackage(
            evidenceId, correlation, preflight.TargetInspection.Target!.IdentityFingerprint,
            new(preflight.TargetInspection.Target.IdentityFingerprint, preflight.Succeeded,
                preflight.HeaderValid, preflight.IntegrityPassed,
                preflight.ForeignKeyViolations.Count == 0, preflight.ReadOnlyConnectionEnforced,
                preflight.TargetInspection.Target.InspectedAtUtc),
            new(classification.Classification, UnifiedTargetMigrationChain.FinalVersion, true, true),
            new($"backup-receipt-{suffix}", backup.SourceIdentity!.IdentityFingerprint,
                backup.BackupIdentity!.IdentityFingerprint, backup.IsVerified, backup.IntegrityPassed,
                backup.BackupSizeBytes, backup.CreatedAtUtc),
            new($"rehearsal-receipt-{suffix}", rehearsal.Passed, rehearsal.IdempotentRerun,
                rehearsal.OriginalBackupUnchanged, rehearsal.FinalVersion,
                rehearsal.EsdReconciliationState, rehearsal.EsdAuthorityMode,
                new(rehearsal.Preservation!.Passed, rehearsal.Preservation.FinalizedSnapshotsPreserved,
                    rehearsal.Preservation.ReportLocksPreserved, rehearsal.Preservation.LegacySchemaPreserved,
                    rehearsal.Preservation.LegacyEsdPreserved, rehearsal.Preservation.NoRbacIntroduced,
                    rehearsal.Preservation.NoSupportIdentityIntroduced), Now.AddMinutes(-5)),
            new(preflight.IntegrityPassed, preflight.ForeignKeyViolations.Count == 0,
                backup.IntegrityPassed, rehearsal.Preservation!.Passed),
            new(true, ProductionActivationScope.UnifiedMigrationActivation,
                preflight.TargetInspection.Target.IdentityFingerprint, evidenceId),
            Now.AddMinutes(1));
        ActivationEvidenceValidationResult evidenceValidation = ActivationEvidencePackageValidator.Validate(evidence);
        Assert.True(evidenceValidation.IsComplete, string.Join(',', evidenceValidation.Issues));
        var approval = new ProductionActivationApproval(
            $"approval-{suffix}", "management-review-reference", Now.AddMinutes(-3),
            ProductionActivationScope.UnifiedMigrationActivation,
            evidence.DatabaseIdentityFingerprint, evidence.EvidencePackageId, correlation,
            Now.AddMinutes(30));
        var readiness = new MaintenanceWindowReadinessResult(
            MaintenanceReadinessStatus.ReadyForFutureMigrationApproval,
            Array.Empty<string>(), Array.Empty<string>());
        ProductionActivationGuardResult guard = new ProductionActivationGuard(new FixedClock(Now)).Evaluate(
            new(readiness, preflight, classification, backup, rehearsal, evidence, approval,
                ProductionActivationScope.UnifiedMigrationActivation));
        Assert.True(guard.Decision == ActivationGuardDecision.Allowed,
            $"{guard.Decision}: {string.Join(',', guard.Reasons)}");
        var authorization = new ExplicitProductionMigrationAuthorization(
            $"authorization-{suffix}", "management-review-reference", approval.ApprovalId,
            evidence.EvidencePackageId, evidence.DatabaseIdentityFingerprint, correlation,
            Now.AddMinutes(-2), Now.AddMinutes(20));
        return new(database.Path, evidence, approval, authorization, guard, backupPath,
            backup.BackupSha256);
    }

    private static async Task CreateLegacyDatabaseAsync(string path)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        await using SqliteConnection connection = new(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString());
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE app_settings(id INTEGER PRIMARY KEY, esd_extra_runtime_hours REAL NOT NULL);
            INSERT INTO app_settings VALUES(1, 2.5);
            CREATE TABLE unit_runtime_base(id INTEGER PRIMARY KEY, hours REAL NOT NULL);
            INSERT INTO unit_runtime_base VALUES(1, 123.5);
            CREATE TABLE tbl_events(id INTEGER PRIMARY KEY, kind TEXT NOT NULL);
            INSERT INTO tbl_events VALUES(1, 'legacy-event');
            CREATE TABLE tbl_monthly_report_header(id INTEGER PRIMARY KEY, payload BLOB NOT NULL);
            INSERT INTO tbl_monthly_report_header VALUES(1, X'000102FEFF');
            CREATE TABLE tbl_monthly_lock(id INTEGER PRIMARY KEY, is_finalized INTEGER NOT NULL);
            INSERT INTO tbl_monthly_lock VALUES(1, 1);
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> TableExistsAsync(string path, string name)
    {
        await using SqliteConnection connection = new(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name;";
        command.Parameters.AddWithValue("$name", name);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    private sealed record Services(
        IClock Clock,
        ReadOnlyDatabasePreflightAnalyzer Preflight,
        MigrationHistoryClassifier Classifier,
        ExplicitSqliteBackupService Backup,
        MigrationRehearsalService Rehearsal,
        ProductionMigrationExecutor Executor);

    private sealed record FixedClock(DateTimeOffset UtcNow) : IClock
    {
        public DateTimeOffset LocalNow => UtcNow.ToOffset(TimeSpan.FromHours(3.5));
    }

    private sealed record FixedCapacityProvider(long Available) : IDiskCapacityProvider
    {
        public Task<long?> GetAvailableBytesAsync(string explicitDestinationPath,
            CancellationToken cancellationToken = default) => Task.FromResult<long?>(Available);
    }
}
