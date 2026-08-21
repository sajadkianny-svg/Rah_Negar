using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Core;

/// <summary>
/// قرارداد ساختار اختصاصی tbl_data برای هر ایستگاه
/// </summary>
public interface IStationDataSchema
{
    /// <summary>
    /// نام ایستگاه مربوط به این ساختار
    /// </summary>
    string StationName { get; }

    /// <summary>
    /// SQL ساخت جدول tbl_data برای ایستگاه
    /// </summary>
    string GetCreateTableSql();

    /// <summary>
    /// SQLهای ایندکس tbl_data برای ایستگاه
    /// </summary>
    List<string> GetIndexSqlList();
}