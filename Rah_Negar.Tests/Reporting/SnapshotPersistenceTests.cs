using Microsoft.Data.Sqlite;
using Rah_Negar.Core.Reporting.Snapshot;
using Rah_Negar.Foundation.Application.Reporting.Finalization;
using Rah_Negar.Foundation.Application.Reporting.Persistence;
using Rah_Negar.Infrastructure.Database;
using Rah_Negar.Infrastructure.Database.Checksums;
using Rah_Negar.Infrastructure.Database.Migrations;
using Rah_Negar.Infrastructure.Database.Migrations.Drafts;
using Rah_Negar.Infrastructure.Reporting.Snapshot;
using Rah_Negar.Tests.Database;
using Rah_Negar.Tests.Reporting.Synthetic;

namespace Rah_Negar.Tests.Reporting;

public sealed class SnapshotPersistenceTests
{
    [Fact]
    public async Task Migration_CreatesOnlyTargetSnapshotTables()
    {
        await using TemporarySqliteDatabase database = await DatabaseAsync();

        Assert.True(await TableExistsAsync(database, "ReportSnapshots"));
        Assert.True(await TableExistsAsync(database, "ReportPeriodLocks"));
        Assert.True(await TableExistsAsync(database, "ReportFinalizationReceipts"));
    }

    [Fact]
    public async Task Serializer_IsDeterministicAndRoundTripsWithChecksumValidation()
    {
        FinalizedReportSnapshot snapshot = await SnapshotAsync();
        var serializer = new CanonicalJsonReportSnapshotSerializer();

        SerializedReportSnapshot first = serializer.Serialize(snapshot);
        SerializedReportSnapshot second = serializer.Serialize(snapshot);
        FinalizedReportSnapshot restored = serializer.Deserialize(first);

        Assert.Equal(first.CanonicalJson, second.CanonicalJson);
        Assert.Equal(first.Checksum.Value, second.Checksum.Value);
        Assert.Equal(SnapshotChecksumState.Calculated, restored.Checksum.State);
        Assert.Equal(snapshot.Identity.SnapshotId, restored.Identity.SnapshotId);
        Assert.Equal(snapshot.EventLog, restored.EventLog);
        Assert.Equal(snapshot.RuntimeSummaries, restored.RuntimeSummaries);
    }

    [Fact]
    public async Task SnapshotStore_InsertsReadsAndDetectsDuplicates()
    {
        await using TemporarySqliteDatabase database = await DatabaseAsync();
        Stores stores = Stores.Create(database);
        FinalizedReportSnapshot snapshot = await SnapshotAsync();

        SnapshotInsertResult inserted = await stores.Snapshots.InsertAsync(snapshot);
        SnapshotInsertResult replay = await stores.Snapshots.InsertAsync(snapshot);
        FinalizedReportSnapshot? restored = await stores.Snapshots.GetByIdAsync(snapshot.Identity.SnapshotId);
        FinalizedReportSnapshot changed = await SnapshotAsync(finalizationId: "finalization-other",
            finalizedAt: new DateTimeOffset(2026, 8, 22, 14, 0, 0, TimeSpan.FromHours(3.5)));
        SnapshotInsertResult conflict = await stores.Snapshots.InsertAsync(changed);

        Assert.Equal(SnapshotInsertOutcome.Inserted, inserted.Outcome);
        Assert.Equal(SnapshotInsertOutcome.AlreadyExistsSameContent, replay.Outcome);
        Assert.Equal(SnapshotInsertOutcome.Conflict, conflict.Outcome);
        Assert.NotNull(restored);
        Assert.Equal(snapshot.Evidence.FinalizationId, restored!.Evidence.FinalizationId);
    }

    [Fact]
    public async Task LockStore_UsesCompareRevisionAndRejectsSecondInitialTransition()
    {
        await using TemporarySqliteDatabase database = await DatabaseAsync();
        Stores stores = Stores.Create(database);
        FinalizedReportSnapshot snapshot = await SnapshotAsync();
        await stores.Snapshots.InsertAsync(snapshot);

        PeriodLockTransitionResult first = await stores.Locks.TryFinalizeAsync(snapshot.Identity,
            "finalization-1", snapshot.Evidence.FinalizedAt, "actor-1", 0);
        PeriodLockTransitionResult second = await stores.Locks.TryFinalizeAsync(snapshot.Identity,
            "finalization-2", snapshot.Evidence.FinalizedAt, "actor-1", 0);

        Assert.True(first.Succeeded);
        Assert.Equal(1, first.Lock!.Revision);
        Assert.False(second.Succeeded);
        Assert.Equal("report.lock.conflict", second.FailureCode);
        Assert.Equal(snapshot.Identity.SnapshotId, second.Lock!.EffectiveSnapshotId);
    }

    [Fact]
    public async Task AtomicCoordinator_CommitsAndReusesIdempotentResult()
    {
        await using TemporarySqliteDatabase database = await DatabaseAsync();
        Stores stores = Stores.Create(database);
        ReportFinalizationRequest request = await RequestAsync("finalization-1", "snapshot-1");

        AtomicFinalizationResult first = await stores.Coordinator.FinalizeAsync(request, 0);
        AtomicFinalizationResult replay = await stores.Coordinator.FinalizeAsync(request, 0);

        Assert.Equal(AtomicFinalizationOutcome.Committed, first.Outcome);
        Assert.Equal(AtomicFinalizationOutcome.IdempotentReplay, replay.Outcome);
        Assert.Equal(first.SnapshotId, replay.SnapshotId);
        Assert.Equal(first.LockRevision, replay.LockRevision);
        Assert.NotNull(await stores.Receipts.GetAsync(request.FinalizationId));
    }

    [Fact]
    public async Task LockConflict_RollsBackSnapshotAndReceipt()
    {
        await using TemporarySqliteDatabase database = await DatabaseAsync();
        Stores stores = Stores.Create(database);
        ReportFinalizationRequest winner = await RequestAsync("finalization-winner", "snapshot-winner");
        ReportFinalizationRequest loser = await RequestAsync("finalization-loser", "snapshot-loser",
            snapshotSequence: 2, supersedesSnapshotId: "snapshot-winner");
        Assert.True((await stores.Coordinator.FinalizeAsync(winner, 0)).IsSuccess);

        AtomicFinalizationResult result = await stores.Coordinator.FinalizeAsync(loser, 0);

        Assert.Equal(AtomicFinalizationOutcome.LockConflict, result.Outcome);
        Assert.Null(await stores.Snapshots.GetByIdAsync("snapshot-loser"));
        Assert.Null(await stores.Receipts.GetAsync("finalization-loser"));
        ReportPeriodLock? periodLock = await stores.Locks.ReadAsync("rasht", 10_000, 53_200, "Monthly");
        Assert.Equal("snapshot-winner", periodLock!.EffectiveSnapshotId);
    }

    [Fact]
    public async Task SimultaneousFinalizationAttempts_ProduceOneEffectiveSnapshot()
    {
        await using TemporarySqliteDatabase database = await DatabaseAsync();
        Stores stores = Stores.Create(database);
        ReportFinalizationRequest firstRequest = await RequestAsync("finalization-a", "snapshot-a");
        ReportFinalizationRequest secondRequest = await RequestAsync("finalization-b", "snapshot-b");

        AtomicFinalizationResult[] results = await Task.WhenAll(
            stores.Coordinator.FinalizeAsync(firstRequest, 0),
            stores.Coordinator.FinalizeAsync(secondRequest, 0));

        Assert.Single(results, x => x.IsSuccess);
        Assert.Single(results, x => !x.IsSuccess);
        ReportPeriodLock? periodLock = await stores.Locks.ReadAsync("rasht", 10_000, 53_200, "Monthly");
        Assert.NotNull(periodLock);
        Assert.Contains(periodLock!.EffectiveSnapshotId, new[] { "snapshot-a", "snapshot-b" });
        int persisted = (await stores.Snapshots.GetByIdAsync("snapshot-a") is null ? 0 : 1) +
                        (await stores.Snapshots.GetByIdAsync("snapshot-b") is null ? 0 : 1);
        Assert.Equal(1, persisted);
    }

    private static async Task<TemporarySqliteDatabase> DatabaseAsync()
    {
        TemporarySqliteDatabase database = TemporarySqliteDatabase.Create();
        var checksum = new Sha256ChecksumService();
        var runner = new MigrationRunner(new SqliteTransactionManager(database.Factory),
            new MigrationChecksumValidator(checksum));
        await runner.RunPendingAsync(UnifiedTargetMigrationChain.Create(checksum));
        return database;
    }

    private static async Task<FinalizedReportSnapshot> SnapshotAsync(
        string finalizationId = "finalization-1", string snapshotId = "snapshot-1",
        DateTimeOffset? finalizedAt = null)
    {
        ReportFinalizationRequest request = await RequestAsync(finalizationId, snapshotId, finalizedAt);
        var validator = new ReportFinalizationValidator();
        return new ReportSnapshotFactory().Create(request, validator.Validate(request)).Snapshot!;
    }

    private static async Task<ReportFinalizationRequest> RequestAsync(string finalizationId, string snapshotId,
        DateTimeOffset? finalizedAt = null, int snapshotSequence = 1, string? supersedesSnapshotId = null)
    {
        SyntheticPipelineResult pipeline = await new SyntheticReportingFixture().RunAsync(
            SyntheticReportingScenario.Complete);
        return new(finalizationId, snapshotId, pipeline.Projection!, "rasht", 10_000, 53_200,
            ["unit-2", "unit-1"], "synthetic-read-revision-v1", "synthetic-read-revision-v1",
            snapshotSequence, supersedesSnapshotId, "synthetic-actor", finalizedAt ??
            new DateTimeOffset(2026, 8, 22, 13, 0, 0, TimeSpan.FromHours(3.5)),
            "finalization-policy-v1", "snapshot-integrity-v1");
    }

    private static async Task<bool> TableExistsAsync(TemporarySqliteDatabase database, string table)
    {
        await using SqliteConnection connection = await database.Factory.OpenConnectionAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name;";
        command.Parameters.AddWithValue("$name", table);
        return (long)(await command.ExecuteScalarAsync())! == 1;
    }

    private sealed record Stores(SQLiteReportSnapshotStore Snapshots, SQLiteReportPeriodLockStore Locks,
        SQLiteFinalizationReceiptStore Receipts, SQLiteAtomicReportFinalizationService Coordinator)
    {
        public static Stores Create(TemporarySqliteDatabase database)
        {
            var serializer = new CanonicalJsonReportSnapshotSerializer();
            var snapshots = new SQLiteReportSnapshotStore(database.Factory, serializer);
            var locks = new SQLiteReportPeriodLockStore(database.Factory);
            var receipts = new SQLiteFinalizationReceiptStore(database.Factory);
            var coordinator = new SQLiteAtomicReportFinalizationService(database.Factory,
                new ReportFinalizationValidator(), new ReportSnapshotFactory(), serializer,
                snapshots, locks, receipts);
            return new(snapshots, locks, receipts, coordinator);
        }
    }
}
