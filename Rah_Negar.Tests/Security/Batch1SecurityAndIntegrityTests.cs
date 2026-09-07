using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Rah_Negar.Core.Event;
using Rah_Negar.Core.Reports;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Models;
using Rah_Negar.Models.Reports;
using Rah_Negar.Services;
using Rah_Negar.Services.Reports;
using Rah_Negar.UI.Forms;
using Rah_Negar.Tests.Database;
using Rah_Negar.Infrastructure.Database.Readiness;

namespace Rah_Negar.Tests.Security;

public sealed class Batch1SecurityAndIntegrityTests
{
    [Fact]
    public void Production_assembly_contains_no_seed_or_deterministic_recovery_entry_point()
    {
        Assembly assembly = typeof(BackupEncryptionService).Assembly;
        Assert.Null(assembly.GetType("Rah_Negar.Services.Reports.TestDataSeederService"));
        Assert.Null(assembly.GetType("Rah_Negar.Services.RecoveryService"));
        Assert.Null(typeof(FrmRecords).GetMethod("button1_Click", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.Null(typeof(FrmRecords).GetMethod("button11_Click", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.Null(typeof(FrmSettings).GetMethod("ConfirmLoginPassword", BindingFlags.Instance | BindingFlags.NonPublic));
        MethodInfo esdSettings = typeof(AppSettingsService).GetMethod("SaveNsdRuntimeSettings")!;
        Assert.Contains(esdSettings.GetParameters(), parameter =>
            parameter.ParameterType == typeof(ManagementAuthorizationProof));
    }

    [Fact]
    public void Backup_format_is_versioned_authenticated_and_rejects_tampering()
    {
        string directory = CreateTempDirectory();
        try
        {
            string source = Path.Combine(directory, "source.db");
            string backup = Path.Combine(directory, "backup.rngbak");
            string destination = Path.Combine(directory, "destination.db");
            File.WriteAllBytes(source, [1, 2, 3, 4, 5]);
            var custody = new FixedBackupKeyCustody(RandomNumberGenerator.GetBytes(32));

            BackupEncryptionService.EncryptFile(source, backup, custody);
            Assert.Equal("RNBK", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(backup)[..4]));
            BackupEncryptionService.DecryptFile(backup, destination, custody);
            Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(destination));

            byte[] tampered = File.ReadAllBytes(backup);
            tampered[^1] ^= 0x55;
            File.WriteAllBytes(backup, tampered);
            File.WriteAllText(destination, "untouched");
            Assert.Throws<InvalidDataException>(() =>
                BackupEncryptionService.DecryptFile(backup, destination, custody));
            Assert.Equal("untouched", File.ReadAllText(destination));
        }
        finally { DeleteTempDirectory(directory); }
    }

    [Fact]
    public async Task Legacy_event_authority_rejects_duplicate_minute_invalid_transition_and_stop_type()
    {
        await using TemporarySqliteDatabase database = TemporarySqliteDatabase.Create();
        await using SqliteConnection connection = await database.Factory.OpenConnectionAsync();
        await ExecuteAsync(connection, """
            CREATE TABLE unit_runtime_base(unit_no INTEGER PRIMARY KEY, initial_is_running INTEGER NOT NULL);
            INSERT INTO unit_runtime_base VALUES(1,0);
            CREATE TABLE tbl_events(date_rep INTEGER NOT NULL, unit TEXT NOT NULL, event_type TEXT NOT NULL, event_time TEXT NOT NULL);
            """);
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

        DailyEventRowModel[] duplicate =
        [
            Event(14050101, "U1", "START", "01:00"),
            Event(14050101, "U1", "OH", "01:00")
        ];
        Assert.Throws<InvalidOperationException>(() =>
            LegacyEventAuthorityValidator.ValidateReplacement(connection, transaction, 14050101, duplicate));

        DailyEventRowModel[] invalidTransition = [Event(14050101, "U1", "NSD", "02:00")];
        Assert.Throws<InvalidOperationException>(() =>
            LegacyEventAuthorityValidator.ValidateReplacement(connection, transaction, 14050101, invalidTransition));

        DailyEventRowModel[] genericStop = [Event(14050101, "U1", "STOP", "03:00")];
        Assert.Throws<InvalidOperationException>(() =>
            LegacyEventAuthorityValidator.ValidateReplacement(connection, transaction, 14050101, genericStop));
        DailyEventRowModel nonMinute = Event(14050101, "U1", "START", "04:00:30");
        Assert.Throws<InvalidOperationException>(() =>
            LegacyEventAuthorityValidator.ValidateReplacement(connection, transaction, 14050101, [nonMinute]));
        await transaction.RollbackAsync();
    }

    [Fact]
    public void Active_runtime_calculator_rejects_invalid_chain_before_projection()
    {
        DateTime nsd = PersianDate(1405, 1, 1, 1, 0);
        var profile = new ReportStationProfile { StationName = "Rasht Station", Units = ["U1"] };
        var events = new List<EventLogItem>
        {
            new() { Unit = "U1", EventType = "NSD", EventDate = 14050101, EventTime = "01:00", EventDateTime = nsd }
        };
        var states = new Dictionary<string, UnitInitialEventState>
        {
            ["U1"] = new() { Unit = "U1", IsRunningAtPeriodStart = false }
        };

        Assert.Throws<InvalidDataException>(() => EventRuntimeCalculationService.Calculate(
            profile, events, 14050101, 14050101,
            new Dictionary<string, double>(), new Dictionary<string, double>(), states, false, 0));
    }

    [Fact]
    public void Recovery_required_marker_blocks_until_verified_recovery_clears_it()
    {
        string directory = CreateTempDirectory();
        string databasePath = Path.Combine(directory, "RahNegar.db");
        try
        {
            RecoveryRequiredStateStore.MarkRequired(databasePath, "failure-injection");
            Assert.True(RecoveryRequiredStateStore.IsRequired(databasePath));

            RecoveryRequiredStateStore.ClearAfterVerifiedRecovery(databasePath);

            Assert.False(RecoveryRequiredStateStore.IsRequired(databasePath));
        }
        finally { DeleteTempDirectory(directory); }
    }

    private static DailyEventRowModel Event(long date, string unit, string type, string time) => new()
    {
        DateRep = date, Unit = unit, EventType = type, EventTime = time
    };

    private static DateTime PersianDate(int year, int month, int day, int hour, int minute) =>
        new System.Globalization.PersianCalendar().ToDateTime(year, month, day, hour, minute, 0, 0);

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "RahNegar-Batch1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempDirectory(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }

    private sealed class FixedBackupKeyCustody(byte[] key) : IBackupKeyCustody
    {
        public byte[] LoadOrCreateKey() => key.ToArray();
    }
}
