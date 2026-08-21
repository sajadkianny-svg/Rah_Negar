using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



using Microsoft.Data.Sqlite;
using Rah_Negar.Data;

namespace Rah_Negar.Core;

/// <summary>
/// سرویس ساخت جداول و ایندکس‌های دیتابیس بر اساس پروفایل ایستگاه.
/// این کلاس تنها محل اجرای SQLهای ساخت Schema است.
/// </summary>
public static class StationSchemaBuilderService
{
    /// <summary>
    /// جداول اختصاصی و مشترک دیتابیس را برای پروفایل انتخاب‌شده می‌سازد.
    /// </summary>
    public static void Build(IStationProfile profile, SqliteConnection conn, SqliteTransaction tx)
    {
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));

        IStationDataSchema stationSchema = profile.GetDataSchema();

        SqliteCommandHelper.ExecuteNonQuery(conn, stationSchema.GetCreateTableSql(), transaction: tx);

        SqliteCommandHelper.ExecuteNonQuery(conn, CommonDataSchema.GetCreateTblUniqueSql(), transaction: tx);
        SqliteCommandHelper.ExecuteNonQuery(conn, CommonDataSchema.GetCreateTblEventsSql(), transaction: tx);

        foreach (string sql in stationSchema.GetIndexSqlList())
            SqliteCommandHelper.ExecuteNonQuery(conn, sql, transaction: tx);

        foreach (string sql in CommonDataSchema.GetCommonIndexSqlList())
            SqliteCommandHelper.ExecuteNonQuery(conn, sql, transaction: tx);
    }
}