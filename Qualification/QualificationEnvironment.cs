using Microsoft.Data.Sqlite;
using Rah_Negar.Core;
using Rah_Negar.Data;
using Rah_Negar.Utils;

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

    private static void CreateScenario(string path, StationType station, int unitCount)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path)) File.Delete(path);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open();
        using var transaction = connection.BeginTransaction();
        Execute(connection, transaction, "PRAGMA foreign_keys=ON;");
        Execute(connection, transaction, BaseSchema(station));
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

    private static string BaseSchema(StationType station)
    {
        string stationTable = station == StationType.Rasht
            ? new RashtDataSchema().GetCreateTableSql()
            : new RamsarDataSchema().GetCreateTableSql();
        return $"""
        CREATE TABLE app_settings(id INTEGER PRIMARY KEY AUTOINCREMENT,is_initialized INTEGER NOT NULL,station_type TEXT NOT NULL,station_name TEXT NOT NULL,user_reset_password_hash TEXT NOT NULL,user_reset_password_salt TEXT NOT NULL,created_at TEXT NOT NULL,last_backup_at TEXT,password_changed_at TEXT,theme_index INTEGER NOT NULL DEFAULT 0,esd_extra_runtime_enabled INTEGER NOT NULL DEFAULT 0,esd_extra_runtime_hours REAL NOT NULL DEFAULT 0,data_start_date INTEGER NOT NULL);
        CREATE TABLE unit_runtime_base(id INTEGER PRIMARY KEY AUTOINCREMENT,unit_no INTEGER NOT NULL,base_runtime_hours REAL NOT NULL,base_runtime_after_oh_hours REAL NOT NULL,initial_is_running INTEGER NOT NULL,initial_status TEXT NOT NULL);
        CREATE TABLE tbl_unique(id INTEGER PRIMARY KEY AUTOINCREMENT,date_rep INTEGER NOT NULL,ir_f REAL,turbine_fuel REAL,turbine_flow REAL,non_turbine_flow REAL,vent REAL);
        CREATE UNIQUE INDEX idx_tbl_unique_date ON tbl_unique(date_rep);
        CREATE TABLE tbl_events(id INTEGER PRIMARY KEY AUTOINCREMENT,date_rep INTEGER NOT NULL,unit TEXT NOT NULL,event_type TEXT NOT NULL,event_time TEXT NOT NULL,remark TEXT);
        {stationTable}
        CREATE TABLE Stations(StationId TEXT PRIMARY KEY,StationName TEXT NOT NULL,CreatedAtUtc TEXT NOT NULL,Revision INTEGER NOT NULL);
        CREATE TABLE Units(StationId TEXT NOT NULL,UnitId TEXT NOT NULL,UnitNumber INTEGER NOT NULL,UnitName TEXT NOT NULL,IsActive INTEGER NOT NULL,Revision INTEGER NOT NULL,PRIMARY KEY(StationId,UnitId));
        CREATE TABLE SecurityShiftProfiles(ShiftProfileId TEXT PRIMARY KEY,StationId TEXT NOT NULL,ShiftNumber INTEGER NOT NULL,ShiftName TEXT NOT NULL,SupervisorFirstName TEXT NOT NULL,SupervisorLastName TEXT NOT NULL,PersonnelNo TEXT NOT NULL,PersonnelNoNormalized TEXT NOT NULL,IsActive INTEGER NOT NULL,CreatedAtUtc TEXT NOT NULL,UpdatedAtUtc TEXT NOT NULL,Revision INTEGER NOT NULL);
        CREATE TABLE SecurityShiftProfileCredentials(ShiftProfileId TEXT NOT NULL,CredentialVersion INTEGER NOT NULL,KdfAlgorithm TEXT NOT NULL,KdfParameters TEXT NOT NULL,Salt BLOB NOT NULL,PasswordVerifier BLOB NOT NULL,IsCurrent INTEGER NOT NULL,CreatedAtUtc TEXT NOT NULL,RetiredAtUtc TEXT,PRIMARY KEY(ShiftProfileId,CredentialVersion));
        """;
    }

    private static void Execute(SqliteConnection c, SqliteTransaction t, string sql)
    { using var command = c.CreateCommand(); command.Transaction = t; command.CommandText = sql; command.ExecuteNonQuery(); }
}
