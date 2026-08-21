using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace Rah_Negar.Models;

/// <summary>
/// مدل ذخیره tbl_data برای Ramsar Station
/// </summary>
public sealed class RamsarDailyDataSaveModel
{
    public long DateRep { get; set; }

    public List<RamsarDailyDataRowModel> Rows { get; set; } = new();
}