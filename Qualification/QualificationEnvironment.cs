using Microsoft.Data.Sqlite;
using Rah_Negar.Core;
using Rah_Negar.Data;
using Rah_Negar.Foundation.Application.Security;
using Rah_Negar.Utils;
using Rah_Negar.Foundation.Application.Provisioning;
using Rah_Negar.Infrastructure.ApplicationData;
using Rah_Negar.Infrastructure.Database.Readiness;

namespace Rah_Negar.Qualification;

public static class QualificationEnvironment
{
    public const string LoginPassword = "Qualification-9.4C!";
    public const long DataStartDate = 14050101;
    private const string FixedSalt = "cXVhbGlmaWNhdGlvbi05LjRj";

    public static void Prepare(string rootDirectory)
    {
        string root = Path.GetFullPath(rootDirectory);
        if (Path.GetFileName(root).Equals("Data", StringComparison.OrdinalIgnoreCase) ||
            root.Contains("\\Data\\", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Qualification output must be outside the production Data directory.");
        Directory.CreateDirectory(root);
        CreateScenario(Path.Combine(root, "Rasht", "db.sys"), StationType.Rasht, 3);
        CreateScenario(Path.Combine(root, "Ramsar", "db.sys"), StationType.Ramsar, 4);
    }

    public static void PrepareGeneric(string rootDirectory, int unitCount)
    {
        if (!TargetStationProfileRules.IsUnitCountSupported(unitCount))
            throw new ArgumentOutOfRangeException(nameof(unitCount), "Generic profile unit count must be 3 through 5.");

        string root = Path.GetFullPath(rootDirectory);
        if (Path.GetFileName(root).Equals("Data", StringComparison.OrdinalIgnoreCase) ||
            root.Contains("\\Data\\", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Qualification output must be outside the production Data directory.");

        string profileName = GenericProfileIdentity.Create(unitCount);
        string scenarioRoot = Path.Combine(root, $"Generic-{unitCount}");
        CreateGenericScenario(Path.Combine(scenarioRoot, "db.sys"), profileName, unitCount);
        File.WriteAllText(Path.Combine(scenarioRoot, "generic-profile.json"),
            $"{{\"profileName\":\"{profileName}\",\"unitCount\":{unitCount},\"productionDataTouched\":false}}");
    }

    private static void CreateScenario(string path, StationType station, int unitCount)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path)) File.Delete(path);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open();
        using var transaction = connection.BeginTransaction();
        Execute(connection, transaction, "PRAGMA foreign_keys=ON;");
        Execute(connection, transaction, BaseSchema(station == StationType.Rasht
            ? new RashtDataSchema()
            : new RamsarDataSchema()));
        string stationId = station == StationType.Rasht ? "station-rasht" : "station-ramsar";
        string stationName = station == StationType.Rasht ? "Rasht Station" : "Ramsar Station";
        string hash = PasswordHelper.HashPassword(LoginPassword, FixedSalt);
        Execute(connection, transaction, $"""
            INSERT INTO app_settings(is_initialized,station_type,station_name,user_reset_password_hash,user_reset_password_salt,created_at,theme_index,esd_extra_runtime_enabled,esd_extra_runtime_hours,data_start_date)
            VALUES (1,'{station}', '{stationName}', '{hash}', '{FixedSalt}', '2026-09-01 00:00:00',0,1,1.5,{DataStartDate});
            INSERT INTO Stations VALUES ('{stationId}','{stationName}','2026-09-01T00:00:00Z',1);
            INSERT INTO SecurityShiftProfiles VALUES ('qualification-{station.ToString().ToLowerInvariant()}','{stationId}',1,'Qualification Shift','Qualification','Operator','Q-9.4C','Q-9.4C',1,'2026-09-01T00:00:00Z','2026-09-01T00:00:00Z',1);
            INSERT INTO SecurityShiftProfileCredentials VALUES ('qualification-{station.ToString().ToLowerInvariant()}',1,'qualification-only','fixed-scenario',X'01020304',X'05060708',1,'2026-09-01T00:00:00Z',NULL);
            """);
        SeedManagementCredential(connection, transaction);
        for (int unit = 1; unit <= unitCount; unit++)
        {
            Execute(connection, transaction, $"INSERT INTO Units VALUES ('{stationId}','{stationId}-unit-{unit}',{unit},'Unit {unit}',1,1);");
            Execute(connection, transaction, $"INSERT INTO unit_runtime_base(unit_no,base_runtime_hours,base_runtime_after_oh_hours,initial_is_running,initial_status) VALUES ({unit},{100 + unit},{20 + unit},0,'OFF');");
        }
        for (int day = 1; day <= 2; day++)
        {
            long date = DataStartDate + day - 1;
            Execute(connection, transaction, $"INSERT INTO tbl_unique(date_rep,ir_f,turbine_fuel,turbine_flow,non_turbine_flow,vent) VALUES ({date},1.1,100,200,50,2);");
            for (int hour = 1; hour <= 23; hour += 2)
            {
                string values = station == StationType.Rasht
                    ? $"{date},'{hour:00}:00',10,20,5,4,3,'OFF',100,'OFF',101,'OFF',102,30,40,50,60,25,1.2"
                    : $"{date},'{hour:00}:00',10,20,'OFF',100,'OFF',101,'OFF',102,'OFF',103,30,40,50,60,25,1.2";
                Execute(connection, transaction, $"INSERT INTO tbl_data VALUES (NULL,{values});");
            }
            for (int unit = 1; unit <= unitCount; unit++)
            {
                string uid = $"{stationId}-unit-{unit}";
                Execute(connection, transaction, $"INSERT INTO tbl_events(date_rep,unit,event_type,event_time,remark) VALUES ({date},'{uid}','START','01:00','qualification');");
                Execute(connection, transaction, $"INSERT INTO tbl_events(date_rep,unit,event_type,event_time,remark) VALUES ({date},'{uid}','NSD','02:00','qualification');");
            }
        }
        transaction.Commit();
    }

    private static void CreateGenericScenario(string path, string profileName, int unitCount)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path)) File.Delete(path);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open();
        using var transaction = connection.BeginTransaction();
        CanonicalProfileDefinition definition = CanonicalProfileDefinition.Create(
            $"Synthetic Qualification Station {unitCount}", unitCount);
        Execute(connection, transaction, BaseSchema(new GenericDataSchema(definition)));
        string hash = PasswordHelper.HashPassword(LoginPassword, FixedSalt);
        Execute(connection, transaction, $"""
            INSERT INTO app_settings(is_initialized,station_type,station_name,user_reset_password_hash,user_reset_password_salt,created_at,theme_index,esd_extra_runtime_enabled,esd_extra_runtime_hours,data_start_date,profile_id,profile_revision,unit_count,profile_optional_parameters)
            VALUES (1,'Custom','{definition.StationName}', '{hash}', '{FixedSalt}', '2026-09-01 00:00:00',0,1,1.5,{DataStartDate},'{definition.ProfileId}',{definition.Revision},{definition.UnitCount},'{string.Join(',', definition.OptionalParameters)}');
            """);
        SeedManagementCredential(connection, transaction);
        for (int unit = 1; unit <= unitCount; unit++)
        {
            Execute(connection, transaction, $"INSERT INTO unit_runtime_base(unit_no,base_runtime_hours,base_runtime_after_oh_hours,initial_is_running,initial_status) VALUES ({unit},{100 + unit},{20 + unit},0,'OFF');");
        }
        transaction.Commit();
    }

    public static void StageRecoveryRequired(string rootDirectory)
    {
        string root = ValidateQualificationRoot(rootDirectory);
        string databasePath = Path.Combine(root, "Data", "db.sys");
        if (!File.Exists(databasePath))
            throw new FileNotFoundException("Qualification database was not found.", databasePath);

        string? previous = Environment.GetEnvironmentVariable(ApplicationDataPaths.QualificationRootEnvironmentVariable);
        Environment.SetEnvironmentVariable(ApplicationDataPaths.QualificationRootEnvironmentVariable, root);
        try { RecoveryRequiredStateStore.MarkRequired(databasePath, "Qualification-only staged recovery state"); }
        finally { Environment.SetEnvironmentVariable(ApplicationDataPaths.QualificationRootEnvironmentVariable, previous); }
    }

    public static void ClearRecoveryRequired(string rootDirectory)
    {
        string root = ValidateQualificationRoot(rootDirectory);
        string databasePath = Path.Combine(root, "Data", "db.sys");
        string? previous = Environment.GetEnvironmentVariable(ApplicationDataPaths.QualificationRootEnvironmentVariable);
        Environment.SetEnvironmentVariable(ApplicationDataPaths.QualificationRootEnvironmentVariable, root);
        try { RecoveryRequiredStateStore.ClearAfterVerifiedRecovery(databasePath); }
        finally { Environment.SetEnvironmentVariable(ApplicationDataPaths.QualificationRootEnvironmentVariable, previous); }
    }

    private static string ValidateQualificationRoot(string rootDirectory)
    {
        string root = Path.GetFullPath(rootDirectory);
        if (!root.Contains("\\Qualification\\", StringComparison.OrdinalIgnoreCase) &&
            !root.Contains("/Qualification/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Qualification state must remain under the Qualification directory.");
        return root;
    }

    private static void SeedManagementCredential(SqliteConnection connection, SqliteTransaction transaction)
    {
        string? secret = Environment.GetEnvironmentVariable(
            QualificationManagementAccess.ManagementSecretEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(secret) || secret.Length > 256) return;

        byte[] salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        byte[] verifier = Pbkdf2TargetPasswordVerifier.CreateVerifier(secret, salt);
        try
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO SecurityManagementCredentials
                 (SingletonId,CredentialVersion,KdfAlgorithm,KdfParameters,Salt,PasswordVerifier,
                  IsCurrent,IsActive,CreatedAtUtc,UpdatedAtUtc,RetiredAtUtc)
                VALUES (1,1,$algorithm,$parameters,$salt,$verifier,1,1,$created,$updated,NULL);
                """;
            string now = DateTimeOffset.UtcNow.ToString("O");
            command.Parameters.AddWithValue("$algorithm", Pbkdf2TargetPasswordVerifier.Algorithm);
            command.Parameters.AddWithValue("$parameters", Pbkdf2TargetPasswordVerifier.Parameters);
            command.Parameters.AddWithValue("$salt", salt);
            command.Parameters.AddWithValue("$verifier", verifier);
            command.Parameters.AddWithValue("$created", now);
            command.Parameters.AddWithValue("$updated", now);
            command.ExecuteNonQuery();
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(salt);
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(verifier);
        }
    }

    private static string BaseSchema(IStationDataSchema stationSchema)
    {
        string stationTable = stationSchema.GetCreateTableSql();
        return $"""
        CREATE TABLE app_settings(id INTEGER PRIMARY KEY AUTOINCREMENT,is_initialized INTEGER NOT NULL,station_type TEXT NOT NULL,station_name TEXT NOT NULL,user_reset_password_hash TEXT NOT NULL,user_reset_password_salt TEXT NOT NULL,created_at TEXT NOT NULL,last_backup_at TEXT,password_changed_at TEXT,theme_index INTEGER NOT NULL DEFAULT 0,esd_extra_runtime_enabled INTEGER NOT NULL DEFAULT 0,esd_extra_runtime_hours REAL NOT NULL DEFAULT 0,data_start_date INTEGER NOT NULL,profile_id TEXT NOT NULL DEFAULT '',profile_revision INTEGER NOT NULL DEFAULT 0,unit_count INTEGER NOT NULL DEFAULT 0,profile_optional_parameters TEXT NOT NULL DEFAULT '');
        CREATE TABLE unit_runtime_base(id INTEGER PRIMARY KEY AUTOINCREMENT,unit_no INTEGER NOT NULL,base_runtime_hours REAL NOT NULL,base_runtime_after_oh_hours REAL NOT NULL,initial_is_running INTEGER NOT NULL,initial_status TEXT NOT NULL);
        CREATE TABLE tbl_unique(id INTEGER PRIMARY KEY AUTOINCREMENT,date_rep INTEGER NOT NULL,ir_f REAL,turbine_fuel REAL,turbine_flow REAL,non_turbine_flow REAL,vent REAL);
        CREATE UNIQUE INDEX idx_tbl_unique_date ON tbl_unique(date_rep);
        CREATE TABLE tbl_events(id INTEGER PRIMARY KEY AUTOINCREMENT,date_rep INTEGER NOT NULL,unit TEXT NOT NULL,event_type TEXT NOT NULL,event_time TEXT NOT NULL,remark TEXT);
        {stationTable}
        CREATE TABLE Stations(StationId TEXT PRIMARY KEY,StationName TEXT NOT NULL,CreatedAtUtc TEXT NOT NULL,Revision INTEGER NOT NULL);
        CREATE TABLE Units(StationId TEXT NOT NULL,UnitId TEXT NOT NULL,UnitNumber INTEGER NOT NULL,UnitName TEXT NOT NULL,IsActive INTEGER NOT NULL,Revision INTEGER NOT NULL,PRIMARY KEY(StationId,UnitId));
        CREATE TABLE SecurityShiftProfiles(ShiftProfileId TEXT PRIMARY KEY,StationId TEXT NOT NULL,ShiftNumber INTEGER NOT NULL,ShiftName TEXT NOT NULL,SupervisorFirstName TEXT NOT NULL,SupervisorLastName TEXT NOT NULL,PersonnelNo TEXT NOT NULL,PersonnelNoNormalized TEXT NOT NULL,IsActive INTEGER NOT NULL,CreatedAtUtc TEXT NOT NULL,UpdatedAtUtc TEXT NOT NULL,Revision INTEGER NOT NULL);
        CREATE TABLE SecurityShiftProfileCredentials(ShiftProfileId TEXT NOT NULL,CredentialVersion INTEGER NOT NULL,KdfAlgorithm TEXT NOT NULL,KdfParameters TEXT NOT NULL,Salt BLOB NOT NULL,PasswordVerifier BLOB NOT NULL,IsCurrent INTEGER NOT NULL,CreatedAtUtc TEXT NOT NULL,RetiredAtUtc TEXT,PRIMARY KEY(ShiftProfileId,CredentialVersion));
        CREATE TABLE SecurityManagementCredentials(SingletonId INTEGER NOT NULL CHECK(SingletonId=1),CredentialVersion INTEGER NOT NULL CHECK(CredentialVersion>0),KdfAlgorithm TEXT NOT NULL,KdfParameters TEXT NOT NULL,Salt BLOB NOT NULL CHECK(length(Salt)>0),PasswordVerifier BLOB NOT NULL CHECK(length(PasswordVerifier)>0),IsCurrent INTEGER NOT NULL CHECK(IsCurrent IN(0,1)),IsActive INTEGER NOT NULL CHECK(IsActive IN(0,1)),CreatedAtUtc TEXT NOT NULL,UpdatedAtUtc TEXT NOT NULL,RetiredAtUtc TEXT,PRIMARY KEY(SingletonId,CredentialVersion));
        CREATE UNIQUE INDEX UX_SecurityManagementCredentials_Current ON SecurityManagementCredentials(SingletonId) WHERE IsCurrent=1;
        CREATE TABLE SecurityAuditEntries(AuditEntryId TEXT PRIMARY KEY NOT NULL,InitiatingShiftProfileId TEXT NOT NULL,Action TEXT NOT NULL,Scope TEXT NOT NULL,AuthorizationType TEXT NOT NULL,ResultCategory TEXT NOT NULL,TimestampUtc TEXT NOT NULL,CorrelationId TEXT NOT NULL,RequestId TEXT NULL);
        CREATE TABLE SecurityAuditMetadata(AuditEntryId TEXT NOT NULL,MetadataKey TEXT NOT NULL,MetadataValue TEXT NOT NULL,PRIMARY KEY(AuditEntryId,MetadataKey),FOREIGN KEY(AuditEntryId) REFERENCES SecurityAuditEntries(AuditEntryId));
        """;
    }

    private static void Execute(SqliteConnection c, SqliteTransaction t, string sql)
    { using var command = c.CreateCommand(); command.Transaction = t; command.CommandText = sql; command.ExecuteNonQuery(); }
}
