using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Data.Sqlite;
using Rah_Negar.Core.Reports;
using Rah_Negar.Models.Reports;

namespace Rah_Negar.Services.Reports;

/// <summary>
/// سرویس خواندن داده خام برای سیستم گزارش‌گیری.
/// این سرویس فقط داده‌ها را از دیتابیس استخراج می‌کند
/// و هیچ‌گونه محاسبه‌ای انجام نمی‌دهد.
/// </summary>
public static class ReportQueryService
{
    /// <summary>
    /// داده‌های tbl_data را بر اساس بازه زمانی و پارامترهای انتخابی می‌خواند.
    /// </summary>
    public static List<Dictionary<string, object>> LoadDataRows(
        SqliteConnection conn,
        ReportRequest request,
        IReadOnlyList<ReportParameterDefinition> parameters)
    {
        List<Dictionary<string, object>> result = [];

        // فقط ستون‌هایی که لازم داریم
        List<string> columns = parameters
            .Where(p => p.DataColumnName != null)
            .Select(p => p.DataColumnName!)
            .Distinct()
            .ToList();

        // ستون‌های پایه
        columns.Insert(0, "time_rep");
        columns.Insert(0, "date_rep");

        string columnList = string.Join(", ", columns);

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText =
            $"""
            SELECT {columnList}
            FROM tbl_data
            WHERE date_rep BETWEEN $from AND $to
            ORDER BY date_rep, time_rep;
            """;

        cmd.Parameters.AddWithValue("$from", request.DateFrom);
        cmd.Parameters.AddWithValue("$to", request.DateTo);

        using SqliteDataReader reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            Dictionary<string, object> row = [];

            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i)
                    ? null!
                    : reader.GetValue(i);
            }

            result.Add(row);
        }

        return result;
    }

    /// <summary>
    /// داده‌های tbl_unique را برای بازه زمانی مشخص می‌خواند.
    /// </summary>
    public static List<Dictionary<string, object>> LoadUniqueRows(
        SqliteConnection conn,
        ReportRequest request,
        IReadOnlyList<ReportParameterDefinition> parameters)
    {
        List<Dictionary<string, object>> result = [];

        List<string> columns = parameters
            .Where(p => p.UniqueColumnName != null)
            .Select(p => p.UniqueColumnName!)
            .Distinct()
            .ToList();

        columns.Insert(0, "date_rep");

        string columnList = string.Join(", ", columns);

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText =
            $"""
            SELECT {columnList}
            FROM tbl_unique
            WHERE date_rep BETWEEN $from AND $to
            ORDER BY date_rep;
            """;

        cmd.Parameters.AddWithValue("$from", request.DateFrom);
        cmd.Parameters.AddWithValue("$to", request.DateTo);

        using SqliteDataReader reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            Dictionary<string, object> row = [];

            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i)
                    ? null!
                    : reader.GetValue(i);
            }

            result.Add(row);
        }

        return result;
    }
}
