using Microsoft.Data.Sqlite;
using Rah_Negar.Foundation.Application.Reporting.Finalization;
using Rah_Negar.Foundation.Application.Reporting.Finalized;
using Rah_Negar.Infrastructure.Database;
using Rah_Negar.Infrastructure.Database.Checksums;
using Rah_Negar.Infrastructure.Database.Migrations;
using Rah_Negar.Infrastructure.Database.Migrations.Drafts;
using Rah_Negar.Infrastructure.Reporting.Snapshot;
using Rah_Negar.Tests.Database;
using Rah_Negar.Tests.Reporting.Synthetic;

namespace Rah_Negar.Tests.Reporting;

public sealed class FinalizedReportReaderTests
{
    [Fact]
    public async Task EffectiveRead_ReturnsValidSnapshotWithoutOperationalSources()
    {
        await using ReaderFixture fixture = await ReaderFixture.CreateAsync();
        await fixture.FinalizeAsync();
        Assert.False(await fixture.TableExistsAsync("tbl_events"));
        Assert.False(await fixture.TableExistsAsync("tbl_daily_data"));

        FinalizedReportReadResult result = await fixture.Reader.GetEffectiveAsync(Period());

        Assert.True(result.IsSuccess);
        Assert.Equal(FinalizedReportReadStatus.FoundValid, result.Status);
        Assert.Equal("snapshot-1", result.Snapshot!.Identity.SnapshotId);
        Assert.Equal("synthetic-read-revision-v1", result.Snapshot.Evidence.VerifiedSourceRevision);
    }

    [Fact]
    public async Task InvalidChecksum_ReturnsIntegrityInvalid()
    {
        await using ReaderFixture fixture = await ReaderFixture.CreateAsync();
        await fixture.FinalizeAsync();
        await fixture.CorruptSnapshotAsync("ChecksumValue", new string('0', 64));

        FinalizedReportReadResult result = await fixture.Reader.GetBySnapshotIdAsync("snapshot-1");

        Assert.Equal(FinalizedReportReadStatus.IntegrityInvalid, result.Status);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task UnsupportedPayloadSchema_ReturnsIntegrityUnsupported()
    {
        await using ReaderFixture fixture = await ReaderFixture.CreateAsync();
        await fixture.FinalizeAsync();
        await fixture.CorruptSnapshotAsync("PayloadSchemaVersion", 99);

        FinalizedReportReadResult result = await fixture.Reader.GetBySnapshotIdAsync("snapshot-1");

        Assert.Equal(FinalizedReportReadStatus.IntegrityUnsupported, result.Status);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task LockAndSnapshotIdentityMismatch_IsRejected()
    {
        await using ReaderFixture fixture = await ReaderFixture.CreateAsync();
        await fixture.FinalizeAsync();
        await fixture.InsertMismatchedLockAsync();

        FinalizedReportReadResult result = await fixture.Reader.GetEffectiveAsync(
            new("ramsar", 10_000, 53_200, "Monthly"));

        Assert.Equal(FinalizedReportReadStatus.LockSnapshotMismatch, result.Status);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task MissingSnapshotIdentity_ReturnsNotFound()
    {
        await using ReaderFixture fixture = await ReaderFixture.CreateAsync();

        FinalizedReportReadResult result = await fixture.Reader.GetBySnapshotIdAsync("missing-snapshot");

        Assert.Equal(FinalizedReportReadStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task PeriodWithoutLock_ReturnsNotFinalized()
    {
        await using ReaderFixture fixture = await ReaderFixture.CreateAsync();

        FinalizedReportReadResult result = await fixture.Reader.GetEffectiveAsync(Period());

        Assert.Equal(FinalizedReportReadStatus.NotFinalized, result.Status);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task UnsupportedSnapshotVersion_IsRejectedAfterValidChecksumRead()
    {
        await using ReaderFixture fixture = await ReaderFixture.CreateAsync(
            supportedSnapshotFormats: ["different-format"]);
        await fixture.FinalizeAsync();

        FinalizedReportReadResult result = await fixture.Reader.GetBySnapshotIdAsync("snapshot-1");

        Assert.Equal(FinalizedReportReadStatus.IntegrityUnsupported, result.Status);
    }

    private static FinalizedReportQuery Period() => new("rasht", 10_000, 53_200, "Monthly");

    private sealed class ReaderFixture : IAsyncDisposable
    {
        private readonly TemporarySqliteDatabase _database;
        private readonly SQLiteAtomicReportFinalizationService _coordinator;

        private ReaderFixture(TemporarySqliteDatabase database,
            SQLiteAtomicReportFinalizationService coordinator, IFinalizedReportReader reader)
        {
            _database = database;
            _coordinator = coordinator;
            Reader = reader;
        }

        public IFinalizedReportReader Reader { get; }

        public static async Task<ReaderFixture> CreateAsync(
            IEnumerable<string>? supportedSnapshotFormats = null)
        {
            TemporarySqliteDatabase database = TemporarySqliteDatabase.Create();
            var checksum = new Sha256ChecksumService();
            var runner = new MigrationRunner(new SqliteTransactionManager(database.Factory),
                new MigrationChecksumValidator(checksum));
            await runner.RunPendingAsync(UnifiedTargetMigrationChain.Create(checksum));

            var serializer = new CanonicalJsonReportSnapshotSerializer();
            var snapshots = new SQLiteReportSnapshotStore(database.Factory, serializer);
            var locks = new SQLiteReportPeriodLockStore(database.Factory);
            var receipts = new SQLiteFinalizationReceiptStore(database.Factory);
            var coordinator = new SQLiteAtomicReportFinalizationService(database.Factory,
                new ReportFinalizationValidator(), new ReportSnapshotFactory(), serializer,
                snapshots, locks, receipts);
            IFinalizedReportReader reader = new SnapshotFinalizedReportReader(snapshots, locks,
                supportedSnapshotFormats ?? ["snapshot-format-v1"], ["snapshot-integrity-v1"]);
            return new(database, coordinator, reader);
        }

        public async Task FinalizeAsync()
        {
            SyntheticPipelineResult pipeline = await new SyntheticReportingFixture().RunAsync(
                SyntheticReportingScenario.Complete);
            var request = new ReportFinalizationRequest("finalization-1", "snapshot-1",
                pipeline.Projection!, "rasht", 10_000, 53_200, ["unit-2", "unit-1"],
                "synthetic-read-revision-v1", "synthetic-read-revision-v1", 1, null,
                "synthetic-actor", new DateTimeOffset(2026, 8, 22, 13, 0, 0, TimeSpan.FromHours(3.5)),
                "finalization-policy-v1", "snapshot-integrity-v1");
            Assert.True((await _coordinator.FinalizeAsync(request, 0)).IsSuccess);
        }

        public async Task CorruptSnapshotAsync(string column, object value)
        {
            if (column is not ("ChecksumValue" or "PayloadSchemaVersion"))
                throw new ArgumentOutOfRangeException(nameof(column));
            await using SqliteConnection connection = await _database.Factory.OpenConnectionAsync();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"""
                DROP TRIGGER TR_ReportSnapshots_NoUpdate;
                UPDATE ReportSnapshots SET {column} = $value WHERE SnapshotId = 'snapshot-1';
                """;
            command.Parameters.AddWithValue("$value", value);
            await command.ExecuteNonQueryAsync();
        }

        public async Task InsertMismatchedLockAsync()
        {
            await using SqliteConnection connection = await _database.Factory.OpenConnectionAsync();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO ReportPeriodLocks
                    (StationId, PeriodStartMinute, PeriodEndMinute, PeriodKind, LockState,
                     EffectiveSnapshotId, Revision, FinalizationId, FinalizedAt, ActorIdentity)
                VALUES ('ramsar', 10000, 53200, 'Monthly', 'Finalized', 'snapshot-1', 1,
                        'mismatch-fixture', '2026-08-22T13:00:00.0000000+03:30', 'fixture');
                """;
            await command.ExecuteNonQueryAsync();
        }

        public async Task<bool> TableExistsAsync(string table)
        {
            await using SqliteConnection connection = await _database.Factory.OpenConnectionAsync();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name;";
            command.Parameters.AddWithValue("$name", table);
            return (long)(await command.ExecuteScalarAsync())! == 1;
        }

        public ValueTask DisposeAsync() => _database.DisposeAsync();
    }
}
