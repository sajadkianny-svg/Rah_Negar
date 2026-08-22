using Microsoft.Data.Sqlite;
using Rah_Negar.Core.Event;
using Rah_Negar.Core.Event.Rules;
using Rah_Negar.Foundation.Application.Event;
using Rah_Negar.Foundation.Application.Event.Policies;
using Rah_Negar.Foundation.Application.Transactions;
using Rah_Negar.Foundation.Time;
using Rah_Negar.Infrastructure.Database;
using Rah_Negar.Infrastructure.Database.Checksums;
using Rah_Negar.Infrastructure.Database.Migrations;
using Rah_Negar.Infrastructure.Database.Migrations.Drafts;
using Rah_Negar.Infrastructure.Event;
using Rah_Negar.Tests.Database;

namespace Rah_Negar.Tests.Event;

internal sealed class EventApplicationTestContext : IAsyncDisposable
{
    public static readonly Guid ActorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly TemporarySqliteDatabase _database;
    private readonly TestPolicies _policies = new();
    private readonly QueueIdGenerator _ids = new();

    private EventApplicationTestContext(TemporarySqliteDatabase database)
    {
        _database = database;
    }

    public string DatabasePath => _database.Path;
    public bool Locked { get => _policies.Locked; set => _policies.Locked = value; }
    public long BaselineBoundary { get => _policies.BaselineBoundary; set => _policies.BaselineBoundary = value; }

    public static async Task<EventApplicationTestContext> CreateAsync()
    {
        TemporarySqliteDatabase database = TemporarySqliteDatabase.Create();
        var context = new EventApplicationTestContext(database);
        await context.InitializeAsync();
        return context;
    }

    public EventApplicationService CreateService(IEventAuditRepository? auditRepository = null)
    {
        var eventRepository = new SqliteEventRepository();
        return new EventApplicationService(
            new SqliteTransactionManager(_database.Factory), eventRepository,
            auditRepository ?? new SqliteEventAuditRepository(),
            new EventChainEvaluator(new EventStateTransitionEvaluator()),
            _policies, _policies, _policies, _policies, _ids,
            new PersianEventDateTimeConverter(), new FixedClock());
    }

    public async Task<long> ScalarLongAsync(string sql)
    {
        await using SqliteConnection connection = await _database.Factory.OpenConnectionAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    public async Task<string?> ScalarStringAsync(string sql)
    {
        await using SqliteConnection connection = await _database.Factory.OpenConnectionAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync());
    }

    public ValueTask DisposeAsync() => _database.DisposeAsync();

    private async Task InitializeAsync()
    {
        var checksum = new Sha256ChecksumService();
        var runner = new MigrationRunner(
            new SqliteTransactionManager(_database.Factory),
            new MigrationChecksumValidator(checksum));
        await runner.RunPendingAsync(UnifiedTargetMigrationChain.Create(checksum));
        await using SqliteConnection connection = await _database.Factory.OpenConnectionAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Stations VALUES ('RASHT','Rasht','2026-08-22T00:00:00Z',1);
            INSERT INTO Units VALUES ('RASHT','U1',1,'U1',1,1);
            INSERT INTO Units VALUES ('RASHT','U2',2,'U2',1,1);
            INSERT INTO SecurityShiftProfiles VALUES
              ('11111111-1111-1111-1111-111111111111','RASHT',1,'Shift 1','First','Last','1001','1001',1,
               '2026-08-22T00:00:00Z','2026-08-22T00:00:00Z',1);
            """;
        await command.ExecuteNonQueryAsync();
    }

    private sealed class TestPolicies :
        IEventOwnershipPolicy, IFinalizedPeriodPolicy, IOperatingDayPolicy, IEventBaselineStateProvider
    {
        public bool Locked { get; set; }
        public long BaselineBoundary { get; set; }

        public Task<bool> IsUnitOwnedByStationAsync(
            ITransactionContext context, string stationId, string unitId, int eventDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(stationId == "RASHT" && unitId is "U1" or "U2");

        public Task<bool> IsLockedAsync(
            ITransactionContext context, string stationId, int eventDate,
            CancellationToken cancellationToken = default) => Task.FromResult(Locked);

        public Task<bool> IsEligibleAsync(
            ITransactionContext context, string stationId, int eventDate,
            CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<EventBaseline?> GetBaselineAsync(
            ITransactionContext context, string stationId, string unitId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EventBaseline?>(new EventBaseline(
                EventOperationalState.Stopped, BaselineBoundary, 1));
    }

    private sealed class QueueIdGenerator : IEventIdGenerator
    {
        private int _value;
        public string NewId()
        {
            _value++;
            const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
            int number = _value;
            char[] chars = Enumerable.Repeat('0', 26).ToArray();
            for (int index = 25; number > 0; index--)
            {
                chars[index] = alphabet[number % 32];
                number /= 32;
            }
            return new string(chars);
        }
    }

    private sealed class FixedClock : IClock
    {
        private int _ticks;
        public DateTimeOffset UtcNow => new DateTimeOffset(2026, 8, 22, 0, 0, _ticks++, TimeSpan.Zero);
        public DateTimeOffset LocalNow => UtcNow.ToOffset(TimeSpan.FromHours(3.5));
    }
}
