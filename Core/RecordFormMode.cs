using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rah_Negar.Core;

/// <summary>
/// حالت‌های مختلف فرم رکورد
/// </summary>
public enum RecordFormMode
{
    Empty = 0,      // فرم خالی
    Pasted = 1,     // داده paste شده
    Editing = 2,    // در حال ویرایش
    Loaded = 3      // داده از دیتابیس لود شده
}