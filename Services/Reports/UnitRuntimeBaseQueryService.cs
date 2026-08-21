using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Data.Sqlite;

namespace Rah_Negar.Services.Reports;

/// <summary>
/// سرویس خواندن مقادیر پایه ساعات کارکرد واحدها از جدول unit_runtime_base.
/// این مقادیر در زمان راه‌اندازی اولیه پروفایل توسط کاربر ثبت می‌شوند.
/// </summary>
public static class UnitRuntimeBaseQueryService
{
    /// <summary>
    /// مقدار پایه Runtime Hours را برای هر واحد می‌خواند.
    /// کلید دیکشنری نام واحد مثل U1, U2 است.
    /// </summary>
    public static Dictionary<string, double> LoadBaseRuntimeHours(SqliteConnection conn)
    {
        return LoadBaseValues(conn, "base_runtime_hours");
    }

    /// <summary>
    /// مقدار پایه Runtime After OH را برای هر واحد می‌خواند.
    /// کلید دیکشنری نام واحد مثل U1, U2 است.
    /// </summary>
    public static Dictionary<string, double> LoadBaseRuntimeAfterOHHours(SqliteConnection conn)
    {
        return LoadBaseValues(conn, "base_runtime_after_oh_hours");
    }

    /// <summary>
    /// مقادیر پایه یک ستون مشخص را از جدول unit_runtime_base می‌خواند.
    /// </summary>
    private static Dictionary<string, double> LoadBaseValues(SqliteConnection conn, string columnName)
    {
        Dictionary<string, double> result = [];

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText =
            $"""
            SELECT unit_no, {columnName}
            FROM unit_runtime_base
            ORDER BY unit_no;
            """;

        using SqliteDataReader reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            int unitNo = Convert.ToInt32(reader["unit_no"]);
            double value = reader[columnName] == DBNull.Value
                ? 0
                : Convert.ToDouble(reader[columnName]);

            result[$"U{unitNo}"] = value;
        }

        return result;
    }
}