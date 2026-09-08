using Microsoft.Data.Sqlite;
using Rah_Negar.Core;
using Rah_Negar.Data;
using Rah_Negar.Models;
using Rah_Negar.Services.Reports;

namespace Rah_Negar.Services;

/// <summary>
/// Canonical record persistence for all user-defined profiles. SQL is built
/// only from the fixed product parameter catalog and the persisted unit count.
/// </summary>
public static class GenericRecordPersistenceService
{
    public static List<DailyDataRowModel> LoadDailyData(
        SqliteConnection conn, CanonicalProfileDefinition definition, long dateRep)
    {
        ArgumentNullException.ThrowIfNull(conn);
        ArgumentNullException.ThrowIfNull(definition);

        List<string> columns = DataColumns(definition);
        using SqliteCommand command = conn.CreateCommand();
        command.CommandText = $"SELECT {string.Join(',', columns)} FROM tbl_data WHERE date_rep=$date ORDER BY time_rep;";
        command.Parameters.AddWithValue("$date", dateRep);

        using SqliteDataReader reader = command.ExecuteReader();
        List<DailyDataRowModel> rows = [];
        while (reader.Read())
        {
            DailyDataRowModel row = new()
            {
                TimeRep = reader["time_rep"]?.ToString() ?? string.Empty,
                InP = Number(reader, "in_p"),
                OutP = Number(reader, "out_p"),
                Rec = Number(reader, "rec"),
                Flow = Number(reader, "flow"),
                InT = Number(reader, "in_t"),
                OutT = Number(reader, "out_t"),
                AmbT = Number(reader, "amb_t"),
                Ratio = Number(reader, "ratio")
            };
            if (definition.HasLinePressureColumns)
            {
                row.LineFP = Number(reader, "line_f_p");
                row.Line40P = Number(reader, "line40_p");
                row.Line30P = Number(reader, "line30_p");
            }
            for (int unit = 1; unit <= definition.UnitCount; unit++)
            {
                SetUnit(row, unit, reader[$"u{unit}_st"]?.ToString() ?? string.Empty,
                    Convert.ToInt32(reader[$"u{unit}_rpm"]));
            }
            rows.Add(row);
        }
        return rows;
    }

    public static void InsertDailyDataOnly(
        SqliteConnection conn, SqliteTransaction tx, CanonicalProfileDefinition definition,
        DailyDataSaveModel model)
    {
        ArgumentNullException.ThrowIfNull(conn);
        ArgumentNullException.ThrowIfNull(tx);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(model);
        if (model.Rows.Count != 12)
            throw new InvalidOperationException("هر روز باید دقیقاً ۱۲ رکورد اصلی داشته باشد.");

        MonthlyLockService.EnsureDateIsEditable(conn, tx, model.DateRep);
        using (SqliteCommand delete = conn.CreateCommand())
        {
            delete.Transaction = tx;
            delete.CommandText = "DELETE FROM tbl_data WHERE date_rep=$date;";
            delete.Parameters.AddWithValue("$date", model.DateRep);
            delete.ExecuteNonQuery();
        }

        List<string> columns = DataColumns(definition);
        string parameters = string.Join(',', columns.Select(x => "$" + x));
        string sql = $"INSERT INTO tbl_data ({string.Join(',', columns)}) VALUES ({parameters});";
        foreach (DailyDataRowModel row in model.Rows)
        {
            using SqliteCommand insert = conn.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = sql;
            foreach (string column in columns)
                insert.Parameters.AddWithValue("$" + column, Value(row, column, model.DateRep));
            insert.ExecuteNonQuery();
        }
    }

    public static List<string> DataColumns(CanonicalProfileDefinition definition)
    {
        List<string> columns = ["date_rep", "time_rep", "in_p", "out_p"];
        if (definition.HasLinePressureColumns)
            columns.AddRange(["line_f_p", "line40_p", "line30_p"]);
        for (int unit = 1; unit <= definition.UnitCount; unit++)
            columns.AddRange([$"u{unit}_st", $"u{unit}_rpm"]);
        columns.AddRange(["rec", "flow", "in_t", "out_t", "amb_t", "ratio"]);
        return columns;
    }

    private static object Value(DailyDataRowModel row, string column, long dateRep) => column switch
    {
        "date_rep" => dateRep,
        "time_rep" => row.TimeRep,
        "in_p" => row.InP,
        "out_p" => row.OutP,
        "line_f_p" => row.LineFP,
        "line40_p" => row.Line40P,
        "line30_p" => row.Line30P,
        "rec" => row.Rec,
        "flow" => row.Flow,
        "in_t" => row.InT,
        "out_t" => row.OutT,
        "amb_t" => row.AmbT,
        "ratio" => row.Ratio,
        _ when column.StartsWith('u') && column.EndsWith("_st") => GetUnit(row, column, false),
        _ when column.StartsWith('u') && column.EndsWith("_rpm") => GetUnit(row, column, true),
        _ => throw new InvalidOperationException($"پارامتر پشتیبانی نمی‌شود: {column}")
    };

    private static object GetUnit(DailyDataRowModel row, string column, bool rpm)
    {
        int unit = int.Parse(column.AsSpan(1, column.IndexOf('_') - 1));
        return unit switch
        {
            1 => rpm ? row.U1Rpm : row.U1St,
            2 => rpm ? row.U2Rpm : row.U2St,
            3 => rpm ? row.U3Rpm : row.U3St,
            4 => rpm ? row.U4Rpm : row.U4St,
            5 => rpm ? row.U5Rpm : row.U5St,
            _ => throw new ArgumentOutOfRangeException(nameof(column))
        };
    }

    private static double Number(SqliteDataReader reader, string name) =>
        reader[name] == DBNull.Value ? 0 : Convert.ToDouble(reader[name]);

    private static void SetUnit(DailyDataRowModel row, int unit, string status, int rpm)
    {
        switch (unit)
        {
            case 1: row.U1St = status; row.U1Rpm = rpm; break;
            case 2: row.U2St = status; row.U2Rpm = rpm; break;
            case 3: row.U3St = status; row.U3Rpm = rpm; break;
            case 4: row.U4St = status; row.U4Rpm = rpm; break;
            case 5: row.U5St = status; row.U5Rpm = rpm; break;
        }
    }
}
