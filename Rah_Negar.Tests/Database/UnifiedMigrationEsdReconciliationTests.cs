using System.Globalization;
using Microsoft.Data.Sqlite;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Infrastructure.Database;
using Rah_Negar.Infrastructure.Database.Checksums;
using Rah_Negar.Infrastructure.Database.Migrations;
using Rah_Negar.Infrastructure.Database.Migrations.Drafts;
using Rah_Negar.Infrastructure.Security;

namespace Rah_Negar.Tests.Database;

public sealed class UnifiedMigrationEsdReconciliationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Inventory_Is_Deterministic_Unique_Contiguous_And_NonDestructive()
    {
        IReadOnlyList<IDatabaseMigration> first = Chain();
        IReadOnlyList<IDatabaseMigration> second = Chain();
        Assert.Equal(4, first.Count);
        Assert.Equal(first.Select(x => x.Metadata.MigrationId), second.Select(x => x.Metadata.MigrationId));
        Assert.Equal(first.Select(x => x.Metadata.Checksum), second.Select(x => x.Metadata.Checksum));
        Assert.Equal(first.Count, first.Select(x => x.Metadata.MigrationId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(first.Count, first.Select(x => x.Metadata.FromVersion).Distinct().Count());
        Assert.Equal(first.Count, first.Select(x => x.Metadata.ToVersion).Distinct().Count());
        Assert.Equal(0, first[0].Metadata.FromVersion);
        Assert.Equal(UnifiedTargetMigrationChain.FinalVersion, first[^1].Metadata.ToVersion);
        foreach (IDatabaseMigration migration in first)
        for (int i = 1; i < first.Count; i++) Assert.Equal(first[i - 1].Metadata.ToVersion, first[i].Metadata.FromVersion);
        Assert.All(first, migration =>
        {
            Assert.Equal(64, migration.Metadata.Checksum.Length);
            Assert.DoesNotContain("DROP TABLE", migration.ChecksumPayload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DELETE FROM", migration.ChecksumPayload, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Empty_Database_Applies_Complete_Chain_Then_Reruns_As_NoOp()
    {
        await using TemporarySqliteDatabase db = TemporarySqliteDatabase.Create();
        MigrationRunResult first = await Runner(db).RunPendingAsync(Chain());
        MigrationRunResult rerun = await Runner(db).RunPendingAsync(Chain());
        Assert.Equal(UnifiedTargetMigrationChain.FinalVersion, first.FinalVersion);
        Assert.Equal(4, first.AppliedMigrationIds.Count);
        Assert.Empty(rerun.AppliedMigrationIds);
        Assert.Equal(4L, await ScalarLongAsync(db, "SELECT COUNT(*) FROM __rahnegar_migration_history;"));
        foreach (string table in new[] { "Stations", "Units", "SecurityShiftProfiles", "Events", "ReportSnapshots" })
            Assert.True(await TableExistsAsync(db, table));
    }

    [Fact]
    public async Task Representative_Legacy_Database_Is_Preserved_And_Coexists()
    {
        await using TemporarySqliteDatabase db = TemporarySqliteDatabase.Create();
        await CreateLegacyAsync(db, "2.5");
        await Runner(db).RunPendingAsync(Chain());
        await Runner(db).RunPendingAsync(Chain());
        Assert.Equal("LegacyStation", await ScalarStringAsync(db, "SELECT station_name FROM app_settings WHERE id=1;"));
        Assert.Equal(77L, await ScalarLongAsync(db, "SELECT runtime_minutes FROM unit_runtime_base WHERE id=1;"));
        Assert.Equal("ESD", await ScalarStringAsync(db, "SELECT event_type FROM tbl_events WHERE id=1;"));
        Assert.Equal("keep", await ScalarStringAsync(db, "SELECT value FROM arbitrary_legacy WHERE id=1;"));
        Assert.True(await TableExistsAsync(db, "SecurityDeploymentSettings"));
        Assert.True(await TableExistsAsync(db, "ReportSnapshots"));
    }

    [Fact]
    public async Task Runner_Rejects_Duplicate_Id_Version_Collisions_And_Missing_Intermediate()
    {
        await using TemporarySqliteDatabase db = TemporarySqliteDatabase.Create();
        IDatabaseMigration a = Fake("same", 0, 1, "CREATE TABLE a(id INTEGER);");
        IDatabaseMigration duplicateId = Fake("same", 1, 2, "CREATE TABLE b(id INTEGER);");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Runner(db).RunPendingAsync([a, duplicateId]));
        IDatabaseMigration duplicateTarget = Fake("b", 0, 1, "CREATE TABLE b(id INTEGER);");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Runner(db).RunPendingAsync([a, duplicateTarget]));
        IDatabaseMigration gap = Fake("gap", 2, 3, "CREATE TABLE gap(id INTEGER);");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Runner(db).RunPendingAsync([a, gap]));
    }

    [Fact]
    public async Task Runner_Rejects_Checksum_History_And_Schema_Version_Tampering()
    {
        await using TemporarySqliteDatabase db = TemporarySqliteDatabase.Create();
        await Runner(db).RunPendingAsync(Chain());
        await ExecuteAsync(db, "UPDATE __rahnegar_migration_history SET checksum='tampered' WHERE to_version=2;");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Runner(db).RunPendingAsync(Chain()));

        await using TemporarySqliteDatabase wrong = TemporarySqliteDatabase.Create();
        await Runner(wrong).ReadHistoryAsync();
        await ExecuteAsync(wrong, """
            UPDATE __rahnegar_schema_version SET current_version=99;
            INSERT INTO __rahnegar_migration_history VALUES('fabricated',0,99,'x','2026-08-24T00:00:00Z');
            """);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Runner(wrong).RunPendingAsync(Chain()));
    }

    [Fact]
    public async Task Intermediate_Failure_Rolls_Back_Chain_And_History()
    {
        await using TemporarySqliteDatabase db = TemporarySqliteDatabase.Create();
        IReadOnlyList<IDatabaseMigration> normal = Chain();
        IDatabaseMigration failing = new FailingMigration(2, 3);
        await Assert.ThrowsAsync<InjectedMigrationException>(() => Runner(db).RunPendingAsync(
            [normal[0], normal[1], failing, normal[3]]));
        Assert.False(await TableExistsAsync(db, "Stations"));
        Assert.False(await TableExistsAsync(db, "intermediate_transient"));
        Assert.False(await TableExistsAsync(db, "__rahnegar_migration_history"));

        MigrationRunResult recovery = await Runner(db).RunPendingAsync(normal);
        Assert.Equal(4, recovery.FinalVersion);
    }

    [Theory]
    [InlineData("0", "0")]
    [InlineData("12", "12")]
    [InlineData("12.5000", "12.5")]
    [InlineData(" 7.25 ", "7.25")]
    [InlineData("999999.999999", "999999.999999")]
    public async Task Legacy_Esd_Uses_Exact_Invariant_Decimal_And_Zero_Is_Valid(string raw, string canonical)
    {
        await using ReconciliationFixture f = await ReconciliationFixture.CreateAsync(raw);
        LegacyEsdValueResult read = await f.Legacy.ReadAsync();
        Assert.Equal(EsdReconciliationState.LegacyValueFound, read.State);
        Assert.Equal(canonical, read.CanonicalValue);
        EsdReconciliationResult provisioned = await f.Service.ProvisionAsync("correlation-1", Now);
        Assert.Equal(EsdReconciliationState.Provisioned, provisioned.State);
        Assert.Equal(EsdAuthorityMode.LegacyAuthoritative, provisioned.AuthorityMode);
        Assert.Equal(canonical, await ScalarStringAsync(f.Db, "SELECT EsdAdjustmentCanonical FROM SecurityDeploymentSettings;"));
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("1,5")]
    [InlineData("-0.01")]
    [InlineData("1000000")]
    public async Task Malformed_Negative_Locale_And_OutOfPolicy_Legacy_Values_Are_Rejected(string raw)
    {
        await using ReconciliationFixture f = await ReconciliationFixture.CreateAsync(raw);
        EsdReconciliationResult result = await f.Service.ProvisionAsync("correlation-1", Now);
        Assert.Equal(EsdReconciliationState.LegacyValueInvalid, result.State);
        Assert.Equal(0L, await ScalarLongAsync(f.Db, "SELECT COUNT(*) FROM SecurityDeploymentSettings;"));
    }

    [Fact]
    public async Task Missing_And_Multiple_Legacy_Rows_Are_Explicit()
    {
        await using TemporarySqliteDatabase db = TemporarySqliteDatabase.Create();
        await Runner(db).RunPendingAsync(Chain());
        var reader = new SQLiteLegacyEsdValueReader(db.Factory, new BoundedEsdAdjustmentReconciliationPolicy(999999.999999m));
        Assert.Equal(EsdReconciliationState.LegacyValueMissing, (await reader.ReadAsync()).State);
        await ExecuteAsync(db, "CREATE TABLE app_settings(id INTEGER PRIMARY KEY,esd_extra_runtime_hours REAL NOT NULL DEFAULT 0); INSERT INTO app_settings VALUES(1,1),(2,2);");
        Assert.Equal(EsdReconciliationState.LegacyValueInvalid, (await reader.ReadAsync()).State);
    }

    [Fact]
    public async Task Existing_Same_Value_Is_Idempotent_And_Different_Value_Is_Conflict_NoOverwrite()
    {
        await using ReconciliationFixture same = await ReconciliationFixture.CreateAsync("2.5");
        await same.Target.TryProvisionAsync(2.5m, "2.5", Now);
        Assert.Equal(EsdReconciliationState.TargetAlreadyProvisionedSameValue,
            (await same.Service.ProvisionAsync("same", Now)).State);

        await using ReconciliationFixture different = await ReconciliationFixture.CreateAsync("2.5");
        await different.Target.TryProvisionAsync(9m, "9", Now);
        EsdReconciliationResult result = await different.Service.ProvisionAsync("different", Now);
        Assert.Equal(EsdReconciliationState.TargetAlreadyProvisionedDifferentValue, result.State);
        Assert.Equal("Conflict", result.ResultCategory);
        Assert.Equal("9", await ScalarStringAsync(different.Db, "SELECT EsdAdjustmentCanonical FROM SecurityDeploymentSettings;"));
    }

    [Fact]
    public async Task Provisioning_Does_Not_Cut_Over_Authority_Or_Add_Unit_Scope()
    {
        await using ReconciliationFixture f = await ReconciliationFixture.CreateAsync("3.75");
        await f.Service.ProvisionAsync("correlation-1", Now);
        Assert.Equal(EsdAuthorityMode.LegacyAuthoritative, (await new InactivePreCutoverEsdAuthorityProvider().GetAsync()).Mode);
        string[] columns = await ColumnsAsync(f.Db, "SecurityDeploymentSettings");
        Assert.DoesNotContain(columns, x => x.Contains("Unit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Finalized_Snapshot_Bytes_And_Lock_Are_Unchanged_By_Reconciliation()
    {
        await using ReconciliationFixture f = await ReconciliationFixture.CreateAsync("4.5");
        byte[] payload = [0, 1, 2, 127, 128, 255];
        string canonical = Convert.ToBase64String(payload);
        await ExecuteAsync(f.Db, $"""
            INSERT INTO ReportSnapshots
             (SnapshotId,ReportId,StationId,PeriodStartMinute,PeriodEndMinute,PeriodKind,SnapshotSequence,
              SupersedesSnapshotId,PayloadSchemaVersion,CanonicalJson,ChecksumAlgorithm,IntegrityFormatVersion,
              ChecksumValue,CanonicalPayloadLength,SourceRevision,FinalizedAt)
            VALUES ('snapshot-1','report-1','station-1',1,2,'Monthly',1,NULL,1,'{canonical}',
              'SHA256','v1','checksum',{canonical.Length},'source','2026-08-24T12:00:00Z');
            INSERT INTO ReportPeriodLocks VALUES
              ('station-1',1,2,'Monthly','Finalized','snapshot-1',1,'finalization-1','2026-08-24T12:00:00Z','shift-1');
            """);
        string beforeSnapshot = await ScalarStringAsync(f.Db, "SELECT hex(CanonicalJson) FROM ReportSnapshots WHERE SnapshotId='snapshot-1';");
        string beforeLock = await ScalarStringAsync(f.Db, "SELECT quote(EffectiveSnapshotId)||':'||Revision FROM ReportPeriodLocks WHERE StationId='station-1';");
        await f.Service.ProvisionAsync("correlation-1", Now);
        Assert.Equal(beforeSnapshot, await ScalarStringAsync(f.Db, "SELECT hex(CanonicalJson) FROM ReportSnapshots WHERE SnapshotId='snapshot-1';"));
        Assert.Equal(beforeLock, await ScalarStringAsync(f.Db, "SELECT quote(EffectiveSnapshotId)||':'||Revision FROM ReportPeriodLocks WHERE StationId='station-1';"));
    }

    [Fact]
    public void Chain_Source_Has_No_Production_Discovery_Rbac_Support_Or_Destructive_Legacy_Sql()
    {
        string combined = string.Join('\n', Chain().Select(x => x.ChecksumPayload));
        Assert.DoesNotContain("app_settings", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RoleId", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupportProfile", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(typeof(UnifiedTargetMigrationChain).GetMethods(), x => x.Name.Contains("Discover", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<IDatabaseMigration> Chain() => UnifiedTargetMigrationChain.Create(new Sha256ChecksumService());
    private static MigrationRunner Runner(TemporarySqliteDatabase db) => new(new SqliteTransactionManager(db.Factory),
        new MigrationChecksumValidator(new Sha256ChecksumService()));
    private static IDatabaseMigration Fake(string id, int from, int to, string sql) => new InlineMigration(id, from, to, sql);

    private static async Task CreateLegacyAsync(TemporarySqliteDatabase db, string rawEsd)
    {
        await ExecuteAsync(db, $"""
            CREATE TABLE app_settings(id INTEGER PRIMARY KEY,is_initialized INTEGER NOT NULL,station_name TEXT NOT NULL,
              esd_extra_runtime_hours REAL NOT NULL DEFAULT 0);
            INSERT INTO app_settings VALUES(1,1,'LegacyStation','{rawEsd.Replace("'", "''")}');
            CREATE TABLE unit_runtime_base(id INTEGER PRIMARY KEY,runtime_minutes INTEGER NOT NULL);
            INSERT INTO unit_runtime_base VALUES(1,77);
            CREATE TABLE tbl_events(id INTEGER PRIMARY KEY,event_type TEXT NOT NULL);
            INSERT INTO tbl_events VALUES(1,'ESD');
            CREATE TABLE arbitrary_legacy(id INTEGER PRIMARY KEY,value TEXT NOT NULL);
            INSERT INTO arbitrary_legacy VALUES(1,'keep');
            """);
    }

    private static async Task ExecuteAsync(TemporarySqliteDatabase db, string sql)
    { await using SqliteConnection c=await db.Factory.OpenConnectionAsync();await using SqliteCommand cmd=c.CreateCommand();cmd.CommandText=sql;await cmd.ExecuteNonQueryAsync(); }
    private static async Task<bool> TableExistsAsync(TemporarySqliteDatabase db,string name)
    { await using SqliteConnection c=await db.Factory.OpenConnectionAsync();await using SqliteCommand cmd=c.CreateCommand();cmd.CommandText="SELECT EXISTS(SELECT 1 FROM sqlite_master WHERE type='table' AND name=$name);";cmd.Parameters.AddWithValue("$name",name);return Convert.ToInt32(await cmd.ExecuteScalarAsync())==1; }
    private static async Task<long> ScalarLongAsync(TemporarySqliteDatabase db,string sql)
    { await using SqliteConnection c=await db.Factory.OpenConnectionAsync();await using SqliteCommand cmd=c.CreateCommand();cmd.CommandText=sql;return Convert.ToInt64(await cmd.ExecuteScalarAsync(),CultureInfo.InvariantCulture); }
    private static async Task<string> ScalarStringAsync(TemporarySqliteDatabase db,string sql)
    { await using SqliteConnection c=await db.Factory.OpenConnectionAsync();await using SqliteCommand cmd=c.CreateCommand();cmd.CommandText=sql;return Convert.ToString(await cmd.ExecuteScalarAsync(),CultureInfo.InvariantCulture)!; }
    private static async Task<string[]> ColumnsAsync(TemporarySqliteDatabase db,string table)
    { await using SqliteConnection c=await db.Factory.OpenConnectionAsync();await using SqliteCommand cmd=c.CreateCommand();cmd.CommandText=$"PRAGMA table_info({table});";var result=new List<string>();await using SqliteDataReader r=await cmd.ExecuteReaderAsync();while(await r.ReadAsync())result.Add(r.GetString(1));return result.ToArray(); }

    private sealed class InlineMigration : IDatabaseMigration
    {
        public InlineMigration(string id,int from,int to,string sql) { ChecksumPayload=sql;Metadata=new(id,from,to,id,new Sha256ChecksumService().Compute(sql)); }
        public MigrationMetadata Metadata { get; } public string ChecksumPayload { get; }
        public async Task ApplyAsync(SqliteConnection connection,SqliteTransaction transaction,CancellationToken cancellationToken=default)
        { await using SqliteCommand command=connection.CreateCommand();command.Transaction=transaction;command.CommandText=ChecksumPayload;await command.ExecuteNonQueryAsync(cancellationToken); }
    }
    private sealed class FailingMigration : IDatabaseMigration
    {
        public FailingMigration(int from,int to) { Metadata=new("injected-failure",from,to,"failure",new Sha256ChecksumService().Compute(ChecksumPayload)); }
        public MigrationMetadata Metadata { get; } public string ChecksumPayload => "CREATE TABLE intermediate_transient(id INTEGER);";
        public async Task ApplyAsync(SqliteConnection connection,SqliteTransaction transaction,CancellationToken cancellationToken=default)
        { await using SqliteCommand command=connection.CreateCommand();command.Transaction=transaction;command.CommandText=ChecksumPayload;await command.ExecuteNonQueryAsync(cancellationToken);throw new InjectedMigrationException(); }
    }
    private sealed class InjectedMigrationException : Exception;

    private sealed class ReconciliationFixture : IAsyncDisposable
    {
        private ReconciliationFixture(TemporarySqliteDatabase db,SQLiteLegacyEsdValueReader legacy,
            SQLiteTargetEsdProvisioningStore target,LegacyEsdReconciliationService service)
        { Db=db;Legacy=legacy;Target=target;Service=service; }
        public TemporarySqliteDatabase Db { get; } public SQLiteLegacyEsdValueReader Legacy { get; }
        public SQLiteTargetEsdProvisioningStore Target { get; } public LegacyEsdReconciliationService Service { get; }
        public static async Task<ReconciliationFixture> CreateAsync(string raw)
        { var db=TemporarySqliteDatabase.Create();await CreateLegacyAsync(db,raw);await Runner(db).RunPendingAsync(Chain());var policy=new BoundedEsdAdjustmentReconciliationPolicy(999999.999999m);var legacy=new SQLiteLegacyEsdValueReader(db.Factory,policy);var target=new SQLiteTargetEsdProvisioningStore(db.Factory);return new(db,legacy,target,new(legacy,target,new InactivePreCutoverEsdAuthorityProvider())); }
        public ValueTask DisposeAsync()=>Db.DisposeAsync();
    }
}
