using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Infrastructure.Database.Checksums;
using Rah_Negar.Infrastructure.Database.Migrations;
using Rah_Negar.Infrastructure.Database.Migrations.Drafts;
using Rah_Negar.Infrastructure.Security;
using Rah_Negar.Tests.Database;

namespace Rah_Negar.Tests.Security;

public sealed class SecurityPersistenceAtomicEsdTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Migration_Creates_Target_Schema_And_Is_Idempotent()
    {
        await using TemporarySqliteDatabase db = TemporarySqliteDatabase.Create();
        MigrationRunner runner = Runner(db);
        MigrationRunResult first = await runner.RunPendingAsync(Chain());
        MigrationRunResult second = await runner.RunPendingAsync(Chain());
        Assert.Equal(UnifiedTargetMigrationChain.FinalVersion, first.FinalVersion);
        Assert.Empty(second.AppliedMigrationIds);
        string[] tables = await NamesAsync(db, "table");
        Assert.Contains("SecurityShiftProfiles", tables);
        Assert.Contains("SecurityConsumedVendorAuthorizations", tables);
        Assert.DoesNotContain(tables, x => x.Contains("Role", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("Support", StringComparison.OrdinalIgnoreCase) || x.Contains("Permission", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Migration_Preserves_Representative_Legacy_Data()
    {
        await using TemporarySqliteDatabase db = TemporarySqliteDatabase.Create();
        await ExecuteAsync(db, "CREATE TABLE legacy_records(id INTEGER PRIMARY KEY,value TEXT); INSERT INTO legacy_records VALUES(7,'preserve-me');");
        await Runner(db).RunPendingAsync(Chain());
        Assert.Equal("preserve-me", await ScalarAsync<string>(db, "SELECT value FROM legacy_records WHERE id=7;"));
    }

    [Fact]
    public async Task ShiftProfile_Crud_Preserves_Stable_Id_And_Has_No_Roles()
    {
        await using Fixture f = await Fixture.CreateAsync();
        ShiftProfile profile = Profile("shift-1", "  ab-100  ", 1);
        await f.Profiles.CreateAsync(profile);
        Assert.Equal("shift-1", (await f.Profiles.FindByPersonnelNoAsync("station-1", "AB-100"))!.ShiftProfileId);
        ShiftProfile changed = profile with { SupervisorFirstName = "Changed", PersonnelNo = "new-200", UpdatedAt = Now.AddMinutes(1) };
        Assert.True(await f.Profiles.UpdateAsync(changed, 1));
        ShiftProfile loaded = (await f.Profiles.FindByPersonnelNoAsync("station-1", " NEW-200 "))!;
        Assert.Equal("shift-1", loaded.ShiftProfileId);
        Assert.Equal(2, loaded.Revision);
        Assert.DoesNotContain(typeof(ShiftProfile).GetProperties(), p => p.Name.Contains("Role") || p.Name.Contains("Support"));
    }

    [Fact]
    public async Task Active_Normalized_PersonnelNo_Is_Unique_Per_Station()
    {
        await using Fixture f = await Fixture.CreateAsync();
        await f.Profiles.CreateAsync(Profile("shift-1", "abc", 1));
        await Assert.ThrowsAsync<SqliteException>(() => f.Profiles.CreateAsync(Profile("shift-2", " ABC ", 2)));
        await f.Profiles.CreateAsync(Profile("shift-other", "ABC", 1) with { StationId = "station-2" });
    }

    [Fact]
    public async Task Concurrent_Duplicate_PersonnelNo_Has_Exactly_One_Winner()
    {
        await using Fixture f = await Fixture.CreateAsync();
        Task<bool>[] attempts = [TryAsync(() => f.Profiles.CreateAsync(Profile("shift-a", "same", 1))),
            TryAsync(() => f.Profiles.CreateAsync(Profile("shift-b", " SAME ", 2)))];
        Assert.Equal(1, (await Task.WhenAll(attempts)).Count(x => x));
    }

    [Fact]
    public async Task ShiftProfile_Credential_Is_OneToOne_Current_And_Revisioned()
    {
        await using Fixture f = await Fixture.CreateAsync(); await f.SeedProfileAsync();
        Assert.True(await f.Credentials.ReplaceAsync(Credential(1), null));
        Assert.True(await f.Credentials.ReplaceAsync(Credential(2), 1));
        Assert.Equal(2, (await f.Credentials.LoadCurrentAsync("shift-1"))!.CredentialVersion);
        Assert.Equal(1L, await ScalarAsync<long>(f.Db, "SELECT COUNT(*) FROM SecurityShiftProfileCredentials WHERE ShiftProfileId='shift-1' AND IsCurrent=1;"));
        string[] columns = await ColumnsAsync(f.Db, "SecurityShiftProfileCredentials");
        Assert.DoesNotContain(columns, x => x.Contains("Username", StringComparison.OrdinalIgnoreCase) || x.Contains("Role", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Concurrent_Credential_Replacement_Has_One_Winner()
    {
        await using Fixture f = await Fixture.CreateAsync(); await f.SeedProfileAsync();
        Assert.True(await f.Credentials.ReplaceAsync(Credential(1), null));
        bool[] results = await Task.WhenAll(f.Credentials.ReplaceAsync(Credential(2), 1),
            f.Credentials.ReplaceAsync(Credential(3), 1));
        Assert.Equal(1, results.Count(x => x));
        Assert.Equal(1L, await ScalarAsync<long>(f.Db, "SELECT COUNT(*) FROM SecurityShiftProfileCredentials WHERE IsCurrent=1;"));
    }

    [Fact]
    public async Task ManagementCredential_Is_Singleton_Revisioned_And_Concurrency_Safe()
    {
        await using Fixture f = await Fixture.CreateAsync();
        bool[] initialized = await Task.WhenAll(f.Management.ReplaceAsync(Management(1), null),
            f.Management.ReplaceAsync(Management(2), null));
        Assert.Equal(1, initialized.Count(x => x));
        ManagementCredentialRecord current = (await f.Management.LoadCurrentAsync())!;
        Assert.True(await f.Management.ReplaceAsync(Management(current.CredentialVersion + 2), current.CredentialVersion));
        Assert.Equal(1L, await ScalarAsync<long>(f.Db, "SELECT COUNT(*) FROM SecurityManagementCredentials WHERE SingletonId=1 AND IsCurrent=1;"));
    }

    [Fact]
    public async Task DeviceId_Is_Opaque_Stable_Singleton()
    {
        await using Fixture f = await Fixture.CreateAsync();
        Assert.True(await f.Devices.TryProvisionAsync(new("9f67ea42c40c4f9ea67b5c96c1cbd470", Now, 1)));
        Assert.False(await f.Devices.TryProvisionAsync(new("anotheropaqueidentity0000000000", Now, 1)));
        Assert.Equal("9f67ea42c40c4f9ea67b5c96c1cbd470", await f.Devices.GetDeviceIdAsync());
    }

    [Fact]
    public async Task Vendor_Public_Key_Is_PublicOnly_Unique_And_Lifecycle_Preserved()
    {
        await using Fixture f = await Fixture.CreateAsync();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var item = new TrustedVendorPublicKey("key-1", key.ExportSubjectPublicKeyInfo(), Now.AddDays(-1), null);
        Assert.True(await f.Keys.AddAsync(item, "ECDSA-P256-SHA256", Now, 1));
        Assert.False(await f.Keys.AddAsync(item, "ECDSA-P256-SHA256", Now, 1));
        Assert.True(await f.Keys.RetireAsync("key-1", Now.AddDays(1), 1));
        Assert.NotNull((await f.Keys.FindByKeyIdAsync("key-1"))!.RetiredAtUtc);
        string[] columns = await ColumnsAsync(f.Db, "SecurityTrustedVendorPublicKeys");
        Assert.DoesNotContain(columns, x => x.Contains("Private", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Audit_Is_AppendOnly_And_Metadata_Is_AllowListed()
    {
        await using Fixture f = await Fixture.CreateAsync();
        await f.Audit.WriteAsync(new("shift-1", ProtectedAction.ChangeEsdAdjustment, "station-1",
            SecurityAuthorizationType.ExternalVendorSupport, true, Now, "correlation-1",
            SecurityAuditMetadataBuilder.Create([new("RequestId", "request-1"), new("DeviceId", "device-1")])));
        await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(f.Db, "UPDATE SecurityAuditEntries SET Scope='changed';"));
        await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(f.Db, "DELETE FROM SecurityAuditMetadata;"));
        await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(f.Db,
            "INSERT INTO SecurityAuditMetadata VALUES((SELECT AuditEntryId FROM SecurityAuditEntries LIMIT 1),'Password','secret');"));
    }

    [Fact]
    public async Task Atomic_Esd_Mutation_Preserves_Exact_Decimal_And_One_Receipt()
    {
        await using Fixture f = await Fixture.CreateAtomicAsync();
        AtomicEsdExecutionResult result = await f.Atomic.ExecuteOnceAsync(f.Consumption("request-1", "receipt-1"),
            123456789.123456789m, _ => Task.CompletedTask);
        Assert.Equal(AtomicEsdExecutionStatus.Executed, result.Status);
        Assert.Equal("123456789.123456789", await ScalarAsync<string>(f.Db, "SELECT EsdAdjustmentCanonical FROM SecurityDeploymentSettings WHERE SingletonId=1;"));
        Assert.Equal(1L, await ScalarAsync<long>(f.Db, "SELECT COUNT(*) FROM SecurityConsumedVendorAuthorizations;"));
        Assert.Equal(1L, await ScalarAsync<long>(f.Db, "SELECT COUNT(*) FROM SecurityProtectedExecutionReceipts WHERE ResultStatus='Succeeded';"));
    }

    [Fact]
    public async Task Same_Request_Replay_Cannot_Mutate_Twice()
    {
        await using Fixture f = await Fixture.CreateAtomicAsync();
        int callbacks = 0;
        Assert.Equal(AtomicEsdExecutionStatus.Executed, (await f.Atomic.ExecuteOnceAsync(f.Consumption("request-1", "receipt-1"), 1m, _ => { callbacks++; return Task.CompletedTask; })).Status);
        Assert.Equal(AtomicEsdExecutionStatus.AlreadyConsumed, (await f.Atomic.ExecuteOnceAsync(f.Consumption("request-1", "receipt-2"), 9m, _ => { callbacks++; return Task.CompletedTask; })).Status);
        Assert.Equal(1, callbacks);
        Assert.Equal("1", await ScalarAsync<string>(f.Db, "SELECT EsdAdjustmentCanonical FROM SecurityDeploymentSettings;"));
    }

    [Fact]
    public async Task Concurrent_Replay_Race_Has_Exactly_One_Commit()
    {
        await using Fixture f = await Fixture.CreateAtomicAsync();
        AtomicEsdExecutionResult[] results = await Task.WhenAll(
            f.Atomic.ExecuteOnceAsync(f.Consumption("same-request", "receipt-a"), 2m, _ => Task.CompletedTask),
            f.Atomic.ExecuteOnceAsync(f.Consumption("same-request", "receipt-b"), 2m, _ => Task.CompletedTask));
        Assert.Equal(1, results.Count(x => x.Status == AtomicEsdExecutionStatus.Executed));
        Assert.Equal(1L, await ScalarAsync<long>(f.Db, "SELECT COUNT(*) FROM SecurityConsumedVendorAuthorizations WHERE RequestId='same-request';"));
        Assert.Equal(1L, await ScalarAsync<long>(f.Db, "SELECT COUNT(*) FROM SecurityProtectedExecutionReceipts WHERE RequestId='same-request';"));
    }

    [Theory]
    [InlineData(AtomicEsdFailurePoint.AfterReplayCheck)]
    [InlineData(AtomicEsdFailurePoint.AfterConsumeInsert)]
    [InlineData(AtomicEsdFailurePoint.AfterSettingMutation)]
    [InlineData(AtomicEsdFailurePoint.AfterReceiptInsert)]
    [InlineData(AtomicEsdFailurePoint.BeforeCommit)]
    public async Task Injected_Failure_Rolls_Back_All_Database_State(AtomicEsdFailurePoint point)
    {
        await using Fixture f = await Fixture.CreateAtomicAsync(new Failure(point));
        Assert.Equal(AtomicEsdExecutionStatus.StoreFailed,
            (await f.Atomic.ExecuteOnceAsync(f.Consumption("request-fail", "receipt-fail"), 8.75m, _ => Task.CompletedTask)).Status);
        Assert.Equal("0", await ScalarAsync<string>(f.Db, "SELECT EsdAdjustmentCanonical FROM SecurityDeploymentSettings;"));
        Assert.Equal(0L, await ScalarAsync<long>(f.Db, "SELECT COUNT(*) FROM SecurityConsumedVendorAuthorizations;"));
        Assert.Equal(0L, await ScalarAsync<long>(f.Db, "SELECT COUNT(*) FROM SecurityProtectedExecutionReceipts;"));
    }

    [Fact]
    public async Task Consumed_And_Receipt_Evidence_Is_AppendOnly()
    {
        await using Fixture f = await Fixture.CreateAtomicAsync();
        await f.Atomic.ExecuteOnceAsync(f.Consumption("request-1", "receipt-1"), 1m, _ => Task.CompletedTask);
        await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(f.Db, "DELETE FROM SecurityConsumedVendorAuthorizations;"));
        await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(f.Db, "UPDATE SecurityProtectedExecutionReceipts SET ResultStatus='Succeeded';"));
    }

    [Fact]
    public async Task Durable_Replay_Store_Atomically_Claims_Request_Once()
    {
        await using Fixture f = await Fixture.CreateAtomicAsync();
        VendorAuthorizationConsumption value = f.Consumption("reserved-request", "reservation-receipt")
            with { ProposedEsdAdjustment = 7.25m };
        bool[] results = await Task.WhenAll(f.Replay.TryConsumeAsync(value), f.Replay.TryConsumeAsync(value));
        Assert.Equal(1, results.Count(x => x));
        Assert.True(await f.Replay.IsConsumedAsync("reserved-request", "correlation-1"));
        Assert.Equal("7.25", await ScalarAsync<string>(f.Db,
            "SELECT ProposedEsdAdjustmentCanonical FROM SecurityConsumedVendorAuthorizations WHERE RequestId='reserved-request';"));
    }

    [Fact]
    public async Task Esd_Is_DeploymentWide_And_Finalized_Snapshot_Is_Untouched()
    {
        await using Fixture f = await Fixture.CreateAtomicAsync();
        await ExecuteAsync(f.Db, """
            INSERT INTO ReportSnapshots
             (SnapshotId,ReportId,StationId,PeriodStartMinute,PeriodEndMinute,PeriodKind,SnapshotSequence,
              SupersedesSnapshotId,PayloadSchemaVersion,CanonicalJson,ChecksumAlgorithm,IntegrityFormatVersion,
              ChecksumValue,CanonicalPayloadLength,SourceRevision,FinalizedAt)
            VALUES ('final-1','report-1','station-1',1,2,'Monthly',1,NULL,1,'immutable-evidence',
              'SHA256','v1','checksum',18,'source','2026-08-24T12:00:00Z');
            """);
        await f.Atomic.ExecuteOnceAsync(f.Consumption("request-1", "receipt-1"), 4.5m, _ => Task.CompletedTask);
        Assert.Equal("immutable-evidence", await ScalarAsync<string>(f.Db, "SELECT CanonicalJson FROM ReportSnapshots WHERE SnapshotId='final-1';"));
        Assert.DoesNotContain(await ColumnsAsync(f.Db, "SecurityDeploymentSettings"), x => x.Contains("Unit", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<IDatabaseMigration> Chain() => UnifiedTargetMigrationChain.Create(new Sha256ChecksumService());
    private static MigrationRunner Runner(TemporarySqliteDatabase db) => new(
        new Infrastructure.Database.SqliteTransactionManager(db.Factory), new MigrationChecksumValidator(new Sha256ChecksumService()));

    private static ShiftProfile Profile(string id, string personnel, int shift) => new(id,"station-1",shift,$"Shift {shift}","First","Last",personnel,true,Now,Now,1);
    private static ShiftProfileCredentialRecord Credential(int version) => new("shift-1",version,"PBKDF2-SHA256","iterations=210000",[1,2,3],[4,5,6],true,Now.AddMinutes(version),null);
    private static ManagementCredentialRecord Management(int version) => new(version,"PBKDF2-SHA256","iterations=210000",[1],[2],true,true,Now,Now,null);

    private static async Task<bool> TryAsync(Func<Task> action) { try { await action(); return true; } catch (SqliteException) { return false; } }
    private static async Task ExecuteAsync(TemporarySqliteDatabase db, string sql)
    { await using SqliteConnection c=await db.Factory.OpenConnectionAsync(); await using SqliteCommand cmd=c.CreateCommand(); cmd.CommandText=sql; await cmd.ExecuteNonQueryAsync(); }
    private static async Task<T> ScalarAsync<T>(TemporarySqliteDatabase db,string sql)
    { await using SqliteConnection c=await db.Factory.OpenConnectionAsync(); await using SqliteCommand cmd=c.CreateCommand(); cmd.CommandText=sql; return (T)Convert.ChangeType((await cmd.ExecuteScalarAsync())!,typeof(T),CultureInfo.InvariantCulture); }
    private static async Task<string[]> NamesAsync(TemporarySqliteDatabase db,string type)
    { await using SqliteConnection c=await db.Factory.OpenConnectionAsync(); await using SqliteCommand cmd=c.CreateCommand(); cmd.CommandText="SELECT name FROM sqlite_master WHERE type=$type ORDER BY name;";cmd.Parameters.AddWithValue("$type",type);var list=new List<string>();await using SqliteDataReader r=await cmd.ExecuteReaderAsync();while(await r.ReadAsync())list.Add(r.GetString(0));return list.ToArray(); }
    private static async Task<string[]> ColumnsAsync(TemporarySqliteDatabase db,string table)
    { await using SqliteConnection c=await db.Factory.OpenConnectionAsync(); await using SqliteCommand cmd=c.CreateCommand(); cmd.CommandText=$"PRAGMA table_info({table});";var list=new List<string>();await using SqliteDataReader r=await cmd.ExecuteReaderAsync();while(await r.ReadAsync())list.Add(r.GetString(1));return list.ToArray(); }

    private sealed class Failure(AtomicEsdFailurePoint target) : IAtomicEsdFailureInjector
    { public void ThrowIfRequested(AtomicEsdFailurePoint point) { if(point==target) throw new InjectedFailureException(); } }
    private sealed class InjectedFailureException : Exception;

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(TemporarySqliteDatabase db,IAtomicEsdFailureInjector? failure)
        { Db=db;Profiles=new(db.Factory);Credentials=new(db.Factory);Management=new(db.Factory);Devices=new(db.Factory);Keys=new(db.Factory);Audit=new(db.Factory);Replay=new(db.Factory);Atomic=new(db.Factory,failure); }
        public TemporarySqliteDatabase Db { get; } public SQLiteShiftProfileRepository Profiles { get; }
        public SQLiteShiftProfileCredentialRepository Credentials { get; } public SQLiteManagementCredentialRepository Management { get; }
        public SQLiteDeviceIdentityRepository Devices { get; } public SQLiteTrustedVendorPublicKeyRepository Keys { get; }
        public SQLiteSecurityAuditSink Audit { get; } public SQLiteConsumedVendorAuthorizationStore Replay { get; }
        public SQLiteAtomicEsdAdjustmentExecutionBoundary Atomic { get; }
        public static async Task<Fixture> CreateAsync(IAtomicEsdFailureInjector? failure=null)
        { var db=TemporarySqliteDatabase.Create();await Runner(db).RunPendingAsync(Chain());return new(db,failure); }
        public static async Task<Fixture> CreateAtomicAsync(IAtomicEsdFailureInjector? failure=null)
        { Fixture f=await CreateAsync(failure);await f.SeedProfileAsync();using ECDsa key=ECDsa.Create(ECCurve.NamedCurves.nistP256);await f.Keys.AddAsync(new("key-1",key.ExportSubjectPublicKeyInfo(),Now.AddDays(-1),null),"ECDSA-P256-SHA256",Now,1);await ExecuteAsync(f.Db,"INSERT INTO SecurityDeploymentSettings VALUES(1,'0',1,'2026-08-24T12:00:00.0000000+00:00',NULL);");return f; }
        public async Task SeedProfileAsync() { if(await ScalarAsync<long>(Db,"SELECT COUNT(*) FROM SecurityShiftProfiles;")==0)await Profiles.CreateAsync(Profile("shift-1","1001",1)); }
        public VendorAuthorizationConsumption Consumption(string request,string receipt)=>new(request,"correlation-1",Now,receipt,"opaque-device-123456",VendorSupportAction.ChangeEsdAdjustment,"key-1","shift-1");
        public ValueTask DisposeAsync()=>Db.DisposeAsync();
    }
}
