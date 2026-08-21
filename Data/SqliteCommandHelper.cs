using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Data.Sqlite;

namespace Rah_Negar.Data;

/// <summary>
/// اجرای دستورات پایه SQLite به‌صورت متمرکز
/// </summary>
public static class SqliteCommandHelper
{
    public static SqliteParameter Param(string name, object? value)
    {
        return new SqliteParameter(name, value ?? DBNull.Value);
    }

    public static int ExecuteNonQuery(
        SqliteConnection connection,
        string sql,
        IEnumerable<SqliteParameter>? parameters = null,
        SqliteTransaction? transaction = null)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        if (transaction != null)
            cmd.Transaction = transaction;

        if (parameters != null)
        {
            foreach (var p in parameters)
                cmd.Parameters.Add(p);
        }

        return cmd.ExecuteNonQuery();
    }

    public static object? ExecuteScalar(
        SqliteConnection connection,
        string sql,
        IEnumerable<SqliteParameter>? parameters = null,
        SqliteTransaction? transaction = null)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        if (transaction != null)
            cmd.Transaction = transaction;

        if (parameters != null)
        {
            foreach (var p in parameters)
                cmd.Parameters.Add(p);
        }

        return cmd.ExecuteScalar();
    }
}