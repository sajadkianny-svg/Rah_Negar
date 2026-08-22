using Microsoft.Data.Sqlite;
using Rah_Negar.Infrastructure.Database;
using Rah_Negar.Infrastructure.Database.Checksums;
using Rah_Negar.Infrastructure.Database.Migrations;
using Rah_Negar.Infrastructure.Database.Migrations.Drafts;
using Rah_Negar.Tests.Database;

namespace Rah_Negar.Tests.Event;

public sealed class EventTargetSchemaTests
{
    private const string EventId = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
    private const string AuditId = "01ARZ3NDEKTSV4RRFFQ69G5FAW";
    private const string ActorId = "11111111-1111-1111-1111-111111111111";

    [Fact]
    public async Task Draft_migration_creates_target_schema_without_touching_legacy_table()
    {
        await using TemporarySqliteDatabase database = TemporarySqliteDatabase.Create();
        await CreatePrerequisitesAsync(database, includeLegacyTable: true);
        await RunDraftAsync(database);

        Assert.True(await ObjectExistsAsync(database, "table", "Events"));
        Assert.True(await ObjectExistsAsync(database, "table", "EventAudit"));
        Assert.True(await ObjectExistsAsync(database, "table", "tbl_events"));
        Assert.True(await ObjectExistsAsync(database, "index", "UX_Events_ActiveUnitTimestamp"));
        Assert.True(await ObjectExistsAsync(database, "trigger", "TR_EventAudit_NoUpdate"));
    }

    [Fact]
    public async Task Events_constraints_reject_unknown_type_duplicate_and_wrong_unit()
    {
        await using TemporarySqliteDatabase database = TemporarySqliteDatabase.Create();
        await CreatePrerequisitesAsync(database);
        await RunDraftAsync(database);
        await InsertEventAsync(database, EventId, "START", "U1", 144_000_060);

        await Assert.ThrowsAsync<SqliteException>(() =>
            InsertEventAsync(database, "01ARZ3NDEKTSV4RRFFQ69G5FAX", "STOP", "U1", 144_001_500));
        await Assert.ThrowsAsync<SqliteException>(() =>
            InsertEventAsync(database, "01ARZ3NDEKTSV4RRFFQ69G5FAY", "NSD", "U1", 144_000_060));
        await Assert.ThrowsAsync<SqliteException>(() =>
            InsertEventAsync(database, "01ARZ3NDEKTSV4RRFFQ69G5FAZ", "START", "UNKNOWN", 144_002_940));
    }

    [Fact]
    public async Task Audit_shape_and_append_only_constraints_are_enforced()
    {
        await using TemporarySqliteDatabase database = TemporarySqliteDatabase.Create();
        await CreatePrerequisitesAsync(database);
        await RunDraftAsync(database);
        await InsertEventAsync(database, EventId, "START", "U1", 144_000_060);
        await InsertAuditAsync(database, AuditId, "ADD", null, "{}");

        await Assert.ThrowsAsync<SqliteException>(() =>
            InsertAuditAsync(database, "01ARZ3NDEKTSV4RRFFQ69G5FAX", "DELETE", null, "{}"));

        await using SqliteConnection connection = await database.Factory.OpenConnectionAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE EventAudit SET Reason='changed' WHERE AuditId=$id;";
        command.Parameters.AddWithValue("$id", AuditId);
        await Assert.ThrowsAsync<SqliteException>(() => command.ExecuteNonQueryAsync());
    }

    private static async Task RunDraftAsync(TemporarySqliteDatabase database)
    {
        var checksum = new Sha256ChecksumService();
        var runner = new MigrationRunner(
            new SqliteTransactionManager(database.Factory),
            new MigrationChecksumValidator(checksum));
        await runner.RunPendingAsync(UnifiedTargetMigrationChain.Create(checksum));
        await using SqliteConnection connection = await database.Factory.OpenConnectionAsync();
        await using SqliteCommand seed = connection.CreateCommand();
        seed.CommandText = """
            INSERT INTO Stations VALUES ('RASHT','Rasht','2026-08-22T00:00:00Z',1);
            INSERT INTO Units VALUES ('RASHT','U1',1,'U1',1,1);
            INSERT INTO SecurityShiftProfiles VALUES
              ('11111111-1111-1111-1111-111111111111','RASHT',1,'Shift 1','First','Last','1001','1001',1,
               '2026-08-22T00:00:00Z','2026-08-22T00:00:00Z',1);
            """;
        await seed.ExecuteNonQueryAsync();
    }

    private static async Task CreatePrerequisitesAsync(
        TemporarySqliteDatabase database,
        bool includeLegacyTable = false)
    {
        if (!includeLegacyTable) return;
        await using SqliteConnection connection = await database.Factory.OpenConnectionAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE tbl_events (id INTEGER PRIMARY KEY, event_type TEXT); INSERT INTO tbl_events VALUES (1, 'legacy');";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertEventAsync(
        TemporarySqliteDatabase database,
        string eventId,
        string eventType,
        string unitId,
        long eventDateTime)
    {
        await using SqliteConnection connection = await database.Factory.OpenConnectionAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Events (
                EventId, StationId, UnitId, EventType, EventDate, EventTime,
                EventDateTime, CreatedAt, CreatedByShiftProfileId)
            VALUES ($id, 'RASHT', $unit, $type, 14050531, 60, $dateTime,
                '2026-08-22T00:00:00.0000000Z', $actor);
            """;
        command.Parameters.AddWithValue("$id", eventId);
        command.Parameters.AddWithValue("$unit", unitId);
        command.Parameters.AddWithValue("$type", eventType);
        command.Parameters.AddWithValue("$dateTime", eventDateTime);
        command.Parameters.AddWithValue("$actor", ActorId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertAuditAsync(
        TemporarySqliteDatabase database,
        string auditId,
        string action,
        string? oldValue,
        string? newValue)
    {
        await using SqliteConnection connection = await database.Factory.OpenConnectionAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO EventAudit (
                AuditId, EventId, ActionType, OldValue, NewValue,
                ActorShiftProfileId, TimestampUtc, Reason, CorrelationId)
            VALUES ($auditId, $eventId, $action, $old, $new, $actor,
                '2026-08-22T00:00:00.0000000Z', 'test', 'correlation-1');
            """;
        command.Parameters.AddWithValue("$auditId", auditId);
        command.Parameters.AddWithValue("$eventId", EventId);
        command.Parameters.AddWithValue("$action", action);
        command.Parameters.AddWithValue("$old", (object?)oldValue ?? DBNull.Value);
        command.Parameters.AddWithValue("$new", (object?)newValue ?? DBNull.Value);
        command.Parameters.AddWithValue("$actor", ActorId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> ObjectExistsAsync(
        TemporarySqliteDatabase database,
        string type,
        string name)
    {
        await using SqliteConnection connection = await database.Factory.OpenConnectionAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type=$type AND name=$name;";
        command.Parameters.AddWithValue("$type", type);
        command.Parameters.AddWithValue("$name", name);
        return (long)(await command.ExecuteScalarAsync())! == 1;
    }
}
